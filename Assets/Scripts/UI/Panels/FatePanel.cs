using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 命运一览面板：显示四个花色分别对应的下一事件。
/// 部分花色的事件会以「？」隐藏。完全运行时构建 UI，无需预制体。
/// </summary>
public class FatePanel : BasePanel
{
    private Transform rowContainer;
    private Action onCloseCb;
    private bool built;

    public void ShowFate(RunData runData, Action onClose)
    {
        onCloseCb = onClose;
        EnsureBuilt();

        for (int i = rowContainer.childCount - 1; i >= 0; i--)
            Destroy(rowContainer.GetChild(i).gameObject);

        Suit[] order = { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };

        // 保底商店：连续 4 场未刷商店时，本场必出商店（只在指定花色槽显示）
        bool guaranteedShop = runData != null && runData.battlesSinceShop >= 4;
        int shopSuit = runData != null ? runData.shopSuitIndex : -1;
        // 事件效果：本场命运一览不隐藏
        bool revealAll = runData != null && runData.nextBattleRevealAll;

        foreach (var suit in order)
        {
            if (guaranteedShop && (int)suit == shopSuit)
            {
                CreateRow(suit, "商店（保底）", false, true);
                continue;
            }

            int eventId = runData != null ? runData.GetSuitEventId(suit) : 0;
            var ev = ConfigLoader.GetEvent(eventId);
            bool hidden = runData != null && runData.IsSuitHidden(suit) && !revealAll;
            string eventName = hidden ? "？？？" : (ev != null ? ev.title : "（未分配）");
            CreateRow(suit, eventName, hidden, false);
        }

        Show();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        // 全屏遮罩（拦截点击）
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

        // 中央卡片
        var card = new GameObject("FateCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(760, 560);
        cardRt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        // 标题
        var title = CreateText(card.transform, "Title", font, 42, TextAlignmentOptions.Center);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -28);
        titleRt.sizeDelta = new Vector2(700, 60);
        title.text = "花色命运";

        // 副标题
        var hint = CreateText(card.transform, "Hint", font, 22, TextAlignmentOptions.Center);
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0.5f, 1f);
        hintRt.anchorMax = new Vector2(0.5f, 1f);
        hintRt.pivot = new Vector2(0.5f, 1f);
        hintRt.anchoredPosition = new Vector2(0, -92);
        hintRt.sizeDelta = new Vector2(700, 40);
        hint.color = new Color(0.75f, 0.75f, 0.8f);
        hint.text = "本局打出的最多花色，决定战斗后进入的事件（？为未知）";

        // 行容器
        var container = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
        container.transform.SetParent(card.transform, false);
        rowContainer = container.transform;
        var cRt = container.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(640, 300);
        cRt.anchoredPosition = new Vector2(0, 10);
        var vlg = container.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 14;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 关闭按钮
        var btnGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(card.transform, false);
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0, 28);
        brt.sizeDelta = new Vector2(200, 52);

        var bimg = btnGo.GetComponent<Image>();
        bimg.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = bimg;
        btn.onClick.AddListener(() =>
        {
            var cb = onCloseCb;
            Hide();
            cb?.Invoke();
        });

        var label = CreateText(btnGo.transform, "Label", font, 24, TextAlignmentOptions.Center);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        label.text = "关闭";
    }

    private void CreateRow(Suit suit, string eventName, bool hidden, bool isShop)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(rowContainer, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(620, 62);
        go.GetComponent<Image>().color = new Color(0.18f, 0.19f, 0.25f, 1f);

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        var left = CreateText(go.transform, "Suit", font, 28, TextAlignmentOptions.Left);
        var lrt = left.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.offsetMin = new Vector2(26f, 0f);
        lrt.offsetMax = Vector2.zero;
        left.text = $"{SuitSymbol(suit)}  {SuitName(suit)}";

        var right = CreateText(go.transform, "Event", font, 26, TextAlignmentOptions.Right);
        var rrt = right.rectTransform;
        rrt.anchorMin = new Vector2(0.5f, 0f);
        rrt.anchorMax = new Vector2(1f, 1f);
        rrt.offsetMin = Vector2.zero;
        rrt.offsetMax = new Vector2(-26f, 0f);
        right.text = eventName;
        if (isShop) right.color = new Color(0.4f, 0.9f, 0.5f);
        else right.color = hidden ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.84f, 0f);
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
}
