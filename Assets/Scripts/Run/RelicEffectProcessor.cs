using System;
using System.Collections.Generic;
using System.Linq;
using Roguelike.Data;
using UnityEngine;
using Roguelike; // HandTypeResult, HandType

namespace Roguelike
{
    /// <summary>
    /// 遗物效果处理器：负责在特定触发时机应用遗物效果
    /// 纯逻辑，不持有状态，通过回调通知数值变化
    /// </summary>
    public class RelicEffectProcessor
    {
        private readonly RelicSystem relicSystem;
        
        // 事件回调 - 使用新的 delta 事件系统
        public Action<BattleUnit, int> OnPlayerHpChanged;
        public Action<BattleUnit, int> OnEnemyHpChanged;
        public Action<BattleUnit, int> OnPlayerMaxHpChanged;
        public Action<BattleUnit, int> OnEnemyMaxHpChanged;
        public Action<BattleUnit, int> OnPlayerDefenseChanged;
        public Action<BattleUnit, int> OnEnemyDefenseChanged;
        public Action<int> OnGoldChanged;
        public Action OnHandChanged;
        public Action<int> OnDeckCountChanged;
        public Action OnCardVisualsChanged;   // 牌面（花色/点数）被临时改变时通知 UI 刷新

        // 「同化之心」临时变成红桃的手牌（记录原判定花色，回合结束恢复）
        private class HeartifiedCard
        {
            public CardData card;
            public int prevJudgeSuit;
        }
        private readonly List<HeartifiedCard> heartified = new List<HeartifiedCard>();

        /// <summary>回合结束：把「同化之心」临时变成红桃的牌恢复原花色</summary>
        public void RestoreHeartified()
        {
            if (heartified.Count == 0) return;
            foreach (var h in heartified)
                if (h.card != null) h.card.judgeSuit = h.prevJudgeSuit;
            heartified.Clear();
            OnHandChanged?.Invoke();
            OnCardVisualsChanged?.Invoke();
        }
        
        // 运行时上下文（每次 ApplyEffects 传入）
        public class Context
        {
            public BattleUnit Player;
            public BattleUnit Enemy;
            public List<BattleUnit> Enemies;         // 全部存活敌人（群体效果用）
            public HandArea HandArea;
            public DeckPile DeckPile;
            public RunData RunData;
            public EnchantmentSystem EnchantmentSystem;
            public float NextPlayDamageMultiplier = 1f;
            public float DamageMultiplier = 1f;      // 本次出牌的伤害倍率（同花之王/皇家赌注等）
            public List<CardData> PlayedCards;       // 本次打出的牌（葫芦收藏家等）
            
            // 当前打出的牌型（OnHandType 触发时可用）
            public HandTypeResult HandTypeResult;
            
            // 遗物累积状态（跨触发周期保持）
            public int PendingDamageBonus = 0;
            public bool IsHandTypeDuplicated = false;
            public int HandTypeTransformLevel = 0;
            public int HealOnDamageDealt = 0;
            public int GoldOnKill = 0;
            public int RelicOnKillId = 0;
            public int PotionOnKillId = 0;
            public string CardOnKillKey = null;
        }

        public RelicEffectProcessor(RelicSystem relicSystem)
        {
            this.relicSystem = relicSystem;
        }

        /// <summary>
        /// 应用指定触发器的所有遗物效果
        /// </summary>
        public void ApplyEffects(string trigger, Context ctx)
        {
            if (relicSystem == null || ctx == null) return;

            var effects = relicSystem.GetEffectsByTrigger(trigger);
            foreach (var effect in effects)
            {
                ApplySingleEffect(effect, ctx);
            }
        }

