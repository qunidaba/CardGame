using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 战斗管理器：核心战斗逻辑
/// 由 RunDirector 创建和持有，不负责单例
/// 纯逻辑层，只转发数据层事件，不手动 Invoke UI 更新
/// </summary>
public partial class BattleManager : IPotionContext, IEnemyAbilityContext, IDestinySkillContext
{
    // ===== 子系统 =====
    /// <summary>药水系统（使用药水 / 点数修正）</summary>
    public PotionSystem Potions { get; private set; }

    /// <summary>敌人能力系统（吞噬 / 弱点）</summary>
    public EnemyAbilitySystem EnemyAbilities { get; private set; }

    /// <summary>花色统计（本场战斗）</summary>
    public SuitTally Suits { get; private set; }

    /// <summary>命格主动技系统</summary>
    public DestinySkillSystem DestinySkills { get; private set; }

    /// <summary>命格被动系统（开局被动）</summary>
    public DestinyPassiveSystem DestinyPassives { get; private set; }

    public BattleManager()
    {
        Potions = new PotionSystem(this);
        EnemyAbilities = new EnemyAbilitySystem(this);
        Suits = new SuitTally((suit, count) => OnSuitTallyChanged?.Invoke(suit, count));
        DestinySkills = new DestinySkillSystem(this);
        DestinyPassives = new DestinyPassiveSystem();
    }

    // ===== IPotionContext 实现（供药水系统回调）=====
    public RunData Run => runData;
    public BattleUnit Player => player;
    public BattleUnit CurrentEnemy => GetEnemy();
    public IReadOnlyList<CardData> HandCards => handArea != null ? handArea.HandCards : null;
    public int PoisonBonus => GetPoisonBonus();
    public List<CardData> DrawToHand(int count)
    {
        var drawn = deckPile.Draw(count);
        AddToHand(drawn);
        return drawn;
    }
    public void NotifyCardVisualsChanged() => NotifyEnchantmentsChanged();
    public void NotifyPotionsChanged() => OnPotionsChanged?.Invoke();

    // ===== IEnemyAbilityContext 实现（供敌人能力系统回调）=====
    public IReadOnlyList<BattleUnit> Enemies => enemies;
    public DeckPile Deck => deckPile;
    // 注意：BattleManager 已有同名 CurrentTarget（玩家选中目标，语义不同），故显式实现
    BattleUnit IEnemyAbilityContext.CurrentTarget => GetEnemy();

    // ===== IDestinySkillContext 实现（供命格主动技回调；Run/Player/CurrentEnemy/IsBattleOver/GetAliveEnemies 已公开）=====
    float IDestinySkillContext.HitInterval => hitInterval;
    void IDestinySkillContext.GrantRandomEnchants(List<CardData> cards, int count, int minTier) => GrantRandomEnchantsToCards(cards, count, minTier);
    void IDestinySkillContext.NotifyDestinyChanged() => OnDestinyChanged?.Invoke();
    void IDestinySkillContext.NotifyHandChanged() => OnHandChanged?.Invoke();
    void IDestinySkillContext.CheckBattleEnd() => CheckBattleEnd();

    // ===== 对外转发（供 Handler / 状态注册表调用）=====
    public void ApplySwallow(int count, BattleUnit attacker) => EnemyAbilities.ApplySwallow(count, attacker);
    public List<CardData> GetSwallowedCards(BattleUnit unit) => EnemyAbilities.GetSwallowedCards(unit);
    public List<WeaknessType> GetWeaknesses(BattleUnit unit) => EnemyAbilities.GetWeaknesses(unit);

    // --- 精确数据变化事件（UI 直接订阅，携带 delta）---
    public event Action<BattleUnit, int> OnPlayerHpChanged;      // delta: +治疗 -伤害
    public event Action<BattleUnit, int> OnPlayerMaxHpChanged;
    public event Action<BattleUnit, int> OnPlayerDefenseChanged; // delta: +得盾 -失盾
    public event Action<BattleUnit, int> OnEnemyHpChanged;
    public event Action<BattleUnit, int> OnEnemyMaxHpChanged;
    public event Action<BattleUnit, int> OnEnemyDefenseChanged;

    // 本次攻击的实际伤害（= 扣血 + 被防御抵消），与是否掉血无关，用于伤害飘字
    public event Action<BattleUnit, int> OnPlayerDamageTaken;
    public event Action<BattleUnit, int> OnEnemyDamageTaken;

    // --- 状态效果事件 ---
    public event Action<StatusEffectType, int> OnPlayerStatusAdded;    // type, amount
    public event Action<StatusEffectType, int> OnPlayerStatusRemoved;  // type, amount
    public event Action<StatusEffectType, int> OnPlayerStatusChanged;  // type, newAmount
    public event Action<BattleUnit, StatusEffectType, int> OnEnemyStatusAdded;
    public event Action<BattleUnit, StatusEffectType, int> OnEnemyStatusRemoved;
    public event Action<BattleUnit, StatusEffectType, int> OnEnemyStatusChanged;

    /// <summary>敌人列表发生变化（召唤）：UI 需要重建槽位</summary>
    public event Action OnEnemyListChanged;

    /// <summary>敌人数量上限（召唤类技能不能超过）</summary>
    public const int MaxEnemyCount = 3;

    public event Action<int> OnDeckCountChanged;
    public event Action<int> OnDiscardCountChanged;
    public event Action OnHandChanged;

    /// <summary>牌面附魔发生变化（魔导/改造等），UI 订阅强制刷新手牌</summary>
    public event Action OnCardEnchantmentsChanged;
    public void NotifyEnchantmentsChanged() => OnCardEnchantmentsChanged?.Invoke();

    public event Action<int> OnGoldChanged;
    public event Action OnRelicsChanged;
    public event Action OnPotionsChanged;

    // --- 流程事件 ---
    public event Action OnPlayerTurnStart;
    public event Action OnPlayerTurnEnd;
    public event Action OnEnemyTurnStart;
    public event Action OnEnemyTurnEnd;
    public event Action<HandTypeResult> OnPreviewChanged;
    public event Action<bool> OnBattleOver;
    public event Action<bool> OnTurnEndPrompt;

    // --- 回合阶段事件（UI 订阅做动画/提示）---
    public event Action<string> OnPlayerPhaseChanged;   // phase name: "ClearDefense", "DrawCards", "TurnStartEffects", "WaitInput", "DiscardHand", "TurnEndEffects"
        public event Action<string> OnEnemyPhaseChanged;    // phase name: "TurnStart", "ClearDefense", "TurnStartEffects", "DoT", "ShowIntent", "Act", "TurnEndEffects", "TurnEnd"

        /// <summary>敌人出手前（前摇）：unit = 出手的敌人，intent = 它将执行的意图。UI 播放大/变红/意图闪烁。</summary>
        public event Action<BattleUnit, Roguelike.Data.IntentData> OnEnemyActing;

    // --- 玩家可操作状态变化 ---
    public event Action<bool> OnCanPlayerActChanged;

    // 敌人意图就绪（玩家回合开始时触发，用于显示意图）
    public event Action OnEnemyIntentReady;

    /// <summary>敌人行动完毕，隐藏意图显示（等下次刷新再出现）</summary>
    public event Action OnEnemyIntentHidden;

    // 敌方行动延迟回调
    public System.Action<System.Action> OnRequestEnemyActionDelay;

    // --- 模块引用 ---
    private DeckPile deckPile;
    private HandArea handArea;
    private BattleUnit player;
    /// <summary>当前战场上的所有敌人（公开供 Handler 遍历）</summary>
    public readonly List<BattleUnit> enemies = new List<BattleUnit>();
    private readonly List<Roguelike.Data.EnemyData> enemyDatas = new List<Roguelike.Data.EnemyData>();

    /// <summary>当前目标下标：所有「单体」效果（出牌伤害/附魔/遗物/药水）都作用于它</summary>
    public int CurrentTargetIndex { get; private set; }

    // --- 奖励 ---

    // --- 配置 ---
    private int drawPerTurn = 10;
    private int maxHandSize = 15;
    private int enemyAttackMin = 3;
    private int enemyAttackMax = 8;
    private int enemyDefenseValue = 5;

    // --- 回合阶段延迟配置 ---
    public float phaseDelay = 0.5f;           // 通用阶段间隔
    public float dotInterval = 0.3f;          // DoT 逐个结算间隔
    public float hitInterval = 0.25f;         // 多段伤害每段间隔
        public float intentDisplayDuration = 1.0f; // 意图显示时长
        public float enemyWindupDelay = 0.2f;      // 敌人出手前摇时长（前摇动画在 UI 侧播）
    public float drawCardInterval = 0.06f;    // 抽牌逐张间隔（0 = 一次性）

    // --- 状态 ---
    public bool IsPlayerTurn { get; private set; }
    public bool IsBattleOver { get; private set; }
    public bool IsWin { get; private set; }
    private int mulligansUsed = 0;
    public bool HasUsedMulligan
    {
        get
        {
            if (battleNoMulligan) return true;
            int extra = (relicSystem != null ? relicSystem.GetFlatBonus("ExtraMulligan", 0) : 0) + battleMulliganDelta;
            return mulligansUsed >= 1 + extra;
        }
    }

