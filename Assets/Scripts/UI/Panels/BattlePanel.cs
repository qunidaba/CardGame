using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;
using Roguelike.Core;

/// <summary>
/// 战斗界面面板（使用TextMeshPro）
/// 显示血量、防御、牌堆、手牌、牌型预览等
/// 精确事件驱动更新：数据变了直接更新 UI + 飘字
/// </summary>
public class BattlePanel : BasePanel
{
    [Header("玩家信息")]
    public TextMeshProUGUI playerHpText;
    public TextMeshProUGUI playerDefenseText;

    [Header("敌人信息")]
    public TextMeshProUGUI enemyHpText;
    public TextMeshProUGUI enemyDefenseText;
    public TextMeshProUGUI enemyNameText;      // 敌人名字
    public TextMeshProUGUI enemyIntentText;    // 敌人意图显示

    [Header("敌人立绘")]
    public Image enemyImage;                       // 旧版单敌人立绘（多敌人槽位启用后会被隐藏）
    public string enemyImageFolder = "Enemies/";   // Resources 下的文件夹
    public bool enemyImageFallbackById = true;     // 未配置 image 时用敌人 id 当文件名
    public UnityEngine.U2D.SpriteAtlas enemyAtlas; // 敌人图集（可选，按名称取图）
    public float enemyFrameRate = 8f;              // 多帧动画默认帧率（敌人可单独覆盖）

    [Header("多敌人布局（以 enemyAnchor 为锚点，像手牌一样向两边展开）")]
    public Vector2 enemyAnchor = new Vector2(0f, 230f);           // 敌人整体的锚点位置
    public float enemyUnitWidth = 240f;                          // 单个敌人占位宽
    public float enemyUnitHeight = 330f;                         // 单个敌人占位高
    public float enemyUnitSpacing = 12f;                         // 敌人之间的间距
    public Vector2 enemyArtSize = new Vector2(120f, 120f);       // 立绘默认尺寸（敌人配置未指定时）

    [Header("血条 / 护盾图标")]
    public Image playerHpFill;          // 玩家血条（Image Type = Filled / Horizontal）
    public Image enemyHpFill;           // 敌人血条
    public Image playerShieldIcon;      // 玩家护盾图标（留空则只显示文字）
    public Image enemyShieldIcon;       // 敌人护盾图标

    [Header("回合数")]
    public TextMeshProUGUI turnCountText;   // 留空则运行时在顶部中央创建

    [Header("图标资源路径（Resources 下，例如 shield/Wood Shield；留空则不改动已赋值的 Sprite）")]
    public string shieldIconPath = "";                  // 留空 = Resources/Battle/DefenseIcon（玩家/敌人共用）
    public string selectionSpritePath = "Battle/UIS";   // 敌人选中提示框图集（多帧则逐帧播放）

    [Header("分辨率自适应")]
    public Image backgroundImage;          // 背景图（留空则自动找子物体 "Background"）
    public bool responsiveLayout = true;   // 关掉就用预制体里写死的位置

    [Header("牌堆信息")]
    public TextMeshProUGUI deckCountText;      // 牌堆数量（点击查看牌堆内容）
    public TextMeshProUGUI discardCountText;   // 弃牌堆数量（点击查看弃牌堆内容；留空则并入 deckCountText）

    [Header("牌型预览")]
    public TextMeshProUGUI handTypeText;
    public TextMeshProUGUI effectPreviewText;

    [Header("手牌容器")]
    public Transform handContainer;
    public GameObject cardPrefab;

    [Header("手牌布局")]
    public float handMaxWidth = 2200f;   // 手牌最大总宽，超出则自动缩小间隔/重叠
    public float handMaxSpacing = 10f;   // 牌少时的最大间隔（调大可让手牌铺得更开）
    public float handShiftDuration = 0.34f;  // 已有手牌挪到新位置的时长（建议≈抽牌飞行总时长，让两边同时结束）
    public float handShiftDelay = 0f;        // 新牌落地后额外等待多久再挪

    [Header("抽牌动画")]
    public RectTransform deckPileAnchor;          // 牌堆位置（留空则用「牌堆: N」文本的位置）
    public RectTransform discardPileAnchor;       // 弃牌堆位置（留空则用「弃牌: M」文本的位置；搜寻回手从此飞出）
    public float drawFlyUpHeight = 160f;          // 起飞阶段向上飞的高度（像素）
    public float drawFlyUpDuration = 0.12f;       // 起飞阶段时长（秒）
    public float drawFlyToHandDuration = 0.22f;   // 飞向手牌阶段时长（秒）
    public float drawArrivePopScale = 1.15f;      // 到达手牌时弹出缩放
    public float drawAnimStagger = 0.06f;         // 一次新增多张时，每张起飞错开的间隔

    [Header("药水栏布局")]
    public Vector2 potionBarStart = new Vector2(-800f, 300f);  // 第一个药水槽位置（遗物栏下方最左）
    public float potionSlotSpacing = 150f;

    [Header("按钮")]
    public Button playButton;
    public Button endTurnButton;
    public Button mulliganButton;
    public TextMeshProUGUI mulliganButtonText;

    [Header("金币与遗物")]
    public TextMeshProUGUI goldText;
    public RelicSlotUI[] relicSlots = new RelicSlotUI[6]; // 固定6个槽位

    [Header("花色命运")]
    public TextMeshProUGUI suitHudText;   // 显示本场四花色计数 + 事件预告（为空时运行时自动创建）

    [Header("战斗反馈")]
    public GameObject damageTextPrefab;      // 伤害/治疗/防御飘字预制体
    public Transform damageTextContainer;    // 飘字容器
    public GameObject turnEndPromptPrefab;   // 回合结束提示预制体
    public Transform promptContainer;        // 提示容器

    [Header("伤害飘字")]
    public float floatingTextDuration = 0.9f;      // 显示时长
    public float floatingTextRise = 90f;           // 上浮像素
    public float floatingTextMinFontSize = 34f;    // 最小字号（预制体字号低于此值则抬高）
    public float floatingTextPopScale = 1.35f;     // 弹出时的最大缩放
    public bool floatingTextOutline = true;        // 是否加描边（背景再花也看得清）

    [Header("屏震（受击震动幅度）")]
    public float shakePlayerHitMagnitude = 9f;     // 玩家受击震动幅度（像素）
    public float shakePlayerHitDuration = 0.18f;   // 玩家受击震动时长（秒）
    public float shakeEnemyHitMagnitude = 5f;      // 敌人受击震动幅度（像素）
    public float shakeEnemyHitDuration = 0.12f;    // 敌人受击震动时长（秒）

    [Header("伤害飘字分档（数值越大越醒目）")]
    public int damageTier2Threshold = 15;          // 达到该伤害进入第 2 档
    public int damageTier3Threshold = 30;          // 达到该伤害进入第 3 档
    public float damageTier2FontSize = 44f;
    public float damageTier3FontSize = 58f;
    public float damageTier2PopScale = 1.5f;
    public float damageTier3PopScale = 1.75f;
    public Color damageTier1Color = new Color(1f, 0.38f, 0.32f);
    public Color damageTier2Color = new Color(1f, 0.62f, 0.18f);
    public Color damageTier3Color = new Color(1f, 0.85f, 0.2f);

    [Header("出牌飞行演出")]
    public float cardFlyDuration = 0.34f;    // 单张牌飞行时长
    public float cardFlyStagger = 0.05f;     // 多张牌依次起飞的间隔
    public float cardFlyArc = 220f;          // 弧线高度基准（调大弧度更弯）
    public float cardFlyArcPerWidth = 0.35f; // 横向距离每像素追加的弧高
    public bool cardFlyTrail = true;         // 是否显示拖尾
    public bool cardFlyImpact = true;        // 命中时是否闪光
    public Sprite cardTrailSprite;           // 单张拖尾贴图（横向渐变条；留空则退回用牌面图）
    public string cardTrailSpritePath = "";  // 单张拖尾贴图 Resources 路径（cardTrailSprite 为空时用）

    [Header("出牌拖尾（跟随式 ribbon）")]
    public Sprite[] cardTrailFrames;                // 可选序列帧，循环播放作为拖尾贴图
    public string cardTrailFramesPath = "";         // Resources 文件夹路径，例如 fx/trail（按文件名排序加载）
    public float cardTrailFrameRate = 16f;          // 序列帧播放帧率
    public Vector2 cardTrailSize = new Vector2(170f, 90f); // 拖尾厚度参考（宽度随速度自动拉伸）

    [Header("出牌投射物（牌打出后变成的效果，朝敌人飞）")]
    public Sprite cardProjectileSprite;             // 投射物贴图；留空则依次用 序列帧[0] / 拖尾贴图 / 运行时圆光
    public string cardProjectileSpritePath = "";    // Resources 路径
    public Vector2 cardProjectileSize = new Vector2(90f, 90f);
    public bool cardProjectileSpin = true;          // 是否随飞行方向旋转（横向特效开，放射类关）

    [Header("命中特效（投射物击中敌人时）")]
    public GameObject hitEffectPrefab;              // 命中时实例化的预制体（优先；UI 特效或 UIParticle）
    public bool hitEffectWorldSpace = false;        // 预制体是否用世界空间（3D 粒子用，不挂到 UI 容器）
    public float hitEffectLifetime = 2f;            // 预制体自动销毁时间
    public Sprite[] hitEffectFrames;                // 命中序列帧（无预制体时用）
    public string hitEffectFramesPath = "";         // 序列帧 Resources 文件夹路径（按文件名排序）
    public float hitEffectFrameRate = 20f;          // 序列帧帧率
    public Vector2 hitEffectSize = new Vector2(160f, 160f);
    public bool hitEffectRandomRotation = true;     // 每次随机旋转，避免重复感

    [Header("状态效果")]
    public StatusEffectBar playerStatusBar;
    public StatusEffectBar enemyStatusBar;

    private BattleManager battleManager;
    private int lastHandVersion = -1;
    private Button fateButton;
    private Button modifyButton;
    private Button destinyButton;
    private Button activeSkillButton;
    private TextMeshProUGUI destinyHudText;

    private PotionSlotUI[] potionSlots = new PotionSlotUI[3];   // 药水槽（运行时创建在遗物栏下方）

    /// <summary>
    /// 注入BattleManager引用，绑定按钮事件和订阅事件
    /// </summary>
    public void SetBattleManager(BattleManager manager)
    {
        // 先取消订阅旧的（防止重复绑定）
        if (battleManager != null)
        {
            UnsubscribeEvents();
        }

        battleManager = manager;

        playButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.RemoveAllListeners();
        mulliganButton.onClick.RemoveAllListeners();

        playButton.onClick.AddListener(OnPlayClicked);

        endTurnButton.onClick.AddListener(() =>
        {
            battleManager.EndPlayerTurn();
        });

        mulliganButton.onClick.AddListener(() =>
        {
            battleManager.TryMulligan();
        });

        // 自动创建容器（如果未指定）
        EnsureContainers();

        // 注册对象池
        RegisterPools();

        // 清掉上一场可能残留的表现层（面板被隐藏时协程中断，飘字/特效/手牌会卡住）
        ResetBattleVisuals();

        // 构建多敌人槽位（会隐藏预制体里的旧单敌人 UI）
        BuildEnemySlots();

        EnsureSuitHud();
        EnsureModifyButton();
        EnsureDestinyHud();
        EnsurePileTextClicks();
        EnsurePotionBar();
        ApplyShieldIcon();
        SetupHpBar(playerHpFill, new Color(0.85f, 0.30f, 0.30f));
        SetupHpBar(enemyHpFill, new Color(0.90f, 0.40f, 0.25f));
        EnsureTurnCount();

        // 分辨率自适应：把写死的位置改成贴边（只做一次）
        ApplyResponsiveLayout();

// 订阅精确事件（携带 delta）
        SubscribeEvents();

        // 初始化UI
        RefreshAll();
        
        // 战斗开始时隐藏敌人意图
        if (enemyIntentText != null)
            enemyIntentText.text = "";
    }