        /// <summary>
        /// 应用牌型效果（包含遗物额外加成）
        /// </summary>
        public void ApplyHandTypeEffects(List<HandEffectTable.HandEffect> effects, Context ctx)
        {
            int lastDamageDealt = 0;

            // 先结算「防御」，再结算其它效果。
            // 原因：造成伤害会触发敌人的荆棘反伤，如果防御还没加上，反伤就直接扣血了。
            // （其它效果保持配置里的原顺序，比如吸血要在伤害之后）
            var ordered = new List<HandEffectTable.HandEffect>(effects.Count);
            ordered.AddRange(effects.FindAll(e => e.effectType == HandEffectTable.EffectType.Defense));
            ordered.AddRange(effects.FindAll(e => e.effectType != HandEffectTable.EffectType.Defense));

            foreach (var effect in ordered)
            {
                switch (effect.effectType)
                {
                    case HandEffectTable.EffectType.Damage:
                        float permMult = ctx.RunData != null ? ctx.RunData.permanentDamageMultiplier : 1f;
                        int rawDamage = Mathf.RoundToInt((effect.value + ctx.PendingDamageBonus) * ctx.NextPlayDamageMultiplier * ctx.DamageMultiplier * permMult);
                        ctx.PendingDamageBonus = 0;
                        rawDamage = Mathf.Max(0, rawDamage);
                        // 攻击方状态：力量/虚弱/专注
                        int dealtDamage = ctx.Player.DealDamage(rawDamage);
                        lastDamageDealt = dealtDamage;
                        // 受击方状态：易伤/无形 + 荆棘反伤（没有选中敌人时跳过）
                        if (ctx.Enemy != null && !ctx.Enemy.IsDead)
                            ctx.Enemy.TakeDamage(dealtDamage, ctx.Player);
                        // BattleUnit.TakeDamage 触发 OnHpChanged/OnDefenseChanged，BattleManager 自动转发
                        
                        ctx.EnchantmentSystem?.OnDealDamage(dealtDamage);
                        ApplyEffects("OnDealDamage", ctx);
                        
                        if (ctx.HealOnDamageDealt > 0)
                        {
                            ctx.Player.Heal(ctx.HealOnDamageDealt);
                            // BattleUnit.CurrentHp setter triggers event, BattleManager forwards it
                        }
                        break;

                    case HandEffectTable.EffectType.Defense:
                        ctx.Player.AddDefense(effect.value);
                        // BattleUnit.Defense setter triggers event, BattleManager forwards it
                        break;

                    case HandEffectTable.EffectType.DrawCard:
                        var drawn = ctx.DeckPile.Draw(effect.value);
                        var drawOverflow = ctx.HandArea.AddCards(drawn);
                        if (drawOverflow != null && drawOverflow.Count > 0) ctx.DeckPile.ReturnToDeck(drawOverflow);
                        Debug.Log($"额外抽了 {drawn.Count} 张牌");
                        OnHandChanged?.Invoke();
                        OnDeckCountChanged?.Invoke(ctx.DeckPile.Count);
                        break;

                    case HandEffectTable.EffectType.Heal:
                        ctx.Player.Heal(effect.value);
                        // BattleUnit.CurrentHp setter triggers event, BattleManager forwards it
                        break;

                    case HandEffectTable.EffectType.HealPercentOfDamage:
                        if (lastDamageDealt > 0)
                            ctx.Player.Heal(Mathf.RoundToInt(lastDamageDealt * effect.value / 100f));
                        break;
                }
            }
        }

