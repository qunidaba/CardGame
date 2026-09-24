using System;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 附魔系统：管理附魔触发、效果结算（仅处理附魔，不处理遗物）
    /// </summary>
    public class EnchantmentSystem
    {
        // --- 触发器类型 ---
        public enum TriggerType
        {
            OnPlay,           // 出牌时
            OnHandType,       // 打出特定牌型时
            OnOtherCardPlayed,// 此牌在手牌中，其他牌被打出时（燃料）
            OnCombatStart,    // 战斗开始
            OnTurnStart,      // 回合开始
            OnTurnEnd,        // 回合结束
            OnDraw,           // 抽牌时
            OnDiscard,        // 被弃置时（弃牌重抽/回合结束回牌堆）
            OnTakeDamage,     // 受伤时
            OnDealDamage,     // 造成伤害时
            OnKill,           // 击杀时
            OnGoldGain,       // 获得金币时
            OnPotionUse,      // 使用药水时
            OnCombatEnd,      // 战斗结束
            OnBattleWin,      // 战斗胜利
            OnBattleLose,     // 战斗失败
            Passive           // 常驻（标记类，由 CardData/流程读取，不触发事件）
        }

        // --- 效果类型 ---
        public enum EffectType
        {
            // ===== 基础数值类（写入 HandEffect 列表，即时生效）=====
            AddDamage,            // 伤害+：出牌结算时额外伤害
            AddDefense,           // 防御+：出牌结算时获得防御
            AddDraw,              // 抽牌+：出牌结算时额外抽牌
            AddHeal,              // 治疗+：出牌结算时治疗
            AddGold,              // 金币+：即时获得金币
            MultiplyDamage,       // 伤害倍率：将本次牌型伤害乘以 value（牌型联动用）
            AddTurnDamageBonus,   // 本回合后续出牌伤害 +value（顺风耳）
            AddDrawNextTurn,      // 下回合开始额外抽 value 张（同花之魂）
            AddDamageEqualRank,   // 额外造成等同于该牌点数的伤害（葫芦大师）
            ReturnCardsToHand,    // 本次出牌后返回 value 张牌到手牌（连对专家）
            AddSuitDamageBonus,   // 本场战斗中该牌花色每张牌额外造成 value 伤害（同花顺之巅）

            // ===== 状态流 =====
            ApplyPoisonEqualCount,   // 施加中毒 = 本次出牌张数（瘟疫）
            ApplyVulnerable,         // 施加易伤（破甲）
            HealPercentOfDamage,     // 按本次造成伤害的百分比回血（吸血）
            AddDamagePerEnemyPoison, // 敌人每有 1 层中毒，额外造成 value 伤害（剧毒共鸣）
            DoubleEnemyBurn,         // 敌人灼烧层数翻倍（灼烧引爆）

            // ===== 手牌被动 =====
            ApplyBurnPerRedCard,     // 每打出一张红色牌，施加 value 层灼烧（燃料）

            // ===== 状态联动（进阶）=====
            HealPerEnemyPoison,         // 敌人每层中毒回复 value 生命（瘟疫医生）
            GainStrengthPerEnemyPoison, // 敌人每层中毒自身获得 value 力量（镜像）
            ExecuteBelowHpPercent,      // 敌人生命低于 value% 时本次伤害 ×3（处决）
            AddDamagePerEnemyDebuff,    // 敌人每有 1 种负面状态，额外造成 value 伤害（元素共鸣）
            LoseHp,                     // 失去 value 生命（自残契约）
            ApplySelfStatus,            // 给自己施加 status 状态（荆棘之肤/再生之种）
            ApplyEnemyStatus,           // 给敌人施加 status 状态
            GainBuffByDominantSuit,     // 按当前命运花色给自己对应 buff（四色印记）

            // ===== 牌自身定位（标记类由 CardData/流程读取；魔导为主动效果）=====
            Wildcard,                   // 万能牌：可手动改判定花色与点数
            SuitShift,                  // 变色：可手动改判定花色
            Mirror,                     // 镜牌：可变为手牌中另一张的复制
            ReturnToHand,               // 回手：打出后回到手牌
            Retain,                     // 保留：回合结束不弃
            Destiny,                    // 天命：战斗第一回合必定抽到
            GrantRandomTier1Enchant,    // 魔导：打出时给随机一张手牌一个随机附魔

            // ===== 状态效果类（施加 Buff/Debuff，需持续时间）=====
            ApplyPoison,          // 施加中毒：敌人回合结束受伤，需 duration
            ApplyBurn,            // 施加灼烧：敌人回合结束受伤且层数递减，需 duration
            ApplyWeaken,          // 施加虚弱：敌人造成伤害降低，需 duration
            ApplyPoisonRandom,    // 施加中毒（随机一名存活敌人，不依赖选中目标）
            ApplyBurnRandom,      // 施加灼烧（随机一名存活敌人，不依赖选中目标）

            // ===== 复杂/延迟触发类（需特殊逻辑，暂留空实现）=====
            DrawSpecificCard,     // 抽指定牌：从牌库抽特定牌（如抽一张 A），需牌库搜索
            DuplicateHandType,    // 牌型效果双倍：本次出牌的牌型效果触发两次
            TransformHandType,    // 牌型升级：将当前牌型视为更高一级（如顺子→同花顺）
            HealOnDamage,         // 吸血：造成伤害时治疗自己（数值=治疗量）
            NextPlayDamageMult,   // 下次出牌伤害倍率：单次生效后清零（如 1.5 倍）

            // ===== 打出限制 / 特殊（事件诅咒用）=====
            Unplayable,           // 禁锢：此牌无法被打出
            NoSameRank,           // 割裂：此牌无法与同数字的牌一起打出
            SearchDiscard,        // 搜寻：打出后从弃牌堆选一张牌回手
            ReduceDamage,         // 无力：本次出牌伤害 -value（最低 0）
            LoseGold,             // 吞噬：触发时金币 -value（配合 OnPlay / OnDraw）
        }

        /// <summary>
        /// 附魔效果数据
        /// </summary>
        [Serializable]
        public class EnchantmentEffect
        {
            public EffectType type;
            public float value;           // 数值（伤害量、层数、倍率等）
            public int duration = -1;     // 持续回合数（-1=永久/战斗结束，>0 为具体回合数）
            public string condition;      // 触发条件（如 "StraightFlush5", "DamageDealt"）
            public string targetTag;      // 目标标签
        }

        /// <summary>
        /// 附魔实例（运行时）
        /// </summary>
        public class EnchantmentInstance
        {
            public int enchantmentId;     // 配置表 ID
            public string name;           // 附魔名称
            public TriggerType trigger;   // 触发时机
            public List<EnchantmentEffect> effects = new List<EnchantmentEffect>();
            public int tier;              // 稀有度
            public bool isUnique;         // 同张牌只能有一个
            public string condition;      // 额外条件
        }

        // --- 运行时上下文 ---
        private RunData runData;
        private BattleManager battleManager;
        private RunDirector runDirector;

        public void Initialize(RunData runData, BattleManager battleManager, RunDirector runDirector)
        {
            this.runData = runData;
            this.battleManager = battleManager;
            this.runDirector = runDirector;
        }

        // ===== 触发入口 =====

        /// <summary>
        /// 出牌时触发：OnPlay
        /// </summary>
        public void OnCardPlayed(CardData card, HandTypeResult handResult, List<HandEffectTable.HandEffect> effects)
        {
            var enchantments = GetCardEnchantments(card);
            foreach (var enchId in enchantments)
            {
                var ench = ConfigLoader.GetEnchantment(enchId);
                if (ench != null && ench.trigger == "OnPlay")
                {
                    if (CheckCondition(ench.condition, handResult))
                        ApplyEnchantmentEffects(ench, effects, card, handResult);
                }
            }
        }

        /// <summary>
        /// 预览专用：只应用「纯数值」类附魔（写入 HandEffect 列表、无任何副作用），
        /// 让手牌效果预览（伤害 / 抽牌 / 防御 / 治疗）尽量与实打一致。
        /// </summary>
        public void ApplyPreviewModifiers(List<CardData> cards, HandTypeResult handResult, List<HandEffectTable.HandEffect> effects)
        {
            if (cards == null || effects == null) return;

            foreach (var card in cards)
            {
                if (card == null) continue;

                foreach (var enchId in GetCardEnchantments(card))
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench == null) continue;
                    if (ench.trigger != "OnPlay" && ench.trigger != "OnHandType") continue;
                    if (!CheckCondition(ench.condition, handResult)) continue;

                    foreach (var eff in ench.effects)
                    {
                        if (!Enum.TryParse<EffectType>(eff.type, true, out var effectType)) continue;
                        ApplyPreviewEffect(effectType, GetIntValue(eff.value), GetFloatValue(eff.value), card, effects);
                    }
                }
            }
        }

        /// <summary>只处理不产生副作用的数值类效果；其余（加金币/施状态/抽牌库等）一律跳过</summary>
        private static void ApplyPreviewEffect(EffectType type, int intValue, float floatValue, CardData card, List<HandEffectTable.HandEffect> effects)
        {
            switch (type)
            {
                case EffectType.AddDamage:
                    HandEffectTable.AddOrMerge(effects, HandEffectTable.EffectType.Damage, intValue);
                    break;
                case EffectType.AddDefense:
                    HandEffectTable.AddOrMerge(effects, HandEffectTable.EffectType.Defense, intValue);
                    break;
                case EffectType.AddDraw:
                    HandEffectTable.AddOrMerge(effects, HandEffectTable.EffectType.DrawCard, intValue);
                    break;
                case EffectType.AddHeal:
                    HandEffectTable.AddOrMerge(effects, HandEffectTable.EffectType.Heal, intValue);
                    break;
                case EffectType.AddDamageEqualRank:
                    if (card != null)
                        HandEffectTable.AddOrMerge(effects, HandEffectTable.EffectType.Damage, card.rank);
                    break;
                case EffectType.MultiplyDamage:
                {
                    var dmg = effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage);
                    if (dmg != null)
                    {
                        dmg.value = Mathf.RoundToInt(dmg.value * floatValue);
                        dmg.description = HandEffectTable.Describe(HandEffectTable.EffectType.Damage, dmg.value);
                    }
                    break;
                }
                case EffectType.ReduceDamage:
                {
                    var dmg = effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage);
                    if (dmg != null)
                    {
                        dmg.value = Mathf.Max(0, dmg.value - intValue);
                        dmg.description = HandEffectTable.Describe(HandEffectTable.EffectType.Damage, dmg.value);
                    }
                    break;
                }
                case EffectType.HealPercentOfDamage:
                    HandEffectTable.AddOrMerge(effects, HandEffectTable.EffectType.HealPercentOfDamage, intValue);
                    break;
            }
        }

        /// <summary>
        /// 抽到某张牌时触发：OnDraw（如诅咒「吞噬+」）
        /// </summary>
        public void OnDrawn(CardData card)
        {
            if (card == null) return;

            foreach (var enchId in GetCardEnchantments(card))
            {
                var ench = ConfigLoader.GetEnchantment(enchId);
                if (ench != null && ench.trigger == "OnDraw")
                    ApplyEnchantmentEffects(ench, null, card, null);
            }
        }

        /// <summary>
        /// 打出特定牌型时触发：OnHandType
        /// </summary>
        public void OnHandTypePlayed(CardData card, HandTypeResult handResult, List<HandEffectTable.HandEffect> effects)
        {
            var enchantments = GetCardEnchantments(card);
            foreach (var enchId in enchantments)
            {
                var ench = ConfigLoader.GetEnchantment(enchId);
                if (ench != null && ench.trigger == "OnHandType")
                {
                    if (CheckCondition(ench.condition, handResult))
                    {
                        ApplyEnchantmentEffects(ench, effects, card, handResult);
                    }
                }
            }
        }

        /// <summary>
        /// 其他牌被打出时（燃料）：检查仍留在手牌中的牌
        /// </summary>
        public void OnOtherCardsPlayed(List<CardData> playedCards)
        {
            if (runData == null || battleManager == null || playedCards == null) return;

            var hand = battleManager.GetHandArea();
            var enemy = battleManager.GetEnemy();
            if (hand == null || enemy == null) return;

            int redCount = 0;
            foreach (var c in playedCards)
            {
                if (c.suit == Suit.Heart || c.suit == Suit.Diamond) redCount++;
            }
            if (redCount == 0) return;

            foreach (var handCard in hand.HandCards)
            {
                var enchantments = GetCardEnchantments(handCard);
                foreach (var enchId in enchantments)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench == null || ench.trigger != "OnOtherCardPlayed") continue;

                    foreach (var eff in ench.effects)
                    {
                        if (Enum.TryParse<EffectType>(eff.type, true, out var effectType) &&
                            effectType == EffectType.ApplyBurnPerRedCard)
                        {
                            enemy.AddStatus(StatusEffectType.Burn, redCount * GetIntValue(eff.value));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 牌被弃掉时（余烬/残毒）：触发 OnDiscard
        /// </summary>
        public void OnCardsDiscarded(List<CardData> cards)
        {
            if (runData == null || cards == null) return;
            foreach (var card in cards)
            {
                var enchantments = GetCardEnchantments(card);
                foreach (var enchId in enchantments)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench != null && ench.trigger == "OnDiscard")
                        ApplyEnchantmentEffects(ench, null, card, null);
                }
            }
        }

        /// <summary>
        /// 战斗开始触发：OnCombatStart
        /// </summary>
        public void OnCombatStart()
        {
            foreach (var kvp in runData.cardEnchantmentIds)
            {
                foreach (int enchId in kvp.Value)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench != null && ench.trigger == "OnCombatStart")
                    {
                        ApplyEnchantmentEffects(ench, null, null, null);
                    }
                }
            }
        }

        /// <summary>
        /// 回合开始触发：OnTurnStart
        /// </summary>
        public void OnTurnStart()
        {
            foreach (var kvp in runData.cardEnchantmentIds)
            {
                foreach (int enchId in kvp.Value)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench != null && ench.trigger == "OnTurnStart")
                    {
                        ApplyEnchantmentEffects(ench, null, null, null);
                    }
                }
            }
        }

        /// <summary>
        /// 回合结束触发：OnTurnEnd
        /// </summary>
        public void OnTurnEnd()
        {
            foreach (var kvp in runData.cardEnchantmentIds)
            {
                foreach (int enchId in kvp.Value)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench != null && ench.trigger == "OnTurnEnd")
                    {
                        ApplyEnchantmentEffects(ench, null, null, null);
                    }
                }
            }
        }

        /// <summary>
        /// 造成伤害触发：OnDealDamage
        /// </summary>
        public void OnDealDamage(int damage)
        {
            foreach (var kvp in runData.cardEnchantmentIds)
            {
                foreach (int enchId in kvp.Value)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench != null && ench.trigger == "OnDealDamage")
                    {
                        ApplyEnchantmentEffects(ench, null, null, null);
                    }
                }
            }
        }

        /// <summary>
        /// 受伤触发：OnTakeDamage
        /// </summary>
        public void OnTakeDamage(int damage)
        {
            foreach (var kvp in runData.cardEnchantmentIds)
            {
                foreach (int enchId in kvp.Value)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench != null && ench.trigger == "OnTakeDamage")
                    {
                        ApplyEnchantmentEffects(ench, null, null, null);
                    }
                }
            }
        }

        /// <summary>
        /// 击杀触发：OnKill
        /// </summary>
        public void OnKillEnemy()
        {
            foreach (var kvp in runData.cardEnchantmentIds)
            {
                foreach (int enchId in kvp.Value)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench != null && ench.trigger == "OnKill")
                    {
                        ApplyEnchantmentEffects(ench, null, null, null);
                    }
                }
            }
        }

        /// <summary>
        /// 获得金币触发：OnGoldGain
        /// </summary>
        public int OnGoldGain(int baseGold)
        {
            // 附魔不处理金币倍率（由遗物系统处理）
            return baseGold;
        }

        /// <summary>
        /// 进入商店触发：OnShopEnter
        /// </summary>
        public float OnShopEnter(float basePriceMult)
        {
            // 附魔不处理商店价格（由遗物系统处理）
            return basePriceMult;
        }

        /// <summary>
        /// 使用药水触发：OnPotionUse
        /// </summary>
        public float OnPotionUse(float baseEffect)
        {
            // 附魔不处理药水倍率（由遗物系统处理）
            return baseEffect;
        }

        /// <summary>
        /// 战斗胜利触发：OnBattleWin
        /// </summary>
        public void OnBattleWin()
        {
            foreach (var kvp in runData.cardEnchantmentIds)
            {
                foreach (int enchId in kvp.Value)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench != null && ench.trigger == "OnBattleWin")
                    {
                        ApplyEnchantmentEffects(ench, null, null, null);
                    }
                }
            }
        }

        /// <summary>
        /// 战斗结束触发：OnCombatEnd
        /// </summary>
        public void OnCombatEnd()
        {
            foreach (var kvp in runData.cardEnchantmentIds)
            {
                foreach (int enchId in kvp.Value)
                {
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench != null && ench.trigger == "OnCombatEnd")
                    {
                        ApplyEnchantmentEffects(ench, null, null, null);
                    }
                }
            }
        }

        // ===== 效果应用（仅实现基础数值类，状态类委托给 StatusEffectSystem）=====

        private void ApplyEnchantmentEffects(EnchantmentData ench, List<HandEffectTable.HandEffect> effects, CardData card, HandTypeResult handResult)
        {
            foreach (var eff in ench.effects)
            {
                if (!Enum.TryParse<EffectType>(eff.type, true, out var effectType))
                {
                    Debug.LogWarning($"[EnchantmentSystem] 未知效果类型: {eff.type}");
                    continue;
                }

                switch (effectType)
                {
                    // ===== 基础数值类 - 直接写入 HandEffect 列表 =====
                    case EffectType.AddDamage:
                        if (effects != null)
                            AddEffectValue(effects, HandEffectTable.EffectType.Damage, GetIntValue(eff.value));
                        break;
                    case EffectType.AddDefense:
                        if (effects != null)
                            AddEffectValue(effects, HandEffectTable.EffectType.Defense, GetIntValue(eff.value));
                        break;
                    case EffectType.AddDraw:
                        if (effects != null)
                            AddEffectValue(effects, HandEffectTable.EffectType.DrawCard, GetIntValue(eff.value));
                        break;
                    case EffectType.AddHeal:
                        if (effects != null)
                            AddEffectValue(effects, HandEffectTable.EffectType.Heal, GetIntValue(eff.value));
                        break;
                    case EffectType.AddGold:
                        if (runData != null)
                            runData.Gold += GetIntValue(eff.value);
                        break;

                    // ===== 诅咒「吞噬」：触发时金币 -value =====
                    case EffectType.LoseGold:
                        if (runData != null && runData.Gold > 0)
                        {
                            int loss = Mathf.Min(runData.Gold, Mathf.Max(0, GetIntValue(eff.value)));
                            runData.Gold -= loss;
                            Debug.Log($"[吞噬] 失去 {loss} 金币（剩 {runData.Gold}）");
                        }
                        break;

                    // ===== 诅咒「无力」：本次出牌伤害 -value =====
                    case EffectType.ReduceDamage:
                        if (effects != null)
                        {
                            var dmgEff = effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage);
                            if (dmgEff != null)
                            {
                                dmgEff.value = Mathf.Max(0, dmgEff.value - GetIntValue(eff.value));
                                dmgEff.description = $"造成 {dmgEff.value} 点伤害";
                            }
                        }
                        break;

                    // ===== 牌型联动：按倍率放大本次牌型伤害 =====
                    case EffectType.MultiplyDamage:                        if (effects != null)
                        {
                            var dmg = effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage);
                            if (dmg != null)
                            {
                                dmg.value = Mathf.RoundToInt(dmg.value * GetFloatValue(eff.value));
                                dmg.description = $"造成 {dmg.value} 点伤害";
                            }
                        }
                        break;

                    // ===== 牌型联动：跨出牌/跨回合状态（挂在玩家身上，走状态栏显示）=====
                    case EffectType.AddTurnDamageBonus:
                        battleManager?.GetPlayer()?.AddStatus(StatusEffectType.TurnDamageBonus, GetIntValue(eff.value), 1);
                        break;
                    case EffectType.AddDrawNextTurn:
                        battleManager?.GetPlayer()?.AddStatus(StatusEffectType.NextTurnDraw, GetIntValue(eff.value), -1);
                        break;
                    case EffectType.AddDamageEqualRank:
                        if (effects != null && card != null)
                            AddEffectValue(effects, HandEffectTable.EffectType.Damage, card.rank);
                        break;
                    case EffectType.ReturnCardsToHand:
                        battleManager?.RequestReturnToHand(GetIntValue(eff.value));
                        break;
                    case EffectType.AddSuitDamageBonus:
                        if (card != null)
                            battleManager?.GetPlayer()?.AddStatus(StatusEffectType.SuitDamageBonus, card.suit, GetIntValue(eff.value), -1);
                        break;

                    // ===== 状态流 =====
                    case EffectType.ApplyPoisonEqualCount:
                    {
                        var enemyUnit = battleManager?.GetEnemy();
                        int cnt = handResult != null ? handResult.cardCount : 0;
                        if (enemyUnit != null && cnt > 0)
                            enemyUnit.AddStatus(StatusEffectType.Poison, cnt + (battleManager?.GetPoisonBonus() ?? 0), eff.duration);
                        break;
                    }
                    case EffectType.ApplyVulnerable:
                    {
                        var enemyUnit = battleManager?.GetEnemy();
                        if (enemyUnit != null)
                            enemyUnit.AddStatus(StatusEffectType.Vulnerable, GetIntValue(eff.value), eff.duration);
                        break;
                    }
                    case EffectType.HealPercentOfDamage:
                        if (effects != null)
                            effects.Add(new HandEffectTable.HandEffect
                            {
                                effectType = HandEffectTable.EffectType.HealPercentOfDamage,
                                value = GetIntValue(eff.value),
                                description = $"回复伤害的 {GetIntValue(eff.value)}%"
                            });
                        break;
                    case EffectType.AddDamagePerEnemyPoison:
                    {
                        var enemyUnit = battleManager?.GetEnemy();
                        int poison = enemyUnit != null ? enemyUnit.GetStatusAmount(StatusEffectType.Poison) : 0;
                        if (effects != null && poison > 0)
                            AddEffectValue(effects, HandEffectTable.EffectType.Damage, poison * GetIntValue(eff.value));
                        break;
                    }
                    case EffectType.DoubleEnemyBurn:
                    {
                        var enemyUnit = battleManager?.GetEnemy();
                        if (enemyUnit == null) break;

                        int burn = enemyUnit.GetStatusAmount(StatusEffectType.Burn);
                        if (burn > 0)
                        {
                            enemyUnit.AddStatus(StatusEffectType.Burn, burn);
                            Debug.Log($"[灼烧引爆] {card?.DisplayName} 触发：灼烧 {burn} → {enemyUnit.GetStatusAmount(StatusEffectType.Burn)}");
                        }
                        else
                        {
                            Debug.Log($"[灼烧引爆] {card?.DisplayName} 触发，但敌人没有灼烧层数");
                        }
                        break;
                    }

                    // ===== 状态联动（进阶）=====
                    case EffectType.HealPerEnemyPoison:
                    {
                        var enemyUnit = battleManager?.GetEnemy();
                        int poison = enemyUnit != null ? enemyUnit.GetStatusAmount(StatusEffectType.Poison) : 0;
                        if (effects != null && poison > 0)
                            AddEffectValue(effects, HandEffectTable.EffectType.Heal, poison * GetIntValue(eff.value));
                        break;
                    }
                    case EffectType.GainStrengthPerEnemyPoison:
                    {
                        var enemyUnit = battleManager?.GetEnemy();
                        int poison = enemyUnit != null ? enemyUnit.GetStatusAmount(StatusEffectType.Poison) : 0;
                        if (poison > 0)
                            battleManager?.GetPlayer()?.AddStatus(StatusEffectType.Strength, poison * GetIntValue(eff.value), -1);
                        break;
                    }
                    case EffectType.ExecuteBelowHpPercent:
                    {
                        var enemyUnit = battleManager?.GetEnemy();
                        if (enemyUnit != null && effects != null && enemyUnit.MaxHp > 0)
                        {
                            float hpPercent = (float)enemyUnit.CurrentHp / enemyUnit.MaxHp * 100f;
                            if (hpPercent < GetIntValue(eff.value))
                            {
                                var dmg = effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage);
                                if (dmg != null)
                                {
                                    dmg.value *= 3;
                                    dmg.description = $"造成 {dmg.value} 点伤害";
                                }
                            }
                        }
                        break;
                    }
                    case EffectType.AddDamagePerEnemyDebuff:
                    {
                        var enemyUnit = battleManager?.GetEnemy();
                        if (enemyUnit != null && effects != null)
                        {
                            int count = CountEnemyDebuffs(enemyUnit);
                            if (count > 0)
                                AddEffectValue(effects, HandEffectTable.EffectType.Damage, count * GetIntValue(eff.value));
                        }
                        break;
                    }
                    case EffectType.LoseHp:
                    {
                        var playerUnit = battleManager?.GetPlayer();
                        int v = GetIntValue(eff.value);
                        if (playerUnit != null && v > 0)
                            playerUnit.CurrentHp -= v;
                        break;
                    }
                    case EffectType.ApplySelfStatus:
                    {
                        var playerUnit = battleManager?.GetPlayer();
                        if (playerUnit != null && !string.IsNullOrEmpty(eff.status) &&
                            Enum.TryParse<StatusEffectType>(eff.status, true, out var st))
                            playerUnit.AddStatus(st, GetIntValue(eff.value), eff.duration);
                        break;
                    }
                    case EffectType.ApplyEnemyStatus:
                    {
                        var enemyUnit = battleManager?.GetEnemy();
                        if (enemyUnit != null && !string.IsNullOrEmpty(eff.status) &&
                            Enum.TryParse<StatusEffectType>(eff.status, true, out var st))
                            enemyUnit.AddStatus(st, GetIntValue(eff.value), eff.duration);
                        break;
                    }
                    case EffectType.GainBuffByDominantSuit:
                    {
                        var playerUnit = battleManager?.GetPlayer();
                        if (playerUnit != null && battleManager != null)
                        {
                            var suit = battleManager.GetDominantSuit();
                            playerUnit.AddStatus(SuitToBuffStatus(suit), GetIntValue(eff.value), eff.duration);
                        }
                        break;
                    }

                    case EffectType.GrantRandomTier1Enchant:
                    {
                        var hand = battleManager?.GetHandArea();
                        if (hand != null && runData != null)
                        {
                            // 只从「留在手牌中」的牌里选（排除本次打出的牌）
                            var played = new HashSet<CardData>(hand.GetSelectedCards());
                            var candidates = new List<CardData>();
                            foreach (var c in hand.HandCards)
                                if (!played.Contains(c)) candidates.Add(c);

                            // 随机附魔：按配置权重（weight > 0），诅咒 weight=0 不会出现
                            var pool = ConfigLoader.Config.enchantments.FindAll(e => e.weight > 0);
                            if (candidates.Count > 0 && pool.Count > 0)
                            {
                                var targetCard = candidates[UnityEngine.Random.Range(0, candidates.Count)];

                                int total = 0;
                                foreach (var e in pool) total += e.weight;
                                int roll = UnityEngine.Random.Range(0, total);
                                int cum = 0;
                                var picked = pool[0];
                                foreach (var e in pool)
                                {
                                    cum += e.weight;
                                    if (roll < cum) { picked = e; break; }
                                }

                                // 魔导给的附魔只在本场战斗有效
                                runData.AddTempEnchantment(targetCard.EnchantRank, targetCard.EnchantSuit, picked.id);
                                battleManager?.NotifyEnchantmentsChanged();
                                Debug.Log($"[魔导] {targetCard.DisplayName} 获得附魔 {picked.name}");
                            }
                        }
                        break;
                    }

                    // ===== 状态效果类 - 委托给 StatusEffectSystem（需持续时间）=====
                    case EffectType.ApplyPoison:
                        var enemy = battleManager?.GetEnemy();
                        if (enemy != null)
                            enemy.AddStatus(StatusEffectType.Poison, GetIntValue(eff.value) + (battleManager?.GetPoisonBonus() ?? 0), eff.duration);
                        break;
                    case EffectType.ApplyBurn:
                        enemy = battleManager?.GetEnemy();
                        if (enemy != null)
                            enemy.AddStatus(StatusEffectType.Burn, GetIntValue(eff.value), eff.duration);
                        break;
                    case EffectType.ApplyWeaken:
                        enemy = battleManager?.GetEnemy();
                        if (enemy != null)
                            enemy.AddStatus(StatusEffectType.Weaken, GetIntValue(eff.value), eff.duration);
                        break;

                    // ===== 随机敌人版本（余烬 / 残毒）：没有选中目标也能生效 =====
                    case EffectType.ApplyPoisonRandom:
                    {
                        var rndPoison = PickRandomAliveEnemy();
                        if (rndPoison != null)
                            rndPoison.AddStatus(StatusEffectType.Poison,
                                GetIntValue(eff.value) + (battleManager?.GetPoisonBonus() ?? 0), eff.duration);
                        break;
                    }
                    case EffectType.ApplyBurnRandom:
                    {
                        var rndBurn = PickRandomAliveEnemy();
                        if (rndBurn != null)
                            rndBurn.AddStatus(StatusEffectType.Burn, GetIntValue(eff.value), eff.duration);
                        break;
                    }

                    // ===== 复杂/延迟触发类（暂留空，后续实现）=====
                    case EffectType.DrawSpecificCard:
                    case EffectType.DuplicateHandType:
                    case EffectType.TransformHandType:
                    case EffectType.HealOnDamage:
                    case EffectType.NextPlayDamageMult:
                        break;
                }
            }
        }

        /// <summary>随机挑一名存活敌人（没有敌人时返回 null）</summary>
        private BattleUnit PickRandomAliveEnemy()
        {
            var alive = battleManager?.GetAliveEnemies();
            if (alive == null || alive.Count == 0) return null;
            return alive[UnityEngine.Random.Range(0, alive.Count)];
        }

        private bool CheckCondition(string condition, HandTypeResult handResult)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            if (handResult == null || !handResult.IsValid) return false;

            var t = handResult.type;
            switch (condition)
            {
                case "DamageDealt": return true;
                case "OnePair": return t == HandType.OnePair;
                case "TwoConsecutivePairs": return t == HandType.TwoConsecutivePairs;
                case "ThreeOfAKind": return t == HandType.ThreeOfAKind;
                case "FullHouse": return t == HandType.FullHouse;
                case "FourOfAKind": return t == HandType.FourOfAKind;
                case "StraightAny": return t == HandType.Straight3 || t == HandType.Straight4 || t == HandType.Straight5;
                case "Straight3": return t == HandType.Straight3;
                case "Straight4": return t == HandType.Straight4;
                case "Straight5": return t == HandType.Straight5;
                case "FlushAny": return t == HandType.Flush3 || t == HandType.Flush4 || t == HandType.Flush5;
                case "Flush3": return t == HandType.Flush3;
                case "Flush4": return t == HandType.Flush4;
                case "Flush5": return t == HandType.Flush5;
                case "StraightFlushAny": return t == HandType.StraightFlush3 || t == HandType.StraightFlush4 || t == HandType.StraightFlush5;
                case "StraightFlush3": return t == HandType.StraightFlush3;
                case "StraightFlush4": return t == HandType.StraightFlush4;
                case "StraightFlush5": return t == HandType.StraightFlush5;
            }
            return false;
        }

        /// <summary>
        /// 取这张牌生效的附魔列表。
        /// 用「附魔键」(EnchantRank/EnchantSuit) 而不是原始点数花色——镜牌复制的附魔存在目标牌上。
        /// 返回副本，避免遍历时被「双倍附魔 / 魔导」等写入同一列表导致异常。
        /// </summary>
        private List<int> GetCardEnchantments(CardData card)
        {
            if (runData == null || card == null) return new List<int>();
            return new List<int>(runData.GetCardEnchantments(card.EnchantRank, card.EnchantSuit));
        }

        private void AddEffectValue(List<HandEffectTable.HandEffect> effects, HandEffectTable.EffectType type, int value)
        {
            var existing = effects.Find(e => e.effectType == type);
            if (existing != null)
                existing.value += value;
            else
                effects.Add(new HandEffectTable.HandEffect { effectType = type, value = value, description = $"+{value}" });
        }

        private int GetIntValue(object value)
        {
            if (value == null) return 0;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (value is float f) return (int)f;
            if (int.TryParse(value.ToString(), out var parsed)) return parsed;
            return 0;
        }

        private float GetFloatValue(object value)
        {
            if (value == null) return 1f;
            if (value is float f) return f;
            if (value is double d) return (float)d;
            if (value is int i) return i;
            if (value is long l) return l;
            if (float.TryParse(value.ToString(), out var parsed)) return parsed;
            return 1f;
        }

        /// <summary>统计敌人身上的负面状态种类数（元素共鸣用）</summary>
        private static int CountEnemyDebuffs(BattleUnit enemy)
        {
            int count = 0;
            if (enemy.GetStatusAmount(StatusEffectType.Poison) > 0) count++;
            if (enemy.GetStatusAmount(StatusEffectType.Burn) > 0) count++;
            if (enemy.GetStatusAmount(StatusEffectType.Weaken) > 0) count++;
            if (enemy.GetStatusAmount(StatusEffectType.Vulnerable) > 0) count++;
            return count;
        }

        /// <summary>命运花色 → 对应自buff（四色印记）</summary>
        private static StatusEffectType SuitToBuffStatus(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spade: return StatusEffectType.Strength;
                case Suit.Heart: return StatusEffectType.Regeneration;
                case Suit.Club: return StatusEffectType.Focus;
                case Suit.Diamond: return StatusEffectType.Dexterity;
                default: return StatusEffectType.Strength;
            }
        }
    }
}