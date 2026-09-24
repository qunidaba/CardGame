using System.Collections.Generic;

/// <summary>
/// 牌型效果表：将牌型映射为具体效果
/// </summary>
public class HandEffectTable
{
    public enum EffectType
    {
        Damage,     // 造成伤害
        Defense,    // 获得防御
        DrawCard,   // 抽牌
        Heal,       // 回血
        HealPercentOfDamage  // 按本次造成伤害的百分比回血（吸血）
    }

    public class HandEffect
    {
        public EffectType effectType;
        public int value;
        public string description;
    }

    /// <summary>
    /// 根据牌型获取对应效果列表
    /// </summary>
    public static List<HandEffect> GetEffects(HandTypeResult result)
    {
        if (!result.IsValid) return new List<HandEffect>();

        var effects = new List<HandEffect>();

        switch (result.type)
        {
            case HandType.OnePair:
                effects.Add(CreateEffect(EffectType.Damage, 6, "造成 6 点伤害"));
                break;

            case HandType.TwoConsecutivePairs:
                effects.Add(CreateEffect(EffectType.Damage, 14, "造成 14 点伤害"));
                effects.Add(CreateEffect(EffectType.Defense, 5, "获得 5 点防御"));
                break;

            case HandType.ThreeOfAKind:
                effects.Add(CreateEffect(EffectType.Damage, 9, "造成 9 点伤害"));
                effects.Add(CreateEffect(EffectType.Defense, 4, "获得 4 点防御"));
                break;

            case HandType.Straight3:
                effects.Add(CreateEffect(EffectType.Damage, 8, "造成 9 点伤害"));
                effects.Add(CreateEffect(EffectType.DrawCard, 1, "额外抽 1 张牌"));
                break;

            case HandType.Straight4:
                effects.Add(CreateEffect(EffectType.Damage, 11, "造成 11 点伤害"));
                effects.Add(CreateEffect(EffectType.DrawCard, 2, "额外抽 2 张牌"));
                effects.Add(CreateEffect(EffectType.Defense, 3, "获得 3 点防御"));
                break;

            case HandType.Straight5:
                effects.Add(CreateEffect(EffectType.Damage, 14, "造成 14 点伤害"));
                effects.Add(CreateEffect(EffectType.DrawCard, 3, "额外抽 3 张牌"));
                effects.Add(CreateEffect(EffectType.Defense, 4, "获得 4 点防御"));
                break;

            case HandType.Flush3:
                effects.Add(CreateEffect(EffectType.Damage, 12, "造成 12 点伤害"));
                effects.Add(CreateEffect(EffectType.Defense, 2, "获得 2 点防御"));
                break;

            case HandType.Flush4:
                effects.Add(CreateEffect(EffectType.Damage, 15, "造成 15 点伤害"));
                effects.Add(CreateEffect(EffectType.Defense, 4, "获得 4 点防御"));
                break;

            case HandType.Flush5:
                effects.Add(CreateEffect(EffectType.Damage, 18, "造成 18 点伤害"));
                effects.Add(CreateEffect(EffectType.Defense, 6, "获得 6 点防御"));
                break;

            case HandType.FullHouse:
                effects.Add(CreateEffect(EffectType.Damage, 13, "造成 13 点伤害"));
                effects.Add(CreateEffect(EffectType.Defense, 5, "获得 5 点防御"));
                break;

            case HandType.FourOfAKind:
                effects.Add(CreateEffect(EffectType.Damage, 20, "造成 20 点伤害"));
                effects.Add(CreateEffect(EffectType.Heal, 3, "回复 3 点血量"));
                effects.Add(CreateEffect(EffectType.Defense, 6, "获得 6 点防御"));
                effects.Add(CreateEffect(EffectType.DrawCard, 2, "额外抽 2 张牌"));
                break;

            case HandType.StraightFlush3:
                effects.Add(CreateEffect(EffectType.Damage, 12, "造成 12 点伤害"));
                effects.Add(CreateEffect(EffectType.DrawCard, 2, "额外抽 2 张牌"));
                effects.Add(CreateEffect(EffectType.Defense, 4, "获得 4 点防御"));
                break;

            case HandType.StraightFlush4:
                effects.Add(CreateEffect(EffectType.Damage, 15, "造成 15 点伤害"));
                effects.Add(CreateEffect(EffectType.DrawCard, 3, "额外抽 3 张牌"));
                effects.Add(CreateEffect(EffectType.Defense, 5, "获得 5 点防御"));
                break;

            case HandType.StraightFlush5:
                effects.Add(CreateEffect(EffectType.Damage, 20, "造成 20 点伤害"));
                effects.Add(CreateEffect(EffectType.DrawCard, 4, "额外抽 4 张牌"));
                effects.Add(CreateEffect(EffectType.Defense, 6, "获得 6 点防御"));
                break;
        }

        return effects;
    }

    private static HandEffect CreateEffect(EffectType type, int value, string description)
    {
        return new HandEffect { effectType = type, value = value, description = description };
    }

    /// <summary>把数值叠加到指定类型的效果上（不存在则新建），并自动刷新描述</summary>
    public static void AddOrMerge(List<HandEffect> effects, EffectType type, int amount)
    {
        if (effects == null || amount == 0) return;

        var e = effects.Find(x => x.effectType == type);
        if (e != null)
        {
            e.value += amount;
            e.description = Describe(type, e.value);
        }
        else
        {
            effects.Add(new HandEffect { effectType = type, value = amount, description = Describe(type, amount) });
        }
    }

    /// <summary>按效果类型生成描述文本</summary>
    public static string Describe(EffectType type, int value)
    {
        switch (type)
        {
            case EffectType.Damage: return $"造成 {value} 点伤害";
            case EffectType.Defense: return $"获得 {value} 点防御";
            case EffectType.DrawCard: return $"额外抽 {value} 张牌";
            case EffectType.Heal: return $"回复 {value} 点血量";
            case EffectType.HealPercentOfDamage: return $"回复伤害的 {value}%";
            default: return $"+{value}";
        }
    }
}