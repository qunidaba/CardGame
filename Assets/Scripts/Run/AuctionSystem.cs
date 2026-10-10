using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>拍卖会的一件拍品</summary>
    [System.Serializable]
    public class AuctionItem
    {
        public bool isPotion;
        public int id;
        public string label;
        public int startPrice;
        public int minIncrement;
        public int currentPrice;   // 当前（玩家）出价；0 = 还没出价
        public int state;          // 0=待拍, 1=已拍得, 2=放弃
    }

    /// <summary>
    /// 拍卖会逻辑：生成拍品 + 被 NPC 加价的概率。
    /// 规则：遗物 20 起拍、每次最少 +5；药水 10 起拍、每次最少 +3；
    /// 被加价概率 80% 起，价格每 +1 次最低额 -10%，下限 0。
    /// </summary>
    public static class AuctionSystem
    {
        public const int ItemCount = 3;
        public const int RelicStartPrice = 20;
        public const int RelicMinIncrement = 5;
        public const int PotionStartPrice = 10;
        public const int PotionMinIncrement = 3;
        public const float BaseOutbidChance = 0.8f;
        public const float OutbidChancePerIncrement = 0.1f;

        /// <summary>随机生成 N 件拍品（遗物/药水各半概率，去重）</summary>
        public static List<AuctionItem> Generate(RunData run)
        {
            var items = new List<AuctionItem>();
            var usedRelic = new HashSet<int>();
            var usedPotion = new HashSet<int>();

            var relics = ConfigLoader.Config != null ? ConfigLoader.Config.relics : null;
            var potions = ConfigLoader.Config != null ? ConfigLoader.Config.potions : null;

            for (int i = 0; i < ItemCount; i++)
            {
                bool wantPotion = UnityEngine.Random.value < 0.5f;

                if (wantPotion && potions != null)
                {
                    var pool = potions.FindAll(p => p.rarity != "Event" && !usedPotion.Contains(p.id));
                    if (pool.Count > 0)
                    {
                        var p = pool[UnityEngine.Random.Range(0, pool.Count)];
                        usedPotion.Add(p.id);
                        items.Add(new AuctionItem
                        {
                            isPotion = true, id = p.id, label = p.name,
                            startPrice = PotionStartPrice, minIncrement = PotionMinIncrement, currentPrice = 0
                        });
                        continue;
                    }
                }

                if (relics != null)
                {
                    var pool = relics.FindAll(r =>
                        RarityUtil.Matches(r.rarity, "") &&
                        !r.eventOnly &&
                        (run == null || !run.HasRelic(r.id)) &&
                        !usedRelic.Contains(r.id));
                    if (pool.Count > 0)
                    {
                        var r = pool[UnityEngine.Random.Range(0, pool.Count)];
                        usedRelic.Add(r.id);
                        items.Add(new AuctionItem
                        {
                            isPotion = false, id = r.id, label = r.name,
                            startPrice = RelicStartPrice, minIncrement = RelicMinIncrement, currentPrice = 0
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>某次出价对应的「被加价」概率（价格越高越低，下限 0）</summary>
        public static float OutbidChance(AuctionItem item, int bid)
        {
            if (item == null || item.minIncrement <= 0) return 0f;
            int increments = Mathf.Max(0, (bid - item.startPrice) / item.minIncrement);
            return Mathf.Clamp01(BaseOutbidChance - OutbidChancePerIncrement * increments);
        }

        /// <summary>拍品的显示描述（遗物/药水）</summary>
        public static string Describe(AuctionItem item)
        {
            if (item == null) return "";
            if (item.isPotion)
            {
                var p = ConfigLoader.GetPotion(item.id);
                return p != null ? p.description : "";
            }
            var r = ConfigLoader.GetRelic(item.id);
            return r != null ? r.description : "";
        }

        public static string Rarity(AuctionItem item)
        {
            if (item == null) return "Common";
            if (item.isPotion)
            {
                var p = ConfigLoader.GetPotion(item.id);
                return p != null ? p.rarity : "Common";
            }
            var r = ConfigLoader.GetRelic(item.id);
            return r != null ? r.rarity : "Common";
        }
    }
}
