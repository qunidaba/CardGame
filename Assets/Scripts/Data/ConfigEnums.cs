namespace Roguelike.Data
{
    // ===== 配置表可选值枚举（编辑器下拉的唯一来源）=====
    // 说明：JSON 里这些字段仍是字符串，枚举名必须与字符串值完全一致。
    // 以后新增类型时，在这里加一个成员即可，配置表编辑器会自动出现该选项。

    public enum Rarity
    {
        Common,   // 普通
        Rare,     // 稀有
        Epic,     // 史诗
        Curse,    // 诅咒（负面附魔专用）
        Event     // 事件（事件专属遗物专用，不进任何随机池）
    }

    /// <summary>事件里「获得某稀有度」的筛选：随机 / 指定 / 某档及以上</summary>
    public enum RarityFilter
    {
        Any,           // 随机
        Common,        // 普通
        Rare,          // 稀有
        Epic,          // 史诗
        RareOrAbove    // 稀有及以上
    }

    /// <summary>事件的随机附魔池（只有事件按这个字段抽附魔，其它随机来源不看它）</summary>
    public enum EnchantPool
    {
        HandType,   // 牌型类（参与顺子/同花/对子…时）
        Buff        // 增益类（打出时生效）
    }

    public enum IntentType
    {
        Attack,
        Defense,
        Buff,
        Debuff,
        MultiAttack,
        Multi,
        Sequence,
        Special,
        Taunt,     // 嘲讽：强制玩家只能选它（1 回合，挂在玩家状态栏）
        Charge,    // 蓄力：下次攻击行动伤害翻倍
        Sunder,    // 破防：对防御伤害翻倍（防御只能抵消一半，向上取整）
        Curse,     // 诅咒：给玩家随机 N 张牌附加指定诅咒（本场战斗临时，value=附魔id，hitCount=张数）
        Swallow,   // 吞噬：吞掉玩家抽牌堆里 N 张牌（value=张数），以状态挂在敌人身上，敌人死亡时归还
        Burrow,    // 遁地：受到的攻击伤害固定为 1，被攻击 value 次后出来（未遁地时优先使用，已遁地时不能再用）
        Summon,    // 召唤：召唤敌人（value=敌人id，hitCount=数量）；敌人上限 3 个，满员时不能用
        HealAllies // 全体回血：给所有友方敌人回复 value 点生命
    }

    /// <summary>敌人被动（EnemyData.passive）：不进意图池，整场战斗常驻</summary>
    public enum EnemyPassive
    {
        None,       // 无
        Weakness    // 弱点：每回合刷新 2 个弱点牌型；被非弱点牌型攻击时 +1 力量
    }

    public enum PotionEffectType
    {
        HealPercent,
        NextPlayDamageMult,
        GainDefense,
        DrawCards,
        ApplyStatus,
        CardRankShift,
        SetHandSuit
    }

    /// <summary>药水状态作用目标</summary>
    public enum StatusTarget
    {
        enemy,
        self
    }

    public enum RelicTrigger
    {
        OnCombatStart,
        OnPlayerTurnStart,
        OnEnemyTurnStart,
        OnTurnStart,
        OnTurnEnd,
        OnPlayerTurnEnd,
        OnCardPlayed,
        OnHandType,
        OnTakeDamage,
        OnDealDamage,
        OnKillEnemy,
        OnGoldGain,
        OnShopEnter,
        OnRest,
        OnEventChoice,
        OnEnchantAdded,
        OnBattleWin,
        OnBattleLose,
        OnPotionUse,
        Passive
    }

    public enum RelicEffectType
    {
        EnchantRandomCard,
        AddDamage,
        AddDefense,
        AddTempDefense,
        AddDraw,
        AddHeal,
        AddGold,
        AddMaxHp,
        AddMaxHpPermanent,
        MultiplyGold,
        MultiplyPotionEffect,
        AddPotionSlot,
        HealOnDamage,
        HealOnDamageDealt,
        ModifyShopPrice,
        NextBattleEnemyBuff,
        NextBattleStartHp,
        NextPlayDamageMult,
        ApplyPoison,
        ApplyBurn,
        ApplyWeaken,
        AddDamageOnPlay,
        AddDrawOnPlay,
        AddHealOnPlay,
        AddGoldOnKill,
        AddRelicOnKill,
        AddPotionOnKill,
        AddCardOnKill,
        DuplicateHandType,
        TransformHandType,
        GainRelicEffect,
        ReduceCost,
        GainEnergy,
        ExhaustCard,
        RetainCard,
        UpgradeCard,
        RemoveCard,
        CopyCard,
        ChangeCardType
    }

    /// <summary>事件结果类型（必须与 EventSystem 的 case 字符串一致）</summary>
    public enum EventResultType
    {
        // 金币 / 生命
        GainGold,
        LoseGold,
        GainGoldPerHp,
        Heal,
        HealPerGold,
        LoseHp,
        LoseHpPercent,
        SetHpPercent,
        LoseMaxHp,
        GainMaxHp,

        // 附魔
        EnchantRandomCard,
        EnchantRandomCardWith,
        EnchantRandomCards,
        EnchantCard,
        EnchantCardWithSpecific,
        ReplaceEnchant,
        ClearCardEnchant,
        RemoveAllEnchantmentsRandomCard,
        GiveDestinyCard,
        EnchantSuitRandom,
        CurseCardRandom,
        RerollAllEnchantments,
        CopyEnchantsBetweenCards,
        SacrificeEnchantsByRarity,
        RemoveAllAndEnchantRandomCard,
        EnchantRandomCardsHandType,
        EnchantRandomCardsBuff,

        // 遗物 / 药水
        GainRelic,
        GainSpecificRelic,
        LoseRelic,
        RemoveSpecificRelic,
        LoseChosenRelic,
        GainPotion,
        GainSpecificPotion,
        SacrificePotions,

        // 下场战斗
        NextBattleStartHp,
        NextBattleStartStatus,
        NextBattleEnemyBuff,
        NextBattleEnemyDebuff,
        NextBattleDrawBonus,
        NextBattleMulligan,
        NextBattleNoMulligan,
        NextBattleStraightDraw,
        NextBattleFatePowerFull,
        NextBattleFateBonus,
        NextBattleStrengthFromGold,
        GuaranteeSuitHand,
        RevealHiddenSuits,
        SealSuitNextBattle,
        PermanentStartStrength,
        PermanentSelfDamage,
        PermanentDamageMultiplier,
        PermanentStraightDraw,
        PermanentFlushDraw,
        PermanentFlushDamage,
        DisableFateGainNextBattles,

        // 命格 / 其他
        RandomBigReward,
        RandomOutcome,
        ResetDestiny,
        AddDestinyPoint,
        AddDestinyPointSuit,
        StartChallenge,
        StartCombatWithReward,
        StageGamble,
        Gamble
    }

    public enum HandCondition
    {
        DamageDealt,
        OnePair,
        TwoConsecutivePairs,
        ThreeOfAKind,
        FullHouse,
        FourOfAKind,
        StraightAny,
        Straight3,
        Straight4,
        Straight5,
        FlushAny,
        Flush3,
        Flush4,
        Flush5,
        StraightFlushAny,
        StraightFlush3,
        StraightFlush4,
        StraightFlush5
    }
}
