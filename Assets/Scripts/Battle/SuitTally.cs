using System;
using System.Collections.Generic;

namespace Roguelike
{
    /// <summary>
    /// 本场战斗的花色统计（供花色命运 / 主命格判定）。
    /// 从 BattleManager 抽出（纯搬运，逻辑未改动）。
    /// </summary>
    public class SuitTally
    {
        private readonly Dictionary<Suit, int> tally = new Dictionary<Suit, int>();
        private Suit lastPlayed = Suit.Spade;
        private readonly Action<Suit, int> onChanged;

        public SuitTally(Action<Suit, int> onChanged = null)
        {
            this.onChanged = onChanged;
        }

        public int GetCount(Suit suit) => tally.TryGetValue(suit, out var v) ? v : 0;

        public Dictionary<Suit, int> GetAll() => new Dictionary<Suit, int>(tally);

        /// <summary>本场战斗打出的最多花色；平局时以最后打出的花色优先</summary>
        public Suit Dominant()
        {
            int max = -1;
            Suit best = lastPlayed;
            foreach (Suit s in Enum.GetValues(typeof(Suit)))
            {
                int c = GetCount(s);
                if (c > max) { max = c; best = s; }
            }
            if (max > 0 && GetCount(lastPlayed) == max)
                best = lastPlayed;
            return best;
        }

        public void Reset()
        {
            tally.Clear();
            lastPlayed = Suit.Spade;
        }

        public void CountPlayed(List<CardData> cards)
        {
            foreach (var card in cards)
            {
                tally[card.suit] = GetCount(card.suit) + 1;
                lastPlayed = card.suit;
            }
            foreach (var card in cards)
                onChanged?.Invoke(card.suit, GetCount(card.suit));
        }
    }
}