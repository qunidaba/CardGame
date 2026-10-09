using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 事件执行结果：用于事件结束后向玩家展示获得了什么
    /// </summary>
    public class EventOutcome
    {
        public List<GrantedEnchantment> enchantments = new List<GrantedEnchantment>();
        public List<string> messages = new List<string>();

        /// <summary>多阶段事件：本阶段失败，展示结果后回到事件面板进入下一阶段</summary>
        public bool reopenEvent;

        /// <summary>遗物槽已满、等待玩家选择「替换 / 丢弃」的遗物 id</summary>
        public List<int> pendingRelicIds = new List<int>();

        /// <summary>药水槽已满、等待玩家选择「替换 / 丢弃」的药水 id</summary>
        public List<int> pendingPotionIds = new List<int>();

        public bool IsEmpty => enchantments.Count == 0 && messages.Count == 0
                            && pendingRelicIds.Count == 0 && pendingPotionIds.Count == 0;
    }

    /// <summary>
    /// 一次附魔发放：目标牌 + 附魔
    /// </summary>
    public class GrantedEnchantment
    {
        public string cardKey;          // 如 "Spade_14"
        public string cardDisplayName;  // 如 "A♠"
        public int enchantmentId;
    }

    /// <summary>事件选项需要玩家先做的选择（选牌 / 选花色）</summary>
    public class EventChoiceContext
    {
        public bool hasCard;
        public int cardRank;
        public Suit cardSuit;

        /// <summary>多选时，除第一张外的其余牌</summary>
        public List<int> extraCardRanks = new List<int>();
        public List<Suit> extraCardSuits = new List<Suit>();

        public bool hasSuit;
        public Suit suit;

        /// <summary>需要选遗物时</summary>
        public bool hasRelic;
        public int relicId;
    }

    /// <summary>事件预抽物品的存档记录（按选项/结果下标定位）</summary>
    public class PreparedItemSave
    {
        public int optionIndex;
        public int resultIndex;
        public bool isPotion;
        public int id;
    }

    /// <summary>牌引用（点数 + 花色）</summary>
    public struct CardRef
    {
        public int rank;
        public Suit suit;
    }

    /// <summary>
    /// 事件执行器：把事件选项的结果应用到 RunData。
    /// 纯逻辑层，不依赖 UI；返回 EventOutcome 供 UI 展示。
    /// </summary>
    public class EventSystem
    {
        private readonly RewardSystem rewardSystem = new RewardSystem();

        private const int EnchantDestinyId = 65;   // 「天命」
        private const int EnchantCurseId = 67;     // 「咒印」

        /// <summary>该选项是否需要先选一张牌</summary>
        public static bool NeedsCardPick(EventOptionData option)
        {
            if (option?.results == null) return false;
            foreach (var r in option.results)
            {
                if (r == null) continue;
                if (r.type == "EnchantCard" || r.type == "ClearCardEnchant" ||
                    r.type == "GiveDestinyCard" || r.type == "ReplaceEnchant" ||
                    r.type == "EnchantCardWithSpecific" || r.type == "SacrificeEnchantsByRarity" ||
                    r.type == "CopyEnchantsBetweenCards")
                    return true;
                if (!string.IsNullOrEmpty(r.require)) return true;
            }
            return false;
        }

        /// <summary>
        /// 获得遗物：已拥有则直接忽略；槽位未满直接加入；
        /// 槽位已满则挂到 outcome，由 RunDirector 弹「替换 / 丢弃」面板让玩家选择。
        /// </summary>
        private static void GrantRelic(RunData runData, EventOutcome outcome, RelicData relic)
        {
            if (relic == null) return;

            // 已拥有：不折算金币、不提示，直接忽略
            if (runData.HasRelic(relic.id)) return;

            if (runData.RelicIds.Count >= RelicSystem.MaxSlots)
            {
                outcome.pendingRelicIds.Add(relic.id);
                outcome.messages.Add($"获得遗物：{relic.name}（遗物槽已满，需要替换或丢弃）");
                return;
            }

            if (runData.TryAddRelic(relic.id))
                outcome.messages.Add($"获得遗物：{relic.name}");
        }

        /// <summary>
        /// 获得药水（同种药水允许重复）：槽位已满则挂到 outcome，
        /// 由 RunDirector 弹「替换 / 丢弃」面板让玩家选择。
        /// </summary>
        private static void GrantPotion(RunData runData, EventOutcome outcome, PotionData potion)
        {
            if (potion == null) return;

            if (runData.PotionIds.Count >= runData.PotionSlotCap)
            {
                outcome.pendingPotionIds.Add(potion.id);
                outcome.messages.Add($"获得药水：{potion.name}（药水槽已满，需要替换或丢弃）");
                return;
            }

            if (runData.TryAddPotion(potion.id))
                outcome.messages.Add($"获得药水：{potion.name}");
            else
                runData.Gold += 30;
        }

        /// <summary>解析 require 里的附魔 id（"Enchant:32" → 32；否则 0）</summary>
        private static int ParseRequireEnchant(string require)
        {
            const string prefix = "Enchant:";
            if (string.IsNullOrEmpty(require) || !require.StartsWith(prefix)) return 0;
            return GetInt(require.Substring(prefix.Length));
        }

        /// <summary>选项的选牌限制（require 字符串；空 = 无限制）
        /// 支持：Enchanted（任意附魔）/ Enchant:N（指定附魔）/ Rarity:Common|Rare|Epic</summary>
        public static string GetCardRequire(EventOptionData option)
        {
            if (option?.results == null) return null;
            foreach (var r in option.results)
                if (r != null && !string.IsNullOrEmpty(r.require)) return r.require;
            return null;
        }

        /// <summary>某张牌是否满足选牌限制</summary>
        public static bool MatchesCardRequire(string require, RunData runData, int rank, Suit suit)
        {
            if (string.IsNullOrEmpty(require)) return true;
            if (runData == null) return false;

            if (require == "Enchanted")
                return runData.HasCardEnchantment(rank, suit);

            if (require.StartsWith("Enchant:"))
            {
                int id = ParseRequireEnchant(require);
                return id > 0 && runData.GetCardEnchantments(rank, suit).Contains(id);
            }

            if (require.StartsWith("Rarity:"))
            {
                string want = require.Substring("Rarity:".Length);
                foreach (var id in runData.GetCardEnchantments(rank, suit))
                {
                    var e = ConfigLoader.GetEnchantment(id);
                    if (e != null && e.rarity == want) return true;
                }
                return false;
            }

            return true;
        }

        /// <summary>牌组里是否有满足该限制的牌</summary>
        public static bool HasAnyCardMatching(RunData runData, string require)
        {
            if (string.IsNullOrEmpty(require)) return true;
            if (runData == null) return false;

            foreach (Suit s in new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond })
                for (int r = 2; r <= 14; r++)
                    if (MatchesCardRequire(require, runData, r, s)) return true;
            return false;
        }

        /// <summary>该选项是否需要先选择一个遗物（丢弃）</summary>
        public static bool NeedsRelicPick(EventOptionData option)
        {
            if (option?.results == null) return false;
            foreach (var r in option.results)
                if (r != null && r.type == "LoseChosenRelic") return true;
            return false;
        }

        /// <summary>选项的显示条件是否满足（如 HasRelic:30 需要拥有该遗物）</summary>
        public static bool MeetsOptionCondition(EventOptionData option, RunData runData)
        {
            if (option == null || string.IsNullOrEmpty(option.condition)) return true;

            var c = option.condition;
            if (c.StartsWith("HasRelic:"))
            {
                if (int.TryParse(c.Substring("HasRelic:".Length), out var id))
                    return runData != null && runData.RelicIds.Contains(id);
            }
            // 多阶段事件：仅当前阶段显示（"Stage:2"）
            if (c.StartsWith("Stage:"))
            {
                if (int.TryParse(c.Substring("Stage:".Length), out var stage))
                    return runData != null && runData.currentEventStage == stage;
            }
            return true;
        }

        /// <summary>该选项在条件不满足时是否应直接隐藏（目前仅多阶段事件的 Stage:N 如此）</summary>
        public static bool IsOptionHidden(EventOptionData option, RunData runData)
        {
            if (option == null || string.IsNullOrEmpty(option.condition)) return false;
            if (!option.condition.StartsWith("Stage:")) return false;
            return !MeetsOptionCondition(option, runData);
        }

        /// <summary>
        /// 该选项需要几张「已有附魔的牌」才能选（0 = 不要求）。
        /// ClearCardEnchant(count=N) 需要 N 张；RemoveAllEnchantmentsRandomCard 需要 1 张。
        /// </summary>
        public static int GetRequiredEnchantedCardCount(EventOptionData option)
        {
            if (option?.results == null) return 0;

            int need = 0;
            foreach (var r in option.results)
            {
                if (r == null) continue;

                if (r.type == "ClearCardEnchant")
                    need = Mathf.Max(need, Mathf.Max(1, r.count));
                else if (r.type == "RemoveAllEnchantmentsRandomCard")
                    need = Mathf.Max(need, 1);
            }
            return need;
        }

        /// <summary>该选项需要选几张牌（ClearCardEnchant 的 count 最大值 / 复制附魔固定 2 张）</summary>
        public static int GetCardPickCount(EventOptionData option)
        {
            if (option?.results == null) return 1;
            int max = 1;
            foreach (var r in option.results)
            {
                if (r == null) continue;
                if (r.type == "CopyEnchantsBetweenCards") return 2;
                if (r.type == "ClearCardEnchant" && r.count > max) max = r.count;
            }
            return max;
        }

        /// <summary>该选项是否要求先选一个花色</summary>
        public static bool NeedsSuitPick(EventOptionData option)
        {
            if (option?.results == null) return false;
            foreach (var r in option.results)
                if (r != null && (r.type == "EnchantSuitRandom" || r.type == "GuaranteeSuitHand"))
                    return true;
            return false;
        }

        /// <summary>该选项是否涉及「清除附魔」（有的话：没附魔的牌不可选；一张都没有时选项禁用）</summary>
        public static bool NeedsEnchantedCard(EventOptionData option)
        {
            if (option?.results == null) return false;
            foreach (var r in option.results)
                if (r != null && (r.type == "ClearCardEnchant" || r.type == "RemoveAllEnchantmentsRandomCard"))
                    return true;
            return false;
        }

        /// <summary>该选项需要支付的金币（LoseGold 之和；Gamble 按「输」分支里的 LoseGold 计）</summary>
        public static int GetGoldCost(EventOptionData option)
        {
            if (option?.results == null) return 0;
            int cost = 0;
            foreach (var r in option.results)
                cost += GetGoldCostOfResult(r);
            return cost;
        }

        private static int GetGoldCostOfResult(EventResultData r)
        {
            if (r == null) return 0;
            if (r.type == "LoseGold") return GetInt(r.value);
            if (r.type == "StageGamble") return GetInt(r.value);   // 多阶段赌博：投入的金币
            if (r.type == "Gamble")
            {
                int c = 0;
                if (r.lose != null)
                    foreach (var x in r.lose) c += GetGoldCostOfResult(x);
                return c;
            }
            return 0;
        }

        public EventOutcome ExecuteOption(EventOptionData option, RunData runData, RelicSystem relicSystem, EventChoiceContext ctx = null)
        {
            var outcome = new EventOutcome();
            if (option == null || option.results == null) return outcome;

            for (int i = 0; i < option.results.Count; i++)
                ExecuteResult(option.results[i], runData, relicSystem, outcome, ctx, option, i);

            return outcome;
        }

        /// <summary>执行一组结果（无选项的场景：挑战结算等）</summary>
        public EventOutcome ExecuteResults(List<EventResultData> results, RunData runData, RelicSystem relicSystem)
        {
            var outcome = new EventOutcome();
            if (results == null) return outcome;

            for (int i = 0; i < results.Count; i++)
                ExecuteResult(results[i], runData, relicSystem, outcome, null, null, i);

            return outcome;
        }

        // ===== 选项预抽（拍卖会提前展示拍品）=====
        private struct PreparedItem
        {
            public bool isPotion;
            public int id;
        }

        private readonly Dictionary<EventOptionData, Dictionary<int, PreparedItem>> preparedItems =
            new Dictionary<EventOptionData, Dictionary<int, PreparedItem>>();

        /// <summary>导出预抽结果（存档用；按选项/结果写下标，读档可原样还原，不再重抽）</summary>
        public List<PreparedItemSave> ExportPrepared(EventData ev)
        {
            var list = new List<PreparedItemSave>();
            if (ev?.options == null) return list;

            for (int i = 0; i < ev.options.Count; i++)
            {
                var opt = ev.options[i];
                if (opt == null || !preparedItems.TryGetValue(opt, out var map)) continue;
                foreach (var kv in map)
                    list.Add(new PreparedItemSave
                    {
                        optionIndex = i,
                        resultIndex = kv.Key,
                        isPotion = kv.Value.isPotion,
                        id = kv.Value.id
                    });
            }
            return list;
        }

        /// <summary>还原预抽结果（读档用）</summary>
        public void ImportPrepared(EventData ev, List<PreparedItemSave> list)
        {
            preparedItems.Clear();
            if (ev?.options == null || list == null) return;

            foreach (var s in list)
            {
                if (s.optionIndex < 0 || s.optionIndex >= ev.options.Count) continue;
                var opt = ev.options[s.optionIndex];
                if (opt == null) continue;

                if (!preparedItems.TryGetValue(opt, out var map))
                {
                    map = new Dictionary<int, PreparedItem>();
                    preparedItems[opt] = map;
                }
                map[s.resultIndex] = new PreparedItem { isPotion = s.isPotion, id = s.id };
            }
        }

        /// <summary>为事件的所有选项预抽「可获得的具体物品」（GainRelic / GainPotion），供展示与执行共用</summary>
        public void PrepareEvent(EventData ev, RunData runData)
        {
            preparedItems.Clear();
            if (ev?.options == null) return;

            foreach (var opt in ev.options)
            {
                if (opt?.results == null) continue;
                Dictionary<int, PreparedItem> map = null;

                for (int i = 0; i < opt.results.Count; i++)
                {
                    var r = opt.results[i];
                    if (r == null || !r.preview) continue;   // 只有标记 preview 的才提前展示

                    if (r.type == "GainRelic")
                    {
                        var pool = RewardSystem.GetRandomRelicPool(runData, r.rarity);
                        if (pool.Count > 0)
                        {
                            if (map == null) map = new Dictionary<int, PreparedItem>();
                            int id = pool[UnityEngine.Random.Range(0, pool.Count)].id;
                            CodexData.DiscoverRelic(id);   // 图鉴：事件里展示过即算发现
                            map[i] = new PreparedItem { isPotion = false, id = id };
                        }
                    }
                    else if (r.type == "GainPotion")
                    {
                        var potions = ConfigLoader.Config.potions;
                        if (potions != null && potions.Count > 0)
                        {
                            if (map == null) map = new Dictionary<int, PreparedItem>();
                            int id = potions[UnityEngine.Random.Range(0, potions.Count)].id;
                            CodexData.DiscoverPotion(id);  // 图鉴
                            map[i] = new PreparedItem { isPotion = true, id = id };
                        }
                    }
                }

                if (map != null) preparedItems[opt] = map;
            }
        }

        /// <summary>预抽到的遗物 id（0 = 没有）</summary>
        private int GetPreparedRelicId(EventOptionData option, int resultIndex)
        {
            if (option != null && preparedItems.TryGetValue(option, out var map) &&
                map.TryGetValue(resultIndex, out var item) && !item.isPotion)
                return item.id;
            return 0;
        }

        /// <summary>预抽到的药水 id（0 = 没有）</summary>
        private int GetPreparedPotionId(EventOptionData option, int resultIndex)
        {
            if (option != null && preparedItems.TryGetValue(option, out var map) &&
                map.TryGetValue(resultIndex, out var item) && item.isPotion)
                return item.id;
            return 0;
        }

        /// <summary>选项展示文本（附上预抽到的具体拍品及其效果）</summary>
        public string GetOptionDisplayText(EventOptionData option)
        {
            if (option == null) return "";
            if (!preparedItems.TryGetValue(option, out var map) || map.Count == 0) return option.text;

            var parts = new List<string>();
            foreach (var kv in map)
            {
                if (kv.Value.isPotion)
                {
                    var p = ConfigLoader.GetPotion(kv.Value.id);
                    if (p != null) parts.Add($"{p.name}：{p.description}");
                }
                else
                {
                    var r = ConfigLoader.GetRelic(kv.Value.id);
                    if (r != null) parts.Add($"{r.name}：{r.description}");
                }
            }
            if (parts.Count == 0) return option.text;
            return $"{option.text}\n（{string.Join("；", parts)}）";
        }

        private void ExecuteResult(EventResultData result, RunData runData, RelicSystem relicSystem, EventOutcome outcome, EventChoiceContext ctx, EventOptionData option = null, int resultIndex = -1)
        {
            if (result == null || runData == null) return;

            switch (result.type)
            {
                case "GainGold":
                {
                    int v = GetInt(result.value);
                    if (relicSystem != null) relicSystem.GainGold(v);
                    else runData.Gold += v;
                    runData.totalGoldGained += v;
                    outcome.messages.Add($"获得 {v} 金币");
                    break;
                }
                case "LoseGold":
                {
                    int v = GetInt(result.value);
                    runData.Gold -= v;
                    outcome.messages.Add($"失去 {v} 金币");
                    break;
                }
                case "Heal":
                {
                    int v = GetInt(result.value);
                    runData.CurrentHp = Mathf.Min(runData.MaxHp, runData.CurrentHp + v);
                    outcome.messages.Add($"回复 {v} 生命");
                    break;
                }
                case "LoseMaxHp":
                {
                    int v = GetInt(result.value);
                    runData.MaxHp = Mathf.Max(1, runData.MaxHp - v);
                    runData.CurrentHp = Mathf.Min(runData.CurrentHp, runData.MaxHp);
                    outcome.messages.Add($"失去 {v} 最大生命");
                    break;
                }
                case "GainMaxHp":
                {
                    int v = GetInt(result.value);
                    runData.MaxHp += v;   // 只加最大生命，不回血
                    outcome.messages.Add($"获得 {v} 最大生命");
                    break;
                }
                case "EnchantRandomCard":
                {
                    int count = result.count > 0 ? result.count : 1;
                    var options = rewardSystem.GenerateEnchantmentOptions(runData, count, result.rarity);
                    foreach (var opt in options)
                    {
                        rewardSystem.ApplyEnchantmentReward(runData, opt);
                        outcome.enchantments.Add(new GrantedEnchantment
                        {
                            cardKey = opt.cardKey,
                            cardDisplayName = opt.cardDisplayName,
                            enchantmentId = opt.enchantmentId
                        });
                    }
                    break;
                }
                case "EnchantCard":
                {
                    if (ctx == null || !ctx.hasCard) break;
                    int count = result.count > 0 ? result.count : 1;
                    for (int i = 0; i < count; i++)
                    {
                        int enchId = PickEnchantmentByRarity(result.rarity);
                        if (enchId <= 0) break;
                        runData.AddEnchantment(ctx.cardRank, ctx.cardSuit, enchId);
                        outcome.enchantments.Add(MakeGranted(ctx.cardRank, ctx.cardSuit, enchId));
                    }
                    break;
                }
                case "ClearCardEnchant":
                {
                    if (ctx == null || !ctx.hasCard) break;

                    ClearOneCard(ctx.cardRank, ctx.cardSuit, runData, outcome);
                    int extra = Mathf.Min(ctx.extraCardRanks.Count, ctx.extraCardSuits.Count);
                    for (int i = 0; i < extra; i++)
                        ClearOneCard(ctx.extraCardRanks[i], ctx.extraCardSuits[i], runData, outcome);
                    break;
                }
                case "GiveDestinyCard":
                {
                    if (ctx == null || !ctx.hasCard) break;
                    runData.AddEnchantment(ctx.cardRank, ctx.cardSuit, EnchantDestinyId);
                    outcome.enchantments.Add(MakeGranted(ctx.cardRank, ctx.cardSuit, EnchantDestinyId));
                    break;
                }
                case "EnchantSuitRandom":
                {
                    if (ctx == null || !ctx.hasSuit) break;
                    int count = result.count > 0 ? result.count : 1;
                    var ranks = new List<int>();
                    for (int r = 2; r <= 14; r++) ranks.Add(r);
                    for (int i = ranks.Count - 1; i > 0; i--)
                    {
                        int j = UnityEngine.Random.Range(0, i + 1);
                        int tmp = ranks[i]; ranks[i] = ranks[j]; ranks[j] = tmp;
                    }
                    int n = Mathf.Min(count, ranks.Count);
                    for (int i = 0; i < n; i++)
                    {
                        // 每张牌各自独立随机一个附魔
                        int enchId = PickEnchantmentByRarity(result.rarity);
                        if (enchId <= 0) break;
                        runData.AddEnchantment(ranks[i], ctx.suit, enchId);
                        outcome.enchantments.Add(MakeGranted(ranks[i], ctx.suit, enchId));
                    }
                    break;
                }
                case "RemoveAllEnchantmentsRandomCard":
                {
                    var keys = runData.GetEnchantedCardKeys();
                    if (keys.Count > 0)
                    {
                        string key = keys[UnityEngine.Random.Range(0, keys.Count)];
                        var parts = key.Split('_');
                        if (parts.Length == 2 &&
                            System.Enum.TryParse(parts[0], out Suit suit) &&
                            int.TryParse(parts[1], out int rank))
                        {
                            // 先记录失去的附魔名
                            var lostNames = new List<string>();
                            foreach (var id in runData.GetCardEnchantments(rank, suit))
                            {
                                var e = ConfigLoader.GetEnchantment(id);
                                if (e != null) lostNames.Add(e.name);
                            }
                            runData.ClearCardEnchantments(rank, suit);

                            if (lostNames.Count > 0)
                                outcome.messages.Add($"{CardName(rank, suit)} 失去了附魔：{string.Join("、", lostNames)}");
                            else
                                outcome.messages.Add($"{CardName(rank, suit)} 失去了所有附魔");
                        }
                    }
                    break;
                }
                case "GainRelic":
                {
                    RelicData relic = null;
                    int preparedId = GetPreparedRelicId(option, resultIndex);
                    if (preparedId > 0) relic = ConfigLoader.GetRelic(preparedId);

                    if (relic == null)
                    {
                        List<RelicData> pool = RewardSystem.GetRandomRelicPool(runData, result.rarity);
                        if (pool != null && pool.Count > 0)
                            relic = pool[UnityEngine.Random.Range(0, pool.Count)];
                    }

                    if (relic != null)
                    {
                        GrantRelic(runData, outcome, relic);
                    }
                    break;
                }
                case "LoseRelic":
                    if (runData.RelicIds.Count > 0)
                    {
                        int idx = UnityEngine.Random.Range(0, runData.RelicIds.Count);
                        int removedId = runData.RelicIds[idx];
                        var removed = ConfigLoader.GetRelic(removedId);
                        runData.TryRemoveRelic(removedId);
                        outcome.messages.Add($"失去遗物：{removed?.name}");
                    }
                    break;
                case "GainPotion":
                {
                    PotionData p = null;
                    int preparedId = GetPreparedPotionId(option, resultIndex);
                    if (preparedId > 0) p = ConfigLoader.GetPotion(preparedId);

                    if (p == null)
                    {
                        var potions = ConfigLoader.Config.potions;
                        if (potions != null && potions.Count > 0)
                            p = potions[UnityEngine.Random.Range(0, potions.Count)];
                    }

                    if (p != null)
                    {
                        GrantPotion(runData, outcome, p);
                    }
                    break;
                }
                case "NextBattleStartHp":
                {
                    int v = GetInt(result.value);
                    runData.nextBattleStartHpMod += v;
                    outcome.messages.Add($"下场战斗开局 {(v >= 0 ? "+" : "")}{v} 生命");
                    break;
                }
                case "NextBattleEnemyBuff":
                {
                    int v = GetInt(result.value);
                    runData.nextBattleEnemyBuff += v;
                    outcome.messages.Add($"下场战斗敌人强化 {v} 级");
                    break;
                }
                case "PermanentStartStrength":
                {
                    int v = GetInt(result.value);
                    runData.permanentStartStrength += v;
                    outcome.messages.Add($"永久：每场战斗开局 +{v} 力量");
                    break;
                }
                case "PermanentSelfDamage":
                {
                    int v = GetInt(result.value);
                    runData.permanentTurnEndSelfDamage += v;
                    outcome.messages.Add($"永久：每回合结束自伤 {v}");
                    break;
                }
                case "NextBattleMulligan":
                {
                    int v = GetInt(result.value);
                    runData.nextBattleMulliganDelta += v;
                    outcome.messages.Add($"下场战斗重抽次数 {(v >= 0 ? "+" : "")}{v}");
                    break;
                }
                case "NextBattleNoMulligan":
                    runData.nextBattleNoMulligan = true;
                    outcome.messages.Add("下场战斗无法弃牌重抽");
                    break;
                case "NextBattleEnemyDebuff":
                {
                    int v = GetInt(result.value);
                    runData.nextBattleEnemyDebuff += v;
                    outcome.messages.Add($"下场战斗敌人弱化 {v} 级");
                    break;
                }
                case "NextBattleDrawBonus":
                {
                    int v = GetInt(result.value);
                    runData.nextBattleDrawBonus += v;
                    outcome.messages.Add($"下场战斗抽牌数 +{v}");
                    break;
                }
                case "GuaranteeSuitHand":
                {
                    if (ctx == null || !ctx.hasSuit) break;
                    int v = GetInt(result.value);
                    runData.nextBattleGuaranteeSuit = (int)ctx.suit;
                    runData.nextBattleGuaranteeCount = v;
                    outcome.messages.Add($"下场战斗开局保底 {v} 张{SuitName(ctx.suit)}");
                    break;
                }
                case "RevealHiddenSuits":
                    runData.nextBattleRevealAll = true;
                    outcome.messages.Add("下场战斗命运一览不再隐藏");
                    break;
                case "RandomBigReward":
                {
                    // 60% 稀有及以上遗物 / 40% 3 个稀有及以上附魔
                    if (UnityEngine.Random.value < 0.6f)
                    {
                        var pool = RewardSystem.GetRandomRelicPool(runData, "RareOrAbove");
                        if (pool.Count > 0)
                        {
                            var relic = pool[UnityEngine.Random.Range(0, pool.Count)];
                            GrantRelic(runData, outcome, relic);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            int enchId = PickEnchantmentByRarity("RareOrAbove");
                            if (enchId <= 0) break;
                            int r = UnityEngine.Random.Range(2, 15);
                            Suit s = (Suit)UnityEngine.Random.Range(0, 4);
                            runData.AddEnchantment(r, s, enchId);
                            outcome.enchantments.Add(MakeGranted(r, s, enchId));
                        }
                    }
                    break;
                }
                case "ResetDestiny":
                {
                    string before = runData.HasMainDestiny ? DestinyInfo.SuitName((Suit)runData.mainDestinySuit) : "无";
                    runData.ResetMainDestiny();
                    string after = runData.HasMainDestiny ? DestinyInfo.SuitName((Suit)runData.mainDestinySuit) : "无";
                    outcome.messages.Add($"主命格重置：{before} → {after}");
                    break;
                }
                case "AddDestinyPoint":
                {
                    if (!runData.HasMainDestiny) break;
                    int v = GetInt(result.value);
                    runData.AddDestinyPoint((Suit)runData.mainDestinySuit, v);
                    outcome.messages.Add($"主命格花色 +{v} 命格值");
                    break;
                }
                case "CurseCardRandom":
                {
                    int curseId = GetInt(result.value);
                    if (curseId <= 0 || ConfigLoader.GetEnchantment(curseId) == null)
                        curseId = EnchantCurseId;

                    int count = result.count > 0 ? result.count : 1;
                    var picks = ShuffledAllCards();
                    int n = Mathf.Min(count, picks.Count);
                    for (int i = 0; i < n; i++)
                    {
                        runData.AddEnchantment(picks[i].rank, picks[i].suit, curseId);
                        outcome.enchantments.Add(MakeGranted(picks[i].rank, picks[i].suit, curseId));
                    }
                    break;
                }
                case "EnchantRandomCardWith":
                {
                    int enchId = GetInt(result.value);
                    if (ConfigLoader.GetEnchantment(enchId) == null) break;

                    int count = result.count > 0 ? result.count : 1;
                    var picks = ShuffledAllCards();
                    int n = Mathf.Min(count, picks.Count);
                    for (int i = 0; i < n; i++)
                    {
                        runData.AddEnchantment(picks[i].rank, picks[i].suit, enchId);
                        outcome.enchantments.Add(MakeGranted(picks[i].rank, picks[i].suit, enchId));
                    }
                    break;
                }
                case "AddDestinyPointSuit":
                {
                    if (!TryParseSuit(result.suit, out var ds)) break;
                    int v = GetInt(result.value);
                    runData.AddDestinyPoint(ds, v);
                    outcome.messages.Add($"{DestinyInfo.SuitName(ds)} 命格值 +{v}");
                    break;
                }
                case "SealSuitNextBattle":
                {
                    if (!TryParseSuit(result.suit, out var ss)) break;
                    if (!runData.nextBattleSealedSuits.Contains((int)ss))
                        runData.nextBattleSealedSuits.Add((int)ss);
                    outcome.messages.Add($"下场战斗无法打出 {DestinyInfo.PlainSuitName(ss)}");
                    break;
                }
                case "StartCombatWithReward":
                {
                    int relicId = GetInt(result.value);
                    runData.nextBattleBonusRelicId = relicId;
                    runData.pendingEventEnemyId = result.enemyId;
                    var r = ConfigLoader.GetRelic(relicId);
                    if (r != null) outcome.messages.Add($"获得遗物：{r.name}");
                    break;
                }
                case "LoseHp":
                {
                    int v = GetInt(result.value);
                    runData.CurrentHp = Mathf.Max(1, runData.CurrentHp - v);
                    outcome.messages.Add($"失去 {v} 点生命");
                    break;
                }
                case "RemoveSpecificRelic":
                {
                    int relicId = GetInt(result.value);
                    var relic = ConfigLoader.GetRelic(relicId);
                    if (runData.TryRemoveRelic(relicId))
                        outcome.messages.Add($"失去遗物：{relic?.name}");
                    break;
                }
                case "GainSpecificRelic":
                {
                    int relicId = GetInt(result.value);
                    var relic = ConfigLoader.GetRelic(relicId);
                    if (relic == null) break;
                    GrantRelic(runData, outcome, relic);
                    break;
                }
                case "Gamble":
                {
                    bool win = UnityEngine.Random.value < 0.5f;
                    outcome.messages.Add(win ? "赌博：赢了！" : "赌博：输了…");
                    var list = win ? result.win : result.lose;
                    if (list != null)
                        foreach (var r in list)
                            ExecuteResult(r, runData, relicSystem, outcome, ctx, null, -1);
                    break;
                }
                // ===== 红桃事件 =====

                case "GainGoldPerHp":
                {
                    int per = result.value != null ? GetInt(result.value) : 1;
                    int gold = runData.CurrentHp * per;
                    if (relicSystem != null) relicSystem.GainGold(gold);
                    else runData.Gold += gold;
                    runData.totalGoldGained += gold;
                    outcome.messages.Add($"按当前生命获得 {gold} 金币");
                    break;
                }
                case "LoseHpPercent":
                {
                    int percent = GetInt(result.value);
                    int loss = Mathf.Max(1, Mathf.RoundToInt(runData.CurrentHp * percent / 100f));
                    runData.CurrentHp = Mathf.Max(1, runData.CurrentHp - loss);
                    outcome.messages.Add($"失去 {loss} 点生命");
                    break;
                }
                case "SetHpPercent":
                {
                    int percent = GetInt(result.value);
                    int target = Mathf.Max(1, Mathf.RoundToInt(runData.MaxHp * percent / 100f));
                    runData.CurrentHp = target;
                    outcome.messages.Add($"生命值变为 {target}（最大生命的 {percent}%）");
                    break;
                }
                case "NextBattleStartStatus":
                {
                    if (string.IsNullOrEmpty(result.status)) break;
                    int stacks = GetInt(result.value);
                    int turns = result.count > 0 ? result.count : -1;
                    runData.nextBattleStartStatuses.Add(new RunData.BattleStartStatus
                    {
                        status = result.status,
                        stacks = stacks,
                        turns = turns
                    });
                    string turnText = turns > 0 ? $"{turns} 回合" : "整场战斗";
                    outcome.messages.Add($"下场战斗开局获得 {stacks} 层{StatusName(result.status)}（{turnText}）");
                    break;
                }
                case "GainSpecificPotion":
                {
                    int potionId = GetInt(result.value);
                    var potion = ConfigLoader.GetPotion(potionId);
                    if (potion == null) break;
                    GrantPotion(runData, outcome, potion);
                    break;
                }
                case "SacrificePotions":
                {
                    int perMax = result.count > 0 ? result.count : 5;
                    var owned = new List<int>(runData.PotionIds);
                    if (owned.Count == 0)
                    {
                        outcome.messages.Add("没有药水可以交出");
                        break;
                    }

                    var names = new List<string>();
                    foreach (var id in owned)
                    {
                        var p = ConfigLoader.GetPotion(id);
                        if (p != null) names.Add(p.name);
                        runData.TryRemovePotion(id);
                    }
                    int gain = owned.Count * perMax;
                    runData.MaxHp += gain;
                    outcome.messages.Add($"交出 {owned.Count} 瓶药水（{string.Join("、", names)}），最大生命 +{gain}");
                    break;
                }
                case "ReplaceEnchant":
                {
                    if (ctx == null || !ctx.hasCard) break;
                    int toId = GetInt(result.value);
                    int fromId = ParseRequireEnchant(result.require);
                    var toEnch = ConfigLoader.GetEnchantment(toId);
                    if (toEnch == null) break;

                    var fromEnch = ConfigLoader.GetEnchantment(fromId);
                    runData.RemoveEnchantment(ctx.cardRank, ctx.cardSuit, fromId);
                    runData.AddEnchantment(ctx.cardRank, ctx.cardSuit, toId);
                    outcome.messages.Add($"{CardName(ctx.cardRank, ctx.cardSuit)} 的附魔「{fromEnch?.name}」替换为「{toEnch.name}」");
                    outcome.enchantments.Add(MakeGranted(ctx.cardRank, ctx.cardSuit, toId));
                    break;
                }
                case "EnchantCardWithSpecific":
                {
                    if (ctx == null || !ctx.hasCard) break;
                    int enchId = GetInt(result.value);
                    if (ConfigLoader.GetEnchantment(enchId) == null) break;
                    runData.AddEnchantment(ctx.cardRank, ctx.cardSuit, enchId);
                    outcome.enchantments.Add(MakeGranted(ctx.cardRank, ctx.cardSuit, enchId));
                    break;
                }

                // ===== 梅花事件 =====

                case "EnchantRandomCards":
                {
                    // count = 张数，value = 每张附魔数量，rarity = 稀有度筛选
                    int cards = result.count > 0 ? result.count : 1;
                    int perCard = result.value != null ? Mathf.Max(1, GetInt(result.value)) : 1;

                    var picks = ShuffledAllCards();
                    int n = Mathf.Min(cards, picks.Count);
                    for (int i = 0; i < n; i++)
                    {
                        for (int k = 0; k < perCard; k++)
                        {
                            int enchId = PickEnchantmentByRarity(result.rarity);
                            if (enchId <= 0) break;
                            runData.AddEnchantment(picks[i].rank, picks[i].suit, enchId);
                            outcome.enchantments.Add(MakeGranted(picks[i].rank, picks[i].suit, enchId));
                        }
                    }
                    break;
                }
                case "RandomOutcome":
                {
                    var list = result.outcomes;
                    if (list == null || list.Count == 0) break;

                    int total = 0;
                    foreach (var o in list) if (o != null) total += Mathf.Max(0, o.weight);
                    if (total <= 0) break;

                    int roll = UnityEngine.Random.Range(0, total);
                    int cum = 0;
                    foreach (var o in list)
                    {
                        if (o == null) continue;
                        cum += Mathf.Max(0, o.weight);
                        if (roll < cum)
                        {
                            ExecuteResult(o, runData, relicSystem, outcome, ctx, option, resultIndex);
                            break;
                        }
                    }
                    break;
                }
                case "SacrificeEnchantsByRarity":
                {
                    if (ctx == null || !ctx.hasCard) break;
                    string want = string.IsNullOrEmpty(result.rarity) ? "Common" : result.rarity;

                    var lost = new List<int>();
                    foreach (var id in new List<int>(runData.GetCardEnchantments(ctx.cardRank, ctx.cardSuit)))
                    {
                        var e = ConfigLoader.GetEnchantment(id);
                        if (e != null && e.rarity == want)
                        {
                            runData.RemoveEnchantment(ctx.cardRank, ctx.cardSuit, id);
                            lost.Add(id);
                        }
                    }

                    if (lost.Count == 0)
                    {
                        outcome.messages.Add($"{CardName(ctx.cardRank, ctx.cardSuit)} 没有{RarityUtil.Name(want)}附魔");
                        break;
                    }

                    var lostNames = new List<string>();
                    foreach (var id in lost)
                    {
                        var e = ConfigLoader.GetEnchantment(id);
                        if (e != null) lostNames.Add(e.name);
                    }
                    outcome.messages.Add($"{CardName(ctx.cardRank, ctx.cardSuit)} 献祭了：{string.Join("、", lostNames)}");

                    var pool = RewardSystem.GetRandomRelicPool(runData, want);
                    if (pool.Count > 0)
                    {
                        var relic = pool[UnityEngine.Random.Range(0, pool.Count)];
                        GrantRelic(runData, outcome, relic);
                    }
                    break;
                }
                case "RerollAllEnchantments":
                {
                    int changed = 0;
                    foreach (var key in runData.GetEnchantedCardKeys())
                    {
                        int us = key.IndexOf('_');
                        if (us <= 0) continue;
                        if (!System.Enum.TryParse(key.Substring(0, us), out Suit s)) continue;
                        if (!int.TryParse(key.Substring(us + 1), out int r)) continue;

                        var olds = new List<int>(runData.GetCardEnchantments(r, s));
                        if (olds.Count == 0) continue;

                        runData.ClearCardEnchantments(r, s);
                        foreach (var oldId in olds)
                        {
                            var oldE = ConfigLoader.GetEnchantment(oldId);
                            string rarity = oldE?.rarity ?? "Common";

                            // 诅咒不属于「同等级附魔」，原样保留
                            if (rarity == "Curse")
                            {
                                runData.AddEnchantment(r, s, oldId);
                                continue;
                            }

                            int newId = PickEnchantmentByRarityExcluding(rarity, oldId);
                            if (newId <= 0)
                            {
                                runData.AddEnchantment(r, s, oldId);
                                continue;
                            }
                            runData.AddEnchantment(r, s, newId);
                            changed++;
                            outcome.enchantments.Add(MakeGranted(r, s, newId));
                        }
                    }
                    outcome.messages.Add(changed > 0 ? $"重构了 {changed} 个附魔" : "没有可重构的附魔");
                    break;
                }
                case "CopyEnchantsBetweenCards":
                {
                    if (ctx == null || !ctx.hasCard) break;
                    if (ctx.extraCardRanks.Count == 0 || ctx.extraCardSuits.Count == 0) break;

                    int srcR = ctx.cardRank, dstR = ctx.extraCardRanks[0];
                    Suit srcS = ctx.cardSuit, dstS = ctx.extraCardSuits[0];

                    var src = new List<int>(runData.GetCardEnchantments(srcR, srcS));
                    if (src.Count == 0)
                    {
                        outcome.messages.Add($"{CardName(srcR, srcS)} 没有附魔可复制");
                        break;
                    }

                    foreach (var id in src)
                    {
                        runData.AddEnchantment(dstR, dstS, id);
                        outcome.enchantments.Add(MakeGranted(dstR, dstS, id));
                    }
                    outcome.messages.Add($"把 {CardName(srcR, srcS)} 的 {src.Count} 个附魔复制到了 {CardName(dstR, dstS)}");
                    break;
                }
                case "PermanentDamageMultiplier":
                {
                    int percent = GetInt(result.value);
                    runData.permanentDamageMultiplier *= (1f + percent / 100f);
                    outcome.messages.Add($"造成伤害永久 {(percent >= 0 ? "+" : "")}{percent}%（当前 ×{runData.permanentDamageMultiplier:0.##}）");
                    break;
                }
                case "PermanentStraightDraw":
                {
                    int v = GetInt(result.value);
                    runData.permanentStraightDrawBonus += v;
                    outcome.messages.Add($"永久：顺子额外抽 {v} 张牌");
                    break;
                }
                case "NextBattleStraightDraw":
                {
                    int v = GetInt(result.value);
                    runData.nextBattleStraightDraw += v;
                    outcome.messages.Add($"下场战斗顺子额外抽 {v} 张牌");
                    break;
                }
                case "PermanentFlushDraw":
                {
                    int v = GetInt(result.value);
                    runData.permanentFlushDrawBonus += v;
                    outcome.messages.Add($"永久：同花额外抽 {v} 张牌");
                    break;
                }
                case "PermanentFlushDamage":
                {
                    int v = GetInt(result.value);
                    runData.permanentFlushDamageBonus += v;
                    outcome.messages.Add($"永久：同花伤害 +{v}");
                    break;
                }
                case "NextBattleFatePowerFull":
                    runData.nextBattleFateFull = true;
                    outcome.messages.Add("下场战斗开局回满命运之力");
                    break;
                case "NextBattleFateBonus":
                {
                    int v = GetInt(result.value);
                    runData.nextBattleFateBonus += v;
                    outcome.messages.Add($"下场战斗命运之力积攒 +{v}");
                    break;
                }
                case "DisableFateGainNextBattles":
                {
                    int v = result.count > 0 ? result.count : 1;
                    runData.fateGainDisabledBattles += v;
                    outcome.messages.Add($"接下来 {v} 场战斗无法积攒命运之力");
                    break;
                }
                case "LoseChosenRelic":
                {
                    if (ctx == null || !ctx.hasRelic) break;
                    var relic = ConfigLoader.GetRelic(ctx.relicId);
                    if (runData.TryRemoveRelic(ctx.relicId))
                        outcome.messages.Add($"失去遗物：{relic?.name}");
                    break;
                }
                case "StartChallenge":
                {
                    int limit = GetInt(result.value);
                    runData.challengeHpLimit = limit;
                    runData.challengeWin = result.win;
                    runData.challengeLose = result.lose;
                    outcome.messages.Add($"接受挑战：下一场战斗失去生命不超过 {limit} 点");
                    break;
                }
                // ===== 方片事件 =====

                case "NextBattleStrengthFromGold":
                {
                    int per = Mathf.Max(1, GetInt(result.value));
                    int stacks = runData.Gold / per;
                    if (stacks <= 0)
                    {
                        outcome.messages.Add($"金币不足，没有获得力量");
                        break;
                    }
                    runData.nextBattleStartStatuses.Add(new RunData.BattleStartStatus
                    {
                        status = "Strength",
                        stacks = stacks,
                        turns = -1
                    });
                    outcome.messages.Add($"下场战斗开局获得 {stacks} 层力量");
                    break;
                }
                case "HealPerGold":
                {
                    int per = Mathf.Max(1, GetInt(result.value));
                    int heal = runData.Gold / per;
                    if (heal <= 0)
                    {
                        outcome.messages.Add($"金币不足，没有回复生命");
                        break;
                    }
                    int before = runData.CurrentHp;
                    runData.CurrentHp = Mathf.Min(runData.MaxHp, runData.CurrentHp + heal);
                    outcome.messages.Add($"回复 {runData.CurrentHp - before} 点生命");
                    break;
                }
                case "RemoveAllAndEnchantRandomCard":
                {
                    int enchId = GetInt(result.value);
                    var ench = ConfigLoader.GetEnchantment(enchId);
                    if (ench == null) break;

                    var picks = ShuffledAllCards();
                    if (picks.Count == 0) break;
                    var pick = picks[0];

                    var lostNames = new List<string>();
                    foreach (var id in runData.GetCardEnchantments(pick.rank, pick.suit))
                    {
                        var e = ConfigLoader.GetEnchantment(id);
                        if (e != null) lostNames.Add(e.name);
                    }
                    runData.ClearCardEnchantments(pick.rank, pick.suit);
                    runData.AddEnchantment(pick.rank, pick.suit, enchId);
                    outcome.enchantments.Add(MakeGranted(pick.rank, pick.suit, enchId));
                    outcome.messages.Add(lostNames.Count > 0
                        ? $"{CardName(pick.rank, pick.suit)} 失去了「{string.Join("、", lostNames)}」，获得「{ench.name}」"
                        : $"{CardName(pick.rank, pick.suit)} 获得「{ench.name}」");
                    break;
                }
                case "EnchantRandomCardsHandType":
                case "EnchantRandomCardsBuff":
                {
                    // 只从配置的 pool 字段抽：HandType 牌型类 / Buff 增益类
                    string wantPool = result.type == "EnchantRandomCardsHandType" ? "HandType" : "Buff";
                    var pool = ConfigLoader.Config.enchantments.FindAll(e =>
                        e.weight > 0 && e.pool == wantPool);
                    if (pool.Count == 0) break;

                    int cards = result.count > 0 ? result.count : 1;
                    var picks = ShuffledAllCards();
                    int n = Mathf.Min(cards, picks.Count);
                    for (int i = 0; i < n; i++)
                    {
                        int enchId = PickWeightedEnchantment(pool);
                        if (enchId <= 0) break;
                        runData.AddEnchantment(picks[i].rank, picks[i].suit, enchId);
                        outcome.enchantments.Add(MakeGranted(picks[i].rank, picks[i].suit, enchId));
                    }
                    break;
                }
                case "StageGamble":
                {
                    // 多阶段事件：投入金币 + 概率获得指定稀有度遗物；失败则进入下一阶段
                    int cost = GetInt(result.value);
                    int chance = result.count > 0 ? result.count : 0;

                    runData.Gold -= cost;
                    outcome.messages.Add($"投入 {cost} 金币（当前 {runData.Gold}）");

                    if (UnityEngine.Random.Range(0, 100) < chance)
                    {
                        var pool = RewardSystem.GetRandomRelicPool(runData, result.rarity);
                        if (pool.Count > 0)
                        {
                            var relic = pool[UnityEngine.Random.Range(0, pool.Count)];
                            outcome.messages.Add("成功！");
                            GrantRelic(runData, outcome, relic);
                        }
                    }
                    else
                    {
                        outcome.messages.Add($"失败…，可以继续投入");
                        runData.currentEventStage++;
                        outcome.reopenEvent = true;
                    }
                    break;
                }
                default:
                    Debug.LogWarning($"[EventSystem] 未实现的结果类型: {result.type}");
                    break;
            }
        }

        /// <summary>按稀有度筛选权重随机一个附魔 id（支持 Any / RareOrAbove；0 = 没有可用的）</summary>
        private static int PickEnchantmentByRarity(string rarityFilter)
        {
            var pool = ConfigLoader.Config.enchantments.FindAll(e => e.weight > 0 && RarityUtil.Matches(e.rarity, rarityFilter));
            if (pool.Count == 0) pool = ConfigLoader.Config.enchantments.FindAll(e => e.weight > 0);
            if (pool.Count == 0) return 0;

            int total = 0;
            foreach (var e in pool) total += e.weight;
            int roll = UnityEngine.Random.Range(0, total);
            int cum = 0;
            foreach (var e in pool)
            {
                cum += e.weight;
                if (roll < cum) return e.id;
            }
            return pool[0].id;
        }

        /// <summary>从给定附魔池按 weight 加权随机一个 id（0 = 空池）</summary>
        private static int PickWeightedEnchantment(List<EnchantmentData> pool)
        {
            if (pool == null || pool.Count == 0) return 0;

            int total = 0;
            foreach (var e in pool) total += Mathf.Max(1, e.weight);
            int roll = UnityEngine.Random.Range(0, total);
            int cum = 0;
            foreach (var e in pool)
            {
                cum += Mathf.Max(1, e.weight);
                if (roll < cum) return e.id;
            }
            return pool[0].id;
        }

        /// <summary>按稀有度筛选权重随机一个附魔 id（排除某个 id；用于「附魔重构」）</summary>
        private static int PickEnchantmentByRarityExcluding(string rarityFilter, int excludeId)
        {
            var pool = ConfigLoader.Config.enchantments.FindAll(e =>
                e.weight > 0 && e.id != excludeId && RarityUtil.Matches(e.rarity, rarityFilter));
            if (pool.Count == 0)
                pool = ConfigLoader.Config.enchantments.FindAll(e => e.weight > 0 && e.id != excludeId);
            if (pool.Count == 0) return 0;

            int total = 0;
            foreach (var e in pool) total += e.weight;
            int roll = UnityEngine.Random.Range(0, total);
            int cum = 0;
            foreach (var e in pool)
            {
                cum += e.weight;
                if (roll < cum) return e.id;
            }
            return pool[0].id;
        }

        private static GrantedEnchantment MakeGranted(int rank, Suit suit, int enchantmentId)
        {
            return new GrantedEnchantment
            {
                cardKey = RunData.GetCardKey(rank, suit),
                cardDisplayName = CardName(rank, suit),
                enchantmentId = enchantmentId
            };
        }

        private static string CardName(int rank, Suit suit)
        {
            string r;
            switch (rank)
            {
                case 11: r = "J"; break;
                case 12: r = "Q"; break;
                case 13: r = "K"; break;
                case 14: r = "A"; break;
                default: r = rank.ToString(); break;
            }
            string s;
            switch (suit)
            {
                case Suit.Spade: s = "♠"; break;
                case Suit.Heart: s = "♥"; break;
                case Suit.Club: s = "♣"; break;
                default: s = "♦"; break;
            }
            return r + s;
        }

        private static string SuitName(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spade: return "黑桃";
                case Suit.Heart: return "红桃";
                case Suit.Club: return "梅花";
                default: return "方块";
            }
        }

        /// <summary>状态效果的中文名（事件结算文案用）</summary>
        private static string StatusName(string status)
        {
            switch (status)
            {
                case "Poison": return "中毒";
                case "Burn": return "灼烧";
                case "Weaken": return "虚弱";
                case "Strength": return "力量";
                case "Dexterity": return "敏捷";
                case "Focus": return "专注";
                case "Artifact": return "神器";
                case "Thorns": return "荆棘";
                case "Regeneration": return "再生";
                case "Metallicize": return "金属化";
                case "Intangible": return "无形";
                case "Vulnerable": return "易伤";
                default: return status;
            }
        }

        private static void ClearOneCard(int rank, Suit suit, RunData runData, EventOutcome outcome)
        {
            var lostNames = new List<string>();
            foreach (var id in runData.GetCardEnchantments(rank, suit))
            {
                var e = ConfigLoader.GetEnchantment(id);
                if (e != null) lostNames.Add(e.name);
            }
            runData.ClearCardEnchantments(rank, suit);

            if (lostNames.Count > 0)
                outcome.messages.Add($"{CardName(rank, suit)} 失去了附魔：{string.Join("、", lostNames)}");
            else
                outcome.messages.Add($"{CardName(rank, suit)} 失去了所有附魔");
        }

        /// <summary>全部 52 张牌，打乱顺序</summary>
        private static List<CardRef> ShuffledAllCards()
        {
            var all = new List<CardRef>();
            foreach (Suit s in new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond })
                for (int r = 2; r <= 14; r++)
                    all.Add(new CardRef { rank = r, suit = s });

            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var t = all[i]; all[i] = all[j]; all[j] = t;
            }
            return all;
        }

        private static bool TryParseSuit(string s, out Suit suit)
        {
            suit = Suit.Spade;
            if (string.IsNullOrEmpty(s)) return false;
            return System.Enum.TryParse(s, true, out suit);
        }

        private static int GetInt(object value)
        {
            if (value == null) return 0;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (value is float f) return (int)f;
            if (int.TryParse(value.ToString(), out var parsed)) return parsed;
            return 0;
        }
    }
}