        private void ApplySingleEffect(RelicEffectData effect, Context ctx)
        {
            if (ctx.RunData == null) return;

            Debug.Log($"[RelicProcessor] ApplyEffect: trigger={effect.trigger}, type={effect.type}, value={effect.value}");

            // 条件门控（如 Straight5/FlushAny/FullHouse 等）
            if (!string.IsNullOrEmpty(effect.condition))
            {
                if (ctx.HandTypeResult == null || !CheckHandTypeCondition(effect.condition, ctx.HandTypeResult))
                    return;
            }

            switch (effect.type)
            {
                // ===== Passive / CombatStart / TurnStart 数值类 =====
                case "AddMaxHp":
                case "AddMaxHpPermanent":
                    int hpIncrease = GetIntValue(effect.value);
                    ctx.Player.MaxHp += hpIncrease;
                    ctx.Player.CurrentHp += hpIncrease;
                    // BattleUnit.MaxHp/CurrentHp setters trigger events, BattleManager forwards them
                    break;

                case "AddDefense":
                case "AddTempDefense":
                    int defenseGain = GetIntValue(effect.value);
                    ctx.Player.AddDefense(defenseGain);
                    // BattleUnit.Defense setter triggers event, BattleManager forwards it
                    break;

                case "AddGold":
                    int goldAmount = GetIntValue(effect.value);
                    int finalGold = (int)(goldAmount * relicSystem.GetMultiplier("MultiplyGold", 1f));
                    ctx.RunData.Gold += finalGold;
                    OnGoldChanged?.Invoke(finalGold);
                    break;

                // ===== 状态标记类（跨战斗持久）=====
                case "NextBattleEnemyBuff":
                    ctx.RunData.nextBattleEnemyBuff += GetIntValue(effect.value);
                    break;

                case "NextBattleStartHp":
                    ctx.RunData.nextBattleStartHpMod += GetIntValue(effect.value);
                    break;

                case "AddPotionSlot":
                    // 药水槽容量由 RunData.PotionSlotCap 实时从遗物计算，这里无需处理
                    break;

                case "GainRandomPotion":
                {
                    var potions = Roguelike.Data.ConfigLoader.Config?.potions;
                    if (potions != null)
                    {
                        var pool = potions.FindAll(p => p.rarity != "Event");
                        if (pool.Count > 0)
                        {
                            var p = pool[UnityEngine.Random.Range(0, pool.Count)];
                            ctx.RunData.TryAddPotion(p.id);
                        }
                    }
                    break;
                }

                // ===== OnCardPlayed 即时效果 =====
                case "AddDamageOnPlay":
                    ctx.PendingDamageBonus += GetIntValue(effect.value);
                    break;

                case "AddDrawOnPlay":
                    var drawn = ctx.DeckPile.Draw(GetIntValue(effect.value));
                    var overflow = ctx.HandArea.AddCards(drawn);
                    if (overflow != null && overflow.Count > 0) ctx.DeckPile.ReturnToDeck(overflow);
                    OnHandChanged?.Invoke();
                    OnDeckCountChanged?.Invoke(ctx.DeckPile.Count);
                    break;

                case "AddHealOnPlay":
                    int healAmount = GetIntValue(effect.value);
                    int hpBefore = ctx.Player.CurrentHp;
                    ctx.Player.Heal(healAmount);
                    // BattleUnit.CurrentHp setter triggers event, BattleManager forwards it
                    break;

                // ===== 新增遗物效果 =====
                case "AddStrength":
                    ctx.Player.AddStatus(StatusEffectType.Strength, GetIntValue(effect.value), -1);
                    break;

                case "MultiplyDamage":
                    ctx.DamageMultiplier *= GetFloatValue(effect.value);
                    break;

                case "GainGoldFromFullHouse":
                    GainGoldFromFullHouse(ctx);
                    break;

                case "ApplyVulnerable":
                {
                    // 对敌方所有存活角色生效
                    int vulnAmount = GetIntValue(effect.value);
                    if (ctx.Enemies != null && ctx.Enemies.Count > 0)
                    {
                        foreach (var e in ctx.Enemies)
                            if (e != null && !e.IsDead) e.AddStatus(StatusEffectType.Vulnerable, vulnAmount, effect.duration);
                    }
                    else if (ctx.Enemy != null && !ctx.Enemy.IsDead)
                    {
                        ctx.Enemy.AddStatus(StatusEffectType.Vulnerable, vulnAmount, effect.duration);
                    }
                    break;
                }

                case "ApplyThorns":
                    ctx.Player.AddStatus(StatusEffectType.Thorns, GetIntValue(effect.value), -1);
                    break;

                // ===== OnHandType 牌型增强 =====
                case "DuplicateHandType":
                    ctx.IsHandTypeDuplicated = true;
                    break;

                case "TransformHandType":
                    ctx.HandTypeTransformLevel = Mathf.Max(ctx.HandTypeTransformLevel, GetIntValue(effect.value));
                    break;

                case "ApplyPoison":
                case "ApplyBurn":
                case "ApplyWeaken":
                    if (ctx.HandTypeResult != null && ctx.HandTypeResult.IsValid)
                    {
                        // 可选：检查 effect.condition 限定特定牌型
                        if (CheckHandTypeCondition(effect.condition, ctx.HandTypeResult))
                        {
                            var statusType = effect.type == "ApplyPoison" ? StatusEffectType.Poison :
                                             effect.type == "ApplyBurn" ? StatusEffectType.Burn :
                                             StatusEffectType.Weaken;
                            int amount = GetIntValue(effect.value);
                            if (statusType == StatusEffectType.Poison && relicSystem != null)
                                amount += relicSystem.GetFlatBonus("PoisonBonus", 0);
                            int duration = effect.duration;
                            if (ctx.Enemy != null && !ctx.Enemy.IsDead)
                                ctx.Enemy.AddStatus(statusType, amount, duration);
                        }
                    }
                    break;

                // ===== OnDealDamage 伤害后效果 =====
                case "HealOnDamageDealt":
                    ctx.HealOnDamageDealt += GetIntValue(effect.value);
                    break;

                // ===== OnKill 击杀奖励 =====
                case "AddGoldOnKill":
                    ctx.GoldOnKill += GetIntValue(effect.value);
                    break;

                case "AddRelicOnKill":
                    ctx.RelicOnKillId = effect.value is int id ? id : 0;
                    break;

                case "AddPotionOnKill":
                    ctx.PotionOnKillId = effect.value is int id2 ? id2 : 0;
                    break;

                case "AddCardOnKill":
                    ctx.CardOnKillKey = effect.value?.ToString();
                    break;

                // ===== 红桃事件遗物 =====

                // 「滋养手环」：回合结束时最大生命 +value、生命 +value（每局最多 duration 次，默认 5）
                case "AddMaxHpAndHealPerTurn":
                {
                    int per = Mathf.Max(1, GetIntValue(effect.value));
                    int cap = effect.duration > 0 ? effect.duration : 5;
                    if (ctx.RunData.GetRelicCounter("nourish") >= cap) break;
                    ctx.RunData.AddRelicCounter("nourish", 1);
                    ctx.Player.MaxHp += per;
                    ctx.Player.CurrentHp += per;
                    break;
                }

                // 「同化之心」：随机把 value 张非红桃手牌临时变为红桃（回合结束恢复）
                case "HeartifyHandCard":
                {
                    int want = Mathf.Max(1, GetIntValue(effect.value));
                    var hand = ctx.HandArea != null ? ctx.HandArea.HandCards : null;
                    if (hand == null || hand.Count == 0) break;

                    var candidates = new List<CardData>();
                    foreach (var c in hand)
                    {
                        if (c == null || c.EffectiveSuit == Suit.Heart) continue;
                        // 本次打出的牌不算（它们马上要离手）
                        if (ctx.PlayedCards != null && ctx.PlayedCards.Contains(c)) continue;
                        candidates.Add(c);
                    }

                    for (int i = candidates.Count - 1; i > 0; i--)
                    {
                        int j = UnityEngine.Random.Range(0, i + 1);
                        var t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t;
                    }

                    int n = Mathf.Min(want, candidates.Count);
                    for (int i = 0; i < n; i++)
                    {
                        heartified.Add(new HeartifiedCard { card = candidates[i], prevJudgeSuit = candidates[i].judgeSuit });
                        candidates[i].SetSuitOverride((int)Suit.Heart);
                    }
                    if (n > 0)
                    {
                        OnHandChanged?.Invoke();
                        OnCardVisualsChanged?.Invoke();
                    }
                    break;
                }

                // 「半人石像」：出牌时，生命 ≥50% 抽 1 张并失去 2 点生命；否则回复 1 点生命
                case "HalfHpPlayEffect":
                {
                    if (ctx.Player == null) break;
                    float pct = ctx.Player.MaxHp > 0 ? (float)ctx.Player.CurrentHp / ctx.Player.MaxHp : 1f;
                    if (pct >= 0.5f)
                    {
                        ctx.Player.TakeDamage(1);
                        if (ctx.DeckPile != null && ctx.HandArea != null)
                        {
                            var bonusDraw = ctx.DeckPile.Draw(1);
                            var over = ctx.HandArea.AddCards(bonusDraw);
                            if (over != null && over.Count > 0) ctx.DeckPile.ReturnToDeck(over);
                            OnHandChanged?.Invoke();
                            OnDeckCountChanged?.Invoke(ctx.DeckPile.Count);
                        }
                    }
                    else
                    {
                        ctx.Player.Heal(1);
                    }
                    break;
                }

                // 「魔导书」：一次打出 5 张牌时，其中随机 value 张获得随机附魔
                case "GrantRandomEnchantToPlayedCards":
                {
                    if (ctx.PlayedCards == null || ctx.PlayedCards.Count != 5) break;
                    int want = Mathf.Max(1, GetIntValue(effect.value));

                    var pool = new List<CardData>(ctx.PlayedCards);
                    for (int i = pool.Count - 1; i > 0; i--)
                    {
                        int j = UnityEngine.Random.Range(0, i + 1);
                        var t = pool[i]; pool[i] = pool[j]; pool[j] = t;
                    }

                    int n = Mathf.Min(want, pool.Count);
                    int granted = 0;
                    for (int i = 0; i < n; i++)
                    {
                        int enchId = PickRandomEnchantment();
                        if (enchId <= 0) break;
                        // 和「魔导」附魔一样：只在本场战斗有效
                        ctx.RunData.AddTempEnchantment(pool[i].EnchantRank, pool[i].EnchantSuit, enchId);
                        granted++;
                    }
                    if (granted > 0)
                    {
                        Debug.Log($"[魔导书] 打出 5 张牌，{granted} 张获得随机附魔（仅本场战斗）");
                        OnCardVisualsChanged?.Invoke();
                    }
                    break;
                }

                // ===== 其他暂不处理 =====
                case "MultiplyGold":
                case "MultiplyPotionEffect":
                case "ModifyShopPrice":
                case "GainRelicEffect":
                case "NextPlayDamageMult":
                case "ReduceCost":
                case "GainEnergy":
                case "ExhaustCard":
                case "RetainCard":
                case "UpgradeCard":
                case "RemoveCard":
                case "CopyCard":
                case "ChangeCardType":
                    break;
            }
        }