    /// <summary>本场战斗已进行的回合数（遗物「速通者」用）</summary>
    public int TurnsElapsed { get; private set; }
    public bool IsElite { get; private set; }

    /// <summary>
    /// 玩家是否可以操作（回合开始效果全部执行完，进入等待输入阶段）
    /// </summary>
    public bool CanPlayerAct { get; private set; }

    // 当前打出的牌型结果（用于 OnHandType 触发时传给遗物处理器）
    private HandTypeResult currentHandTypeResult;

    // 每个敌人各自的意图（本回合将执行的那个）
    private readonly Dictionary<BattleUnit, Roguelike.Data.IntentData> enemyIntents =
        new Dictionary<BattleUnit, Roguelike.Data.IntentData>();

    // --- 对外访问 ---
    public BattleUnit GetPlayer() => player;

    /// <summary>当前选中的敌人（未选中 / 已阵亡时为 null；所有单体效果打它）</summary>
    public BattleUnit GetEnemy() => HasTarget ? CurrentTarget : null;

    /// <summary>当前目标下标指向的单位（可能为 null，也可能已阵亡）</summary>
    public BattleUnit CurrentTarget =>
        (CurrentTargetIndex >= 0 && CurrentTargetIndex < enemies.Count) ? enemies[CurrentTargetIndex] : null;

    /// <summary>是否已选中一个存活敌人（为空时不能打出需要目标的牌）</summary>
    public bool HasTarget => CurrentTarget != null && !CurrentTarget.IsDead;

    /// <summary>全部敌人（含已死亡，按出场顺序）</summary>
    public IReadOnlyList<BattleUnit> GetEnemies() => enemies;

    /// <summary>全部敌人的配置数据</summary>
    public IReadOnlyList<Roguelike.Data.EnemyData> GetEnemyDatas() => enemyDatas;

    public int EnemyCount => enemies.Count;

    /// <summary>存活敌人（按出场顺序）</summary>
    public List<BattleUnit> GetAliveEnemies()
    {
        var list = new List<BattleUnit>();
        foreach (var e in enemies)
            if (e != null && !e.IsDead) list.Add(e);
        return list;
    }

    /// <summary>切换当前目标（只能选存活敌人；嘲讽生效时只能选嘲讽者）</summary>
    public void SetTarget(int index)
    {
        if (index < 0 || index >= enemies.Count) return;
        if (enemies[index] == null || enemies[index].IsDead) return;

        int tauntIdx = GetTauntTargetIndex();
        if (tauntIdx >= 0 && index != tauntIdx) return;   // 被嘲讽：不能选别人

        if (CurrentTargetIndex == index) return;

        CurrentTargetIndex = index;
        OnTargetChanged?.Invoke();
    }

    /// <summary>清空选中目标（开局 / 玩家回合结束 / 当前敌人阵亡时调用）</summary>
    public void ClearTarget()
    {
        if (CurrentTargetIndex < 0) return;
        CurrentTargetIndex = -1;
        OnTargetChanged?.Invoke();
    }

    /// <summary>当前目标已阵亡时清空选中（不再自动改选其他敌人）</summary>
    public void EnsureValidTarget()
    {
        if (HasTarget) return;
        ClearTarget();
    }

    /// <summary>当前目标变化（UI 刷新用）</summary>
    public event Action OnTargetChanged;

    public HandArea GetHandArea() => handArea;
    public int GetDeckCount() => deckPile.Count;
    public int GetDiscardCount() => deckPile.DiscardCount;
    public DeckPile GetDeckPile() => deckPile;
    public RunData GetRunData() => runData;

    /// <summary>当前目标的意图</summary>
    public Roguelike.Data.IntentData GetCurrentEnemyIntent() => GetIntentOf(GetEnemy());

    /// <summary>指定敌人的意图（没有则 null）</summary>
    public Roguelike.Data.IntentData GetIntentOf(BattleUnit unit)
        => (unit != null && enemyIntents.TryGetValue(unit, out var i)) ? i : null;

    /// <summary>当前目标的配置数据（含 image 立绘路径）</summary>
    public Roguelike.Data.EnemyData CurrentEnemyData =>
        (CurrentTargetIndex >= 0 && CurrentTargetIndex < enemyDatas.Count) ? enemyDatas[CurrentTargetIndex] : null;

    /// <summary>
    /// 敌人意图对玩家的预览伤害：经过敌人力量/虚弱与玩家易伤/无形修正（无副作用，仅用于 UI）
    /// </summary>
    public int GetEnemyIntentPreviewDamage() => GetIntentPreviewDamage(GetCurrentEnemyIntent());

    /// <summary>某个意图对玩家的预览伤害（type=Multi 会累加子行动）</summary>
    public int GetIntentPreviewDamage(Roguelike.Data.IntentData intent)
        => GetIntentPreviewDamage(intent, GetEnemy());

    /// <summary>指定敌人某个意图的预览伤害</summary>
    public int GetIntentPreviewDamage(Roguelike.Data.IntentData intent, BattleUnit attacker)
    {
        if (attacker == null || player == null || intent == null) return 0;

        switch (intent.type)
        {
            case "Attack":
            case "MultiAttack":
            case "Sunder":
                return player.StatusEffects.PreviewTakeDamage(PreviewChargeDamage(attacker, intent));
            case "Multi":
            {
                int sum = 0;
                if (intent.actions != null)
                    foreach (var a in intent.actions) sum += GetIntentPreviewDamage(a, attacker);
                return sum;
            }
            default:
                return 0;
        }
    }

    /// <summary>预览用：把「蓄力」的翻倍也算进去（不消耗）</summary>
    private static int PreviewChargeDamage(BattleUnit attacker, Roguelike.Data.IntentData intent)
    {
        int damage = attacker.DealDamage(intent.value);
        if (damage > 0 && attacker.GetStatusAmount(StatusEffectType.Charge) > 0)
            damage *= 2;
        return damage;
    }

    // ===== 花色命运（SuitTally 子系统）=====

    public int GetSuitCount(Suit suit) => Suits.GetCount(suit);
    public Dictionary<Suit, int> GetSuitTally() => Suits.GetAll();
    public Suit GetDominantSuit() => Suits.Dominant();
    private void ResetSuitTally() => Suits.Reset();
    private void CountPlayedSuits(List<CardData> cards) => Suits.CountPlayed(cards);

    // ===== 附魔回合状态接口（由 EnchantmentSystem 调用）=====

    /// <summary>本次出牌结束后返回 value 张牌到手牌（连对专家）</summary>
    public void RequestReturnToHand(int value) => returnToHandCount += value;

    /// <summary>
    /// 把出牌的各类加成套用到 effects 上（纯计算，实际出牌与预览共用）。
    /// playIndex = 本次是第几手（从 1 起）；consumeRage=true 时消耗暴怒层数（实际出牌），false 时不消耗（预览）。
    /// </summary>
    private void ApplyPlayBonuses(List<HandEffectTable.HandEffect> effects, List<CardData> selected, HandTypeResult result, int playIndex, bool consumeRage)
    {
        // 顺风耳：本回合出牌伤害加成
        int turnBonus = player.GetStatusAmount(StatusEffectType.TurnDamageBonus);
        if (turnBonus > 0) HandEffectUtil.AddDamageToEffects(effects, turnBonus);

        // 同花顺之巅：本场该花色每张牌的额外伤害
        int suitBonus = 0;
        foreach (var card in selected)
            suitBonus += player.GetStatusAmount(StatusEffectType.SuitDamageBonus, card.suit);
        if (suitBonus > 0) HandEffectUtil.AddDamageToEffects(effects, suitBonus);

        // 遗物「花色调和」：主命格花色每张牌额外 +N 伤害
        if (relicSystem != null && runData != null && runData.HasMainDestiny)
        {
            int per = relicSystem.GetFlatBonus("MainSuitDamageBonus", 0);
            if (per > 0)
            {
                int cnt = 0;
                foreach (var card in selected)
                    if ((int)card.suit == runData.mainDestinySuit) cnt++;
                if (cnt > 0) HandEffectUtil.AddDamageToEffects(effects, cnt * per);
            }
        }

        // 遗物「附魔共鸣」：每 4 张已附魔的牌 +1 伤害
        if (relicSystem != null && runData != null && relicSystem.GetFlatBonus("EnchantResonance", 0) > 0)
        {
            int bonus = runData.GetEnchantedCardKeys().Count / 4;
            if (bonus > 0) HandEffectUtil.AddDamageToEffects(effects, bonus);
        }

        // 命格：黑桃被动（每回合第 1 手翻倍 / 第 2 手起 +N）——数值集中在 DestinyPassiveSystem
        DestinyPassiveSystem.GetSpadePlayBonus(runData, playIndex, out int spadeAdd, out float spadeMult);
        if (spadeMult != 1f) HandEffectUtil.MultiplyDamageEffect(effects, spadeMult);
        if (spadeAdd > 0) HandEffectUtil.AddDamageToEffects(effects, spadeAdd);

        // 暴怒：下一次出牌伤害翻倍
        if (player.GetStatusAmount(StatusEffectType.Rage) > 0)
        {
            HandEffectUtil.MultiplyDamageEffect(effects, 2);
            if (consumeRage) player.RemoveStatus(StatusEffectType.Rage, 1);
        }

        // 事件：顺子/同花 额外抽牌与同花额外伤害（永久 + 下场战斗）
        if (runData != null)
        {
            int straightDraw = battleStraightDraw + runData.permanentStraightDrawBonus;
            if (straightDraw > 0 && HandEffectUtil.IsStraightHand(result))
                HandEffectUtil.AddEffectValue(effects, HandEffectTable.EffectType.DrawCard, straightDraw);

            if (runData.permanentFlushDrawBonus > 0 && HandEffectUtil.IsFlushHand(result))
                HandEffectUtil.AddEffectValue(effects, HandEffectTable.EffectType.DrawCard, runData.permanentFlushDrawBonus);

            if (runData.permanentFlushDamageBonus > 0 && HandEffectUtil.IsFlushHand(result))
                HandEffectUtil.AddDamageToEffects(effects, runData.permanentFlushDamageBonus);
        }
    }

