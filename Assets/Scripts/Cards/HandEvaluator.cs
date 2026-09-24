using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 牌型枚举（从大到小排列）
/// </summary>
public enum HandType
{
    OnePair,              // 一对（仅2张）
    TwoConsecutivePairs,  // 两连对（4张，如3344、5566）
    ThreeOfAKind,         // 三条（仅3张）
    Straight3,            // 3张顺子
    Straight4,            // 4张顺子
    Straight5,            // 5张顺子
    Flush3,               // 3张同花
    Flush4,               // 4张同花
    Flush5,               // 5张同花
    FullHouse,            // 葫芦（3+2，共5张）
    FourOfAKind,          // 四条（仅4张）
    StraightFlush3,       // 3张同花顺
    StraightFlush4,       // 4张同花顺
    StraightFlush5        // 5张同花顺
}

/// <summary>
/// 牌型判定结果
/// </summary>
public class HandTypeResult
{
    public HandType type;
    public bool IsValid;
    public int cardCount;   // 参与判定的牌数（供附魔读取）

    public static HandTypeResult Invalid => new HandTypeResult { IsValid = false };

    public static HandTypeResult Create(HandType handType)
    {
        return new HandTypeResult { IsValid = true, type = handType };
    }
}

/// <summary>
/// 手牌牌型评估器
/// </summary>
public static class HandEvaluator
{
    /// <summary>遗物「圣锤」：三条 + 比它大 1 的牌 也视为四条（由 BattleManager 每场设置）</summary>
    public static bool HammerEnabled = false;

    /// <summary>
    /// 评估选中牌的牌型
    /// </summary>
    public static HandTypeResult Evaluate(IEnumerable<CardData> cards)
    {
        if (cards == null || cards.Count() < 2 || cards.Count() > 5)
            return HandTypeResult.Invalid;

        var cardList = cards.ToList();
        var result = EvaluateType(cardList);
        if (result != null && result.IsValid)
            result.cardCount = cardList.Count;
        return result;
    }

    private static HandTypeResult EvaluateType(List<CardData> cardList)
    {
        int count = cardList.Count;

        // 按优先级从高到低检查
        var straightFlush = CheckStraightFlush(cardList);
        if (straightFlush != HandType.OnePair) // OnePair 是最小值，用作无效标记
            return HandTypeResult.Create(straightFlush);
        if (IsFourOfAKind(cardList))
            return HandTypeResult.Create(HandType.FourOfAKind);
        if (IsFullHouse(cardList))
            return HandTypeResult.Create(HandType.FullHouse);
        var flush = CheckFlush(cardList);
        if (flush != HandType.OnePair)
            return HandTypeResult.Create(flush);
        var straight = CheckStraight(cardList);
        if (straight != HandType.OnePair)
            return HandTypeResult.Create(straight);
        if (IsTwoConsecutivePairs(cardList))
            return HandTypeResult.Create(HandType.TwoConsecutivePairs);
        if (IsThreeOfAKind(cardList))
            return HandTypeResult.Create(HandType.ThreeOfAKind);
        if (IsOnePair(cardList))
            return HandTypeResult.Create(HandType.OnePair);

        return HandTypeResult.Invalid;
    }

    private static HandType CheckStraightFlush(List<CardData> cards)
    {
        if (cards.Count < 3) return HandType.OnePair;
        if (!IsFlush(cards) || !IsStraight(cards)) return HandType.OnePair;
        
        return cards.Count switch
        {
            3 => HandType.StraightFlush3,
            4 => HandType.StraightFlush4,
            5 => HandType.StraightFlush5,
            _ => HandType.OnePair
        };
    }

    private static HandType CheckFlush(List<CardData> cards)
    {
        if (cards.Count < 3) return HandType.OnePair;
        if (!IsFlush(cards)) return HandType.OnePair;
        
        return cards.Count switch
        {
            3 => HandType.Flush3,
            4 => HandType.Flush4,
            5 => HandType.Flush5,
            _ => HandType.OnePair
        };
    }

    private static HandType CheckStraight(List<CardData> cards)
    {
        if (cards.Count < 3) return HandType.OnePair;
        if (!IsStraight(cards)) return HandType.OnePair;
        
        return cards.Count switch
        {
            3 => HandType.Straight3,
            4 => HandType.Straight4,
            5 => HandType.Straight5,
            _ => HandType.OnePair
        };
    }

    private static bool IsFourOfAKind(List<CardData> cards)
    {
        if (cards.Count != 4) return false;
        if (cards.GroupBy(c => c.EffectiveRank).Any(g => g.Count() == 4)) return true;

        // 遗物「圣锤」：三条 + 一张比它大 1 的牌 → 视为四条
        if (HammerEnabled)
        {
            var groups = cards.GroupBy(c => c.EffectiveRank).OrderByDescending(g => g.Count()).ToList();
            if (groups.Count == 2 && groups[0].Count() == 3 && groups[1].Count() == 1
                && groups[1].Key == groups[0].Key + 1)
                return true;
        }
        return false;
    }

    private static bool IsFullHouse(List<CardData> cards)
    {
        if (cards.Count != 5) return false;
        var groups = cards.GroupBy(c => c.EffectiveRank).OrderByDescending(g => g.Count());
        return groups.Count() == 2
            && groups.First().Count() == 3
            && groups.Skip(1).First().Count() == 2;
    }

