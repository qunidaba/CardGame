using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 牌的详情窗口：左边显示牌面，右边列出该牌所有附魔的具体效果，底部「返回」。
/// 运行时构建 UI。
/// </summary>
public class CardDetailPanel : BasePanel
{
    private Image cardImage;
    private Transform enchantList;
    private Action onBack;
    private bool built;

    public void Show(int rank, Suit suit, Action back)
    {
        onBack = back;
        EnsureBuilt();
        Refresh(rank, suit);
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

        var card = new GameObject("DetailCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1000f, 640f);
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.99f);

        // ===== 左侧：牌面 =====
        var imgGo = new GameObject("CardImage", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(card.transform, false);
        var irt = imgGo.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0f, 0.5f);
        irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(40f, 30f);
        irt.sizeDelta = new Vector2(300f, 420f);
        cardImage = imgGo.GetComponent<Image>();
        cardImage.preserveAspect = true;
        cardImage.raycastTarget = false;

        // ===== 右侧：附魔列表 =====
        var title = CreateText(card.transform, "EnchTitle", font, 28, TextAlignmentOptions.Left, new Vector2(540f, 40f));
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(0f, 1f);
        trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = new Vector2(390f, -30f);
        title.text = "附魔";

        var listGo = new GameObject("EnchantList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listGo.transform.SetParent(card.transform, false);
        enchantList = listGo.transform;
        var lrt = listGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0f, 1f);
        lrt.anchoredPosition = new Vector2(390f, -80f);
        lrt.sizeDelta = new Vector2(570f, 460f);
        var vlg = listGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // ===== 返回 =====
        var backGo = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
        backGo.transform.SetParent(card.transform, false);
        var brt = backGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 22f);
        brt.sizeDelta = new Vector2(200f, 52f);
        var bimg = backGo.GetComponent<Image>();
        bimg.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var btn = backGo.GetComponent<Button>();
        btn.targetGraphic = bimg;
        btn.onClick.AddListener(() =>
        {
            Hide();
            onBack?.Invoke();
        });
        var blabel = CreateText(backGo.transform, "Label", font, 24, TextAlignmentOptions.Center, new Vector2(200f, 52f));
        var blrt = blabel.rectTransform;
        blrt.anchorMin = Vector2.zero;
        blrt.anchorMax = Vector2.one;
        blrt.offsetMin = Vector2.zero;
        blrt.offsetMax = Vector2.zero;
        blabel.text = "返回";
    }

    private void Refresh(int rank, Suit suit)
    {
        var sprite = Resources.Load<Sprite>($"Poker/{ResourceName(rank, suit)}");
        if (cardImage != null)
        {
            cardImage.sprite = sprite;
            cardImage.enabled = sprite != null;
        }

        for (int i = enchantList.childCount - 1; i >= 0; i--)
            Destroy(enchantList.GetChild(i).gameObject);

        var runData = RunDirector.Instance != null ? RunDirector.Instance.RunData : null;
        var ids = runData != null ? runData.GetCardEnchantments(rank, suit) : new List<int>();

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        if (ids == null || ids.Count == 0)
        {
            var none = CreateText(enchantList, "None", font, 22, TextAlignmentOptions.Left, new Vector2(560f, 40f));
            none.text = "无附魔";
            none.color = new Color(0.7f, 0.7f, 0.75f);
            return;
        }

        foreach (var id in ids)
        {
            var e = ConfigLoader.GetEnchantment(id);
            if (e == null) continue;

            var row = new GameObject("Row", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(enchantList, false);
            row.GetComponent<Image>().color = new Color(0.18f, 0.19f, 0.25f, 1f);
            row.GetComponent<Image>().raycastTarget = false;

            var rowText = CreateText(row.transform, "Text", font, 21, TextAlignmentOptions.TopLeft, new Vector2(550f, 60f));
            var rrt = rowText.rectTransform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(12f, 8f);
            rrt.offsetMax = new Vector2(-12f, -8f);
            rowText.text = $"<color=#FFD700>{e.name}</color>（{RarityUtil.Name(e.rarity)}）\n{e.description}";
            rowText.raycastTarget = false;

            // 行高按文字自适应
            var fitter = row.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private static string CardName(int rank, Suit suit)
    {
        string r;
        switch (rank)
        {
            case 11: r = "J"; break;
            case 12: r = "Q"; break;
            case 13: r = "K"; break;
            case 14: r = "A"; break;
            default: r = rank.ToString(); break;
        }
        string s;
        switch (suit)
        {
            case Suit.Spade: s = "♠"; break;
            case Suit.Heart: s = "♥"; break;
            case Suit.Club: s = "♣"; break;
            default: s = "♦"; break;
        }
        return r + s;
    }

    private static string ResourceName(int rank, Suit suit)
    {
        string r;
        switch (rank)
        {
            case 11: r = "J"; break;
            case 12: r = "Q"; break;
            case 13: r = "K"; break;
            case 14: r = "A"; break;
            default: r = rank.ToString(); break;
        }
        string s;
        switch (suit)
        {
            case Suit.Spade: s = "S"; break;
            case Suit.Heart: s = "H"; break;
            case Suit.Club: s = "C"; break;
            default: s = "D"; break;
        }
        return $"{r}-{s}";
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
