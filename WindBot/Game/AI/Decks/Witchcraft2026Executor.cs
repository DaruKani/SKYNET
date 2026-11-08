using System;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    [Deck("Witchcraft2026", "AI_Witchcraft2026", "Test")]
    public partial class Witchcraft2026Executor : DefaultExecutor
    {
        public class CardId
        {
            public const int MadameVerre = 21522601;
            public const int Haine = 84523092;
            public const int Marka = 6071005;
            public const int Edel = 58139997;
            public const int Schmietta = 21744288;
            public const int Seed = 59106048;
            public const int MaidVerre = 22623509;
            public const int Pittore = 95245544;
            public const int AshBlossom = 14558127;
            public const int MaxxC = 23434538;
            public const int Potterie = 59851535;
            public const int Terracottan = 70686400;
            public const int MaidGenni = 7656689;
            public const int Genni = 64756282;
            public const int RainbowBridgeBifrost = 32236916;
            public const int Terraforming = 73628505;
            public const int Walpurgis = 32353566;
            public const int Creation = 57916305;
            public const int Lacrima = 73664385;
            public const int Unveiling = 70226289;
            public const int Bystreet = 83289866;
            public const int SpellPowerMastery = 38943357;
            public const int GazeOfTimaeus = 22283204;
            public const int ConfusionConfession = 35098357;
            public const int Celebration = 6958567;
            public const int Distortion = 69748261;

            public const int MadameRillie = 33475154;
            public const int RedEyesDarkDragoon = 37818794;
            public const int DarkMagicianOfDestruction = 59400890;
            public const int ViceMadame = 9603252;
            public const int Pupils = 69964858;
            public const int CosmicQuasarDragon = 37442336;
            public const int DespianLuluwalilith = 53971455;
            public const int ElfnoteJunePride = 5559570;
            public const int CharmerQuartet = 27519978;
            public const int Selene = 45819647;
            public const int SpLittleKnight = 29301451;
            public const int IpMasquerena = 65741789;
            public const int CrossSheep = 50277355;
        }

        private const int WitchcrafterSetcode = 0x128;
        private bool _spellActivity, _normalUsed, _extraLock, _maxx;
        private bool _awaitingChain;
        private readonly HashSet<string> _used = new HashSet<string>();
        private readonly Dictionary<int, WAction> _prompts = new Dictionary<int, WAction>();
        private IList<ClientCard> _chainOffers;
        private MainPhase _main;
        private WPlan _plan;
        private WAction _accepted;
        private string _planKey;
        private readonly System.Threading.CancellationTokenSource _shutdown =
            new System.Threading.CancellationTokenSource();

        public Witchcraft2026Executor(GameAI ai, Duel duel) : base(ai, duel)
        {
            if (AI.Game != null && AI.Game.Connection != null)
                AI.Game.Connection.Disconnected += error => _shutdown.Cancel();
            AddExecutor(ExecutorType.Activate, CardId.AshBlossom, DefaultAshBlossomAndJoyousSpring);
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, DefaultMaxxC);
            AddExecutor(ExecutorType.Activate, CardId.Distortion, DistortionEffect);
            AddExecutor(ExecutorType.Activate, CardId.RedEyesDarkDragoon, DragoonEffect);
            AddExecutor(ExecutorType.Activate, CardId.Haine, HaineEffect);
            AddExecutor(ExecutorType.Activate, CardId.MadameVerre, VerreEffect);
            AddExecutor(ExecutorType.Activate, CardId.ViceMadame, ViceResponse);
            AddExecutor(ExecutorType.Activate, CardId.Lacrima, QuickResourceAction);
            AddExecutor(ExecutorType.Activate, CardId.MaidGenni, QuickResourceAction);
            foreach (int id in LowIds)
                AddExecutor(ExecutorType.Activate, id, QuickResourceAction);
            AddExecutor(ExecutorType.Activate, CardId.Seed, SeedResponse);
            AddExecutor(ExecutorType.GoToBattlePhase, TryBattleForLethal);
            // All planned effects share one comparison, independent of registration order.
            foreach (int id in KnownIds)
                AddExecutor(ExecutorType.Activate, id, PlannedAction);
            foreach (int id in NormalIds)
                AddExecutor(ExecutorType.Summon, id, PlannedAction);
            AddExecutor(ExecutorType.SpSummon, CardId.DarkMagicianOfDestruction, PlannedAction);
            AddExecutor(ExecutorType.SpellSet, CardId.Distortion, PlannedAction);
            AddExecutor(ExecutorType.Activate, EndPhaseRecovery);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        public override bool OnSelectHand() { return true; }

        private void Invalidate()
        {
            _plan = null;
            _planKey = null;
        }

        public override void OnNewTurn()
        {
            _spellActivity = _normalUsed = _extraLock = _maxx = false;
            _used.Clear();
            _prompts.Clear();
            _accepted = null;
            _awaitingChain = false;
            _chainOffers = null;
            _viceRemoval = null;
            _lastTacticalDiagnostic = null;
            _limitCounts.Clear();
            _summonLimitSources.Clear();
            _predictions.Clear();
            _graveFusionSpent.Clear();
            Invalidate();
            base.OnNewTurn();
        }

        public override void OnSummoning()
        {
            if (Duel.LastSummonPlayer == 0) _normalUsed = true;
            Invalidate();
            base.OnSummoning();
        }

        public override void OnSpSummoned()
        {
            SummonOutcomes();
            Invalidate();
            base.OnSpSummoned();
        }

        public override void OnSelectChain(IList<ClientCard> cards)
        {
            _main = Duel.MainPhase;
            _chainOffers = cards;
            Invalidate();
            base.OnSelectChain(cards);
        }

        public override void OnChaining(int player, ClientCard card)
        {
            if (player == 1 && card != null && card.Id == GraveFusionTrigger && card.Location == CardLocation.Grave)
                _graveFusionSpent.Add(card.Id);
            if (card != null && card.IsSpell()) _spellActivity = true;
            if (player == 0 && _awaitingChain && _accepted != null && card != null &&
                card.Id == _accepted.Source)
            {
                // GameBehavior appends ChainInfo immediately after this callback.
                _prompts[Duel.CurrentChain.Count + 1] = _accepted;
                _awaitingChain = false;
            }
            Invalidate();
            base.OnChaining(player, card);
        }

        public override void OnChainSolved(int chainIndex)
        {
            ChainInfo info = Duel.GetCurrentSolvingChainInfo();
            if (info != null && !Duel.IsCurrentSolvingChainNegated())
            {
                if (info.ActivatePlayer == 1 && info.ActivateId == CardId.MaxxC)
                    _maxx = true;
                if (info.ActivatePlayer == 0 && info.ActivateId == CardId.Terracottan &&
                    info.ActivateLocation == CardLocation.Hand)
                    _extraLock = true;
            }
            Invalidate();
            base.OnChainSolved(chainIndex);
        }

        public override void OnChainEnd()
        {
            ReconcilePredictions();
            _viceRemoval = null;
            _lastTacticalDiagnostic = null;
            _prompts.Clear();
            _accepted = null;
            _awaitingChain = false;
            _chainOffers = null;
            _summonLimitSources.Clear();
            Invalidate();
            base.OnChainEnd();
        }

        public override void OnMove(ClientCard card, int previousController, int previousLocation,
            int currentController, int currentLocation)
        {
            if (previousLocation == (int)CardLocation.MonsterZone &&
                currentLocation != (int)CardLocation.MonsterZone)
            {
                _limitCounts.Remove(card);
                _summonLimitSources.Remove(card);
                _expires.Remove(card);
            }
            Invalidate();
            base.OnMove(card, previousController, previousLocation, currentController, currentLocation);
        }

        private static WZone Zone(CardLocation location)
        {
            switch (location)
            {
                case CardLocation.Hand: return WZone.Hand;
                case CardLocation.MonsterZone: return WZone.Board;
                case CardLocation.Grave: return WZone.Grave;
                case CardLocation.Removed: return WZone.Banished;
                case CardLocation.Extra: return WZone.Extra;
                case CardLocation.SpellZone: return WZone.Backrow;
                default: return WZone.Deck;
            }
        }

        private WState Observe()
        {
            WState s = new WState
            {
                Normal = !_normalUsed, SpellActivity = _spellActivity,
                ExtraLock = _extraLock, Maxx = _maxx,
                OpponentTurn = Duel.Player == 1, EnemyChain = Duel.LastChainPlayer == 1,
                IncomingAttack = Enemy.GetMonsters().Where(c => c.IsFaceup()).Sum(c => Math.Max(0, c.Attack)),
                LifePoints = Bot.LifePoints,
                Used = new HashSet<string>(_used)
            };
            foreach (int id in KnownIds)
            {
                int count = Bot.GetCardCountInDeck(id);
                if (count > 0) s.Add(id, WZone.Deck, count);
            }
            foreach (ClientCard card in Bot.Hand.Concat(Bot.GetMonsters()).Concat(Bot.Graveyard)
                .Concat(Bot.Banished).Concat(Bot.ExtraDeck).Concat(Bot.GetSpells()))
            {
                if (card == null || !KnownIds.Contains(card.Id)) continue;
                if ((card.Location == CardLocation.MonsterZone ||
                    card.Location == CardLocation.Removed) && card.IsFacedown()) continue;
                s.Add(card.Id, Zone(card.Location));
                if ((card.Location == CardLocation.MonsterZone || card.Location == CardLocation.SpellZone)
                    && card.IsDisabled()) s.Disabled.Add(card.Id);
            }
            s.UnknownHand = Bot.Hand.Count - s.Size(WZone.Hand);
            s.OtherMonsters = Bot.GetMonsters().Count - s.Size(WZone.Board);
            ClientCard extraZoneCard = Bot.GetMonsters().FirstOrDefault(c => c.Sequence >= 5);
            s.ExtraOccupant = extraZoneCard == null ? 0 : extraZoneCard.Id;
            ObserveOutcomes(s);
            // A multi-trigger window may offer several monsters. Pending belongs to
            // this candidate, not whichever unrelated offer happens to be listed last.
            if ((AI.SelectingEffectYn || _chainOffers != null) && Card != null)
            {
                if (Card.Controller == 0 && Card.Location == CardLocation.MonsterZone)
                {
                    if (Card.IsCode(CardId.DarkMagicianOfDestruction, CardId.MadameRillie, CardId.MaidVerre))
                        s.Pending = Card.Id;
                    if (Card.Id == CardId.ViceMadame) s.ViceWindow = true;
                }
            }
            return s;
        }

        private static ExecutorType ActionType(WAction action)
        {
            if (action.Kind == WKind.Normal) return ExecutorType.Summon;
            if (action.Kind == WKind.DarkSummon) return ExecutorType.SpSummon;
            if (action.Kind == WKind.SetTrap) return ExecutorType.SpellSet;
            return ExecutorType.Activate;
        }

        private static bool Matches(WAction action, ClientCard card, int description)
        {
            // MSG_SELECT_EFFECTYN uses the generic 221 prompt (normalized to -1)
            // for optional summon triggers; it does not always carry Stringid.
            bool genericTrigger = description <= 0 && (action.Kind == WKind.GazeSearch ||
                action.Kind == WKind.RillieRecruit || action.Kind == WKind.MaidSearch);
            return card != null && card.Controller == 0 && card.Id == action.Source &&
                Zone(card.Location) == action.Location && (action.Description < 0 || genericTrigger ||
                description == UtilString(action.Source, action.Description));
        }

        private static int UtilString(int id, int offset) { return id * 16 + offset; }

        private bool IsOffered(WAction action)
        {
            if (AI.SelectingEffectYn)
                return ActionType(action) == Type && Matches(action, Card, ActivateDescription);
            if (_chainOffers != null)
            {
                if (ActionType(action) != ExecutorType.Activate) return false;
                // Passing a fast-effect window preserves the next idle action. Do not
                // spend Celebration just because Terra's ignition effect is not offered
                // in this window. Only expiring trigger/recovery opportunities run here.
                if (action.Kind != WKind.GazeSearch && action.Kind != WKind.RillieRecruit &&
                    action.Kind != WKind.MaidSearch && action.Kind != WKind.ViceRecover) return false;
                // Descriptions are available for the currently evaluated offer only.
                // Never choose a different effect of this same card in a chain window.
                return action.Source == Card.Id && Matches(action, Card, ActivateDescription);
            }
            MainPhase main = Duel.MainPhase;
            if (main == null) return false;
            IList<ClientCard> cards = ActionType(action) == ExecutorType.Summon ? main.SummonableCards :
                ActionType(action) == ExecutorType.SpSummon ? main.SpecialSummonableCards :
                ActionType(action) == ExecutorType.SpellSet ? main.SpellSetableCards : main.ActivableCards;
            for (int i = 0; i < cards.Count; i++)
                if (Matches(action, cards[i], ActionType(action) == ExecutorType.Activate ?
                    main.ActivableDescs[i] : -1)) return true;
            return false;
        }

        private bool PlannedAction()
        {
            if (!ReferenceEquals(_main, Duel.MainPhase))
            {
                _main = Duel.MainPhase;
                _chainOffers = null;
                Invalidate();
            }
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2)
                return false;
            if (Duel.CurrentChain.Count == 0 && !_awaitingChain) ReconcilePredictions();
            WState observed = Observe();
            string key = observed.Key() + (AI.SelectingEffectYn || _chainOffers != null ?
                "|" + Card.Id + ":" + ActivateDescription : "");
            if (_plan == null || key != _planKey)
            {
                _plan = WPlanner.Plan(observed, IsOffered, _shutdown.Token);
                _planKey = key;
                Logger.DebugWriteLine("Witchcraft2026 compare: hold=" + WPlanner.Evaluate(observed).ToString("0.0") +
                    " best=" + _plan.Score.ToString("0.0") + " nodes=" + _plan.Expanded +
                    " bounded=" + _plan.TimedOut + " alternatives=" + string.Join(" | ", _plan.Alternatives));
                if (_plan.Steps.Count == 0 && Duel.CurrentChain.Count == 0 && Duel.Player == 0)
                    Logger.DebugWriteLine("Witchcraft2026 hold audit: model=" + WPlanner.Actions(observed).Take(257).Count() +
                        " offered-model=" + WPlanner.Actions(observed).Count(IsOffered) +
                        " engine-activate=" + (Duel.MainPhase == null ? "unknown" :
                        string.Join(",", Duel.MainPhase.ActivableCards.Select((c, i) => c.Id + "/" + Duel.MainPhase.ActivableDescs[i]))) +
                        " restrictions=" + RestrictionSummary(observed));
            }
            if (_plan.Steps.Count == 0) return false;
            WAction action = _plan.Steps[0].Action;
            if (ActionType(action) != Type || !Matches(action, Card, ActivateDescription)) return false;
            AcceptAction(action);
            Logger.DebugWriteLine("Witchcraft2026 action: " + action + " route=" + action.Route +
                " forecast=" + string.Join(" > ", _plan.Steps.Select(step => step.Action.Kind.ToString())));
            Invalidate();
            return true;
        }

        private void AcceptAction(WAction action)
        {
            if (action.Kind != WKind.Tactical)
            {
                WState before = Observe();
                _predictions.Add(new WPrediction { Action = action, Before = before, After = WPlanner.Apply(before, action) });
            }
            _accepted = action;
            _awaitingChain = Type == ExecutorType.Activate;
            _used.Add(action.Usage);
            if (action.Source == CardId.Pupils && (action.Kind == WKind.Fusion || action.Kind == WKind.PupilsSearch))
            {
                _used.Add(CardId.Pupils + ":" + WKind.Fusion);
                _used.Add(CardId.Pupils + ":" + WKind.PupilsSearch);
            }
            if (action.Kind == WKind.GenniCopy || action.Kind == WKind.GenniLacrima)
            {
                _used.Add(CardId.Genni + ":" + WKind.GenniCopy);
                _used.Add(CardId.Genni + ":" + WKind.GenniLacrima);
            }
        }

        private bool DistortionEffect()
        {
            return Card.Location == CardLocation.SpellZone && ValuableNegate("Distortion", 5) &&
                Bot.GetMonsters().Any(c => c.IsFaceup() && c.HasSetcode(WitchcrafterSetcode) && c.Level >= 5);
        }

        private bool DragoonEffect()
        {
            if (ActivateDescription == UtilString(CardId.RedEyesDarkDragoon, 1) &&
                Bot.Hand.Count > 0 && ValuableNegate("Dragoon", 10))
            {
                AI.SelectCard(DiscardOrder(Bot.Hand));
                return true;
            }
            return false;
        }

        private bool HaineEffect()
        {
            ClientCard target = RemovalTarget(true);
            if (target == null || !Bot.Hand.Any(c => c.IsSpell())) return false;
            if (!TacticalDecision("Haine", RemovalValue(target, true), 6,
                "discard plus once-per-turn; destruction is not negation", target)) return false;
            AcceptAction(new WAction { Kind = WKind.Tactical, Source = CardId.Haine,
                Location = WZone.Board, Description = 0, Target = target.Id,
                Cost = DiscardOrder(Bot.Hand.Where(c => c.IsSpell()))[0].Id, CostZone = WZone.Hand });
            return true;
        }

        private bool VerreEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.MadameVerre, 1) &&
                Duel.LastChainPlayer == 1 && Enemy.GetMonsters().Any(c => c.IsFaceup() && !c.IsDisabled()) &&
                Bot.Hand.Any(c => c.IsSpell()))
            {
                AI.SelectCard(DiscardOrder(Bot.Hand.Where(c => c.IsSpell())));
                _used.Add(CardId.MadameVerre + ":" + WKind.NegateBoard);
                return true;
            }
            return false;
        }

        private bool ViceResponse()
        {
            if (Duel.Player == 0) return false; // Own-turn choices belong to the planner.
            if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 0))
            {
                ClientCard target = RemovalTarget(false);
                if (target == null || !TacticalDecision("Vice-destroy", RemovalValue(target, false), 4,
                    "coordinate with reverse-chain removals; choose again at resolution", target)) return false;
                _viceRemoval = target;
                return true;
            }
            if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 1))
            {
                AI.SelectCard(CardId.Seed, CardId.Schmietta);
                return true;
            }
            if (ActivateDescription == Util.GetStringId(CardId.ViceMadame, 2))
            {
                AI.SelectCard(CardId.Celebration, CardId.ConfusionConfession, CardId.Creation);
                return true;
            }
            return false;
        }

        private bool SeedResponse()
        {
            ClientCard target = Enemy.GetMonsters().Concat(Enemy.GetSpells()).Where(c => c.IsFaceup() &&
                !c.IsShouldNotBeTarget() && !c.IsShouldNotBeMonsterTarget() && !ChainForecast().Removed.Contains(c))
                .OrderByDescending(c => c.IsFloodgate()).ThenByDescending(c => c.Attack).FirstOrDefault();
            if (Card.Location != CardLocation.MonsterZone || target == null) return false;
            AcceptAction(new WAction { Kind = WKind.Tactical, Source = CardId.Seed,
                Location = WZone.Board, Description = 0, Target = target.Id });
            return true;
        }

        private bool EndPhaseRecovery()
        {
            if (Duel.Phase != DuelPhase.End || Card.Controller != 0) return false;
            if (Card.Id == CardId.Pupils && Card.Location == CardLocation.MonsterZone)
            {
                AI.SelectCard(CardId.Creation, CardId.Genni, CardId.Marka);
                return true;
            }
            return Card.Location == CardLocation.Grave && Card.HasSetcode(WitchcrafterSetcode) &&
                Card.IsSpell() && Bot.GetMonsters().Any(c => c.IsFaceup() && c.HasSetcode(WitchcrafterSetcode));
        }

        private WAction PromptAction()
        {
            if (_awaitingChain) return _accepted; // Cost selection precedes MSG_CHAINING.
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
            {
                WAction action;
                return solving.ActivatePlayer == 0 && _prompts.TryGetValue(Duel.SolvingChainIndex, out action)
                    ? action : null;
            }
            ClientCard current = Duel.GetCurrentChainCard();
            if (current != null)
            {
                WAction action;
                return current.Controller == 0 && _prompts.TryGetValue(Duel.CurrentChain.Count, out action) ? action : null;
            }
            return _accepted; // Summon procedure material selection has no chain.
        }

        private static IList<ClientCard> Pick(IList<ClientCard> cards, IEnumerable<int> ids,
            int min, int max, int wanted = 1)
        {
            List<int> order = ids.ToList();
            return cards.OrderBy(c => order.IndexOf(c.Id) < 0 ? 999 : order.IndexOf(c.Id))
                .Take(Math.Min(max, Math.Max(min, wanted))).ToList();
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max,
            int hint, bool cancelable)
        {
            ChainInfo resolving = Duel.GetCurrentSolvingChainInfo();
            if (hint == HintMsg.Destroy && resolving != null && resolving.ActivatePlayer == 0 &&
                (resolving.ActivateId == CardId.Celebration || resolving.ActivateId == CardId.Pupils))
                return SelectDestructionPair(cards, min, max, cancelable);
            if (hint == HintMsg.Destroy && resolving != null && resolving.ActivatePlayer == 0 &&
                resolving.ActivateId == CardId.ViceMadame)
                return cards.OrderByDescending(c => RemovalValue(c, false))
                    .ThenByDescending(c => c.Controller == 1).Take(Math.Min(max, Math.Max(1, min))).ToList();
            WAction action = PromptAction();
            if (action != null)
            {
                if (action.Kind == WKind.Tactical && (hint == HintMsg.Destroy || hint == HintMsg.ReturnToHand))
                    return Pick(cards, new[] { action.Target }, min, max);
                if ((hint == HintMsg.Discard || hint == HintMsg.ToGrave) && action.Cost != 0)
                {
                    ClientCard cost = cards.FirstOrDefault(c => c.Id == action.Cost && Zone(c.Location) == action.CostZone);
                    if (cost != null && min <= 1) return new[] { cost };
                }
                if (hint == HintMsg.SpSummon && action.Kind == WKind.LacrimaRecruit)
                {
                    WState state = Observe();
                    WZone origin = RecruitOrigin(state, action);
                    ClientCard target = cards.FirstOrDefault(c => c.Id == action.Target && Zone(c.Location) == origin);
                    if (target != null && min <= 1) return new[] { target };
                }
                if (hint == HintMsg.SpSummon && action.Kind == WKind.RillieRecruit)
                    return Pick(cards, action.Recruits, min, max, action.Recruits.Length);
                if (hint == HintMsg.AddToHand || hint == HintMsg.SpSummon ||
                    (hint == HintMsg.ToGrave && (action.Kind == WKind.Send || action.Kind == WKind.LacrimaSend)))
                    if (action.Target != 0) return Pick(cards, new[] { action.Target }, min, max);
                if (hint == HintMsg.Remove && action.Kind == WKind.GenniCopy)
                    return Pick(cards, new[] { CardId.Creation, CardId.Genni }, min, max);
                if (hint == HintMsg.Remove && action.Kind == WKind.GenniLacrima)
                    return Pick(cards, new[] { CardId.Lacrima }, min, max);
                if (hint == HintMsg.Remove && action.Kind == WKind.MaidExchange)
                    return Pick(cards, action.Materials, min, max);
                if (hint == HintMsg.Remove && action.Kind == WKind.DarkSummon)
                    return Pick(cards, action.Materials, min, max);
                if (hint == HintMsg.Remove && action.Source == CardId.RainbowBridgeBifrost)
                    return Pick(cards, new[] { CardId.ElfnoteJunePride, CardId.CosmicQuasarDragon,
                        CardId.DespianLuluwalilith }, min, max, 3);
                if (hint == HintMsg.ToDeck && action.Kind == WKind.Gaze)
                    return Pick(cards, action.Materials, min, max);
                if (hint == HintMsg.Confirm && action.Source == CardId.Pupils)
                    return Pick(cards, new[] { CardId.Celebration }, min, max);
            }
            if (hint == HintMsg.Destroy || hint == HintMsg.Disable || hint == HintMsg.ReturnToHand)
                return cards.OrderByDescending(c => c.Controller == 1).ThenByDescending(c => c.IsFloodgate())
                    .ThenByDescending(c => c.Attack).Take(Math.Min(max, Math.Max(1, min))).ToList();
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public override IList<ClientCard> OnSelectFusionMaterial(IList<ClientCard> cards, int min, int max)
        {
            WAction action = PromptAction();
            if (action == null || action.Materials.Length == 0)
                return base.OnSelectFusionMaterial(cards, min, max);
            List<ClientCard> selected = new List<ClientCard>();
            for (int i = 0; i < action.Materials.Length; i++)
            {
                ClientCard material = cards.FirstOrDefault(c => c.Id == action.Materials[i] &&
                    Zone(c.Location) == action.MaterialZones[i] && !selected.Contains(c));
                if (material != null) selected.Add(material);
            }
            // The engine can request fusion materials one at a time.
            if (selected.Count >= min)
                return selected.Take(Math.Min(max, selected.Count)).ToList();
            Logger.DebugWriteLine("Witchcraft2026 material mismatch: " + action);
            return base.OnSelectFusionMaterial(cards, min, max);
        }

        public override int OnSelectOption(IList<int> options)
        {
            WAction action = PromptAction();
            if (action != null && action.Kind == WKind.PupilsSearch)
            {
                int search = options.IndexOf(UtilString(CardId.Pupils, 2));
                if (search >= 0) return search;
            }
            if (action != null && (action.Kind == WKind.LacrimaSend ||
                action.Kind == WKind.LacrimaRecruit || action.Kind == WKind.GenniLacrima))
            {
                int option = options.IndexOf(UtilString(CardId.Lacrima, action.Kind == WKind.LacrimaSend ? 1 : 2));
                if (option >= 0) return option;
            }
            if (action != null && action.Kind == WKind.Fusion)
            {
                foreach (int id in new[] { CardId.Pupils, CardId.Celebration })
                {
                    int index = options.IndexOf(Util.GetStringId(id, 3));
                    if (index >= 0) return index;
                }
                // We cannot fabricate a cancel option after activation. Retire the
                // fusion expectation and let the owned destruction callback minimize
                // loss if the engine supplies only that mandatory mode.
                Logger.DebugWriteLine("Witchcraft2026 mode mismatch: wanted Fusion, offered=" +
                    string.Join(",", options) + "; forecast invalidated, no invented cancel");
                Invalidate();
            }
            return base.OnSelectOption(options);
        }

        public override int OnSelectPlace(int cardId, int player, CardLocation location, int available)
        {
            if (player == 0 && location == CardLocation.MonsterZone &&
                cardId == CardId.Pupils)
            {
                if ((available & 0x20) != 0) return 0x20;
                if ((available & 0x40) != 0) return 0x40;
            }
            return base.OnSelectPlace(cardId, player, location, available);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (Duel.Turn <= 1 && cardId != CardId.RedEyesDarkDragoon && cardId != CardId.Haine &&
                positions.Contains(CardPosition.FaceUpDefence)) return CardPosition.FaceUpDefence;
            return base.OnSelectPosition(cardId, positions);
        }

        private static IList<ClientCard> DiscardOrder(IEnumerable<ClientCard> cards)
        {
            return cards.OrderBy(c => GetWalpurgisDiscardRank(c.Id, true)).ToList();
        }

        private static int GetNextGuideLowWitchcrafter(int id)
        {
            if (id == CardId.Genni) return CardId.Schmietta;
            if (id == CardId.Schmietta) return CardId.Pittore;
            if (id == CardId.Pittore) return CardId.Potterie;
            if (id == CardId.Potterie) return CardId.Marka;
            return 0;
        }

        private static int GetWalpurgisDiscardRank(int id, bool guide)
        {
            if (guide && id == CardId.MadameVerre) return -100;
            int[] order = { CardId.RainbowBridgeBifrost, CardId.MaidVerre, CardId.Distortion,
                CardId.Celebration, CardId.Bystreet, CardId.Unveiling, CardId.ConfusionConfession,
                CardId.Creation, CardId.Lacrima, CardId.Terraforming, CardId.SpellPowerMastery };
            int index = Array.IndexOf(order, id);
            if (index >= 0) return index;
            if (guide && (id == CardId.Genni || id == CardId.GazeOfTimaeus)) return 1000;
            return 100;
        }
    }
}
