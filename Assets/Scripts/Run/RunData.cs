using System;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 当前运行的完整状态（可序列化为 JSON 存档）
    /// </summary>
    [Serializable]
    public class RunData
    {
        // 基础
        public int seed;
        public int actId = 1;              // 当前章节
        public int battleIndex = 0;        // 当前第几场战斗（从 0 开始）
        public bool isBossBattle = false;  // 下一场是否 Boss

        // 玩家状态
        private int _maxHp = 30;
        public int MaxHp
        {
            get => _maxHp;
            set
            {
                if (_maxHp != value)
                {
                    _maxHp = value;
                    OnMaxHpChanged?.Invoke(_maxHp);
                }
            }
        }

        private int _currentHp = 30;
        public int CurrentHp
        {
            get => _currentHp;
            set
            {
                if (_currentHp != value)
                {
                    _currentHp = Mathf.Clamp(value, 0, MaxHp);
                    OnCurrentHpChanged?.Invoke(_currentHp);
                }
            }
        }

        private int _gold = 0;
        public int Gold
        {
            get => _gold;
            set
            {
                if (_gold != value)
                {
                    _gold = Mathf.Max(0, value);
                    OnGoldChanged?.Invoke(_gold);
                }
            }
        }

        // 兼容旧字段名
        public int maxHp { get => MaxHp; set => MaxHp = value; }
        public int currentHp { get => CurrentHp; set => CurrentHp = value; }
        public int gold { get => Gold; set => Gold = value; }

        // 牌组：固定 52 张，只存每张牌的附魔 ID 列表
        // key: "Suit_Rank" 如 "Spade_14"（A）、"Heart_2"
        public Dictionary<string, List<int>> cardEnchantmentIds = new Dictionary<string, List<int>>();

        // 遗物（槽位制，最多 6 格）
        private List<int> _relicIds = new List<int>();
        public IReadOnlyList<int> RelicIds => _relicIds;
        public event Action OnRelicsChanged;
        public List<int> relicIds { get => _relicIds; set => _relicIds = value; }

        // 药水槽（最多 3 格）
        private List<int> _potionIds = new List<int>();
        public IReadOnlyList<int> PotionIds => _potionIds;
        public event Action OnPotionsChanged;
        public List<int> potionIds { get => _potionIds; set => _potionIds = value; }

        // 进度
        public List<int> visitedEventIds = new List<int>();     // 已触发事件 ID（防重复）

        /// <summary>当前事件阶段（多阶段事件用，从 1 起；进入事件时重置为 1）</summary>
        public int currentEventStage = 1;
        public List<string> unlockedContent = new List<string>(); // 解锁内容
        public int eliteKillCount = 0;

        // 临时状态（战斗间传递）
        public int nextBattleStartHpMod = 0;      // 下场战斗初始血量修正
        public int nextBattleEnemyBuff = 0;       // 下场战斗敌人强化等级

        // ===== 事件 / 永久效果 =====
        public int permanentStartStrength = 0;       // 永久：每场战斗开局 +N 力量
        public int permanentTurnEndSelfDamage = 0;   // 永久：每回合结束自伤 N
        public int nextBattleMulliganDelta = 0;      // 下场重抽次数修正
        public bool nextBattleNoMulligan = false;    // 下场重抽次数归零
        public int nextBattleEnemyDebuff = 0;        // 下场敌人弱化级数
        public int nextBattleDrawBonus = 0;          // 下场抽牌数 +N
        public int nextBattleGuaranteeSuit = -1;     // 下场开局保底花色（-1=无）
        public int nextBattleGuaranteeCount = 0;     // 下场保底张数
        public bool nextBattleRevealAll = false;     // 下场命运一览不隐藏
        public List<int> nextBattleSealedSuits = new List<int>();  // 下场无法打出的花色

        /// <summary>下场战斗开始时获得的状态（事件用）</summary>
        [System.Serializable]
        public class BattleStartStatus
        {
            public string status;      // StatusEffectType 名称
            public int stacks;         // 层数
            public int turns = -1;     // 回合数（-1 = 整场战斗）
        }
        public List<BattleStartStatus> nextBattleStartStatuses = new List<BattleStartStatus>();
        public int nextBattleBonusRelicId = 0;       // 下场战斗胜利后额外获得的遗物
        public int pendingEventEnemyId = 0;          // 事件专属战斗的敌人 id（0=无）

        // ===== 梅花事件：永久修正 / 下场战斗修正 =====
        public float permanentDamageMultiplier = 1f;   // 永久：造成伤害倍率（事件「-25%」= 0.75）
        public int permanentStraightDrawBonus = 0;     // 永久：顺子/同花顺 额外抽牌
        public int permanentFlushDrawBonus = 0;        // 永久：同花/同花顺 额外抽牌
        public int permanentFlushDamageBonus = 0;      // 永久：同花/同花顺 额外伤害
        public int nextBattleStraightDraw = 0;         // 下场战斗：顺子额外抽牌
        public bool nextBattleFateFull = false;        // 下场战斗开局回满命运之力
        public int nextBattleFateBonus = 0;            // 下场战斗命运之力每次积攒 +N
        public int fateGainDisabledBattles = 0;        // 接下来 N 场战斗无法积攒命运之力

        // ===== 挑战（事件）：下场战斗失血不超过 N 则胜 =====
        public int challengeHpLimit = -1;              // -1 = 无挑战
        public List<Roguelike.Data.EventResultData> challengeWin;
        public List<Roguelike.Data.EventResultData> challengeLose;

        // 花色命运：每局开始随机分配「主导花色 → 事件」
        // 主题固定（黑桃=危险/红桃=生命/梅花=成长/方块=财富），具体事件每局随机
        public int spadeEventId = 0;
        public int heartEventId = 0;
        public int clubEventId = 0;
        public int diamondEventId = 0;

        // 被隐藏为「？」的花色（每局随机 1~2 个，增加信息博弈）
        public List<int> hiddenSuitIndices = new List<int>();

        // 保底商店占用的花色槽（仅用于「命运一览」显示，-1=未定）
        public int shopSuitIndex = -1;

        // ===== 命格系统 =====
        // 命格值（按 Suit 顺序：Spade, Heart, Club, Diamond）
        public List<int> destinyPoints = new List<int> { 0, 0, 0, 0 };
        public int mainDestinySuit = -1;          // 主命格花色（-1=未定）
        public int fatePower = 0;                 // 命运之力（主命格主动技能充能）
        public bool heartDeathSaveUsed = false;   // 红桃 Lv2「每局一次免死」是否已用
        public bool phoenixUsed = false;          // 遗物「不死鸟」是否已用
        public int battlesSinceShop = 0;          // 距上次商店经过的战斗数（4 场保底）

        /// <summary>遗物触发计数（如「滋养手环」每局最多 5 次）</summary>
        public Dictionary<string, int> relicCounters = new Dictionary<string, int>();
        public int GetRelicCounter(string key) => relicCounters.TryGetValue(key, out var v) ? v : 0;
        public void AddRelicCounter(string key, int amount) => relicCounters[key] = GetRelicCounter(key) + amount;

        /// <summary>遗物「双倍附魔」：是否拥有（实时查询，作弊/奖励/事件都能生效）</summary>
        public bool doubleEnchant
        {
            get
            {
                foreach (int id in _relicIds)
                {
                    var r = ConfigLoader.GetRelic(id);
                    if (r != null && r.effects != null && r.effects.Exists(e => e.type == "DoubleEnchant"))
                        return true;
                }
                return false;
            }
        }

        public const int FatePowerMax = 20;
        public const int DestinyLv1At = 2;
        public const int DestinyLv2At = 5;
        public const int DestinyLv3At = 10;

        public event Action OnDestinyChanged;
        public void NotifyDestinyChanged() => OnDestinyChanged?.Invoke();

        public static int RankFromPoints(int points)
        {
            if (points >= DestinyLv3At) return 3;
            if (points >= DestinyLv2At) return 2;
            if (points >= DestinyLv1At) return 1;
            return 0;
        }

        public int GetDestinyRank(Suit suit) => RankFromPoints(destinyPoints[(int)suit]);
        public bool IsMainSuit(Suit suit) => mainDestinySuit == (int)suit;
        public bool HasMainDestiny => mainDestinySuit >= 0;
        public int MainDestinyRank => mainDestinySuit >= 0 ? GetDestinyRank((Suit)mainDestinySuit) : 0;

        /// <summary>给某花色加命格值（并刷新 UI）</summary>
        public void AddDestinyPoint(Suit suit, int amount)
        {
            destinyPoints[(int)suit] = Mathf.Max(0, destinyPoints[(int)suit] + amount);

            // 主命格：第一个达到 Lv.1 的花色（无论命格值来自战斗还是事件）
            TrySetMainDestiny(suit);

            NotifyDestinyChanged();
        }

        /// <summary>主命格尚未确立时，若该花色已达 Lv.1 则确立为主命格（返回是否本次确立）</summary>
        public bool TrySetMainDestiny(Suit suit)
        {
            if (mainDestinySuit >= 0) return false;
            if (GetDestinyRank(suit) < 1) return false;

            mainDestinySuit = (int)suit;
            return true;
        }

        /// <summary>重置主命格：在已有命格值（≥1）的花色里重新随机一个（尽量换一个不同的）</summary>
        public void ResetMainDestiny()
        {
            var candidates = new List<int>();
            // 候选：已经达到 Lv1 的花色；一个都没有时退回「有命格值」的花色
            for (int i = 0; i < destinyPoints.Count; i++)
                if (RankFromPoints(destinyPoints[i]) >= 1) candidates.Add(i);

            if (candidates.Count == 0)
            {
                for (int i = 0; i < destinyPoints.Count; i++)
                    if (destinyPoints[i] >= 1) candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                mainDestinySuit = -1;
            }
            else
            {
                var pool = candidates;
                if (candidates.Count > 1 && mainDestinySuit >= 0)
                {
                    var others = candidates.FindAll(i => i != mainDestinySuit);
                    if (others.Count > 0) pool = others;
                }
                mainDestinySuit = pool[UnityEngine.Random.Range(0, pool.Count)];
            }
            NotifyDestinyChanged();
        }

        // 统计
        public int battlesWon = 0;
        public int totalDamageDealt = 0;
        public int totalGoldGained = 0;

        // 事件
        public event Action<int> OnMaxHpChanged;
        public event Action<int> OnCurrentHpChanged;
        public event Action<int> OnGoldChanged;

        // 兼容旧事件名
        public event Action<int> OnMaxHpChanged_Obsolete { add => OnMaxHpChanged += value; remove => OnMaxHpChanged -= value; }
        public event Action<int> OnCurrentHpChanged_Obsolete { add => OnCurrentHpChanged += value; remove => OnCurrentHpChanged -= value; }
        public event Action<int> OnGoldChanged_Obsolete { add => OnGoldChanged += value; remove => OnGoldChanged -= value; }

        /// <summary>
        /// 获取卡牌唯一键
        /// </summary>
        public static string GetCardKey(int rank, Suit suit)
        {
            return $"{suit}_{rank}";
        }

        /// <summary>
        /// 获取某张牌的附魔 ID 列表
        /// </summary>
        public List<int> GetCardEnchantments(int rank, Suit suit)
        {
            if (IsPolluted(rank, suit)) return new List<int>();   // 被污染：所有附魔失效
            string key = GetCardKey(rank, suit);
            if (cardEnchantmentIds.TryGetValue(key, out var list))
                return list;
            return new List<int>();
        }

        /// <summary>
        /// <summary>某张牌是否已有附魔</summary>
        public bool HasCardEnchantment(int rank, Suit suit)
        {
            if (IsPolluted(rank, suit)) return false;
            return cardEnchantmentIds.TryGetValue(GetCardKey(rank, suit), out var list) && list != null && list.Count > 0;
        }

        /// <summary>
        /// 给某张牌添加附魔
        /// </summary>
        public void AddEnchantment(int rank, Suit suit, int enchantmentId, bool allowBonus = true)
        {
            CodexData.DiscoverEnchantment(enchantmentId);   // 图鉴：获得即算发现
            string key = GetCardKey(rank, suit);
            if (!cardEnchantmentIds.ContainsKey(key))
                cardEnchantmentIds[key] = new List<int>();

            // 互斥组：同一张牌上同组只能有一个（如 万能牌 / 变色 / 镜牌）
            var ench = ConfigLoader.GetEnchantment(enchantmentId);
            string group = ench?.exclusiveGroup;
            if (!string.IsNullOrEmpty(group))
            {
                cardEnchantmentIds[key].RemoveAll(id =>
                {
                    if (id == enchantmentId) return false;
                    var other = ConfigLoader.GetEnchantment(id);
                    return other != null && other.exclusiveGroup == group;
                });
            }

            if (!cardEnchantmentIds[key].Contains(enchantmentId))
                cardEnchantmentIds[key].Add(enchantmentId);

            // 遗物「双倍附魔」：仅当本次加的是「正常附魔」时才再送一个（诅咒 / 事件专属 / 临时附魔都不触发）
            bool isNormalEnchant = ench != null && RarityUtil.Matches(ench.rarity, "");
            if (allowBonus && isNormalEnchant && doubleEnchant && !grantingBonusEnchant)
                GrantBonusEnchantment(rank, suit, enchantmentId);
        }

        private bool grantingBonusEnchant = false;

        private void GrantBonusEnchantment(int rank, Suit suit, int excludeId)
        {
            var pool = ConfigLoader.Config?.enchantments;
            if (pool == null) return;

            string key = GetCardKey(rank, suit);
            var existing = cardEnchantmentIds.TryGetValue(key, out var list) ? list : new List<int>();

            // 该牌当前已有的互斥组（避免送来的附魔顶掉已有附魔）
            var usedGroups = new HashSet<string>();
            foreach (var id in existing)
            {
                var e = ConfigLoader.GetEnchantment(id);
                if (e != null && !string.IsNullOrEmpty(e.exclusiveGroup)) usedGroups.Add(e.exclusiveGroup);
            }

            // 只挑「正常」附魔：有权重、非诅咒、非事件专属、不重复、不与已有互斥组冲突
            var candidates = pool.FindAll(e =>
                e != null &&
                e.id != excludeId &&
                e.weight > 0 &&
                RarityUtil.Matches(e.rarity, "") &&
                !existing.Contains(e.id) &&
                (string.IsNullOrEmpty(e.exclusiveGroup) || !usedGroups.Contains(e.exclusiveGroup)));

            if (candidates.Count == 0) return;

            grantingBonusEnchant = true;
            try
            {
                var ench = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                AddEnchantment(rank, suit, ench.id);
            }
            finally
            {
                grantingBonusEnchant = false;
            }
        }

        /// <summary>
        /// 移除某张牌的某个附魔
        /// </summary>
        public void RemoveEnchantment(int rank, Suit suit, int enchantmentId)
        {
            string key = GetCardKey(rank, suit);
            if (cardEnchantmentIds.TryGetValue(key, out var list))
                list.Remove(enchantmentId);
        }

        /// <summary>
        /// 清空某张牌的所有附魔
        /// </summary>
        public void ClearCardEnchantments(int rank, Suit suit)
        {
            string key = GetCardKey(rank, suit);
            if (cardEnchantmentIds.ContainsKey(key))
                cardEnchantmentIds[key].Clear();
        }

        // ===== 本场战斗临时附魔（战斗结束自动移除，如「魔导」给的）=====

        /// <summary>临时附魔记录：卡牌 key → 附魔 id 列表</summary>
        public Dictionary<string, List<int>> tempCardEnchantments = new Dictionary<string, List<int>>();

        /// <summary>添加本场战斗临时附魔（战斗结束时自动移除）</summary>
        public void AddTempEnchantment(int rank, Suit suit, int enchantmentId)
        {
            AddEnchantment(rank, suit, enchantmentId, allowBonus: false);

            string key = GetCardKey(rank, suit);
            if (!tempCardEnchantments.ContainsKey(key))
                tempCardEnchantments[key] = new List<int>();
            if (!tempCardEnchantments[key].Contains(enchantmentId))
                tempCardEnchantments[key].Add(enchantmentId);
        }

        /// <summary>这张牌是否有本场临时附魔</summary>
        public bool HasTempEnchantment(int rank, Suit suit)
            => tempCardEnchantments.TryGetValue(GetCardKey(rank, suit), out var l) && l.Count > 0;

        /// <summary>这张牌的临时附魔 id 列表（没有则返回 null）</summary>
        public List<int> GetTempEnchantments(int rank, Suit suit)
            => tempCardEnchantments.TryGetValue(GetCardKey(rank, suit), out var l) ? l : null;

        /// <summary>移除所有本场临时附魔（战斗结束 / 开始时调用）</summary>
        public void ClearTempEnchantments()
        {
            foreach (var kvp in tempCardEnchantments)
            {
                int us = kvp.Key.IndexOf('_');
                if (us <= 0) continue;
                if (!System.Enum.TryParse(kvp.Key.Substring(0, us), out Suit suit)) continue;
                if (!int.TryParse(kvp.Key.Substring(us + 1), out int rank)) continue;

                foreach (var id in kvp.Value)
                    RemoveEnchantment(rank, suit, id);
            }
            tempCardEnchantments.Clear();
        }

        // ===== 本场战斗「污染」（Boss 深渊之主）：被污染的牌附魔全部失效、参与牌型伤害 -1、打出后解除 =====

        private readonly HashSet<string> pollutedCardKeys = new HashSet<string>();

        /// <summary>某张牌是否被污染（按物理牌 rank_suit 判定）</summary>
        public bool IsPolluted(int rank, Suit suit) => pollutedCardKeys.Contains(GetCardKey(rank, suit));

        public int PollutedCount => pollutedCardKeys.Count;

        public void Pollute(int rank, Suit suit)
        {
            if (rank <= 0) return;
            pollutedCardKeys.Add(GetCardKey(rank, suit));
        }

        /// <summary>解除某张牌的污染</summary>
        public void CleansePollution(int rank, Suit suit) => pollutedCardKeys.Remove(GetCardKey(rank, suit));

        /// <summary>清空所有污染（战斗开始 / 结束）</summary>
        public void ClearPollution() => pollutedCardKeys.Clear();

        /// <summary>牌组里是否有某张牌拥有指定附魔</summary>
        public bool HasAnyCardWithEnchant(int enchantmentId)
        {
            if (enchantmentId <= 0) return false;
            foreach (var kvp in cardEnchantmentIds)
                if (kvp.Value != null && kvp.Value.Contains(enchantmentId)) return true;
            return false;
        }

        /// <summary>
        /// 获取所有已附魔的牌的 key 列表
        /// </summary>
        public List<string> GetEnchantedCardKeys()
        {
            var keys = new List<string>();
            foreach (var kvp in cardEnchantmentIds)
            {
                if (kvp.Value.Count > 0)
                    keys.Add(kvp.Key);
            }
            return keys;
        }

        /// <summary>
        /// 获取所有卡牌的附魔副本（用于存档/传递）
        /// </summary>
        public Dictionary<string, List<int>> GetAllCardEnchantmentsCopy()
        {
            var copy = new Dictionary<string, List<int>>();
            foreach (var kvp in cardEnchantmentIds)
            {
                copy[kvp.Key] = new List<int>(kvp.Value);
            }
            return copy;
        }

        // === 遗物操作 ===

        /// <summary>是否已拥有该遗物</summary>
        public bool HasRelic(int relicId) => _relicIds.Contains(relicId);

        public bool TryAddRelic(int relicId)        {
            if (_relicIds.Contains(relicId)) return false;
            if (_relicIds.Count >= RelicSystem.MaxSlots) return false;
            _relicIds.Add(relicId);
            CodexData.DiscoverRelic(relicId);   // 图鉴
            ApplyRelicAcquireEffects(relicId);   // 应用 OnAcquire 效果（如 神之血 +10 最大生命）
            OnRelicsChanged?.Invoke();
            return true;
        }

        /// <summary>应用遗物的「获得时」效果（trigger = OnAcquire）</summary>
        public void ApplyRelicAcquireEffects(int relicId)
        {
            var relic = ConfigLoader.GetRelic(relicId);
            if (relic == null || relic.effects == null) return;

            foreach (var eff in relic.effects)
            {
                if (eff.trigger != "OnAcquire") continue;
                int v = ToInt(eff.value);
                switch (eff.type)
                {
                    case "AddGold":
                        Gold += v;
                        break;
                    case "AddMaxHp":
                    case "AddMaxHpPermanent":
                        MaxHp += v;
                        CurrentHp = Mathf.Min(CurrentHp, MaxHp);
                        break;
                }
            }
        }

        private static int ToInt(object value)
        {
            if (value == null) return 0;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (value is float f) return (int)f;
            if (int.TryParse(value.ToString(), out var p)) return p;
            return 0;
        }

        public bool TryRemoveRelic(int relicId)
        {
            if (_relicIds.Remove(relicId))
            {
                OnRelicsChanged?.Invoke();
                return true;
            }
            return false;
        }

        public bool TryReplaceRelic(int oldRelicId, int newRelicId)
        {
            if (!_relicIds.Contains(oldRelicId)) return false;
            _relicIds.Remove(oldRelicId);
            _relicIds.Add(newRelicId);
            CodexData.DiscoverRelic(newRelicId);   // 图鉴
            ApplyRelicAcquireEffects(newRelicId);
            OnRelicsChanged?.Invoke();
            return true;
        }

        // === 药水操作 ===

        /// <summary>药水槽基础上限</summary>
        public const int MaxPotions = 3;

        /// <summary>药水槽实际上限（基础 3 + 遗物「AddPotionSlot」提供的额外槽）</summary>
        public int PotionSlotCap
        {
            get
            {
                int cap = MaxPotions;
                foreach (int id in _relicIds)
                {
                    var r = ConfigLoader.GetRelic(id);
                    if (r == null || r.effects == null) continue;
                    foreach (var e in r.effects)
                        if (e.type == "AddPotionSlot") cap += ToInt(e.value);
                }
                return cap;
            }
        }

        /// <summary>获得药水（允许同种药水重复持有）</summary>
        public bool TryAddPotion(int potionId)
        {
            if (_potionIds.Count >= PotionSlotCap) return false;
            _potionIds.Add(potionId);
            CodexData.DiscoverPotion(potionId);   // 图鉴
            OnPotionsChanged?.Invoke();
            return true;
        }

        public bool TryRemovePotion(int potionId)
        {
            if (_potionIds.Remove(potionId))
            {
                OnPotionsChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>用新药水替换掉某个已有药水（槽满时购买用；允许同种药水重复）</summary>
        public bool TryReplacePotion(int oldPotionId, int newPotionId)
        {
            if (!_potionIds.Contains(oldPotionId)) return false;

            _potionIds.Remove(oldPotionId);
            _potionIds.Add(newPotionId);
            CodexData.DiscoverPotion(newPotionId);   // 图鉴
            OnPotionsChanged?.Invoke();
            return true;
        }

        // === 花色命运映射 ===

        /// <summary>
        /// 获取某花色对应的下一事件 ID（0 = 未分配）
        /// </summary>
        public int GetSuitEventId(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spade: return spadeEventId;
                case Suit.Heart: return heartEventId;
                case Suit.Club: return clubEventId;
                case Suit.Diamond: return diamondEventId;
                default: return 0;
            }
        }

        public void SetSuitEventId(Suit suit, int eventId)
        {
            switch (suit)
            {
                case Suit.Spade: spadeEventId = eventId; break;
                case Suit.Heart: heartEventId = eventId; break;
                case Suit.Club: clubEventId = eventId; break;
                case Suit.Diamond: diamondEventId = eventId; break;
            }
        }

        /// <summary>
        /// 该花色对应的事件是否被隐藏（显示为「？」）
        /// </summary>
        public bool IsSuitHidden(Suit suit) => hiddenSuitIndices.Contains((int)suit);

        /// <summary>
        /// 每局开始调用：为四个花色各随机分配一个该主题下的事件
        /// 并随机隐藏其中 1~2 个花色的事件
        /// </summary>
        public void GenerateSuitEventMap(int seed)
        {
            var rng = new System.Random(seed);
            spadeEventId = PickEventForTheme("Spade", rng);
            heartEventId = PickEventForTheme("Heart", rng);
            clubEventId = PickEventForTheme("Club", rng);
            diamondEventId = PickEventForTheme("Diamond", rng);

            // 随机隐藏 1~2 个花色
            hiddenSuitIndices.Clear();
            var suits = new List<int>
            {
                (int)Suit.Spade, (int)Suit.Heart, (int)Suit.Club, (int)Suit.Diamond
            };
            for (int i = suits.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = suits[i];
                suits[i] = suits[j];
                suits[j] = tmp;
            }
            int hiddenCount = rng.Next(1, 3); // 1 或 2
            for (int i = 0; i < hiddenCount && i < suits.Count; i++)
                hiddenSuitIndices.Add(suits[i]);

            // 商店位置：每场随机落在四个花色之一。
            // 非保底时该位置正常参与隐藏（可能显示 ？？？），保底时在 UI 上强制揭晓为商店。
            shopSuitIndex = rng.Next(4);
        }

        private int PickEventForTheme(string theme, System.Random rng)
        {
            var pool = ConfigLoader.GetEventsByTheme(theme)?.FindAll(MeetsCondition);
            if (pool != null && pool.Count > 0)
                return pool[rng.Next(pool.Count)].id;

            // 该主题没有可用事件时，从全部满足条件的事件里取
            var all = ConfigLoader.Config?.events?.FindAll(MeetsCondition);
            if (all == null || all.Count == 0) return 0;
            return all[rng.Next(all.Count)].id;
        }

        /// <summary>事件出现条件是否满足（condition 留空 = 无条件）</summary>
        private bool MeetsCondition(Roguelike.Data.EventData ev)
        {
            if (ev == null) return false;

            // 一局只出现一次的事件：已经出现过就不再进入池子
            if (ev.once && visitedEventIds.Contains(ev.id)) return false;

            if (string.IsNullOrEmpty(ev.condition)) return true;

            switch (ev.condition)
            {
                case "HasMainDestiny": return HasMainDestiny;
                default: return true;
            }
        }
    }

    /// <summary>
    /// 战斗奖励数据
    /// </summary>
    [Serializable]
    public class CombatReward
    {
        public int gold;
        public List<EnchantmentRewardOption> enchantmentOptions = new List<EnchantmentRewardOption>();
        public int relicId;           // 精英/Boss 掉落遗物
        public int potionId;          // 掉落药水
    }

    /// <summary>
    /// 附魔奖励选项（三选一）
    /// </summary>
    [Serializable]
    public class EnchantmentRewardOption
    {
        public string cardKey;        // 目标牌 key，如 "Spade_14"
        public string cardDisplayName; // 显示名
        public int enchantmentId;     // 附魔 ID
    }
}