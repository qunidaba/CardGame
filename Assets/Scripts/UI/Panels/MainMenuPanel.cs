using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;

/// <summary>
/// 主菜单：游戏启动后显示的第一个界面。
/// 完全在运行时构建 UI，无需预制体（和 EventPanel 一样）。
/// 目前实现「开始游戏 / 退出游戏」，其余入口后续逐步加入。
/// </summary>
public class MainMenuPanel : BasePanel
{
    private bool built;

    public override void Init()
    {
        base.Init();
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        // 全屏背景（同时拦截点击，挡住下面的东西）
        var bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.09f, 1f);
        bg.raycastTarget = true;

        var rt = gameObject.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // 标题
        var title = CreateText(transform, "Title", font, 72, TextAlignmentOptions.Center, new Vector2(1200, 120));
        title.text = "王 牌 命 途";
        title.color = new Color(0.95f, 0.9f, 0.7f);
        title.fontStyle = FontStyles.Bold;
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -160));

        var subtitle = CreateText(transform, "Subtitle", font, 24, TextAlignmentOptions.Center, new Vector2(1200, 50));
        subtitle.text = "扑克牌型 · 卡牌构筑 Roguelike";
        subtitle.color = new Color(0.6f, 0.6f, 0.68f);
        Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -250));

        // 按钮容器（垂直排列）
        var container = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        container.transform.SetParent(transform, false);
        var crt = container.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(420, 400);
        crt.anchoredPosition = new Vector2(0, -40);
        var vlg = container.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 22;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        CreateButton(container.transform, "开始游戏", new Vector2(400, 74), OnStartGame, font, true);
        if (RunSaveSystem.HasSave())
        {
            // 只有存在存档时才显示「继续游戏」，位置在「开始游戏」下方
            CreateButton(container.transform, "继续游戏", new Vector2(400, 74), OnContinueGame, font, true);
        }
        CreateButton(container.transform, "设置", new Vector2(400, 74), OnOpenSettings, font, false);
        CreateButton(container.transform, "退出游戏", new Vector2(400, 74), OnQuit, font, false);

        // 版本号
        var ver = CreateText(transform, "Version", font, 18, TextAlignmentOptions.BottomRight, new Vector2(400, 30));
        ver.text = "v0.1 prototype";
        ver.color = new Color(0.45f, 0.45f, 0.5f);
        Anchor(ver.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20, 15));
    }

    private void OnStartGame()
    {
        if (UIManager.Instance != null) UIManager.Instance.Destroy<MainMenuPanel>();

        if (RunDirector.Instance == null)
        {
            Debug.LogError("[MainMenu] RunDirector 不存在，无法开始游戏");
            return;
        }

        RunDirector.Instance.StartNewRun();
    }

    private void OnContinueGame()
    {
        if (!RunSaveSystem.HasSave()) return;
        if (UIManager.Instance != null) UIManager.Instance.Destroy<MainMenuPanel>();

        if (RunDirector.Instance == null)
        {
            Debug.LogError("[MainMenu] RunDirector 不存在，无法继续游戏");
            return;
        }

        RunDirector.Instance.ContinueFromSave();
    }

    private void OnOpenSettings()
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowPanel<SettingsPanel>();
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ===== 构建辅助（与 EventPanel 风格一致）=====

    private Button CreateButton(Transform parent, string text, Vector2 size, Action onClick, TMP_FontAsset font, bool primary)
    {
        var go = new GameObject("Btn_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = primary ? new Color(0.30f, 0.42f, 0.30f, 1f) : new Color(0.22f, 0.24f, 0.32f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = primary ? new Color(0.40f, 0.55f, 0.40f, 1f) : new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = primary ? new Color(0.22f, 0.32f, 0.22f, 1f) : new Color(0.16f, 0.18f, 0.24f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var label = CreateText(go.transform, "Label", font, 30, TextAlignmentOptions.Center, Vector2.zero);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        label.text = text;

        return btn;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = sizeDelta;
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