    /// <summary>
    /// 预览当前选中手牌的完整效果列表（基础牌型 + 出牌加成 + 纯数值附魔）。
    /// 不含遗物 OnCardPlayed / OnHandType 的额外加成（那些有副作用，无法安全预览）。
    /// </summary>
    public List<HandEffectTable.HandEffect> PreviewSelectedEffects()
    {
        if (handArea == null || player == null) return null;

        var selected = handArea.GetSelectedCards();
        if (selected == null || selected.Count == 0) return null;

        var result = HandEvaluator.Evaluate(selected);
        if (!result.IsValid) return null;

        var effects = HandEffectTable.GetEffects(result);
        ApplyPlayBonuses(effects, selected, result, playsThisTurn + 1, consumeRage: false);
        enchantmentSystem?.ApplyPreviewModifiers(selected, result, effects);
        return effects;
    }

    /// <summary>
    /// 预览当前选中手牌对「当前目标」的实际伤害（含出牌加成 + 玩家力量/虚弱/专注 + 敌人易伤/无形）。
    /// </summary>
    public int PreviewSelectedDamage()
    {
        var target = GetEnemy();
        if (handArea == null || player == null || target == null) return 0;

        var selected = handArea.GetSelectedCards();
        if (selected == null || selected.Count == 0) return 0;

        var effects = PreviewSelectedEffects();
        if (effects == null) return 0;

        var dmg = effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage);
        if (dmg == null) return 0;

        float permMult = runData != null ? runData.permanentDamageMultiplier : 1f;
        int raw = Mathf.RoundToInt(dmg.value * nextPlayDamageMultiplier * permMult);
        int dealt = player.DealDamage(raw);
        int total = target.StatusEffects.PreviewTakeDamage(dealt);

        // 遗物「跳动的心脏」：每张黑桃额外造成 1 次 1 点伤害
        if (relicSystem != null && relicSystem.GetFlatBonus("BeatingHeart", 0) > 0)
        {
            int spades = 0;
            foreach (var c in selected)
                if (c.EffectiveSuit == Suit.Spade) spades++;
            for (int i = 0; i < spades; i++)
                total += target.StatusEffects.PreviewTakeDamage(player.DealDamage(1));
        }

