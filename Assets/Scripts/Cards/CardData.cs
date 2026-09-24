using System;
using System.Collections.Generic;
using Roguelike.Data;

public enum Suit
{
    Spade,     // 黑桃
    Heart,     // 红心
    Club,      // 梅花
    Diamond    // 方块
}

public class CardData : IEquatable<CardData>
{
    public int rank;      // 2~14（11=J, 12=Q, 13=K, 14=A）
    public Suit suit;

    // 运行时附魔 ID 列表（快照；实时读取优先走 RunData）
    public List<int> enchantmentIds = new List<int>();

    // ===== 牌自身定位覆盖（运行时，仅本场有效）=====
    // 判定用（影响牌型）：万能牌/变色/镜牌
    public int judgeRank = 0;    // 0 = 用 rank
    public int judgeSuit = -1;   // -1 = 用 suit
    // 附魔键用（影响读取哪张牌的附魔）：镜牌
    public int keyRank = 0;      // 0 = 用 rank
    public int keySuit = -1;     // -1 = 用 suit

    /// <summary>判定用点数</summary>
    public int EffectiveRank => judgeRank > 0 ? judgeRank : rank;

    /// <summary>判定用花色</summary>
    public Suit EffectiveSuit => judgeSuit >= 0 ? (Suit)judgeSuit : suit;

    /// <summary>附魔键点数</summary>
    public int EnchantRank => keyRank > 0 ? keyRank : rank;

    /// <summary>附魔键花色</summary>
    public Suit EnchantSuit => keySuit >= 0 ? (Suit)keySuit : suit;

    public string DisplayName => $"{RankLabel(EffectiveRank)}{GetSuitSymbol(EffectiveSuit)}";

    /// <summary>点数显示：11~14 显示为 J/Q/K/A</summary>
    public static string RankLabel(int rank)
    {
        switch (rank)
        {
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            case 14: return "A";
            default: return rank.ToString();
        }
    }

    private static string GetSuitSymbol(Suit s)
    {
        switch (s)
        {
            case Suit.Spade: return "\u2660";
            case Suit.Heart: return "\u2665";
            case Suit.Club: return "\u2663";
            case Suit.Diamond: return "\u2666";
            default: return "";
        }
    }

    public bool Equals(CardData other)
    {
        if (other == null) return false;
        return rank == other.rank && suit == other.suit;
    }

    /// <summary>实时附魔 ID（按附魔键读 RunData，保证作弊/事件/镜牌改动能反映）</summary>
    public List<int> GetLiveEnchantmentIds()
    {
        var rd = Roguelike.RunDirector.Instance?.RunData;
        if (rd != null) return rd.GetCardEnchantments(EnchantRank, EnchantSuit);
        return enchantmentIds;
    }

    public List<string> GetEnchantmentNames()
    {
        var names = new List<string>();
        var tempIds = Roguelike.RunDirector.Instance?.RunData?.GetTempEnchantments(EnchantRank, EnchantSuit);

        foreach (int id in GetLiveEnchantmentIds())
        {
            var ench = Roguelike.Data.ConfigLoader.GetEnchantment(id);
            if (ench == null) continue;

            // 本场临时附魔（如「魔导」给的）加标记，战斗结束会消失
            bool temp = tempIds != null && tempIds.Contains(id);
            names.Add(temp ? $"{ench.name}（临）" : ench.name);
        }
        return names;
    }

    // ===== 定位能力与行为标记（来自附魔）=====

    private IEnumerable<EnchantmentData> LiveEnchantments()
    {
        foreach (int id in GetLiveEnchantmentIds())
        {
            var e = ConfigLoader.GetEnchantment(id);
            if (e != null) yield return e;
        }
    }

    public bool HasModifier(string effectType)
    {
        foreach (var e in LiveEnchantments())
            foreach (var eff in e.effects)
                if (eff.type == effectType) return true;
        return false;
    }

    public bool CanWildcard => HasModifier("Wildcard");
    public bool CanSuitShift => HasModifier("SuitShift");
    public bool CanMirror => HasModifier("Mirror");
    public bool IsReturnToHand => HasModifier("ReturnToHand");
    public bool IsRetain => HasModifier("Retain");
    public bool IsDestiny => HasModifier("Destiny");

    /// <summary>禁锢：此牌无法被打出</summary>
    public bool IsUnplayable => HasModifier("Unplayable");

    /// <summary>割裂：此牌无法与同数字的牌一起打出</summary>
    public bool HasRift => HasModifier("NoSameRank");

    /// <summary>搜寻：打出后可从弃牌堆选一张牌回手</summary>
    public bool HasSearch => HasModifier("SearchDiscard");

    // ===== 定位覆盖操作 =====

    /// <summary>万能牌：手动设定判定点数与花色</summary>
    public void SetJudgeOverride(int rankValue, int suitValue)
    {
        judgeRank = rankValue;
        judgeSuit = suitValue;
    }

    /// <summary>变色：手动设定判定花色</summary>
    public void SetSuitOverride(int suitValue)
    {
        judgeSuit = suitValue;
    }

    /// <summary>镜牌：变为目标牌的复制（判定 + 附魔键一致）</summary>
    public void MirrorCopy(CardData target)
    {
        if (target == null) return;
        judgeRank = target.EffectiveRank;
        judgeSuit = (int)target.EffectiveSuit;
        keyRank = target.EnchantRank;
        keySuit = (int)target.EnchantSuit;
    }

    public void ClearOverrides()
    {
        judgeRank = 0;
        judgeSuit = -1;
        keyRank = 0;
        keySuit = -1;
    }

    public bool HasOverrides => judgeRank != 0 || judgeSuit >= 0 || keyRank != 0 || keySuit >= 0;

    public override bool Equals(object obj) => Equals(obj as CardData);

    public override int GetHashCode() => rank.GetHashCode() ^ ((int)suit).GetHashCode();
}
