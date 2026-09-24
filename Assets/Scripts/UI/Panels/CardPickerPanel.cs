using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 选一张牌面板：显示全部 52 张牌的图片，并显示每张牌的附魔。
/// 4 行（花色）× 13 列（点数），运行时构建 UI。
/// </summary>
public class CardPickerPanel : BasePanel
{
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI cancelLabel;
    private Transform grid;
    private Action<int, Suit> onPick;
    private Action onCancel;
    private bool viewerMode;
    private bool requireEnchanted;
    private Func<int, Suit, bool> isDisabled;
    private bool built;

    private static readonly Suit[] SuitOrder = { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };

    public void ShowPicker(string title, Action<int, Suit> pick, Action cancel, bool requireEnchanted = false, Func<int, Suit, bool> isDisabled = null)
    {
        viewerMode = false;
        this.requireEnchanted = requireEnchanted;
        this.isDisabled = isDisabled;
        onPick = pick;
        onCancel = cancel;
        EnsureBuilt();
        if (titleText != null) titleText.text = title;
        if (cancelLabel != null) cancelLabel.text = "取消";
        BuildGrid();
        Show();
    }

    /// <summary>只读查看模式：点击/右键牌看详情，底部按钮为「关闭」</summary>
    public void ShowViewer(string title, Action onClose)
    {
        viewerMode = true;
        requireEnchanted = false;
        isDisabled = null;
        onPick = null;
        onCancel = onClose;
        EnsureBuilt();
        if (titleText != null) titleText.text = title;
        if (cancelLabel != null) cancelLabel.text = "关闭";
        BuildGrid();
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

        var card = new GameObject("PickerCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1370f, 770f);
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.99f);

        titleText = CreateText(card.transform, "Title", font, 32, TextAlignmentOptions.Center, new Vector2(1240f, 50f));
        var trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -18f);

        var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(card.transform, false);
        grid = gridGo.transform;
        var grt = gridGo.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0.5f, 0.5f);
        grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.pivot = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(1320f, 580f);
        grt.anchoredPosition = new Vector2(0f, 20f);
        var glg = gridGo.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(96f, 136f);
        glg.spacing = new Vector2(5f, 5f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 13;
        glg.childAlignment = TextAnchor.MiddleCenter;

        var cancel = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        cancel.transform.SetParent(card.transform, false);
        var crt2 = cancel.GetComponent<RectTransform>();
        crt2.anchorMin = new Vector2(0.5f, 0f);
        crt2.anchorMax = new Vector2(0.5f, 0f);
        crt2.pivot = new Vector2(0.5f, 0f);
        crt2.anchoredPosition = new Vector2(0f, 18f);
        crt2.sizeDelta = new Vector2(200f, 50f);
        var cimg = cancel.GetComponent<Image>();
        cimg.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var cbtn = cancel.GetComponent<Button>();
        cbtn.targetGraphic = cimg;
        cbtn.onClick.AddListener(() => { HideDetail(); Hide(); onCancel?.Invoke(); });
        var clabel = CreateText(cancel.transform, "Label", font, 24, TextAlignmentOptions.Center, new Vector2(200f, 50f));
        var clrt = clabel.rectTransform;
        clrt.anchorMin = Vector2.zero;
        clrt.anchorMax = Vector2.one;
        clrt.offsetMin = Vector2.zero;
        clrt.offsetMax = Vector2.zero;
        clabel.text = "取消";
        cancelLabel = clabel;
    }

    private void BuildGrid()
    {
        for (int i = grid.childCount - 1; i >= 0; i--)
            Destroy(grid.GetChild(i).gameObject);

        var runData = RunDirector.Instance != null ? RunDirector.Instance.RunData : null;
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        foreach (var suit in SuitOrder)
        {
            for (int rank = 2; rank <= 14; rank++)
            {
                int r = rank;
                Suit s = suit;

                var go = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(grid, false);

                // 先算附魔（决定这张牌是否可选）
                var ids = runData != null ? runData.GetCardEnchantments(r, s) : null;
                var names = new List<string>();
                if (ids != null)
                {
                    foreach (var id in ids)
                    {
                        var e = ConfigLoader.GetEnchantment(id);
                        if (e != null) names.Add(e.name);
                    }
                }
                bool enchanted = names.Count > 0;
                bool disabled = (requireEnchanted && !enchanted) || (isDisabled != null && isDisabled(r, s));

                var img = go.GetComponent<Image>();
                var sprite = Resources.Load<Sprite>($"Poker/{ResourceName(r, s)}");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = disabled ? new Color(0.35f, 0.35f, 0.4f, 1f) : Color.white;
                    img.preserveAspect = true;
                }
                else
                {
                    img.color = new Color(0.18f, 0.19f, 0.25f, 1f);
                }

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.interactable = !disabled;
                btn.onClick.AddListener(() =>
                {
                    if (viewerMode)
                    {
                        ShowDetail(r, s);
                        return;
                    }
                    HideDetail();
                    Hide();
                    onPick?.Invoke(r, s);
                });

                // 右键：查看这张牌的详情
                var rc = go.AddComponent<RightClickable>();
                rc.OnRightClick = () => ShowDetail(r, s);

                if (enchanted)
                {
                    // 底部深色条 + 附魔名
                    var strip = new GameObject("Strip", typeof(RectTransform), typeof(Image));
                    strip.transform.SetParent(go.transform, false);
                    var srt = strip.GetComponent<RectTransform>();
                    srt.anchorMin = new Vector2(0f, 0f);
                    srt.anchorMax = new Vector2(1f, 0f);
                    srt.pivot = new Vector2(0.5f, 0f);
                    srt.sizeDelta = new Vector2(0f, names.Count > 2 ? 50f : 34f);
                    srt.anchoredPosition = Vector2.zero;
                    var simg = strip.GetComponent<Image>();
                    simg.color = new Color(0f, 0f, 0f, 0.72f);
                    simg.raycastTarget = false;

                    string text = string.Join("\n", names.GetRange(0, Mathf.Min(2, names.Count)));
                    if (names.Count > 2) text += $"\n+{names.Count - 2}";

                    var label = CreateText(strip.transform, "Ench", font, 14, TextAlignmentOptions.Center, new Vector2(96f, 50f));
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
    }

    private static void HideDetail()
    {
        UIManager.Instance?.Hide<CardDetailPanel>();
    }

    private static void ShowDetail(int rank, Suit suit)
    {
        if (UIManager.Instance == null) return;
        var detail = UIManager.Instance.ShowPanel<CardDetailPanel>();
        detail?.Show(rank, suit, null);
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
