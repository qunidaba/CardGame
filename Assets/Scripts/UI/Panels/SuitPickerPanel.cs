using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;

/// <summary>
/// 选一个花色面板（♠♥♣♦）。运行时构建 UI。
/// </summary>
public class SuitPickerPanel : BasePanel
{
    private TextMeshProUGUI titleText;
    private Transform row;
    private Action<Suit> onPick;
    private Action onCancel;
    private bool built;

    private static readonly Suit[] SuitOrder = { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };

    public void ShowPicker(string title, Action<Suit> pick, Action cancel)
    {
        onPick = pick;
        onCancel = cancel;
        EnsureBuilt();
        if (titleText != null) titleText.text = title;
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

        var card = new GameObject("SuitCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(760f, 360f);
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        titleText = CreateText(card.transform, "Title", font, 30, TextAlignmentOptions.Center, new Vector2(700f, 50f));
        var trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -20f);

        var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(card.transform, false);
        row = rowGo.transform;
        var rrt = rowGo.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0.5f);
        rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(700f, 130f);
        rrt.anchoredPosition = new Vector2(0f, 10f);
        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;

        foreach (var suit in SuitOrder)
        {
            Suit s = suit;
            var go = new GameObject("SuitBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(row, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 120f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.19f, 0.25f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => { Hide(); onPick?.Invoke(s); });

            var label = CreateText(go.transform, "Label", font, 30, TextAlignmentOptions.Center, new Vector2(140f, 120f));
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            label.text = $"{SuitSymbol(s)}\n{SuitName(s)}";
        }

        var cancel = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        cancel.transform.SetParent(card.transform, false);
        var crt2 = cancel.GetComponent<RectTransform>();
        crt2.anchorMin = new Vector2(0.5f, 0f);
        crt2.anchorMax = new Vector2(0.5f, 0f);
        crt2.pivot = new Vector2(0.5f, 0f);
        crt2.anchoredPosition = new Vector2(0f, 22f);
        crt2.sizeDelta = new Vector2(200f, 50f);
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

    private static string SuitName(Suit s)
    {
        switch (s)
        {
            case Suit.Spade: return "黑桃";
            case Suit.Heart: return "红桃";
            case Suit.Club: return "梅花";
            case Suit.Diamond: return "方块";
            default: return "?";
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
