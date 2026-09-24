using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 游戏内作弊面板（全部三批）：
///   基础（运行状态 / 资源 / 战斗 / 进度）、遗物、附魔、药水、状态效果、跳到事件、敌人组。
/// 完全运行时构建，不需要预制体；由 UIManager 用快捷键开关（默认 F1）。
/// 列表项点击即生效，不做二次确认（这是作弊面板）。
/// </summary>
public class CheatPanel : BasePanel
{
    private const int TabBasic = 0;
    private const int TabRelic = 1;
    private const int TabEnchant = 2;
    private const int TabPotion = 3;
    private const int TabStatus = 4;
    private const int TabEvent = 5;
    private const int TabEncounter = 6;

    private bool built;
    private TMP_FontAsset font;

    // 分页：每页的行都挂在同一个滚动内容里，切页时按行 SetActive（布局组会自动跳过隐藏项）
    private Transform content;
    private readonly List<List<GameObject>> pageRows = new List<List<GameObject>>();
    private readonly List<Image> tabImages = new List<Image>();
    private int currentTab = TabBasic;
    private int buildingTab = TabBasic;

    // 基础页：自动刷新的状态文本
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI enemyText;
    private float refreshTimer;

    // 附魔页参数
    private int enchantSuit;
    private int enchantRank = 14;
    private TextMeshProUGUI enchantParamLabel;

    // 状态效果页参数
    private int statusAmount = 1;
    private int statusDuration = 1;
    private int statusTarget = 1;   // 0=玩家 1=敌人
    private TextMeshProUGUI statusParamLabel;

    // 状态效果页：选择给哪个敌人
    private Transform statusEnemyRow;
    private BattleManager statusEnemyBattle;
    private readonly List<Image> statusEnemyButtons = new List<Image>();
    private readonly List<int> statusEnemyIndices = new List<int>();
    private int statusEnemyIndex;
    private int statusEnemyAliveCount = -1;

    // 事件 / 敌人组页：搜索 + 过滤 + 列表（行直接挂在主内容里，不套容器，避免嵌套布局高度不同步）
    private string eventSearch = "";
    private string encounterSearch = "";
    private int eventTheme;
    private int encounterPool;
    private readonly List<GameObject> eventRows = new List<GameObject>();
    private readonly List<GameObject> encounterRows = new List<GameObject>();
    private TextMeshProUGUI eventCountLabel;
    private TextMeshProUGUI encounterCountLabel;
    private readonly List<Image> eventThemeButtons = new List<Image>();
    private readonly List<Image> encounterPoolButtons = new List<Image>();
    private string[] encounterPools = new string[0];
    private bool eventListDirty = true;
    private bool encounterListDirty = true;

    private static readonly string[] ThemeNames = { "全部", "黑桃", "红桃", "梅花", "方块" };

    public override void Init()
    {
        EnsureBuilt();
        RefreshStatus();
        RebuildEventList();
        RebuildEncounterList();
        RefreshStatusEnemyRow();
    }

    private void Update()
    {
        // 不在战斗中（战斗结束 / 已经离场）→ 自动关掉
        if (!CheatToggle.InBattle)
        {
            Hide();
            return;
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.2f;
            RefreshStatus();
        }

        // 搜索框内容变化 → 重建列表
        if (eventListDirty) RebuildEventList();
        if (encounterListDirty) RebuildEncounterList();

        // 换战斗后重建「选择敌人」
        RefreshStatusEnemyRow();
    }

    // ================= 构建 =================

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        for (int i = 0; i < 7; i++) pageRows.Add(new List<GameObject>());

