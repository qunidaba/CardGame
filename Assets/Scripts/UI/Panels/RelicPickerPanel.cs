using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 选择一个遗物面板（事件「指定丢弃一个遗物」）。运行时构建 UI。
/// </summary>
public class RelicPickerPanel : BasePanel
{
    private TextMeshProUGUI titleText;
    private Transform list;
    private Action<int> onPick;
    private Action onCancel;
    private bool built;

    public void ShowPicker(string title, Action<int> pick, Action cancel)
    {
        onPick = pick;
        onCancel = cancel;
        EnsureBuilt();
        if (titleText != null) titleText.text = title;
        BuildList();
        Show();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        var overlay = gameObject.GetComponent<Image>();
        if (overlay == null) overlay = gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.82f);
        overlay.raycastTarget = true;

        var rootRt = gameObject.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
        }

        var card = new GameObject("RelicCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(720f, 640f);
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        titleText = CreateText(card.transform, "Title", font, 30, TextAlignmentOptions.Center, new Vector2(660f, 50f));
        var trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -20f);

        // 滚动列表
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0.5f);
        srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(660f, 480f);
        srt.anchoredPosition = new Vector2(0f, 5f);

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
        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = crt2;
        scroll.viewport = vrt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var cancel = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        cancel.transform.SetParent(card.transform, false);
        var ccrt = cancel.GetComponent<RectTransform>();
        ccrt.anchorMin = new Vector2(0.5f, 0f);
        ccrt.anchorMax = new Vector2(0.5f, 0f);
        ccrt.pivot = new Vector2(0.5f, 0f);
        ccrt.anchoredPosition = new Vector2(0f, 22f);
        ccrt.sizeDelta = new Vector2(200f, 50f);
        var cimg = cancel.GetComponent<Image>();
        cimg.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var cbtn = cancel.GetComponent<Button>();
        cbtn.targetGraphic = cimg;
        cbtn.onClick.AddListener(() => { Hide(); onCancel?.Invoke(); });
        var clabel = CreateText(cancel.transform, "Label", font, 24, TextAlignmentOptions.Center, new Vector2(200f, 50f));
        var clrt = clabel.rectTransform;
        clrt.anchorMin = Vector2.zero;
        clrt.anchorMax = Vector2.one;
        clrt.offsetMin = Vector2.zero;
        clrt.offsetMax = Vector2.zero;
        clabel.text = "取消";
    }

    private void BuildList()
    {
        for (int i = list.childCount - 1; i >= 0; i--)
            Destroy(list.GetChild(i).gameObject);

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var runData = RunDirector.Instance != null ? RunDirector.Instance.RunData : null;
        if (runData == null) return;

        foreach (var id in runData.RelicIds)
        {
            int relicId = id;
            var relic = ConfigLoader.GetRelic(relicId);
            if (relic == null) continue;

            var go = new GameObject("RelicBtn", typeof(RectTransform), typeof(Image), typeof(Button));
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
            btn.onClick.AddListener(() => { Hide(); onPick?.Invoke(relicId); });

            var label = CreateText(go.transform, "Label", font, 20, TextAlignmentOptions.Left, new Vector2(620f, 84f));
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(14f, 6f);
            lrt.offsetMax = new Vector2(-14f, -6f);
            label.text = $"<color={RarityUtil.ColorHex(relic.rarity)}>{relic.name}</color>（{RarityUtil.Name(relic.rarity)}）\n{relic.description}";
            label.enableWordWrapping = true;
        }
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