    /// <summary>
    /// 重置战斗表现层。上一场战斗面板被隐藏（SetActive(false)）时协程会中断，
    /// 导致飘字 / 投射物 / 命中特效 / 手牌移动卡在半途，换场时必须统一清掉。
    /// </summary>
    private void ResetBattleVisuals()
    {
        // 停掉可能还在跑的协程
        if (handDriver != null) { StopCoroutine(handDriver); handDriver = null; }
        if (enemyAnimRoutine != null) { StopCoroutine(enemyAnimRoutine); enemyAnimRoutine = null; }
        if (shakeRoutine != null) { StopCoroutine(shakeRoutine); shakeRoutine = null; }

        activeProjectiles = 0;
        deferredEnemyVisuals.Clear();
        pendingDrawAnims.Clear();
        deferredOldShift.Clear();

        // 清空飘字 / 投射物 / 命中特效 / 抽牌幻影容器
        ClearChildren(damageTextContainer);
        ClearChildren(promptContainer);

        // 清空手牌 UI 与布局缓存
        foreach (var kv in cardUis)
        {
            if (kv.Value == null) continue;
            kv.Value.transform.SetParent(null, false);
            Destroy(kv.Value.gameObject);
        }
        cardUis.Clear();
        handTargets.Clear();
        handMoves.Clear();

        if (handContainer != null)
        {
            for (int i = handContainer.childCount - 1; i >= 0; i--)
            {
                var child = handContainer.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        lastHandVersion = -1;
    }

    /// <summary>立刻清空容器（先脱离父级，避免延迟销毁影响本帧统计）</summary>
    private static void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void EnsureContainers()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // 飘字容器
        if (damageTextContainer == null)
        {
            var go = new GameObject("FloatingTextContainer");
            go.transform.SetParent(canvas.transform, false);
            damageTextContainer = go.transform;
        }

        // 提示容器
        if (promptContainer == null)
        {
            var go = new GameObject("PromptContainer");
            go.transform.SetParent(canvas.transform, false);
            promptContainer = go.transform;
        }

        // 这两个容器是 Canvas 下的独立节点，必须盖在战斗面板之上
        // （战斗面板每次 ShowPanel 都会被提到最前，所以这里重新提到最后）
        damageTextContainer.SetAsLastSibling();
        promptContainer.SetAsLastSibling();
    }

    private void SubscribeEvents()
    {
        if (battleManager == null) return;

        // 精确数值变化事件（携带 delta：+治疗/得盾，-伤害/失盾）
        battleManager.OnPlayerHpChanged += OnPlayerHpChanged;
        battleManager.OnPlayerMaxHpChanged += OnPlayerMaxHpChanged;
        battleManager.OnPlayerDefenseChanged += OnPlayerDefenseChanged;
        battleManager.OnEnemyHpChanged += OnEnemyHpChanged;
        battleManager.OnEnemyMaxHpChanged += OnEnemyMaxHpChanged;
        battleManager.OnEnemyDefenseChanged += OnEnemyDefenseChanged;
        battleManager.OnPlayerDamageTaken += OnPlayerDamageTaken;
        battleManager.OnEnemyDamageTaken += OnEnemyDamageTaken;

        // 状态效果事件
        battleManager.OnPlayerStatusAdded += OnPlayerStatusChanged;
        battleManager.OnPlayerStatusRemoved += OnPlayerStatusChanged;
        battleManager.OnPlayerStatusChanged += OnPlayerStatusChanged;
        battleManager.OnEnemyStatusAdded += OnEnemyStatusChanged;
        battleManager.OnEnemyStatusRemoved += OnEnemyStatusChanged;
        battleManager.OnEnemyStatusChanged += OnEnemyStatusChanged;
        battleManager.OnEnemyListChanged += OnEnemyListChanged;

        battleManager.OnDeckCountChanged += count => RefreshPileCounts();
        battleManager.OnDiscardCountChanged += count => RefreshPileCounts();
        battleManager.OnHandChanged += OnHandChanged;

        battleManager.OnGoldChanged += gold => goldText.text = $"金币: {gold}";
        battleManager.OnRelicsChanged += RefreshRelics;
        battleManager.OnPotionsChanged += RefreshPotions;

        // 流程事件
        battleManager.OnPlayerTurnStart += OnPlayerTurnStart;
        battleManager.OnPlayerTurnEnd += OnPlayerTurnEnd;
        battleManager.OnEnemyTurnStart += OnEnemyTurnStart;
        battleManager.OnEnemyTurnEnd += OnEnemyTurnEnd;
        battleManager.OnSelectionChanged += OnSelectionChanged;
        battleManager.OnPreviewChanged += OnPreviewChanged;
        battleManager.OnBattleOver += OnBattleOver;
        battleManager.OnTurnEndPrompt += OnTurnEndPrompt;
        battleManager.OnRequestEnemyActionDelay += OnRequestEnemyActionDelay;
        battleManager.OnCanPlayerActChanged += OnCanPlayerActChanged;
        battleManager.OnEnemyIntentReady += OnEnemyIntentReady;
        battleManager.OnTargetChanged += OnTargetChanged;
        battleManager.OnEnemyIntentHidden += OnEnemyIntentHidden;
        battleManager.OnSuitTallyChanged += OnSuitTallyChanged;
        battleManager.OnCardEnchantmentsChanged += ForceRefreshHand;
        battleManager.OnDestinyChanged += RefreshDestinyHud;
    }

    private void UnsubscribeEvents()
    {
        if (battleManager == null) return;

        battleManager.OnPlayerHpChanged -= OnPlayerHpChanged;
        battleManager.OnPlayerMaxHpChanged -= OnPlayerMaxHpChanged;
        battleManager.OnPlayerDefenseChanged -= OnPlayerDefenseChanged;
        battleManager.OnEnemyHpChanged -= OnEnemyHpChanged;
        battleManager.OnEnemyMaxHpChanged -= OnEnemyMaxHpChanged;
        battleManager.OnEnemyDefenseChanged -= OnEnemyDefenseChanged;
        battleManager.OnPlayerDamageTaken -= OnPlayerDamageTaken;
        battleManager.OnEnemyDamageTaken -= OnEnemyDamageTaken;

        // 状态效果事件
        battleManager.OnPlayerStatusAdded -= OnPlayerStatusChanged;
        battleManager.OnPlayerStatusRemoved -= OnPlayerStatusChanged;
        battleManager.OnPlayerStatusChanged -= OnPlayerStatusChanged;
        battleManager.OnEnemyStatusAdded -= OnEnemyStatusChanged;
        battleManager.OnEnemyStatusRemoved -= OnEnemyStatusChanged;
        battleManager.OnEnemyStatusChanged -= OnEnemyStatusChanged;
        battleManager.OnEnemyListChanged -= OnEnemyListChanged;

        battleManager.OnDeckCountChanged -= count => RefreshPileCounts();
        battleManager.OnDiscardCountChanged -= count => RefreshPileCounts();
        battleManager.OnHandChanged -= OnHandChanged;

        battleManager.OnGoldChanged -= gold => goldText.text = $"金币: {gold}";
        battleManager.OnRelicsChanged -= RefreshRelics;
        battleManager.OnPotionsChanged -= RefreshPotions;

        battleManager.OnPlayerTurnStart -= OnPlayerTurnStart;
        battleManager.OnPlayerTurnEnd -= OnPlayerTurnEnd;
        battleManager.OnEnemyTurnStart -= OnEnemyTurnStart;
        battleManager.OnEnemyTurnEnd -= OnEnemyTurnEnd;
        battleManager.OnSelectionChanged -= OnSelectionChanged;
        battleManager.OnPreviewChanged -= OnPreviewChanged;
        battleManager.OnBattleOver -= OnBattleOver;
        battleManager.OnTurnEndPrompt -= OnTurnEndPrompt;
        battleManager.OnRequestEnemyActionDelay -= OnRequestEnemyActionDelay;
        battleManager.OnCanPlayerActChanged -= OnCanPlayerActChanged;
        battleManager.OnEnemyIntentReady -= OnEnemyIntentReady;
        battleManager.OnTargetChanged -= OnTargetChanged;
        battleManager.OnEnemyIntentHidden -= OnEnemyIntentHidden;
        battleManager.OnSuitTallyChanged -= OnSuitTallyChanged;
        battleManager.OnCardEnchantmentsChanged -= ForceRefreshHand;
        battleManager.OnDestinyChanged -= RefreshDestinyHud;
    }

    // --- 事件处理：数据变化 = 即时刷新 UI + 飘字 ---

    private void OnPlayerHpChanged(BattleUnit unit, int delta)
    {
        if (delta == 0) return;
        var player = battleManager.GetPlayer();
        playerHpText.text = $"{player.CurrentHp}/{player.MaxHp}";
        UpdateHpBars();

        // 挑战状态图标要实时反映「已失去血量」
        if (playerStatusBar != null) playerStatusBar.Refresh(player);

        if (delta > 0)
            ShowFloatingText(GetUnitWorldPosition(unit, false), $"+{delta}", Color.green);
        // 掉血飘字改由 OnPlayerDamageTaken 负责（显示本次实际伤害）
    }

    private void OnPlayerMaxHpChanged(BattleUnit unit, int maxHp)
    {
        if (battleManager == null) return;
        var player = battleManager.GetPlayer();
        if (player == null) return;
        playerHpText.text = $"HP: {player.CurrentHp}/{maxHp}";
        UpdateHpBars();
    }

    private void OnEnemyMaxHpChanged(BattleUnit unit, int maxHp)
    {
        FindSlot(unit)?.RefreshHp();
    }

    private void OnPlayerDamageTaken(BattleUnit unit, int damage)
    {
        if (damage <= 0) return;
        var tier = GetDamageTier(damage);
        ShowFloatingText(GetUnitWorldPosition(unit, false), damage.ToString(), tier.color, tier.fontSize, tier.popScale);
        FlashText(playerHpText, Color.red);
        ShakeScreen(shakePlayerHitMagnitude, shakePlayerHitDuration);
    }

    private void OnEnemyHpChanged(BattleUnit unit, int delta)
    {
        if (delta == 0) return;

        var slot = FindSlot(unit);
        if (slot == null) return;

        if (delta > 0)
        {
            slot.RefreshHp();
            ShowFloatingText(slot.ArtworkWorld, $"+{delta}", Color.green);
            return;
        }

        // 掉血：血条等投射物命中后再刷新。
        // 注意：这里不要删槽位！TakeDamage 里 OnHpChanged 先于 OnDamageTaken 触发，
        // 如果这里先删了，紧接着的 OnEnemyDamageTaken 就找不到槽位，飘字/震动会被吞掉。
        // 阵亡删除统一交给 OnEnemyDamageTaken（那里才有飘字和震屏）。
        QueueOrRunEnemyVisual(() =>
        {
            if (slot == null) return;
            slot.RefreshHp();
        }, refreshEnemyUi: false);
    }

    private void OnEnemyDamageTaken(BattleUnit unit, int damage)
    {
        if (damage <= 0) return;

        var slot = FindSlot(unit);
        if (slot == null) return;

        Vector3 pos = slot.ArtworkWorld;
        QueueOrRunEnemyVisual(() =>
        {
            var tier = GetDamageTier(damage);
            ShowFloatingText(pos, damage.ToString(), tier.color, tier.fontSize, tier.popScale);
            ShakeScreen(shakeEnemyHitMagnitude, shakeEnemyHitDuration);

            if (slot == null) return;
            if (unit != null && unit.IsDead) RemoveEnemySlot(slot);   // 阵亡：删掉整个槽位
            else slot.RefreshHp();
        }, refreshEnemyUi: false);
    }

    /// <summary>敌人阵亡：删掉整个显示槽位，剩下的重新居中</summary>
    private void RemoveEnemySlot(EnemySlotUI slot)
    {
        if (slot == null) return;

        enemySlots.Remove(slot);
        slot.transform.SetParent(null, false);
        Destroy(slot.gameObject);
        LayoutEnemySlots();

        Debug.Log($"[BattlePanel] 敌人阵亡，移除显示槽位（剩 {enemySlots.Count} 个）");
    }

    /// <summary>按当前槽位数量重新居中排布（有敌人阵亡后调用）</summary>
    private void LayoutEnemySlots()
    {
        int n = enemySlots.Count;
        float totalWidth = n * enemyUnitWidth + Mathf.Max(0, n - 1) * enemyUnitSpacing;
        float startX = -totalWidth * 0.5f + enemyUnitWidth * 0.5f;

        for (int i = 0; i < n; i++)
        {
            var s = enemySlots[i];
            if (s == null) continue;
            if (s.transform is RectTransform rt)
                rt.anchoredPosition = enemyAnchor + new Vector2(startX + i * (enemyUnitWidth + enemyUnitSpacing), 0f);
        }
    }

    // 防御：只显示「获得防御」；失去防御并入伤害飘字，不再单独显示
    private void OnPlayerDefenseChanged(BattleUnit unit, int delta)
    {
        if (delta == 0) return;
        var player = battleManager.GetPlayer();
        UpdateShield(playerDefenseText, playerShieldIcon, player.Defense);
        if (delta > 0)
            ShowFloatingText(GetUnitWorldPosition(unit, true), $"+{delta}", Color.cyan);
    }

    private void OnEnemyDefenseChanged(BattleUnit unit, int delta)
    {
        if (delta == 0) return;

        var slot = FindSlot(unit);
        if (slot == null) return;

        if (delta > 0)
        {
            slot.RefreshShield();
            ShowFloatingText(slot.ShieldWorld, $"+{delta}", Color.cyan);
            return;
        }

        // 失去防御（多为被打掉）：等投射物命中后再刷新
        QueueOrRunEnemyVisual(() => { if (slot != null) slot.RefreshShield(); }, refreshEnemyUi: false);
    }

    // ===== 敌人受击表现（血条/飘字/闪红/屏震）延迟到投射物命中 =====

    private int activeProjectiles;
    private bool enemyUiRefreshQueued;
    private readonly List<System.Action> deferredEnemyVisuals = new List<System.Action>();

    private void QueueOrRunEnemyVisual(System.Action visual, bool refreshEnemyUi)
    {
        if (activeProjectiles > 0)
        {
            if (refreshEnemyUi) enemyUiRefreshQueued = true;
            if (visual != null) deferredEnemyVisuals.Add(visual);
        }
        else
        {
            if (refreshEnemyUi) RefreshEnemyUi();
            visual?.Invoke();
        }
    }

    private void RefreshEnemyUi()
    {
        if (battleManager == null) return;
        var enemy = battleManager.GetEnemy();
        if (enemy == null) return;

        if (enemyHpText != null) enemyHpText.text = $"{enemy.CurrentHp}/{enemy.MaxHp}";
        UpdateHpBars();
        RefreshEnemySlots();
    }

    // ===== 敌人立绘 / 帧动画 =====

    private Sprite[] enemyFrames;
    private int enemyFrameIndex;
    private Coroutine enemyAnimRoutine;
    private Vector2 enemyImageBaseSize;
    private Vector2 enemyImageBasePos;
    private bool enemyImageBaseCaptured;

    private void RefreshEnemyImage()
    {
        StopEnemyAnim();
        enemyFrames = null;

        // 多敌人显示已接管：不再驱动隐藏的旧立绘（省一次动画协程 + 贴图加载）
        if (enemySlots.Count > 0) return;

        if (enemyImage == null || battleManager == null) return;

        ApplyEnemyImageLayout();

        enemyFrames = ResolveEnemyFrames();
        if (enemyFrames == null || enemyFrames.Length == 0)
        {
            enemyImage.enabled = false;
            return;
        }

        enemyImage.enabled = true;
        enemyFrameIndex = 0;
        enemyImage.sprite = enemyFrames[0];

        if (enemyFrames.Length > 1 && isActiveAndEnabled)
            enemyAnimRoutine = StartCoroutine(EnemyAnimRoutine());
    }

    /// <summary>按配置里的宽/高/缩放/偏移调整立绘（0 表示用预制体默认）</summary>
    private void ApplyEnemyImageLayout()
    {
        var cfg = battleManager != null ? battleManager.CurrentEnemyData : null;
        if (cfg == null) return;

        var rt = enemyImage.rectTransform;
        if (!enemyImageBaseCaptured)
        {
            enemyImageBaseCaptured = true;
            enemyImageBaseSize = rt.sizeDelta;
            enemyImageBasePos = rt.anchoredPosition;
        }

        float scale = cfg.imageScale > 0f ? cfg.imageScale : 1f;
        float w = (cfg.imageWidth > 0f ? cfg.imageWidth : enemyImageBaseSize.x) * scale;
        float h = (cfg.imageHeight > 0f ? cfg.imageHeight : enemyImageBaseSize.y) * scale;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = enemyImageBasePos + new Vector2(cfg.imageOffsetX, cfg.imageOffsetY);
    }

    private void StopEnemyAnim()
    {
        if (enemyAnimRoutine != null)
        {
            StopCoroutine(enemyAnimRoutine);
            enemyAnimRoutine = null;
        }
    }

    private System.Collections.IEnumerator EnemyAnimRoutine()
    {
        var cfg = battleManager != null ? battleManager.CurrentEnemyData : null;
        float rate = (cfg != null && cfg.frameRate > 0f) ? cfg.frameRate : enemyFrameRate;
        var wait = new WaitForSeconds(1f / Mathf.Max(1f, rate));

        while (true)
        {
            yield return wait;
            if (enemyImage == null || enemyFrames == null || enemyFrames.Length == 0) yield break;
            enemyFrameIndex = (enemyFrameIndex + 1) % enemyFrames.Length;
            enemyImage.sprite = enemyFrames[enemyFrameIndex];
        }
    }

    /// <summary>
    /// 取敌人的帧序列，优先级：
    /// 1) cfg.frames 显式帧名（图集/切片）
    /// 2) cfg.image 指定路径：Resources.LoadAll 取全部子图（多帧）或单图
    /// 3) 图集里按名称取单张
    /// </summary>
    private Sprite[] ResolveEnemyFrames()
        => ResolveEnemyFrames(battleManager != null ? battleManager.CurrentEnemyData : null);

    private Sprite[] ResolveEnemyFrames(Roguelike.Data.EnemyData cfg)
    {
        if (cfg == null) return null;

        // 1) 显式帧列表
        if (cfg.frames != null && cfg.frames.Count > 0)
        {
            var list = new List<Sprite>();
            foreach (var n in cfg.frames)
            {
                if (string.IsNullOrEmpty(n)) continue;
                Sprite s = null;
                if (enemyAtlas != null) s = enemyAtlas.GetSprite(n);
                if (s == null) s = Resources.Load<Sprite>(n);
                if (s != null) list.Add(s);
            }
            if (list.Count > 0) return list.ToArray();
        }

        string name = !string.IsNullOrEmpty(cfg.image) ? GetLastSegment(cfg.image) : cfg.id.ToString();
        string path = ResolveEnemyImagePath(cfg);

        // 2) Resources：文件夹里的多张图 / 多切片贴图
        if (!string.IsNullOrEmpty(path))
        {
            var all = Resources.LoadAll<Sprite>(path);
            if (all != null && all.Length > 0)
            {
                System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
                return all;
            }
        }

        // 3) 图集单张
        if (enemyAtlas != null)
        {
            var s = enemyAtlas.GetSprite(name);
            if (s != null) return new[] { s };
        }

        Debug.LogWarning($"[BattlePanel] 找不到敌人图片: 名称='{name}' 路径='{path}'");
        return null;
    }

    /// <summary>
    /// 取某个状态动画（遁地/出来）的帧序列：显式帧名优先，其次按文件夹路径 LoadAll。
    /// 没配就返回 null（该状态没有动画）。
    /// </summary>
    private Sprite[] ResolveStateFrames(string imagePath, List<string> frameNames)
    {
        if (string.IsNullOrEmpty(imagePath) && (frameNames == null || frameNames.Count == 0))
            return null;

        // 1) 显式帧名（图集/切片）
        if (frameNames != null && frameNames.Count > 0)
        {
            var list = new List<Sprite>();
            foreach (var n in frameNames)
            {
                if (string.IsNullOrEmpty(n)) continue;
                Sprite s = null;
                if (enemyAtlas != null) s = enemyAtlas.GetSprite(n);
                if (s == null) s = Resources.Load<Sprite>(n);
                if (s != null) list.Add(s);
            }
            if (list.Count > 0) return list.ToArray();
        }

        // 2) Resources 文件夹
        if (!string.IsNullOrEmpty(imagePath))
        {
            string path = imagePath.Contains("/") ? imagePath : enemyImageFolder + imagePath;
            var all = Resources.LoadAll<Sprite>(path);
            if (all != null && all.Length > 0)
            {
                System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
                return all;
            }

            if (enemyAtlas != null)
            {
                var s = enemyAtlas.GetSprite(GetLastSegment(imagePath));
                if (s != null) return new[] { s };
            }

            Debug.LogWarning($"[BattlePanel] 找不到敌人状态动画: 路径='{path}'");
        }

        return null;
    }

    /// <summary>敌人图片路径：优先配置里的 image，其次（可选）用敌人 id 当文件名</summary>
    private string ResolveEnemyImagePath(Roguelike.Data.EnemyData cfg)
    {
        if (cfg == null) return null;

        if (!string.IsNullOrEmpty(cfg.image))
            return cfg.image.Contains("/") ? cfg.image : enemyImageFolder + cfg.image;

        return enemyImageFallbackById ? enemyImageFolder + cfg.id : null;
    }

    private static string GetLastSegment(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        int i = path.LastIndexOf('/');
        return i >= 0 ? path.Substring(i + 1) : path;
    }

    // ===== 多敌人槽位 =====

    private readonly List<EnemySlotUI> enemySlots = new List<EnemySlotUI>();

    /// <summary>
    /// 构建敌人显示：不用容器/布局组，直接以 enemyAnchor 为锚点，
    /// 像手牌一样把每个敌人向两边居中展开。
    /// </summary>
    private void BuildEnemySlots()
    {
        if (battleManager == null) return;

        // 清掉旧的
        for (int i = 0; i < enemySlots.Count; i++)
        {
            if (enemySlots[i] == null) continue;
            enemySlots[i].transform.SetParent(null, false);
            Destroy(enemySlots[i].gameObject);
        }
        enemySlots.Clear();

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var template = enemyStatusBar;   // 旧状态栏作为模板（图标预制体/图标尺寸）

        var units = battleManager.GetEnemies();
        var datas = battleManager.GetEnemyDatas();
        var shieldSprite = GetShieldIconSprite();
        var selectionFrames = GetSelectionFrames();

        for (int i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            var data = (i < datas.Count) ? datas[i] : null;

            var slotGo = new GameObject($"Enemy{i}", typeof(RectTransform));
            slotGo.transform.SetParent(transform, false);
            var rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(enemyUnitWidth, enemyUnitHeight);

            var slot = slotGo.AddComponent<EnemySlotUI>();
            slot.Build(font, shieldSprite, template, enemyUnitWidth, enemyUnitHeight, enemyArtSize, selectionFrames);

            int idx = i;
            slot.OnClicked = index => { if (battleManager != null) battleManager.SetTarget(index); };
            slot.SetUnit(idx, unit, data,
                ResolveEnemyFrames(data),
                ResolveStateFrames(data != null ? data.burrowImage : null, data != null ? data.burrowFrames : null),
                ResolveStateFrames(data != null ? data.emergeImage : null, data != null ? data.emergeFrames : null),
                enemyFrameRate);

            enemySlots.Add(slot);
        }

        // 像手牌一样居中展开（阵亡移除后也会复用同一套排布）
        LayoutEnemySlots();

        // 诊断：把锚点 / 槽位实际坐标 / 世界坐标打出来，方便核对位置
        var panelRt = transform as RectTransform;
        string slotInfo = "-";
        if (enemySlots.Count > 0 && enemySlots[0] != null)
        {
            var srt = enemySlots[0].transform as RectTransform;
            slotInfo = $"槽位0 anchoredPos={srt.anchoredPosition} 世界坐标={srt.position}";
        }
        Debug.Log($"[BattlePanel][BUILD-5] 敌人={enemySlots.Count} 锚点={enemyAnchor} 单元={enemyUnitWidth}x{enemyUnitHeight} 立绘={enemyArtSize} 面板={panelRt.rect.size} {slotInfo}");

        // 旧的单敌人 UI 隐藏（新显示接管）
        DisableLegacyEnemyUi();
        RefreshEnemySlots();
    }

    /// <summary>隐藏预制体里的旧单敌人 UI（多敌人槽位启用后不再使用）</summary>
    private void DisableLegacyEnemyUi()
    {
        // 只隐藏旧 UI，不置空引用：
        // enemyStatusBar / enemyShieldIcon 等还要作为新敌人显示的模板复用，
        // 置空会导致第二场战斗起状态栏图标/护盾图标丢失。
        HideGo(enemyImage != null ? enemyImage.gameObject : null);
        HideGo(enemyHpText != null ? enemyHpText.gameObject : null);
        HideGo(enemyHpFill != null ? enemyHpFill.gameObject : null);
        HideGo(enemyDefenseText != null ? enemyDefenseText.gameObject : null);
        HideGo(enemyNameText != null ? enemyNameText.gameObject : null);
        HideGo(enemyIntentText != null ? enemyIntentText.gameObject : null);
        HideGo(enemyShieldIcon != null ? enemyShieldIcon.gameObject : null);
        HideGo(enemyStatusBar != null ? enemyStatusBar.gameObject : null);
    }

    private static void HideGo(GameObject go)
    {
        if (go != null) go.SetActive(false);
    }

    // ===== 分辨率自适应 =====

    // 预制体里所有位置都是按 1920x1080、以屏幕中心为原点写死的
    private const float DesignW = 1920f;
    private const float DesignH = 1080f;

    private bool responsiveApplied;
    private Vector2 lastPanelSize;

    private static RectTransform Rt(Component c) => c != null ? c.transform as RectTransform : null;

    /// <summary>
    /// 把预制体里「按 1920x1080 中心偏移」摆放的元素改成贴到对应边，
    /// 这样换宽高比时它们跟着屏幕边缘走，不会跑出屏幕。
    /// 只在第一次战斗时执行一次（之后锚点会自己跟着分辨率走）。
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        if (responsiveApplied || !responsiveLayout) return;
        responsiveApplied = true;

        MakeBackgroundCover();

        // 顶部：遗物栏贴左上、金币贴右上
        for (int i = 0; i < relicSlots.Length; i++)
            SnapToEdge(Rt(relicSlots[i]), 0f, 1f);
        SnapToEdge(Rt(goldText), 1f, 1f);

        // 底部按钮（居中贴底）——先摆好，下面要用它们的实际位置算安全边距
        SnapToEdge(Rt(playButton), 0.5f, 0f);
        SnapToEdge(Rt(endTurnButton), 0.5f, 0f);
        SnapToEdge(Rt(mulliganButton), 0.5f, 0f);

        // 牌堆/弃牌计数：16:9 保持设计位置；窄屏时按按钮边缘往角落让位，避免被压住
        PinPileCounters();

        SnapToEdge(Rt(playerHpText), 0.5f, 0f);
        SnapToEdge(Rt(playerHpFill), 0.5f, 0f);
        SnapToEdge(Rt(playerDefenseText), 0.5f, 0f);
        SnapToEdge(Rt(playerShieldIcon), 0.5f, 0f);
        SnapToEdge(Rt(playerStatusBar), 0.5f, 0f);
        SnapToEdge(Rt(handContainer), 0.5f, 0f);

        Debug.Log("[BattlePanel] 已应用分辨率自适应布局");
    }

