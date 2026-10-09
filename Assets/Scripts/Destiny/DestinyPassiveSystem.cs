using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// 命格被动的唯一来源：**全部数值 + 全部判定 + 战斗开始状态**都在这个类里。
    ///
    /// 说明：被动挂在不同游戏时机上（战斗开始 / 出牌 / 受致命伤 / 战斗胜利 / 生成奖励），
    /// 触发点天然分散，但"做什么、数值多少、判定条件"全部集中在这里：
    ///   - 常量：所有数值（改平衡只改这里）
    ///   - 静态钩子：无状态的判定/结算（只要 RunData），任何触发点直接调
    ///   - 实例状态：只有「梅花 Lv1 首回合抽牌 / 梅花 Lv3 开局附魔」需要把状态
    ///     从战斗开始带到首回合，所以由 BattleManager 持有一个实例
    ///
    /// 文案由 DestinyInfo 从这里取数生成。
    /// </summary>
    public class DestinyPassiveSystem
    {
        // ============================================================
        //  数值表（唯一来源）
        // ============================================================

        // ---- 黑桃 ----
        public const int SpadeLv1Strength = 2;               // Lv1：战斗开始 +N 层力量
        public const int SpadeLv2DamageBonus = 3;            // Lv2：每回合第 2 手起，伤害 +N
        public const float SpadeLv3FirstPlayMultiplier = 2f; // Lv3：每回合第 1 手，伤害 ×N

        // ---- 红桃 ----
        public const int HeartLv1BattleEndHeal = 5;          // Lv1：战斗结束回复 N 生命
        public const float HeartLv2DeathSavePercent = 0.3f;  // Lv2：免死时回复最大生命的 N
        public const int HeartLv3LifestealPercent = 15;      // Lv3：造成伤害回复其 N%

        // ---- 梅花 ----
        public const int ClubLv1FirstTurnDraw = 2;           // Lv1：第一回合额外抽 N 张
        public const int ClubLv2EnchantOptionCountBase = 3;  // Lv2：附魔奖励 N 选 1
        public const int ClubLv2EnchantOptionCount = 4;      //      → M 选 1
        public const int ClubLv3OpeningEnchantCount = 2;     // Lv3：开局随机 N 张手牌附魔

        // ---- 方块 ----
        public const int DiamondLv1ShopDiscountPercent = 20; // Lv1：商店降价 N%（⚠ 见文件末尾备注）
        public const int DiamondLv2GoldPerStrength = 50;     // Lv2：开局每 N 金币 +1 力量
        public const int DiamondLv3GoldGainPercent = 10;     // Lv3：战斗结束后金币 +N%

        // ---- 主动技 ----
        public const int ActiveSpadeHitsBase = 3;          // 破军：(N + 等级) 次 1 点伤害 → Lv1:4 / Lv2:5 / Lv3:6
        public const int ActiveHeartRegenBase = 2;         // 回春：N + 等级 层再生
        public const int ActiveHeartRegenTurns = 3;        //       持续 N 回合
        public const int ActiveClubDraw = 4;               // 顿悟：抽 N 张
        public const int ActiveClubEnchantLv2 = 2;         //       其中 N 张附魔
        public const int ActiveClubEnchantLv3 = 3;         //       其中 N 张附稀有附魔
        public const int ActiveDiamondGoldLv1 = 30;        // 聚宝：获得金币
        public const int ActiveDiamondGoldLv2 = 60;
        public const int ActiveDiamondGoldLv3 = 100;
        public const int ActiveDiamondDamagePerGold = 20;  //       造成 金币/N 伤害

        // ============================================================
        //  战斗开始状态（实例成员，由 BattleManager 持有一个实例）
        // ============================================================

        /// <summary>梅花 Lv1：第一回合额外抽牌张数（0 = 无）</summary>
        public int FirstTurnBonusDraw { get; private set; }

        /// <summary>梅花 Lv3：开局随机附魔张数（0 = 无）</summary>
        public int ClubOpeningEnchant { get; private set; }

        /// <summary>
        /// 战斗开始被动：黑桃 Lv1 力量 / 方块 Lv2 金币换力量 / 梅花 Lv1 首回合抽牌 / 梅花 Lv3 开局附魔。
        /// （梅花 Lv1/Lv3 的计数存到本实例，供首个玩家回合消费）
        /// </summary>
        public void ApplyCombatStart(RunData run, BattleUnit player)
        {
            FirstTurnBonusDraw = 0;
            ClubOpeningEnchant = 0;
            if (run == null || player == null) return;

            // 黑桃 Lv1：战斗开始获得力量
            if (run.GetDestinyRank(Suit.Spade) >= 1)
                player.AddStatus(StatusEffectType.Strength, SpadeLv1Strength, -1);

            // 方块 Lv2：开局每 N 金币获得 1 层力量
            if (run.GetDestinyRank(Suit.Diamond) >= 2)
            {
                int strength = run.Gold / DiamondLv2GoldPerStrength;
                if (strength > 0) player.AddStatus(StatusEffectType.Strength, strength, -1);
            }

            // 梅花 Lv1 / Lv3（计数留给首回合消费）
            if (run.GetDestinyRank(Suit.Club) >= 1) FirstTurnBonusDraw = ClubLv1FirstTurnDraw;
            if (run.GetDestinyRank(Suit.Club) >= 3) ClubOpeningEnchant = ClubLv3OpeningEnchantCount;
        }

        // ============================================================
        //  无状态钩子（静态，任何触发点直接调）
        // ============================================================

        /// <summary>
        /// 黑桃出牌被动（Lv3 第 1 手伤害翻倍 / Lv2 第 2 手起伤害 +N）。
        /// 只返回数值，具体怎么加到效果列表由 BattleManager 决定。
        /// </summary>
        public static void GetSpadePlayBonus(RunData run, int playIndex, out int addDamage, out float multiplier)
        {
            addDamage = 0;
            multiplier = 1f;
            if (run == null) return;

            int rank = run.GetDestinyRank(Suit.Spade);
            if (rank >= 3 && playIndex == 1)
                multiplier = SpadeLv3FirstPlayMultiplier;
            else if (rank >= 2 && playIndex >= 2)
                addDamage = SpadeLv2DamageBonus;
        }

        /// <summary>红桃 Lv3：是否拥有「造成伤害回复其 N% 生命」（吸血）</summary>
        public static bool HasHeartLifesteal(RunData run)
            => run != null && run.GetDestinyRank(Suit.Heart) >= 3;

        /// <summary>红桃 Lv2：免死时应该回复多少生命</summary>
        public static int GetDeathSaveHealAmount(BattleUnit player)
            => player == null ? 0 : Mathf.RoundToInt(player.MaxHp * HeartLv2DeathSavePercent);

        /// <summary>红桃 Lv2：这一局还能不能触发免死</summary>
        public static bool CanDeathSave(RunData run)
            => run != null && run.GetDestinyRank(Suit.Heart) >= 2 && !run.heartDeathSaveUsed;

        /// <summary>战斗胜利结算：红桃 Lv1 回血 / 方块 Lv3 金币增长</summary>
        public static void ApplyBattleWin(RunData run)
        {
            if (run == null) return;

            // 红桃 Lv1：战斗结束回复生命
            if (run.GetDestinyRank(Suit.Heart) >= 1)
                run.CurrentHp = Mathf.Min(run.MaxHp, run.CurrentHp + HeartLv1BattleEndHeal);

            // 方块 Lv3：金币增长
            if (run.GetDestinyRank(Suit.Diamond) >= 3)
            {
                int gain = Mathf.RoundToInt(run.Gold * DiamondLv3GoldGainPercent / 100f);
                if (gain > 0) run.Gold += gain;
            }
        }

        /// <summary>黑桃主动技：按等级决定打几次（Lv1:4 / Lv2:5 / Lv3:6）</summary>
        public static int GetActiveSpadeHits(int rank)
            => ActiveSpadeHitsBase + Mathf.Clamp(rank, 1, 3);

        /// <summary>梅花 Lv2：附魔奖励的选项数量（3 → 4）</summary>
        public static int GetEnchantOptionCount(RunData run)
            => (run != null && run.GetDestinyRank(Suit.Club) >= 2)
                ? ClubLv2EnchantOptionCount
                : ClubLv2EnchantOptionCountBase;

        /// <summary>方块 Lv1：商店价格折扣倍率（Lv1+ → 0.8；否则 1）</summary>
        public static float GetShopDiscountMultiplier(RunData run)
            => (run != null && run.GetDestinyRank(Suit.Diamond) >= 1)
                ? 1f - DiamondLv1ShopDiscountPercent / 100f
                : 1f;
    }
}
