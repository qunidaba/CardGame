using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 牌型效果列表的小工具（纯函数，无状态）：
/// 叠伤害 / 乘倍率 / 加某项数值 / 判断顺子·同花类牌型。
/// 从 BattleManager 抽出（纯搬运）。
/// </summary>
public static class HandEffectUtil
{
    /// <summary>把额外伤害叠加到牌型效果列表的伤害项上</summary>
    public static void AddDamageToEffects(List<HandEffectTable.HandEffect> effects, int amount)
    {
        if (amount == 0) return;
        var dmg = effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage);
        if (dmg != null)
        {
            dmg.value += amount;
            dmg.description = $"造成 {dmg.value} 点伤害";
        }
        else
        {
            effects.Add(new HandEffectTable.HandEffect
            {
                effectType = HandEffectTable.EffectType.Damage,
                value = amount,
                description = $"造成 {amount} 点伤害"
            });
        }
    }

    /// <summary>把牌型效果列表里的伤害乘以倍率</summary>
    public static void MultiplyDamageEffect(List<HandEffectTable.HandEffect> effects, float mult)
    {
        var dmg = effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage);
        if (dmg != null)
        {
            dmg.value = Mathf.RoundToInt(dmg.value * mult);
            dmg.description = $"造成 {dmg.value} 点伤害";
        }
    }

    /// <summary>把数值叠加到指定类型的效果项上（不存在则新建）</summary>
    public static void AddEffectValue(List<HandEffectTable.HandEffect> effects, HandEffectTable.EffectType type, int amount)
    {
        HandEffectTable.AddOrMerge(effects, type, amount);
    }

    /// <summary>是否顺子类牌型（含同花顺）</summary>
    public static bool IsStraightHand(HandTypeResult r)
    {
        if (r == null) return false;
        switch (r.type)
        {
            case HandType.Straight3:
            case HandType.Straight4:
            case HandType.Straight5:
            case HandType.StraightFlush3:
            case HandType.StraightFlush4:
            case HandType.StraightFlush5:
                return true;
            default:
                return false;
        }
    }

    /// <summary>是否同花类牌型（含同花顺）</summary>
    public static bool IsFlushHand(HandTypeResult r)
    {
        if (r == null) return false;
        switch (r.type)
        {
            case HandType.Flush3:
            case HandType.Flush4:
            case HandType.Flush5:
            case HandType.StraightFlush3:
            case HandType.StraightFlush4:
            case HandType.StraightFlush5:
                return true;
            default:
                return false;
        }
    }
}