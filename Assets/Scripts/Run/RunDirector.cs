using System;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 运行总监：单例，管理整局游戏的流程、状态
    /// DontDestroyOnLoad，跨场景持久化
    /// </summary>
    public class RunDirector : MonoBehaviour
    {
        public static RunDirector Instance { get; private set; }

        [Header("引用")]
        public UIManager uiManager;

        // 运行时创建
        public BattleManager BattleManager { get; private set; }

        // 核心数据
        public RunData RunData { get; private set; }

        /// <summary>事件系统（UI 取选项展示文本等）</summary>
        public EventSystem Events => eventSystem;

        // 系统
        private RewardSystem rewardSystem;
        private RelicSystem relicSystem;
        private EventSystem eventSystem;
        private ShopSystem shopSystem;

        // 当前战斗奖励缓存
        private CombatReward currentCombatReward;

        // 事件专属战斗：打完后要展示的事件结算
        private EventOutcome pendingEventOutcome;

        // 当前正在展示的事件（多阶段事件失败后要重新打开它）
        private Roguelike.Data.EventData currentEvent;

        // 当前是否在事件专属战斗中（不结算命格、无普通奖励、隐藏命运花色）
        private bool inEventBattle;

        /// <summary>当前是否为事件专属战斗</summary>
        public bool InEventBattle => inEventBattle;

        // 战斗进度配置
        [Serializable]
        public class BattleProgressionConfig
        {
            public int totalBattles = 7;                    // 打几场进 Boss
            public string commonPool = "act1_common";       // 普通怪池
            public string elitePool = "act1_elite";         // 精英怪池
            public float[] eliteChancePerBattle = new float[] 
            { 
                0f,    // 第1关：0% 必普通
                0f,    // 第2关：0% 必普通  
                0.2f,  // 第3关：20% 精英
                0.35f, // 第4关：35%
                0.5f,  // 第5关：50%
                0.7f,  // 第6关：70%
                0.9f   // 第7关：90%
            };
        }

        public BattleProgressionConfig progression;

        // 运行时状态
        private int battleCount = 0;        // 当前第几场战斗

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Roguelike.Data.ConfigLoader.LoadAll();
            rewardSystem = new RewardSystem();
            relicSystem = new RelicSystem();
            eventSystem = new EventSystem();
            shopSystem = new ShopSystem();

            // 尽早初始化 uiManager，防止 Start 前被调用
            if (uiManager == null)
                uiManager = UIManager.Instance;
        }

        private void Start()
        {
            if (uiManager == null)
                uiManager = UIManager.Instance;
        }

        public void StartNewRun(int seed = 0)
        {
            RunData = new RunData
            {
                seed = seed != 0 ? seed : (int)DateTime.Now.Ticks,
                battleIndex = 0,
                actId = 1,
                maxHp = 60,
                currentHp = 60,
                gold = 0
            };

            RunData.cardEnchantmentIds.Clear();
            RunData.relicIds.Clear();
            RunData.potionIds.Clear();
            RunData.visitedEventIds.Clear();
            RunData.unlockedContent.Clear();

            // 重置事件战斗状态
            inEventBattle = false;
            pendingEventOutcome = null;

            // 初始化遗物系统
            relicSystem.Initialize(RunData);

            // 花色命运：每局随机分配「花色 → 事件」
            RunData.GenerateSuitEventMap(RunData.seed);
            Debug.Log($"[RunDirector] 花色命运映射: ♠={RunData.spadeEventId} ♥={RunData.heartEventId} ♣={RunData.clubEventId} ♦={RunData.diamondEventId}");

            UnityEngine.Random.InitState(RunData.seed);

            // 重置战斗计数
            battleCount = 0;

            // 调试：打印实际加载的数组值
            if (progression != null && progression.eliteChancePerBattle != null)
            {
                string arrStr = string.Join(", ", progression.eliteChancePerBattle);
                Debug.Log($"[RunDirector] 加载的精英概率数组: [{arrStr}]");
            }
            else
            {
                Debug.LogError("[RunDirector] progression 或 eliteChancePerBattle 为 null！");
            }

            Debug.Log($"[RunDirector] 新游戏开始 Seed={RunData.seed}");

            // 测试：给所有 8 点数牌加 "烈火" (ID=1)
            //RunData.AddEnchantment(8, Suit.Heart, 65);
            //RunData.AddEnchantment(8, Suit.Club, 65);
            //RunData.AddEnchantment(8, Suit.Diamond, 65);
            //RunData.AddEnchantment(8, Suit.Spade, 65);

            // 测试：给一个遗物
            //relicSystem.TryAddRelic(1, out _); // 赌徒护腕
            //relicSystem.TryAddRelic(5, out _);

            // 进入第一场
            EnterNextBattle();
        }

        public void ContinueRun(RunData data)
        {
            RunData = data;
            UnityEngine.Random.InitState(RunData.seed);

            // 恢复战斗计数
            battleCount = RunData.battleIndex;

            Debug.Log($"[RunDirector] 继续游戏 BattleIndex={RunData.battleIndex} Act={RunData.actId}");

            EnterNextBattle();
        }

        /// <summary>
        /// 进入下一场战斗
        /// </summary>
        /// <summary>
        /// 当前章节的战斗配置：acts.json 里配了就用它的，没配则回落到 Inspector 的 progression。
        /// </summary>
        private void GetActBattleConfig(out int totalBattles, out string commonPool, out string elitePool, out float[] eliteChances)
        {
            totalBattles = progression != null ? progression.totalBattles : 7;
            commonPool = progression != null ? progression.commonPool : "act1_common";
            elitePool = progression != null ? progression.elitePool : "act1_elite";
            eliteChances = (progression != null && progression.eliteChancePerBattle != null && progression.eliteChancePerBattle.Length > 0)
                ? progression.eliteChancePerBattle
                : new float[] { 0f };

            var act = Roguelike.Data.ConfigLoader.GetAct(RunData.actId);
            if (act == null) return;

            if (act.totalBattles > 0) totalBattles = act.totalBattles;
            if (!string.IsNullOrEmpty(act.commonPool)) commonPool = act.commonPool;
            if (!string.IsNullOrEmpty(act.elitePool)) elitePool = act.elitePool;
            if (act.eliteChancePerBattle != null && act.eliteChancePerBattle.Count > 0)
                eliteChances = act.eliteChancePerBattle.ToArray();
        }

        /// <summary>后半段的普通怪池（acts.json 没配就返回 null，沿用前半）</summary>
        private string GetCommonPoolLate()
        {
            var act = Roguelike.Data.ConfigLoader.GetAct(RunData.actId);
            if (act == null || string.IsNullOrEmpty(act.commonPoolLate)) return null;
            return act.commonPoolLate;
        }

        /// <summary>池里有没有东西（固定组合或敌人）——没配就别切过去，否则开不了战斗</summary>
        private static bool PoolHasContent(string pool)
        {
            if (string.IsNullOrEmpty(pool)) return false;
            if (Roguelike.Data.ConfigLoader.GetEncountersByPool(pool).Count > 0) return true;
            return Roguelike.Data.ConfigLoader.GetEnemiesByPool(pool).Count > 0;
        }

        public void EnterNextBattle()
        {
            if (RunData == null) return;

            battleCount++;

            // 每场战斗重新随机「花色 → 事件」映射与隐藏花色（预览为本场命运）
            RunData.GenerateSuitEventMap(RunData.seed + battleCount * 131);
            Debug.Log($"[RunDirector] 花色命运映射(第{battleCount}战): ♠={RunData.spadeEventId} ♥={RunData.heartEventId} ♣={RunData.clubEventId} ♦={RunData.diamondEventId}");

            GetActBattleConfig(out int totalBattles, out string commonPool, out string elitePool, out float[] eliteChances);

            Debug.Log($"[RunDirector] EnterNextBattle: 第{RunData.actId}章 battleCount={battleCount}, totalBattles={totalBattles}");

            // 打完指定场数 → 进 Boss
            if (battleCount > totalBattles)
            {
                Debug.Log($"[RunDirector] battleCount ({battleCount}) >= totalBattles ({totalBattles})，进入 Boss 战");
                EnterBossBattle();
                return;
            }

            // 安全检查：数组越界保护
            if (eliteChances == null || eliteChances.Length == 0)
            {
                Debug.LogError("[RunDirector] eliteChancePerBattle 数组为空！使用默认 0%");
                eliteChances = new float[] { 0f };
            }

            int index = Mathf.Clamp(battleCount - 1, 0, eliteChances.Length - 1);
            float chance = Mathf.Clamp01(eliteChances[index]);

            // 1.0f 时强制精英（Random.value 是 [0,1)，小于 1.0 总为真；为防万一显式处理）
            bool isElite = (chance >= 1f) || (UnityEngine.Random.value < chance);

            // 后半段换普通怪池（精英怪池不分区段）
            bool secondHalf = battleCount > totalBattles / 2;
            string pool = isElite ? elitePool : commonPool;
            if (!isElite && secondHalf)
            {
                string late = GetCommonPoolLate();
                if (!string.IsNullOrEmpty(late))
                {
                    if (PoolHasContent(late)) pool = late;
                    else Debug.LogWarning($"[RunDirector] 后半池 {late} 还没配任何敌人/组合，本场沿用 {pool}");
                }
            }

            Debug.Log($"[RunDirector] 第 {battleCount}/{totalBattles} 场（{(secondHalf ? "后半" : "前半")}），数组索引={index}，精英概率={chance:P0} → {(isElite ? "精英" : "普通")} (池:{pool})");

            // 更新 RunData 索引（用于存档）
            RunData.battleIndex = battleCount;

            StartCombat(pool, isElite);
        }

        /// <summary>Boss 战胜利：有下一章就继续，否则通关</summary>
        private void OnBossDefeated()
        {
            int nextAct = RunData.actId + 1;
            var next = Roguelike.Data.ConfigLoader.GetAct(nextAct);

            if (next == null)
            {
                Debug.Log("[RunDirector] 已是最后一章，通关");
                ShowVictory();
                return;
            }

            RunData.actId = nextAct;
            battleCount = 0;
            Debug.Log($"[RunDirector] 进入第 {nextAct} 章：{next.name}");
            EnterNextBattle();
        }

        private void StartCombat(string enemyPool, bool isElite)
        {
            var encounter = BuildEncounter(enemyPool, isElite);
            if (encounter == null || encounter.Count == 0)
            {
                Debug.LogError($"[RunDirector] Enemy pool {enemyPool} empty");
                return;
            }

            // 应用敌人强化（作用于本场所有敌人）
            if (RunData.nextBattleEnemyBuff > 0)
            {
                int lv = RunData.nextBattleEnemyBuff;
                RunData.nextBattleEnemyBuff = 0;
                for (int i = 0; i < encounter.Count; i++)
                    encounter[i] = ApplyEnemyBuff(encounter[i], lv);
            }

            // 应用敌人弱化（作用于本场所有敌人）
            if (RunData.nextBattleEnemyDebuff > 0)
            {
                int lv = RunData.nextBattleEnemyDebuff;
                RunData.nextBattleEnemyDebuff = 0;
                for (int i = 0; i < encounter.Count; i++)
                    encounter[i] = ApplyEnemyDebuff(encounter[i], lv);
            }

            // 应用开局血量修正
            int playerStartHp = RunData.currentHp;
            if (RunData.nextBattleStartHpMod != 0)
            {
                playerStartHp = Mathf.Clamp(playerStartHp + RunData.nextBattleStartHpMod, 1, RunData.maxHp);
                RunData.nextBattleStartHpMod = 0;
            }

            // 写回 RunData，供 BattleManager 使用
            RunData.currentHp = playerStartHp;

            Debug.Log($"[RunDirector] 战斗开始: {string.Join("、", encounter.ConvertAll(e => e.name))} (精英={isElite})");

            CreateBattleManager(encounter, RunData, isElite);
        }

        /// <summary>
        /// 本场遭遇：优先用 encounters.json 里该池的固定组合（加权随机抽一个），
        /// 池里没配就回落到「随机 1~3 只 / 精英 1~2 只」。
        /// </summary>
        private List<EnemyData> BuildEncounter(string enemyPool, bool isElite)
        {
            var picked = Roguelike.Data.ConfigLoader.PickEncounterByPool(enemyPool);
            if (picked != null)
            {
                var resolved = Roguelike.Data.ConfigLoader.ResolveEncounter(picked);
                if (resolved.Count > 0)
                {
                    Debug.Log($"[RunDirector] 遭遇「{picked.name}」: {string.Join("、", resolved.ConvertAll(e => e.name))}");
                    return resolved;
                }
                Debug.LogWarning($"[RunDirector] 遭遇「{picked.name}」没有有效敌人，回落到随机");
            }

            var enemies = Roguelike.Data.ConfigLoader.GetEnemiesByPool(enemyPool);
            if (enemies == null || enemies.Count == 0) return new List<EnemyData>();

            int maxCount = isElite ? 2 : 3;
            int count = UnityEngine.Random.Range(1, maxCount + 1);

            var encounter = new List<EnemyData>();
            for (int i = 0; i < count; i++)
                encounter.Add(enemies[UnityEngine.Random.Range(0, enemies.Count)]);

            Debug.Log($"[RunDirector] 池 {enemyPool} 没有配置固定组合，随机 {count} 只");
            return encounter;
        }

        private void CreateBattleManager(List<EnemyData> enemyList, RunData runData, bool isElite)
        {
            // 创建新的 BattleManager
            BattleManager = new BattleManager();
            BattleManager.InitBattleWithData(enemyList, runData, isElite, relicSystem);

            // 订阅战斗结束事件
            BattleManager.OnBattleOver += OnBattleOver;

            // 显示战斗面板
            if (uiManager != null)
            {
                var battlePanel = uiManager.ShowPanel<BattlePanel>();
                if (battlePanel != null)
                    battlePanel.SetBattleManager(BattleManager);
                uiManager.Hide<FatePanel>();
                uiManager.Hide<EventResultPanel>();
                uiManager.Hide<CardModifierPanel>();
                uiManager.Hide<DestinyPanel>();
                uiManager.Hide<ShopPanel>();
            }
        }

        private void OnBattleOver(bool isWin)
        {
            Debug.Log($"[RunDirector] OnBattleOver: isWin={isWin}, isBossBattle={RunData.isBossBattle}");

            // 取消订阅
            if (BattleManager != null)
            {
                // 保存玩家当前血量到 RunData（用于下一场战斗）
                if (BattleManager.GetPlayer() != null)
                {
                    RunData.currentHp = BattleManager.GetPlayer().CurrentHp;
                }
                BattleManager.OnBattleOver -= OnBattleOver;
            }

            // 事件挑战结算：本场失去生命 ≤ 上限 则挑战成功
            if (RunData.challengeHpLimit >= 0)
            {
                int limit = RunData.challengeHpLimit;
                int lost = BattleManager != null ? BattleManager.PlayerHpLostThisBattle : 0;
                bool ok = lost <= limit;

                RunData.challengeHpLimit = -1;
                var winResults = RunData.challengeWin;
                var loseResults = RunData.challengeLose;
                RunData.challengeWin = null;
                RunData.challengeLose = null;

                Debug.Log($"[挑战] 本场失血 {lost}/{limit} → {(ok ? "成功" : "失败")}");
                var co = eventSystem.ExecuteResults(ok ? winResults : loseResults, RunData, relicSystem);
                if (co != null)
                {
                    co.messages.Insert(0, ok
                        ? $"挑战成功（失去 {lost} ≤ {limit} 点生命）"
                        : $"挑战失败（失去 {lost} > {limit} 点生命）");
                }

                if (co != null && !co.IsEmpty && uiManager != null)
                {
                    // 不隐藏战斗面板：结算面板自带全屏遮罩，直接覆盖在上面即可
                    var challengePanel = uiManager.ShowPanel<EventResultPanel>();
                    if (challengePanel != null)
                    {
                        challengePanel.ShowOutcome(co, () => HandleBattleOver(isWin));
                        return;
                    }
                }
            }

            HandleBattleOver(isWin);
        }

        /// <summary>战斗结束主流程（挑战结算之后）</summary>
        private void HandleBattleOver(bool isWin)
        {
            if (isWin)
            {
                // 事件专属战斗：不结算命格、不给普通奖励，直接展示事件结算
                if (inEventBattle)
                {
                    inEventBattle = false;

                    var evOutcome = pendingEventOutcome;
                    pendingEventOutcome = null;

                    int bonusId = RunData.nextBattleBonusRelicId;
                    RunData.nextBattleBonusRelicId = 0;

                    if (bonusId > 0)
                    {
                        // 事件奖励遗物：槽满时让玩家选择替换 / 丢弃
                        AcquireRelic(bonusId, () => ShowEventOutcome(evOutcome));
                        return;
                    }

                    ShowEventOutcome(evOutcome);
                    return;
                }

                ApplyDestinyOnWin();

                // 生成战斗奖励（金币 = 本场每个敌人各滚一次之和；isBoss 取本场敌人里是否有 boss）
                var isElite = BattleManager != null && BattleManager.IsElite;
                var enemyDatas = BattleManager != null ? new List<EnemyData>(BattleManager.GetEnemyDatas()) : null;
                var isBoss = RunData.isBossBattle || (enemyDatas != null && enemyDatas.Exists(e => e != null && e.isBoss));
                Debug.Log($"[RunDirector] Generating reward: isElite={isElite}, isBoss={isBoss}, 敌人={enemyDatas?.Count ?? 0}");
                currentCombatReward = rewardSystem.GenerateCombatReward(RunData, isElite, isBoss, enemyDatas);

                // 显示奖励选择面板（遗物 + 附魔三选一 + 金币）
                if (uiManager != null)
                {
                    var panel = uiManager.ShowPanel<RewardPanel>();
                    if (panel != null)
                        panel.ShowCombatReward(currentCombatReward, OnRewardConfirmed);
                    else
                        Debug.LogError("[RunDirector] RewardPanel is null!");
                }
                else
                {
                    // 没有 UI 面板时直接应用奖励
                    Debug.Log("[RunDirector] No uiManager, calling OnRewardConfirmed directly");
                    OnRewardConfirmed(currentCombatReward);
                }
            }
            else
            {
                OnBattleLose();
            }
        }

        /// <summary>战斗胜利后结算命格：命格值 +1、确立主命格、结算战斗结束类被动</summary>
        private void ApplyDestinyOnWin()
        {
            if (RunData == null || BattleManager == null) return;

            var suit = BattleManager.GetDominantSuit();

            // 命格值 +1（AddDestinyPoint 内部会处理「第一个到 Lv.1 的花色成为主命格」）
            bool hadMain = RunData.HasMainDestiny;
            RunData.AddDestinyPoint(suit, 1);
            if (!hadMain && RunData.HasMainDestiny)
                Debug.Log($"[命格] 主命格确立：{DestinyInfo.SuitName((Suit)RunData.mainDestinySuit)}");

            // 命格：战斗结束类被动（红桃 Lv1 回血 / 方块 Lv3 金币）——数值集中在 DestinyEffects
            DestinyEffects.ApplyBattleWin(RunData);

            // 遗物「四色祭仪」：其余三花色与命运花色的出牌数差值都 ≤3 → 额外 +1 命格值
            if (relicSystem != null && relicSystem.GetFlatBonus("FourColorRite", 0) > 0)
            {
                int dom = (int)suit;
                int domCount = BattleManager.GetSuitCount(suit);
                bool ok = true;
                for (int i = 0; i < 4; i++)
                {
                    if (i == dom) continue;
                    if (Mathf.Abs(BattleManager.GetSuitCount((Suit)i) - domCount) > 3) { ok = false; break; }
                }
                if (ok)
                {
                    RunData.AddDestinyPoint(suit, 1);
                    Debug.Log("[遗物] 四色祭仪：额外 +1 命格值");
                }
            }

            // 遗物「速通者」：两回合内结束战斗 +10 金币
            if (relicSystem != null && relicSystem.GetFlatBonus("FastWinGold", 0) > 0 && BattleManager.TurnsElapsed <= 2)
            {
                int g = relicSystem.GetFlatBonus("FastWinGold", 0);
                RunData.Gold += g;
                Debug.Log($"[遗物] 速通者：+{g} 金币");
            }

            RunData.NotifyDestinyChanged();
            Debug.Log($"[命格] 命运花色={DestinyInfo.SuitName(suit)} 命格值={RunData.destinyPoints[(int)suit]} 等级={RunData.GetDestinyRank(suit)}");
        }

        private static int GetInt(object value)
        {
            if (value == null) return 0;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (value is float f) return (int)f;
            if (int.TryParse(value.ToString(), out var p)) return p;
            return 0;
        }

        /// <summary>深拷贝敌人（避免修改到缓存的配置）</summary>
        private static EnemyData CloneEnemy(EnemyData src)
        {
            var e = new EnemyData
            {
                id = src.id,
                name = src.name,
                image = src.image,
                frames = new System.Collections.Generic.List<string>(src.frames),
                frameRate = src.frameRate,
                imageWidth = src.imageWidth,
                imageHeight = src.imageHeight,
                imageScale = src.imageScale,
                imageOffsetX = src.imageOffsetX,
                imageOffsetY = src.imageOffsetY,
                hp = src.hp,
                maxHp = src.maxHp,
                goldRange = new System.Collections.Generic.List<int>(src.goldRange),
                relicDrop = src.relicDrop,
                bossRelic = src.bossRelic,
                isBoss = src.isBoss,
                pools = new System.Collections.Generic.List<string>(src.pools),
                intents = new System.Collections.Generic.List<IntentData>()
            };
            foreach (var it in src.intents)
            {
                e.intents.Add(new IntentData
                {
                    type = it.type,
                    value = it.value,
                    multiHit = it.multiHit,
                    hitCount = it.hitCount,
                    description = it.description,
                    weight = it.weight,
                    status = it.status,
                    duration = it.duration
                });
            }
            return e;
        }

        private EnemyData ApplyEnemyBuff(EnemyData baseEnemy, int buffLevel)
        {
            var buffed = CloneEnemy(baseEnemy);
            buffed.name = baseEnemy.name + $" 强化{buffLevel}";
            buffed.hp = Mathf.RoundToInt(baseEnemy.hp * (1 + buffLevel * 0.2f));
            buffed.maxHp = Mathf.RoundToInt(baseEnemy.maxHp * (1 + buffLevel * 0.2f));

            foreach (var intent in buffed.intents)
            {
                intent.value = Mathf.RoundToInt(intent.value * (1 + buffLevel * 0.15f));
            }
            buffed.goldRange[0] = Mathf.RoundToInt(buffed.goldRange[0] * (1 + buffLevel * 0.25f));
            buffed.goldRange[1] = Mathf.RoundToInt(buffed.goldRange[1] * (1 + buffLevel * 0.25f));

            return buffed;
        }

        /// <summary>敌人弱化：意图数值与血量按级数降低</summary>
        private EnemyData ApplyEnemyDebuff(EnemyData baseEnemy, int level)
        {
            var debuffed = CloneEnemy(baseEnemy);
            debuffed.name = baseEnemy.name + $" 虚弱{level}";
            debuffed.hp = Mathf.Max(1, Mathf.RoundToInt(baseEnemy.hp * (1 - level * 0.15f)));
            debuffed.maxHp = Mathf.Max(1, Mathf.RoundToInt(baseEnemy.maxHp * (1 - level * 0.15f)));

            foreach (var intent in debuffed.intents)
            {
                intent.value = Mathf.Max(1, Mathf.RoundToInt(intent.value * (1 - level * 0.15f)));
            }

            return debuffed;
        }

        private void EnterBossBattle()
        {
            var act = Roguelike.Data.ConfigLoader.GetAct(RunData.actId);
            if (act == null)
            {
                Debug.LogError($"[RunDirector] EnterBossBattle: Act {RunData.actId} not found!");
                return;
            }

            var bossId = act.bossEnemyId;
            var bossEnemy = Roguelike.Data.ConfigLoader.GetEnemy(bossId);

            // Boss 也可以配固定组合（池 act{N}_boss，例如 Boss + 小弟）；没配就用单个 Boss
            var bossEncounter = Roguelike.Data.ConfigLoader.PickEncounterByPool($"act{act.actId}_boss");
            List<EnemyData> bossList = null;
            if (bossEncounter != null)
            {
                bossList = Roguelike.Data.ConfigLoader.ResolveEncounter(bossEncounter);
                if (bossList.Count > 0)
                    Debug.Log($"[RunDirector] Boss 遭遇「{bossEncounter.name}」: {string.Join("、", bossList.ConvertAll(e => e.name))}");
            }

            if (bossList == null || bossList.Count == 0)
            {
                if (bossEnemy == null)
                {
                    Debug.LogError($"[RunDirector] EnterBossBattle: Boss enemy {bossId} not found!");
                    return;
                }
                Debug.Log($"[RunDirector] 进入 Boss 战: {bossEnemy.name} (ID={bossId})");
                bossList = new List<EnemyData> { bossEnemy };
            }

            RunData.isBossBattle = true;
            CreateBattleManager(bossList, RunData, false);
        }

        // 奖励确认回调（包含金币/遗物/药水/附魔的完整奖励）
        /// <summary>
        /// 获得遗物：槽位未满直接加入；已拥有折算金币；槽位已满则弹「替换 / 丢弃」面板，选完再继续。
        /// </summary>
        public void AcquireRelic(int relicId, Action onDone)
        {
            var relic = ConfigLoader.GetRelic(relicId);
            if (relic == null) { onDone?.Invoke(); return; }

            // 已拥有 → 直接跳过（不折算金币）
            if (RunData.HasRelic(relicId))
            {
                Debug.Log($"[RunDirector] 已拥有 {relic.name}，跳过");
                onDone?.Invoke();
                return;
            }

            // 槽位未满 → 直接加入
            if (RunData.TryAddRelic(relicId))
            {
                Debug.Log($"[RunDirector] 获得遗物：{relic.name}");
                onDone?.Invoke();
                return;
            }

            // 槽位已满 → 让玩家选择替换或丢弃
            if (uiManager == null) { onDone?.Invoke(); return; }

            var panel = uiManager.ShowPanel<ReplacePickerPanel>();
            if (panel == null) { onDone?.Invoke(); return; }

            panel.Show("遗物槽已满",
                relic.name, relic.description, RarityUtil.ColorHex(relic.rarity),
                ReplacePickerPanel.BuildRelicEntries(RunData),
                replacedId =>
                {
                    if (replacedId > 0 && RunData.TryReplaceRelic(replacedId, relicId))
                    {
                        var old = ConfigLoader.GetRelic(replacedId);
                        Debug.Log($"[RunDirector] 遗物替换：{old?.name} → {relic.name}");
                    }
                    onDone?.Invoke();
                },
                () =>
                {
                    Debug.Log($"[RunDirector] 丢弃遗物：{relic.name}");
                    onDone?.Invoke();
                },
                "丢弃新遗物");
        }

        /// <summary>依次获得多个遗物（槽满时逐个让玩家选择替换 / 丢弃）</summary>
        public void AcquireRelics(List<int> relicIds, Action onDone)
        {
            if (relicIds == null || relicIds.Count == 0) { onDone?.Invoke(); return; }

            AcquireRelic(relicIds[0], () =>
            {
                if (relicIds.Count <= 1) { onDone?.Invoke(); return; }
                AcquireRelics(relicIds.GetRange(1, relicIds.Count - 1), onDone);
            });
        }

        /// <summary>结算事件里因遗物槽满而挂起的遗物（逐个选择替换 / 丢弃）</summary>
        private void ResolvePendingRelics(EventOutcome outcome, Action onDone)
        {
            if (outcome == null || outcome.pendingRelicIds == null || outcome.pendingRelicIds.Count == 0)
            {
                onDone?.Invoke();
                return;
            }

            var ids = new List<int>(outcome.pendingRelicIds);
            outcome.pendingRelicIds.Clear();
            AcquireRelics(ids, onDone);
        }

        /// <summary>结算事件里因药水槽满而挂起的药水（逐个选择替换 / 丢弃）</summary>
        private void ResolvePendingPotions(EventOutcome outcome, Action onDone)
        {
            if (outcome == null || outcome.pendingPotionIds == null || outcome.pendingPotionIds.Count == 0)
            {
                onDone?.Invoke();
                return;
            }

            int potionId = outcome.pendingPotionIds[0];
            outcome.pendingPotionIds.RemoveAt(0);

            var potion = ConfigLoader.GetPotion(potionId);
            var panel = (potion != null && uiManager != null) ? uiManager.ShowPanel<ReplacePickerPanel>() : null;
            if (panel == null)
            {
                ResolvePendingPotions(outcome, onDone);
                return;
            }

            panel.Show("药水槽已满",
                potion.name, potion.description, "#7FE3A0",
                ReplacePickerPanel.BuildPotionEntries(RunData),
                replacedId =>
                {
                    if (RunData.TryReplacePotion(replacedId, potionId))
                        Debug.Log($"[RunDirector] 药水替换 → {potion.name}");
                    ResolvePendingPotions(outcome, onDone);
                },
                () => ResolvePendingPotions(outcome, onDone),
                "丢弃新药水");
        }

        public void OnRewardConfirmed(CombatReward reward)
        {            Debug.Log($"[RunDirector] OnRewardConfirmed: reward={reward}, isBossBattle={RunData.isBossBattle}");

            if (reward == null)
            {
                Debug.LogError("[RunDirector] OnRewardConfirmed: reward is null!");
                return;
            }

            // 发放金币（应用遗物金币倍率）
            int goldReward = reward.gold;
            if (relicSystem != null)
                goldReward = relicSystem.GainGold(goldReward);
            else
                RunData.Gold += goldReward;

            RunData.totalGoldGained += goldReward;

            // 发放遗物（奖励遗物 + 事件奖励遗物）：槽满时逐个让玩家选择替换 / 丢弃
            var relicIds = new List<int>();
            if (reward.relicId > 0) relicIds.Add(reward.relicId);
            if (RunData.nextBattleBonusRelicId > 0)
            {
                relicIds.Add(RunData.nextBattleBonusRelicId);
                RunData.nextBattleBonusRelicId = 0;
            }

            // 发放药水
            if (reward.potionId > 0)
            {
                if (RunData.potionIds.Count < 3)
                    RunData.potionIds.Add(reward.potionId);
                else
                    Debug.Log("[RunDirector] 药水槽已满，药水丢失");
            }

            // 发放附魔（RewardPanel 已经处理了选择，这里直接应用选中的）
            foreach (var opt in reward.enchantmentOptions)
            {
                rewardSystem.ApplyEnchantmentReward(RunData, opt);
            }

            Debug.Log($"[RunDirector] 奖励结算: 金币={goldReward}, 遗物={reward.relicId}, 药水={reward.potionId}, 附魔={reward.enchantmentOptions.Count}个");

            // 遗物发完后继续原流程（Boss 胜利 / 事件结算 / 下一场）
            AcquireRelics(relicIds, () => ContinueAfterReward());
        }

        /// <summary>奖励遗物发放完毕后的流程分支</summary>
        private void ContinueAfterReward()
        {
            // 如果是 Boss 战胜利：有下一章就继续，否则通关
            if (RunData.isBossBattle)
            {
                RunData.isBossBattle = false; // 重置标记
                OnBossDefeated();
                return;
            }

            // 事件专属战斗结算：展示事件结果面板（而不是继续走花色事件）
            if (pendingEventOutcome != null)
            {
                var outcome = pendingEventOutcome;
                pendingEventOutcome = null;
                ShowEventOutcome(outcome);
                return;
            }

            // 进入下一场
            Debug.Log("[RunDirector] 非 Boss 战，进入花色事件");
            EnterSuitEvent();
        }

        /// <summary>
        /// 根据本场战斗的主导花色进入对应事件
        /// </summary>
        private void EnterSuitEvent()
        {
            if (RunData == null || BattleManager == null)
            {
                EnterNextBattle();
                return;
            }

            // 本场的「命运一览不隐藏」到此消费完毕
            RunData.nextBattleRevealAll = false;

            // 主导花色：商店与事件都按它决定
            var suit = BattleManager.GetDominantSuit();

            // 商店只出现在 RunData.shopSuitIndex 对应的花色槽位：
            // - 保底（连续 4 场没进商店）：该槽位在「命运一览」里揭晓为「商店（保底）」，打出该花色必进
            // - 非保底：打出该花色时 10% 概率进商店
            bool guaranteed = RunData.battlesSinceShop >= 4;
            bool isShopSuit = (int)suit == RunData.shopSuitIndex;

            if (isShopSuit && (guaranteed || UnityEngine.Random.value < 0.1f))
            {
                if (OpenShop(EnterNextBattle))
                {
                    RunData.battlesSinceShop = 0;
                    Debug.Log(guaranteed
                        ? $"[RunDirector] 保底商店已开启（花色槽 {suit}）"
                        : $"[RunDirector] 命运事件刷出商店（花色槽 {suit}）");
                    return;
                }
                // 商店开启失败则继续本场命运事件
            }
            RunData.battlesSinceShop++;

            int eventId = RunData.GetSuitEventId(suit);
            var ev = Roguelike.Data.ConfigLoader.GetEvent(eventId);

            if (ev == null)
            {
                Debug.LogWarning($"[RunDirector] 主导花色 {suit} 未映射到事件(id={eventId})，直接进入下一战");
                EnterNextBattle();
                return;
            }

            Debug.Log($"[RunDirector] 花色命运: 主导花色={suit} → 事件「{ev.title}」(id={eventId})");

            // 多阶段事件：进入时重置阶段
            RunData.currentEventStage = 1;
            currentEvent = ev;

            // 记录已出现的事件（供 once 事件去重）
            if (!RunData.visitedEventIds.Contains(ev.id))
                RunData.visitedEventIds.Add(ev.id);

            if (uiManager != null)
            {
                uiManager.Hide<FatePanel>();
                uiManager.Hide<EventResultPanel>();
                uiManager.Hide<CardModifierPanel>();
                uiManager.Hide<DestinyPanel>();
                uiManager.Hide<ShopPanel>();
                uiManager.Hide<BattlePanel>();
                var panel = uiManager.ShowPanel<EventPanel>();
                if (panel != null)
                {
                    eventSystem.PrepareEvent(ev, RunData);   // 预抽可获得的具体物品（拍卖会等）
                    panel.ShowEvent(ev, idx => OnEventOptionChosen(ev, idx));
                    return;
                }
            }

            // 无 UI 时跳过事件
            EnterNextBattle();
        }

        /// <summary>调试：跳过当前战斗，直接进入指定事件</summary>
        /// <summary>调试用：中止当前战斗并直接进入下一场（按章节进度/精英概率抽池）</summary>
        public void DebugEnterNextBattle()
        {
            BattleManager?.AbortBattle();
            EnterNextBattle();
        }

        /// <summary>调试用：直接用指定敌人组开一场战斗（先中止当前战斗）</summary>
        public void DebugStartEncounter(int encounterId)
        {
            if (RunData == null)
            {
                Debug.LogWarning("[RunDirector] DebugStartEncounter: RunData 为空");
                return;
            }

            var enc = Roguelike.Data.ConfigLoader.GetEncounter(encounterId);
            if (enc == null)
            {
                Debug.LogWarning($"[RunDirector] DebugStartEncounter: 找不到敌人组 {encounterId}");
                return;
            }

            var list = Roguelike.Data.ConfigLoader.ResolveEncounter(enc);
            if (list.Count == 0)
            {
                Debug.LogWarning($"[RunDirector] DebugStartEncounter: 敌人组「{enc.name}」没有有效敌人");
                return;
            }

            // 中止当前战斗，避免后台协程继续跑
            BattleManager?.AbortBattle();

            bool isElite = !string.IsNullOrEmpty(enc.pool) && enc.pool.Contains("elite");
            bool isBoss = !string.IsNullOrEmpty(enc.pool) && enc.pool.Contains("boss");

            inEventBattle = false;
            RunData.isBossBattle = isBoss;

            Debug.Log($"[RunDirector][调试] 直接开战「{enc.name}」: {string.Join("、", list.ConvertAll(e => e.name))} (池={enc.pool}, 精英={isElite}, Boss={isBoss})");
            CreateBattleManager(list, RunData, isElite);
        }

        public void DebugEnterEvent(int eventId)
        {
            if (RunData == null)
            {
                Debug.LogWarning("[RunDirector] DebugEnterEvent: RunData 为空");
                return;
            }

            var ev = Roguelike.Data.ConfigLoader.GetEvent(eventId);
            if (ev == null)
            {
                Debug.LogWarning($"[RunDirector] DebugEnterEvent: 找不到事件 {eventId}");
                return;
            }

            // 强制结束当前战斗，避免后台协程继续跑
            BattleManager?.AbortBattle();

            if (uiManager == null)
            {
                Debug.LogWarning("[RunDirector] DebugEnterEvent: uiManager 为空");
                return;
            }

            uiManager.Hide<FatePanel>();
            uiManager.Hide<EventResultPanel>();
            uiManager.Hide<CardModifierPanel>();
            uiManager.Hide<DestinyPanel>();
            uiManager.Hide<ShopPanel>();
            uiManager.Hide<BattlePanel>();

            var panel = uiManager.ShowPanel<EventPanel>();
            if (panel != null)
            {
                Debug.Log($"[RunDirector] 调试进入事件「{ev.title}」(id={ev.id})");
                if (!RunData.visitedEventIds.Contains(ev.id))
                    RunData.visitedEventIds.Add(ev.id);
                RunData.currentEventStage = 1;
                currentEvent = ev;
                eventSystem.PrepareEvent(ev, RunData);
                panel.ShowEvent(ev, idx => OnEventOptionChosen(ev, idx));
            }
        }

        private void OnEventOptionChosen(Roguelike.Data.EventData ev, int optionIndex)
        {
            if (ev == null || optionIndex < 0 || optionIndex >= ev.options.Count)
            {
                FinishEventOption(null);
                return;
            }

            var option = ev.options[optionIndex];
            Debug.Log($"[RunDirector] 事件「{ev.title}」选择选项 {optionIndex}: {option.text}");

            // 需要选牌/选花色/选遗物的事件：先弹选择面板，选完再执行
            if (uiManager != null && EventSystem.NeedsCardPick(option))
            {
                bool requireEnchanted = EventSystem.NeedsEnchantedCard(option);
                string cardRequire = EventSystem.GetCardRequire(option);
                int pickCount = Mathf.Max(1, EventSystem.GetCardPickCount(option));
                PickCardsForOption(option, requireEnchanted, cardRequire, pickCount, new EventChoiceContext());
                return;
            }

            if (uiManager != null && EventSystem.NeedsRelicPick(option))
            {
                var relicPanel = uiManager.ShowPanel<RelicPickerPanel>();
                relicPanel?.ShowPicker("选择要丢弃的遗物",
                    relicId => FinishEventOption(eventSystem.ExecuteOption(option, RunData, relicSystem,
                        new EventChoiceContext { hasRelic = true, relicId = relicId })),
                    () => FinishEventOption(null));
                return;
            }

            if (uiManager != null && EventSystem.NeedsSuitPick(option))
            {
                var panel = uiManager.ShowPanel<SuitPickerPanel>();
                panel?.ShowPicker("选择一个花色",
                    suit => FinishEventOption(eventSystem.ExecuteOption(option, RunData, relicSystem,
                        new EventChoiceContext { hasSuit = true, suit = suit })),
                    () => FinishEventOption(null));
                return;
            }

            FinishEventOption(eventSystem.ExecuteOption(option, RunData, relicSystem));
        }

        /// <summary>连续选牌（ClearCardEnchant / 复制附魔等需要选多张时）</summary>
        private void PickCardsForOption(Roguelike.Data.EventOptionData option, bool requireEnchanted, string cardRequire, int remaining, EventChoiceContext ctx)
        {
            if (uiManager == null)
            {
                FinishEventOption(null);
                return;
            }

            var panel = uiManager.ShowPanel<CardPickerPanel>();
            panel?.ShowPicker(
                remaining > 1 ? $"选择一张牌（还需 {remaining} 张）" : "选择一张牌",
                (rank, suit) =>
                {
                    if (!ctx.hasCard)
                    {
                        ctx.hasCard = true;
                        ctx.cardRank = rank;
                        ctx.cardSuit = suit;
                    }
                    else
                    {
                        ctx.extraCardRanks.Add(rank);
                        ctx.extraCardSuits.Add(suit);
                    }

                    if (remaining > 1)
                        PickCardsForOption(option, requireEnchanted, cardRequire, remaining - 1, ctx);
                    else
                        FinishEventOption(eventSystem.ExecuteOption(option, RunData, relicSystem, ctx));
                },
                () => FinishEventOption(null),
                requireEnchanted,
                // 已选过的牌不能再选；选牌限制只约束第一张（如「复制附魔」的源牌）
                (rank, suit) => IsCardAlreadyPicked(ctx, rank, suit) ||
                                (!ctx.hasCard && !EventSystem.MatchesCardRequire(cardRequire, RunData, rank, suit)));
        }

        private static bool IsCardAlreadyPicked(EventChoiceContext ctx, int rank, Suit suit)
        {
            if (ctx.hasCard && ctx.cardRank == rank && (int)ctx.cardSuit == (int)suit) return true;
            int n = Mathf.Min(ctx.extraCardRanks.Count, ctx.extraCardSuits.Count);
            for (int i = 0; i < n; i++)
                if (ctx.extraCardRanks[i] == rank && (int)ctx.extraCardSuits[i] == (int)suit) return true;
            return false;
        }

        /// <summary>事件选项执行完（或取消）后：展示结果并进入下一战</summary>
        private void FinishEventOption(EventOutcome outcome)
        {
            if (uiManager != null)
            {
                uiManager.Hide<EventPanel>();
                uiManager.Hide<CardPickerPanel>();
                uiManager.Hide<SuitPickerPanel>();
                uiManager.Hide<RelicPickerPanel>();
            }

            // 多阶段事件（如赌徒的阶梯）：本阶段失败，展示结果后回到事件面板进入下一阶段
            if (outcome != null && outcome.reopenEvent)
            {
                if (!outcome.IsEmpty && uiManager != null)
                {
                    var reopenPanel = uiManager.ShowPanel<EventResultPanel>();
                    if (reopenPanel != null)
                    {
                        reopenPanel.ShowOutcome(outcome, ReopenCurrentEvent);
                        return;
                    }
                }
                ReopenCurrentEvent();
                return;
            }

            // 事件专属战斗（如「圣锤守卫」）：先打一场，打完再展示事件结算
            if (RunData != null && RunData.pendingEventEnemyId > 0)
            {
                int enemyId = RunData.pendingEventEnemyId;
                RunData.pendingEventEnemyId = 0;
                pendingEventOutcome = outcome;
                StartSpecialBattle(enemyId);
                return;
            }

            ShowEventOutcome(outcome);
        }

        /// <summary>重新打开当前事件（多阶段事件：失败后进入下一阶段）</summary>
        private void ReopenCurrentEvent()
        {
            if (uiManager == null || currentEvent == null)
            {
                EnterNextBattle();
                return;
            }

            Debug.Log($"[RunDirector] 多阶段事件「{currentEvent.title}」进入阶段 {RunData.currentEventStage}");
            uiManager.ShowPanel<EventPanel>()?.ShowEvent(currentEvent, idx => OnEventOptionChosen(currentEvent, idx));
        }

        /// <summary>展示事件结算面板；没有内容则直接进入下一战</summary>
        private void ShowEventOutcome(EventOutcome outcome)
        {
            // 展示完结果后：先结算槽满挂起的遗物、药水（替换/丢弃），再进入下一战
            Action after = () => ResolvePendingRelics(outcome, () => ResolvePendingPotions(outcome, EnterNextBattle));

            // 有获得内容时先展示结果（尤其是「哪张牌获得了什么附魔」）
            if (outcome != null && !outcome.IsEmpty && uiManager != null)
            {
                var resultPanel = uiManager.ShowPanel<EventResultPanel>();
                if (resultPanel != null)
                {
                    resultPanel.ShowOutcome(outcome, after);
                    return;
                }
            }

            after();
        }

        /// <summary>事件专属战斗：直接用指定敌人开一场（不计入关卡数）</summary>
        private void StartSpecialBattle(int enemyId)
        {
            var enemy = Roguelike.Data.ConfigLoader.GetEnemy(enemyId);
            if (enemy == null)
            {
                Debug.LogWarning($"[RunDirector] 事件战斗敌人不存在: {enemyId}");
                EnterNextBattle();
                return;
            }

            Debug.Log($"[RunDirector] 事件专属战斗：{enemy.name}");
            inEventBattle = true;
            CreateBattleManager(new List<EnemyData> { enemy }, RunData, false);
        }

        /// <summary>打开商店（onClose 为离开后的回调）</summary>
        public bool OpenShop(System.Action onClose = null)
        {
            if (RunData == null)
            {
                onClose?.Invoke();
                return false;
            }

            if (uiManager == null)
            {
                Debug.LogWarning("[RunDirector] OpenShop: uiManager 为空，跳过商店");
                onClose?.Invoke();
                return false;
            }

            // 先清除战斗面板及其它覆盖层，避免遮挡商店
            uiManager.Hide<FatePanel>();
            uiManager.Hide<EventPanel>();
            uiManager.Hide<EventResultPanel>();
            uiManager.Hide<CardModifierPanel>();
            uiManager.Hide<DestinyPanel>();
            uiManager.Hide<BattlePanel>();

            var inv = shopSystem.Generate(RunData);
            var panel = uiManager.ShowPanel<ShopPanel>();
            if (panel == null)
            {
                Debug.LogWarning("[RunDirector] OpenShop: ShopPanel 创建失败");
                onClose?.Invoke();
                return false;
            }

            // 置于最前，确保商店不被其它面板遮挡
            panel.transform.SetAsLastSibling();
            panel.ShowShop(inv, RunData, relicSystem, onClose);
            return true;
        }

        public void OnBattleLose()
        {
            Debug.Log("[RunDirector] 战斗失败，游戏结束");
            // 兜底获取 uiManager，防止 Awake/Start 前被调用
            var ui = uiManager ?? UIManager.Instance;
            if (ui != null)
            {
                var panel = ui.ShowPanel<ResultPanel>();
                panel?.ShowResult(false);
                panel?.SetRestartAction(() =>
                {
                    ui.Hide<ResultPanel>();
                    StartNewRun();
                });
            }
        }
    private void ShowVictory()
        {
            Debug.Log("[RunDirector] 通关胜利！");
            var ui = uiManager ?? UIManager.Instance;
            if (ui != null)
            {
                var panel = ui.ShowPanel<ResultPanel>();
                panel?.ShowResult(true);
                panel?.SetRestartAction(() =>
                {
                    ui.Hide<ResultPanel>();
                    StartNewRun();
                });
            }
        }
    }
}