        var overlay = gameObject.GetComponent<Image>();
        if (overlay == null) overlay = gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.80f);
        overlay.raycastTarget = true;

        var rootRt = gameObject.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
        }

        var card = new GameObject("CheatCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1180f, 1000f);
        card.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 0.99f);

        var title = CreateText(card.transform, "Title", font, 28, TextAlignmentOptions.Left, new Vector2(700f, 40f));
        AnchorTop(title.rectTransform, 20f, 16f, 0f, 1f);
        title.text = "<color=#FFD24D><b>作弊面板</b></color>   <size=70%>F1 开关</size>";

        var closeBtn = MakeButton(card.transform, "关闭", new Vector2(110f, 40f), () =>
        {
            if (UIManager.Instance != null) UIManager.Instance.HidePanel("CheatPanel");
        });
        AnchorTop(closeBtn.GetComponent<RectTransform>(), -20f, 16f, 1f, 1f);

        BuildTabBar(card.transform);

        // 滚动区域
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(18f, 18f);
        srt.offsetMax = new Vector2(-18f, -116f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.004f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport.transform, false);
        content = contentGo.transform;
        var cct = contentGo.GetComponent<RectTransform>();
        cct.anchorMin = new Vector2(0f, 1f);
        cct.anchorMax = new Vector2(1f, 1f);
        cct.pivot = new Vector2(0.5f, 1f);
        cct.offsetMin = Vector2.zero;
        cct.offsetMax = Vector2.zero;
        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = cct;
        scroll.viewport = vrt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;

        // 各页内容（按顺序构建，行会注册到对应页）
        BuildBasicPage();
        BuildRelicPage();
        BuildEnchantPage();
        BuildPotionPage();
        BuildStatusPage();
        BuildEventPage();
        BuildEncounterPage();

        SelectTab(TabBasic);
    }

    private void BuildTabBar(Transform card)
    {
        string[] names = { "基础", "遗物", "附魔", "药水", "状态", "事件", "敌人组" };

        var bar = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bar.transform.SetParent(card, false);
        var brt = bar.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 1f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.offsetMin = new Vector2(18f, 0f);
        brt.offsetMax = new Vector2(-18f, 0f);
        brt.anchoredPosition = new Vector2(0f, -66f);
        brt.sizeDelta = new Vector2(-36f, 40f);

        var hlg = bar.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var go = MakeButton(bar.transform, names[i], new Vector2(120f, 36f), () => SelectTab(index));
            tabImages.Add(go.GetComponent<Image>());
        }
    }

    private void SelectTab(int index)
    {
        currentTab = index;

        for (int p = 0; p < pageRows.Count; p++)
        {
            bool show = p == index;
            foreach (var row in pageRows[p])
                if (row != null) row.SetActive(show);
        }

        for (int i = 0; i < tabImages.Count; i++)
        {
            if (tabImages[i] == null) continue;
            tabImages[i].color = i == index
                ? new Color(0.32f, 0.45f, 0.75f, 1f)
                : new Color(0.20f, 0.22f, 0.30f, 1f);
        }
    }

    // ================= 各页 =================

    private void BuildBasicPage()
    {
        BeginPage(TabBasic);

        AddSection("运行状态");
        statusText = AddLabel("", 20, Color.white, 96f);
        enemyText = AddLabel("", 20, new Color(1f, 0.82f, 0.6f), 64f);

        AddSection("资源");
        AddButtons(
            ("金币 +10", () => AddGold(10)),
            ("金币 +100", () => AddGold(100)),
            ("金币 -10", () => AddGold(-10)),
            ("金币清零", () => SetGold(0)));
        AddButtons(
            ("治疗 10", () => ChangeHp(10)),
            ("受伤 10", () => ChangeHp(-10)),
            ("受伤 50", () => ChangeHp(-50)),
            ("回满血", FullHeal));
        AddButtons(
            ("最大生命 +5", () => AddMaxHp(5)),
            ("最大生命 +20", () => AddMaxHp(20)),
            ("最大生命 -5", () => AddMaxHp(-5)));

        AddSection("战斗");
        AddButtons(
            ("对敌伤害 5", () => DealEnemyDamage(5)),
            ("对敌伤害 20", () => DealEnemyDamage(20)),
            ("对敌伤害 100", () => DealEnemyDamage(100)),
            ("秒杀目标", KillEnemy));
        AddButtons(
            ("抽 1 张", () => DrawCards(1)),
            ("抽 3 张", () => DrawCards(3)),
            ("玩家回满血", FullHeal),
            ("结束玩家回合", () => RunDirector.Instance?.BattleManager?.EndPlayerTurn()));
        AddButtons(
            ("给敌人 +5 防御", () => AddEnemyDefense(5)),
            ("清空敌人防御", ClearEnemyDefense),
            ("玩家 +10 防御", () => AddPlayerDefense(10)));

        AddSection("进度");
        AddButtons(
            ("进入下一场", () => RunDirector.Instance?.DebugEnterNextBattle()),
            ("跳到 Boss", () => CallPrivate(RunDirector.Instance, "EnterBossBattle")),
            ("打开商店", OpenShop),
            ("回到战斗面板", () =>
            {
                var d = RunDirector.Instance;
                if (d != null && d.BattleManager != null && !d.BattleManager.IsBattleOver && UIManager.Instance != null)
                    UIManager.Instance.ShowPanel<BattlePanel>();
            }));

        EndPage();
    }

    private void BuildRelicPage()
    {
        BeginPage(TabRelic);

        var cfg = ConfigLoader.Config;
        AddSection("遗物（点击直接获得）");

        if (cfg == null || cfg.relics.Count == 0)
        {
            AddLabel("（没有遗物配置）", 20, Color.gray, 40f);
        }
        else
        {
            foreach (var relic in cfg.relics)
            {
                var r = relic;
                AddListRow($"{r.id}  <color={RarityUtil.ColorHex(r.rarity)}>{r.name}</color>   <size=80%>{r.description}</size>",
                    40f, () =>
                    {
                        var run = Run;
                        if (run == null) return;
                        if (!run.TryAddRelic(r.id)) Debug.LogWarning("[Cheat] 遗物槽已满或已拥有");
                    });
            }
        }

        EndPage();
    }

    private void BuildEnchantPage()
    {
        BeginPage(TabEnchant);

        AddSection("附魔（选好花色/点数后点附魔）");

        enchantParamLabel = AddLabel("", 20, new Color(0.8f, 0.9f, 1f), 34f);
        RefreshEnchantParamLabel();

        AddButtons(
            ("花色 -", () => { enchantSuit = (enchantSuit + 3) % 4; RefreshEnchantParamLabel(); }),
            ("花色 +", () => { enchantSuit = (enchantSuit + 1) % 4; RefreshEnchantParamLabel(); }),
            ("点数 -", () => { enchantRank = Mathf.Max(2, enchantRank - 1); RefreshEnchantParamLabel(); }),
            ("点数 +", () => { enchantRank = Mathf.Min(14, enchantRank + 1); RefreshEnchantParamLabel(); }));

        var cfg = ConfigLoader.Config;
        if (cfg == null || cfg.enchantments.Count == 0)
        {
            AddLabel("（没有附魔配置）", 20, Color.gray, 40f);
        }
        else
        {
            foreach (var ench in cfg.enchantments)
            {
                var e = ench;
                AddListRow($"{e.id}  <color={RarityUtil.ColorHex(e.rarity)}>{e.name}</color>   <size=80%>{e.description}</size>",
                    38f, () => ApplyEnchant(e));
            }
        }

        EndPage();
    }

    private void BuildPotionPage()
    {
        BeginPage(TabPotion);

        var cfg = ConfigLoader.Config;
        AddSection("药水（点击直接获得）");

        if (cfg == null || cfg.potions.Count == 0)
        {
            AddLabel("（没有药水配置）", 20, Color.gray, 40f);
        }
        else
        {
            foreach (var potion in cfg.potions)
            {
                var p = potion;
                AddListRow($"{p.id}  {p.name}   <size=80%>{p.description}</size>", 40f, () =>
                {
                    var run = Run;
                    if (run == null) return;
                    if (!run.TryAddPotion(p.id)) Debug.LogWarning("[Cheat] 药水槽已满");
                });
            }
        }

        EndPage();
    }

    private void BuildStatusPage()
    {
        BeginPage(TabStatus);

        AddSection("状态效果");

        statusParamLabel = AddLabel("", 20, new Color(0.9f, 0.85f, 1f), 34f);
        RefreshStatusParamLabel();

        AddButtons(
            ("层数 -", () => { statusAmount = Mathf.Max(0, statusAmount - 1); RefreshStatusParamLabel(); }),
            ("层数 +", () => { statusAmount++; RefreshStatusParamLabel(); }),
            ("持续 -", () => { statusDuration = Mathf.Max(-1, statusDuration - 1); RefreshStatusParamLabel(); }),
            ("持续 +", () => { statusDuration++; RefreshStatusParamLabel(); }));
        AddButtons(
            ("目标：玩家", () => { statusTarget = 0; RefreshStatusParamLabel(); }),
            ("目标：敌人", () => { statusTarget = 1; RefreshStatusParamLabel(); }),
            ("清除目标所有状态", () =>
            {
                var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
                var target = GetTarget(bm);
                if (target == null) return;
                target.StatusEffects.Clear();
            }));

        // 选择给哪个敌人（目标为敌人时生效；换战斗会自动重建）
        statusEnemyRow = MakeRow(40f);

        foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
        {
            if (type == StatusEffectType.None) continue;
            var t = type;
            AddListRow($"{StatusEffectNames.Name(t)}   <size=80%><color=#888888>{t}</color></size>", 36f, () =>
            {
                var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
                var target = GetTarget(bm);
                if (target == null) return;
                target.AddStatus(t, statusAmount, statusDuration);
                Debug.Log($"[Cheat] 给{(statusTarget == 0 ? "玩家" : target.Name)}施加 {t} x{statusAmount}（{statusDuration}）");
            });
        }

        EndPage();
    }

    private void BuildEventPage()
    {
        BeginPage(TabEvent);

        AddSection("跳到事件（跳过当前战斗）");

        var searchGo = MakeInput("搜索事件 id / 标题 / 主题", 300f, v => { eventSearch = v; eventListDirty = true; });
        var searchRow = new GameObject("SearchRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        searchRow.transform.SetParent(content, false);
        searchRow.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 40f);
        var srh = searchRow.GetComponent<HorizontalLayoutGroup>();
        srh.spacing = 6f;
        srh.childControlWidth = true;
        srh.childControlHeight = true;
        srh.childForceExpandWidth = true;
        srh.childForceExpandHeight = true;
        searchGo.transform.SetParent(searchRow.transform, false);
        Register(searchRow);

        // 主题筛选按钮
        var filterRow = new GameObject("ThemeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        filterRow.transform.SetParent(content, false);
        filterRow.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 38f);
        var frh = filterRow.GetComponent<HorizontalLayoutGroup>();
        frh.spacing = 6f;
        frh.childControlWidth = true;
        frh.childControlHeight = true;
        frh.childForceExpandWidth = true;
        frh.childForceExpandHeight = true;
        Register(filterRow);

        eventThemeButtons.Clear();
        for (int i = 0; i < ThemeNames.Length; i++)
        {
            int index = i;
            var go = MakeButton(filterRow.transform, ThemeNames[i], new Vector2(100f, 36f), () =>
            {
                eventTheme = index;
                eventListDirty = true;
                RefreshFilterHighlight(eventThemeButtons, index);
            });
            eventThemeButtons.Add(go.GetComponent<Image>());
        }
        RefreshFilterHighlight(eventThemeButtons, eventTheme);

        eventCountLabel = AddLabel("", 18, new Color(0.7f, 0.7f, 0.75f), 28f);
        AddButtons(("重新载入事件列表", () => eventListDirty = true));

        EndPage();
    }

    private void BuildEncounterPage()
    {
        BeginPage(TabEncounter);

        AddSection("敌人组（点击直接用该组合开战）");

        var searchGo = MakeInput("搜索组合 id / 名字 / 池 / 敌人名", 300f, v => { encounterSearch = v; encounterListDirty = true; });
        var searchRow = new GameObject("SearchRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        searchRow.transform.SetParent(content, false);
        searchRow.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 40f);
        var srh = searchRow.GetComponent<HorizontalLayoutGroup>();
        srh.spacing = 6f;
        srh.childControlWidth = true;
        srh.childControlHeight = true;
        srh.childForceExpandWidth = true;
        srh.childForceExpandHeight = true;
        searchGo.transform.SetParent(searchRow.transform, false);
        Register(searchRow);

        encounterCountLabel = AddLabel("", 18, new Color(0.7f, 0.7f, 0.75f), 28f);

        // 池筛选按钮（每行 5 个）
        var cfg = ConfigLoader.Config;
        var pools = new List<string> { "全部" };
        if (cfg != null)
        {
            foreach (var enc in cfg.encounters)
                if (!string.IsNullOrEmpty(enc.pool) && !pools.Contains(enc.pool)) pools.Add(enc.pool);
        }
        encounterPools = pools.GetRange(1, pools.Count - 1).ToArray();

        encounterPoolButtons.Clear();
        for (int i = 0; i < pools.Count; i += 5)
        {
            var row = new GameObject("PoolRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(content, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 36f);
            Register(row);

            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            for (int j = i; j < Mathf.Min(i + 5, pools.Count); j++)
            {
                int index = j;
                var go = MakeButton(row.transform, pools[j], new Vector2(100f, 34f), () =>
                {
                    encounterPool = index;
                    encounterListDirty = true;
                    RefreshFilterHighlight(encounterPoolButtons, index);
                });
                encounterPoolButtons.Add(go.GetComponent<Image>());
            }
        }
        RefreshFilterHighlight(encounterPoolButtons, encounterPool);

        AddButtons(("重新载入敌人组", () => encounterListDirty = true));

        EndPage();
    }

    // ================= 列表构建 =================

    private void RebuildEventList()
    {
        if (!built) return;
        eventListDirty = false;

        ClearRows(eventRows, TabEvent);

        var cfg = ConfigLoader.Config;
        if (cfg == null) return;

        BeginPage(TabEvent);   // 行要登记到事件页，否则会跑到基础页去

        string q = string.IsNullOrEmpty(eventSearch) ? null : eventSearch.Trim().ToLowerInvariant();
        string themeWant = eventTheme == 0 ? null : ((Suit)(eventTheme - 1)).ToString();

        int count = 0;
        foreach (var ev in cfg.events)
        {
            if (themeWant != null && ev.theme != themeWant) continue;
            if (q != null && !($"{ev.id} {ev.title} {ev.theme}".ToLowerInvariant().Contains(q))) continue;

            var captured = ev;
            eventRows.Add(AddListRow($"{ev.id}  {ev.title}   <size=80%><color=#888888>[{ThemeName(ev.theme)}] {ev.options.Count} 选项</color></size>",
                38f, () =>
                {
                    RunDirector.Instance?.DebugEnterEvent(captured.id);
                    CloseSelf();   // 进入事件后直接关掉作弊面板
                }));
            count++;
        }

        if (count == 0) eventRows.Add(AddListRow("（没有匹配的事件）", 34f, null));
        if (eventCountLabel != null) eventCountLabel.text = $"共 {count} 个事件";

        EndPage();
    }

    private void RebuildEncounterList()
    {
        if (!built) return;
        encounterListDirty = false;

        ClearRows(encounterRows, TabEncounter);

        var cfg = ConfigLoader.Config;
        if (cfg == null) return;

        // 池筛选按钮（第一次构建时收集）
        if (encounterPools.Length == 0)
        {
            var pools = new List<string>();
            foreach (var enc in cfg.encounters)
                if (!string.IsNullOrEmpty(enc.pool) && !pools.Contains(enc.pool)) pools.Add(enc.pool);
            pools.Sort(StringComparer.Ordinal);
            encounterPools = pools.ToArray();
        }

        BeginPage(TabEncounter);   // 行要登记到敌人组页

        string q = string.IsNullOrEmpty(encounterSearch) ? null : encounterSearch.Trim().ToLowerInvariant();
        string poolWant = (encounterPool <= 0 || encounterPool > encounterPools.Length) ? null : encounterPools[encounterPool - 1];

        int count = 0;
        foreach (var enc in cfg.encounters)
        {
            if (poolWant != null && enc.pool != poolWant) continue;
            if (q != null && !($"{enc.id} {enc.name} {enc.pool} {EnemyNames(enc)}".ToLowerInvariant().Contains(q))) continue;

            var captured = enc;
            encounterRows.Add(AddListRow($"{enc.id}  {enc.name}   <size=80%><color=#888888>[{enc.pool}] 权重{enc.weight} · {EnemyNames(enc)}</color></size>",
                38f, () =>
                {
                    RunDirector.Instance?.DebugStartEncounter(captured.id);
                    CloseSelf();   // 开战后直接关掉作弊面板
                }));
            count++;
        }

        if (count == 0) encounterRows.Add(AddListRow("（没有匹配的敌人组）", 34f, null));
        if (encounterCountLabel != null) encounterCountLabel.text = $"共 {count} 个组合";

        EndPage();
    }

    /// <summary>清掉某组列表行（同时从分页登记里移除，避免切页时访问已销毁对象）</summary>
    private void ClearRows(List<GameObject> rows, int tab)
    {
        foreach (var go in rows)
        {
            if (go == null) continue;
            pageRows[tab].Remove(go);
            go.transform.SetParent(null, false);
            Destroy(go);
        }
        rows.Clear();
    }

    private void CloseSelf()
    {
        if (UIManager.Instance != null) UIManager.Instance.HidePanel("CheatPanel");
    }

    private static string EnemyNames(EncounterData enc)
    {
        if (enc == null || enc.enemies == null || enc.enemies.Count == 0) return "（空）";

        var names = new List<string>();
        foreach (int id in enc.enemies)
        {
            var e = ConfigLoader.GetEnemy(id);
            names.Add(e != null ? e.name : $"#{id}");
        }
        return string.Join("、", names);
    }

    private static string ThemeName(string theme)
    {
        switch (theme)
        {
            case "Spade": return "黑桃";
            case "Heart": return "红桃";
            case "Club": return "梅花";
            case "Diamond": return "方块";
            default: return string.IsNullOrEmpty(theme) ? "无" : theme;
        }
    }

    // ================= 状态刷新 =================

    private void RefreshStatus()
    {
        if (statusText == null) return;

        var director = RunDirector.Instance;
        var run = director != null ? director.RunData : null;
        if (run == null)
        {
            statusText.text = "（尚未开始游戏）";
            if (enemyText != null) enemyText.text = "";
            return;
        }

        statusText.text =
            $"种子 {run.seed}    章节 {run.actId}    战斗序号 {run.battleIndex}\n" +
            $"生命 {run.CurrentHp}/{run.MaxHp}    金币 {run.Gold}\n" +
            $"遗物 {run.RelicIds.Count}    药水 {run.PotionIds.Count}    已附魔牌 {run.GetEnchantedCardKeys().Count}";

        if (enemyText == null) return;

        var bm = director.BattleManager;
        if (bm == null)
        {
            enemyText.text = "（不在战斗中）";
            return;
        }

        var player = bm.GetPlayer();
        var enemy = bm.GetEnemy();
        string line = enemy != null
            ? $"当前目标: {enemy.Name}   HP {enemy.CurrentHp}/{enemy.MaxHp}   防御 {enemy.Defense}"
            : $"当前目标: 无（存活敌人 {bm.GetAliveEnemies().Count}）";

        enemyText.text = $"{line}\n玩家防御 {player.Defense}    玩家回合 {bm.IsPlayerTurn}    可操作 {bm.CanPlayerAct}";
    }

    private void RefreshEnchantParamLabel()
    {
        if (enchantParamLabel == null) return;
        var suit = (Suit)enchantSuit;
        enchantParamLabel.text = $"当前：<color=#FFD24D>{enchantRank} {DestinyInfo.SuitSymbol(suit)}</color>（11=J 12=Q 13=K 14=A）";
    }

    private void RefreshStatusParamLabel()
    {
        if (statusParamLabel == null) return;

        string targetText = "玩家";
        if (statusTarget == 1)
        {
            var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
            var picked = GetTarget(bm);
            targetText = picked != null ? picked.Name : "敌人";
        }

        statusParamLabel.text = $"层数 <color=#FFD24D>{statusAmount}</color>    持续 <color=#FFD24D>{statusDuration}</color>（-1=永久）    目标 <color=#FFD24D>{targetText}</color>";
    }

    private static void RefreshFilterHighlight(List<Image> buttons, int active)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].color = i == active
                ? new Color(0.32f, 0.45f, 0.75f, 1f)
                : new Color(0.20f, 0.22f, 0.30f, 1f);
        }
    }

    // ================= 具体作弊操作 =================

    private static RunData Run => RunDirector.Instance != null ? RunDirector.Instance.RunData : null;

    private BattleUnit GetTarget(BattleManager bm)
    {
        if (bm == null) return null;
        if (statusTarget == 0) return bm.GetPlayer();

        // 敌人：优先用「选择敌人」里选的那个
        var enemies = bm.GetEnemies();
        if (statusEnemyIndex >= 0 && statusEnemyIndex < enemies.Count)
        {
            var picked = enemies[statusEnemyIndex];
            if (picked != null && !picked.IsDead) return picked;
        }
        return bm.GetEnemy();   // 兜底：当前选中的目标
    }

    /// <summary>换战斗（或敌人数量变化）就重建「选择敌人」按钮行</summary>
    private void RefreshStatusEnemyRow()
    {
        var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
        int alive = bm != null ? bm.GetAliveEnemies().Count : 0;

        if (bm == statusEnemyBattle && statusEnemyButtons.Count > 0 && alive == statusEnemyAliveCount) return;

        statusEnemyBattle = bm;
        statusEnemyAliveCount = alive;

        foreach (var img in statusEnemyButtons)
            if (img != null) Destroy(img.gameObject);
        statusEnemyButtons.Clear();
        statusEnemyIndices.Clear();
        statusEnemyIndex = 0;

        if (bm == null || statusEnemyRow == null) return;

        var enemies = bm.GetEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null || enemies[i].IsDead) continue;

            int idx = i;
            var go = MakeButton(statusEnemyRow, $"{i + 1}. {enemies[i].Name}", new Vector2(100f, 36f), () =>
            {
                statusEnemyIndex = idx;
                RefreshStatusEnemyHighlight();
                RefreshStatusParamLabel();
            });
            statusEnemyButtons.Add(go.GetComponent<Image>());
            statusEnemyIndices.Add(i);
        }

        // 只有一个敌人时直接选它
        if (statusEnemyButtons.Count > 0) statusEnemyIndex = statusEnemyIndices[0];

        RefreshStatusEnemyHighlight();
        RefreshStatusParamLabel();
    }

    private void RefreshStatusEnemyHighlight()
    {
        for (int i = 0; i < statusEnemyButtons.Count; i++)
        {
            if (statusEnemyButtons[i] == null) continue;
            bool active = i < statusEnemyIndices.Count && statusEnemyIndices[i] == statusEnemyIndex;
            statusEnemyButtons[i].color = active
                ? new Color(0.32f, 0.45f, 0.75f, 1f)
                : new Color(0.20f, 0.22f, 0.30f, 1f);
        }
    }

    private void AddGold(int delta)
    {
        var r = Run;
        if (r != null) r.Gold = Mathf.Max(0, r.Gold + delta);
    }

    private void SetGold(int value)
    {
        var r = Run;
        if (r != null) r.Gold = Mathf.Max(0, value);
    }

    private void ChangeHp(int delta)
    {
        var r = Run;
        if (r == null) return;
        r.CurrentHp = Mathf.Clamp(r.CurrentHp + delta, 0, r.MaxHp);
    }

    private void FullHeal()
    {
        var r = Run;
        if (r != null) r.CurrentHp = r.MaxHp;
    }

    private void AddMaxHp(int delta)
    {
        var r = Run;
        if (r == null) return;
        r.MaxHp = Mathf.Max(1, r.MaxHp + delta);
        r.CurrentHp = Mathf.Min(r.CurrentHp, r.MaxHp);
    }

    private void ApplyEnchant(EnchantmentData ench)
    {
        var run = Run;
        if (run == null || ench == null) return;

        run.AddEnchantment(enchantRank, (Suit)enchantSuit, ench.id);
        Debug.Log($"[Cheat] 给 {enchantRank}{(Suit)enchantSuit} 附魔 {ench.name}");

        var battle = UIManager.Instance != null ? UIManager.Instance.GetPanel<BattlePanel>() : null;
        battle?.ForceRefreshHand();
    }

    private void DealEnemyDamage(int amount)
    {
        var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
        var enemy = bm != null ? bm.GetEnemy() : null;
        if (enemy == null) return;

        enemy.TakeDamage(amount, bm.GetPlayer());
        CallPrivate(bm, "CheckBattleEnd");
    }

    private void KillEnemy()
    {
        var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
        var enemy = bm != null ? bm.GetEnemy() : null;
        if (enemy == null) return;

        enemy.CurrentHp = 0;
        CallPrivate(bm, "CheckBattleEnd");
    }

    private void AddEnemyDefense(int amount)
    {
        var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
        var enemy = bm != null ? bm.GetEnemy() : null;
        if (enemy != null) enemy.AddDefense(amount);
    }

    private void ClearEnemyDefense()
    {
        var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
        var enemy = bm != null ? bm.GetEnemy() : null;
        if (enemy != null) enemy.ClearDefense();
    }

    private void AddPlayerDefense(int amount)
    {
        var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
        bm?.GetPlayer()?.AddDefense(amount);
    }

    private void DrawCards(int count)
    {
        var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
        if (bm == null || count <= 0) return;

        var drawn = bm.GetDeckPile().Draw(count);
        var overflow = bm.GetHandArea().AddCards(drawn);
        if (overflow != null && overflow.Count > 0) bm.GetDeckPile().ReturnToDeck(overflow);
    }

    private void OpenShop()
    {
        var director = RunDirector.Instance;
        if (director == null) return;

        bool ok = director.OpenShop(() =>
        {
            if (director.BattleManager != null && !director.BattleManager.IsBattleOver && UIManager.Instance != null)
                UIManager.Instance.ShowPanel<BattlePanel>();
        });
        if (!ok) Debug.LogWarning("[Cheat] 打开商店失败");
    }

    private static object CallPrivate(object obj, string method, params object[] args)
    {
        if (obj == null) return null;

        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null)
        {
            Debug.LogWarning($"[Cheat] 找不到方法 {method}");
            return null;
        }
        return m.Invoke(obj, args);
    }

    // ================= UI 便捷构造 =================

    private void BeginPage(int tab)
    {
        buildingTab = tab;
    }

    private void EndPage()
    {
        buildingTab = TabBasic;
    }

    private void Register(GameObject go)
    {
        if (go == null) return;
        pageRows[buildingTab].Add(go);
        go.SetActive(currentTab == buildingTab);
    }

    private void AddSection(string title)
    {
        var t = CreateText(content, "Section_" + title, font, 23, TextAlignmentOptions.Left, new Vector2(100f, 38f));
        t.text = $"<color=#FFD24D><b>{title}</b></color>";
        t.color = new Color(1f, 0.85f, 0.4f);
        Register(t.gameObject);
    }

    private TextMeshProUGUI AddLabel(string text, float size, Color color, float height)
    {
        var t = CreateText(content, "Label", font, size, TextAlignmentOptions.TopLeft, new Vector2(100f, height));
        t.text = text;
        t.color = color;
        t.enableWordWrapping = true;
        Register(t.gameObject);
        return t;
    }

    private void AddButtons(params (string label, Action action)[] items)
    {
        var row = MakeRow(46f);

        foreach (var item in items)
        {
            var captured = item.action;
            MakeButton(row, item.label, new Vector2(100f, 42f), () => captured?.Invoke());
        }
    }

    /// <summary>建一个横向按钮行（会登记到当前分页，切页时自动显示/隐藏）</summary>
    private Transform MakeRow(float height)
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(content, false);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, height);
        Register(row);

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        return row.transform;
    }

    /// <summary>整行一个按钮（列表项），返回该行以便列表重建时清理</summary>
    private GameObject AddListRow(string text, float height, Action onClick)
    {
        var go = AddListRowTo(content, text, height, onClick);
        if (go != null) Register(go);
        return go;
    }

    private GameObject AddListRowTo(Transform parent, string text, float height, Action onClick)
    {
        var go = new GameObject("ListRow", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, height);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.17f, 0.23f, 1f);

        if (onClick != null)
        {
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.28f, 0.32f, 0.44f, 1f);
            colors.pressedColor = new Color(0.12f, 0.14f, 0.2f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());
        }
        else
        {
            img.color = new Color(0.13f, 0.13f, 0.17f, 1f);
        }

        var label = CreateText(go.transform, "Label", font, 19, TextAlignmentOptions.Left, new Vector2(100f, height));
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(12f, 2f);
        lrt.offsetMax = new Vector2(-12f, -2f);
        label.text = text;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    private TMP_InputField MakeInput(string placeholder, float width, Action<string> onChanged)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image));
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 38f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.16f, 0.17f, 0.22f, 1f);

        var input = go.AddComponent<TMP_InputField>();

        var areaGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        areaGo.transform.SetParent(go.transform, false);
        var areaRt = areaGo.GetComponent<RectTransform>();
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(8f, 4f);
        areaRt.offsetMax = new Vector2(-8f, -4f);

        var text = CreateText(areaGo.transform, "Text", font, 19, TextAlignmentOptions.Left, new Vector2(10f, 30f));
        var textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var ph = CreateText(areaGo.transform, "Placeholder", font, 19, TextAlignmentOptions.Left, new Vector2(10f, 30f));
        ph.text = placeholder;
        ph.color = new Color(0.55f, 0.55f, 0.6f);
        ph.fontStyle = FontStyles.Italic;
        var phRt = ph.rectTransform;
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = Vector2.zero;
        phRt.offsetMax = Vector2.zero;

        input.textViewport = areaRt;
        input.textComponent = text;
        input.placeholder = ph;
        input.targetGraphic = bg;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.onValueChanged.AddListener(v => onChanged?.Invoke(v));

        return input;
    }

    private GameObject MakeButton(Transform parent, string label, Vector2 size, Action onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.20f, 0.22f, 0.30f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.14f, 0.16f, 0.22f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var text = CreateText(go.transform, "Label", font, 19, TextAlignmentOptions.Center, size);
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(4f, 2f);
        trt.offsetMax = new Vector2(-4f, -2f);
        text.text = label;

        return go;
    }

    private static void AnchorTop(RectTransform rt, float x, float y, float anchorX, float anchorY)
    {
        rt.anchorMin = new Vector2(anchorX, 1f);
        rt.anchorMax = new Vector2(anchorX, 1f);
        rt.pivot = new Vector2(anchorX, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size,
                                             TextAlignmentOptions align, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.rectTransform.sizeDelta = sizeDelta;
        return t;
    }
}
