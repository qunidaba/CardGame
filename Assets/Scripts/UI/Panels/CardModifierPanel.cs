using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;

/// <summary>
/// 牌改造面板：万能牌（改花色+点数）、变色（改花色）、镜牌（复制手牌中的另一张）。
/// 镜牌模式会列出手牌缩略图：左键选中、右键查看附魔详情，点「确认」才应用并退出。
/// 完全运行时构建 UI，无需预制体。
/// </summary>
public class CardModifierPanel : BasePanel
{
    private CardData card;
    private BattleManager battleManager;
    private Action onCloseCb;

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI infoText;
    private Transform controls;
    private bool built;

    // 镜牌：选中的复制目标
    private CardData mirrorTarget;

    public void ShowCard(CardData c, BattleManager bm, Action onClose)
    {
        card = c;
        battleManager = bm;
        onCloseCb = onClose;
        mirrorTarget = null;
        EnsureBuilt();
        Refresh();
        Show();
    }

    private void Refresh()
    {
        if (card == null) return;

        titleText.text = "改造牌";
        var names = card.GetEnchantmentNames();
        infoText.text = $"当前：{card.DisplayName}    附魔：{(names.Count > 0 ? string.Join("、", names) : "无")}";

        for (int i = controls.childCount - 1; i >= 0; i--)
            Destroy(controls.GetChild(i).gameObject);

        if (card.CanWildcard || card.CanSuitShift)
        {
            MakeLabel("选择花色");
            var row = MakeRow();
            foreach (Suit s in new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond })
            {
                var suit = s;
                MakeButton(row, SuitSymbol(suit), () =>
                {
                    if (card.CanWildcard)
                        card.SetJudgeOverride(card.EffectiveRank, (int)suit);
                    else
                        card.SetSuitOverride((int)suit);
                    AfterChange();
                });
            }
        }

        if (card.CanWildcard)
        {
            MakeLabel("选择点数");
            var row = MakeRow();
            for (int r = 2; r <= 14; r++)
            {
                int rank = r;
                MakeButton(row, RankLabel(r), () =>
                {
                    card.SetJudgeOverride(rank, (int)card.EffectiveSuit);
                    AfterChange();
                });
            }
        }

        if (card.CanMirror)
        {
            MakeLabel("复制手牌中的另一张（花色 / 点数 / 附魔一致）　左键选中 · 右键查看详情");
            BuildMirrorGrid();
        }

