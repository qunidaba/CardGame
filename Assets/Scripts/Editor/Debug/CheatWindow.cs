using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 作弊调试窗口（仅编辑器，且需进入 Play 模式）
/// 菜单：Tools/作弊调试窗口
/// </summary>
public class CheatWindow : EditorWindow
{
    [MenuItem("Tools/作弊调试窗口")]
    public static void ShowWindow()
    {
        GetWindow<CheatWindow>("作弊调试").Show();
    }

    // ---- 输入字段 ----
    private int newRunSeed = 0;
    private int goldValue = 100;
    private int hpValue = 10;
    private int maxHpValue = 5;
    private int relicIndex = 0;
    private int enchantIndex = 0;
    private int suitIndex = 0;
    private int cardRank = 14;
    private int potionIndex = 0;
    private int enemyDamage = 20;
    private int enemySetHp = 1;
    private int drawCount = 1;
    private int statusIndex = 0;
    private int statusAmount = 1;
    private int statusDuration = 1;
    private int statusTarget = 1; // 0=玩家 1=敌人
    private int destinyPointDelta = 1;
    private int eventJumpIndex = 0;

    // 跳到事件：搜索 / 主题筛选 / 滚动位置 / 过滤结果（存 cfg.events 下标）
    private string eventSearch = "";
    private int eventThemeFilter = 0;
    private Vector2 eventScroll;
    private readonly System.Collections.Generic.List<int> filteredEventIndices = new System.Collections.Generic.List<int>();

    // 指定敌人组开战：搜索 / 池筛选 / 滚动位置 / 过滤结果（存 cfg.encounters 下标）
    private string encounterSearch = "";
    private int encounterPoolFilter = 0;
    private Vector2 encounterScroll;
    private int encounterIndex = 0;
    private readonly System.Collections.Generic.List<int> filteredEncounterIndices = new System.Collections.Generic.List<int>();
    private string[] encounterPoolLabels = { "全部" };

    private Vector2 scroll;

    private string[] relicNames = new string[0];
    private string[] enchantNames = new string[0];
    private string[] potionNames = new string[0];
    private string[] suitNames = new string[0];
    private string[] statusNames = new string[0];
    private string[] statusDisplayNames = new string[0];

    private void OnEnable() => BuildNames();
    private void OnFocus() => BuildNames();
    private void OnInspectorUpdate() => Repaint();

    private void BuildNames()
    {
        try
        {
            var cfg = ConfigLoader.Config;
            if (cfg == null) return;
            relicNames = cfg.relics.ConvertAll(r => $"{r.id} {r.name}").ToArray();
            enchantNames = cfg.enchantments.ConvertAll(e => $"{e.id} {e.name}").ToArray();
            potionNames = cfg.potions.ConvertAll(p => $"{p.id} {p.name}").ToArray();
            suitNames = Enum.GetNames(typeof(Suit));
            statusNames = Enum.GetNames(typeof(StatusEffectType));
            statusDisplayNames = Array.ConvertAll(statusNames, n =>
                StatusEffectNames.Name((StatusEffectType)Enum.Parse(typeof(StatusEffectType), n)));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CheatWindow] 读取配置失败: {e.Message}");
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("请先进入 Play 模式。", MessageType.Warning);
            return;
        }

        var director = RunDirector.Instance;
        if (director == null)
        {
            EditorGUILayout.HelpBox("RunDirector.Instance 为空（场景里没有 RunDirector？）。", MessageType.Warning);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawStatus(director);
        DrawNewRun(director);

        var run = director.RunData;
        if (run != null)
        {
            DrawResources(run);
            DrawRelic(run);
            DrawEnchant(run);
            DrawPotion(run);
            DrawBattle(director);
            DrawStatusEffects(director);
            DrawDestiny(director);
            DrawProgress(director);
            DrawEncounter(director);
        }

        EditorGUILayout.EndScrollView();
    }

    // ================= 各区块 =================

    private void DrawStatus(RunDirector director)
    {
        EditorGUILayout.LabelField("运行状态", EditorStyles.boldLabel);
        var run = director.RunData;
        if (run == null)
        {
            EditorGUILayout.LabelField("（尚未开始游戏）");
            return;
        }

        EditorGUILayout.LabelField($"种子: {run.seed}    章节: {run.actId}    战斗序号: {run.battleIndex}");
        EditorGUILayout.LabelField($"生命: {run.CurrentHp}/{run.MaxHp}    金币: {run.Gold}");
        EditorGUILayout.LabelField($"遗物: {run.RelicIds.Count}    药水: {run.PotionIds.Count}    已附魔牌: {run.GetEnchantedCardKeys().Count}");

        var bm = director.BattleManager;
        if (bm != null && bm.GetEnemy() != null)
        {
            var enemy = bm.GetEnemy();
            var player = bm.GetPlayer();
            EditorGUILayout.LabelField($"敌人: {enemy.Name}  HP {enemy.CurrentHp}/{enemy.MaxHp}  防御 {enemy.Defense}");
            EditorGUILayout.LabelField($"玩家防御: {player.Defense}    可操作: {bm.CanPlayerAct}");
        }
    }

