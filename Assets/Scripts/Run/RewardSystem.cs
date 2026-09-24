using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 奖励系统：战斗胜利后生成附魔三选一、宝箱奖励等
    /// </summary>
    public class RewardSystem
    {
        /// <summary>
        /// 随机遗物池：按稀有度筛选，排除事件专属与**已拥有**的遗物。
        /// 已拥有的不会出现在池子里；若该档已全部拥有，池子为空（本次不给遗物）。
        /// </summary>
        public static List<RelicData> GetRandomRelicPool(RunData runData, string rarityFilter)
        {
            return ConfigLoader.Config.relics.FindAll(r =>
                RarityUtil.Matches(r.rarity, rarityFilter) &&
                !r.eventOnly &&
                (runData == null || !runData.HasRelic(r.id)));
        }
        // ===== 测试用掉率倍率（Inspector 可调，或代码修改）=====
        public static float RelicDropRateMultiplier = 1f;   // 遗物掉率倍率
        public static float PotionDropRateMultiplier = 1f;  // 药水掉率倍率
        public static bool ForceRelicDrop = true;          // 强制掉落遗物（测试用）
        public static bool ForcePotionDrop = false;         // 强制掉落药水（测试用）

        public CombatReward GenerateCombatReward(RunData runData, bool isElite, bool isBoss, List<EnemyData> enemies = null)
        {
            var reward = new CombatReward();

            // 金币：每个敌人各滚一次自己的 goldRange 再求和（没有敌人数据时退回按类型给固定范围）
            if (enemies != null && enemies.Count > 0)
            {
                int gold = 0;
                foreach (var e in enemies)
                {
                    if (e == null || e.goldRange == null || e.goldRange.Count < 2) continue;
                    int lo = e.goldRange[0];
                    int hi = Mathf.Max(lo, e.goldRange[1]);
                    gold += UnityEngine.Random.Range(lo, hi + 1);
                }
                reward.gold = gold;
            }
            else if (isBoss)
            {
                reward.gold = UnityEngine.Random.Range(50, 101);
            }
            else if (isElite)
            {
                reward.gold = UnityEngine.Random.Range(25, 51);
            }
            else
            {
                reward.gold = UnityEngine.Random.Range(10, 31);
            }

            // 精英/Boss 额外奖励：遗物掉落
            if (isElite || isBoss)
            {
                float relicChance = 1f;
                relicChance *= RelicDropRateMultiplier;

                if (ForceRelicDrop || UnityEngine.Random.value < relicChance)
                {
                    var rarity = isBoss ? "Epic" : (isElite ? "Rare" : "Common");
                    var relics = GetRandomRelicPool(runData, rarity);
                    if (relics.Count > 0)
                        reward.relicId = relics[UnityEngine.Random.Range(0, relics.Count)].id;
                }
            }

            // 药水掉落：精英/Boss 必掉，普通战斗 30%
            float potionChance = (isElite || isBoss) ? 1f : 0.3f;
            potionChance *= PotionDropRateMultiplier;
            if (ForcePotionDrop || potionChance >= 1f || UnityEngine.Random.value < potionChance)
            {
                var potions = ConfigLoader.Config.potions;
                if (potions != null && potions.Count > 0)
                    reward.potionId = potions[UnityEngine.Random.Range(0, potions.Count)].id;
            }

            // 附魔三选一（梅花 Lv2 → 四选一）——选项数集中在 DestinyEffects
            int optCount = DestinyEffects.GetEnchantOptionCount(runData);
            reward.enchantmentOptions = GenerateEnchantmentOptions(runData, optCount);

            return reward;
        }

        public List<EnchantmentRewardOption> GenerateEnchantmentOptions(RunData runData, int count, int minTier = 1)
        {
            var options = new List<EnchantmentRewardOption>();
            var availableCards = GetAvailableCardKeys(runData);

            // 随机选择 count 张不同的牌
            var shuffled = new List<string>(availableCards);
            for (int i = 0; i < shuffled.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, shuffled.Count);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            int optionsNeeded = Mathf.Min(count, shuffled.Count);
            for (int i = 0; i < optionsNeeded; i++)
            {
                string cardKey = shuffled[i];
                var enchantment = PickRandomEnchantment(runData, minTier);
                if (enchantment != null)
                {
                    options.Add(new EnchantmentRewardOption
                    {
                        cardKey = cardKey,
                        cardDisplayName = GetCardDisplayName(cardKey),
                        enchantmentId = enchantment.id
                    });
                }
            }

            // 如果不够，补齐
            while (options.Count < count)
            {
                var enchantment = PickRandomEnchantment(runData, minTier);
                if (enchantment == null) break;
                string cardKey = availableCards.Count > 0 ? availableCards[UnityEngine.Random.Range(0, availableCards.Count)] : "Spade_14";
                options.Add(new EnchantmentRewardOption
                {
                    cardKey = cardKey,
                    cardDisplayName = GetCardDisplayName(cardKey),
                    enchantmentId = enchantment.id
                });
            }

            return options;
        }

        public void ApplyEnchantmentReward(RunData runData, EnchantmentRewardOption option)
        {
            if (option == null) return;

            // 解析 cardKey (如 "Spade_14")
            var parts = option.cardKey.Split('_');
            if (parts.Length == 2 && 
                System.Enum.TryParse(parts[0], out Suit suit) && 
                int.TryParse(parts[1], out int rank))
            {
                runData.AddEnchantment(rank, suit, option.enchantmentId);
                Debug.Log($"[RewardSystem] 给 {option.cardDisplayName} 添加附魔: {ConfigLoader.GetEnchantment(option.enchantmentId)?.name}");
            }
        }

        private List<string> GetAvailableCardKeys(RunData runData)
        {
            var suits = new[] { "Spade", "Heart", "Club", "Diamond" };
            var keys = new List<string>();
            foreach (var suit in suits)
            {
                for (int rank = 2; rank <= 14; rank++)
                {
                    keys.Add($"{suit}_{rank}");
                }
            }
            return keys;
        }

        private string GetCardDisplayName(string cardKey)
        {
            var parts = cardKey.Split('_');
            if (parts.Length != 2) return cardKey;
            string suit = parts[0];
            int rank = int.Parse(parts[1]);

            string rankStr = rank switch
            {
                11 => "J",
                12 => "Q",
                13 => "K",
                14 => "A",
                _ => rank.ToString()
            };

            string suitSymbol = suit switch
            {
                "Spade" => "♠",
                "Heart" => "♥",
                "Club" => "♣",
                "Diamond" => "♦",
                _ => ""
            };

            return $"{rankStr}{suitSymbol}";
        }

        private EnchantmentData PickRandomEnchantment(RunData runData, int minTier = 1)
        {
            // 按权重随机，考虑 tier 分布（低 tier 概率高）
            var weights = new Dictionary<int, int> { { 1, 65 }, { 2, 30 }, { 3, 5 } };

            var candidates = new List<EnchantmentData>();
            foreach (var kvp in weights)
            {
                if (kvp.Key < minTier) continue;
                candidates.AddRange(ConfigLoader.Config.enchantments.FindAll(e => e.weight > 0 && RarityUtil.Tier(e.rarity) == kvp.Key));
            }

            // 保底：如果 minTier 过滤后为空，则放宽到所有 >= minTier 的附魔
            if (candidates.Count == 0)
                candidates = ConfigLoader.Config.enchantments.FindAll(e => e.weight > 0 && RarityUtil.Tier(e.rarity) >= minTier);
            if (candidates.Count == 0) return null;

            // 按 weight 随机
            int totalWeight = 0;
            foreach (var e in candidates) totalWeight += e.weight;
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            foreach (var e in candidates)
            {
                cumulative += e.weight;
                if (roll < cumulative) return e;
            }
            return candidates[candidates.Count - 1];
        }
    }
}
