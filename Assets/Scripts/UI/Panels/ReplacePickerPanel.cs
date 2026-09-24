using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 通用「已满，选择要替换掉的」面板：
/// 顶部显示新物品，中间列出当前拥有的物品（点击 = 用它替换掉），底部为取消。
/// 遗物 / 药水共用。运行时构建 UI。
/// </summary>
public class ReplacePickerPanel : BasePanel
{
    /// <summary>候选条目</summary>
    public class Entry
    {
        public int id;
        public string name;
        public string description;
        public string colorHex = "#FFFFFF";
    }

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI newItemText;
    private TextMeshProUGUI cancelLabel;
    private Transform list;
    private Action<int> onReplace;
    private Action onCancel;
    private bool built;

    /// <summary>newItem* 为新物品的展示信息；onReplace(被替换掉的 id) / onCancel()</summary>
    public void Show(string title, string newItemName, string newItemDesc, string newItemColor,
                     List<Entry> owned, Action<int> onReplace, Action onCancel, string cancelText = null)
    {
        this.onReplace = onReplace;
        this.onCancel = onCancel;

        EnsureBuilt();

        if (titleText != null) titleText.text = string.IsNullOrEmpty(title) ? "栏位已满" : title;
        if (cancelLabel != null) cancelLabel.text = string.IsNullOrEmpty(cancelText) ? "取消" : cancelText;

        if (newItemText != null)
        {
            newItemText.text = $"新获得：<color={newItemColor}>{newItemName}</color>\n<size=85%>{newItemDesc}</size>";
        }

        BuildList(owned);
        Show();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

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

        var card = new GameObject("ReplaceCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(740f, 700f);
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.99f);

        titleText = CreateText(card.transform, "Title", font, 32, TextAlignmentOptions.Center, new Vector2(680f, 50f));
        var trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -18f);

        // 新物品信息
        var infoGo = new GameObject("NewItem", typeof(RectTransform), typeof(Image));
        infoGo.transform.SetParent(card.transform, false);
        var irt = infoGo.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 1f);
        irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.sizeDelta = new Vector2(660f, 84f);
        irt.anchoredPosition = new Vector2(0f, -72f);
        infoGo.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.3f, 1f);

        newItemText = CreateText(infoGo.transform, "Text", font, 21, TextAlignmentOptions.Left, new Vector2(636f, 80f));
        var nrt = newItemText.rectTransform;
        nrt.anchorMin = Vector2.zero;
        nrt.anchorMax = Vector2.one;
        nrt.offsetMin = new Vector2(12f, 4f);
        nrt.offsetMax = new Vector2(-12f, -4f);
        newItemText.enableWordWrapping = true;

        // 提示
        var hint = CreateText(card.transform, "Hint", font, 20, TextAlignmentOptions.Left, new Vector2(660f, 30f));
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(0.5f, 1f);
        hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -166f);
        hint.text = "点击下方条目，用它替换掉选中的：";
        hint.color = new Color(0.85f, 0.85f, 0.9f);

        // 滚动列表
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(660f, 400f);
        srt.anchoredPosition = new Vector2(0f, -204f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.004f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        list = content.transform;
        var crt2 = content.GetComponent<RectTransform>();
        crt2.anchorMin = new Vector2(0f, 1f);
        crt2.anchorMax = new Vector2(1f, 1f);
        crt2.pivot = new Vector2(0.5f, 1f);
        crt2.offsetMin = Vector2.zero;
        crt2.offsetMax = Vector2.zero;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = crt2;
        scroll.viewport = vrt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        // 取消
        var cancel = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        cancel.transform.SetParent(card.transform, false);
        var drt = cancel.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.5f, 0f);
        drt.anchorMax = new Vector2(0.5f, 0f);
        drt.pivot = new Vector2(0.5f, 0f);
        drt.anchoredPosition = new Vector2(0f, 22f);
        drt.sizeDelta = new Vector2(220f, 52f);
        var dimg = cancel.GetComponent<Image>();
        dimg.color = new Color(0.28f, 0.22f, 0.24f, 1f);
        var dbtn = cancel.GetComponent<Button>();
        dbtn.targetGraphic = dimg;
        var dcolors = dbtn.colors;
        dcolors.highlightedColor = new Color(0.4f, 0.3f, 0.34f, 1f);
        dcolors.pressedColor = new Color(0.2f, 0.16f, 0.18f, 1f);
        dbtn.colors = dcolors;
        dbtn.onClick.AddListener(() =>
        {
            var cb = onCancel;
            onCancel = null;
            onReplace = null;
            Hide();
            cb?.Invoke();
        });
        var dlabel = CreateText(cancel.transform, "Label", font, 24, TextAlignmentOptions.Center, new Vector2(220f, 52f));
        var dlrt = dlabel.rectTransform;
        dlrt.anchorMin = Vector2.zero;
        dlrt.anchorMax = Vector2.one;
        dlrt.offsetMin = Vector2.zero;
        dlrt.offsetMax = Vector2.zero;
        dlabel.text = "取消";
        cancelLabel = dlabel;
    }

    private void BuildList(List<Entry> owned)
    {
        for (int i = list.childCount - 1; i >= 0; i--)
            Destroy(list.GetChild(i).gameObject);

        if (owned == null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        foreach (var item in owned)
        {
            var e = item;
            var go = new GameObject("EntryBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(list, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(640f, 88f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.19f, 0.25f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.3f, 0.32f, 0.42f, 1f);
            colors.pressedColor = new Color(0.14f, 0.15f, 0.2f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                var cb = onReplace;
                onReplace = null;
                onCancel = null;
                Hide();
                cb?.Invoke(e.id);
            });

            var label = CreateText(go.transform, "Label", font, 20, TextAlignmentOptions.Left, new Vector2(620f, 84f));
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(14f, 6f);
            lrt.offsetMax = new Vector2(-14f, -6f);
            label.text = $"<color={e.colorHex}>{e.name}</color>\n<size=85%>{e.description}</size>";
            label.enableWordWrapping = true;
        }
    }

    // ===== 便捷构造 =====

    /// <summary>当前拥有的遗物 → 候选列表</summary>
    public static List<Entry> BuildRelicEntries(RunData runData)
    {
        var result = new List<Entry>();
        if (runData == null) return result;

        foreach (var id in runData.RelicIds)
        {
            var r = ConfigLoader.GetRelic(id);
            if (r == null) continue;
            result.Add(new Entry { id = id, name = r.name, description = r.description, colorHex = RarityUtil.ColorHex(r.rarity) });
        }
        return result;
    }

    /// <summary>当前拥有的药水 → 候选列表</summary>
    public static List<Entry> BuildPotionEntries(RunData runData)
    {
        var result = new List<Entry>();
        if (runData == null) return result;

        foreach (var id in runData.PotionIds)
        {
            var p = ConfigLoader.GetPotion(id);
            if (p == null) continue;
            result.Add(new Entry { id = id, name = p.name, description = p.description, colorHex = "#7FE3A0" });
        }
        return result;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align, Vector2 sizeDelta)
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
