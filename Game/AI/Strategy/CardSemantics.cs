using System.Collections.Generic;
using YGOSharp.OCGWrapper.Enums;

namespace Skynet.Game.AI.Strategy
{
    public enum EffectRole
    {
        Unknown,
        Draw,
        Search,
        DestroyEnemyMonsters,
        DestroyAllMonsters,
        DestroyEnemySpell,
        Revive,
        BattleTrap,
        SummonTrap,
        DestroyFaceupMonster
    }

    /// <summary>
    /// Reviewed semantics for a small bootstrap pool. These are card capabilities,
    /// shared by every deck, rather than executable deck combo lists.
    /// Selection stage and hint were checked against ygopro-scripts/c{id}.lua.
    /// Unlisted optional effects are deliberately unsupported.
    /// </summary>
    public sealed class CardSemantics
    {
        public int CardId { get; private set; }
        public EffectRole Role { get; private set; }
        public int SelectionHint { get; private set; }
        public bool SelectDuringResolution { get; private set; }

        private CardSemantics(int cardId, EffectRole role, int hint, bool resolution)
        {
            CardId = cardId;
            Role = role;
            SelectionHint = hint;
            SelectDuringResolution = resolution;
        }

        private static readonly IDictionary<int, CardSemantics> Entries =
            new Dictionary<int, CardSemantics>
            {
                { 55144522, new CardSemantics(55144522, EffectRole.Draw, 0, false) },
                { 12580477, new CardSemantics(12580477, EffectRole.DestroyEnemyMonsters, 0, false) },
                { 53129443, new CardSemantics(53129443, EffectRole.DestroyAllMonsters, 0, false) },
                { 83764718, new CardSemantics(83764718, EffectRole.Revive, HintMsg.SpSummon, false) },
                { 5318639, new CardSemantics(5318639, EffectRole.DestroyEnemySpell, HintMsg.Destroy, false) },
                { 44095762, new CardSemantics(44095762, EffectRole.BattleTrap, 0, false) },
                { 4206964, new CardSemantics(4206964, EffectRole.SummonTrap, 0, false) },
                { 66788016, new CardSemantics(66788016, EffectRole.DestroyFaceupMonster, HintMsg.Destroy, true) },
                { 97169186, new CardSemantics(97169186, EffectRole.DestroyFaceupMonster, HintMsg.Destroy, true) },
                { 32807846, new CardSemantics(32807846, EffectRole.Search, HintMsg.AddToHand, true) },
                { 26202165, new CardSemantics(26202165, EffectRole.Search, HintMsg.AddToHand, true) }
            };

        public static CardSemantics Find(int cardId)
        {
            CardSemantics result;
            return Entries.TryGetValue(cardId, out result) ? result : null;
        }

        public static CardSemantics Find(ClientCard card)
        {
            return card == null || card.Id == 0 ? null : Find(card.GetNonAltartCode());
        }

        public static bool HasType(ClientCard card, CardType type)
        {
            if (card == null || card.Id == 0)
                return false;
            int cardType = card.Location == CardLocation.MonsterZone && card.IsFaceup()
                ? card.Type : card.Data != null ? card.Data.Type : card.Type;
            return (cardType & (int)type) != 0;
        }

        public static int Attack(ClientCard card)
        {
            if (card == null || card.Id == 0)
                return 0;
            return card.Location == CardLocation.MonsterZone && card.IsFaceup()
                ? card.Attack : card.Data != null ? card.Data.Attack : card.Attack;
        }

        public static int Defense(ClientCard card)
        {
            if (card == null || card.Id == 0)
                return 0;
            return card.Location == CardLocation.MonsterZone && card.IsFaceup()
                ? card.Defense : card.Data != null ? card.Data.Defense : card.Defense;
        }

        public static int Level(ClientCard card)
        {
            if (card == null || card.Id == 0)
                return 0;
            return card.Location == CardLocation.MonsterZone && card.IsFaceup()
                ? card.Level : card.Data != null ? card.Data.Level : card.Level;
        }
    }
}