        var btnRow = MakeRow();
        if (card.CanMirror)
            MakeButton(btnRow, "确认", ConfirmMirror, mirrorTarget != null);
        MakeButton(btnRow, "重置", () =>
        {
            card.ClearOverrides();
            mirrorTarget = null;
            AfterChange();
        });
        MakeButton(btnRow, "关闭", ClosePanel);
    }

    private void AfterChange()
    {
        var hand = battleManager?.GetHandArea();
        hand?.ClearSelection();
        hand?.SortHand();
        battleManager?.NotifyEnchantmentsChanged();
        Refresh();
    }

    /// <summary>确认复制并退出改造界面</summary>
    private void ConfirmMirror()
    {
        if (card == null || mirrorTarget == null) return;

        card.MirrorCopy(mirrorTarget);
        Debug.Log($"[镜牌] {card.DisplayName} 复制了 {mirrorTarget.DisplayName}");

        var hand = battleManager?.GetHandArea();
        hand?.ClearSelection();
        hand?.SortHand();
        battleManager?.NotifyEnchantmentsChanged();

        ClosePanel();
    }

    private void ClosePanel()
    {
        var cb = onCloseCb;
        onCloseCb = null;
        Hide();
        cb?.Invoke();
    }

    // ===== 镜牌：手牌网格 =====

    private void BuildMirrorGrid()
    {
        var hand = battleManager?.GetHandArea();
        if (hand == null) return;

        var scrollGo = new GameObject("MirrorScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(controls, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.sizeDelta = new Vector2(840f, 320f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.004f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;
        var glg = content.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(88f, 124f);
        glg.spacing = new Vector2(5f, 5f);
        glg.padding = new RectOffset(6, 6, 6, 6);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 9;
        glg.childAlignment = TextAnchor.UpperCenter;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = crt;
        scroll.viewport = vrt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        foreach (var c in hand.HandCards)
        {
            if (c == card) continue;   // 不能复制自己
            var target = c;

            var go = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(content.transform, false);

            var img = go.GetComponent<Image>();
            var sprite = Resources.Load<Sprite>($"Poker/{ResourceName(target.EffectiveRank, target.EffectiveSuit)}");
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
            }
            // 选中高亮
            img.color = (mirrorTarget == target) ? new Color(0.65f, 1f, 0.65f) : Color.white;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                mirrorTarget = target;
                Refresh();   // 重建以刷新高亮与「确认」可用状态
            });

            // 右键：查看这张牌的附魔详情
            var rc = go.AddComponent<RightClickable>();
            rc.OnRightClick = () =>
            {
                if (UIManager.Instance == null) return;
                UIManager.Instance.ShowPanel<CardDetailPanel>()?.Show(target.EffectiveRank, target.EffectiveSuit, null);
            };

            // 附魔名条
            var names = target.GetEnchantmentNames();
            if (names.Count > 0)
            {
                var strip = new GameObject("Strip", typeof(RectTransform), typeof(Image));
                strip.transform.SetParent(go.transform, false);
                var strt = strip.GetComponent<RectTransform>();
                strt.anchorMin = new Vector2(0f, 0f);
                strt.anchorMax = new Vector2(1f, 0f);
                strt.pivot = new Vector2(0.5f, 0f);
                strt.sizeDelta = new Vector2(0f, names.Count > 2 ? 46f : 30f);
                strt.anchoredPosition = Vector2.zero;
                var simg = strip.GetComponent<Image>();
                simg.color = new Color(0f, 0f, 0f, 0.72f);
                simg.raycastTarget = false;

                string text = string.Join("\n", names.GetRange(0, Mathf.Min(2, names.Count)));
                if (names.Count > 2) text += $"\n+{names.Count - 2}";

                var label = CreateText(strip.transform, "Ench", font, 13, TextAlignmentOptions.Center);
                var lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(1f, 0f);
                lrt.offsetMax = new Vector2(-1f, 0f);
                label.text = text;
                label.color = new Color(1f, 0.92f, 0.55f);
                label.raycastTarget = false;
                label.enableWordWrapping = false;
            }
        }
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

    // ===== UI 构建 =====

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

        var cardPanel = new GameObject("ModifierCard", typeof(RectTransform), typeof(Image));
        cardPanel.transform.SetParent(transform, false);
        var cardRt = cardPanel.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(1200, 820);
        cardRt.anchoredPosition = Vector2.zero;
        cardPanel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        titleText = CreateText(cardPanel.transform, "Title", font, 40, TextAlignmentOptions.Center);
        var trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0, -24);
        trt.sizeDelta = new Vector2(860, 56);

        infoText = CreateText(cardPanel.transform, "Info", font, 24, TextAlignmentOptions.Center);
        var irt = infoText.rectTransform;
        irt.anchorMin = new Vector2(0.5f, 1f);
        irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.anchoredPosition = new Vector2(0, -88);
        irt.sizeDelta = new Vector2(860, 40);
        infoText.color = new Color(1f, 0.9f, 0.4f);

        var container = new GameObject("Controls", typeof(RectTransform), typeof(VerticalLayoutGroup));
        container.transform.SetParent(cardPanel.transform, false);
        controls = container.transform;
        var crt = container.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(860, 660);
        crt.anchoredPosition = new Vector2(0, -60);
        var vlg = container.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    private void MakeLabel(string text)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var t = CreateText(controls, "Label", font, 22, TextAlignmentOptions.Center);
        t.rectTransform.sizeDelta = new Vector2(840, 32);
        t.color = new Color(0.85f, 0.85f, 0.9f);
        t.text = text;
    }

    private Transform MakeRow()
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(controls, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(840, 54);
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        return go.transform;
    }

    private void MakeButton(Transform parent, string label, Action onClick, bool interactable = true)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(72, 48);

        var img = go.GetComponent<Image>();
        img.color = interactable ? new Color(0.22f, 0.24f, 0.32f, 1f) : new Color(0.16f, 0.16f, 0.2f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = interactable;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        colors.disabledColor = new Color(0.16f, 0.16f, 0.2f, 0.8f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var t = CreateText(go.transform, "Label", font, 22, TextAlignmentOptions.Center);
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        t.text = label;
        if (!interactable) t.color = new Color(0.5f, 0.5f, 0.55f);
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

    private static string RankLabel(int rank)
    {
        switch (rank)
        {
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            case 14: return "A";
            default: return rank.ToString();
        }
    }
}
