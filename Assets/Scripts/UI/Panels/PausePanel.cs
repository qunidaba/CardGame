using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;

/// <summary>
/// 暂停界面（局内按 ESC 打开）：调整设置 / 保存并返回主菜单。
/// 纯代码构建，无需预制体。打开时冻结对局（Time.timeScale = 0）。
/// </summary>
public class PausePanel : BasePanel
{
    private bool built;

    public override void Init()
    {
        base.Init();
        EnsureBuilt();
    }

    // 面板被激活 / 关闭（UIManager 用 SetActive 控制）→ 冻结 / 恢复对局
    private void OnEnable() { Time.timeScale = 0f; }
    private void OnDisable() { Time.timeScale = 1f; }

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

        var card = new GameObject("PauseCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(620f, 520f);
        crt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        var title = CreateText(card.transform, "Title", font, 46, TextAlignmentOptions.Center);
        title.text = "暂停";
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -36));
        title.rectTransform.sizeDelta = new Vector2(500, 64);

        // 按钮（竖排）
        var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        buttons.transform.SetParent(card.transform, false);
        var brt = buttons.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0, -30);
        brt.sizeDelta = new Vector2(420f, 320f);
        var vlg = buttons.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 18;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;

        AddButton(buttons.transform, "继续游戏", font, () =>
        {
            if (UIManager.Instance != null) UIManager.Instance.Hide<PausePanel>();
            else Hide();
        }, true);

        AddButton(buttons.transform, "设置", font, () =>
        {
            UIManager.Instance?.ShowPanel<SettingsPanel>();
        }, false);

        AddButton(buttons.transform, "牌型效果", font, () =>
        {
            UIManager.Instance?.ShowPanel<HandTypePanel>();
        }, false);

        AddButton(buttons.transform, "保存并返回主菜单", font, () =>
        {
            if (UIManager.Instance != null) UIManager.Instance.Hide<PausePanel>();
            RunDirector.Instance?.SaveAndReturnToMainMenu();
        }, false);
    }

    private void AddButton(Transform parent, string text, TMP_FontAsset font, Action onClick, bool primary)
    {
        var go = new GameObject("Btn_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = primary ? new Color(0.30f, 0.42f, 0.30f, 1f) : new Color(0.22f, 0.24f, 0.32f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = primary ? new Color(0.40f, 0.55f, 0.40f, 1f) : new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = primary ? new Color(0.22f, 0.32f, 0.22f, 1f) : new Color(0.16f, 0.18f, 0.24f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => { AudioManager.Instance?.Play(Sfx.Click); onClick?.Invoke(); });

        var label = CreateText(go.transform, "Label", font, 28, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.text = text;
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