    /// <summary>
    /// 把元素从「设计画布中心偏移」改成贴到指定边（边距保持不变）。
    /// anchorX: 0=贴左 / 0.5=水平居中 / 1=贴右；anchorY: 0=贴下 / 0.5=垂直居中 / 1=贴上
    /// 因为 pivot 不变、只换锚点，所以在 1920x1080 下位置完全不变。
    /// </summary>
    private static void SnapToEdge(RectTransform rt, float anchorX, float anchorY)
    {
        if (rt == null) return;

        Vector2 d = rt.anchoredPosition;                 // 预制体里的中心偏移（设计坐标）
        float left = DesignW * 0.5f + d.x;
        float right = DesignW * 0.5f - d.x;
        float top = DesignH * 0.5f - d.y;
        float bottom = DesignH * 0.5f + d.y;

        rt.anchorMin = new Vector2(anchorX, anchorY);
        rt.anchorMax = new Vector2(anchorX, anchorY);
        rt.anchoredPosition = new Vector2(
            anchorX <= 0f ? left : (anchorX >= 1f ? -right : d.x),
            anchorY <= 0f ? bottom : (anchorY >= 1f ? -top : d.y));
    }

    /// <summary>
    /// 牌堆 / 弃牌计数：
    ///   16:9（或更宽）保持预制体里的设计位置（正好和按钮留 20px 间距）；
    ///   屏幕变窄时按钮会相对边缘往中间靠，所以按按钮边缘把计数文字往角落让位。
    /// </summary>
    private void PinPileCounters()
    {
        float w = transform is RectTransform panelRt ? panelRt.rect.width : DesignW;
        if (w <= 1f) w = DesignW;

        const float textW = 150f;   // DeckCountText / DiscardCountText 宽
        const float btnHalf = 75f;  // 按钮半宽
        const float gap = 20f;      // 与按钮之间的最小间距
        const float bottom = 340f;  // 距底边（= 设计 y -200 换算）

        float mulliganX = -459.47f, endTurnX = 300f;
        var mrt = Rt(mulliganButton); if (mrt != null) mulliganX = mrt.anchoredPosition.x;
        var ert = Rt(endTurnButton); if (ert != null) endTurnX = ert.anchoredPosition.x;

        // 重抽按钮的左边缘 / 结束回合按钮的右边缘（相对面板左边）
        float mulliganLeft = w * 0.5f + mulliganX - btnHalf;
        float endTurnRight = w * 0.5f + endTurnX + btnHalf;

        // 设计位置：牌堆文字左边缘距左 256，弃牌文字右边缘距右 415
        float deckMargin = Mathf.Min(256f, mulliganLeft - textW - gap);
        float discMargin = Mathf.Min(415f, w - endTurnRight - textW - gap);

        PinToCorner(Rt(deckCountText), 0f, 0f, Mathf.Max(40f, deckMargin), bottom);
        PinToCorner(Rt(discardCountText), 1f, 0f, Mathf.Max(40f, discMargin), bottom);
    }

    /// <summary>
    /// 把元素钉到某个角（anchorX: 0=左边 / 1=右边；anchorY: 0=下边 / 1=上边），
    /// 边距用固定像素，不随分辨率变化 —— 用于角落里的计数文字。
    /// </summary>
    private static void PinToCorner(RectTransform rt, float anchorX, float anchorY, float marginX, float marginY)
    {
        if (rt == null) return;

        rt.anchorMin = new Vector2(anchorX, anchorY);
        rt.anchorMax = new Vector2(anchorX, anchorY);
        rt.pivot = new Vector2(anchorX, 0.5f);   // 左右贴边对齐，垂直居中
        rt.anchoredPosition = new Vector2(
            anchorX <= 0f ? marginX : -marginX,
            anchorY <= 0f ? marginY : -marginY);
    }

    /// <summary>背景铺满整个面板：保持图片比例放大到覆盖，多出来的部分裁掉（不变形）</summary>
    private void MakeBackgroundCover()
    {
        var bg = backgroundImage;
        if (bg == null)
        {
            var t = transform.Find("Background");
            if (t != null) bg = t.GetComponent<Image>();
            if (bg != null) backgroundImage = bg;   // 缓存
        }
        if (bg == null) return;

        var rt = bg.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var panelRt = transform as RectTransform;
        var sp = bg.sprite;
        if (panelRt == null || sp == null) return;

        float pw = panelRt.rect.width;
        float ph = panelRt.rect.height;
        if (pw <= 1f || ph <= 1f) return;

        float spriteAspect = sp.rect.width / sp.rect.height;
        float panelAspect = pw / ph;

        // 铺满：面板比图宽就加高，否则加宽
        rt.sizeDelta = (panelAspect > spriteAspect)
            ? new Vector2(0f, pw / spriteAspect - ph)
            : new Vector2(ph * spriteAspect - pw, 0f);
    }

    /// <summary>分辨率变化时重新算背景覆盖尺寸（锚点会自己跟着走，不用重算）</summary>
    private void LateUpdate()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;

        Vector2 size = rt.rect.size;
        if (size == lastPanelSize) return;

