using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    public partial class Witchcraft2026Executor
    {
        // Each edge owns its costs and choices. No predicted state is written into Duel.
        internal enum WZone { Deck, Hand, Board, Grave, Banished, Extra, Backrow }
        internal enum WKind
        {
            FieldAccess, Mastery, Creation, Marka, ActivateField, FieldSearch,
            Normal, Recruit, DarkSummon, GazeSearch, Gaze, GenniCopy, Recover,
            Fusion, Send, RillieRecruit, EdelRevive, ViceRecover, MaidSummon,
            MaidSearch, SetTrap, Unveiling
        }

        internal sealed class WAction
        {
            public WKind Kind;
            public int Source;
            public WZone Location;
            public int Description;
            public int Target;
            public int Cost;
            public WZone CostZone;
            public int[] Materials = new int[0];
            public WZone[] MaterialZones = new WZone[0];
            public int[] Recruits = new int[0];
            public string Route;
            public string Usage { get { return Source + ":" + Kind; } }
            public string Identity { get { return Kind + ":" + Source + ":" + Location; } }
            public override string ToString()
            {
                return Kind + "(" + Source + ")->" + Target + " cost=" + Cost +
                    " materials=" + string.Join(",", Materials);
            }
        }

        internal sealed class WState
        {
            public Dictionary<long, int> Cards = new Dictionary<long, int>();
            public HashSet<string> Used = new HashSet<string>();
            public HashSet<int> Disabled = new HashSet<int>();
            public bool Normal = true;
            public bool SpellActivity;
            public bool ExtraLock;
            public bool Maxx;
            public bool ViceWindow;
            public int Pending;
            public int UnknownHand;
            public int OtherMonsters;
            public int ExtraOccupant;
            public int FutureSummons;
            public int Steps;
            public WState Copy()
            {
                WState copy = (WState)MemberwiseClone();
                copy.Cards = new Dictionary<long, int>(Cards);
                copy.Used = new HashSet<string>(Used);
                copy.Disabled = new HashSet<int>(Disabled);
                return copy;
            }
            public int Count(int id, WZone zone)
            {
                int count;
                return Cards.TryGetValue((long)id * 8 + (int)zone, out count) ? count : 0;
            }
            public bool Has(int id, WZone zone) { return Count(id, zone) > 0; }
            public void Add(int id, WZone zone, int amount = 1)
            {
                long key = (long)id * 8 + (int)zone;
                int count = Count(id, zone) + amount;
                if (count < 0) throw new InvalidOperationException("Model spent absent card " + id);
                if (count == 0) Cards.Remove(key); else Cards[key] = count;
            }
            public void Move(int id, WZone from, WZone to)
            {
                Add(id, from, -1);
                Add(id, to);
                if (from == WZone.Board && ExtraOccupant == id) ExtraOccupant = 0;
            }
            public IEnumerable<int> In(WZone zone)
            {
                return Cards.Where(pair => pair.Key % 8 == (int)zone && pair.Value > 0)
                    .Select(pair => (int)(pair.Key / 8));
            }
            public int Size(WZone zone) { return In(zone).Sum(id => Count(id, zone)); }
            public bool Active(int id)
            {
                return Has(id, WZone.Board) && !Disabled.Contains(id);
            }
            public string Key()
            {
                return string.Join(",", Cards.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)) +
                    "|" + string.Join(",", Used.OrderBy(k => k)) + "|" +
                    string.Join(",", Disabled.OrderBy(k => k)) + "|" +
                    Normal + "|" + SpellActivity + "|" + ExtraLock + "|" + Maxx +
                    "|" + Pending + "|" + ViceWindow + "|" + FutureSummons + "|" +
                    ExtraOccupant + "|" + UnknownHand + "|" + OtherMonsters;
            }
        }

        internal sealed class WMove
        {
            public WAction Action;
            public WState After;
        }

        internal sealed class WPlan
        {
            public List<WMove> Steps = new List<WMove>();
            public List<string> Alternatives = new List<string>();
            public double Score;
            public int Expanded;
            public bool TimedOut;
        }

        internal static readonly int[] LowIds =
        {
            CardId.Genni, CardId.Schmietta, CardId.Pittore, CardId.Potterie
        };
        internal static readonly int[] WitchMonsters =
        {
            CardId.Genni, CardId.Schmietta, CardId.Pittore, CardId.Potterie,
            CardId.Marka, CardId.Terracottan, CardId.Haine, CardId.MadameVerre,
            CardId.Edel, CardId.Seed
        };
        internal static readonly int[] WitchSpells =
        {
            CardId.Walpurgis, CardId.Creation, CardId.Celebration,
            CardId.ConfusionConfession, CardId.Unveiling, CardId.Bystreet
        };
        internal static readonly int[] KnownIds = typeof(CardId).GetFields()
            .Where(field => field.IsLiteral).Select(field => (int)field.GetRawConstantValue()).ToArray();

        internal static bool IsModelSpell(int id)
        {
            return WitchSpells.Contains(id) || id == CardId.Lacrima ||
                id == CardId.Terraforming || id == CardId.RainbowBridgeBifrost ||
                id == CardId.GazeOfTimaeus || id == CardId.SpellPowerMastery;
        }
        internal static bool IsWitchMaterial(int id)
        {
            return WitchMonsters.Contains(id) || id == CardId.Pupils ||
                id == CardId.MadameRillie || id == CardId.ViceMadame;
        }
        internal static bool IsSpellcasterMaterial(int id)
        {
            return IsWitchMaterial(id) || id == CardId.MaidVerre ||
                id == CardId.MaidGenni || id == CardId.RedEyesDarkDragoon ||
                id == CardId.DarkMagicianOfDestruction;
        }

        internal static class WPlanner
        {
            private const int MaxDepth = 30;
            private const int BeamWidth = 64;
            private const int MaxNodes = 5000;
            private const int BudgetMs = 180;
            private const int MaxCandidates = 256;
            private sealed class Node
            {
                public WState State;
                public List<WMove> Path;
                public double Score;
            }

            public static WPlan Plan(WState root, Func<WAction, bool> offered,
                CancellationToken cancellation)
            {
                Stopwatch watch = Stopwatch.StartNew();
                WPlan result = new WPlan { Score = Evaluate(root) };
                List<Node> frontier = new List<Node>
                {
                    new Node { State = root, Path = new List<WMove>(), Score = result.Score }
                };
                Dictionary<string, Node> alternatives = new Dictionary<string, Node>();
                // Evaluate the documented route to its endpoint before beam pruning.
                // Every edge still comes from the same resource-checked action generator.
                // This keeps a long bridge from losing to a shallow, immediately valuable boss.
                foreach (WAction opening in Actions(root).Take(MaxCandidates).OrderByDescending(a => BookRank(root, a)))
                {
                    if (cancellation.IsCancellationRequested)
                        return new WPlan { Score = Evaluate(root) };
                    if (watch.ElapsedMilliseconds >= BudgetMs || result.Expanded >= MaxNodes)
                    {
                        result.TimedOut = true;
                        break;
                    }
                    if (offered != null && !offered(opening)) continue;
                    WState state = root;
                    WAction action = opening;
                    List<WMove> path = new List<WMove>();
                    while (action != null && path.Count < MaxDepth && watch.ElapsedMilliseconds < BudgetMs &&
                        result.Expanded < MaxNodes)
                    {
                        if (cancellation.IsCancellationRequested)
                            return new WPlan { Score = Evaluate(root) };
                        state = Apply(state, action);
                        result.Expanded++;
                        path.Add(new WMove { Action = action, After = state });
                        double score = Evaluate(state);
                        if (score > result.Score + .01)
                        {
                            result.Score = score;
                            result.Steps = new List<WMove>(path);
                        }
                        Node previous;
                        string key = opening.ToString();
                        if (!alternatives.TryGetValue(key, out previous) || score > previous.Score)
                            alternatives[key] = new Node { State = state, Path = new List<WMove>(path), Score = score };
                        action = Actions(state).Take(MaxCandidates).Where(a => BookRank(state, a) > 0)
                            .OrderByDescending(a => BookRank(state, a)).FirstOrDefault();
                    }
                }
                for (int depth = 0; depth < MaxDepth && frontier.Count > 0; depth++)
                {
                    List<Node> next = new List<Node>();
                    foreach (Node node in frontier)
                    {
                        if (cancellation.IsCancellationRequested)
                            return new WPlan { Score = Evaluate(root) };
                        if (watch.ElapsedMilliseconds >= BudgetMs || result.Expanded >= MaxNodes)
                        {
                            result.TimedOut = true;
                            break;
                        }
                        result.Expanded++;
                        foreach (WAction action in Actions(node.State).Take(MaxCandidates))
                        {
                            if (watch.ElapsedMilliseconds >= BudgetMs)
                            {
                                result.TimedOut = true;
                                break;
                            }
                            if (depth == 0 && offered != null && !offered(action)) continue;
                            WState after = Apply(node.State, action);
                            WMove move = new WMove { Action = action, After = after };
                            List<WMove> path = new List<WMove>(node.Path) { move };
                            double score = Evaluate(after);
                            Node child = new Node { State = after, Path = path, Score = score };
                            next.Add(child);
                            string first = path[0].Action.ToString();
                            Node previous;
                            if (!alternatives.TryGetValue(first, out previous) || score > previous.Score)
                                alternatives[first] = child;
                            // A reference route is a search prior only. The selected endpoint must
                            // beat preserving the observed board/resources under the same evaluator.
                            if (score > result.Score + 0.01)
                            {
                                result.Score = score;
                                result.Steps = path;
                            }
                        }
                        if (result.TimedOut) break;
                    }
                    if (result.TimedOut) break;
                    // Preserve different openings so an early engine extension cannot erase all
                    // candidates belonging to the book or the low-summon control alternative.
                    frontier = next.GroupBy(node => node.State.Key())
                        .Select(group => group.OrderByDescending(n => n.Score).First())
                        .GroupBy(n => n.Path[0].Action.Identity)
                        .SelectMany(group => group.OrderByDescending(n =>
                            n.Score + Potential(n.State)).Take(10))
                        .OrderByDescending(n => n.Score + Potential(n.State))
                        .Take(BeamWidth).ToList();
                }
                result.Alternatives = alternatives.Values.OrderByDescending(n => n.Score)
                    .Take(4).Select(n => n.Path[0].Action + " => " +
                        n.Score.ToString("0.0") + " [" + BoardSummary(n.State) + "]").ToList();
                return result;
            }

            private static WAction A(WKind kind, int source, WZone zone,
                int target = 0, int description = -1)
            {
                return new WAction { Kind = kind, Source = source, Location = zone,
                    Target = target, Description = description, Route = "resource-search" };
            }

            private static int BookRank(WState s, WAction a)
            {
                if (s.Pending != 0) return 1000;
                if (a.Kind == WKind.ViceRecover && a.Target == CardId.Distortion) return 980;
                if (a.Kind == WKind.SetTrap && s.Active(CardId.ViceMadame)) return 970;
                if (a.Kind == WKind.EdelRevive && s.Active(CardId.ViceMadame)) return 960;
                if (a.Kind == WKind.Fusion && a.Source == CardId.Celebration &&
                    a.Target == CardId.ViceMadame && s.Active(CardId.MadameRillie) &&
                    a.Materials.Contains(CardId.Terracottan) && a.Materials.Contains(CardId.Marka)) return 950;
                if (a.Kind == WKind.Fusion && a.Source == CardId.Pupils &&
                    a.Target == CardId.MadameRillie &&
                    a.Materials.Contains(CardId.Genni) && a.Materials.Contains(CardId.Schmietta)) return 940;
                if (a.Kind == WKind.Send && a.Target == CardId.MadameVerre &&
                    s.Active(CardId.Pupils) && !s.Has(CardId.MadameVerre, WZone.Grave) &&
                    !s.Has(CardId.MadameVerre, WZone.Banished)) return 930;
                if (a.Kind == WKind.Fusion && a.Source == CardId.Terracottan &&
                    a.Target == CardId.Pupils && a.Materials.Contains(CardId.Terracottan) &&
                    a.Materials.Contains(CardId.RedEyesDarkDragoon)) return 920;
                if (a.Kind == WKind.Recover && a.Target == CardId.Celebration &&
                    s.Active(CardId.RedEyesDarkDragoon)) return 910;
                if (a.Kind == WKind.GenniCopy && a.Target == CardId.Terracottan &&
                    s.Active(CardId.RedEyesDarkDragoon) && !s.Has(CardId.Terracottan, WZone.Hand)) return 900;
                if (a.Kind == WKind.Gaze) return 890;
                if (a.Kind == WKind.DarkSummon) return 880;
                if (a.Kind == WKind.Recruit && a.Target == GetNextGuideLowWitchcrafter(a.Source))
                {
                    int cost = a.Source == CardId.Genni ? CardId.Creation :
                        a.Source == CardId.Schmietta ? CardId.Celebration :
                        a.Source == CardId.Pittore ? CardId.Distortion : CardId.ConfusionConfession;
                    return a.Cost == cost ? 870 : 860;
                }
                if (a.Kind == WKind.Normal && a.Source == CardId.Genni &&
                    s.Has(CardId.Walpurgis, WZone.Backrow)) return 850;
                if (a.Kind == WKind.FieldSearch && a.Target == CardId.Genni &&
                    !s.Has(CardId.Genni, WZone.Hand) && s.Normal)
                    return 840 - Math.Min(20, Math.Max(-1, GetWalpurgisDiscardRank(a.Cost, true)));
                if (a.Kind == WKind.ActivateField) return 810;
                if (a.Kind == WKind.FieldAccess && !s.Has(CardId.Walpurgis, WZone.Hand)) return 800;
                if (a.Kind == WKind.Marka) return 790;
                if (a.Kind == WKind.Creation && a.Target == CardId.Marka &&
                    !s.Has(CardId.Marka, WZone.Hand) && !s.Has(CardId.Walpurgis, WZone.Backrow) &&
                    !s.Has(CardId.Walpurgis, WZone.Hand)) return 780;
                return 0;
            }

            public static IEnumerable<WAction> Actions(WState s)
            {
                foreach (WAction a in Generate(s))
                {
                    if (s.Used.Contains(a.Usage)) continue;
                    if (a.Location == WZone.Board && s.Disabled.Contains(a.Source)) continue;
                    yield return a;
                }
            }

            private static IEnumerable<WAction> Generate(WState s)
            {
                int board = s.Size(WZone.Board) + s.OtherMonsters;
                if (s.Pending == CardId.DarkMagicianOfDestruction)
                {
                    if (s.Has(CardId.GazeOfTimaeus, WZone.Deck))
                        yield return A(WKind.GazeSearch, s.Pending, WZone.Board, CardId.GazeOfTimaeus, 0);
                    yield break;
                }
                if (s.Pending == CardId.MadameRillie)
                {
                    int[] ids = new[] { CardId.Haine, CardId.Edel, CardId.MaidGenni };
                    int free = Math.Max(0, 5 - MainMonsters(s));
                    int[] recruit = ids.Where(id => s.Has(id, WZone.Deck) || s.Has(id, WZone.Hand))
                        .Take(free).ToArray();
                    if (recruit.Length > 0)
                    {
                        WAction a = A(WKind.RillieRecruit, s.Pending, WZone.Board, 0, 0);
                        a.Recruits = recruit;
                        a.Route = "guide:Rilliona recruits";
                        yield return a;
                    }
                    yield break;
                }
                if (s.Pending == CardId.MaidVerre)
                {
                    foreach (int target in new[] { CardId.Walpurgis, CardId.Creation,
                        CardId.Distortion, CardId.Celebration })
                        if (s.Has(target, WZone.Deck))
                            yield return A(WKind.MaidSearch, s.Pending, WZone.Board, target, 1);
                    yield break;
                }

                foreach (int source in new[] { CardId.Terraforming, CardId.RainbowBridgeBifrost })
                {
                    if (s.Has(source, WZone.Hand) && s.Has(CardId.Walpurgis, WZone.Deck) &&
                        !s.Has(CardId.Walpurgis, WZone.Backrow) &&
                        (source != CardId.RainbowBridgeBifrost ||
                        new[] { CardId.CosmicQuasarDragon, CardId.DespianLuluwalilith,
                            CardId.ElfnoteJunePride }.All(id => s.Has(id, WZone.Extra))))
                        yield return A(WKind.FieldAccess, source, WZone.Hand, CardId.Walpurgis);
                }
                if (s.Has(CardId.SpellPowerMastery, WZone.Hand))
                    foreach (int id in new[] { CardId.MaidVerre, CardId.MaidGenni })
                        if (s.Has(id, WZone.Deck))
                            yield return A(WKind.Mastery, CardId.SpellPowerMastery, WZone.Hand, id);

                if (s.Has(CardId.Walpurgis, WZone.Hand) && !s.Has(CardId.Walpurgis, WZone.Backrow))
                    yield return A(WKind.ActivateField, CardId.Walpurgis, WZone.Hand);
                if (s.Has(CardId.Walpurgis, WZone.Backrow) && !s.Disabled.Contains(CardId.Walpurgis))
                {
                    foreach (int target in new[] { CardId.Genni, CardId.Schmietta,
                        CardId.Terracottan, CardId.Marka })
                    {
                        if (!s.Has(target, WZone.Deck)) continue;
                        foreach (int discard in s.In(WZone.Hand).OrderBy(id =>
                            GetWalpurgisDiscardRank(id, true)).Take(3))
                        {
                            WAction a = A(WKind.FieldSearch, CardId.Walpurgis, WZone.Backrow, target, 0);
                            a.Cost = discard; a.CostZone = WZone.Hand;
                            a.Route = "guide:Walpurgis access";
                            yield return a;
                        }
                    }
                }
                if (s.Has(CardId.Creation, WZone.Hand))
                    foreach (int target in new[] { CardId.Marka, CardId.Genni,
                        CardId.Schmietta, CardId.Terracottan })
                        if (s.Has(target, WZone.Deck))
                            yield return A(WKind.Creation, CardId.Creation, WZone.Hand, target);
                foreach (WZone zone in new[] { WZone.Hand, WZone.Board })
                    if (s.Has(CardId.Marka, zone) && !s.Has(CardId.Walpurgis, WZone.Backrow) &&
                        !s.Has(CardId.Walpurgis, WZone.Hand) && s.Has(CardId.Walpurgis, WZone.Deck))
                        yield return A(WKind.Marka, CardId.Marka, zone, CardId.Walpurgis, 0);

                if (s.Normal && MainMonsters(s) < 5)
                    foreach (int low in LowIds)
                        if (s.Has(low, WZone.Hand))
                            yield return A(WKind.Normal, low, WZone.Hand);
                foreach (int low in LowIds)
                {
                    if (!s.Active(low)) continue;
                    int next = GetNextGuideLowWitchcrafter(low);
                    IEnumerable<int> targets = new[] { next, CardId.Haine, CardId.MadameVerre };
                    foreach (int target in targets.Distinct())
                    {
                        if (!s.Has(target, WZone.Deck)) continue;
                        foreach (var cost in Costs(s, low).Take(2))
                        {
                            WAction a = A(WKind.Recruit, low, WZone.Board, target, 0);
                            a.Cost = cost.Item1; a.CostZone = cost.Item2;
                            a.Route = target == next ? "guide:low ladder" : "control:stop on boss";
                            yield return a;
                        }
                    }
                }
                if (s.SpellActivity && !s.ExtraLock && s.Active(CardId.Marka) &&
                    s.Has(CardId.DarkMagicianOfDestruction, WZone.Extra))
                {
                    WAction a = A(WKind.DarkSummon, CardId.DarkMagicianOfDestruction, WZone.Extra);
                    a.Materials = new[] { CardId.Marka }; a.MaterialZones = new[] { WZone.Board };
                    a.Route = "guide:Dragoon bridge";
                    yield return a;
                }
                if (!s.ExtraLock && s.Has(CardId.GazeOfTimaeus, WZone.Hand) &&
                    s.Has(CardId.RedEyesDarkDragoon, WZone.Extra))
                    foreach (WZone zone in new[] { WZone.Board, WZone.Grave })
                        if (s.Has(CardId.DarkMagicianOfDestruction, zone))
                        {
                            WAction a = A(WKind.Gaze, CardId.GazeOfTimaeus, WZone.Hand,
                                CardId.RedEyesDarkDragoon, 0);
                            a.Materials = new[] { CardId.DarkMagicianOfDestruction };
                            a.MaterialZones = new[] { zone };
                            yield return a;
                        }
                if (s.Has(CardId.Genni, WZone.Grave) && s.Has(CardId.Creation, WZone.Grave))
                    foreach (int target in new[] { CardId.Terracottan, CardId.Marka })
                        if (s.Has(target, WZone.Deck))
                            yield return A(WKind.GenniCopy, CardId.Genni, WZone.Grave, target, 1);
                if (s.Has(CardId.Terracottan, WZone.Hand) && MainMonsters(s) < 5)
                    foreach (int target in new[] { CardId.Celebration, CardId.Creation, CardId.ConfusionConfession })
                        if (s.Has(target, WZone.Grave))
                            yield return A(WKind.Recover, CardId.Terracottan, WZone.Hand, target, 0);
                if (s.Has(CardId.Schmietta, WZone.Grave))
                    foreach (int target in new[] { CardId.MadameVerre, CardId.Celebration, CardId.Bystreet })
                        if (s.Has(target, WZone.Deck) && !s.Has(target, WZone.Grave))
                            yield return A(WKind.Send, CardId.Schmietta, WZone.Grave, target, 1);

                foreach (WAction fusion in Fusions(s)) yield return fusion;
                if (s.Active(CardId.Edel) && s.Has(CardId.RedEyesDarkDragoon, WZone.Grave))
                    yield return A(WKind.EdelRevive, CardId.Edel, WZone.Board, CardId.RedEyesDarkDragoon, 1);
                if (s.Active(CardId.ViceMadame) && s.ViceWindow)
                    foreach (int target in new[] { CardId.Distortion, CardId.Celebration, CardId.Creation })
                        if (s.Has(target, WZone.Grave))
                            yield return A(WKind.ViceRecover, CardId.ViceMadame, WZone.Board, target, 2);
                if (s.Has(CardId.MaidVerre, WZone.Hand) && s.SpellActivity && MainMonsters(s) < 5)
                    yield return A(WKind.MaidSummon, CardId.MaidVerre, WZone.Hand, 0, 0);
                if (s.Has(CardId.Unveiling, WZone.Hand) && MainMonsters(s) < 5)
                    foreach (int target in new[] { CardId.Haine, CardId.MadameVerre, CardId.Edel })
                        if (s.Has(target, WZone.Hand))
                            yield return A(WKind.Unveiling, CardId.Unveiling, WZone.Hand, target);
                if (s.Has(CardId.Distortion, WZone.Hand) && !s.Has(CardId.Distortion, WZone.Backrow))
                    yield return A(WKind.SetTrap, CardId.Distortion, WZone.Hand);
            }

            internal static int MainMonsters(WState s)
            {
                return s.Size(WZone.Board) + s.OtherMonsters - (s.ExtraOccupant != 0 ? 1 : 0);
            }

            private static IEnumerable<Tuple<int, WZone>> Costs(WState s, int low)
            {
                int preferred = low == CardId.Genni ? CardId.Creation :
                    low == CardId.Schmietta ? CardId.Celebration :
                    low == CardId.Pittore ? CardId.Distortion : CardId.ConfusionConfession;
                int[] order = new[] { preferred, CardId.Bystreet, CardId.Creation,
                    CardId.Unveiling, CardId.Celebration, CardId.ConfusionConfession };
                if (s.Has(CardId.Walpurgis, WZone.Backrow) && !s.Disabled.Contains(CardId.Walpurgis))
                    foreach (int id in order.Distinct())
                        if (s.Has(id, WZone.Deck)) yield return Tuple.Create(id, WZone.Deck);
                foreach (int id in s.In(WZone.Hand).Where(IsModelSpell)
                    .OrderBy(id => Array.IndexOf(order, id) < 0 ? 100 : Array.IndexOf(order, id)))
                    yield return Tuple.Create(id, WZone.Hand);
            }

            private static IEnumerable<WAction> Fusions(WState s)
            {
                foreach (int source in new[] { CardId.Terracottan, CardId.Pupils,
                    CardId.Celebration, CardId.ConfusionConfession })
                {
                    bool boardSource = source == CardId.Terracottan || source == CardId.Pupils;
                    if (boardSource ? !s.Active(source) : !s.Has(source, WZone.Hand)) continue;
                    bool recycle = source == CardId.Celebration || source == CardId.Pupils;
                    if (source == CardId.Pupils && !s.Has(CardId.Celebration, WZone.Hand)) continue;
                    WZone[] zones = recycle ? new[] { WZone.Grave, WZone.Banished } :
                        new[] { WZone.Board, WZone.Hand };
                    List<Tuple<int, WZone>> materials = zones.SelectMany(zone =>
                        s.In(zone).Where(IsSpellcasterMaterial).SelectMany(id =>
                            Enumerable.Repeat(Tuple.Create(id, zone), s.Count(id, zone)))).Take(16).ToList();
                    foreach (int target in new[] { CardId.MadameRillie, CardId.Pupils, CardId.ViceMadame })
                    {
                        if (!s.Has(target, WZone.Extra)) continue;
                        // Limit material candidates by preservation value; keep the documented pair
                        // available even if a boss is also a legal material.
                        IEnumerable<Tuple<int, WZone>[]> sets = MaterialSets(materials,
                            target == CardId.MadameRillie ? 3 : 2);
                        foreach (var set in sets)
                        {
                            if (target == CardId.MadameRillie ?
                                !set.Any(m => m.Item1 == CardId.MadameVerre) :
                                !set.Any(m => IsWitchMaterial(m.Item1))) continue;
                            int fieldConsumed = set.Count(m => m.Item2 == WZone.Board);
                            if (s.Size(WZone.Board) + s.OtherMonsters - fieldConsumed >= 6) continue;
                            WAction a = A(WKind.Fusion, source,
                                boardSource ? WZone.Board : WZone.Hand, target,
                                source == CardId.Terracottan ? 1 : source == CardId.Pupils ? 0 : -1);
                            a.Materials = set.Select(m => m.Item1).ToArray();
                            a.MaterialZones = set.Select(m => m.Item2).ToArray();
                            a.Route = source == CardId.Pupils ? "guide:copy Celebration" :
                                target == CardId.Pupils ? "guide:Pupils bridge" : "fusion alternatives";
                            yield return a;
                        }
                    }
                }
            }

            private static IEnumerable<Tuple<int, WZone>[]> MaterialSets(
                IList<Tuple<int, WZone>> cards, int count)
            {
                for (int i = 0; i < cards.Count; i++)
                    for (int j = i + 1; j < cards.Count; j++)
                    {
                        if (count == 2) yield return new[] { cards[i], cards[j] };
                        else for (int k = j + 1; k < cards.Count; k++)
                            yield return new[] { cards[i], cards[j], cards[k] };
                    }
            }

            public static WState Apply(WState state, WAction a)
            {
                WState s = state.Copy();
                s.Used.Add(a.Usage);
                s.Steps++;
                s.Pending = 0;
                s.ViceWindow = a.Kind != WKind.Normal && a.Kind != WKind.SetTrap &&
                    a.Kind != WKind.Fusion && a.Source != CardId.ViceMadame &&
                    (IsModelSpell(a.Source) || IsSpellcasterMaterial(a.Source));
                if (a.Location == WZone.Hand && IsModelSpell(a.Source) &&
                    a.Kind != WKind.ActivateField && a.Kind != WKind.SetTrap)
                {
                    s.Move(a.Source, WZone.Hand, WZone.Grave);
                    s.SpellActivity = true;
                }
                switch (a.Kind)
                {
                    case WKind.FieldAccess:
                        if (a.Source == CardId.RainbowBridgeBifrost)
                            foreach (int id in new[] { CardId.CosmicQuasarDragon,
                                CardId.DespianLuluwalilith, CardId.ElfnoteJunePride })
                                s.Move(id, WZone.Extra, WZone.Banished);
                        s.Move(a.Target, WZone.Deck, WZone.Hand); break;
                    case WKind.Mastery:
                    case WKind.Creation:
                    case WKind.GazeSearch:
                    case WKind.MaidSearch:
                        s.Move(a.Target, WZone.Deck, WZone.Hand); break;
                    case WKind.Marka:
                        s.Move(a.Source, a.Location, WZone.Grave);
                        s.Move(a.Target, WZone.Deck, WZone.Hand); break;
                    case WKind.ActivateField:
                        s.Move(a.Source, WZone.Hand, WZone.Backrow); s.SpellActivity = true; break;
                    case WKind.FieldSearch:
                        s.Move(a.Target, WZone.Deck, WZone.Hand);
                        s.Move(a.Cost, WZone.Hand, WZone.Grave); break;
                    case WKind.Normal:
                        s.Move(a.Source, WZone.Hand, WZone.Board); s.Normal = false; break;
                    case WKind.Recruit:
                        s.Move(a.Source, WZone.Board, WZone.Grave);
                        s.Move(a.Cost, a.CostZone, WZone.Grave);
                        s.Move(a.Target, WZone.Deck, WZone.Board); s.FutureSummons++; break;
                    case WKind.DarkSummon:
                        s.Move(CardId.Marka, WZone.Board, WZone.Banished);
                        s.Move(a.Source, WZone.Extra, WZone.Board);
                        if (s.ExtraOccupant == 0) s.ExtraOccupant = a.Source;
                        s.Pending = a.Source; s.FutureSummons++; break;
                    case WKind.Gaze:
                        s.Move(a.Materials[0], a.MaterialZones[0], WZone.Extra);
                        s.Move(a.Target, WZone.Extra, WZone.Board); s.FutureSummons++; break;
                    case WKind.GenniCopy:
                        s.Move(CardId.Genni, WZone.Grave, WZone.Banished);
                        s.Move(CardId.Creation, WZone.Grave, WZone.Banished);
                        s.Move(a.Target, WZone.Deck, WZone.Hand); break;
                    case WKind.Recover:
                        s.Move(a.Target, WZone.Grave, WZone.Hand);
                        s.Move(a.Source, WZone.Hand, WZone.Board);
                        s.ExtraLock = true; s.FutureSummons++; break;
                    case WKind.Send:
                        s.Move(a.Source, WZone.Grave, WZone.Banished);
                        s.Move(a.Target, WZone.Deck, WZone.Grave); break;
                    case WKind.Fusion:
                        bool recycle = a.Source == CardId.Pupils || a.Source == CardId.Celebration;
                        for (int i = 0; i < a.Materials.Length; i++)
                            s.Move(a.Materials[i], a.MaterialZones[i],
                                recycle ? (IsExtra(a.Materials[i]) ? WZone.Extra : WZone.Deck) : WZone.Grave);
                        s.Move(a.Target, WZone.Extra, WZone.Board);
                        if (s.ExtraOccupant == 0) s.ExtraOccupant = a.Target;
                        if (a.Target == CardId.MadameRillie) s.Pending = a.Target;
                        s.FutureSummons++; break;
                    case WKind.RillieRecruit:
                        foreach (int id in a.Recruits)
                            s.Move(id, s.Has(id, WZone.Hand) ? WZone.Hand : WZone.Deck, WZone.Board);
                        s.FutureSummons++; break; // simultaneous summon = one Maxx C draw
                    case WKind.EdelRevive:
                        s.Move(a.Source, WZone.Board, WZone.Grave);
                        s.Move(a.Target, WZone.Grave, WZone.Board); s.FutureSummons++; break;
                    case WKind.ViceRecover:
                        s.Move(a.Target, WZone.Grave, WZone.Hand); break;
                    case WKind.MaidSummon:
                        s.Move(a.Source, WZone.Hand, WZone.Board);
                        s.Pending = CardId.MaidVerre; s.FutureSummons++; break;
                    case WKind.Unveiling:
                        s.Move(a.Target, WZone.Hand, WZone.Board); s.FutureSummons++; break;
                    case WKind.SetTrap:
                        s.Move(a.Source, WZone.Hand, WZone.Backrow); break;
                }
                return s;
            }

            private static bool IsExtra(int id)
            {
                return id == CardId.Pupils || id == CardId.ViceMadame ||
                    id == CardId.MadameRillie || id == CardId.RedEyesDarkDragoon ||
                    id == CardId.DarkMagicianOfDestruction;
            }

            private static double Potential(WState s)
            {
                // Route-book milestones guide beam survival but never inflate terminal quality.
                double p = 0;
                if (s.Has(CardId.Walpurgis, WZone.Backrow)) p += 12;
                if (s.Active(CardId.Genni)) p += 8;
                if (s.Has(CardId.Genni, WZone.Grave) && s.Has(CardId.Creation, WZone.Grave)) p += 15;
                if (s.Has(CardId.Celebration, WZone.Grave)) p += 8;
                if (s.Active(CardId.Marka) && !s.ExtraLock) p += 20;
                if (s.Active(CardId.DarkMagicianOfDestruction)) p += 30;
                if (s.Has(CardId.GazeOfTimaeus, WZone.Hand)) p += 20;
                if (s.Active(CardId.Terracottan) && s.Active(CardId.RedEyesDarkDragoon)) p += 35;
                if (s.Active(CardId.Pupils) && s.Has(CardId.Celebration, WZone.Hand)) p += 40;
                if (s.Pending != 0) p += 30;
                return s.Maxx ? p * .2 : p;
            }

            public static double Evaluate(WState s)
            {
                int spells = s.In(WZone.Hand).Where(IsModelSpell).Sum(id => s.Count(id, WZone.Hand));
                bool witchBoard = s.In(WZone.Board).Any(IsWitchMaterial);
                // Evaluate usable interaction after the upcoming End Phase, including
                // the named recovery restrictions. Genni's copy does not spend Creation.
                int recoverable = witchBoard ? new[] { CardId.Creation, CardId.ConfusionConfession,
                    CardId.Unveiling }.Count(id => s.Has(id, WZone.Grave) &&
                    !s.Used.Contains(id + ":" + (id == CardId.Creation ? WKind.Creation :
                    id == CardId.Unveiling ? WKind.Unveiling : WKind.Fusion))) : 0;
                bool cost = spells + recoverable > 0 || s.Has(CardId.Bystreet, WZone.Backrow);
                double score = s.Size(WZone.Hand) * 2 + s.UnknownHand * 2 + recoverable * 2;
                foreach (int id in s.In(WZone.Board))
                {
                    double value = 3;
                    if (id == CardId.RedEyesDarkDragoon)
                        value = s.Size(WZone.Hand) + s.UnknownHand > 0 ? 40 : 18;
                    if (id == CardId.Haine) value = cost ? 23 : 9;
                    if (id == CardId.MadameVerre) value = cost ? 24 : 10;
                    if (id == CardId.ViceMadame) value = 32;
                    if (id == CardId.Pupils) value = 16;
                    if (id == CardId.MadameRillie) value = 8;
                    if (id == CardId.MaidGenni) value = 14;
                    if (id == CardId.Edel && s.Has(CardId.RedEyesDarkDragoon, WZone.Grave)) value = 8;
                    if (s.Disabled.Contains(id)) value = 2;
                    // Multiple copies share hard once-per-name interaction limits.
                    // Count extra bodies, not another full set of negates/recoveries.
                    score += value + 3 * (s.Count(id, WZone.Board) - 1);
                }
                bool highWitch = s.In(WZone.Board).Any(id => new[]
                {
                    CardId.Marka, CardId.Haine, CardId.MadameVerre, CardId.Edel,
                    CardId.Pupils, CardId.MadameRillie, CardId.ViceMadame
                }.Contains(id));
                if (s.Has(CardId.Distortion, WZone.Backrow)) score += highWitch ? 25 : 1;
                if (s.Has(CardId.Walpurgis, WZone.Backrow)) score += 3;
                foreach (int id in WitchSpells.Where(id => s.Has(id, WZone.Grave))) score += 1;
                if (s.Active(CardId.Pupils) && s.Has(CardId.ConfusionConfession, WZone.Hand)) score += 5;
                score -= s.FutureSummons * (s.Maxx ? 21 : .15);
                score -= s.Steps * .3;
                // A board waiting on an optional trigger is not a completed endpoint.
                if (s.Pending != 0) score -= 15;
                return score;
            }

            public static string BoardSummary(WState s)
            {
                return string.Join(",", s.In(WZone.Board)) + ";hand=" +
                    s.Size(WZone.Hand) + ";draws=" + s.FutureSummons;
            }
        }
    }
}
