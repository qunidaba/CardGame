using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 显示一堆牌（弃牌堆 / 牌堆）：
/// - 选择模式（附魔「搜寻」）：点牌选中；
/// - 查看模式：只读，点牌看详情，底部「关闭」。
/// 运行时构建 UI。
/// </summary>
public class DiscardPickerPanel : BasePanel
{
    private TextMeshProUGUI titleText;
    private Transform grid;
    private Action<CardData> onPick;
    private Action onClose;
    private Button closeButton;
    private ScrollRect scrollRect;
    private bool viewerMode;
    private bool built;

    public void ShowPicker(string title, List<CardData> cards, Action<CardData> pick)
    {
        viewerMode = false;
        onPick = pick;
        onClose = null;
        EnsureBuilt();
        if (closeButton != null) closeButton.gameObject.SetActive(false);
        if (titleText != null) titleText.text = title;
        BuildGrid(cards);
        Show();
        ResetScroll();
    }

    /// <summary>只读查看模式：点牌看详情，底部按钮为「关闭」</summary>
    public void ShowViewer(string title, List<CardData> cards, Action onClose)
    {
        viewerMode = true;
        onPick = null;
        this.onClose = onClose;
        EnsureBuilt();
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (titleText != null) titleText.text = title;
        BuildGrid(cards);
        Show();
        ResetScroll();
    }

    private void ResetScroll()
    {
        if (scrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
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

        var card = new GameObject("DiscardCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1240f, 660f);
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.99f);

        titleText = CreateText(card.transform, "Title", font, 32, TextAlignmentOptions.Center, new Vector2(1160f, 50f));
        var trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -20f);

        // 滚动区域（牌多时不会溢出）
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0.5f);
        srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(1180f, 510f);
        srt.anchoredPosition = new Vector2(0f, 15f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.004f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        gridGo.transform.SetParent(viewport.transform, false);
        grid = gridGo.transform;
        var grt = gridGo.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0f, 1f);
        grt.anchorMax = new Vector2(1f, 1f);
        grt.pivot = new Vector2(0.5f, 1f);
        grt.offsetMin = Vector2.zero;
        grt.offsetMax = Vector2.zero;
        var glg = gridGo.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(84f, 118f);
        glg.spacing = new Vector2(6f, 6f);
        glg.padding = new RectOffset(8, 8, 8, 8);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 13;
        glg.childAlignment = TextAnchor.UpperCenter;
        var fitter = gridGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.content = grt;
        scrollRect.viewport = vrt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // 关闭按钮（仅查看模式显示）
        var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(card.transform, false);
        var crt2 = closeGo.GetComponent<RectTransform>();
        crt2.anchorMin = new Vector2(0.5f, 0f);
        crt2.anchorMax = new Vector2(0.5f, 0f);
        crt2.pivot = new Vector2(0.5f, 0f);
        crt2.anchoredPosition = new Vector2(0f, 18f);
        crt2.sizeDelta = new Vector2(200f, 50f);
        var cimg = closeGo.GetComponent<Image>();
        cimg.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        closeButton = closeGo.GetComponent<Button>();
        closeButton.targetGraphic = cimg;
        closeButton.onClick.AddListener(() =>
        {
            var cb = onClose;
            onClose = null;
            Hide();
            cb?.Invoke();
        });
        var clabel = CreateText(closeGo.transform, "Label", font, 24, TextAlignmentOptions.Center, new Vector2(200f, 50f));
        var clrt = clabel.rectTransform;
        clrt.anchorMin = Vector2.zero;
        clrt.anchorMax = Vector2.one;
        clrt.offsetMin = Vector2.zero;
        clrt.offsetMax = Vector2.zero;
        clabel.text = "关闭";
        closeGo.SetActive(false);
    }

    private static readonly Suit[] SuitOrder = { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };

    private void BuildGrid(List<CardData> cards)
    {
        for (int i = grid.childCount - 1; i >= 0; i--)
            Destroy(grid.GetChild(i).gameObject);

        if (cards == null) return;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        // 固定 4 行 × 13 列：每行一个花色，列按点数 2→A 排列；
        // 不在堆里的位置放空占位，保证列对齐（不按抽取顺序排）
        var pool = new List<CardData>(cards);

        foreach (Suit suit in SuitOrder)
        {
            for (int rank = 2; rank <= 14; rank++)
            {
                // 按「原始花色点数」定位（判定覆盖只影响牌面/效果，不影响摆放位置），保证一格一张不撞位
                int idx = pool.FindIndex(c => c != null && c.rank == rank && c.suit == suit);
                if (idx < 0)
                {
                    new GameObject("Empty", typeof(RectTransform)).transform.SetParent(grid, false);
                    continue;
                }

                var c = pool[idx];
                pool.RemoveAt(idx);
                CreateCell(c, font);
            }
        }

        // 兜底：判定坐标重复 / 越界的牌直接追加，避免漏显示或相互覆盖
        foreach (var c in pool)
            CreateCell(c, font);
    }

    private void CreateCell(CardData c, TMP_FontAsset font)
    {
        var go = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(grid, false);

        var img = go.GetComponent<Image>();
        var sprite = Resources.Load<Sprite>($"Poker/{ResourceName(c.EffectiveRank, c.EffectiveSuit)}");
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        else
        {
            img.color = new Color(0.18f, 0.19f, 0.25f, 1f);
        }

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            if (viewerMode)
            {
                ShowDetail(c);
                return;
            }
            Hide();
            onPick?.Invoke(c);
        });

        // 右键看详情
        var rc = go.AddComponent<RightClickable>();
        rc.OnRightClick = () => ShowDetail(c);

        var names = c.GetEnchantmentNames();
        if (names.Count > 0)
        {
            var strip = new GameObject("Strip", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(go.transform, false);
            var srt = strip.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.sizeDelta = new Vector2(0f, names.Count > 2 ? 46f : 30f);
            srt.anchoredPosition = Vector2.zero;
            var simg = strip.GetComponent<Image>();
            simg.color = new Color(0f, 0f, 0f, 0.72f);
            simg.raycastTarget = false;

            string text = string.Join("\n", names.GetRange(0, Mathf.Min(2, names.Count)));
            if (names.Count > 2) text += $"\n+{names.Count - 2}";

            var label = CreateText(strip.transform, "Ench", font, 13, TextAlignmentOptions.Center, new Vector2(84f, 46f));
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

    private static void ShowDetail(CardData c)
    {
        if (UIManager.Instance == null || c == null) return;
        var detail = UIManager.Instance.ShowPanel<CardDetailPanel>();
        detail?.Show(c.EffectiveRank, c.EffectiveSuit, null);
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
