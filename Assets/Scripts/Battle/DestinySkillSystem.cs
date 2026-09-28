using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 命格主动技依赖的宿主能力（由 BattleManager 实现）。
    /// </summary>
    public interface IDestinySkillContext
    {
        RunData Run { get; }
        BattleUnit Player { get; }
        BattleUnit CurrentEnemy { get; }
        bool IsBattleOver { get; }
        List<BattleUnit> GetAliveEnemies();
        float HitInterval { get; }

        List<CardData> DrawToHand(int count);
        void GrantRandomEnchants(List<CardData> cards, int count, int minTier);
        void NotifyEnchantmentsChanged();
        void NotifyDestinyChanged();
        void NotifyHandChanged();
        void CheckBattleEnd();
    }

    /// <summary>
    /// 命格主动技系统：释放主命格主动技能（破军 / 回春 / 顿悟 / 聚宝）。
    /// 从 BattleManager 抽出（纯搬运，逻辑未改动）。
    /// </summary>
    public class DestinySkillSystem
    {
        private readonly IDestinySkillContext ctx;

        public DestinySkillSystem(IDestinySkillContext ctx)
        {
            this.ctx = ctx;
        }

        /// <summary>释放主命格主动技能（需命运之力攒满）</summary>
        public bool Activate()
        {
            var run = ctx.Run;
            if (run == null || !run.HasMainDestiny) return false;
            if (run.fatePower < RunData.FatePowerMax) return false;

            var suit = (Suit)run.mainDestinySuit;
            int rank = run.GetDestinyRank(suit);
            if (rank < 1) rank = 1;

            // 释放：清空命运之力
            run.fatePower = 0;
            run.NotifyDestinyChanged();
            ctx.NotifyDestinyChanged();
            Debug.Log($"[命格] 释放主动技能 {DestinyInfo.GetActiveName(suit)}（Lv{rank}）");

            switch (suit)
            {
                case Suit.Spade: // 破军：对随机敌人打 N 次 1 点伤害（协程，避免同时结算）
                    CoroutineRunner.Instance.StartCoroutine(SpadeRoutine(DestinyEffects.GetActiveSpadeHits(rank)));
                    return true;

                case Suit.Heart: // 回春：清除负面 + 再生
                    ctx.Player.RemoveStatus(StatusEffectType.Poison);
                    ctx.Player.RemoveStatus(StatusEffectType.Burn);
                    ctx.Player.RemoveStatus(StatusEffectType.Weaken);
                    ctx.Player.RemoveStatus(StatusEffectType.Vulnerable);
                    ctx.Player.AddStatus(StatusEffectType.Regeneration,
                        DestinyEffects.ActiveHeartRegenBase + rank, DestinyEffects.ActiveHeartRegenTurns);
                    break;

                case Suit.Club: // 顿悟：抽 N 张，按等级附魔
                {
                    var drawn = ctx.DrawToHand(DestinyEffects.ActiveClubDraw);
                    int enchCount = rank == 1 ? 0 : (rank == 2 ? DestinyEffects.ActiveClubEnchantLv2 : DestinyEffects.ActiveClubEnchantLv3);
                    int minTier = rank >= 3 ? 2 : 1;
                    ctx.GrantRandomEnchants(drawn, enchCount, minTier);
                    break;
                }

                case Suit.Diamond: // 聚宝：金币 + 伤害
                {
                    int gold = rank == 1 ? DestinyEffects.ActiveDiamondGoldLv1
                             : (rank == 2 ? DestinyEffects.ActiveDiamondGoldLv2 : DestinyEffects.ActiveDiamondGoldLv3);
                    run.Gold += gold;
                    var goldTarget = ctx.CurrentEnemy;
                    if (rank >= 2 && goldTarget != null && !goldTarget.IsDead)
                    {
                        int dmg = run.Gold / DestinyEffects.ActiveDiamondDamagePerGold;
                        if (dmg > 0) goldTarget.TakeDamage(ctx.Player.DealDamage(dmg), ctx.Player);
                    }
                    break;
                }
            }

            ctx.NotifyHandChanged();
            ctx.CheckBattleEnd();
            return true;
        }

        /// <summary>黑桃主动技「破军」：对随机敌人造成 hits 次 1 点伤害（每次目标重新随机，可联动力量）</summary>
        private IEnumerator SpadeRoutine(int hits)
        {
            for (int i = 0; i < hits; i++)
            {
                if (ctx.IsBattleOver) break;

                var alive = ctx.GetAliveEnemies();
                if (alive.Count == 0) break;

                // 每一击都重新随机选一个存活敌人
                var skillTarget = alive[UnityEngine.Random.Range(0, alive.Count)];
                skillTarget.TakeDamage(ctx.Player.DealDamage(1), ctx.Player);

                if (i < hits - 1)
                    yield return new WaitForSeconds(ctx.HitInterval);
            }
            ctx.NotifyHandChanged();
            ctx.CheckBattleEnd();
        }
    }
}