        // 随机附魔池（weight > 0 的附魔；诅咒 weight=0 不会出现）
        private static List<EnchantmentData> randomEnchantPool;

        /// <summary>按 weight 加权随机一个可用附魔 id（0 = 无）</summary>
        private static int PickRandomEnchantment()
        {
            if (randomEnchantPool == null)
                randomEnchantPool = ConfigLoader.Config.enchantments.FindAll(e => e.weight > 0);
            if (randomEnchantPool.Count == 0) return 0;

            int total = 0;
            foreach (var e in randomEnchantPool) total += e.weight;
            int roll = UnityEngine.Random.Range(0, total);
            int cum = 0;
            foreach (var e in randomEnchantPool)
            {
                cum += e.weight;
                if (roll < cum) return e.id;
            }
            return randomEnchantPool[0].id;
        }

        private int GetIntValue(object value)        {
            if (value == null) return 0;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
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

        /// <summary>葫芦收藏家：获得对子点数 + 三条点数 的金币</summary>
        private void GainGoldFromFullHouse(Context ctx)
        {
            if (ctx.PlayedCards == null || ctx.PlayedCards.Count == 0) return;

            var groups = ctx.PlayedCards.GroupBy(c => c.EffectiveRank).OrderByDescending(g => g.Count()).ToList();
            if (groups.Count < 2) return;

            int triple = groups[0].Count() >= 3 ? groups[0].Key : groups[1].Key;
            int pair = groups[0].Count() == 2 ? groups[0].Key : groups[1].Key;
            int gold = triple + pair;

            ctx.RunData.Gold += gold;
            OnGoldChanged?.Invoke(gold);
        }

        private bool CheckHandTypeCondition(string condition, HandTypeResult result)
        {
            if (string.IsNullOrEmpty(condition)) return true;

            return condition switch
            {
                "StraightAny" => result.type == HandType.Straight3 || result.type == HandType.Straight4 || result.type == HandType.Straight5,
                "FlushAny" => result.type == HandType.Flush3 || result.type == HandType.Flush4 || result.type == HandType.Flush5,
                "StraightFlushAny" => result.type == HandType.StraightFlush3 || result.type == HandType.StraightFlush4 || result.type == HandType.StraightFlush5,
                "OnePair" => result.type == HandType.OnePair,
                "TwoConsecutivePairs" => result.type == HandType.TwoConsecutivePairs,
                "ThreeOfAKind" => result.type == HandType.ThreeOfAKind,
                "Straight3" => result.type == HandType.Straight3,
                "Straight4" => result.type == HandType.Straight4,
                "Straight5" => result.type == HandType.Straight5,
                "Flush3" => result.type == HandType.Flush3,
                "Flush4" => result.type == HandType.Flush4,
                "Flush5" => result.type == HandType.Flush5,
                "FullHouse" => result.type == HandType.FullHouse,
                "FourOfAKind" => result.type == HandType.FourOfAKind,
                "StraightFlush3" => result.type == HandType.StraightFlush3,
                "StraightFlush4" => result.type == HandType.StraightFlush4,
                "StraightFlush5" => result.type == HandType.StraightFlush5,
                _ => true
            };
        }
    }
}