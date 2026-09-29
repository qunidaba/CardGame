using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板：主音量 / 全屏 / 分辨率。纯代码构建，运行时无预制体。
/// </summary>
public class SettingsPanel : BasePanel
{
    private bool built;
    private TextMeshProUGUI volumeValue;
    private TextMeshProUGUI fullscreenValue;
    private TextMeshProUGUI resolutionValue;

    public override void Init()
    {
        base.Init();
        EnsureBuilt();
        RefreshLabels();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        // 全屏遮罩（挡住主菜单的点击）
        var overlay = gameObject.GetComponent<Image>();
        if (overlay == null) overlay = gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.8f);
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
        var card = new GameObject("SettingsCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(760f, 560f);
        crt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        var title = CreateText(card.transform, "Title", font, 44, TextAlignmentOptions.Center);
        title.text = "设置";
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30));
        title.rectTransform.sizeDelta = new Vector2(600, 60);

        // 内容竖向排列
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(card.transform, false);
        var ccr = content.GetComponent<RectTransform>();
        ccr.anchorMin = new Vector2(0.5f, 1f);
        ccr.anchorMax = new Vector2(0.5f, 1f);
        ccr.pivot = new Vector2(0.5f, 1f);
        ccr.sizeDelta = new Vector2(660f, 380f);
        ccr.anchoredPosition = new Vector2(0, -120);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 22;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // ---- 主音量 ----
        var volRow = AddRow(content.transform);
        AddLabel(volRow, "主音量", font);
        AddButton(volRow, "－", 70, font, () => ChangeVolume(-0.1f));
        volumeValue = AddValue(volRow, font);
        AddButton(volRow, "＋", 70, font, () => ChangeVolume(0.1f));

        // ---- 全屏 ----
        var fsRow = AddRow(content.transform);
        AddLabel(fsRow, "全屏", font);
        AddFlex(fsRow);
        fullscreenValue = AddValue(fsRow, font);
        AddButton(fsRow, "切换", 110, font, () =>
        {
            GameSettings.SetFullscreen(!GameSettings.Fullscreen);
            RefreshLabels();
        });

        // ---- 分辨率 ----
        var resRow = AddRow(content.transform);
        AddLabel(resRow, "分辨率", font);
        AddButton(resRow, "<", 70, font, () =>
        {
            GameSettings.CycleResolution(-1);
            RefreshLabels();
        });
        resolutionValue = AddValue(resRow, font);
        AddButton(resRow, ">", 70, font, () =>
        {
            GameSettings.CycleResolution(1);
            RefreshLabels();
        });

        // ---- 底部按钮 ----
        var bottom = AddRow(content.transform);
        AddFlex(bottom);
        AddButton(bottom, "恢复默认", 150, font, () =>
        {
            GameSettings.ResetToDefault();
            RefreshLabels();
        });
        AddButton(bottom, "返回", 150, font, Close);
        AddFlex(bottom);
    }

    private void ChangeVolume(float delta)
    {
        GameSettings.SetVolume(GameSettings.Volume + delta);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (volumeValue != null)
            volumeValue.text = $"{Mathf.RoundToInt(GameSettings.Volume * 100f)}%";
        if (fullscreenValue != null)
            fullscreenValue.text = GameSettings.Fullscreen ? "开" : "关";
        if (resolutionValue != null)
            resolutionValue.text = GameSettings.ResolutionLabel();
    }

    private void Close()
    {
        if (UIManager.Instance != null) UIManager.Instance.Hide<SettingsPanel>();
        else Hide();
    }

    // ===== 构建辅助 =====

    private RectTransform AddRow(Transform parent)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 12;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        go.GetComponent<LayoutElement>().preferredHeight = 70f;
        return go.GetComponent<RectTransform>();
    }

    private TextMeshProUGUI AddLabel(Transform row, string text, TMP_FontAsset font)
    {
        var t = CreateText(row, "Label", font, 30, TextAlignmentOptions.Left);
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 220f;
        le.flexibleWidth = 1f;
        t.text = text;
        return t;
    }

    private TextMeshProUGUI AddValue(Transform row, TMP_FontAsset font)
    {
        var t = CreateText(row, "Value", font, 30, TextAlignmentOptions.Center);
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 200f;
        return t;
    }

    private void AddFlex(Transform row)
    {
        var go = new GameObject("Flex", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(row, false);
        go.GetComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private Button AddButton(Transform row, string label, float width, TMP_FontAsset font, Action onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(row, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = width;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.22f, 0.24f, 0.32f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var t = CreateText(go.transform, "Label", font, 26, TextAlignmentOptions.Center);
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        t.text = label;

        return btn;
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
