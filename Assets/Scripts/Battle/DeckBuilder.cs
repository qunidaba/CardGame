using System.Collections.Generic;
using Roguelike;

public static class DeckBuilder
{
    /// <summary>
    /// 构建标准52张扑克牌
    /// </summary>
    public static List<CardData> BuildStandardDeck()
    {
        var deck = new List<CardData>();
        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            for (int rank = 2; rank <= 14; rank++)
            {
                deck.Add(new CardData { rank = rank, suit = suit });
            }
        }
        return deck;
    }

    /// <summary>
    /// 根据 RunData 构建带附魔的 52 张牌
    /// </summary>
    public static List<CardData> BuildDeckWithEnchantments(RunData runData)
    {
        var deck = new List<CardData>();
        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            for (int rank = 2; rank <= 14; rank++)
            {
                var card = new CardData { rank = rank, suit = suit };
                string key = $"{suit}_{rank}";
                if (runData.cardEnchantmentIds.TryGetValue(key, out var enchIds))
                {
                    card.enchantmentIds = new List<int>(enchIds);
                }
                deck.Add(card);
            }
        }
        return deck;
    }
}