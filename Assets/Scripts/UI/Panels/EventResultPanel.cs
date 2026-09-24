using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 事件结果面板：展示事件获得的内容，尤其是「哪张牌获得了什么附魔」。
/// 完全运行时构建 UI，无需预制体。
/// </summary>
public class EventResultPanel : BasePanel
{
    private Transform contentContainer;
    private ScrollRect contentScroll;
    private Action onConfirmCb;
    private bool built;

    public void ShowOutcome(EventOutcome outcome, Action onConfirm)
    {
        onConfirmCb = onConfirm;
        EnsureBuilt();

        for (int i = contentContainer.childCount - 1; i >= 0; i--)
            Destroy(contentContainer.GetChild(i).gameObject);

        if (outcome != null)
        {
            // 1. 附魔发放：显示目标牌 + 附魔
            foreach (var granted in outcome.enchantments)
                CreateEnchantmentRow(granted);

            // 2. 其他结果文本
            foreach (var msg in outcome.messages)
                CreateMessageRow(msg);
        }

        Show();

        // 内容可能很长：滚回顶部
        if (contentScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            contentScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        var overlay = gameObject.GetComponent<Image>();
        if (overlay == null) overlay = gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.75f);
        overlay.raycastTarget = true;

        var rootRt = gameObject.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
        }

        var card = new GameObject("ResultCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(780, 660);
        cardRt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        var title = CreateText(card.transform, "Title", font, 40, TextAlignmentOptions.Center);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -28);
        titleRt.sizeDelta = new Vector2(700, 56);
        title.text = "事件结果";

        // 滚动区域（结果条目多时不会溢出到「继续」按钮下面）
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(700f, 460f);
        srt.anchoredPosition = new Vector2(0f, -96f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.004f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var container = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        container.transform.SetParent(viewport.transform, false);
        contentContainer = container.transform;
        var cRt = container.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;
        var vlg = container.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.padding = new RectOffset(8, 8, 6, 6);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var fitter = container.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        contentScroll = scrollGo.GetComponent<ScrollRect>();
        contentScroll.content = cRt;
        contentScroll.viewport = vrt;
        contentScroll.horizontal = false;
        contentScroll.vertical = true;
        contentScroll.movementType = ScrollRect.MovementType.Clamped;
        contentScroll.scrollSensitivity = 30f;

        var btnGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(card.transform, false);
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0, 30);
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
            var cb = onConfirmCb;
            onConfirmCb = null;
            Hide();
            cb?.Invoke();
        });

        var label = CreateText(btnGo.transform, "Label", font, 26, TextAlignmentOptions.Center);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        label.text = "继续";
    }

    private void CreateEnchantmentRow(GrantedEnchantment granted)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var ench = ConfigLoader.GetEnchantment(granted.enchantmentId);

        string enchName = ench != null ? ench.name : $"附魔#{granted.enchantmentId}";
        string enchDesc = ench != null ? ench.description : "";

        var row = new GameObject("EnchantRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(contentContainer, false);
        var rt = row.GetComponent<RectTransform>();

        // 描述可能换行：按字数粗估行数，避免文字被裁切
        int descLines = string.IsNullOrEmpty(enchDesc) ? 1 : Mathf.Max(1, Mathf.CeilToInt(enchDesc.Length / 26f));
        rt.sizeDelta = new Vector2(660, Mathf.Max(116f, 78f + 24f * descLines));
        row.GetComponent<Image>().color = new Color(0.18f, 0.19f, 0.25f, 1f);

        // 卡牌图片
        var imgGo = new GameObject("CardImage", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(row.transform, false);
        var irt = imgGo.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0f, 0.5f);
        irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(16f, 0f);
        irt.sizeDelta = new Vector2(72f, 100f);
        var img = imgGo.GetComponent<Image>();
        img.preserveAspect = true;
        var sprite = LoadCardSprite(granted.cardKey);
        if (sprite != null) img.sprite = sprite;
        else img.color = new Color(0.3f, 0.3f, 0.35f, 1f);

        // 文本
        var text = CreateText(row.transform, "Text", font, 24, TextAlignmentOptions.Left);
        var trt = text.rectTransform;
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(104f, 10f);
        trt.offsetMax = new Vector2(-16f, -10f);

        text.text = $"{granted.cardDisplayName} 获得附魔\n<color=#FFD700>{enchName}</color>\n<size=80%>{enchDesc}</size>";
    }

    private void CreateMessageRow(string msg)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var text = CreateText(contentContainer, "Message", font, 24, TextAlignmentOptions.Center);

        // 长文本按字数粗估行数，避免被裁切
        int lines = Mathf.Max(1, Mathf.CeilToInt((msg?.Length ?? 0) / 26f));
        text.rectTransform.sizeDelta = new Vector2(660, 32 * lines + 6);
        text.color = new Color(0.85f, 0.85f, 0.9f);
        text.text = msg;
    }

    private Sprite LoadCardSprite(string cardKey)
    {
        if (string.IsNullOrEmpty(cardKey)) return null;
        var parts = cardKey.Split('_');
        if (parts.Length != 2) return null;
        if (!Enum.TryParse(parts[0], out Suit suit)) return null;
        if (!int.TryParse(parts[1], out int rank)) return null;

        string rankStr = rank switch
        {
            11 => "J",
            12 => "Q",
            13 => "K",
            14 => "A",
            _ => rank.ToString()
        };
        string suitLetter = suit switch
        {
            Suit.Spade => "S",
            Suit.Heart => "H",
            Suit.Club => "C",
            Suit.Diamond => "D",
            _ => ""
        };
        return Resources.Load<Sprite>($"Poker/{rankStr}-{suitLetter}");
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
        return t;
    }
}