        return total;
    }

    private RunData runData;
    private bool isElite;

    private EnchantmentSystem enchantmentSystem;
    private RelicSystem relicSystem;
    private RelicEffectProcessor relicProcessor;
    /// <summary>遗物效果处理器（供 Handler 使用）</summary>
    public RelicEffectProcessor RelicProcessor => relicProcessor;
    private bool isFirstTurn = true;

    // 事件效果（本场战斗，开场时从 RunData 读取并消耗）
    private int battleMulliganDelta = 0;
    private bool battleNoMulligan = false;
    private int battleDrawBonus = 0;
    private int battleGuaranteeSuit = -1;
    private int battleGuaranteeCount = 0;
    private readonly List<int> sealedSuits = new List<int>();   // 本场无法打出的花色

    // 梅花事件：本场战斗修正
    private int battleStraightDraw = 0;          // 顺子额外抽牌
    private int battleFateBonus = 0;             // 命运之力积攒 +N
    private bool battleFateGainDisabled = false; // 本场无法积攒命运之力
    private int playerHpLostThisBattle = 0;      // 本场玩家累计失去的生命（事件挑战用）

    /// <summary>本场战斗玩家累计失去的生命（事件挑战结算用）</summary>
    public int PlayerHpLostThisBattle => playerHpLostThisBattle;

    // --- 花色命运：本场战斗各花色出牌计数（逻辑在 SuitTally）---

    /// <summary>某花色计数变化（花色, 最新计数）</summary>
    public event Action<Suit, int> OnSuitTallyChanged;

    // --- 附魔即时状态 ---
    private int returnToHandCount = 0;     // 本次出牌后返回手牌的张数（连对专家）
    private readonly HashSet<CardData> returnedThisTurn = new HashSet<CardData>();  // 本回合已触发过回手的牌（每张每回合一次）
    // 注：顺风耳/同花之魂/同花顺之巅 改为挂在玩家的 StatusEffectSystem 上，直接显示在状态栏

    // --- 命格 ---
    public event Action OnDestinyChanged;   // 命格/命运之力变化（UI 刷新）
    private int playsThisTurn = 0;          // 本回合出牌次数（黑桃 Lv2/Lv3）

    /// <summary>
    /// 初始化一场新战斗（兼容旧接口）
    /// </summary>
    public void InitBattle(int playerHp, int enemyHp)
    {
        player = new BattleUnit("玩家", playerHp);

        enemies.Clear();
        enemyDatas.Clear();
        enemyIntents.Clear();
        queuedIntents.Clear();
        EnemyAbilities.Clear();
        CurrentTargetIndex = -1;   // 开局不预选敌人，需要玩家点选目标

        var dummy = new EnemyData { id = 0, name = "敌人", hp = enemyHp, maxHp = enemyHp };
        enemies.Add(new BattleUnit(dummy.name, dummy.hp));
        enemyDatas.Add(dummy);

        deckPile = new DeckPile();
        var fullDeck = DeckBuilder.BuildStandardDeck();
        deckPile.Init(fullDeck);

        handArea = new HandArea();
        ResetSuitTally();

        IsPlayerTurn = true;
        IsBattleOver = false;
        IsWin = false;
        isFirstTurn = true;

        SubscribeDataEvents();
        Debug.Log("=== 战斗开始 ===");
        StartPlayerTurn();
    }

    /// <summary>
    /// 初始化战斗（Roguelike 模式：带 RunData、敌人列表、精英标记）
    /// </summary>
    public void InitBattleWithData(List<EnemyData> enemyList, RunData runData, bool isElite, RelicSystem relicSystem)
    {
        this.runData = runData;
        this.isElite = isElite;
        this.relicSystem = relicSystem;

        // 创建遗物效果处理器
        relicProcessor = new RelicEffectProcessor(relicSystem);
        relicProcessor.OnPlayerHpChanged = (unit, amount) => OnPlayerHpChanged?.Invoke(unit, amount);
        relicProcessor.OnEnemyHpChanged = (unit, amount) => OnEnemyHpChanged?.Invoke(unit, amount);
        relicProcessor.OnPlayerDefenseChanged = (unit, amount) => OnPlayerDefenseChanged?.Invoke(unit, amount);
        relicProcessor.OnEnemyDefenseChanged = (unit, amount) => OnEnemyDefenseChanged?.Invoke(unit, amount);
        relicProcessor.OnCardVisualsChanged = () => OnCardEnchantmentsChanged?.Invoke();

        // 初始化附魔系统
        enchantmentSystem = new EnchantmentSystem();
        enchantmentSystem.Initialize(runData, this, RunDirector.Instance);

        // 初始化玩家（使用 RunData 的血量）
        player = new BattleUnit("玩家", runData.CurrentHp);
        player.MaxHp = runData.MaxHp;
        player.OnTakeDamageCallback = dmg => 
        {
            enchantmentSystem?.OnTakeDamage(dmg);
            relicProcessor?.ApplyEffects("OnTakeDamage", BuildContext());
        };

        // 初始化敌人（支持多个）
        enemies.Clear();
        enemyDatas.Clear();
        enemyIntents.Clear();
        queuedIntents.Clear();
        EnemyAbilities.Clear();
        CurrentTargetIndex = -1;   // 开局不预选敌人，需要玩家点选目标

        if (enemyList != null)
        {
            foreach (var data in enemyList)
            {
                if (data == null) continue;

                // 同名敌人加编号，便于区分
                string displayName = data.name;
                int sameCount = 0;
                foreach (var d in enemyDatas)
                    if (d != null && d.name == data.name) sameCount++;
                if (sameCount > 0) displayName = $"{data.name} {sameCount + 1}";

                var unit = new BattleUnit(displayName, data.hp);
                unit.MaxHp = data.maxHp;
                unit.OnTakeDamageCallback = dmg => enchantmentSystem?.OnDealDamage(dmg);

                enemies.Add(unit);
                enemyDatas.Add(data);
            }
        }

        if (enemies.Count == 0)
        {
            var fallback = new EnemyData { id = 0, name = "敌人", hp = 20, maxHp = 20 };
            enemies.Add(new BattleUnit(fallback.name, fallback.hp));
            enemyDatas.Add(fallback);
            Debug.LogWarning("[BattleManager] 敌人列表为空，使用占位敌人");
        }

        Debug.Log($"[BattleManager] 战斗开始，敌人 {enemies.Count} 个：{string.Join("、", enemyDatas.ConvertAll(d => d.name))}");

        deckPile = new DeckPile();

        // 使用带附魔的牌组（先清掉上一场遗留的临时附魔）
        runData.ClearTempEnchantments();
        var fullDeck = DeckBuilder.BuildDeckWithEnchantments(runData);
        deckPile.Init(fullDeck);

        // 天命：战斗第一回合必定抽到
        var destinyCards = fullDeck.FindAll(c => c.IsDestiny);
        if (destinyCards.Count > 0)
        {
            deckPile.MoveToTop(destinyCards);
            Debug.Log($"天命：{destinyCards.Count} 张牌置于牌堆顶");
        }

        // 事件效果：读取并消耗「下场战斗」相关
        battleMulliganDelta = runData.nextBattleMulliganDelta;
        battleNoMulligan = runData.nextBattleNoMulligan;
        battleDrawBonus = runData.nextBattleDrawBonus;
        battleGuaranteeSuit = runData.nextBattleGuaranteeSuit;
        battleGuaranteeCount = runData.nextBattleGuaranteeCount;
        runData.nextBattleMulliganDelta = 0;
        runData.nextBattleNoMulligan = false;
        runData.nextBattleDrawBonus = 0;
        runData.nextBattleGuaranteeSuit = -1;
        runData.nextBattleGuaranteeCount = 0;

        // 梅花事件：本场顺子抽牌 / 命运之力修正
        battleStraightDraw = runData.nextBattleStraightDraw;
        battleFateBonus = runData.nextBattleFateBonus;
        runData.nextBattleStraightDraw = 0;
        runData.nextBattleFateBonus = 0;

        // 事件：接下来 N 场战斗无法积攒命运之力
        battleFateGainDisabled = runData.fateGainDisabledBattles > 0;
        if (runData.fateGainDisabledBattles > 0) runData.fateGainDisabledBattles--;

        // 事件挑战：本场失血统计
        playerHpLostThisBattle = 0;

        // 下场无法打出的花色
        sealedSuits.Clear();
        if (runData.nextBattleSealedSuits != null && runData.nextBattleSealedSuits.Count > 0)
        {
            sealedSuits.AddRange(runData.nextBattleSealedSuits);
            runData.nextBattleSealedSuits.Clear();
        }

        // 保底花色：把该花色随机 N 张牌置于牌堆顶（保证开局抽到）
        if (battleGuaranteeCount > 0 && battleGuaranteeSuit >= 0)
        {
            var suitCards = fullDeck.FindAll(c => (int)c.suit == battleGuaranteeSuit);
            for (int i = suitCards.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var tmp = suitCards[i]; suitCards[i] = suitCards[j]; suitCards[j] = tmp;
            }
            int n = Mathf.Min(battleGuaranteeCount, suitCards.Count);
            if (n > 0)
            {
                deckPile.MoveToTop(suitCards.GetRange(0, n));
                Debug.Log($"[事件] 保底花色：{n} 张 {(Suit)battleGuaranteeSuit} 置于牌堆顶");
            }
        }

        handArea = new HandArea();
        ResetSuitTally();

        // 命格：战斗开始类被动
        TurnsElapsed = 0;
        ApplyBattleStartEffects();

        IsPlayerTurn = true;
        IsBattleOver = false;
        IsWin = false;
        mulligansUsed = 0;
        IsElite = isElite;
        isFirstTurn = true;

        SubscribeDataEvents();

        // 初始化所有敌人的意图（不触发显示，等玩家回合开始显示）
        // 第一回合：勾了 battleStart 的意图必定出场
        SelectAllEnemyIntents(preferBattleStart: true);

        Debug.Log($"=== 战斗开始: {enemies.Count} 个敌人 (精英={isElite}) ===");
        StartPlayerTurn();
    }

    private void SubscribeDataEvents()
    {
        // 玩家单位事件：转发 delta
        player.OnHpChanged += (unit, delta) =>
        {
            if (delta < 0)
            {
                playerHpLostThisBattle += -delta;

                // 事件挑战：实时更新「已失去」层数
                if (runData != null && runData.challengeHpLimit >= 0)
                    player.SetStatus(StatusEffectType.Challenge, playerHpLostThisBattle, -1);
            }
            OnPlayerHpChanged?.Invoke(unit, delta);
        };
        player.OnDamageTaken += (unit, dmg) => OnPlayerDamageTaken?.Invoke(unit, dmg);
        player.OnMaxHpChanged += hp => OnPlayerMaxHpChanged?.Invoke(player, hp);
        player.OnDefenseChanged += (unit, delta) => OnPlayerDefenseChanged?.Invoke(unit, delta);

        // 玩家状态效果事件
        player.StatusEffects.OnEffectAdded += (type, amount) => OnPlayerStatusAdded?.Invoke(type, amount);
        player.StatusEffects.OnEffectRemoved += (type, amount) => OnPlayerStatusRemoved?.Invoke(type, amount);
        player.StatusEffects.OnEffectChanged += (type, amount) => OnPlayerStatusChanged?.Invoke(type, amount);

        // 敌人单位事件（每个敌人各自订阅，转发时带上 unit）
        foreach (var unit in enemies)
            SubscribeEnemyUnit(unit);

        // 牌堆
        deckPile.OnCountChanged += count => OnDeckCountChanged?.Invoke(count);
        deckPile.OnDiscardCountChanged += count => OnDiscardCountChanged?.Invoke(count);

        // 手牌
        handArea.OnChanged += () => OnHandChanged?.Invoke();

        // RunData
        runData.OnGoldChanged += gold => OnGoldChanged?.Invoke(gold);
        runData.OnRelicsChanged += () => OnRelicsChanged?.Invoke();
        runData.OnPotionsChanged += () => OnPotionsChanged?.Invoke();
        // RunData 的血量变化（事件/作弊窗口）：同步到战斗单位，再转发（携带正确 delta）
        runData.OnMaxHpChanged += hp =>
        {
            if (player != null) player.MaxHp = hp;
            OnPlayerMaxHpChanged?.Invoke(player, hp);
        };
        runData.OnCurrentHpChanged += hp =>
        {
            if (player != null) player.CurrentHp = hp;   // 触发 OnHpChanged → 正确的 delta
        };
        runData.OnDestinyChanged += () => OnDestinyChanged?.Invoke();
    }

    /// <summary>订阅单个敌人的事件（初始化 / 召唤时都要调）</summary>
    private void SubscribeEnemyUnit(BattleUnit unit)
    {
        if (unit == null) return;
        var u = unit;

        u.OnHpChanged += (unitRef, delta) => OnEnemyHpChanged?.Invoke(unitRef, delta);
        u.OnDamageTaken += (unitRef, dmg) =>
        {
            if (runData != null && dmg > 0) runData.totalDamageDealt += dmg;   // 统计：总伤害
            OnEnemyDamageTaken?.Invoke(unitRef, dmg);
        };
        u.OnMaxHpChanged += hp => OnEnemyMaxHpChanged?.Invoke(u, hp);
        u.OnDefenseChanged += (unitRef, delta) => OnEnemyDefenseChanged?.Invoke(unitRef, delta);

        // 敌人状态效果事件（带 unit，UI 才知道刷新哪个槽位）
        u.StatusEffects.OnEffectAdded += (type, amount) => OnEnemyStatusAdded?.Invoke(u, type, amount);
        u.StatusEffects.OnEffectRemoved += (type, amount) => OnEnemyStatusRemoved?.Invoke(u, type, amount);
        u.StatusEffects.OnEffectChanged += (type, amount) => OnEnemyStatusChanged?.Invoke(u, type, amount);
    }

    /// <summary>调试用：强制结束当前战斗（不结算奖励、不触发后续流程）</summary>
    public void AbortBattle()
    {
        IsBattleOver = true;
        IsPlayerTurn = false;
        CanPlayerAct = false;
    }

    /// <summary>
    /// 开始玩家回合（协程：分阶段执行）
    /// </summary>
    public void StartPlayerTurn()
    {
        if (IsBattleOver) return;
        CoroutineRunner.Instance.StartCoroutine(Roguelike.CoroutineRunner.SafeCoroutine(StartPlayerTurnRoutine()));
    }

    private IEnumerator StartPlayerTurnRoutine()
    {
        IsPlayerTurn = true;
        mulligansUsed = 0;
        CanPlayerAct = false; // 回合开始效果执行期间不可操作
        OnCanPlayerActChanged?.Invoke(false);

        // 新回合：重置「本回合已回手」记录
        returnedThisTurn.Clear();
        playsThisTurn = 0;
        PendingSearchCount = 0;
        TurnsElapsed++;

        // 敌人被动：愤怒骷髅头——每回合刷新 2 个弱点牌型
        EnemyAbilities.RefreshWeaknesses();

        Debug.Log($"--- 玩家回合开始 | 血量: {player.CurrentHp}/{player.MaxHp} | 防御: {player.Defense} ---");

        // 阶段 1: 玩家 DoT（中毒/灼烧/再生）
        // 放在清除防御之前，让玩家残留的防御能吸收这部分伤害（和敌方回合保持一致）
        OnPlayerPhaseChanged?.Invoke("DoT");
        var playerDots = player.StatusEffects.GetAllEffects()
            .Where(e => e.type == StatusEffectType.Poison ||
                        e.type == StatusEffectType.Burn ||
                        e.type == StatusEffectType.Regeneration)
            .ToList();
        foreach (var eff in playerDots)
        {
            ProcessSingleDoT(player, eff);
            yield return new WaitForSeconds(dotInterval);
        }
        if (player.IsDead)
        {
            CheckBattleEnd();
            yield break;
        }
        yield return new WaitForSeconds(phaseDelay);

        // 阶段 2: 清除旧防御（DoT 结算完才清）
        OnPlayerPhaseChanged?.Invoke("ClearDefense");
        if (player.Defense > 0)
        {
            player.ClearDefense();
        }
        yield return new WaitForSeconds(phaseDelay);

        // 阶段 2: 抽牌（先把弃牌堆全部洗回牌堆）
        OnPlayerPhaseChanged?.Invoke("DrawCards");
        deckPile.ReshuffleDiscardIntoDeck();
        int drawBonus = Mathf.Max(0, battleDrawBonus);
        int needDraw = maxHandSize - handArea.Count;   // 手牌上限 15
        if (needDraw > 0)
        {
            int toDraw = Mathf.Min(needDraw, drawPerTurn + drawBonus);
            var drawn = deckPile.Draw(toDraw);
            yield return DrawStaggered(drawn);
            Debug.Log($"玩家抽了 {drawn.Count} 张牌，手牌: {handArea.Count}");
        }

        // 上回合「同花之魂」预约的额外抽牌（读玩家状态，抽完移除）
        int nextDraw = player.GetStatusAmount(StatusEffectType.NextTurnDraw);
        if (nextDraw > 0)
        {
            var extra = deckPile.Draw(nextDraw);
            yield return DrawStaggered(extra);
            Debug.Log($"同花之魂：本回合额外抽了 {extra.Count} 张牌");
            player.RemoveStatus(StatusEffectType.NextTurnDraw);
        }

        // 命格：梅花 Lv1 第一回合额外抽牌 / Lv3 开局附魔
        if (isFirstTurn)
        {
            if (DestinyPassives.FirstTurnBonusDraw > 0)
            {
                var bonus = deckPile.Draw(DestinyPassives.FirstTurnBonusDraw);
                yield return DrawStaggered(bonus);
                Debug.Log($"[命格] 梅花 Lv1：开局额外抽 {bonus.Count} 张");
            }
            if (DestinyPassives.ClubOpeningEnchant > 0)
                GrantRandomEnchantsToCards(new List<CardData>(handArea.HandCards), DestinyPassives.ClubOpeningEnchant, 1);
        }
        yield return new WaitForSeconds(phaseDelay);

        // 触发流程事件
        OnPlayerTurnStart?.Invoke();

        // 阶段 3: 回合开始效果 (遗物 OnPlayerTurnStart, 附魔 OnTurnStart)
        OnPlayerPhaseChanged?.Invoke("TurnStartEffects");
        var ctx = BuildContext();
        relicProcessor?.ApplyEffects("OnPlayerTurnStart", ctx);
        enchantmentSystem?.OnTurnStart();

        // 首回合额外触发 OnCombatStart
        if (isFirstTurn)
        {
            player?.OnCombatStart();
            foreach (var unit in enemies) unit?.OnCombatStart();
            relicProcessor?.ApplyEffects("OnCombatStart", ctx);
            enchantmentSystem?.OnCombatStart();
            isFirstTurn = false;
        }
        yield return new WaitForSeconds(phaseDelay);

        // 阶段 4: 等待玩家输入
        CanPlayerAct = true;
        OnCanPlayerActChanged?.Invoke(true);

        // 嘲讽生效中：强制把选中目标设为嘲讽者
        int tauntIdx = GetTauntTargetIndex();
        if (tauntIdx >= 0) SetTarget(tauntIdx);

        OnEnemyIntentReady?.Invoke();
        OnPlayerPhaseChanged?.Invoke("WaitInput");
    }

    /// <summary>逐张把牌加入手牌（每张之间留间隔，让 UI 逐张播放抽牌动画）</summary>
    private IEnumerator DrawStaggered(List<CardData> cards)
    {
        if (cards == null) yield break;
        foreach (var card in cards)
        {
            AddToHand(new List<CardData> { card });
            if (drawCardInterval > 0f)
                yield return new WaitForSeconds(drawCardInterval);
        }
    }

    /// <summary>
    /// 加入手牌；手牌已满时超出的牌放回牌堆（视为没抽出来）。返回真正进入手牌的牌。
    /// triggerOnDraw=false 用于「搜寻 / 回手」这类重新入手的牌（不重复触发「抽到时」效果）。
    /// </summary>
    private List<CardData> AddToHand(List<CardData> cards, bool triggerOnDraw = true)
    {
        if (cards == null || cards.Count == 0) return new List<CardData>();

        var added = new List<CardData>(cards);

        var overflow = handArea.AddCards(cards);
        if (overflow != null && overflow.Count > 0)
        {
            deckPile.ReturnToDeck(overflow);
            foreach (var c in overflow) added.Remove(c);
        }

        if (triggerOnDraw)
            foreach (var c in added) enchantmentSystem?.OnDrawn(c);

        return added;
    }

    // 保留原方法兼容性（不再直接使用）
    private void ApplyTurnStartEffects()
    {
        if (IsBattleOver) return;
        var ctx = BuildContext();
        relicProcessor?.ApplyEffects("OnTurnStart", ctx);
        enchantmentSystem?.OnTurnStart();
        if (isFirstTurn)
        {
            player?.OnCombatStart();
            foreach (var unit in enemies) unit?.OnCombatStart();
            relicProcessor?.ApplyEffects("OnCombatStart", ctx);
            enchantmentSystem?.OnCombatStart();
            isFirstTurn = false;
        }
    }

    /// <summary>待处理的「搜寻」次数（打出后从弃牌堆取牌回手）</summary>
    public int PendingSearchCount { get; private set; } = 0;

    /// <summary>弃牌堆快照</summary>
    public List<CardData> GetDiscardCards() => deckPile != null ? deckPile.GetDiscard() : new List<CardData>();

    /// <summary>牌堆快照（查看用）</summary>
    public List<CardData> GetDeckCards() => deckPile != null ? deckPile.GetCards() : new List<CardData>();

    /// <summary>某张牌是否还在弃牌堆里</summary>
    public bool IsInDiscard(CardData card) => deckPile != null && deckPile.HasInDiscard(card);

    /// <summary>从弃牌堆取一张牌回手（搜寻）</summary>
    public bool MoveDiscardToHand(CardData card)
    {
        if (card == null || deckPile == null || handArea == null) return false;
        if (!deckPile.TakeFromDiscard(card)) return false;

        // 记录来源，让抽牌动画从弃牌堆飞出
        fromDiscardToHand.Add(card);
        AddToHand(new List<CardData> { card }, triggerOnDraw: false);
        if (PendingSearchCount > 0) PendingSearchCount--;
        OnHandChanged?.Invoke();
        Debug.Log($"[搜寻] {card.DisplayName} 从弃牌堆回到手牌");
        return true;
    }

    // 本次从弃牌堆回到手牌、还没播过动画的牌
    private readonly HashSet<CardData> fromDiscardToHand = new HashSet<CardData>();

    /// <summary>该牌本次是否来自弃牌堆（消费一次，供 UI 决定飞行动画起点）</summary>
    public bool ConsumeFromDiscard(CardData card)
    {
        if (card == null) return false;
        return fromDiscardToHand.Remove(card);
    }

    /// <summary>
    /// 当前选中的牌能否打出（含牌型与诅咒限制）；reason 返回原因
    /// </summary>
    public bool CanPlaySelection(out string reason)
    {
        reason = null;
        if (handArea == null) { reason = "无手牌"; return false; }

        var selected = handArea.GetSelectedCards();
        if (selected.Count == 0) { reason = "未选牌"; return false; }

        // 诅咒「禁锢」：无法被打出
        foreach (var c in selected)
            if (c.IsUnplayable) { reason = $"{c.DisplayName} 被禁锢，无法打出"; return false; }

        // 事件封禁的花色：无法打出
        foreach (var c in selected)
            if (sealedSuits.Contains((int)c.EffectiveSuit))
            { reason = $"{DestinyInfo.PlainSuitName(c.EffectiveSuit)}被封印，无法打出"; return false; }

        // 诅咒「割裂」：不能与同点数的牌一起打出
        foreach (var c in selected)
        {
            if (!c.HasRift) continue;
            foreach (var o in selected)
                if (o != c && o.EffectiveRank == c.EffectiveRank)
                { reason = $"{c.DisplayName} 被割裂，不能与同点数一起打出"; return false; }
        }

        if (!HandEvaluator.Evaluate(selected).IsValid) { reason = "无效牌型"; return false; }

        // 需要敌人目标的牌：没选中敌人时不能打出
        if (!HasTarget && SelectionNeedsTarget()) { reason = "没有选中对象"; return false; }
        return true;
    }

    /// <summary>
    /// 当前选中的牌是否需要敌人目标（有伤害 / 吸血就必须先选敌人）
    /// </summary>
    public bool SelectionNeedsTarget()
    {
        if (handArea == null) return false;

        var selected = handArea.GetSelectedCards();
        if (selected == null || selected.Count == 0) return false;

        var effects = PreviewSelectedEffects();
        if (effects == null) return false;

        foreach (var e in effects)
            if (e.effectType == HandEffectTable.EffectType.Damage ||
                e.effectType == HandEffectTable.EffectType.HealPercentOfDamage)
                return true;

        return false;
    }

    /// <summary>
    /// 尝试出牌：识别牌型 -> 结算效果 -> 牌回牌堆
    /// </summary>
    public bool TryPlayCards()
    {
        if (!IsPlayerTurn || IsBattleOver) return false;

        var selected = handArea.GetSelectedCards();
        if (selected.Count == 0)
        {
            Debug.Log("没有选中任何牌");
            return false;
        }

        if (!CanPlaySelection(out var reason))
        {
            Debug.Log($"[出牌失败] {reason}");
            return false;
        }

        var result = HandEvaluator.Evaluate(selected);
        if (!result.IsValid)
        {
            Debug.Log("无效的牌型组合");
            return false;
        }

        // 保存当前牌型结果供 OnHandType 触发使用
        currentHandTypeResult = result;

        // 敌人被动：被非弱点牌型攻击 → 自身 +1 力量
        EnemyAbilities.ApplyWeaknessOnPlay(result.type);

        // 花色命运：统计本场打出的花色
        CountPlayedSuits(selected);

        // 命格：打出主命格花色的牌积攒命运之力
        if (runData != null && runData.HasMainDestiny)
        {
            int mainGain = 0;
            foreach (var card in selected)
                if ((int)card.suit == runData.mainDestinySuit) mainGain++;

            // 事件：本场战斗无法积攒命运之力
            if (mainGain > 0 && battleFateGainDisabled) mainGain = 0;

            // 遗物「命运共鸣」：充能翻倍
            if (mainGain > 0 && relicSystem != null)
                mainGain = Mathf.RoundToInt(mainGain * relicSystem.GetMultiplier("MultiplyFatePower", 1f));

            // 事件：下场战斗命运之力积攒 +N
            if (mainGain > 0) mainGain += battleFateBonus;

            if (mainGain > 0)
            {
                runData.fatePower = Mathf.Min(RunData.FatePowerMax, runData.fatePower + mainGain);
                runData.NotifyDestinyChanged();
                OnDestinyChanged?.Invoke();
            }
        }

        // 重置本次出牌的「返回手牌」请求
        returnToHandCount = 0;

        var effects = HandEffectTable.GetEffects(result);

        // 出牌加成（顺风耳/同花顺之巅/花色调和/附魔共鸣/黑桃被动/暴怒）——与预览共用
        playsThisTurn++;
        ApplyPlayBonuses(effects, selected, result, playsThisTurn, consumeRage: true);

        // 命格：红桃 Lv3 吸血（百分比集中在 DestinyPassiveSystem）
        if (DestinyPassiveSystem.HasHeartLifesteal(runData))
        {
            effects.Add(new HandEffectTable.HandEffect
            {
                effectType = HandEffectTable.EffectType.HealPercentOfDamage,
                value = DestinyPassiveSystem.HeartLv3LifestealPercent,
                description = $"回复伤害的 {DestinyPassiveSystem.HeartLv3LifestealPercent}%"
            });
        }

        Debug.Log($"玩家打出 {result.type}（{string.Join(", ", selected.ConvertAll(c => c.DisplayName))}）");

        // 遗物触发：OnCardPlayed（应用即时效果）
        var ctx = BuildContext();
        ctx.PlayedCards = selected;
        relicProcessor?.ApplyEffects("OnCardPlayed", ctx);

        // 附魔触发：OnPlay
        foreach (var card in selected)
        {
            enchantmentSystem?.OnCardPlayed(card, result, effects);
        }

        // 遗物触发：OnHandType（牌型增强效果，如 DuplicateHandType/TransformHandType/ApplyPoison等）
        relicProcessor?.ApplyEffects("OnHandType", ctx);

        // 处理牌型重复触发（从 processor context 读取）
        if (ctx.IsHandTypeDuplicated)
        {
            ctx.IsHandTypeDuplicated = false; // 重置
        }

        // 附魔触发：OnHandType
        foreach (var card in selected)
        {
            enchantmentSystem?.OnHandTypePlayed(card, result, effects);
        }

        // 先把打出的牌从手牌移除（这样「出牌后抽牌」能按出牌后的手牌数判断余量）
        handArea.RemoveCards(selected);

        // 应用基础牌型效果（伤害/防御/抽牌/治疗，包含遗物额外伤害）
        relicProcessor.ApplyHandTypeEffects(effects, ctx);

        // 遗物「跳动的心脏」：红桃回 1 血；黑桃失去 1 血并额外造成 1 次 1 点伤害
        if (relicSystem != null && relicSystem.GetFlatBonus("BeatingHeart", 0) > 0)
        {
            foreach (var card in selected)
            {
                if (card.EffectiveSuit == Suit.Heart)
                {
                    player.Heal(1);
                }
                else if (card.EffectiveSuit == Suit.Spade)
                {
                    player.TakeDamage(1);
                    var heartTarget = GetEnemy();
                    if (heartTarget != null && !heartTarget.IsDead)
                        heartTarget.TakeDamage(player.DealDamage(1), player);
                }
            }
        }

        // 燃料等：其他牌打出时，检查仍留在手牌中的附魔
        enchantmentSystem?.OnOtherCardsPlayed(selected);

        // 附魔「搜寻」：打出后可从弃牌堆取牌回手
        foreach (var card in selected)
            if (card.HasSearch) PendingSearchCount++;

        // 回手（每张牌每回合最多一次）/ 连对专家：把部分牌返回手牌而非回牌堆
        var toReturn = new List<CardData>();
        var remaining = new List<CardData>();
        foreach (var card in selected)
        {
            if (card.IsReturnToHand && !returnedThisTurn.Contains(card))
            {
                toReturn.Add(card);
                returnedThisTurn.Add(card);
            }
            else
            {
                remaining.Add(card);
            }
        }
        if (returnToHandCount > 0)
        {
            int n = Mathf.Min(returnToHandCount, remaining.Count);
            toReturn.AddRange(remaining.Take(n));
            remaining = remaining.Skip(n).ToList();
        }
        returnToHandCount = 0;

        deckPile.Discard(remaining);

        // 回手的牌：动画从弃牌堆位置飞出
        var actuallyReturned = AddToHand(toReturn, triggerOnDraw: false);
        foreach (var c in actuallyReturned) fromDiscardToHand.Add(c);

        CheckBattleEnd();

        // 触发流程事件
        OnHandChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// 弃牌重抽：丢弃选中牌，抽取等量新牌（每回合限一次）
    /// </summary>
    public bool TryMulligan()
    {
        if (!IsPlayerTurn || IsBattleOver) return false;
        if (HasUsedMulligan)
        {
            Debug.Log("本回合已使用过弃牌重抽");
            return false;
        }

        var selected = handArea.GetSelectedCards();
        if (selected.Count == 0)
        {
            Debug.Log("没有选中任何牌");
            return false;
        }

        int discardCount = selected.Count;
        handArea.RemoveCards(selected);

        // 先抽新牌（此时弃掉的牌还没回堆，避免立刻抽回同一张）
        var drawn = deckPile.Draw(discardCount);
        AddToHand(drawn);

        // 再把弃掉的牌放回牌堆并洗牌
        deckPile.Discard(selected);
        enchantmentSystem?.OnCardsDiscarded(selected);

        mulligansUsed++;
        handArea.ClearSelection();

        Debug.Log($"弃牌重抽：丢弃 {discardCount} 张，抽取 {drawn.Count} 张新牌");

        // 触发流程事件
        OnHandChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// 结束玩家回合 -> 进入敌方回合（协程：分阶段执行）
    /// </summary>
    public void EndPlayerTurn()
    {
        if (!IsPlayerTurn || IsBattleOver) return;
        CoroutineRunner.Instance.StartCoroutine(EndPlayerTurnRoutine());
    }

    private IEnumerator EndPlayerTurnRoutine()
    {
        CanPlayerAct = false; // 回合结束，禁止操作
        OnCanPlayerActChanged?.Invoke(false);

        // 回合结束清空选中目标：下回合需要重新点选敌人
        ClearTarget();

        // 阶段 1: 手牌放回牌堆（带「保留」的牌留在手牌）
        OnPlayerPhaseChanged?.Invoke("DiscardHand");
        var allHandCards = new List<CardData>(handArea.HandCards);
        var toReturn = allHandCards.Where(c => !c.IsRetain).ToList();
        if (toReturn.Count > 0)
        {
            handArea.RemoveCards(toReturn);
            deckPile.Discard(toReturn);
            enchantmentSystem?.OnCardsDiscarded(toReturn);
            Debug.Log($"回合结束，{toReturn.Count} 张手牌放回牌堆并洗牌");
        }
        yield return new WaitForSeconds(phaseDelay);

        // 阶段 2: 回合结束效果 (遗物 OnTurnEnd, 附魔 OnTurnEnd)
        OnPlayerPhaseChanged?.Invoke("TurnEndEffects");
        var ctx = BuildContext();
        relicProcessor?.ApplyEffects("OnTurnEnd", ctx);
        relicProcessor?.ApplyEffects("OnPlayerTurnEnd", ctx);
        enchantmentSystem?.OnTurnEnd();

        // 「同化之心」：恢复本回合临时变成红桃的牌
        relicProcessor?.RestoreHeartified();

        // 玩家状态效果回合结束结算 (DoT 已在敌方回合处理，这里只做层数递减等)
        player?.OnTurnEnd();

        // 事件永久效果：每回合结束自伤（读状态栏的「血契」层数）
        int selfDamage = player.GetStatusAmount(StatusEffectType.SelfDamage);
        if (selfDamage > 0 && !player.IsDead)
        {
            player.TakeDamage(selfDamage);
            CheckBattleEnd();
            if (IsBattleOver) yield break;
        }

        yield return new WaitForSeconds(phaseDelay);

        IsPlayerTurn = false;
        handArea.ClearSelection();

        Debug.Log("--- 玩家结束回合 ---");

        // 触发流程事件
        OnPlayerTurnEnd?.Invoke();
        OnHandChanged?.Invoke();

        // 回合结束提示
        OnTurnEndPrompt?.Invoke(true);

        // 开始敌方回合
        if (OnRequestEnemyActionDelay != null)
        {
            OnRequestEnemyActionDelay(() => CoroutineRunner.Instance.StartCoroutine(ExecuteEnemyTurnRoutine()));
        }
else
            {
                CoroutineRunner.Instance.StartCoroutine(Roguelike.CoroutineRunner.SafeCoroutine(ExecuteEnemyTurnRoutine()));
            }
    }

    private IEnumerator ExecuteEnemyTurnRoutine()
    {
        if (IsBattleOver) yield break;

        Debug.Log($"--- 敌方回合开始（{enemies.Count} 个敌人）---");

        // 触发流程事件
        OnEnemyTurnStart?.Invoke();

        // 阶段 1: 回合开始效果 (遗物 OnEnemyTurnStart, 附魔 OnTurnStart)
        OnEnemyPhaseChanged?.Invoke("TurnStartEffects");
        var ctx = BuildContext();
        relicProcessor?.ApplyEffects("OnEnemyTurnStart", ctx);
        enchantmentSystem?.OnTurnStart();
        yield return new WaitForSeconds(phaseDelay);

        // 阶段 2: DoT 逐个结算 (中毒/灼烧/再生)，逐个敌人处理
        // 放在清除防御之前，让敌人残留的防御能吸收这部分伤害
        OnEnemyPhaseChanged?.Invoke("DoT");
        foreach (var unit in enemies)
        {
            if (unit == null || unit.IsDead) continue;

            var dotEffects = unit.StatusEffects.GetAllEffects()
                .Where(e => e.type == StatusEffectType.Poison ||
                           e.type == StatusEffectType.Burn ||
                           e.type == StatusEffectType.Regeneration)
                .ToList();

            foreach (var eff in dotEffects)
            {
                ProcessSingleDoT(unit, eff);
                yield return new WaitForSeconds(dotInterval);
            }

            // DoT 可能直接把敌人打死：先等受击飘字/震屏播完，再结算胜负
            // （否则 CheckBattleEnd 会立刻切到奖励面板，飘字和震动因为面板隐藏而播不出来）
            if (unit.IsDead)
                yield return new WaitForSeconds(deathVisualDelay);

            // DoT 可能直接把敌人打死：立刻结算并中断敌方行动（不能让它继续出手）
            CheckBattleEnd();
            if (IsBattleOver) yield break;
        }
        yield return new WaitForSeconds(phaseDelay);

        // 阶段 3: 清除旧防御（DoT 结算完才清）
        OnEnemyPhaseChanged?.Invoke("ClearDefense");
        foreach (var unit in enemies)
            if (unit != null && !unit.IsDead) unit.ClearDefense();
        yield return new WaitForSeconds(phaseDelay);

        // 阶段 4: 敌人依次执行各自已选定的意图
        // 用快照遍历：召唤会往 enemies 里加人，直接 foreach 会抛「集合被修改」；
        // 快照也让新召唤出来的敌人本回合不行动。
        OnEnemyPhaseChanged?.Invoke("Act");
        foreach (var unit in new List<BattleUnit>(enemies))
        {
            if (unit == null || unit.IsDead) continue;

            var intent = GetIntentOf(unit);
            if (intent == null) continue;

            // 出手前摇：让 UI 播放预警（放大 / 变红 / 意图闪烁），停一小会儿再真正行动
            OnEnemyActing?.Invoke(unit, intent);
            if (enemyWindupDelay > 0f) yield return new WaitForSeconds(enemyWindupDelay);

            yield return ExecuteOneIntent(intent, unit);

            CheckBattleEnd();
            if (IsBattleOver) yield break;

            yield return new WaitForSeconds(hitInterval);
        }

        // 敌人行动完毕：先隐藏意图，等下次刷新（玩家回合开始）再出现
        OnEnemyIntentHidden?.Invoke();

        CheckBattleEnd();
        if (IsBattleOver) yield break;

        // 选择下一回合的意图（玩家回合开始时触发显示）
        SelectAllEnemyIntents();
        // 不在这里触发 OnEnemyIntentReady，由玩家回合开始时触发

        // 阶段 5: 回合结束效果 (遗物 OnTurnEnd, 附魔 OnTurnEnd)
        OnEnemyPhaseChanged?.Invoke("TurnEndEffects");
        relicProcessor?.ApplyEffects("OnTurnEnd", ctx);
        enchantmentSystem?.OnTurnEnd();

        // 阶段 5: 回合结束结算 (层数递减等)
        OnEnemyPhaseChanged?.Invoke("TurnEnd");
        foreach (var unit in enemies)
            unit?.OnTurnEnd();
        yield return new WaitForSeconds(phaseDelay);

        // 触发流程事件
        OnEnemyTurnEnd?.Invoke();

        // 回合结束提示
        OnTurnEndPrompt?.Invoke(false);

        if (!IsBattleOver)
        {
            // 敌方回合结束停顿后开始玩家回合
            if (OnRequestEnemyActionDelay != null)
            {
                OnRequestEnemyActionDelay(StartPlayerTurn);
            }
            else
            {
                StartPlayerTurn();
            }
        }
    }

    /// <summary>
    /// 处理单个 DoT 效果（不经过 OnTurnEnd 统一处理，实现逐个飘字）
    /// </summary>
    private void ProcessSingleDoT(BattleUnit target, StatusEffect effect)
    {
        switch (effect.type)
        {
            case StatusEffectType.Poison:
            case StatusEffectType.Burn:
                target.TakeDamage(effect.amount);
                break;
            case StatusEffectType.Regeneration:
                target.Heal(effect.amount);
                break;
        }

        // 灼烧衰减：默认减半；遗物「灼热之核」改为减少 1/4
        if (effect.type == StatusEffectType.Burn)
        {
            bool slow = relicSystem != null && relicSystem.GetFlatBonus("SlowBurnDecay", 0) > 0;
            int reduce = slow ? Mathf.Max(1, effect.amount / 4) : (effect.amount - effect.amount / 2);
            target.RemoveStatus(StatusEffectType.Burn, reduce);
        }

        // 中毒/灼烧/再生的「持续回合」在这里递减。
        // 它们在回合开始时结算，如果拖到回合结束才递减，回合内新加的状态会白白少一回合。
        // （其余状态仍在 StatusEffectSystem.OnTurnEnd 里递减）
        target.StatusEffects.TickDotDuration(effect.type);
    }

    /// <summary>
    /// 检查战斗是否结束
    /// </summary>
    private void CheckBattleEnd()
    {
        // 嘲讽者已阵亡：清掉玩家身上的嘲讽（否则状态栏会一直挂着）
        RefreshTauntStatus();

        // 吞噬怪已阵亡：归还它吞掉的牌
        EnemyAbilities.ReturnSwallowedCardsOfDeadEnemies();

        if (player.IsDead)
        {
            // 命格：红桃 Lv2 免死（每局一次）——数值集中在 DestinyPassiveSystem
            if (DestinyPassiveSystem.CanDeathSave(runData))
            {
                runData.heartDeathSaveUsed = true;
                player.CurrentHp = Mathf.Max(1, DestinyPassiveSystem.GetDeathSaveHealAmount(player));
                Debug.Log($"[命格] 红桃 Lv2：致命伤害无效，回复 {Mathf.RoundToInt(DestinyPassiveSystem.HeartLv2DeathSavePercent * 100)}% 生命");
                runData.NotifyDestinyChanged();
                OnDestinyChanged?.Invoke();
                return;
            }

            // 遗物「不死鸟」：每局一次复活
            if (runData != null && relicSystem != null && !runData.phoenixUsed &&
                relicSystem.GetFlatBonus("PhoenixRevive", 0) > 0)
            {
                runData.phoenixUsed = true;
                int pct = relicSystem.GetFlatBonus("PhoenixRevive", 30);
                player.CurrentHp = Mathf.RoundToInt(player.MaxHp * pct / 100f);
                Debug.Log("[遗物] 不死鸟：复活");
                return;
            }

            // 遗物「护命铜钱」：致命伤害时若金币 > 100，扣 50 金币并回复 20% 最大生命（可重复）
            if (runData != null && relicSystem != null &&
                relicSystem.GetFlatBonus("CoinGuard", 0) > 0 && runData.Gold > 100)
            {
                runData.Gold -= 50;
                player.CurrentHp = Mathf.RoundToInt(player.MaxHp * 0.2f);
                Debug.Log("[遗物] 护命铜钱：消耗 50 金币，回复 20% 生命");
                return;
            }

            IsBattleOver = true;
            IsWin = false;
            Debug.Log("=== 战斗结束：玩家失败 ===");
            OnBattleOver?.Invoke(false);
            relicProcessor?.ApplyEffects("OnBattleLose", BuildContext());

            // 战斗结束清理状态效果与临时附魔
            player?.OnCombatEnd();
            foreach (var unit in enemies) unit?.OnCombatEnd();
            runData.ClearTempEnchantments();
        }
        else if (GetAliveEnemies().Count == 0)
        {
            IsBattleOver = true;
            IsWin = true;
            Debug.Log("=== 战斗结束：玩家胜利 ===");

            // 还有出牌特效在飞：等打到敌人再结算胜利
            if (deferBattleEnd)
            {
                pendingWinEnd = true;
                return;
            }

            FinishWin();
        }
        else
        {
            // 还有敌人活着：如果当前目标已阵亡，清空选中（需玩家重新点选）
            EnsureValidTarget();
        }
    }

    // 延迟胜利结算（等出牌特效命中）
    private bool deferBattleEnd = false;
    private bool pendingWinEnd = false;

    /// <summary>是否延迟胜利结算（出牌特效飞行中由 UI 设置）</summary>
    public void SetDeferBattleEnd(bool value)
    {
        deferBattleEnd = value;
    }

    /// <summary>特效播放完毕后调用：结算被延迟的胜利</summary>
    public void FlushDeferredBattleEnd()
    {
        if (pendingWinEnd) FinishWin();
    }

    private void FinishWin()
    {
        pendingWinEnd = false;
        OnBattleOver?.Invoke(true);
        relicProcessor?.ApplyEffects("OnBattleWin", BuildContext());

        // 战斗结束清理状态效果与临时附魔
        player?.OnCombatEnd();
        foreach (var unit in enemies) unit?.OnCombatEnd();
        runData.ClearTempEnchantments();
    }

    /// <summary>
    /// 选中状态变化时调用（由 CardUI 点击时触发）
    /// </summary>
    public void OnCardSelectionChanged()
    {
        var preview = handArea.PreviewHandType();
        OnPreviewChanged?.Invoke(preview);
        OnSelectionChanged?.Invoke();
    }

    /// <summary>
    /// 构建遗物效果上下文
    /// </summary>
    /// <summary>构建遗物效果上下文（供 Handler 使用）</summary>
        public RelicEffectProcessor.Context BuildContext()
    {
        return new RelicEffectProcessor.Context
        {
            Player = player,
            Enemy = GetEnemy(),
            HandArea = handArea,
            DeckPile = deckPile,
            RunData = runData,
            EnchantmentSystem = enchantmentSystem,
            NextPlayDamageMultiplier = GetNextPlayDamageMultiplier(),
            HandTypeResult = currentHandTypeResult
        };
    }

    /// <summary>
    /// 选中状态变化
    /// </summary>
    public event Action OnSelectionChanged;

    // 下次出牌伤害倍率（药水/附魔用）
    private float nextPlayDamageMultiplier = 1f;

    public void SetNextPlayDamageMultiplier(float mult)
    {
        nextPlayDamageMultiplier = mult;
    }

public float GetNextPlayDamageMultiplier()
        {
            float mult = nextPlayDamageMultiplier;
            nextPlayDamageMultiplier = 1f; // 重置
            return mult;
        }

    // ===== 战斗开始设置（命格被动 + 事件/遗物开局效果）=====

    /// <summary>战斗开始设置：命格开局被动 + 事件/遗物的开局效果</summary>
    private void ApplyBattleStartEffects()
    {
        if (runData == null) return;

        playsThisTurn = 0;

        // 命格：战斗开始类被动（数值/判定在 DestinyPassiveSystem，开局状态在 DestinyPassiveSystem）
        DestinyPassives.ApplyCombatStart(runData, player);

        // 事件永久效果：每场战斗开局 +N 力量
        if (runData.permanentStartStrength > 0)
            player.AddStatus(StatusEffectType.Strength, runData.permanentStartStrength, -1);

        // 事件永久效果：每回合结束自伤（显示在状态栏）
        if (runData.permanentTurnEndSelfDamage > 0)
            player.AddStatus(StatusEffectType.SelfDamage, runData.permanentTurnEndSelfDamage, -1);

        // 事件「下场战斗」效果：显示在状态栏
        if (battleDrawBonus > 0)
            player.AddStatus(StatusEffectType.DrawBonus, battleDrawBonus, -1);
        if (battleMulliganDelta != 0)
            player.AddStatus(StatusEffectType.MulliganBonus, battleMulliganDelta, -1);
        if (battleNoMulligan)
            player.AddStatus(StatusEffectType.NoMulligan, 1, -1);

        // 封禁的花色：显示在状态栏
        foreach (var s in sealedSuits)
            player.AddStatus(StatusEffectType.SuitSeal, (Suit)s, 1, -1);

        // 梅花事件：永久 / 本场修正，显示在状态栏
        if (runData.permanentStraightDrawBonus > 0)
            player.AddStatus(StatusEffectType.StraightDrawBonus, runData.permanentStraightDrawBonus, -1);

        if (battleStraightDraw > 0)
            player.AddStatus(StatusEffectType.StraightDrawTemp, battleStraightDraw, -1);

        if (runData.permanentFlushDrawBonus > 0)
            player.AddStatus(StatusEffectType.FlushDrawBonus, runData.permanentFlushDrawBonus, -1);

        if (runData.permanentFlushDamageBonus > 0)
            player.AddStatus(StatusEffectType.FlushDamageBonus, runData.permanentFlushDamageBonus, -1);

        if (Mathf.Abs(runData.permanentDamageMultiplier - 1f) > 0.001f)
            player.AddStatus(StatusEffectType.DamageMultiplierMod,
                Mathf.RoundToInt(runData.permanentDamageMultiplier * 100f), -1);

        if (battleFateBonus > 0)
            player.AddStatus(StatusEffectType.FatePowerBonus, battleFateBonus, -1);

        if (battleFateGainDisabled)
            player.AddStatus(StatusEffectType.FateGainSeal, 1, -1);

        // 事件挑战：显示「已失去血量」（层数 = 已失去，上限见悬停说明）
        if (runData.challengeHpLimit >= 0)
            player.SetStatus(StatusEffectType.Challenge, playerHpLostThisBattle, -1);

        // 事件「下场战斗开局获得状态」
        if (runData.nextBattleStartStatuses != null && runData.nextBattleStartStatuses.Count > 0)
        {
            foreach (var s in runData.nextBattleStartStatuses)
            {
                if (s == null || string.IsNullOrEmpty(s.status)) continue;
                if (System.Enum.TryParse<StatusEffectType>(s.status, true, out var st))
                {
                    player.AddStatus(st, s.stacks, s.turns);
                    Debug.Log($"[事件] 开局获得状态 {st} ×{s.stacks}");
                }
            }
            runData.nextBattleStartStatuses.Clear();
        }

        // 方块 Lv2 / 梅花 Lv1 / 梅花 Lv3 已由 DestinyPassives.ApplyCombatStart 处理（见本方法开头）

        // 遗物「圣锤」：三条 + 比它大 1 的牌 视为四条
        HandEvaluator.HammerEnabled = relicSystem != null && relicSystem.GetFlatBonus("Hammer", 0) > 0;

        // 事件：下场战斗开局回满命运之力
        if (runData.nextBattleFateFull)
        {
            runData.nextBattleFateFull = false;
            runData.fatePower = RunData.FatePowerMax;
            runData.NotifyDestinyChanged();
            OnDestinyChanged?.Invoke();
            Debug.Log("[事件] 开局命运之力已回满");
        }
    }

    /// <summary>遗物「瘟疫之心」：施加中毒时的额外层数</summary>
    public int GetPoisonBonus() => relicSystem != null ? relicSystem.GetFlatBonus("PoisonBonus", 0) : 0;

    /// <summary>释放主命格主动技能（需命运之力攒满）—— 逻辑在 DestinySkillSystem</summary>
    public bool ActivateDestinySkill() => DestinySkills.Activate();

    /// <summary>给指定牌各随机附魔（用于梅花顿悟/开局）</summary>
    private void GrantRandomEnchantsToCards(List<CardData> cards, int count, int minTier)
    {
        if (cards == null || count <= 0) return;
        var pool = Roguelike.Data.ConfigLoader.Config.enchantments.FindAll(e => Roguelike.Data.RarityUtil.Tier(e.rarity) >= minTier);
        if (pool.Count == 0) return;

        var shuffled = new List<CardData>(cards);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, shuffled.Count);
            var t = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = t;
        }
        int n = Mathf.Min(count, shuffled.Count);
        for (int i = 0; i < n; i++)
        {
            var c = shuffled[i];
            var ench = pool[UnityEngine.Random.Range(0, pool.Count)];
            runData?.AddEnchantment(c.EnchantRank, c.EnchantSuit, ench.id);
        }
        NotifyEnchantmentsChanged();
    }
}