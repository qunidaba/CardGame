using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 敌人能力系统依赖的宿主能力（由 BattleManager 实现）。
    /// </summary>
    public interface IEnemyAbilityContext
    {
        IReadOnlyList<BattleUnit> Enemies { get; }
        DeckPile Deck { get; }
        BattleUnit CurrentTarget { get; }
        EnemyData GetEnemyData(BattleUnit unit);
    }

    /// <summary>
    /// 敌人能力系统：吞噬（吞玩家抽牌堆的牌，敌人死亡时归还）+ 弱点被动。
    /// 从 BattleManager 抽出（纯搬运，逻辑未改动）。
    /// </summary>
    public class EnemyAbilitySystem
    {
        private readonly IEnemyAbilityContext ctx;

        // 「吞噬」：每个敌人吞掉的牌（本场战斗内有效；敌人死亡时归还到抽牌堆）
        private readonly Dictionary<BattleUnit, List<CardData>> swallowedCards =
            new Dictionary<BattleUnit, List<CardData>>();

        // 敌人被动「弱点」：每个敌人本回合的弱点牌型（每回合刷新）
        private readonly Dictionary<BattleUnit, List<WeaknessType>> weaknesses =
            new Dictionary<BattleUnit, List<WeaknessType>>();

        public EnemyAbilitySystem(IEnemyAbilityContext ctx)
        {
            this.ctx = ctx;
        }

        /// <summary>战斗开始时清空（新一场战斗）</summary>
        public void Clear()
        {
            swallowedCards.Clear();
            weaknesses.Clear();
        }

        // ===== 吞噬 =====

        /// <summary>吞噬：从玩家抽牌堆里随机吞掉 N 张牌，作为状态挂在敌人身上；敌人死亡时归还。</summary>
        public void ApplySwallow(int count, BattleUnit attacker)
        {
            var deck = ctx.Deck;
            if (deck == null || attacker == null || count <= 0) return;

            var pool = deck.GetCards();   // 抽牌堆快照
            if (pool.Count == 0)
            {
                Debug.Log($"{attacker.Name} 吞噬：抽牌堆已空，没有牌可吞");
                return;
            }

            // 洗牌后取前 N 张
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            if (!swallowedCards.TryGetValue(attacker, out var eaten))
            {
                eaten = new List<CardData>();
                swallowedCards[attacker] = eaten;
            }

            int n = Mathf.Min(count, pool.Count);
            for (int i = 0; i < n; i++)
            {
                var card = pool[i];
                if (!deck.RemoveFromDeck(card)) continue;

                eaten.Add(card);
                Debug.Log($"{attacker.Name} 吞噬：吞掉了 {card.DisplayName}（剩余牌堆 {deck.Count}）");
            }

            // 一个 buff 汇总所有被吞的牌（层数 = 张数）
            if (eaten.Count > 0)
                attacker.StatusEffects.SetStatus(StatusEffectType.Swallow, eaten.Count, -1);
        }

        /// <summary>某个敌人当前吞掉的牌（没有则返回 null；供 UI 显示用）</summary>
        public List<CardData> GetSwallowedCards(BattleUnit unit)
            => (unit != null && swallowedCards.TryGetValue(unit, out var list)) ? list : null;

        /// <summary>敌人阵亡：把它吞掉的牌全部归还到抽牌堆，并清掉对应的状态图标</summary>
        public void ReturnSwallowedCardsOfDeadEnemies()
        {
            if (swallowedCards.Count == 0) return;

            List<BattleUnit> cleared = null;
            foreach (var kvp in swallowedCards)
            {
                var unit = kvp.Key;
                if (unit == null || !unit.IsDead) continue;
                if (kvp.Value == null || kvp.Value.Count == 0)
                {
                    (cleared ??= new List<BattleUnit>()).Add(unit);
                    continue;
                }

                ctx.Deck?.ReturnToDeck(kvp.Value);
                unit.StatusEffects.RemoveStatus(StatusEffectType.Swallow);

                Debug.Log($"{unit.Name} 阵亡：归还 {kvp.Value.Count} 张被吞的牌 → 抽牌堆");
                foreach (var c in kvp.Value) Debug.Log($"  - {c.DisplayName}");

                (cleared ??= new List<BattleUnit>()).Add(unit);
            }

            if (cleared != null)
                foreach (var u in cleared) swallowedCards.Remove(u);
        }

        // ===== 弱点（愤怒骷髅头被动）=====

        /// <summary>该敌人是否拥有「弱点」被动</summary>
        private bool HasWeaknessPassive(BattleUnit unit)
        {
            var data = ctx.GetEnemyData(unit);
            return data != null && data.passive == "Weakness";
        }

        /// <summary>某个敌人本回合的弱点牌型（没有则返回 null；供 UI 显示用）</summary>
        public List<WeaknessType> GetWeaknesses(BattleUnit unit)
            => (unit != null && weaknesses.TryGetValue(unit, out var list)) ? list : null;

        /// <summary>玩家回合开始：给所有拥有「弱点」被动的敌人刷新 2 个弱点牌型</summary>
        public void RefreshWeaknesses()
        {
            foreach (var unit in ctx.Enemies)
            {
                if (unit == null || unit.IsDead) continue;
                if (!HasWeaknessPassive(unit)) continue;

                // 从 8 类里随机取 2 个不同的
                var pool = new List<WeaknessType>(WeaknessInfo.All);
                var picked = new List<WeaknessType>();
                for (int i = 0; i < 2 && pool.Count > 0; i++)
                {
                    int idx = UnityEngine.Random.Range(0, pool.Count);
                    picked.Add(pool[idx]);
                    pool.RemoveAt(idx);
                }

                weaknesses[unit] = picked;
                unit.StatusEffects.SetStatus(StatusEffectType.Weakness, picked.Count, -1);

                var names = new List<string>();
                foreach (var w in picked) names.Add(WeaknessInfo.Name(w));
                Debug.Log($"[弱点] {unit.Name} 本回合弱点：{string.Join("、", names)}");
            }
        }

        /// <summary>玩家用某牌型出牌：若不是该敌人的弱点牌型，则敌人获得 1 层力量</summary>
        public void ApplyWeaknessOnPlay(HandType type)
        {
            var target = ctx.CurrentTarget;
            if (target == null || target.IsDead) return;
            if (!HasWeaknessPassive(target)) return;

            var list = GetWeaknesses(target);
            if (list == null || list.Count == 0) return;

            var category = WeaknessInfo.FromHandType(type);
            if (list.Contains(category)) return;   // 打中弱点，不激怒

            target.AddStatus(StatusEffectType.Strength, 1, -1);
            Debug.Log($"[愤怒骷髅头] {target.Name} 被「{WeaknessInfo.Name(category)}」攻击（非弱点），获得 1 层力量");
        }
    }
}