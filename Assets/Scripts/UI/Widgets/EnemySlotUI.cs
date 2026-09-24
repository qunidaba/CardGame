using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 单个敌人的战斗显示槽位（无背景）：
///   名字 / 意图  → 图片正上方
///   图片        → 点击选中（无图时用空白占位图）
///   血条 / 护盾 / 状态栏 → 图片下方
/// 完全运行时构建，不依赖预制体。
/// </summary>
public class EnemySlotUI : MonoBehaviour, IPointerClickHandler
{
    private static readonly Color NameSelected = new Color(1f, 0.87f, 0.42f);
    private static readonly Color BlankArtColor = new Color(0.30f, 0.31f, 0.38f, 0.55f);

    public BattleUnit Unit { get; private set; }
    public int Index { get; private set; }
    public EnemyData Data { get; private set; }

    /// <summary>点击本槽位（参数为下标）</summary>
    public Action<int> OnClicked;

    private Image selectionBox;
    private Sprite[] selectionFrames;      // Resources/Battle/UIS 的两帧（UIS_0 / UIS_1）
    private float selectionAnimTime;
    private Image artwork;
    private Image hpFill;
    private Image shieldIcon;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI shieldText;
    private TextMeshProUGUI intentText;
    private StatusEffectBar statusBar;
    private CanvasGroup canvasGroup;

    private Sprite[] frames;            // 待机（循环）
    private Sprite[] burrowFrames;      // 遁地（一次，停在最后一帧）
    private Sprite[] emergeFrames;      // 出来（一次，播完回待机）
    private bool wasBurrowed;           // 上一帧的遁地状态（用于检测进出遁地）
    private int frameIndex;
    private Coroutine animRoutine;
    private float frameRate = 12f;
    private bool selected;
    private Vector2 artDefaultSize = new Vector2(120f, 120f);
    private Vector2 artBasePos = new Vector2(0f, 0f);   // 立绘居中于槽位锚点：enemyAnchor 的 y 就是立绘的 y

    private const float SelectionFrameRate = 8f;        // 选择框逐帧速度
    private const float SelectionPadding = 40f;         // 选择框比立绘大多少

    // ===== 构建 =====

    public void Build(TMP_FontAsset font, Sprite shieldSprite, StatusEffectBar statusBarTemplate,
                      float width, float height, Vector2 artDefaultSize, Sprite[] selectionFrames = null)
    {
        this.artDefaultSize = artDefaultSize;
        this.selectionFrames = selectionFrames;

        var rt = gameObject.GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;

        // ---- 名字（图片正上方）----
        nameText = MakeText("Name", font, 21, TextAlignmentOptions.Center, new Vector2(width - 16f, 30f));
        Anchor(nameText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f));