        lastPanelSize = size;
        MakeBackgroundCover();
        if (responsiveApplied) PinPileCounters();   // 宽度变了要重算角落里的计数文字
    }

    private EnemySlotUI FindSlot(BattleUnit unit)
    {
        if (unit == null) return null;
        for (int i = 0; i < enemySlots.Count; i++)
            if (enemySlots[i] != null && enemySlots[i].Unit == unit) return enemySlots[i];
        return null;
    }

    /// <summary>刷新所有槽位（血条/护盾/状态/选中高亮）</summary>
    private void RefreshEnemySlots()
    {
        if (battleManager == null) return;

        // 投射物还在飞：逻辑层已经扣血 / 清空目标了，但表现要等命中，
        // 否则血条会在特效打到之前就归零。命中后由 OnProjectileFinished 统一同步。
        if (activeProjectiles > 0) return;

        var current = battleManager.GetEnemy();

        // 兜底：万一某个敌人已阵亡但槽位还在（例如直接改血量的调试操作），这里补删一次
        for (int i = enemySlots.Count - 1; i >= 0; i--)
        {
            var s = enemySlots[i];
            if (s == null) { enemySlots.RemoveAt(i); continue; }
            if (s.Unit != null && s.Unit.IsDead) RemoveEnemySlot(s);
        }

        foreach (var s in enemySlots)
        {
            if (s == null) continue;
            s.RefreshAll();
            s.SetSelected(s.Unit == current);
        }
    }

    private void OnProjectileFinished()
    {
        activeProjectiles--;
        if (activeProjectiles > 0) return;

        activeProjectiles = 0;
        if (enemyUiRefreshQueued)
        {
            enemyUiRefreshQueued = false;
            RefreshEnemyUi();
        }
        foreach (var v in deferredEnemyVisuals) v?.Invoke();
        deferredEnemyVisuals.Clear();

        // 命中后统一同步一次（血条 / 选中高亮 / 阵亡移除后的排布）
        RefreshEnemySlots();

        // 投射物已全部命中：这时才播被推迟的抽牌动画
        FlushDeferredDraws();

        // 特效都打完了：结算被延迟的胜利（奖励面板）
        if (battleManager != null)
        {
            battleManager.SetDeferBattleEnd(false);
            battleManager.FlushDeferredBattleEnd();
        }
    }

    // --- 状态效果处理 ---
    private void OnPlayerStatusChanged(StatusEffectType type, int amount)
    {
        if (playerStatusBar != null)
            playerStatusBar.Refresh(battleManager.GetPlayer());
    }

    private void OnEnemyStatusChanged(BattleUnit unit, StatusEffectType type, int amount)
    {
        var slot = FindSlot(unit);
        if (slot == null) return;

        // 敌方状态变化（被吞/被诅咒/被上 debuff/遁地层数被消耗…）也等投射物命中后再刷新，
        // 否则图标会在特效打到之前就变（遁地 buff 提前消失、debuff 提前出现）。
        QueueOrRunEnemyVisual(() =>
        {
            if (slot == null) return;
            slot.RefreshStatus();
            slot.SyncBurrowVisual();   // 遁地/出来动画也在命中那一刻才切
        }, refreshEnemyUi: false);
    }

    /// <summary>敌人列表变化（召唤）：重建所有槽位并重新居中</summary>
    private void OnEnemyListChanged()
    {
        BuildEnemySlots();
        OnEnemyIntentReady();
    }

    private void OnPlayerTurnStart()
    {
        RefreshAll();
        UpdateButtonStates();
    }

    private void OnPlayerTurnEnd()
    {
        UpdateButtonStates();
    }

