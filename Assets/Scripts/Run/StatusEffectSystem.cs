using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// 状态效果类型
    /// </summary>
    public enum StatusEffectType
    {
        None = 0,
        Poison = 1,         // 中毒：回合结束受到伤害
        Burn = 2,           // 灼烧：回合结束受到伤害，可叠加
        Weaken = 3,         // 虚弱：造成伤害降低
        Strength = 4,       // 力量：造成伤害增加
        Dexterity = 5,      // 敏捷：获得防御增加
        Focus = 6,          // 专注：牌型效果增强
        Artifact = 7,       // 神器：抵消一次负面效果
        Thorns = 8,         // 荆棘：受到攻击时反伤
        Regeneration = 9,   // 再生：回合结束回血
        Metallicize = 10,   // 金属化：回合结束获得防御
        Intangible = 11,    // 无形：下次受到攻击伤害为1
        Vulnerable = 12,    // 易伤：每层受到伤害 +10%
        // 13 原「Frail 虚弱(受击)」已删除；保留编号，避免已序列化的 StatusEffectSprite 错位
        Rage = 14,          // 暴怒：下一次出牌伤害翻倍
        DefenseUp = 15,     // 坚壁：获得防御翻倍（本回合）

        // ===== 附魔遗留增益（战斗内，显示在状态栏）=====
        TurnDamageBonus = 16,  // 顺风耳：本回合出牌伤害 +层数
        NextTurnDraw = 17,     // 同花之魂：下回合抽牌 +层数
        SuitDamageBonus = 18,  // 同花顺之巅：指定花色每张牌额外伤害 +层数（带花色维度）
        SelfDamage = 19,       // 血契：每回合结束自伤 层数（永久，来自事件）
        DrawBonus = 20,        // 抽牌+：本场每回合额外抽 层数 张（来自事件）
        MulliganBonus = 21,    // 重抽+：本场弃牌重抽次数 +层数（来自事件）
        NoMulligan = 22,       // 封印：本场无法弃牌重抽（来自事件）
        SuitSeal = 23,         // 封禁：本场无法打出该花色（带花色维度，来自事件）

        // ===== 梅花事件：永久 / 下场修正（战斗状态栏显示用）=====
        StraightDrawBonus = 24,   // 顺子额外抽牌
        FlushDrawBonus = 25,      // 同花额外抽牌
        FlushDamageBonus = 26,    // 同花额外伤害
        DamageMultiplierMod = 27, // 造成伤害百分比修正
        FatePowerBonus = 28,      // 命运之力积攒 +N
        FateGainSeal = 29,        // 本场无法积攒命运之力
        StraightDrawTemp = 30,    // 顺子额外抽牌（仅本场，来自「下一场战斗」）
        Challenge = 31,           // 挑战：本场失去生命不超过 N（层数 = 已失去）

        // ===== 敌人主动行动产生的状态 =====
        Taunt = 32,               // 嘲讽：玩家只能选中该敌人（amount = 敌人下标 + 1，挂在玩家身上）
        Charge = 33,              // 蓄力：下次攻击行动伤害翻倍（层数 = 剩余次数，挂在敌人身上）
        Swallow = 34,             // 吞噬：敌人吞掉的玩家牌（挂在敌人身上，层数 = 张数，一个 buff 汇总显示）
        Weakness = 35,            // 弱点：愤怒骷髅头被动（挂在敌人身上，展示本回合的弱点牌型）
        Burrow = 36,              // 遁地：受到的攻击伤害固定为 1，层数 = 还需被攻击的次数（挂在敌人身上）
        Gaze = 37,                // 凝视：Boss 记住玩家上一手牌型，重复该牌型伤害减半（amount = (int)HandType）
        Pollute = 38,             // 污染：Boss 每回合污染玩家若干张牌（层数 = 每回合污染张数）
    }

    /// <summary>
    /// 状态效果实例
    /// </summary>
    [Serializable]
    public class StatusEffect
    {
        public StatusEffectType type;
        public int amount;          // 层数/数值
        public int duration;        // 剩余回合数（-1 = 战斗结束不移除）
        public Suit suit;           // 花色维度（useSuit=true 时有效）
        public bool useSuit;        // 是否按花色区分（同类型不同花色可并存）

        public StatusEffect(StatusEffectType type, int amount = 1, int duration = -1, Suit suit = Suit.Spade, bool useSuit = false)
        {
            this.type = type;
            this.amount = amount;
            this.duration = duration;
            this.suit = suit;
            this.useSuit = useSuit;
        }

        /// <summary>是否为同一条状态（类型 + 花色维度）</summary>
        public bool SameKey(StatusEffect other)
        {
            if (other == null) return false;
            if (other.type != type || other.useSuit != useSuit) return false;
            if (useSuit && other.suit != suit) return false;
            return true;
        }

        public StatusEffect Clone()
        {
            return new StatusEffect(type, amount, duration, suit, useSuit);
        }

        public override string ToString()
        {
            string suitStr = useSuit ? $"({suit})" : "";
            return $"{type}{suitStr} x{amount}{(duration > 0 ? $" ({duration}回合)" : "")}";
        }
    }

    /// <summary>
    /// 状态效果系统：管理单位的所有状态效果
    /// </summary>
    public class StatusEffectSystem
    {
        private BattleUnit owner;
        private List<StatusEffect> effects = new List<StatusEffect>();

        // 事件回调
        public Action<StatusEffectType, int> OnEffectAdded;      // type, amount
        public Action<StatusEffectType, int> OnEffectRemoved;    // type, amount
        public Action<StatusEffectType, int> OnEffectChanged;    // type, newAmount

        public StatusEffectSystem(BattleUnit owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// 添加状态效果
        /// </summary>
        public void AddStatus(StatusEffectType type, int amount = 1, int duration = -1)
        {
            AddStatusInternal(type, amount, duration, Suit.Spade, false);
        }

        /// <summary>
        /// 添加带花色维度的状态效果（同类型不同花色可并存）
        /// </summary>
        public void AddStatus(StatusEffectType type, Suit suit, int amount = 1, int duration = -1)
        {
            AddStatusInternal(type, amount, duration, suit, true);
        }

        private void AddStatusInternal(StatusEffectType type, int amount, int duration, Suit suit, bool useSuit)
        {
            if (type == StatusEffectType.None || amount <= 0) return;

            // 神器：抵消一次负面状态
            if (IsNegativeStatus(type) && HasStatus(StatusEffectType.Artifact))
            {
                RemoveStatus(StatusEffectType.Artifact, 1);
                return;
            }

            var existing = FindEffect(type, suit, useSuit);
            if (existing != null)
            {
                existing.amount += amount;
                if (duration > 0)
                {
                    existing.duration = Mathf.Max(existing.duration, duration);
                }
                OnEffectChanged?.Invoke(type, existing.amount);
            }
            else
            {
                var effect = new StatusEffect(type, amount, duration, suit, useSuit);
                effects.Add(effect);
                OnEffectAdded?.Invoke(type, amount);
            }
        }

        /// <summary>
        /// 直接设置状态层数（覆盖而非叠加；允许 0，用于「挑战」这类展示型状态）
        /// </summary>
        public void SetStatus(StatusEffectType type, int amount, int duration = -1)
        {
            if (type == StatusEffectType.None || amount < 0) return;

            var existing = FindEffect(type, Suit.Spade, false);
            if (existing != null)
            {
                existing.amount = amount;
                if (duration > 0) existing.duration = duration;
                OnEffectChanged?.Invoke(type, existing.amount);
                return;
            }

            var effect = new StatusEffect(type, amount, duration, Suit.Spade, false);
            effects.Add(effect);
            OnEffectAdded?.Invoke(type, amount);
        }

        /// <summary>
        /// 移除状态效果
        /// </summary>
        public void RemoveStatus(StatusEffectType type, int amount = -1)
        {
            RemoveStatusInternal(type, Suit.Spade, false, amount);
        }

        /// <summary>
        /// 移除带花色维度的状态效果
        /// </summary>
        public void RemoveStatus(StatusEffectType type, Suit suit, int amount = -1)
        {
            RemoveStatusInternal(type, suit, true, amount);
        }

        private void RemoveStatusInternal(StatusEffectType type, Suit suit, bool useSuit, int amount)
        {
            var effect = FindEffect(type, suit, useSuit);
            if (effect == null) return;

            if (amount < 0 || effect.amount <= amount)
            {
                effects.Remove(effect);
                OnEffectRemoved?.Invoke(type, effect.amount);
            }
            else
            {
                effect.amount -= amount;
                OnEffectChanged?.Invoke(type, effect.amount);
            }
        }

        /// <summary>
        /// DoT（中毒/灼烧/再生）在回合开始结算完后，由 BattleManager 调用这里递减持续回合。
        /// 递减/移除会触发事件，状态栏才能即时刷新（直接改 duration 字段是不会刷新的）。
        /// </summary>
        public void TickDotDuration(StatusEffectType type)
        {
            var effect = FindEffect(type, Suit.Spade, false);
            if (effect == null || effect.duration <= 0) return;

            effect.duration--;
            if (effect.duration <= 0)
                RemoveStatus(type);   // 内部会触发 OnEffectRemoved
            else
                OnEffectChanged?.Invoke(type, effect.amount);
        }

        /// <summary>
        /// 获取状态效果层数
        /// </summary>
        public int GetAmount(StatusEffectType type)
        {
            var effect = FindEffect(type, Suit.Spade, false);
            return effect?.amount ?? 0;
        }

        /// <summary>
        /// 获取指定花色维度的状态效果层数
        /// </summary>
        public int GetAmount(StatusEffectType type, Suit suit)
        {
            var effect = FindEffect(type, suit, true);
            return effect?.amount ?? 0;
        }

        /// <summary>
        /// 是否拥有状态效果
        /// </summary>
        public bool HasStatus(StatusEffectType type)
        {
            return FindEffect(type, Suit.Spade, false) != null;
        }

        /// <summary>
        /// 是否拥有指定花色维度的状态效果
        /// </summary>
        public bool HasStatus(StatusEffectType type, Suit suit)
        {
            return FindEffect(type, suit, true) != null;
        }

        /// <summary>
        /// 是否为负面状态（会被神器抵消）——统一走状态注册表
        /// </summary>
        private static bool IsNegativeStatus(StatusEffectType type)
            => StatusEffectRegistry.IsNegative(type);

        /// <summary>
        /// 按「类型 + 花色维度」查找状态
        /// </summary>
        private StatusEffect FindEffect(StatusEffectType type, Suit suit, bool useSuit)
        {
            foreach (var e in effects)
            {
                if (e.type != type) continue;
                if (e.useSuit != useSuit) continue;
                if (useSuit && e.suit != suit) continue;
                return e;
            }
            return null;
        }

        /// <summary>
        /// 回合结束处理（仅处理层数递减/移除，不处理 DoT 伤害/回血——由 BattleManager 协程显式处理）
        /// </summary>
        public void OnTurnEnd()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];

                // 跳过 DoT 类型的即时效果处理（中毒、灼烧、再生在 BattleManager 协程中单独处理）
                bool isDoT = effect.type == StatusEffectType.Poison || 
                            effect.type == StatusEffectType.Burn || 
                            effect.type == StatusEffectType.Regeneration;
                
                if (!isDoT)
                {
                    ProcessTurnEndEffect(effect);

                    // DoT（中毒/灼烧/再生）的持续回合在回合开始时「结算完当场递减」，
                    // 这里跳过，避免回合内新加的状态白掉一回合
                    if (effect.duration > 0)
                    {
                        effect.duration--;
                        if (effect.duration <= 0)
                        {
                            effects.RemoveAt(i);
                            OnEffectRemoved?.Invoke(effect.type, effect.amount);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 战斗开始处理
        /// </summary>
        public void OnCombatStart()
        {
            // 触发战斗开始时的效果
            foreach (var effect in effects)
            {
                if (effect.type == StatusEffectType.Metallicize)
                {
                    owner.AddDefense(effect.amount);
                }
            }
        }

        /// <summary>
        /// 战斗结束清理
        /// </summary>
        public void OnCombatEnd()
        {
            // 清除所有效果（不再有跨战斗保留）
            var removed = new List<StatusEffect>(effects);
            effects.Clear();
            foreach (var effect in removed)
            {
                OnEffectRemoved?.Invoke(effect.type, effect.amount);
            }
        }

        /// <summary>
        /// 受击时处理（荆棘、无形、神器等）
        /// </summary>
        public int OnTakeDamage(int incomingDamage, BattleUnit attacker)
        {
            int finalDamage = incomingDamage;

            // 易伤：每层 +10% 受到伤害
            int vulnerable = GetAmount(StatusEffectType.Vulnerable);
            if (vulnerable > 0)
            {
                finalDamage = Mathf.RoundToInt(finalDamage * (1f + vulnerable * 0.1f));
            }

            // 无形
            if (HasStatus(StatusEffectType.Intangible))
            {
                finalDamage = 1;
                RemoveStatus(StatusEffectType.Intangible, 1);
            }

            // 遁地：受到的所有伤害固定为 1，每受到一次伤害消耗 1 层（层数 = 还需受击次数）
            if (HasStatus(StatusEffectType.Burrow))
            {
                finalDamage = 1;
                RemoveStatus(StatusEffectType.Burrow, 1);
            }

            // 荆棘反伤
            if (HasStatus(StatusEffectType.Thorns))
            {
                int thornsAmount = GetAmount(StatusEffectType.Thorns);
                attacker?.TakeDamage(thornsAmount);
            }

            return finalDamage;
        }

        /// <summary>
        /// 预览受击伤害：与 OnTakeDamage 相同的修正，但不产生副作用
        /// （不移除「无形」、不触发「荆棘」反伤），仅用于 UI 显示。
        /// </summary>
        public int PreviewTakeDamage(int incomingDamage)
        {
            int finalDamage = incomingDamage;
            int vulnerable = GetAmount(StatusEffectType.Vulnerable);
            if (vulnerable > 0)
                finalDamage = Mathf.RoundToInt(finalDamage * (1f + vulnerable * 0.1f));
            if (HasStatus(StatusEffectType.Intangible))
                finalDamage = 1;
            if (HasStatus(StatusEffectType.Burrow))
                finalDamage = 1;
            return finalDamage;
        }

        /// <summary>
        /// 造成伤害时处理（力量、虚弱等）
        /// </summary>
        public int OnDealDamage(int baseDamage)
        {
            float mult = 1f;

            // 力量：每层 +1 点伤害（固定值）
            int strength = GetAmount(StatusEffectType.Strength);

            // 虚弱：每层 -10% 伤害
            int weaken = GetAmount(StatusEffectType.Weaken);
            if (weaken > 0) mult -= weaken * 0.1f;

            // 专注：每层 +10% 伤害
            int focus = GetAmount(StatusEffectType.Focus);
            if (focus > 0) mult += focus * 0.1f;

            return Mathf.RoundToInt((baseDamage + strength) * mult);
        }

        /// <summary>
        /// 获得防御时处理（敏捷、虚弱(受击)）
        /// </summary>
        public int OnGainDefense(int baseDefense)
        {
            float mult = 1f;

            // 敏捷
            int dexterity = GetAmount(StatusEffectType.Dexterity);
            if (dexterity > 0) mult += dexterity * 0.1f;

            // 坚壁：本回合获得防御翻倍
            if (HasStatus(StatusEffectType.DefenseUp)) mult *= 2f;

            return Mathf.RoundToInt(baseDefense * mult);
        }

        /// <summary>
        /// 处理回合结束效果
        /// </summary>
        private void ProcessTurnEndEffect(StatusEffect effect)
        {
            switch (effect.type)
            {
                case StatusEffectType.Poison:
                    owner.TakeDamage(effect.amount);
                    break;
                case StatusEffectType.Burn:
                    // 灼烧的伤害与层数衰减由 BattleManager 的 DoT 结算处理，
                    // 这里仅作兜底：层数减半
                    effect.amount /= 2;
                    if (effect.amount <= 0)
                    {
                        RemoveStatus(StatusEffectType.Burn);
                    }
                    break;
                case StatusEffectType.Regeneration:
                    owner.Heal(effect.amount);
                    break;
                case StatusEffectType.Metallicize:
                    owner.AddDefense(effect.amount);
                    break;
            }
        }

        /// <summary>
        /// 获取所有状态效果（用于 UI 显示）
        /// </summary>
        public List<StatusEffect> GetAllEffects()
        {
            return new List<StatusEffect>(effects);
        }

        /// <summary>
        /// 清除所有状态
        /// </summary>
        public void Clear()
        {
            var removed = new List<StatusEffect>(effects);
            effects.Clear();
            foreach (var effect in removed)
            {
                OnEffectRemoved?.Invoke(effect.type, effect.amount);
            }
        }
    }

    /// <summary>状态效果的中文名（作弊窗口 / 编辑器 / UI 共用）</summary>
    public static class StatusEffectNames
    {
        public static string Name(StatusEffectType type)
        {
            if (type == StatusEffectType.None) return "无";
            return StatusEffectRegistry.Name(type);
        }
    }
}