    private void DrawNewRun(RunDirector director)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("新游戏", EditorStyles.boldLabel);
        newRunSeed = EditorGUILayout.IntField("种子 (0=随机)", newRunSeed);
        if (GUILayout.Button("开始新游戏"))
            director.StartNewRun(newRunSeed);
    }

    private void DrawResources(RunData run)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("资源", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        goldValue = EditorGUILayout.IntField("金币", goldValue);
        if (GUILayout.Button("增加")) run.Gold += goldValue;
        if (GUILayout.Button("设为")) run.Gold = goldValue;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        hpValue = EditorGUILayout.IntField("生命", hpValue);
        if (GUILayout.Button("治疗")) run.CurrentHp = Mathf.Min(run.MaxHp, run.CurrentHp + hpValue);
        if (GUILayout.Button("受伤")) run.CurrentHp = Mathf.Max(0, run.CurrentHp - hpValue);
        if (GUILayout.Button("回满")) run.CurrentHp = run.MaxHp;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        maxHpValue = EditorGUILayout.IntField("最大生命", maxHpValue);
        if (GUILayout.Button("增加")) run.MaxHp += maxHpValue;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRelic(RunData run)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("遗物", EditorStyles.boldLabel);

        var cfg = ConfigLoader.Config;
        if (cfg == null || relicNames.Length == 0)
        {
            EditorGUILayout.LabelField("（无遗物配置）");
            return;
        }

        relicIndex = Mathf.Clamp(relicIndex, 0, relicNames.Length - 1);
        relicIndex = EditorGUILayout.Popup("遗物", relicIndex, relicNames);
        if (GUILayout.Button("获得"))
        {
            int id = cfg.relics[relicIndex].id;
            if (!run.TryAddRelic(id))
                Debug.LogWarning("[Cheat] 遗物槽已满或已拥有该遗物");
        }
    }

    private void DrawEnchant(RunData run)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("附魔", EditorStyles.boldLabel);

        var cfg = ConfigLoader.Config;
        if (cfg == null || enchantNames.Length == 0)
        {
            EditorGUILayout.LabelField("（无附魔配置）");
            return;
        }

        enchantIndex = Mathf.Clamp(enchantIndex, 0, enchantNames.Length - 1);
        enchantIndex = EditorGUILayout.Popup("附魔", enchantIndex, enchantNames);

        suitIndex = Mathf.Clamp(suitIndex, 0, suitNames.Length - 1);
        suitIndex = EditorGUILayout.Popup("花色", suitIndex, suitNames);
        cardRank = EditorGUILayout.IntSlider("点数 (11=J, 12=Q, 13=K, 14=A)", cardRank, 2, 14);

        if (GUILayout.Button("给该牌附魔"))
        {
            var suit = (Suit)Enum.Parse(typeof(Suit), suitNames[suitIndex]);
            run.AddEnchantment(cardRank, suit, cfg.enchantments[enchantIndex].id);
            Debug.Log($"[Cheat] 给 {cardRank}{suit} 附魔 {cfg.enchantments[enchantIndex].name}");
            RefreshBattleUI();
        }
    }

    /// <summary>让战斗界面立刻刷新手牌（附魔变化后）</summary>
    private void RefreshBattleUI()
    {
        var ui = UIManager.Instance;
        if (ui == null) return;
        ui.GetPanel<BattlePanel>()?.ForceRefreshHand();
    }

    private void DrawPotion(RunData run)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("药水", EditorStyles.boldLabel);

        var cfg = ConfigLoader.Config;
        if (cfg == null || potionNames.Length == 0)
        {
            EditorGUILayout.LabelField("（无药水配置）");
            return;
        }

        potionIndex = Mathf.Clamp(potionIndex, 0, potionNames.Length - 1);
        potionIndex = EditorGUILayout.Popup("药水", potionIndex, potionNames);
        if (GUILayout.Button("获得"))
        {
            if (!run.TryAddPotion(cfg.potions[potionIndex].id))
                Debug.LogWarning("[Cheat] 药水槽已满或已拥有该药水");
        }
    }

    private void DrawBattle(RunDirector director)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("战斗", EditorStyles.boldLabel);

        var bm = director.BattleManager;
        if (bm == null || bm.GetEnemy() == null)
        {
            EditorGUILayout.LabelField("（当前不在战斗中）");
            return;
        }

        var enemy = bm.GetEnemy();
        var player = bm.GetPlayer();

        EditorGUILayout.BeginHorizontal();
        enemyDamage = EditorGUILayout.IntField("对敌伤害", enemyDamage);
        if (GUILayout.Button("造成伤害")) DealDamage(bm, enemy, enemyDamage);
        if (GUILayout.Button("秒杀")) KillEnemy(bm);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        enemySetHp = EditorGUILayout.IntField("敌人血量设为", enemySetHp);
        if (GUILayout.Button("设置")) enemy.CurrentHp = enemySetHp;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        drawCount = EditorGUILayout.IntField("抽牌", drawCount);
        if (GUILayout.Button("抽到手牌")) DrawCards(bm, drawCount);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("玩家回满血")) player.CurrentHp = player.MaxHp;
        if (GUILayout.Button("结束玩家回合")) bm.EndPlayerTurn();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCards(BattleManager bm, int count)
    {
        if (count <= 0) return;
        var drawn = bm.GetDeckPile().Draw(count);
        var overflow = bm.GetHandArea().AddCards(drawn);
        if (overflow != null && overflow.Count > 0) bm.GetDeckPile().ReturnToDeck(overflow);
        Debug.Log($"[Cheat] 抽了 {drawn.Count} 张牌");
    }

    private void DrawStatusEffects(RunDirector director)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("状态效果", EditorStyles.boldLabel);

        var bm = director.BattleManager;
        if (bm == null || statusNames.Length == 0)
        {
            EditorGUILayout.LabelField("（不在战斗中）");
            return;
        }

        statusIndex = Mathf.Clamp(statusIndex, 0, statusNames.Length - 1);
        statusIndex = EditorGUILayout.Popup("状态", statusIndex,
            statusDisplayNames.Length == statusNames.Length ? statusDisplayNames : statusNames);
        statusAmount = EditorGUILayout.IntField("层数", statusAmount);
        statusDuration = EditorGUILayout.IntField("持续回合 (-1=永久)", statusDuration);
        statusTarget = EditorGUILayout.Popup("目标", statusTarget, new[] { "玩家", "敌人" });

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("施加"))
        {
            var type = (StatusEffectType)Enum.Parse(typeof(StatusEffectType), statusNames[statusIndex]);
            GetTarget(bm).AddStatus(type, statusAmount, statusDuration);
        }
        if (GUILayout.Button("清除目标所有状态"))
        {
            GetTarget(bm).StatusEffects.Clear();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDestiny(RunDirector director)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("命格", EditorStyles.boldLabel);

        var run = director.RunData;
        if (run == null) return;

        string mainName = run.HasMainDestiny ? DestinyInfo.SuitName((Suit)run.mainDestinySuit) : "未确立";
        EditorGUILayout.LabelField($"主命格: {mainName}    命运之力: {run.fatePower}/{RunData.FatePowerMax}");
        EditorGUILayout.LabelField($"命格值: ♠{run.destinyPoints[0]}  ♥{run.destinyPoints[1]}  ♣{run.destinyPoints[2]}  ♦{run.destinyPoints[3]}");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("设主命格", GUILayout.Width(70));
        for (int i = 0; i < 4; i++)
        {
            int suit = i;
            if (GUILayout.Button(DestinyInfo.SuitSymbol((Suit)suit), GUILayout.Width(40)))
            {
                run.mainDestinySuit = suit;
                run.NotifyDestinyChanged();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        destinyPointDelta = EditorGUILayout.IntField("命格值 +/-", destinyPointDelta);
        if (GUILayout.Button("♠", GUILayout.Width(36))) AddDestiny(run, Suit.Spade, destinyPointDelta);
        if (GUILayout.Button("♥", GUILayout.Width(36))) AddDestiny(run, Suit.Heart, destinyPointDelta);
        if (GUILayout.Button("♣", GUILayout.Width(36))) AddDestiny(run, Suit.Club, destinyPointDelta);
        if (GUILayout.Button("♦", GUILayout.Width(36))) AddDestiny(run, Suit.Diamond, destinyPointDelta);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("命运之力攒满")) { run.fatePower = RunData.FatePowerMax; run.NotifyDestinyChanged(); }
        if (GUILayout.Button("清空命运之力")) { run.fatePower = 0; run.NotifyDestinyChanged(); }
        EditorGUILayout.EndHorizontal();
    }

    private void AddDestiny(RunData run, Suit suit, int delta)
    {
        int idx = (int)suit;
        run.destinyPoints[idx] = Mathf.Max(0, run.destinyPoints[idx] + delta);
        run.NotifyDestinyChanged();
    }

    private void DrawProgress(RunDirector director)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("进度", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("进入下一场")) director.EnterNextBattle();
        if (GUILayout.Button("跳到 Boss")) CallPrivate(director, "EnterBossBattle");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("打开商店"))
        {
            // 作弊打开商店：关店后把战斗面板恢复回来（OpenShop 会隐藏战斗面板，没回调的话关店后就一片空白）
            bool ok = director.OpenShop(() =>
            {
                if (director.BattleManager != null && !director.BattleManager.IsBattleOver && UIManager.Instance != null)
                    UIManager.Instance.ShowPanel<BattlePanel>();
            });
            if (!ok) Debug.LogWarning("[Cheat] 打开商店失败");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("跳到事件（跳过当前战斗）", EditorStyles.boldLabel);

        // 搜索 + 主题筛选
        EditorGUILayout.BeginHorizontal();
        eventSearch = EditorGUILayout.TextField(eventSearch, EditorStyles.toolbarSearchField, GUILayout.Width(170));
        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22)))
            eventSearch = "";
        eventThemeFilter = EditorGUILayout.Popup(eventThemeFilter, EventThemeLabels, GUILayout.Width(80));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        var cfg = ConfigLoader.Config;

        // 过滤（存的是 cfg.events 的下标）
        filteredEventIndices.Clear();
        if (cfg != null)
        {
            string q = string.IsNullOrEmpty(eventSearch) ? null : eventSearch.Trim().ToLowerInvariant();
            string themeWant = eventThemeFilter == 0 ? null : ((Suit)(eventThemeFilter - 1)).ToString();

            for (int i = 0; i < cfg.events.Count; i++)
            {
                var ev = cfg.events[i];
                if (themeWant != null && ev.theme != themeWant) continue;
                if (q != null && !($"{ev.id} {ev.title} {ev.theme}".ToLowerInvariant().Contains(q))) continue;
                filteredEventIndices.Add(i);
            }
        }

        // 滚动列表
        eventScroll = EditorGUILayout.BeginScrollView(eventScroll, GUILayout.Height(190));
        if (cfg != null)
        {
            foreach (int i in filteredEventIndices)
            {
                var ev = cfg.events[i];
                string text = $"{ev.id}  {ev.title}    <color=#8A8A8A>[{ThemeName(ev.theme)}] {ev.options.Count} 选项</color>";

                var prevBg = GUI.backgroundColor;
                if (i == eventJumpIndex) GUI.backgroundColor = new Color(0.30f, 0.55f, 1f);
                if (GUILayout.Button(text, EventRowStyle)) eventJumpIndex = i;
                GUI.backgroundColor = prevBg;
            }
        }
        if (filteredEventIndices.Count == 0)
            EditorGUILayout.LabelField("（没有匹配的事件）", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("通关并进入", GUILayout.Width(100)))
        {
            if (cfg != null && eventJumpIndex >= 0 && eventJumpIndex < cfg.events.Count)
                director.DebugEnterEvent(cfg.events[eventJumpIndex].id);
        }
        EditorGUILayout.LabelField(
            cfg != null && eventJumpIndex >= 0 && eventJumpIndex < cfg.events.Count
                ? $"已选：{cfg.events[eventJumpIndex].id} {cfg.events[eventJumpIndex].title}"
                : "未选择");
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>直接跳到下一场 / 指定敌人组开战</summary>
    private void DrawEncounter(RunDirector director)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("敌人组（直接开战）", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("跳到下一场", GUILayout.Width(100)))
            director.DebugEnterNextBattle();
        if (GUILayout.Button("跳到 Boss", GUILayout.Width(80)))
            CallPrivate(director, "EnterBossBattle");
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        var cfg = ConfigLoader.Config;
        if (cfg == null || cfg.encounters.Count == 0)
        {
            EditorGUILayout.LabelField("（没有配置敌人组 encounters.json）", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        // 池筛选下拉（从配置里收集所有 pool）
        BuildEncounterPoolLabels(cfg);

        EditorGUILayout.BeginHorizontal();
        encounterSearch = EditorGUILayout.TextField(encounterSearch, EditorStyles.toolbarSearchField, GUILayout.Width(170));
        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22)))
            encounterSearch = "";
        encounterPoolFilter = EditorGUILayout.Popup(encounterPoolFilter, encounterPoolLabels, GUILayout.Width(130));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 过滤（存的是 cfg.encounters 的下标）
        filteredEncounterIndices.Clear();
        string q = string.IsNullOrEmpty(encounterSearch) ? null : encounterSearch.Trim().ToLowerInvariant();
        string poolWant = encounterPoolFilter == 0 ? null : encounterPoolLabels[encounterPoolFilter];

        for (int i = 0; i < cfg.encounters.Count; i++)
        {
            var enc = cfg.encounters[i];
            if (poolWant != null && enc.pool != poolWant) continue;
            if (q != null && !($"{enc.id} {enc.name} {enc.pool} {EncounterEnemyNames(enc)}".ToLowerInvariant().Contains(q))) continue;
            filteredEncounterIndices.Add(i);
        }

        // 滚动列表
        encounterScroll = EditorGUILayout.BeginScrollView(encounterScroll, GUILayout.Height(190));
        foreach (int i in filteredEncounterIndices)
        {
            var enc = cfg.encounters[i];
            string text = $"{enc.id}  {enc.name}    <color=#8A8A8A>[{enc.pool}] 权重{enc.weight} · {EncounterEnemyNames(enc)}</color>";

            var prevBg = GUI.backgroundColor;
            if (i == encounterIndex) GUI.backgroundColor = new Color(0.30f, 0.55f, 1f);
            if (GUILayout.Button(text, EventRowStyle)) encounterIndex = i;
            GUI.backgroundColor = prevBg;
        }
        if (filteredEncounterIndices.Count == 0)
            EditorGUILayout.LabelField("（没有匹配的敌人组）", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("用该组合开战", GUILayout.Width(110)))
        {
            if (encounterIndex >= 0 && encounterIndex < cfg.encounters.Count)
                director.DebugStartEncounter(cfg.encounters[encounterIndex].id);
        }
        EditorGUILayout.LabelField(
            encounterIndex >= 0 && encounterIndex < cfg.encounters.Count
                ? $"已选：{cfg.encounters[encounterIndex].id} {cfg.encounters[encounterIndex].name}"
                : "未选择");
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>收集所有敌人池名，做成筛选下拉（第一个是「全部」）</summary>
    private void BuildEncounterPoolLabels(Roguelike.Data.AllConfig cfg)
    {
        var pools = new System.Collections.Generic.List<string> { "全部" };
        foreach (var enc in cfg.encounters)
        {
            if (string.IsNullOrEmpty(enc.pool)) continue;
            if (!pools.Contains(enc.pool)) pools.Add(enc.pool);
        }
        if (pools.Count != encounterPoolLabels.Length)
            encounterPoolLabels = pools.ToArray();
    }

    /// <summary>把敌人组里的敌人 id 换成名字（找不到就显示 #id）</summary>
    private static string EncounterEnemyNames(Roguelike.Data.EncounterData enc)
    {
        if (enc == null || enc.enemies == null || enc.enemies.Count == 0) return "（空）";

        var names = new System.Collections.Generic.List<string>();
        foreach (int id in enc.enemies)
        {
            var e = ConfigLoader.GetEnemy(id);
            names.Add(e != null ? e.name : $"#{id}");
        }
        return string.Join("、", names);
    }

    private static readonly string[] EventThemeLabels = { "全部", "黑桃", "红桃", "梅花", "方块" };    private static string ThemeName(string theme)
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

    private GUIStyle eventRowStyle;
    private GUIStyle EventRowStyle
    {
        get
        {
            if (eventRowStyle == null)
            {
                eventRowStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleLeft,
                    richText = true,
                    padding = new RectOffset(8, 8, 3, 3)
                };
            }
            return eventRowStyle;
        }
    }

    // ================= 工具 =================

    private BattleUnit GetTarget(BattleManager bm)
    {
        return statusTarget == 0 ? bm.GetPlayer() : bm.GetEnemy();
    }

    private void DealDamage(BattleManager bm, BattleUnit target, int amount)
    {
        target.TakeDamage(amount, bm.GetPlayer());
        CallPrivate(bm, "CheckBattleEnd");
    }

    private void KillEnemy(BattleManager bm)
    {
        bm.GetEnemy().CurrentHp = 0;
        CallPrivate(bm, "CheckBattleEnd");
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
}