    private static bool IsTwoConsecutivePairs(List<CardData> cards)
    {
        if (cards.Count != 4) return false;
        
        var groups = cards.GroupBy(c => c.EffectiveRank).OrderBy(g => g.Key).ToList();
        // 必须是两对，且点数连续
        if (groups.Count != 2) return false;
        if (groups[0].Count() != 2 || groups[1].Count() != 2) return false;
        
        int rank1 = groups[0].Key;
        int rank2 = groups[1].Key;
        
        // 检查是否连续（如 3-4, 10-J, J-Q, Q-K, K-A）
        // A(14) 和 2 不连续
        return rank2 - rank1 == 1;
    }

    private static bool IsThreeOfAKind(List<CardData> cards)
    {
        return cards.Count == 3 && cards.GroupBy(c => c.EffectiveRank).Any(g => g.Count() == 3);
    }

    private static bool IsOnePair(List<CardData> cards)
    {
        return cards.Count == 2 && cards.GroupBy(c => c.EffectiveRank).Any(g => g.Count() == 2);
    }

    private static bool IsFlush(List<CardData> cards)
    {
        if (cards.Count < 3) return false;
        var firstSuit = cards[0].EffectiveSuit;
        return cards.All(c => c.EffectiveSuit == firstSuit);
    }

    private static bool IsStraight(List<CardData> cards)
    {
        if (cards.Count < 3) return false;

        // 先检查是否有重复点数，有重复就不是顺子
        var ranks = cards.Select(c => c.EffectiveRank).ToList();
        if (ranks.Distinct().Count() != ranks.Count)
            return false;

        // 排序
        var sortedRanks = ranks.OrderBy(r => r).ToList();

        // 处理 A 的特殊情况：A-2-3-4-5或A-2-3-4或A-2-3（A=14 当作 1 使用）
        if (sortedRanks.Contains(14) && sortedRanks.Contains(2) && sortedRanks.Contains(3) && sortedRanks.Contains(4) && sortedRanks.Contains(5))
            return true;
        if (sortedRanks.Contains(14) && sortedRanks.Contains(2) && sortedRanks.Contains(3) && sortedRanks.Contains(4) && cards.Count == 4)
            return true;
        if (sortedRanks.Contains(14) && sortedRanks.Contains(2) && sortedRanks.Contains(3) && cards.Count == 3)
            return true;

        // 检查是否连续
        for (int i = 0; i < sortedRanks.Count - 1; i++)
        {
            if (sortedRanks[i + 1] - sortedRanks[i] != 1)
                return false;
        }
        return true;
    }
}

/// <summary>
/// 敌人被动「弱点」用的牌型分类：3/4/5 张的顺子算同一类，同花、同花顺同理。
/// </summary>
public enum WeaknessType
{
    OnePair,               // 一对
    TwoConsecutivePairs,   // 两连对
    ThreeOfAKind,          // 三条
    Straight,              // 顺子（3/4/5 张算一类）
    Flush,                 // 同花（3/4/5 张算一类）
    FullHouse,             // 葫芦
    FourOfAKind,           // 四条
    StraightFlush          // 同花顺（3/4/5 张算一类）
}

/// <summary>弱点牌型的分类映射与文案</summary>
public static class WeaknessInfo
{
    /// <summary>全部弱点分类（随机刷新用）</summary>
    public static readonly WeaknessType[] All =
    {
        WeaknessType.OnePair,
        WeaknessType.TwoConsecutivePairs,
        WeaknessType.ThreeOfAKind,
        WeaknessType.Straight,
        WeaknessType.Flush,
        WeaknessType.FullHouse,
        WeaknessType.FourOfAKind,
        WeaknessType.StraightFlush
    };

    /// <summary>把具体牌型映射到弱点分类（3/4/5 张的同族算一类）</summary>
    public static WeaknessType FromHandType(HandType type)
    {
        switch (type)
        {
            case HandType.OnePair: return WeaknessType.OnePair;
            case HandType.TwoConsecutivePairs: return WeaknessType.TwoConsecutivePairs;
            case HandType.ThreeOfAKind: return WeaknessType.ThreeOfAKind;
            case HandType.Straight3:
            case HandType.Straight4:
            case HandType.Straight5: return WeaknessType.Straight;
            case HandType.Flush3:
            case HandType.Flush4:
            case HandType.Flush5: return WeaknessType.Flush;
            case HandType.FullHouse: return WeaknessType.FullHouse;
            case HandType.FourOfAKind: return WeaknessType.FourOfAKind;
            case HandType.StraightFlush3:
            case HandType.StraightFlush4:
            case HandType.StraightFlush5: return WeaknessType.StraightFlush;
            default: return WeaknessType.Straight;
        }
    }

    public static string Name(WeaknessType w)
    {
        switch (w)
        {
            case WeaknessType.OnePair: return "一对";
            case WeaknessType.TwoConsecutivePairs: return "两连对";
            case WeaknessType.ThreeOfAKind: return "三条";
            case WeaknessType.Straight: return "顺子";
            case WeaknessType.Flush: return "同花";
            case WeaknessType.FullHouse: return "葫芦";
            case WeaknessType.FourOfAKind: return "四条";
            case WeaknessType.StraightFlush: return "同花顺";
            default: return w.ToString();
        }
    }
}
