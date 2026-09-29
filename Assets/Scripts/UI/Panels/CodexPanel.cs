using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 图鉴：展示附魔 / 遗物 / 药水。只有在游戏里「发现过」的才显示，否则显示 ？？？。
/// 支持稀有度筛选；诅咒排在最下面。纯代码构建，无需预制体。
/// </summary>
public class CodexPanel : BasePanel
{
    private enum Section { Enchantment, Relic, Potion }

    private bool built;
    private Section section = Section.Enchantment;
    private string rarityFilter = "";   // "" = 全部

    private TMP_FontAsset font;
    private Transform content;
    private ScrollRect scroll;
    private TextMeshProUGUI countLabel;

    private readonly Dictionary<Section, Button> tabButtons = new Dictionary<Section, Button>();
    private readonly Dictionary<string, Button> filterButtons = new Dictionary<string, Button>();

    // 筛选项：显示名 → rarity 关键字
    private static readonly (string label, string rarity)[] Filters =
    {
        ("全部", ""), ("普通", "Common"), ("稀有", "Rare"),
        ("史诗", "Epic"), ("诅咒", "Curse"), ("事件", "Event"),
    };

    public override void Init()
    {
        base.Init();
        EnsureBuilt();
        Refresh();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        var overlay = gameObject.GetComponent<Image>();
        if (overlay == null) overlay = gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.85f);
        overlay.raycastTarget = true;

