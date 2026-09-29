using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>商店条目</summary>
    public class ShopEntry
    {
        public string kind;   // relic / potion
        public int id;
        public int price;
        public bool sold;
        public string label;
    }

    /// <summary>商店附魔条目：一个已知附魔，可选随机牌或指定牌</summary>
    public class ShopEnchantEntry
    {
        public int enchantmentId;
        public int priceRandom;
        public int priceCustom;
        public bool sold;
    }

    /// <summary>商店库存</summary>
    public class ShopInventory
    {
        public List<ShopEntry> relics = new List<ShopEntry>();
        public List<ShopEntry> potions = new List<ShopEntry>();
        public List<ShopEnchantEntry> enchantments = new List<ShopEnchantEntry>();
    }

    /// <summary>
    /// 商店系统：生成库存、计价、购买
    /// </summary>
    public class ShopSystem
    {
        public ShopInventory Generate(RunData runData)
        {
            var inv = new ShopInventory();

            // ===== 遗物：随机 2~3 个（跨稀有度，排除已拥有）=====
            var relicPool = new List<RelicData>();
            relicPool.AddRange(ConfigLoader.GetRelicsByRarity("Common"));
            relicPool.AddRange(ConfigLoader.GetRelicsByRarity("Rare"));
            relicPool.AddRange(ConfigLoader.GetRelicsByRarity("Epic"));
            relicPool.RemoveAll(r => r.eventOnly);   // 事件专属遗物不进商店
            if (runData != null)
                relicPool.RemoveAll(r => runData.RelicIds.Contains(r.id));
            Shuffle(relicPool);

            int relicCount = Mathf.Min(3, relicPool.Count);
            for (int i = 0; i < relicCount; i++)
            {
                var r = relicPool[i];
                inv.relics.Add(new ShopEntry
                {
                    kind = "relic",
                    id = r.id,
                    price = GetRelicPrice(r.rarity),
                    label = r.name
                });
            }

            // ===== 药水：随机 2~3 个（同种药水可重复，所以不排除已拥有）=====
            // 事件专属药水（rarity = Event）不进商店
            var potionPool = new List<PotionData>(ConfigLoader.Config.potions);
            potionPool.RemoveAll(p => p.rarity == "Event");
            Shuffle(potionPool);
            int potionCount = Mathf.Min(3, potionPool.Count);
            for (int i = 0; i < potionCount; i++)
            {
                var p = potionPool[i];
                inv.potions.Add(new ShopEntry
                {
                    kind = "potion",
                    id = p.id,
                    price = GetPotionPrice(p.rarity),
                    label = p.name
                });
            }

            // ===== 附魔区：随机 3 个可正常获得的附魔（weight>0，排除诅咒与事件专属），每个可选随机牌/指定牌（指定更贵）=====
            var enchPool = ConfigLoader.Config.enchantments.FindAll(e => e.weight > 0);
            Shuffle(enchPool);
            int enchCount = Mathf.Min(3, enchPool.Count);
            for (int i = 0; i < enchCount; i++)
            {
                var e = enchPool[i];
                int basePrice = GetEnchantBasePrice(RarityUtil.Tier(e.rarity));
                inv.enchantments.Add(new ShopEnchantEntry
                {
                    enchantmentId = e.id,
                    priceRandom = basePrice,
                    priceCustom = Mathf.RoundToInt(basePrice * 2f)
                });
            }

            return inv;
        }

        /// <summary>购买遗物（replacedId &gt; 0 时用新遗物替换掉该旧遗物）</summary>
        public bool BuyRelic(ShopEntry e, RunData runData, RelicSystem relicSystem, int replacedId = 0)
        {
            if (e == null || e.sold) return false;
            if (runData.Gold < e.price) return false;

            if (replacedId > 0)
            {
                if (!runData.TryReplaceRelic(replacedId, e.id))
                    return false;
            }
            else if (!runData.TryAddRelic(e.id))
            {
                return false; // 槽满/已拥有
            }

            runData.Gold -= e.price;
            e.sold = true;
            return true;
        }

        /// <summary>购买药水（replacedId &gt; 0 时用新药水替换掉该旧药水）</summary>
        public bool BuyPotion(ShopEntry e, RunData runData, int replacedId = 0)
        {
            if (e == null || e.sold) return false;
            if (runData.Gold < e.price) return false;

            if (replacedId > 0)
            {
                if (!runData.TryReplacePotion(replacedId, e.id))
                    return false;
            }
            else if (!runData.TryAddPotion(e.id))
            {
                return false; // 槽满/已拥有
            }

            runData.Gold -= e.price;
            e.sold = true;
            return true;
        }

        /// <summary>购买附魔：custom=true 附到指定牌（rank,suit），否则附到随机牌</summary>
        public bool BuyEnchant(ShopEnchantEntry entry, RunData runData, bool custom, int rank, Suit suit,
                               out int resultRank, out Suit resultSuit)
        {
            resultRank = rank;
            resultSuit = suit;
            if (entry == null || entry.sold) return false;

            int price = custom ? entry.priceCustom : entry.priceRandom;
            if (runData.Gold < price) return false;

            if (custom)
            {
                runData.AddEnchantment(rank, suit, entry.enchantmentId);
            }
            else
            {
                resultRank = Random.Range(2, 15);
                resultSuit = (Suit)Random.Range(0, 4);
                runData.AddEnchantment(resultRank, resultSuit, entry.enchantmentId);
            }

            runData.Gold -= price;
            entry.sold = true;
            return true;
        }

        // ===== 价格（原先读 shop.json，现已内置）=====

        /// <summary>遗物价格（按稀有度）</summary>
        private static int GetRelicPrice(string rarity)
        {
            switch (rarity)
            {
                case "Common": return 40;
                case "Rare": return 80;
                case "Epic": return 120;
                default: return 120;
            }
        }

        /// <summary>药水价格（按稀有度：普通 10 / 稀有 20 / 史诗 30）</summary>
        private static int GetPotionPrice(string rarity)
        {
            switch (rarity)
            {
                case "Epic": return 30;
                case "Rare": return 20;
                default: return 10;
            }
        }

        /// <summary>附魔基础价（按稀有度等级 1/2/3）</summary>
        private static int GetEnchantBasePrice(int tier)
        {
            switch (tier)
            {
                case 1: return 20;
                case 2: return 40;
                case 3: return 60;
                default: return 60;
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = Random.Range(i, list.Count);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }
    }
}
