using System;
using System.Collections.Generic;

public class HandArea
{
    private List<CardData> handCards = new List<CardData>();
    private HashSet<CardData> selectedCards = new HashSet<CardData>();
    private int version = 0;

    public int Count => handCards.Count;
    public IReadOnlyList<CardData> HandCards => handCards;
    public int Version => version;

    /// <summary>手牌上限（任何情况下都不会超过这个数量）</summary>
    public int MaxSize = 15;

    /// <summary>
    /// 手牌变化事件（添加/移除/清空）
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 添加牌到手牌；超过上限的部分不会加入，通过返回值返回（由调用方处理，如放入弃牌堆）
    /// </summary>
    public List<CardData> AddCards(List<CardData> cards)
    {
        var overflow = new List<CardData>();
        if (cards != null)
        {
            foreach (var c in cards)
            {
                if (handCards.Count >= MaxSize) overflow.Add(c);
                else handCards.Add(c);
            }
        }
        SortHand();
        version++;
        OnChanged?.Invoke();
        return overflow;
    }

    /// <summary>
    /// 手牌排序：先按点数(2<...<A)，再按花色(黑桃>红心>梅花>方块)
    /// </summary>
    public void SortHand()
    {
        handCards.Sort((a, b) =>
        {
            int rankCompare = a.EffectiveRank.CompareTo(b.EffectiveRank);
            if (rankCompare != 0) return rankCompare;
            return ((int)a.EffectiveSuit).CompareTo((int)b.EffectiveSuit);
        });
    }

    /// <summary>
    /// 移除牌（同时清除选中状态）
    /// </summary>
    public void RemoveCards(List<CardData> cards)
    {
        foreach (var card in cards)
        {
            handCards.Remove(card);
            selectedCards.Remove(card);
        }
        version++;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 切换牌的选中状态
    /// </summary>
    public void ToggleSelect(CardData card)
    {
        if (selectedCards.Contains(card))
        {
            selectedCards.Remove(card);
        }
        else
        {
            selectedCards.Add(card);
        }
    }

    /// <summary>
    /// 获取所有选中的牌
    /// </summary>
    public List<CardData> GetSelectedCards()
    {
        return new List<CardData>(selectedCards);
    }

    /// <summary>
    /// 清除所有选中
    /// </summary>
    public void ClearSelection()
    {
        selectedCards.Clear();
    }

    /// <summary>
    /// 预览当前选中牌的牌型
    /// </summary>
    public HandTypeResult PreviewHandType()
    {
        return HandEvaluator.Evaluate(selectedCards);
    }
}