private void OnEnemyTurnStart()
    {
        UpdateButtonStates();

        // 显示敌人名字
        if (enemyNameText != null && battleManager != null)
        {
            var enemy = battleManager.GetEnemy();
            if (enemy != null)
            {
                enemyNameText.text = enemy.Name;
            }
        }
    }

    private void OnEnemyTurnEnd()
    {
        UpdateButtonStates();
    }

    private void OnHandChanged()
    {
        RefreshHandCards();
        RefreshPileCounts();
        UpdateButtonStates();
    }

    private void OnSelectionChanged()
    {
        UpdateSelectionVisuals();
        UpdatePreview();
        UpdateButtonStates();
    }

    private void OnPreviewChanged(HandTypeResult preview)
    {
        UpdatePreview(preview);
    }

    private void OnBattleOver(bool isWin)
    {
        // 战斗结束由 BattleUI 处理面板切换
    }

    // --- 飘字/动画事件处理 ---

    private void OnTurnEndPrompt(bool isPlayerTurnEnd)
    {
        if (turnEndPromptPrefab != null && promptContainer != null)
        {
            string msg = isPlayerTurnEnd ? "回合结束" : "敌方回合结束";
            var promptObj = Roguelike.Core.PoolManager.Get<RectTransform>("TurnPrompt");
            if (promptObj == null)
            {
                var fallback = Instantiate(turnEndPromptPrefab, promptContainer);
                var tmpTextComp = fallback.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpTextComp != null) tmpTextComp.text = msg;
                Destroy(fallback, 1.5f);
                return;
            }

            promptObj.SetParent(promptContainer, false);
            var tmpText = promptObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText != null) tmpText.text = msg;
            promptObj.gameObject.SetActive(true);

            StartCoroutine(ReturnTurnPrompt(promptObj));
        }
    }

    private System.Collections.IEnumerator ReturnTurnPrompt(RectTransform promptObj)
    {
        yield return new WaitForSeconds(1.5f);
        if (promptObj != null)
            Roguelike.Core.PoolManager.Return("TurnPrompt", promptObj);
    }

    private void OnRequestEnemyActionDelay(System.Action onComplete)
    {
        StartCoroutine(EnemyActionDelayCoroutine(onComplete));
    }

    private System.Collections.IEnumerator EnemyActionDelayCoroutine(System.Action onComplete)
    {
        // 敌方回合开始前停顿 1.5 秒
        yield return new WaitForSeconds(1.5f);
        onComplete?.Invoke();
    }

    private Vector3 GetUnitWorldPosition(BattleUnit unit, bool isDefense)
    {
        if (unit == battleManager?.GetPlayer())
        {
            return isDefense ? playerDefenseText.transform.position : playerHpText.transform.position;
        }

        // 敌人：用对应槽位的位置
        var slot = FindSlot(unit);
        if (slot != null)
            return isDefense ? slot.ShieldWorld : slot.ArtworkWorld;

        if (isDefense && enemyDefenseText != null) return enemyDefenseText.transform.position;
        return GetEnemyAnchorWorld();
    }

    /// <summary>敌人受击锚点：优先用当前目标槽位的立绘</summary>
    private Vector3 GetEnemyAnchorWorld()
    {
        var slot = FindSlot(battleManager != null ? battleManager.GetEnemy() : null);
        if (slot != null) return slot.ArtworkWorld;

        if (enemyImage != null && enemyImage.enabled) return enemyImage.transform.position;
        if (enemyHpText != null) return enemyHpText.transform.position;
        return transform.position;
    }

    // --- 飘字系统 ---

    private const float FloatTextInterval = 0.10f;  // 飘字逐个播放间隔

    private struct FloatTextData { public Vector3 pos; public string text; public Color color; public float fontSize; public float popScale; }
    private readonly Queue<FloatTextData> floatQueue = new Queue<FloatTextData>();
    private bool floatQueueRunning;

    private void ShowFloatingText(Vector3 worldPos, string text, Color color, float fontSize = 0f, float popScale = 0f)
    {
        if (damageTextPrefab == null || damageTextContainer == null) return;
        if (!isActiveAndEnabled) return;   // 面板隐藏时（事件/商店等）不播动画

        floatQueue.Enqueue(new FloatTextData { pos = worldPos, text = text, color = color, fontSize = fontSize, popScale = popScale });
        if (!floatQueueRunning)
            StartCoroutine(ProcessFloatQueue());
    }

    private System.Collections.IEnumerator ProcessFloatQueue()
    {
        floatQueueRunning = true;
        while (floatQueue.Count > 0)
        {
            var f = floatQueue.Dequeue();
            SpawnFloatingText(f.pos, f.text, f.color, f.fontSize, f.popScale);
            yield return new WaitForSeconds(FloatTextInterval);
        }
        floatQueueRunning = false;
    }

    private void SpawnFloatingText(Vector3 worldPos, string text, Color color, float fontSize, float popScale)
    {
        var rectT = Roguelike.Core.PoolManager.Get<RectTransform>("DamageText");
        if (rectT == null)
        {
            // 池未就绪时降级
            GameObject fallback = UnityEngine.Object.Instantiate(damageTextPrefab, damageTextContainer);
            SetupFloatingText(fallback, worldPos, text, color, fontSize, popScale);
            var tmpText = fallback.GetComponentInChildren<TextMeshProUGUI>();
            var rectT2 = fallback.GetComponent<RectTransform>();
            StartCoroutine(FloatingTextAnim(fallback, tmpText, rectT2, popScale));
            return;
        }

        rectT.transform.SetParent(damageTextContainer, false);
        SetupFloatingText(rectT.gameObject, worldPos, text, color, fontSize, popScale);
        var tmp = rectT.GetComponentInChildren<TextMeshProUGUI>();
        StartCoroutine(FloatingTextAnim(rectT.gameObject, tmp, rectT, popScale));
    }

    /// <summary>把飘字文本/颜色/位置等初始化到对象上（从池取出后调用）</summary>
    private void SetupFloatingText(GameObject obj, Vector3 worldPos, string text, Color color, float fontSize, float popScale)
    {
        var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = color;
            tmp.enableWordWrapping = false;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            float size = fontSize > 0f ? fontSize : floatingTextMinFontSize;
            if (tmp.fontSize < size) tmp.fontSize = size;
            if (floatingTextOutline)
            {
                tmp.outlineWidth = 0.22f;
                tmp.outlineColor = Color.black;
            }
        }

        var rect = obj.GetComponent<RectTransform>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && rect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldPos),
                canvas.worldCamera,
                out var localPos
            );
            localPos += new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-10f, 10f));
            rect.anchoredPosition = localPos;
        }

        if (rect != null) rect.localScale = Vector3.one * 0.5f;
        obj.SetActive(true);
    }

    private System.Collections.IEnumerator FloatingTextAnim(GameObject obj, TextMeshProUGUI tmp, RectTransform rect, float popScale)
    {
        float duration = Mathf.Max(0.2f, floatingTextDuration);
        float elapsed = 0f;
        Vector2 startPos = rect != null ? rect.anchoredPosition : Vector2.zero;
        Color startColor = tmp != null ? tmp.color : Color.white;
        float driftX = UnityEngine.Random.Range(-30f, 30f);

        while (elapsed < duration && obj != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (rect != null)
            {
                // 上浮：先快后慢
                float rise = 1f - Mathf.Pow(1f - t, 3f);
                rect.anchoredPosition = startPos + new Vector2(driftX * t, rise * floatingTextRise);

                // 缩放：前 15% 弹出放大，之后回落到 1
                float scale = t < 0.15f
                    ? Mathf.Lerp(0.5f, popScale, t / 0.15f)
                    : Mathf.Lerp(popScale, 1f, (t - 0.15f) / 0.85f);
                rect.localScale = Vector3.one * scale;
            }

            // 透明度：前 60% 保持不透明，之后淡出
            if (tmp != null)
            {
                float a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                tmp.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Clamp01(a));
            }

            yield return null;
        }
        if (obj != null)
            Roguelike.Core.PoolManager.Return("DamageText", obj.GetComponent<RectTransform>());
    }

    /// <summary>按伤害值取分档样式（字号 / 弹出缩放 / 颜色）</summary>
    private (float fontSize, float popScale, Color color) GetDamageTier(int damage)
    {
        if (damage >= damageTier3Threshold) return (damageTier3FontSize, damageTier3PopScale, damageTier3Color);
        if (damage >= damageTier2Threshold) return (damageTier2FontSize, damageTier2PopScale, damageTier2Color);
        return (floatingTextMinFontSize, floatingTextPopScale, damageTier1Color);
    }

    // --- 打击感：闪红 / 屏震 ---

    private void FlashText(TextMeshProUGUI text, Color flashColor)
    {
        if (text == null) return;
        if (!isActiveAndEnabled) return;
        StartCoroutine(FlashTextRoutine(text, flashColor));
    }

    private System.Collections.IEnumerator FlashTextRoutine(TextMeshProUGUI text, Color flashColor)
    {
        Color original = text.color;
        text.color = flashColor;
        float elapsed = 0f;
        const float duration = 0.28f;
        while (elapsed < duration && text != null)
        {
            elapsed += Time.unscaledDeltaTime;
            text.color = Color.Lerp(flashColor, original, elapsed / duration);
            yield return null;
        }
        if (text != null) text.color = original;
    }

    private Coroutine shakeRoutine;
    private Vector2 shakeBasePos;

    private void ShakeScreen(float magnitude, float duration)
    {
        if (!gameObject.activeInHierarchy) return;
        if (shakeRoutine != null) return; // 正在震动，忽略新请求，避免基准点漂移

        var rt = transform as RectTransform;
        if (rt == null) return;

        shakeBasePos = rt.anchoredPosition;
        shakeRoutine = StartCoroutine(ShakeRoutine(magnitude, duration));
    }

    private System.Collections.IEnumerator ShakeRoutine(float magnitude, float duration)
    {
        var rt = transform as RectTransform;
        if (rt == null) { shakeRoutine = null; yield break; }

        Vector2 basePos = shakeBasePos;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float damper = 1f - Mathf.Clamp01(elapsed / duration);
            rt.anchoredPosition = basePos + new Vector2(
                UnityEngine.Random.Range(-1f, 1f) * magnitude * damper,
                UnityEngine.Random.Range(-1f, 1f) * magnitude * damper);
            yield return null;
        }
        rt.anchoredPosition = basePos;
        shakeRoutine = null;
    }

    // --- 对象池注册 ---
        private void RegisterPools()
        {
            if (damageTextContainer == null || damageTextPrefab == null) return;

            // 伤害/治疗/防御飘字（池化根节点 RectTransform：TextMeshProUGUI 可能挂在子节点上）
            Roguelike.Core.PoolManager.RegisterPool<RectTransform>("DamageText", damageTextPrefab, 10, 30, damageTextContainer);

            // 命中特效（序列帧动画）
            if (hitEffectFrames != null && hitEffectFrames.Length > 0)
            {
                // 创建一个临时 GameObject 作为预制体（避免每次 new GameObject + AddComponent<Image>）
                var hitEffectPrefab = CreateHitEffectPrefab();
                Roguelike.Core.PoolManager.RegisterPool<Image>("HitEffect", hitEffectPrefab, 5, 15, damageTextContainer);
            }

            // 投射物（卡牌飞行特效）
            var projPrefab = CreateProjectilePrefab();
            if (projPrefab != null)
                Roguelike.Core.PoolManager.RegisterPool<Image>("Projectile", projPrefab, 8, 20, damageTextContainer);

            // 拖尾特效
            var trailPrefab = CreateTrailPrefab();
            if (trailPrefab != null)
                Roguelike.Core.PoolManager.RegisterPool<Image>("CardTrail", trailPrefab, 6, 12, damageTextContainer);

            // 提示框
            if (turnEndPromptPrefab != null)
                Roguelike.Core.PoolManager.RegisterPool<RectTransform>("TurnPrompt", turnEndPromptPrefab, 1, 3, promptContainer);
        }

        // --- 预制体创建辅助（只执行一次，生成供池使用的预制体对象）---
        private GameObject CreateHitEffectPrefab()
        {
            if (hitEffectFrames == null || hitEffectFrames.Length == 0) return null;
            var go = new GameObject("HitEffect_Prefab", typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            var img = go.GetComponent<Image>();
            img.sprite = hitEffectFrames[0];
            img.raycastTarget = false;
            img.color = Color.white;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = hitEffectSize;
            return go;
        }

        private GameObject CreateProjectilePrefab()
        {
            if (damageTextContainer == null) return null;
            var go = new GameObject("PlayProjectile_Prefab", typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            var img = go.GetComponent<Image>();
            img.sprite = GetProjectileSprite();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0.95f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = cardProjectileSize;
            rt.localScale = Vector3.one;
            return go;
        }

        private GameObject CreateTrailPrefab()
        {
            if (damageTextContainer == null) return null;
            var go = new GameObject("CardTrail_Prefab", typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            var img = go.GetComponent<Image>();
            var tf = GetTrailFrames();
            img.sprite = (tf != null && tf.Length > 0) ? tf[0] : null;
            img.raycastTarget = false;
            return go;
        }

    private void OnDestroy()
    {
        StopEnemyAnim();
        UnsubscribeEvents();
    }

    private void RefreshAll()
    {
        var player = battleManager.GetPlayer();
        var enemy = battleManager.GetEnemy();
        var runData = battleManager.GetRunData();

        playerHpText.text = $"{player.CurrentHp}/{player.MaxHp}";
        if (enemy != null)
        {
            if (enemyHpText != null) enemyHpText.text = $"{enemy.CurrentHp}/{enemy.MaxHp}";
            if (enemyNameText != null) enemyNameText.text = enemy.Name;
        }

        RefreshPileCounts();

        goldText.text = $"金币: {runData.Gold}";
        RefreshRelics();
        RefreshPotions();
        RefreshHandCards();
        UpdateSelectionVisuals();
        UpdatePreview();
        UpdateButtonStates();

        // 状态效果栏
        if (playerStatusBar != null) playerStatusBar.Refresh(player);
        if (enemyStatusBar != null && enemy != null) enemyStatusBar.Refresh(enemy);

        RefreshSuitHud();
        RefreshDestinyHud();
        UpdateHpBars();
        if (enemySlots.Count > 0) RefreshEnemySlots();
        else RefreshEnemyImage();
        UpdateTurnCount();
    }

    private void RefreshRelics()
    {
        if (battleManager == null) return;

        var runData = battleManager.GetRunData();
        if (runData == null) return;

        // 先清空所有槽位
        foreach (var slot in relicSlots)
        {
            if (slot != null)
                slot.Clear();
        }

        // 填充拥有的遗物
        for (int i = 0; i < runData.RelicIds.Count && i < relicSlots.Length; i++)
        {
            var relicData = ConfigLoader.GetRelic(runData.RelicIds[i]);
            if (relicData != null && relicSlots[i] != null)
                relicSlots[i].SetRelic(relicData);
        }
    }

    private void RefreshPileCounts()
    {
        if (battleManager == null) return;

        int deck = battleManager.GetDeckCount();
        int discard = battleManager.GetDiscardCount();

        if (deckCountText != null)
        {
            // 没单独指定弃牌堆文本时，退化为合并显示
            deckCountText.text = discardCountText != null ? $"牌堆: {deck}" : $"牌堆: {deck}   弃牌: {discard}";
        }
        if (discardCountText != null)
            discardCountText.text = $"弃牌: {discard}";
    }

    // ===== 血条 / 护盾图标 / 回合数 =====

    private Sprite shieldIconSprite;
    private bool shieldIconLoaded;

    private Sprite[] selectionFramesCache;
    private bool selectionFramesLoaded;

    /// <summary>选中提示框的两帧（Resources/Battle/UIS 里的 UIS_0 / UIS_1）</summary>
    private Sprite[] GetSelectionFrames()
    {
        if (!selectionFramesLoaded)
        {
            selectionFramesLoaded = true;
            var all = Resources.LoadAll<Sprite>(selectionSpritePath);
            if (all != null && all.Length > 0)
            {
                // 按名字排序，保证 UIS_0 → UIS_1 的播放顺序
                System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
                selectionFramesCache = all;
            }
            else
            {
                Debug.LogWarning($"[BattlePanel] 没找到选择框图集: Resources/{selectionSpritePath}");
            }
        }
        return selectionFramesCache;
    }

    private Sprite GetShieldIconSprite()
    {
        if (!shieldIconLoaded)
        {
            shieldIconLoaded = true;

            // 留空时用默认图：Resources/Battle/DefenseIcon（玩家和敌人共用）
            string path = string.IsNullOrEmpty(shieldIconPath) ? "Battle/DefenseIcon" : shieldIconPath;
            shieldIconSprite = Resources.Load<Sprite>(path);
            if (shieldIconSprite == null)
                Debug.LogWarning($"[BattlePanel] 找不到护盾图标: Resources/{path}");
        }
        return shieldIconSprite;
    }

    private Sprite trailSpriteCache;
    private bool trailSpriteLoaded;

    private Sprite GetTrailSprite()
    {
        if (cardTrailSprite != null) return cardTrailSprite;
        if (!trailSpriteLoaded)
        {
            trailSpriteLoaded = true;
            if (!string.IsNullOrEmpty(cardTrailSpritePath))
                trailSpriteCache = Resources.Load<Sprite>(cardTrailSpritePath);
        }
        return trailSpriteCache;
    }

    private Sprite[] trailFramesCache;
    private bool trailFramesLoaded;

    private Sprite[] GetTrailFrames()
    {
        if (cardTrailFrames != null && cardTrailFrames.Length > 0) return cardTrailFrames;

        if (!trailFramesLoaded)
        {
            trailFramesLoaded = true;
            if (!string.IsNullOrEmpty(cardTrailFramesPath))
            {
                var loaded = Resources.LoadAll<Sprite>(cardTrailFramesPath);
                if (loaded != null && loaded.Length > 0)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    trailFramesCache = loaded;
                }
            }
        }
        return trailFramesCache;
    }

    /// <summary>把 shieldIconPath 指定的 Sprite 应用到两个护盾图标上（Inspector 里也可直接赋值）</summary>
    private void ApplyShieldIcon()
    {
        var sprite = GetShieldIconSprite();
        if (sprite == null) return;
        if (playerShieldIcon != null) playerShieldIcon.sprite = sprite;
        if (enemyShieldIcon != null) enemyShieldIcon.sprite = sprite;
    }

    private static Sprite runtimeWhiteSprite;

    private static Sprite GetRuntimeWhiteSprite()
    {
        if (runtimeWhiteSprite == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            runtimeWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            runtimeWhiteSprite.name = "RuntimeWhite";
        }
        return runtimeWhiteSprite;
    }

    /// <summary>
    /// 配置血条 Image：若 Source Image 留空则用运行时 1x1 白图，并强制 Filled/Horizontal/Left。
    /// （填充条必须用无边框的纯色矩形，带边框/透明的图会显示异常）
    /// </summary>
    private void SetupHpBar(Image bar, Color defaultColor)
    {
        if (bar == null) return;

        if (bar.sprite == null)
        {
            bar.sprite = GetRuntimeWhiteSprite();
            bar.color = defaultColor;
        }

        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private void EnsureTurnCount()
    {
        if (turnCountText != null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var go = new GameObject("TurnCountText", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        turnCountText = go.AddComponent<TextMeshProUGUI>();
        if (font != null) turnCountText.font = font;
        turnCountText.fontSize = 24;
        turnCountText.alignment = TextAlignmentOptions.Center;
        turnCountText.color = Color.white;
        var rt = turnCountText.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(400f, 40f);
    }

    private void UpdateHpBars()
    {
        if (battleManager == null) return;
        var player = battleManager.GetPlayer();
        if (player == null) return;

        if (playerHpFill != null)
            playerHpFill.fillAmount = player.MaxHp > 0 ? Mathf.Clamp01((float)player.CurrentHp / player.MaxHp) : 0f;

        UpdateShield(playerDefenseText, playerShieldIcon, player.Defense);

        // 敌方：开局没选中目标时也要把旧显示清成 0，
        // 否则上一场战斗的防御数值会残留在图标/文字上（看起来像防御延续了）
        var enemy = battleManager.GetEnemy();
        if (enemy != null)
        {
            if (enemyHpFill != null)
                enemyHpFill.fillAmount = enemy.MaxHp > 0 ? Mathf.Clamp01((float)enemy.CurrentHp / enemy.MaxHp) : 0f;
            UpdateShield(enemyDefenseText, enemyShieldIcon, enemy.Defense);
        }
        else
        {
            if (enemyHpFill != null) enemyHpFill.fillAmount = 0f;
            UpdateShield(enemyDefenseText, enemyShieldIcon, 0);
        }
    }

    /// <summary>护盾显示：防御 > 0 时才显示图标与数字；为 0 时两者都隐藏</summary>
    private void UpdateShield(TextMeshProUGUI text, Image icon, int defense)
    {
        bool show = defense > 0;

        if (icon != null)
        {
            var sprite = GetShieldIconSprite();
            if (sprite != null) icon.sprite = sprite;
            icon.enabled = show;
        }

        if (text != null)
        {
            text.enabled = show;
            if (show)
                text.text = icon != null ? defense.ToString() : $"防御: {defense}";
        }
    }

    private void UpdateTurnCount()
    {
        if (turnCountText == null || battleManager == null) return;
        turnCountText.text = $"第 {battleManager.TurnsElapsed} 回合";
    }

    private readonly Dictionary<CardData, CardUI> cardUis = new Dictionary<CardData, CardUI>();

    private void RefreshHandCards()
    {
        var handArea = battleManager.GetHandArea();

        // 版本号检测，避免重复刷新
        if (handArea.Version == lastHandVersion) return;
        lastHandVersion = handArea.Version;

        var current = new HashSet<CardData>(handArea.HandCards);

        // 1) 移除已不在手牌里的卡（先脱离父级再销毁，避免延迟销毁被计入 childCount）
        var removed = new List<CardData>();
        foreach (var kv in cardUis)
            if (!current.Contains(kv.Key)) removed.Add(kv.Key);
        foreach (var card in removed)
        {
            var ui = cardUis[card];
            cardUis.Remove(card);
            if (ui != null)
            {
                handTargets.Remove(ui);
                handMoves.Remove(ui);
                ui.transform.SetParent(null, false);
                Destroy(ui.gameObject);
            }
        }

        // 记录当前（可能正在动画中的）位置，作为挪动起点
        var beforePos = new Dictionary<CardUI, Vector2>();
        foreach (var kv in cardUis)
            if (kv.Value != null) beforePos[kv.Value] = ((RectTransform)kv.Value.transform).anchoredPosition;

        // 2) 新增的牌：创建 UI（先不播放动画）
        var newCards = new List<CardData>();
        foreach (var card in handArea.HandCards)
        {
            if (cardUis.ContainsKey(card)) continue;

            var cardObj = Instantiate(cardPrefab, handContainer);
            var cardUI = cardObj.GetComponent<CardUI>();
            cardUI.Init(card);

            var cardData = card;
            cardUI.OnClick = () =>
            {
                // 点数修正药水：待选牌状态 → 点哪张就改哪张
                if (battleManager != null && battleManager.HasPendingRankShift)
                {
                    battleManager.ApplyPendingRankShift(cardData);
                    return;
                }
                handArea.ToggleSelect(cardData);
                battleManager.OnCardSelectionChanged(); // 通知选中变化
            };

            cardUis[card] = cardUI;
            newCards.Add(card);
        }

        // 3) 按手牌顺序排列
        for (int i = 0; i < handArea.HandCards.Count; i++)
        {
            if (cardUis.TryGetValue(handArea.HandCards[i], out var ui) && ui != null)
                ui.transform.SetSiblingIndex(i);
        }

        // 4) 手动算出目标位置（居中向两边展开）
        UpdateHandLayoutTargets();

        // 出牌后的抽牌：等投射物命中后再播抽牌动画
        bool deferDraws = activeProjectiles > 0;

        // 5) 新牌：先摆到目标位置（隐藏），再从牌堆飞出
        float stagger = Mathf.Max(0f, drawAnimStagger);
        for (int i = 0; i < newCards.Count; i++)
        {
            if (cardUis.TryGetValue(newCards[i], out var ui) && ui != null)
            {
                if (handTargets.TryGetValue(ui, out var t))
                    ((RectTransform)ui.transform).anchoredPosition = t;

                var uiCap = ui;
                var cardCap = newCards[i];
                float d = i * stagger;
                if (deferDraws)
                {
                    HideCard(ui.gameObject);   // 立刻隐藏，等命中后再飞进来
                    pendingDrawAnims.Add(() => { if (uiCap != null) PlayDrawInAnim(uiCap.gameObject, cardCap, d); });
                }
                else
                {
                    PlayDrawInAnim(ui.gameObject, newCards[i], d);
                }
            }
        }

        // 6) 已有牌：先按住原位；抽牌动画一开始，就和新牌同步缓慢挪到目标位置
        foreach (var kv in cardUis)
        {
            var ui = kv.Value;
            if (ui == null) continue;
            if (newCards.Contains(kv.Key)) continue;

            var rt = (RectTransform)ui.transform;
            if (beforePos.TryGetValue(ui, out var bp))
                rt.anchoredPosition = bp;   // 先按回原位

            handMoves.Remove(ui);   // 取消正在进行的移动，重新排队

            if (deferDraws)
                deferredOldShift.Add(ui);            // 等投射物命中后再和新牌一起挪
            else
                QueueHandMove(ui, handShiftDelay);   // 立即和新牌一起挪
        }

        StartHandDriver();
    }

    /// <summary>投射物命中后，执行被推迟的抽牌动画与手牌挪位</summary>
    private void FlushDeferredDraws()
    {
        foreach (var ui in deferredOldShift)
            if (ui != null) QueueHandMove(ui, handShiftDelay);
        deferredOldShift.Clear();

        foreach (var a in pendingDrawAnims) a?.Invoke();
        pendingDrawAnims.Clear();

        StartHandDriver();
    }

    /// <summary>把某张牌排入移动队列：从当前位置平滑移动到 handTargets 里的目标位置</summary>
    private void QueueHandMove(CardUI ui, float delay)
    {
        if (ui == null || !handTargets.TryGetValue(ui, out var target)) return;
        var rt = (RectTransform)ui.transform;
        handMoves[ui] = new HandMove
        {
            from = rt.anchoredPosition,
            to = target,
            startTime = Time.time + Mathf.Max(0f, delay),
            duration = Mathf.Max(0.05f, handShiftDuration)
        };
    }

    // ===== 手牌位置驱动（不再让 LayoutGroup 瞬间拍回，自己平滑移动）=====

    private struct HandMove
    {
        public Vector2 from;
        public Vector2 to;
        public float startTime;
        public float duration;
    }

    private readonly Dictionary<CardUI, Vector2> handTargets = new Dictionary<CardUI, Vector2>();
    private readonly Dictionary<CardUI, HandMove> handMoves = new Dictionary<CardUI, HandMove>();
    private Coroutine handDriver;

    // 出牌后的抽牌：等投射物命中再播
    private readonly List<System.Action> pendingDrawAnims = new List<System.Action>();
    private readonly List<CardUI> deferredOldShift = new List<CardUI>();

    /// <summary>
    /// 手动计算手牌目标位置：以 handContainer 中心为基准，向两边展开。
    /// 不依赖 HorizontalLayoutGroup（若场景里还留着会被自动禁用）。
    /// </summary>
    private void UpdateHandLayoutTargets()
    {
        var layout = handContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;

        var handRt = handContainer as RectTransform;
        if (handRt == null) return;

        int n = handRt.childCount;
        if (n == 0) return;

        float cardWidth = 150f;
        if (handRt.GetChild(0) is RectTransform rt0 && rt0.rect.width > 1f)
            cardWidth = rt0.rect.width;

        // 手牌最大总宽：不能超过面板实际宽度（窄屏/4:3 时 handMaxWidth 会溢出屏幕）
        float maxWidth = handMaxWidth;
        if (transform is RectTransform panelRt && panelRt.rect.width > 1f)
            maxWidth = Mathf.Min(maxWidth, panelRt.rect.width - 80f);

        float spacing;
        if (n <= 1)
        {
            spacing = 0f;
        }
        else
        {
            spacing = (maxWidth - n * cardWidth) / (n - 1);
            spacing = Mathf.Min(handMaxSpacing, spacing);
        }

        float totalWidth = n * cardWidth + (n - 1) * spacing;
        float startX = -totalWidth * 0.5f + cardWidth * 0.5f;

        for (int i = 0; i < n; i++)
        {
            if (!(handRt.GetChild(i) is RectTransform child)) continue;
            var ui = child.GetComponent<CardUI>();
            if (ui == null) continue;
            handTargets[ui] = new Vector2(startX + i * (cardWidth + spacing), 0f);
        }
    }

    private void StartHandDriver()
    {
        if (!isActiveAndEnabled) return;
        if (handDriver == null)
            handDriver = StartCoroutine(HandMoveRoutine());
    }

    private System.Collections.IEnumerator HandMoveRoutine()
    {
        var finished = new List<CardUI>();

        while (true)
        {
            yield return null;

            bool anyPending = false;
            finished.Clear();

            foreach (var kv in handMoves)
            {
                var ui = kv.Key;
                if (ui == null) { finished.Add(ui); continue; }

                var m = kv.Value;
                if (Time.time < m.startTime) { anyPending = true; continue; }

                var rt = (RectTransform)ui.transform;
                float t = m.duration > 0f ? Mathf.Clamp01((Time.time - m.startTime) / m.duration) : 1f;
                float e = t * t * (3f - 2f * t);
                rt.anchoredPosition = Vector2.Lerp(m.from, m.to, e);

                if (t >= 1f) finished.Add(ui);
                else anyPending = true;
            }

            foreach (var ui in finished) handMoves.Remove(ui);

            if (!anyPending)
            {
                handDriver = null;
                yield break;
            }
        }
    }

    // ===== 抽牌动画：牌堆 → 上飞 → 手牌 =====

    private void HideCard(GameObject cardObj)
    {
        if (cardObj == null) return;
        var cg = cardObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
    }

    private void PlayDrawInAnim(GameObject cardObj, CardData card, float delay = 0f)
    {
        // 先消费来源标记（「搜寻」回手的牌从弃牌堆飞出）
        bool fromDiscard = battleManager != null && battleManager.ConsumeFromDiscard(card);

        if (cardObj == null) return;
        if (!isActiveAndEnabled) return;
        var rt = cardObj.GetComponent<RectTransform>();
        if (rt == null) return;
        var cg = cardObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();

        cg.alpha = 0f;              // 真实牌先隐藏，等幻影飞到再显示
        cg.blocksRaycasts = false;  // 飞行中不可点

        StartCoroutine(PlayDrawInAnimRoutine(rt, cg, card, delay, fromDiscard));
    }

    private System.Collections.IEnumerator PlayDrawInAnimRoutine(RectTransform rt, CanvasGroup cg, CardData card, float delay, bool fromDiscard)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        bool hasOrigin = fromDiscard
            ? TryGetDiscardWorldPosition(out var origin)
            : TryGetDeckWorldPosition(out origin);

        if (!hasOrigin)
            yield return SimpleFadeIn(cg);
        else
            yield return DrawFlyRoutine(rt, cg, card, origin);

        if (cg != null) cg.blocksRaycasts = true;
    }

    private bool TryGetDeckWorldPosition(out Vector3 world)
    {
        world = Vector3.zero;
        if (deckPileAnchor != null) { world = deckPileAnchor.position; return true; }
        if (deckCountText != null) { world = deckCountText.transform.position; return true; }
        return false;
    }

    private bool TryGetDiscardWorldPosition(out Vector3 world)
    {
        world = Vector3.zero;
        if (discardPileAnchor != null) { world = discardPileAnchor.position; return true; }
        if (discardCountText != null) { world = discardCountText.transform.position; return true; }
        return TryGetDeckWorldPosition(out world);
    }

    private System.Collections.IEnumerator SimpleFadeIn(CanvasGroup cg)
    {
        const float duration = 0.15f;
        float elapsed = 0f;
        while (elapsed < duration && cg != null)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
    }

    private System.Collections.IEnumerator DrawFlyRoutine(RectTransform rt, CanvasGroup cg, CardData card, Vector3 originWorld)
    {
        if (cardPrefab == null || damageTextContainer == null)
        {
            if (cg != null) cg.alpha = 1f;
            yield break;
        }

        // 幻影牌（从牌堆飞出的那张）
        var ghost = Instantiate(cardPrefab, damageTextContainer);
        ghost.name = "DrawGhost";
        ghost.transform.SetAsLastSibling();
        var ghostUI = ghost.GetComponent<CardUI>();
        if (ghostUI != null) { ghostUI.Init(card); ghostUI.enabled = false; }
        foreach (var g in ghost.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        var ghostRt = ghost.GetComponent<RectTransform>();
        if (ghostRt == null)
        {
            Destroy(ghost);
            if (cg != null) cg.alpha = 1f;
            yield break;
        }

        ghostRt.position = originWorld;
        ghostRt.localScale = Vector3.one;

        Vector3 upWorld = originWorld + new Vector3(0f, drawFlyUpHeight, 0f);

        // 阶段 1：向上飞一点
        float elapsed = 0f;
        float d1 = Mathf.Max(0.01f, drawFlyUpDuration);
        while (elapsed < d1 && ghostRt != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d1);
            ghostRt.position = Vector3.Lerp(originWorld, upWorld, 1f - Mathf.Pow(1f - t, 2f));
            yield return null;
        }

        // 阶段 2：飞向手牌槽（目标实时读取，布局变动也能跟上）
        elapsed = 0f;
        float d2 = Mathf.Max(0.01f, drawFlyToHandDuration);
        Vector3 start2 = ghostRt != null ? ghostRt.position : upWorld;
        while (elapsed < d2 && ghostRt != null && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d2);
            float e = t * t * (3f - 2f * t);
            ghostRt.position = Vector3.Lerp(start2, rt.position, e);
            yield return null;
        }

        if (ghostRt != null) Destroy(ghostRt.gameObject);
        if (cg != null) cg.alpha = 1f;

        // 到达时弹一下
        if (rt != null) yield return StartCoroutine(PopRoutine(rt));
    }

    private System.Collections.IEnumerator PopRoutine(RectTransform rt)
    {
        Vector3 baseScale = rt.localScale;
        const float duration = 0.12f;
        float elapsed = 0f;
        while (elapsed < duration && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = t < 0.5f
                ? Mathf.Lerp(1f, drawArrivePopScale, t / 0.5f)
                : Mathf.Lerp(drawArrivePopScale, 1f, (t - 0.5f) / 0.5f);
            rt.localScale = baseScale * s;
            yield return null;
        }
        if (rt != null) rt.localScale = baseScale;
    }

    // --- 出牌飞行演出 ---

    private void OnPlayClicked()
    {
        if (battleManager == null) return;

        // 不能出牌（牌型无效 / 被禁锢 / 割裂 / 封禁花色）时不做任何事
        if (!battleManager.CanPlaySelection(out var reason))
        {
            Debug.Log($"[出牌失败] {reason}");
            return;
        }

        var selected = battleManager.GetHandArea().GetSelectedCards();
        if (selected != null && selected.Count > 0)
            SpawnPlayProjectiles(selected);

        // 有特效飞出去时，等它打到敌人再结算胜利
        battleManager.SetDeferBattleEnd(activeProjectiles > 0);

        // 「搜寻」只能取「出牌之前」就在弃牌堆里的牌，所以先做快照
        var discardBeforePlay = battleManager.GetDiscardCards();

        battleManager.TryPlayCards();

        // 附魔「搜寻」：从弃牌堆选一张牌回手（已经击杀敌人就不再弹）
        if (battleManager.PendingSearchCount > 0 && !battleManager.IsBattleOver)
            OpenDiscardPicker(discardBeforePlay);
    }

    private void OpenDiscardPicker(List<CardData> pool)
    {
        if (battleManager == null || UIManager.Instance == null) return;

        // 只显示出牌前就在弃牌堆、且现在仍在弃牌堆里的牌
        var available = pool != null
            ? pool.FindAll(c => battleManager.IsInDiscard(c))
            : battleManager.GetDiscardCards();
        if (available.Count == 0) return;

        var panel = UIManager.Instance.ShowPanel<DiscardPickerPanel>();
        panel?.ShowPicker("从弃牌堆选一张牌回到手牌", available, card =>
        {
            battleManager.MoveDiscardToHand(card);
            if (battleManager.PendingSearchCount > 0) OpenDiscardPicker(pool);
        });
    }

    /// <summary>把选中的手牌生成投射物，沿弧线飞向敌人；多张错开起飞但「同时命中」</summary>
    private void SpawnPlayProjectiles(List<CardData> cards)
    {
        if (damageTextContainer == null) return;

        // 先收集有效的手牌位置（避免手牌被刷新后找不到）
        var targets = new List<Vector3>();
        foreach (var card in cards)
        {
            var ui = FindCardUI(card);
            if (ui != null) targets.Add(ui.transform.position);
        }
        if (targets.Count == 0) return;

        Vector3 targetWorld = GetEnemyAnchorWorld();

        float stagger = Mathf.Max(0f, cardFlyStagger);
        float baseDuration = Mathf.Max(0.05f, cardFlyDuration);
        // 统一命中时刻：最后一发起飞后 baseDuration 秒
        float arrival = baseDuration + stagger * (targets.Count - 1);

        for (int i = 0; i < targets.Count; i++)
        {
            Vector3 from = targets[i];

            // 牌化作特效的瞬间：原地闪一下
            SpawnImpactFlash(from);

            Image projImg = CreateProjectile();
            Image trailImg = cardFlyTrail ? CreateFollowTrail() : null;

            float delay = i * stagger;
            float duration = Mathf.Max(0.05f, arrival - delay);

            activeProjectiles++;
            StartCoroutine(FlyProjectileRoutine(projImg, trailImg, from, targetWorld, delay, duration, GetTrailFrames()));
        }
    }

    private System.Collections.IEnumerator FlyProjectileRoutine(Image projImg, Image trailImg, Vector3 from, Vector3 to, float delay, float duration, Sprite[] frames)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        RectTransform projRt = projImg != null ? projImg.rectTransform : null;
        RectTransform trailRt = trailImg != null ? trailImg.rectTransform : null;
        if (projRt == null)
        {
            OnProjectileFinished();
            yield break;
        }

        duration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;

        // 弧线：控制点在中点上方，横向距离越大弧越高，再加随机抖动
        Vector3 mid = (from + to) * 0.5f;
        float arc = cardFlyArc + Mathf.Abs(to.x - from.x) * cardFlyArcPerWidth;
        Vector3 control = mid + new Vector3(UnityEngine.Random.Range(-60f, 60f), arc, 0f);

        Vector3 prevPos = from;
        Vector3 tail = from;
        float frameTimer = 0f;
        int frameIndex = 0;

        while (elapsed < duration && projRt != null)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            float et = t * t * (3f - 2f * t); // smoothstep 缓动

            // 二次贝塞尔弧线
            Vector3 a = Vector3.Lerp(from, control, et);
            Vector3 b = Vector3.Lerp(control, to, et);
            projRt.position = Vector3.Lerp(a, b, et);

            // 运动方向
            Vector3 moveDir = projRt.position - prevPos;
            if (moveDir.sqrMagnitude < 0.0001f) moveDir = to - from;
            prevPos = projRt.position;

            // 横向特效旋转对齐飞行方向
            if (cardProjectileSpin)
                projRt.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg);

            // 缩放：先弹大再缩小
            float scale = t < 0.2f
                ? Mathf.Lerp(1f, 1.2f, t / 0.2f)
                : Mathf.Lerp(1.2f, 0.6f, (t - 0.2f) / 0.8f);
            projRt.localScale = Vector3.one * scale;

            // 序列帧投射物
            if (projImg != null && frames != null && frames.Length > 0)
            {
                frameTimer += dt;
                float ft = 1f / Mathf.Max(1f, cardTrailFrameRate);
                while (frameTimer >= ft)
                {
                    frameTimer -= ft;
                    frameIndex = (frameIndex + 1) % frames.Length;
                }
                projImg.sprite = frames[frameIndex];
            }

            // ===== 跟随式拖尾 =====
            if (trailRt != null)
            {
                Vector3 head = projRt.position;
                tail = Vector3.Lerp(tail, head, 1f - Mathf.Exp(-9f * dt));

                Vector3 dir = head - tail;
                if (dir.sqrMagnitude < 0.0001f) dir = to - from;

                float dist = dir.magnitude;
                if (dist > 3f)
                {
                    trailRt.gameObject.SetActive(true);
                    trailRt.position = head;
                    // 枢轴在左端(头部)；+180° 让本地 +X 指向身后，贴图左边=头部贴在投射物上
                    trailRt.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 180f);
                    trailRt.sizeDelta = new Vector2(dist, cardTrailSize.y * scale);

                    if (trailImg != null)
                    {
                        float alpha = 0.55f * Mathf.Clamp01(1f - Mathf.Max(0f, t - 0.6f) / 0.4f);
                        trailImg.color = new Color(1f, 1f, 1f, alpha);
                    }
                }
                else
                {
                    trailRt.gameObject.SetActive(false);
                }
            }

            yield return null;
        }

        if (trailRt != null) Roguelike.Core.PoolManager.Return("CardTrail", trailRt.GetComponent<Image>());
            if (projRt != null)
            {
                if (cardFlyImpact) SpawnHitEffect(projRt.position);
                Roguelike.Core.PoolManager.Return("Projectile", projRt.GetComponent<Image>());
            }
            OnProjectileFinished();
        }

    /// <summary>创建投射物（牌打出后变成的特效）</summary>
    private Image CreateProjectile()
    {
        var img = Roguelike.Core.PoolManager.Get<Image>("Projectile");
        if (img == null)
        {
            // 池未就绪时降级
            if (damageTextContainer == null) return null;
            var go = new GameObject("PlayProjectile", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(damageTextContainer, false);
            go.transform.SetAsLastSibling();
            img = go.GetComponent<Image>();
        }
        else
        {
            img.transform.SetParent(damageTextContainer, false);
            img.transform.SetAsLastSibling();
        }

        img.sprite = GetProjectileSprite();
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.95f);

        var rt = img.rectTransform;
        rt.sizeDelta = cardProjectileSize;
        rt.localScale = Vector3.one;
        rt.localPosition = Vector3.zero;

        return img;
    }

    private Sprite projectileSpriteCache;
    private bool projectileSpriteLoaded;

    private Sprite GetProjectileSprite()
    {
        if (cardProjectileSprite != null) return cardProjectileSprite;

        if (!projectileSpriteLoaded)
        {
            projectileSpriteLoaded = true;
            if (!string.IsNullOrEmpty(cardProjectileSpritePath))
                projectileSpriteCache = Resources.Load<Sprite>(cardProjectileSpritePath);
        }
        if (projectileSpriteCache != null) return projectileSpriteCache;

        var frames = GetTrailFrames();
        if (frames != null && frames.Length > 0) return frames[0];

        var trail = GetTrailSprite();
        if (trail != null) return trail;

        return GetRuntimeCircleSprite();
    }

    // ===== 命中特效 =====

    /// <summary>命中特效：优先预制体，其次序列帧，都没有则退回运行时圆光</summary>
    private void SpawnHitEffect(Vector3 worldPos)
    {
        if (hitEffectPrefab != null)
        {
            var go = hitEffectWorldSpace
                ? Instantiate(hitEffectPrefab)
                : Instantiate(hitEffectPrefab, damageTextContainer);

            if (go != null)
            {
                go.transform.position = worldPos;
                if (hitEffectRandomRotation && !hitEffectWorldSpace)
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                Destroy(go, Mathf.Max(0.1f, hitEffectLifetime));
            }
            return;
        }

        var frames = GetHitEffectFrames();
        if (frames != null && frames.Length > 0)
        {
            SpawnHitEffectAnim(worldPos, frames);
            return;
        }

        SpawnImpactFlash(worldPos);
    }

    private void SpawnHitEffectAnim(Vector3 worldPos, Sprite[] frames)
    {
        if (damageTextContainer == null) return;

        var img = Roguelike.Core.PoolManager.Get<Image>("HitEffect");
        if (img == null)
        {
            // 池未就绪时降级
            var go = new GameObject("HitEffect", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(damageTextContainer, false);
            go.transform.SetAsLastSibling();
            img = go.GetComponent<Image>();
        }
        else
        {
            img.transform.SetParent(damageTextContainer, false);
            img.transform.SetAsLastSibling();
        }

        img.sprite = frames[0];
        img.raycastTarget = false;
        img.color = Color.white;

        var rt = img.rectTransform;
        rt.position = worldPos;
        rt.sizeDelta = hitEffectSize;
        if (hitEffectRandomRotation)
            rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

        StartCoroutine(HitEffectAnimRoutine(rt, img, frames));
    }

    private System.Collections.IEnumerator HitEffectAnimRoutine(RectTransform rt, Image img, Sprite[] frames)
    {
        float frameTime = 1f / Mathf.Max(1f, hitEffectFrameRate);
        float total = frameTime * frames.Length;
        float elapsed = 0f;

        while (elapsed < total && rt != null)
        {
            elapsed += Time.deltaTime;
            int frame = Mathf.Clamp(Mathf.FloorToInt(elapsed / frameTime), 0, frames.Length - 1);
            if (img != null) img.sprite = frames[frame];
            yield return null;
        }
        Roguelike.Core.PoolManager.Return("HitEffect", img);
    }

    private Sprite[] hitFramesCache;
    private bool hitFramesLoaded;

    private Sprite[] GetHitEffectFrames()
    {
        if (hitEffectFrames != null && hitEffectFrames.Length > 0) return hitEffectFrames;

        if (!hitFramesLoaded)
        {
            hitFramesLoaded = true;
            if (!string.IsNullOrEmpty(hitEffectFramesPath))
            {
                var loaded = Resources.LoadAll<Sprite>(hitEffectFramesPath);
                if (loaded != null && loaded.Length > 0)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    hitFramesCache = loaded;
                }
            }
        }
        return hitFramesCache;
    }

    /// <summary>创建跟随式拖尾（枢轴在右端=头部，向身后拉伸）</summary>
    private Image CreateFollowTrail()
    {
        var img = Roguelike.Core.PoolManager.Get<Image>("CardTrail");
        if (img == null)
        {
            // 池未就绪时降级
            if (damageTextContainer == null) return null;
            var go = new GameObject("CardTrailRibbon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(damageTextContainer, false);
            go.transform.SetAsFirstSibling();
            img = go.GetComponent<Image>();
        }
        else
        {
            img.transform.SetParent(damageTextContainer, false);
            img.transform.SetAsFirstSibling();
        }

        img.sprite = GetTrailSprite() ?? GetRuntimeStreakSprite();
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.55f);

        var rt = img.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);   // 左端=头部
        rt.sizeDelta = new Vector2(0f, cardTrailSize.y);
        img.gameObject.SetActive(false);

        return img;
    }

    private static Sprite runtimeStreakSprite;

    /// <summary>运行时生成一条柔光渐变条（右端实、左端透明、中间亮），无需素材</summary>
    private static Sprite GetRuntimeStreakSprite()
    {
        if (runtimeStreakSprite == null)
        {
            const int w = 128;
            const int h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float vy = 1f - Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f; // 中间亮
                vy *= vy;
                for (int x = 0; x < w; x++)
                {
                    float vx = (x + 0.5f) / w; // 0=左(头) 1=右(尾)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, (1f - vx) * vy));
                }
            }
            tex.Apply();
            runtimeStreakSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            runtimeStreakSprite.name = "RuntimeStreak";
        }
        return runtimeStreakSprite;
    }

    private void SpawnImpactFlash(Vector3 worldPos)
    {
        if (damageTextContainer == null) return;

        var go = new GameObject("ImpactFlash", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(damageTextContainer, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.sprite = GetRuntimeCircleSprite();
        img.raycastTarget = false;
        img.color = new Color(1f, 0.92f, 0.6f, 0.85f);

        var rt = go.GetComponent<RectTransform>();
        rt.position = worldPos;
        rt.sizeDelta = new Vector2(70f, 70f);

        StartCoroutine(ImpactFlashRoutine(rt, img));
    }

    private System.Collections.IEnumerator ImpactFlashRoutine(RectTransform rt, Image img)
    {
        const float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.9f, t);
            if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(0.85f, 0f, t));
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private static Sprite runtimeCircleSprite;

    private static Sprite GetRuntimeCircleSprite()
    {
        if (runtimeCircleSprite == null)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(1f - d / r);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            runtimeCircleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            runtimeCircleSprite.name = "RuntimeCircle";
        }
        return runtimeCircleSprite;
    }

    private CardUI FindCardUI(CardData card)
    {
        if (handContainer == null) return null;
        for (int i = 0; i < handContainer.childCount; i++)
        {
            var ui = handContainer.GetChild(i).GetComponent<CardUI>();
            if (ui != null && ui.GetCardData() == card) return ui;
        }
        return null;
    }

    /// <summary>强制刷新手牌 UI（作弊/改造/外部修改附魔后用）</summary>
    public void ForceRefreshHand()
    {
        // 现有牌的附魔/花色可能变了，逐个刷新显示（不重建对象，避免打断动画）
        foreach (var kv in cardUis)
        {
            if (kv.Value != null)
            {
                kv.Value.RefreshArtwork();
                kv.Value.RefreshEnchantmentDisplay();
            }
        }
        lastHandVersion = -1;
        RefreshHandCards();
        UpdateSelectionVisuals();
        UpdateButtonStates();
    }

    private void UpdateSelectionVisuals()
    {
        if (battleManager == null) return;

        var selectedCards = battleManager.GetHandArea().GetSelectedCards();
        var selectedSet = new HashSet<CardData>(selectedCards);

        for (int i = 0; i < handContainer.childCount; i++)
        {
            var cardUI = handContainer.GetChild(i).GetComponent<CardUI>();
            if (cardUI != null)
            {
                bool selected = selectedSet.Contains(cardUI.GetCardData());
                cardUI.SetSelected(selected);
            }
        }
    }

    private void UpdatePreview(HandTypeResult preview = null)
    {
        if (battleManager == null) return;

        if (preview == null)
            preview = battleManager.GetHandArea().PreviewHandType();

        // 有选牌但被诅咒/封禁拦住时，直接提示原因
        bool hasSel = battleManager.GetHandArea().GetSelectedCards().Count > 0;
        if (hasSel && !battleManager.CanPlaySelection(out var blockReason))
        {
            handTypeText.text = preview.IsValid ? preview.type.ToString() : "无有效牌型";
            effectPreviewText.text = string.IsNullOrEmpty(blockReason) ? "" : $"<color=#FF6B6B>{blockReason}</color>";
            return;
        }

        if (preview.IsValid)
        {
            handTypeText.text = preview.type.ToString();

            int predictedDamage = battleManager.PreviewSelectedDamage();
            string effectStr = "";
            if (predictedDamage > 0)
                effectStr += $"<color=#FF0000>预计伤害 {predictedDamage}</color>\n";

            // 用完整预览（含出牌加成与纯数值附魔），这样额外抽牌/防御/治疗也会体现
            var effects = battleManager.PreviewSelectedEffects();
            if (effects != null)
            {
                foreach (var e in effects)
                {
                    // 基础伤害已被「预计伤害」（含各种加成）取代，避免两个数字打架
                    if (e.effectType == HandEffectTable.EffectType.Damage && predictedDamage > 0)
                        continue;
                    effectStr += e.description + "\n";
                }
            }
            effectPreviewText.text = effectStr;
        }
        else
        {
            handTypeText.text = "无有效牌型";
            effectPreviewText.text = "";
        }
    }

    private void UpdateButtonStates()
    {
        if (battleManager == null) return;

        var handArea = battleManager.GetHandArea();
        bool hasSelected = handArea.GetSelectedCards().Count > 0;
        bool inMulliganPhase = battleManager.IsPlayerTurn && !battleManager.IsBattleOver && !battleManager.HasUsedMulligan;
        bool canAct = battleManager.CanPlayerAct; // 新增：是否可操作

        // 出牌按钮：牌型有效，且不受诅咒（禁锢/割裂）或封禁花色限制
        bool canPlay = battleManager.CanPlaySelection(out _);
        playButton.interactable = canPlay && battleManager.IsPlayerTurn && canAct;

        // 结束回合按钮：玩家回合且游戏未结束且可操作
        endTurnButton.interactable = battleManager.IsPlayerTurn && !battleManager.IsBattleOver && canAct;

        // 重抽按钮：重抽阶段且选中了牌
        bool canMulligan = inMulliganPhase && hasSelected;
        mulliganButton.interactable = canMulligan;
        if (mulliganButtonText != null)
        {
            mulliganButtonText.text = battleManager.HasUsedMulligan ? "已重抽" : (hasSelected ? "弃牌重抽" : "请选牌");
        }

        // 改造按钮：恰好选中 1 张且可改造（万能牌/变色/镜牌）
        if (modifyButton != null)
        {
            var selectedCards = handArea.GetSelectedCards();
            bool canModify = selectedCards.Count == 1 &&
                             (selectedCards[0].CanWildcard || selectedCards[0].CanSuitShift || selectedCards[0].CanMirror);
            modifyButton.interactable = canModify && battleManager.IsPlayerTurn && canAct;
        }

        RefreshDestinyHud();
    }

    private void OnCanPlayerActChanged(bool canAct)
    {
        UpdateButtonStates();
    }

    /// <summary>构建某个敌人意图的显示文本（供各槽位使用）</summary>
    private string BuildIntentText(BattleUnit unit, Roguelike.Data.IntentData intent)
    {
        if (intent == null || battleManager == null) return "";

        int value = (intent.type == "Attack" || intent.type == "MultiAttack" || intent.type == "Sunder")
            ? battleManager.GetIntentPreviewDamage(intent, unit)
            : intent.value;

        // 蓄力中：数值已经翻倍（GetIntentPreviewDamage 里算进去了），额外标一下让玩家看得出来
        string chargeTag = IsCharged(unit) ? " <color=#FFD24D>(蓄力×2)</color>" : "";

        switch (intent.type)
        {
            case "Attack":
                return $"<color=#FF6B6B>攻击 {value}</color>{chargeTag}";
            case "Defense":
                return $"<color=#6BCBFF>防御 {value}</color>";
            case "Buff":
            {
                string body;
                if (!string.IsNullOrEmpty(intent.description)) body = intent.description;
                else if (!string.IsNullOrEmpty(intent.status)) body = $"{StatusLabel(intent.status, value, intent.duration)}";
                else body = $"强化 {value}";
                return $"<color=#8FE388>{body}</color>";
            }
            case "MultiAttack":
                return $"<color=#FF6B6B>多重攻击 {value} x{intent.hitCount}</color>{chargeTag}";
            case "Debuff":
            {
                // 和 Buff 一致：填了 description 就用它
                string body = !string.IsNullOrEmpty(intent.description)
                    ? intent.description
                    : StatusLabel(intent.status, value, intent.duration);
                return $"<color=#C08CFF>{body}</color>";
            }
            case "Multi":
                return FormatMultiIntent(unit, intent);
            case "Taunt":
                return "<color=#FFD24D>嘲讽（本回合只能攻击它）</color>";
            case "Charge":
                return "<color=#FFD24D>蓄力（下次攻击伤害翻倍）</color>";
            case "Sunder":
                return $"<color=#FF9A5B>破防攻击 {value}</color>{chargeTag}";
            case "Curse":
            {
                // value = 诅咒附魔 id，hitCount = 张数
                string body = intent.description;
                if (string.IsNullOrEmpty(body))
                {
                    var curse = Roguelike.Data.ConfigLoader.GetEnchantment(intent.value);
                    string curseName = curse != null ? curse.name : $"诅咒#{intent.value}";
                    body = $"诅咒 {Mathf.Max(1, intent.hitCount)} 张牌（{curseName}）";
                }
                return $"<color=#C08CFF>{body}</color>";
            }
            case "Swallow":
            {
                // value = 张数
                string body = !string.IsNullOrEmpty(intent.description)
                    ? intent.description
                    : $"吞噬 {Mathf.Max(1, intent.value)} 张牌";
                return $"<color=#C08CFF>{body}</color>";
            }
            case "Burrow":
            {
                // value = 还需被攻击的次数
                string body = !string.IsNullOrEmpty(intent.description)
                    ? intent.description
                    : $"遁地（受击固定 1 点，{Mathf.Max(1, intent.value)} 次后出来）";
                return $"<color=#FFD24D>{body}</color>";
            }
            case "Summon":
            {
                // value = 敌人 id，hitCount = 数量
                string body = intent.description;
                if (string.IsNullOrEmpty(body))
                {
                    var summonData = Roguelike.Data.ConfigLoader.GetEnemy(intent.value);
                    string summonName = summonData != null ? summonData.name : $"敌人#{intent.value}";
                    body = $"召唤 {Mathf.Max(1, intent.hitCount)} 个 {summonName}";
                }
                return $"<color=#8FE388>{body}</color>";
            }
            case "HealAllies":
            {
                string body = !string.IsNullOrEmpty(intent.description)
                    ? intent.description
                    : $"全体回复 {intent.value} 生命";
                return $"<color=#8FE388>{body}</color>";
            }
            case "Special":
                return "<color=#FFD700>特殊技能</color>";
            default:
                return $"{intent.type} {value}";
        }
    }

    /// <summary>该敌人是否处于蓄力状态（下次攻击伤害翻倍）</summary>
    private static bool IsCharged(BattleUnit unit)
        => unit != null && unit.GetStatusAmount(StatusEffectType.Charge) > 0;

    /// <summary>状态名 + 层数 + 回合数（敌人意图显示用；回合数 &lt;= 0 表示无限持续，不显示）</summary>
    private static string StatusLabel(string status, int value, int duration = -1)
    {
        string name;
        if (!string.IsNullOrEmpty(status) &&
            System.Enum.TryParse(status, true, out StatusEffectType type) &&
            type != StatusEffectType.None)
        {
            name = Roguelike.StatusEffectRegistry.Name(type);
        }
        else
        {
            name = string.IsNullOrEmpty(status) ? "异常状态" : status;
        }

        string text = value > 1 ? $"{name} {value}" : name;
        if (duration > 0) text += $"（{duration}回合）";   // 无限持续不显示回合数
        return text;
    }

    /// <summary>type=Multi 的意图文本：填了 description 就用它，否则把子行动拼成「攻击 8 + 虚弱」</summary>
    private string FormatMultiIntent(BattleUnit unit, Roguelike.Data.IntentData intent)
    {
        if (intent == null) return "多重行动";

        // 描述优先（和其他意图类型一致）
        if (!string.IsNullOrEmpty(intent.description))
            return intent.description;

        if (intent.actions == null || intent.actions.Count == 0) return "多重行动";

        var parts = new List<string>();
        foreach (var a in intent.actions)
        {
            switch (a.type)
            {
                case "Attack":
                case "MultiAttack":
                {
                    int dmg = battleManager != null ? battleManager.GetIntentPreviewDamage(a, unit) : a.value;
                    string suffix = (a.type == "MultiAttack" && a.hitCount > 1) ? $" x{a.hitCount}" : "";
                    string tag = IsCharged(unit) ? " <color=#FFD24D>(蓄力×2)</color>" : "";
                    parts.Add($"<color=#FF6B6B>攻击 {dmg}{suffix}</color>{tag}");
                    break;
                }
                case "Defense":
                    parts.Add($"<color=#6BCBFF>防御 {a.value}</color>");
                    break;
                case "Buff":
                    parts.Add($"<color=#8FE388>{StatusLabel(a.status, a.value, a.duration)}</color>");
                    break;
                case "Debuff":
                    parts.Add($"<color=#C08CFF>{StatusLabel(a.status, a.value, a.duration)}</color>");
                    break;
                case "Taunt":
                    parts.Add("<color=#FFD24D>嘲讽</color>");
                    break;
                case "Charge":
                    parts.Add("<color=#FFD24D>蓄力</color>");
                    break;
                case "Sunder":
                {
                    int sd = battleManager != null ? battleManager.GetIntentPreviewDamage(a, unit) : a.value;
                    parts.Add($"<color=#FF9A5B>破防 {sd}</color>");
                    break;
                }
                case "Curse":
                {
                    var curse = Roguelike.Data.ConfigLoader.GetEnchantment(a.value);
                    string curseName = curse != null ? curse.name : $"诅咒#{a.value}";
                    parts.Add($"<color=#C08CFF>诅咒 {Mathf.Max(1, a.hitCount)} 张（{curseName}）</color>");
                    break;
                }
                case "Swallow":
                    parts.Add($"<color=#C08CFF>吞噬 {Mathf.Max(1, a.value)} 张</color>");
                    break;
                case "Burrow":
                    parts.Add($"<color=#FFD24D>遁地 {Mathf.Max(1, a.value)} 次</color>");
                    break;
                case "Summon":
                {
                    var sd = Roguelike.Data.ConfigLoader.GetEnemy(a.value);
                    parts.Add($"<color=#8FE388>召唤 {Mathf.Max(1, a.hitCount)} 个 {(sd != null ? sd.name : "敌人")}</color>");
                    break;
                }
                case "HealAllies":
                    parts.Add($"<color=#8FE388>全体回复 {a.value}</color>");
                    break;
                default:
                    parts.Add(string.IsNullOrEmpty(a.type)
                        ? "<color=#FF8888>（未设置类型）</color>"
                        : (string.IsNullOrEmpty(a.description) ? a.type : a.description));
                    break;
            }
        }
        return string.Join(" + ", parts);
    }

    private void OnEnemyIntentReady()
    {
        if (battleManager == null) return;

        // 每个敌人各自显示自己的意图
        foreach (var unit in battleManager.GetEnemies())
        {
            if (unit == null || unit.IsDead) continue;
            var slot = FindSlot(unit);
            if (slot == null) continue;
            slot.SetIntent(BuildIntentText(unit, battleManager.GetIntentOf(unit)));
        }
    }

    /// <summary>当前目标变化：刷新槽位高亮 + 预览/出牌按钮（"没有选中对象"提示依赖它）</summary>
    private void OnTargetChanged()
    {
        RefreshEnemySlots();
        UpdatePreview();
        UpdateButtonStates();
    }

    /// <summary>敌人行动完毕：隐藏意图，等下次刷新再出现</summary>
    private void OnEnemyIntentHidden()
    {
        foreach (var slot in enemySlots)
            slot?.SetIntent("");
    }

    // ===== 花色命运 HUD =====

    private void EnsureSuitHud()
    {
        if (suitHudText != null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        // 屏幕正右侧（垂直居中）的花色计数文本
        var go = new GameObject("SuitHudText", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        suitHudText = go.AddComponent<TextMeshProUGUI>();
        if (font != null) suitHudText.font = font;
        suitHudText.fontSize = 22;
        suitHudText.alignment = TextAlignmentOptions.TopRight;
        suitHudText.color = Color.white;
        var rt = suitHudText.rectTransform;
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-20f, 60f);
        rt.sizeDelta = new Vector2(360f, 140f);

        // 「命运一览」按钮
        var btnGo = new GameObject("FateButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(transform, false);
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 0.5f);
        brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = new Vector2(-20f, -60f);
        brt.sizeDelta = new Vector2(180f, 48f);

        var bimg = btnGo.GetComponent<Image>();
        bimg.color = new Color(0.22f, 0.24f, 0.32f, 1f);

        fateButton = btnGo.GetComponent<Button>();
        fateButton.targetGraphic = bimg;
        var colors = fateButton.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        fateButton.colors = colors;
        fateButton.onClick.AddListener(OpenFatePanel);

        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(btnGo.transform, false);
        var ltmp = label.AddComponent<TextMeshProUGUI>();
        if (font != null) ltmp.font = font;
        ltmp.text = "命运一览";
        ltmp.fontSize = 24;
        ltmp.alignment = TextAlignmentOptions.Center;
        ltmp.color = Color.white;
        var lrt = ltmp.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
    }

    private void OpenFatePanel()
    {
        if (UIManager.Instance == null || battleManager == null) return;
        var panel = UIManager.Instance.ShowPanel<FatePanel>();
        panel?.ShowFate(battleManager.GetRunData(), () => UIManager.Instance.Hide<FatePanel>());
    }

    private void EnsureModifyButton()
    {
        if (modifyButton != null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var go = new GameObject("ModifyButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-20f, -140f);
        rt.sizeDelta = new Vector2(180f, 48f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.22f, 0.24f, 0.32f, 1f);

        modifyButton = go.GetComponent<Button>();
        modifyButton.targetGraphic = img;
        var colors = modifyButton.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        modifyButton.colors = colors;
        modifyButton.onClick.AddListener(OpenCardModifier);

        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        var ltmp = label.AddComponent<TextMeshProUGUI>();
        if (font != null) ltmp.font = font;
        ltmp.text = "改造";
        ltmp.fontSize = 24;
        ltmp.alignment = TextAlignmentOptions.Center;
        ltmp.color = Color.white;
        var lrt = ltmp.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
    }

    private void OpenCardModifier()
    {
        if (UIManager.Instance == null || battleManager == null) return;

        var selected = battleManager.GetHandArea().GetSelectedCards();
        CardData target = null;
        foreach (var c in selected)
        {
            if (c.CanWildcard || c.CanSuitShift || c.CanMirror) { target = c; break; }
        }
        if (target == null) return;

        var panel = UIManager.Instance.ShowPanel<CardModifierPanel>();
        panel?.ShowCard(target, battleManager, () => UIManager.Instance.Hide<CardModifierPanel>());
    }

    // ===== 命格 HUD（左侧）=====

    private void EnsureDestinyHud()
    {
        if (destinyHudText != null && destinyButton != null && activeSkillButton != null) return;
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        if (destinyHudText == null)
        {
            var go = new GameObject("DestinyHudText", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            destinyHudText = go.AddComponent<TextMeshProUGUI>();
            if (font != null) destinyHudText.font = font;
            destinyHudText.fontSize = 20;
            destinyHudText.alignment = TextAlignmentOptions.TopLeft;
            destinyHudText.color = Color.white;
            var rt = destinyHudText.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(20f, 70f);
            rt.sizeDelta = new Vector2(360f, 120f);
        }

        if (activeSkillButton == null)
            activeSkillButton = CreateSmallButton("ActiveSkillButton", "主动技能", 0f, OnActiveSkillClicked);
        if (destinyButton == null)
            destinyButton = CreateSmallButton("DestinyButton", "命格", -60f, OpenDestinyPanel);
    }

    // ===== 查看牌堆 / 弃牌堆（点击「牌堆: N」/「弃牌: M」文字）=====

    private bool pileClicksHooked;

    private void EnsurePileTextClicks()
    {
        if (pileClicksHooked) return;
        pileClicksHooked = true;

        HookPileText(deckCountText, () => OpenPileViewer(true));
        HookPileText(discardCountText, () => OpenPileViewer(false));
    }

    /// <summary>给数量文本挂上点击（文字本身即点击区域）</summary>
    private static void HookPileText(TextMeshProUGUI text, UnityEngine.Events.UnityAction onClick)
    {
        if (text == null) return;

        text.raycastTarget = true;

        var btn = text.GetComponent<Button>();
        if (btn == null) btn = text.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = null;
        btn.onClick.AddListener(onClick);
    }

    private void OpenPileViewer(bool deck)
    {
        if (UIManager.Instance == null || battleManager == null) return;

        var cards = deck ? battleManager.GetDeckCards() : battleManager.GetDiscardCards();
        var panel = UIManager.Instance.ShowPanel<DiscardPickerPanel>();
        panel?.ShowViewer(
            deck ? $"牌堆（{cards.Count} 张）" : $"弃牌堆（{cards.Count} 张）",
            cards,
            () => UIManager.Instance.Hide<DiscardPickerPanel>());
    }

    private Button CreateSmallButton(string name, string label, float y, UnityEngine.Events.UnityAction onClick)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(20f, y);
        rt.sizeDelta = new Vector2(200f, 48f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var lgo = new GameObject("Label", typeof(RectTransform));
        lgo.transform.SetParent(go.transform, false);
        var ltmp = lgo.AddComponent<TextMeshProUGUI>();
        if (font != null) ltmp.font = font;
        ltmp.text = label;
        ltmp.fontSize = 22;
        ltmp.alignment = TextAlignmentOptions.Center;
        ltmp.color = Color.white;
        var lrt = ltmp.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        return btn;
    }

    private void RefreshDestinyHud()
    {
        if (battleManager == null) return;
        var rd = battleManager.GetRunData();

        if (destinyHudText != null)
        {
            if (rd != null && rd.HasMainDestiny)
            {
                var suit = (Suit)rd.mainDestinySuit;
                int rank = rd.GetDestinyRank(suit);
                destinyHudText.text = $"主命格 {DestinyInfo.SuitSymbol(suit)} {DestinyInfo.SuitName(suit)} Lv.{rank}\n命运之力 {rd.fatePower}/{RunData.FatePowerMax}";
            }
            else
            {
                destinyHudText.text = "主命格：未确立";
            }
        }

        if (activeSkillButton != null)
        {
            bool ready = rd != null && rd.HasMainDestiny && rd.fatePower >= RunData.FatePowerMax;
            activeSkillButton.interactable = ready && battleManager.IsPlayerTurn && battleManager.CanPlayerAct;
        }
    }

    private void OpenDestinyPanel()
    {
        if (UIManager.Instance == null || battleManager == null) return;
        var panel = UIManager.Instance.ShowPanel<DestinyPanel>();
        panel?.ShowDestiny(battleManager.GetRunData(), () => UIManager.Instance.Hide<DestinyPanel>());
    }

    private void OnActiveSkillClicked()
    {
        if (battleManager == null) return;
        battleManager.ActivateDestinySkill();
        RefreshDestinyHud();
    }

    // ===== 药水栏（遗物栏下方，最左侧）=====

    private void EnsurePotionBar()
    {
        if (potionSlots != null && potionSlots.Length > 0 && potionSlots[0] != null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        Transform parent = (relicSlots != null && relicSlots.Length > 0 && relicSlots[0] != null)
            ? relicSlots[0].transform.parent
            : transform;

        for (int i = 0; i < potionSlots.Length; i++)
        {
            var go = new GameObject($"PotionSlot{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            // 贴左边（potionBarStart 是设计坐标下的中心偏移，这里换算成距左边的边距）
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100, 100);
            rt.anchoredPosition = new Vector2(
                DesignW * 0.5f + potionBarStart.x + i * potionSlotSpacing, potionBarStart.y);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.20f, 0.20f, 0.25f, 0.60f);

            var label = new GameObject("Name", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var ltmp = label.AddComponent<TextMeshProUGUI>();
            if (font != null) ltmp.font = font;
            ltmp.fontSize = 20;
            ltmp.alignment = TextAlignmentOptions.Center;
            ltmp.color = Color.white;
            var lrt = ltmp.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(4, 4);
            lrt.offsetMax = new Vector2(-4, -4);

            var slot = go.AddComponent<PotionSlotUI>();
            slot.iconImage = img;
            slot.nameText = ltmp;
            int idx = i;
            slot.OnClick = () => OnPotionClicked(idx);
            potionSlots[i] = slot;
        }
    }

    private void RefreshPotions()
    {
        if (battleManager == null) return;
        EnsurePotionBar();

        var rd = battleManager.GetRunData();
        for (int i = 0; i < potionSlots.Length; i++)
        {
            if (potionSlots[i] == null) continue;
            if (rd != null && i < rd.PotionIds.Count)
                potionSlots[i].SetPotion(ConfigLoader.GetPotion(rd.PotionIds[i]));
            else
                potionSlots[i].Clear();
        }
    }

    private void OnPotionClicked(int index)
    {
        if (battleManager == null) return;
        if (!battleManager.IsPlayerTurn || !battleManager.CanPlayerAct) return;

        var rd = battleManager.GetRunData();
        if (rd == null || index < 0 || index >= rd.PotionIds.Count) return;

        int potionId = rd.PotionIds[index];
        battleManager.UsePotion(potionId);
        RefreshPotions();
    }

    private void OnSuitTallyChanged(Suit suit, int count)
    {
        RefreshSuitHud();
    }

    private void RefreshSuitHud()
    {
        if (suitHudText == null || battleManager == null) return;

        // 事件专属战斗：不显示命运花色与命运一览
        bool eventBattle = RunDirector.Instance != null && RunDirector.Instance.InEventBattle;
        suitHudText.gameObject.SetActive(!eventBattle);
        if (fateButton != null) fateButton.gameObject.SetActive(!eventBattle);
        if (eventBattle) return;

        var runData = battleManager.GetRunData();
        var dominant = battleManager.GetDominantSuit();

        var sb = new System.Text.StringBuilder();
        sb.Append("花色命运（本场）\n");

        Suit[] order = { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };
        foreach (var s in order)
        {
            int c = battleManager.GetSuitCount(s);
            string seg = $"{SuitSymbol(s)}{c}";
            if (s == dominant && c > 0) seg = $"<color=#FFD700>{seg}</color>";
            sb.Append(seg);
            sb.Append("  ");
        }
        sb.Append("\n");

        bool guaranteedShop = runData != null && runData.battlesSinceShop >= 4;
        int shopSuit = runData != null ? runData.shopSuitIndex : -1;

        // 只有「当前主导花色就是商店槽位」时才提示商店；否则照常显示该花色对应的事件
        if (guaranteedShop && shopSuit >= 0 && (int)dominant == shopSuit)
        {
            sb.Append($"打出 {SuitSymbol(dominant)} → 商店（保底）");
        }
        else
        {
            int eventId = runData != null ? runData.GetSuitEventId(dominant) : 0;
            var ev = ConfigLoader.GetEvent(eventId);
            bool revealAll = runData != null && runData.nextBattleRevealAll;
            if (runData != null && runData.IsSuitHidden(dominant) && !revealAll)
                sb.Append($"最多 {SuitSymbol(dominant)} → ？？？");
            else if (ev != null)
                sb.Append($"最多 {SuitSymbol(dominant)} → {ev.title}");
            else
                sb.Append("打出最多花色决定下个事件");
        }

        suitHudText.text = sb.ToString();
    }

    private static string SuitSymbol(Suit s)
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
}