        var rootRt = gameObject.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
        }

        var card = new GameObject("CodexCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1100f, 880f);
        crt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        var title = CreateText(card.transform, "Title", font, 44, TextAlignmentOptions.Center);
        title.text = "图鉴";
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30));
        title.rectTransform.sizeDelta = new Vector2(600, 60);

        // 分类标签
        var tabRow = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabRow.transform.SetParent(card.transform, false);
        var trt = tabRow.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(760f, 60f);
        trt.anchoredPosition = new Vector2(0, -110);
        var hlg = tabRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        AddTab(tabRow.transform, Section.Enchantment, "附魔");
        AddTab(tabRow.transform, Section.Relic, "遗物");
        AddTab(tabRow.transform, Section.Potion, "药水");

        // 稀有度筛选
        var filterRow = new GameObject("Filters", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        filterRow.transform.SetParent(card.transform, false);
        var frt = filterRow.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.5f, 1f);
        frt.anchorMax = new Vector2(0.5f, 1f);
        frt.pivot = new Vector2(0.5f, 1f);
        frt.sizeDelta = new Vector2(980f, 46f);
        frt.anchoredPosition = new Vector2(0, -180);
        var fhlg = filterRow.GetComponent<HorizontalLayoutGroup>();
        fhlg.spacing = 10;
        fhlg.childAlignment = TextAnchor.MiddleCenter;
        fhlg.childControlWidth = true;
        fhlg.childControlHeight = true;
        fhlg.childForceExpandWidth = true;
        fhlg.childForceExpandHeight = true;

        foreach (var f in Filters)
            AddFilter(filterRow.transform, f.label, f.rarity);

        // 已发现计数
        countLabel = CreateText(card.transform, "Count", font, 24, TextAlignmentOptions.Center);
        countLabel.color = new Color(0.7f, 0.72f, 0.78f);
        Anchor(countLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -240));
        countLabel.rectTransform.sizeDelta = new Vector2(900, 40);

        // 滚动列表
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(980f, 500f);
        srt.anchoredPosition = new Vector2(0f, -290f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = new Vector2(-20f, 0f);   // 右侧留出滚动槽
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.004f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport.transform, false);
        content = contentGo.transform;
        var ccr = contentGo.GetComponent<RectTransform>();
        ccr.anchorMin = new Vector2(0f, 1f);
        ccr.anchorMax = new Vector2(1f, 1f);
        ccr.pivot = new Vector2(0.5f, 1f);
        ccr.offsetMin = Vector2.zero;
        ccr.offsetMax = Vector2.zero;
        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = vrt;
        scroll.content = ccr;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        // 右侧滚动槽
        var scrollbar = BuildScrollbar(scrollGo.transform);
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // 返回
        var btnGo = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(card.transform, false);
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0, 24);
        brt.sizeDelta = new Vector2(220, 56);
        var bimg = btnGo.GetComponent<Image>();
        bimg.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = bimg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            if (UIManager.Instance != null) UIManager.Instance.Hide<CodexPanel>();
            else Hide();
        });
        var blabel = CreateText(btnGo.transform, "Label", font, 26, TextAlignmentOptions.Center);
        blabel.rectTransform.anchorMin = Vector2.zero;
        blabel.rectTransform.anchorMax = Vector2.one;
        blabel.rectTransform.offsetMin = Vector2.zero;
        blabel.rectTransform.offsetMax = Vector2.zero;
        blabel.text = "返回";
    }

    private Scrollbar BuildScrollbar(Transform parent)
    {
        var sbGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        sbGo.transform.SetParent(parent, false);
        sbGo.transform.SetAsLastSibling();
        var sbrt = sbGo.GetComponent<RectTransform>();
        sbrt.anchorMin = new Vector2(1f, 0f);
        sbrt.anchorMax = new Vector2(1f, 1f);
        sbrt.pivot = new Vector2(1f, 0.5f);
        sbrt.sizeDelta = new Vector2(16f, -12f);
        sbrt.anchoredPosition = new Vector2(-2f, 0f);
        var sbBg = sbGo.GetComponent<Image>();
        sbBg.color = new Color(0.08f, 0.08f, 0.11f, 1f);

        var sliding = new GameObject("Sliding Area", typeof(RectTransform));
        sliding.transform.SetParent(sbGo.transform, false);
        var slRt = sliding.GetComponent<RectTransform>();
        slRt.anchorMin = Vector2.zero;
        slRt.anchorMax = Vector2.one;
        slRt.offsetMin = Vector2.zero;
        slRt.offsetMax = Vector2.zero;

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(sliding.transform, false);
        var hRt = handle.GetComponent<RectTransform>();
        hRt.anchorMin = Vector2.zero;
        hRt.anchorMax = Vector2.one;
        hRt.offsetMin = Vector2.zero;
        hRt.offsetMax = Vector2.zero;
        var hImg = handle.GetComponent<Image>();
        hImg.color = new Color(0.42f, 0.46f, 0.58f, 1f);

        var scrollbar = sbGo.GetComponent<Scrollbar>();
        scrollbar.handleRect = hRt;
        scrollbar.targetGraphic = hImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    private void AddTab(Transform parent, Section sec, string text)
    {
        var go = new GameObject("Tab_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.34f, 0.38f, 0.5f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => { section = sec; Refresh(); });

        var label = CreateText(go.transform, "Label", font, 28, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.text = text;

        tabButtons[sec] = btn;
    }

    private void AddFilter(Transform parent, string label, string rarity)
    {
        var go = new GameObject("Filter_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.34f, 0.38f, 0.5f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => { rarityFilter = rarity; Refresh(); });

        var t = CreateText(go.transform, "Label", font, 22, TextAlignmentOptions.Center);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = Vector2.zero;
        t.rectTransform.offsetMax = Vector2.zero;
        t.text = label;

        filterButtons[rarity] = btn;
    }

    private bool PassesFilter(string rarity)
    {
        if (string.IsNullOrEmpty(rarityFilter)) return true;
        return rarity == rarityFilter;
    }

    /// <summary>稀有度排序：普通 &lt; 稀有 &lt; 史诗 &lt; 事件 &lt; 诅咒（诅咒永远最后）</summary>
    private static int RarityOrder(string rarity)
    {
        switch (rarity)
        {
            case "Common": return 1;
            case "Rare": return 2;
            case "Epic": return 3;
            case "Event": return 4;
            case "Curse": return 5;
            default: return 0;
        }
    }

    private void Refresh()
    {
        if (content == null) return;

        // 分类高亮
        foreach (var kv in tabButtons)
            kv.Value.GetComponent<Image>().color = kv.Key == section
                ? new Color(0.30f, 0.42f, 0.30f, 1f)
                : new Color(0.22f, 0.24f, 0.32f, 1f);

        // 药水没有诅咒：在药水分类下隐藏「诅咒」筛选，若当前正选中则回到「全部」
        if (section == Section.Potion && rarityFilter == "Curse") rarityFilter = "";

        // 筛选高亮 / 显隐
        foreach (var kv in filterButtons)
        {
            bool visible = !(section == Section.Potion && kv.Key == "Curse");
            kv.Value.gameObject.SetActive(visible);
            kv.Value.GetComponent<Image>().color = kv.Key == rarityFilter
                ? new Color(0.30f, 0.42f, 0.30f, 1f)
                : new Color(0.22f, 0.24f, 0.32f, 1f);
        }

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        var cfg = ConfigLoader.Config;
        if (cfg == null)
        {
            countLabel.text = "配置未加载";
            return;
        }

        int known = 0, total = 0;

        switch (section)
        {
            case Section.Enchantment:
            {
                var list = new List<EnchantmentData>(cfg.enchantments);
                list.Sort((a, b) =>
                {
                    int t = RarityOrder(a.rarity).CompareTo(RarityOrder(b.rarity));
                    return t != 0 ? t : a.id.CompareTo(b.id);
                });
                foreach (var e in list)
                {
                    if (!PassesFilter(e.rarity)) continue;
                    total++;
                    bool k = CodexData.IsEnchantmentKnown(e.id);
                    if (k) known++;
                    AddRow(k, e.name, e.description, e.rarity);
                }
                break;
            }
            case Section.Relic:
            {
                var list = new List<RelicData>(cfg.relics);
                list.Sort((a, b) =>
                {
                    int t = RarityOrder(a.rarity).CompareTo(RarityOrder(b.rarity));
                    return t != 0 ? t : a.id.CompareTo(b.id);
                });
                foreach (var r in list)
                {
                    if (!PassesFilter(r.rarity)) continue;
                    total++;
                    bool k = CodexData.IsRelicKnown(r.id);
                    if (k) known++;
                    AddRow(k, r.name, r.description, r.rarity);
                }
                break;
            }
            case Section.Potion:
            {
                var list = new List<PotionData>(cfg.potions);
                list.Sort((a, b) =>
                {
                    int t = RarityOrder(a.rarity).CompareTo(RarityOrder(b.rarity));
                    return t != 0 ? t : a.id.CompareTo(b.id);
                });
                foreach (var p in list)
                {
                    if (!PassesFilter(p.rarity)) continue;
                    total++;
                    bool k = CodexData.IsPotionKnown(p.id);
                    if (k) known++;
                    AddRow(k, p.name, p.description, p.rarity);
                }
                break;
            }
        }

        countLabel.text = $"已发现 {known} / {total}";

        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    private void AddRow(bool known, string name, string desc, string rarity)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(content, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = 25;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.enableWordWrapping = true;
        t.richText = true;
        t.raycastTarget = false;

        if (known)
        {
            string hex = RarityUtil.ColorHex(rarity);
            t.text = $"<b><color={hex}>{name}</color></b>  <size=80%><color=#8a8a95>{RarityUtil.Name(rarity)}</color></size>\n" +
                     $"<size=88%><color=#c8c8d0>{desc}</color></size>";
            t.color = Color.white;
        }
        else
        {
            t.text = "<b><color=#5a5a64>？？？</color></b>\n<size=88%><color=#4a4a54>尚未发现</color></size>";
        }
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
    }
}
