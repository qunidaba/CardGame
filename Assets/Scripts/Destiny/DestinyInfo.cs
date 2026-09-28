using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// 命格文案：被动与主动技能的描述（供 UI 显示）
    /// </summary>
    public static class DestinyInfo
    {
        public static string SuitName(Suit s)
        {
            switch (s)
            {
                case Suit.Spade: return "黑桃·战争";
                case Suit.Heart: return "红桃·生命";
                case Suit.Club: return "梅花·成长";
                case Suit.Diamond: return "方块·财富";
                default: return "?";
            }
        }

        public static string SuitSymbol(Suit s)
        {
            switch (s)
            {
                case Suit.Spade: return "♠";
                case Suit.Heart: return "♥";
                case Suit.Club: return "♣";
                case Suit.Diamond: return "♦";
                default: return "?";
            }
        }

        /// <summary>纯花色名（黑桃/红桃/梅花/方块），不带命格后缀</summary>
        public static string PlainSuitName(Suit s)
        {
            switch (s)
            {
                case Suit.Spade: return "黑桃";
                case Suit.Heart: return "红桃";
                case Suit.Club: return "梅花";
                case Suit.Diamond: return "方块";
                default: return "?";
            }
        }

        /// <summary>返回该花色全部 3 级被动文案（数值取自 DestinyPassiveSystem，保证和实际效果一致）</summary>
        public static string[] GetPassives(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spade:
                    return new[]
                    {
                        $"Lv1：战斗开始获得 {DestinyPassiveSystem.SpadeLv1Strength} 层力量",
                        $"Lv2：每回合第 2 手及以后出牌伤害 +{DestinyPassiveSystem.SpadeLv2DamageBonus}",
                        "Lv3：每回合第 1 手出牌伤害翻倍"
                    };
                case Suit.Heart:
                    return new[]
                    {
                        $"Lv1：战斗结束回复 {DestinyPassiveSystem.HeartLv1BattleEndHeal} 生命",
                        $"Lv2：每局一次，受到致命伤害时使其无效并回复 {Mathf.RoundToInt(DestinyPassiveSystem.HeartLv2DeathSavePercent * 100)}% 生命",
                        $"Lv3：造成伤害回复其 {DestinyPassiveSystem.HeartLv3LifestealPercent}% 生命"
                    };
                case Suit.Club:
                    return new[]
                    {
                        $"Lv1：每场战斗第一回合额外抽 {DestinyPassiveSystem.ClubLv1FirstTurnDraw} 张",
                        $"Lv2：附魔奖励 {DestinyPassiveSystem.ClubLv2EnchantOptionCountBase} 选 1 → {DestinyPassiveSystem.ClubLv2EnchantOptionCount} 选 1",
                        $"Lv3：开局随机 {DestinyPassiveSystem.ClubLv3OpeningEnchantCount} 张手牌各获得一个 tier1 附魔"
                    };
                case Suit.Diamond:
                    return new[]
                    {
                        $"Lv1：商店降价 {DestinyPassiveSystem.DiamondLv1ShopDiscountPercent}%",
                        $"Lv2：开局每 {DestinyPassiveSystem.DiamondLv2GoldPerStrength} 金币获得 1 层力量",
                        $"Lv3：战斗结束后金币增长 {DestinyPassiveSystem.DiamondLv3GoldGainPercent}%"
                    };
                default:
                    return new[] { "", "", "" };
            }
        }

        /// <summary>主动技能名</summary>
        public static string GetActiveName(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spade: return "破军";
                case Suit.Heart: return "回春";
                case Suit.Club: return "顿悟";
                case Suit.Diamond: return "聚宝";
                default: return "?";
            }
        }

        /// <summary>主动技能描述（按等级；数值取自 DestinyPassiveSystem）</summary>
        public static string GetActiveDesc(Suit suit, int rank)
        {
            rank = rank < 1 ? 1 : (rank > 3 ? 3 : rank);
            switch (suit)
            {
                case Suit.Spade:
                    return $"对随机敌人造成 {DestinyPassiveSystem.GetActiveSpadeHits(rank)} 次 1 点伤害";
                case Suit.Heart:
                    return $"清除自身所有负面，获得 {DestinyPassiveSystem.ActiveHeartRegenTurns} 回合 {DestinyPassiveSystem.ActiveHeartRegenBase + rank} 层再生";
                case Suit.Club:
                    if (rank == 1) return $"抽 {DestinyPassiveSystem.ActiveClubDraw} 张牌";
                    if (rank == 2) return $"抽 {DestinyPassiveSystem.ActiveClubDraw} 张牌，其中 {DestinyPassiveSystem.ActiveClubEnchantLv2} 张各获得随机附魔";
                    return $"抽 {DestinyPassiveSystem.ActiveClubDraw} 张牌，其中 {DestinyPassiveSystem.ActiveClubEnchantLv3} 张各获得随机稀有附魔";
                case Suit.Diamond:
                    if (rank == 1) return $"立即获得 {DestinyPassiveSystem.ActiveDiamondGoldLv1} 金币";
                    if (rank == 2) return $"立即获得 {DestinyPassiveSystem.ActiveDiamondGoldLv2} 金币，并造成 金币/{DestinyPassiveSystem.ActiveDiamondDamagePerGold} 伤害";
                    return $"立即获得 {DestinyPassiveSystem.ActiveDiamondGoldLv3} 金币，并造成 金币/{DestinyPassiveSystem.ActiveDiamondDamagePerGold} 伤害";
                default:
                    return "";
            }
        }
    }
}
