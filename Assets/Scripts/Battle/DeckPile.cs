using System;
using System.Collections.Generic;
using UnityEngine;

public class DeckPile
{
    private List<CardData> cards = new List<CardData>();
    private List<CardData> discard = new List<CardData>();

    public int Count => cards.Count;

    /// <summary>弃牌堆数量</summary>
    public int DiscardCount => discard.Count;

    /// <summary>弃牌堆快照</summary>
    public List<CardData> GetDiscard() => new List<CardData>(discard);

    /// <summary>牌堆快照（查看用；顺序为内部存储顺序）</summary>
    public List<CardData> GetCards() => new List<CardData>(cards);

    /// <summary>某张牌是否在弃牌堆里</summary>
    public bool HasInDiscard(CardData card) => card != null && discard.Contains(card);

    /// <summary>从弃牌堆取出一张牌（成功返回 true）</summary>
    public bool TakeFromDiscard(CardData card)
    {
        if (card == null) return false;
        if (!discard.Remove(card)) return false;
        OnDiscardCountChanged?.Invoke(discard.Count);
        return true;
    }

    /// <summary>
    /// 牌堆数量变化事件
    /// </summary>
    public event Action<int> OnCountChanged;

    /// <summary>弃牌堆数量变化事件</summary>
    public event Action<int> OnDiscardCountChanged;

    /// <summary>
    /// 初始化牌堆
    /// </summary>
    public void Init(List<CardData> initialCards)
    {
        cards = new List<CardData>(initialCards);
        Shuffle();
        OnCountChanged?.Invoke(cards.Count);
    }

    /// <summary>
    /// 从牌堆顶部抽取指定数量的牌（牌堆为空则抽多少算多少）
    /// </summary>
    public List<CardData> Draw(int count)
    {
        var drawn = new List<CardData>();

        for (int i = 0; i < count; i++)
        {
            if (cards.Count == 0) break;

            drawn.Add(cards[cards.Count - 1]);
            cards.RemoveAt(cards.Count - 1);
            OnCountChanged?.Invoke(cards.Count);
        }
        return drawn;
    }

    /// <summary>
    /// 把牌放入弃牌堆（不立刻洗回牌堆；回合开始时统一洗回）
    /// </summary>
    public void Discard(List<CardData> discarded)
    {
        if (discarded == null || discarded.Count == 0) return;

        discard.AddRange(discarded);
        OnDiscardCountChanged?.Invoke(discard.Count);
    }

    /// <summary>
    /// 把牌放回牌堆（不洗牌）——用于「手牌满了没抽出来」的牌
    /// </summary>
    public void ReturnToDeck(List<CardData> returned)
    {
        if (returned == null || returned.Count == 0) return;

        cards.AddRange(returned);
        OnCountChanged?.Invoke(cards.Count);
    }

    /// <summary>
    /// 把指定牌从牌堆里取出（用于「吞噬」等移除效果；成功返回 true）
    /// </summary>
    public bool RemoveFromDeck(CardData card)
    {
        if (card == null) return false;
        if (!cards.Remove(card)) return false;
        OnCountChanged?.Invoke(cards.Count);
        return true;
    }

    /// <summary>
    /// 把弃牌堆全部放回牌堆并重新洗牌（每回合开始时调用）
    /// </summary>
    public void ReshuffleDiscardIntoDeck()
    {
        if (discard.Count == 0) return;

        cards.AddRange(discard);
        discard.Clear();
        Shuffle();
        OnCountChanged?.Invoke(cards.Count);
        OnDiscardCountChanged?.Invoke(0);
    }

    /// <summary>
    /// 把指定牌移到牌堆顶（下一次抽牌优先抽到，不洗牌）
    /// </summary>
    public void MoveToTop(List<CardData> toTop)
    {
        if (toTop == null || toTop.Count == 0) return;

        foreach (var c in toTop)
            cards.Remove(c);
        cards.AddRange(toTop);
        OnCountChanged?.Invoke(cards.Count);
    }

    /// <summary>
    /// 洗牌（Fisher-Yates 算法）
    /// </summary>
    private void Shuffle()
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            CardData temp = cards[i];
            cards[i] = cards[j];
            cards[j] = temp;
        }
    }
}
