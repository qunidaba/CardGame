using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 药水系统依赖的宿主能力（由 BattleManager 实现）。
    /// 只暴露药水真正需要的东西，避免 PotionSystem 直接依赖整个 BattleManager。
    /// </summary>
    public interface IPotionContext
    {
        RunData Run { get; }
        BattleUnit Player { get; }
        BattleUnit CurrentEnemy { get; }
        IReadOnlyList<CardData> HandCards { get; }

        int PoisonBonus { get; }
        void SetNextPlayDamageMultiplier(float mult);
        void DrawToHand(int count);
        void NotifyCardVisualsChanged();
        void NotifyPotionsChanged();
    }

    /// <summary>
    /// 药水系统：使用药水 + 待处理的点数修正。
    /// 从 BattleManager 抽出（纯搬运，逻辑未改动）。
    /// </summary>
    public class PotionSystem
    {
        private readonly IPotionContext ctx;

        public PotionSystem(IPotionContext ctx)
        {
            this.ctx = ctx;
        }

        /// <summary>待处理的卡牌点数修正（药水 +1/-1，等待玩家点选手牌）</summary>
        public int PendingRankShift { get; private set; } = 0;
        public bool HasPendingRankShift => PendingRankShift != 0;

        /// <summary>使用药水（战斗中）</summary>
        public bool UsePotion(int potionId)
        {
            var run = ctx.Run;
            if (run == null) return false;
            if (!run.PotionIds.Contains(potionId)) return false;

            var potion = ConfigLoader.GetPotion(potionId);
            if (potion == null || potion.effect == null) return false;

            int val = GetIntValue(potion.effect.value);
            int dur = potion.effect.duration;

            switch (potion.effect.type)
            {
                case "HealPercent":
                    ctx.Player.Heal(Mathf.RoundToInt(ctx.Player.MaxHp * GetFloatValue(potion.effect.value)));
                    break;
                case "NextPlayDamageMult":
                    ctx.SetNextPlayDamageMultiplier(GetFloatValue(potion.effect.value));
                    break;
                case "GainDefense":
                    ctx.Player.AddDefense(val);
                    break;
                case "DrawCards":
                    ctx.DrawToHand(val);
                    break;
                case "ApplyStatus":
                {
                    if (string.IsNullOrEmpty(potion.effect.status) ||
                        !Enum.TryParse<StatusEffectType>(potion.effect.status, true, out var st))
                        break;

                    bool toEnemy = string.IsNullOrEmpty(potion.effect.target) ||
                                   potion.effect.target.Equals("enemy", StringComparison.OrdinalIgnoreCase);
                    if (toEnemy)
                    {
                        int amt = val;
                        if (st == StatusEffectType.Poison) amt += ctx.PoisonBonus;
                        ctx.CurrentEnemy?.AddStatus(st, amt, dur);
                    }
                    else
                    {
                        ctx.Player.AddStatus(st, val, dur);
                    }
                    break;
                }
                case "CardRankShift":
                    // 等待玩家点选手牌后应用
                    PendingRankShift = val;
                    Debug.Log($"[药水] 请选择一张手牌进行点数 {(val >= 0 ? "+" : "")}{val}");
                    break;
                case "SetHandSuit":
                {
                    // 把手中的牌全部变为指定花色（判定花色）
                    string suitName = potion.effect.value != null ? potion.effect.value.ToString() : "Heart";
                    if (!Enum.TryParse<Suit>(suitName, true, out var targetSuit))
                        targetSuit = Suit.Heart;

                    int changed = 0;
                    foreach (var c in ctx.HandCards)
                    {
                        if (c.EffectiveSuit == targetSuit) continue;
                        c.SetSuitOverride((int)targetSuit);
                        changed++;
                    }
                    ctx.NotifyCardVisualsChanged();
                    Debug.Log($"[药水] {changed} 张手牌变为 {targetSuit}");
                    break;
                }
            }

            run.TryRemovePotion(potionId);
            ctx.NotifyPotionsChanged();
            Debug.Log($"[药水] 使用 {potion.name}");
            return true;
        }

        /// <summary>把待处理的点数修正应用到指定手牌</summary>
        public bool ApplyPendingRankShift(CardData card)
        {
            if (PendingRankShift == 0 || card == null) return false;

            int newRank = Mathf.Clamp(card.EffectiveRank + PendingRankShift, 2, 14);
            card.SetJudgeOverride(newRank, (int)card.EffectiveSuit);
            PendingRankShift = 0;
            ctx.NotifyCardVisualsChanged();
            Debug.Log($"[药水] 点数修正完成：{card.DisplayName}");
            return true;
        }

        // ===== 配置值解析 =====

        private static int GetIntValue(object value)
        {
            if (value == null) return 0;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (value is float f) return (int)f;
            if (int.TryParse(value.ToString(), out var p)) return p;
            return 0;
        }

        private static float GetFloatValue(object value)
        {
            if (value == null) return 0f;
            if (value is float f) return f;
            if (value is double d) return (float)d;
            if (value is int i) return i;
            if (value is long l) return l;
            if (float.TryParse(value.ToString(), out var p)) return p;
            return 0f;
        }
    }
}