        // ---- 意图（名字下方、图片上方）----
        intentText = MakeText("Intent", font, 18, TextAlignmentOptions.Center, new Vector2(width - 12f, 52f));
        Anchor(intentText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 103f));
        intentText.enableWordWrapping = true;

        // ---- 立绘（点击选中）----
        var artGo = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
        artGo.transform.SetParent(transform, false);
        var artRt = artGo.GetComponent<RectTransform>();
        artRt.anchorMin = new Vector2(0.5f, 0.5f);
        artRt.anchorMax = new Vector2(0.5f, 0.5f);
        artRt.pivot = new Vector2(0.5f, 0.5f);
        artRt.anchoredPosition = artBasePos;
        artRt.sizeDelta = artDefaultSize;
        artwork = artGo.GetComponent<Image>();
        artwork.sprite = GetRuntimeWhiteSprite();   // 无图时用空白占位图
        artwork.color = BlankArtColor;
        artwork.preserveAspect = true;
        artwork.raycastTarget = true;               // 点击区域 = 图片本身

        // ---- 选中提示框（Resources/Battle/UIS 的两帧动画，画在立绘前面）----
        var ringGo = new GameObject("SelectionBox", typeof(RectTransform), typeof(Image));
        ringGo.transform.SetParent(transform, false);
        var ringRt = ringGo.GetComponent<RectTransform>();
        ringRt.anchorMin = new Vector2(0.5f, 0.5f);
        ringRt.anchorMax = new Vector2(0.5f, 0.5f);
        ringRt.pivot = new Vector2(0.5f, 0.5f);
        ringRt.anchoredPosition = artBasePos;
        float boxSize = Mathf.Max(artDefaultSize.x, artDefaultSize.y) + SelectionPadding;
        ringRt.sizeDelta = new Vector2(boxSize, boxSize);
        selectionBox = ringGo.GetComponent<Image>();
        selectionBox.sprite = (selectionFrames != null && selectionFrames.Length > 0)
            ? selectionFrames[0] : GetRuntimeWhiteSprite();
        selectionBox.color = Color.white;
        selectionBox.raycastTarget = false;         // 不挡点击
        selectionBox.preserveAspect = true;
        selectionBox.enabled = false;

        // ---- 血条 + 压在血条上的血量数字 ----
        float barWidth = Mathf.Max(90f, width - 96f);
        float barY = -80f;

        var fillGo = new GameObject("HpFill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0.5f, 0.5f);
        fillRt.anchorMax = new Vector2(0.5f, 0.5f);
        fillRt.pivot = new Vector2(0.5f, 0.5f);
        fillRt.anchoredPosition = new Vector2(0f, barY);
        fillRt.sizeDelta = new Vector2(barWidth, 18f);
        hpFill = fillGo.GetComponent<Image>();
        hpFill.sprite = GetRuntimeWhiteSprite();
        hpFill.color = new Color(0.90f, 0.40f, 0.25f);
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpFill.raycastTarget = false;

        hpText = MakeText("Hp", font, 16, TextAlignmentOptions.Center, new Vector2(barWidth, 18f));
        Anchor(hpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, barY));
        hpText.fontStyle = FontStyles.Bold;

        // ---- 护盾（图标压在血条左端，数值压在图标上，和玩家那边一致）----
        const float shieldSize = 44f;
        float shieldX = -barWidth * 0.5f - shieldSize * 0.5f + 10f;   // 再往右一点，和血条轻微重叠

        var shGo = new GameObject("ShieldIcon", typeof(RectTransform), typeof(Image));
        shGo.transform.SetParent(transform, false);
        var shRt = shGo.GetComponent<RectTransform>();
        shRt.anchorMin = new Vector2(0.5f, 0.5f);
        shRt.anchorMax = new Vector2(0.5f, 0.5f);
        shRt.pivot = new Vector2(0.5f, 0.5f);
        shRt.anchoredPosition = new Vector2(shieldX, barY);
        shRt.sizeDelta = new Vector2(shieldSize, shieldSize);
        shieldIcon = shGo.GetComponent<Image>();
        if (shieldSprite != null) shieldIcon.sprite = shieldSprite;
        shieldIcon.raycastTarget = false;
        shieldIcon.enabled = false;

        shieldText = MakeText("Shield", font, 20, TextAlignmentOptions.Center, new Vector2(shieldSize, shieldSize));
        Anchor(shieldText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(shieldX, barY));
        shieldText.color = new Color(0, 0, 1f);   // 蓝色数字
        shieldText.enabled = false;

        // ---- 状态栏（血条下方）----
        var barGo = new GameObject("StatusBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        barGo.transform.SetParent(transform, false);
        var barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.5f, 0.5f);
        barRt.anchorMax = new Vector2(0.5f, 0.5f);
        barRt.pivot = new Vector2(0.5f, 0.5f);
        barRt.anchoredPosition = new Vector2(0f, -118f);
        barRt.sizeDelta = new Vector2(width - 16f, 38f);
        var hlg = barGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        statusBar = barGo.AddComponent<StatusEffectBar>();
        if (statusBarTemplate != null)
        {
            statusBar.iconPrefab = statusBarTemplate.iconPrefab;
            statusBar.iconSize = statusBarTemplate.iconSize;
            statusBar.sprites = new List<StatusEffectSprite>(statusBarTemplate.sprites);
        }
        statusBar.container = barGo.transform;
    }

    // ===== 数据 =====

    public void SetUnit(int index, BattleUnit unit, EnemyData data, Sprite[] frames, float defaultFrameRate)
    {
        Index = index;
        Unit = unit;
        Data = data;
        this.frames = frames;
        frameRate = (data != null && data.frameRate > 0f) ? data.frameRate : defaultFrameRate;

        if (nameText != null) nameText.text = unit != null ? unit.Name : "";
        ApplyArtworkLayout();
        RefreshAll();
        StartAnim();
    }

    /// <summary>带遁地/出来动画的版本</summary>
    public void SetUnit(int index, BattleUnit unit, EnemyData data, Sprite[] frames,
                        Sprite[] burrowFrames, Sprite[] emergeFrames, float defaultFrameRate)
    {
        this.burrowFrames = burrowFrames;
        this.emergeFrames = emergeFrames;
        SetUnit(index, unit, data, frames, defaultFrameRate);
    }

    /// <summary>按配置的宽/高/缩放/偏移调整立绘与高亮环</summary>
    private void ApplyArtworkLayout()
    {
        if (artwork == null) return;

        float scale = (Data != null && Data.imageScale > 0f) ? Data.imageScale : 1f;
        float w = ((Data != null && Data.imageWidth > 0f) ? Data.imageWidth : artDefaultSize.x) * scale;
        float h = ((Data != null && Data.imageHeight > 0f) ? Data.imageHeight : artDefaultSize.y) * scale;

        float ox = Data != null ? Data.imageOffsetX : 0f;
        float oy = Data != null ? Data.imageOffsetY : 0f;
        Vector2 pos = artBasePos + new Vector2(ox, oy);

        var artRt = artwork.rectTransform;
        artRt.sizeDelta = new Vector2(w, h);
        artRt.anchoredPosition = pos;

        if (selectionBox != null)
        {
            // 方形框：按立绘的长边 + 留白，保证框住敌人
            float boxSize = Mathf.Max(w, h) + SelectionPadding;
            selectionBox.rectTransform.sizeDelta = new Vector2(boxSize, boxSize);
            selectionBox.rectTransform.anchoredPosition = pos;
        }
    }

    public void RefreshAll()
    {
        RefreshHp();
        RefreshStatus();
        SyncBurrowVisual();
    }

    public void RefreshHp()
    {
        if (Unit == null) return;

        // 注意：这里不做任何"阵亡"表现。
        // 血量归零是逻辑层立刻发生的，但攻击特效还在飞，
        // 阵亡表现统一由 BattlePanel 在投射物命中后处理（直接删掉整个槽位）。
        if (hpText != null) hpText.text = $"{Unit.CurrentHp}/{Unit.MaxHp}";
        if (hpFill != null) hpFill.fillAmount = Unit.MaxHp > 0 ? Mathf.Clamp01((float)Unit.CurrentHp / Unit.MaxHp) : 0f;
        RefreshShield();
        ApplySelectedVisual();
    }

    public void RefreshShield()
    {
        int def = Unit != null ? Unit.Defense : 0;
        bool show = def > 0;

        if (shieldIcon != null) shieldIcon.enabled = show && shieldIcon.sprite != null;
        if (shieldText != null)
        {
            shieldText.enabled = show;
            if (show) shieldText.text = def.ToString();
        }
    }

    public void RefreshStatus()
    {
        if (statusBar != null && Unit != null) statusBar.Refresh(Unit);
    }

    public void SetIntent(string text)
    {
        if (intentText != null) intentText.text = text ?? "";
    }

    /// <summary>选中高亮：立绘背后播放两帧选择框动画 + 名字变金</summary>
    public void SetSelected(bool value)
    {
        selected = value;
        ApplySelectedVisual();
    }

    private void ApplySelectedVisual()
    {
        // 阵亡不做特殊处理：命中后整个槽位会被删除
        bool on = selected;

        if (selectionBox != null)
        {
            selectionBox.enabled = on;
            if (on)
            {
                selectionAnimTime = 0f;
                selectionBox.rectTransform.localScale = Vector3.one;
                if (selectionFrames != null && selectionFrames.Length > 0)
                    selectionBox.sprite = selectionFrames[0];
            }
            else
            {
                selectionBox.rectTransform.localScale = Vector3.one;
            }
        }
        if (nameText != null) nameText.color = on ? NameSelected : Color.white;
    }

    /// <summary>选中框的两帧动画 + 轻微跳动</summary>
    private void Update()
    {
        if (selectionBox == null || !selectionBox.enabled) return;

        selectionAnimTime += Time.unscaledDeltaTime;

        if (selectionFrames != null && selectionFrames.Length > 1)
        {
            int idx = Mathf.FloorToInt(selectionAnimTime * SelectionFrameRate) % selectionFrames.Length;
            if (selectionBox.sprite != selectionFrames[idx]) selectionBox.sprite = selectionFrames[idx];
        }

        float pulse = 1f + Mathf.Sin(selectionAnimTime * 7f) * 0.06f;
        selectionBox.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
    }

    // ===== 锚点（飘字/投射物目标）=====

    public Vector3 ArtworkWorld => artwork != null ? artwork.transform.position : transform.position;
    public Vector3 ShieldWorld => shieldIcon != null ? shieldIcon.transform.position : transform.position;
    public Vector3 IntentWorld => intentText != null ? intentText.transform.position : transform.position;

    // ===== 序列帧动画（待机循环 / 遁地一次 / 出来一次）=====

    private void StartAnim()
    {
        StopAnim();
        if (artwork == null) return;

        bool burrowed = Unit != null && Unit.GetStatusAmount(StatusEffectType.Burrow) > 0;
        wasBurrowed = burrowed;

        // 已在遁地状态（例如战斗中重建槽位）：直接停在遁地动画最后一帧
        if (burrowed && burrowFrames != null && burrowFrames.Length > 0)
        {
            artwork.color = Color.white;
            artwork.sprite = burrowFrames[burrowFrames.Length - 1];
            return;
        }

        StartLoop(frames);
    }

    /// <summary>
    /// 检测敌人进出遁地状态并切换动画。
    /// 由 BattlePanel 在「投射物命中后」调用（不要每帧轮询，否则特效还没打到就播出来动画）。
    /// </summary>
    public void SyncBurrowVisual()
    {
        if (Unit == null) return;

        bool burrowed = Unit.GetStatusAmount(StatusEffectType.Burrow) > 0;
        if (burrowed == wasBurrowed) return;

        wasBurrowed = burrowed;
        if (burrowed) PlayBurrow();
        else PlayEmerge();
    }

    private void PlayBurrow()
    {
        // 没配遁地动画：保持当前帧不动
        if (burrowFrames == null || burrowFrames.Length == 0) return;
        StartOneShot(burrowFrames, holdLast: true, thenLoop: null);
    }

    private void PlayEmerge()
    {
        if (emergeFrames == null || emergeFrames.Length == 0)
        {
            StartLoop(frames);
            return;
        }
        StartOneShot(emergeFrames, holdLast: false, thenLoop: frames);
    }

    private void StartLoop(Sprite[] set)
    {
        StopAnim();
        if (artwork == null) return;

        if (set == null || set.Length == 0)
        {
            // 没有图片：用空白占位图（仍可点击选中）
            artwork.sprite = GetRuntimeWhiteSprite();
            artwork.color = BlankArtColor;
            return;
        }

        artwork.color = Color.white;
        frameIndex = 0;
        artwork.sprite = set[0];

        if (set.Length > 1 && isActiveAndEnabled)
            animRoutine = StartCoroutine(LoopRoutine(set));
    }

    private void StartOneShot(Sprite[] set, bool holdLast, Sprite[] thenLoop)
    {
        StopAnim();
        if (artwork == null) return;

        if (set == null || set.Length == 0)
        {
            if (thenLoop != null) StartLoop(thenLoop);
            return;
        }

        artwork.color = Color.white;
        frameIndex = 0;
        artwork.sprite = set[0];

        if (isActiveAndEnabled)
            animRoutine = StartCoroutine(OneShotRoutine(set, holdLast, thenLoop));
    }

    private void StopAnim()
    {
        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
            animRoutine = null;
        }
    }

    private IEnumerator LoopRoutine(Sprite[] set)
    {
        var wait = new WaitForSeconds(1f / Mathf.Max(1f, frameRate));
        int i = 0;
        while (true)
        {
            yield return wait;
            if (artwork == null || set == null || set.Length == 0) yield break;
            i = (i + 1) % set.Length;
            artwork.sprite = set[i];
        }
    }

    private IEnumerator OneShotRoutine(Sprite[] set, bool holdLast, Sprite[] thenLoop)
    {
        var wait = new WaitForSeconds(1f / Mathf.Max(1f, frameRate));
        for (int i = 1; i < set.Length; i++)
        {
            yield return wait;
            if (artwork == null) yield break;
            artwork.sprite = set[i];
        }

        animRoutine = null;
        if (holdLast) yield break;          // 停在最后一帧（遁地中）
        StartLoop(thenLoop);                // 出来播完 → 回待机
    }

    private void OnDisable() => StopAnim();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Unit == null || Unit.IsDead) return;
        OnClicked?.Invoke(Index);
    }

    // ===== 工具 =====

    private TextMeshProUGUI MakeText(string name, TMP_FontAsset font, float size, TextAlignmentOptions align, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = sizeDelta;
        return t;
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
    }

    private static Sprite runtimeWhite;
    private static Sprite GetRuntimeWhiteSprite()
    {
        if (runtimeWhite == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            runtimeWhite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            runtimeWhite.name = "SlotWhite";
        }
        return runtimeWhite;
    }
}
