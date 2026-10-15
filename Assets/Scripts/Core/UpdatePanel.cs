using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 资源更新提示面板：纯代码搭建（不依赖 GameRes 里的预制体/字体）。
/// 在 YooAsset 就绪之前盖在最上层，用于：
///  - 显示"检查更新 / 正在下载 x/y 进度"；
///  - 有可更新时询问玩家「更新 / 跳过」；
///  - 更新完成后显示「进入游戏」。
/// 中文字体用系统字体（Font.CreateDynamicFontFromOSFont），因此不依赖游戏字体资源。
/// </summary>
public class UpdatePanel : MonoBehaviour
{
    private static UpdatePanel _instance;

    private Text _titleText;
    private Text _statusText;
    private Text _detailText;
    private Image _barFill;
    private RectTransform _barBg;
    private Button _yesBtn;
    private Button _noBtn;
    private Text _yesLabel;
    private Text _noLabel;
    private bool _confirming;
    private float _dotsTimer;
    private int _dots;

    /// <summary>玩家选择：true=更新/进入 / false=跳过 / null=尚未选择。</summary>
    public static bool? Choice;

    public static UpdatePanel Show()
    {
        if (_instance != null) return _instance;
        Choice = null;
        var go = new GameObject("[UpdatePanel]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<UpdatePanel>();
        _instance.Build();
        return _instance;
    }

    /// <summary>询问："是否更新" —— 显示 yes/no 两个按钮（并排），等待玩家点击。</summary>
    public static void ShowChoice(string statusText, string yesText, string noText)
    {
        if (_instance == null) return;
        Choice = null;
        _instance._confirming = true;
        _instance._barBg.gameObject.SetActive(false);
        _instance._detailText.text = "";
        _instance._statusText.text = statusText;
        _instance._yesLabel.text = yesText;
        _instance._noLabel.text = noText;
        SetBtnPos(_instance._yesBtn, -150f);
        SetBtnPos(_instance._noBtn, 150f);
        _instance._yesBtn.gameObject.SetActive(true);
        _instance._noBtn.gameObject.SetActive(true);
    }

    /// <summary>更新完成："点击进入" —— 只显示一个按钮（居中）。</summary>
    public static void ShowConfirm(string statusText, string yesText)
    {
        if (_instance == null) return;
        Choice = null;
        _instance._confirming = true;
        _instance._barBg.gameObject.SetActive(false);
        _instance._detailText.text = "";
        _instance._statusText.text = statusText;
        _instance._yesLabel.text = yesText;
        SetBtnPos(_instance._yesBtn, 0f);
        _instance._yesBtn.gameObject.SetActive(true);
        _instance._noBtn.gameObject.SetActive(false);
    }

    private static void SetBtnPos(Button btn, float x)
    {
        if (btn == null) return;
        ((RectTransform)btn.transform).anchoredPosition = new Vector2(x, -140f);
    }

    public static void Hide()
    {
        if (_instance == null) return;
        Destroy(_instance.gameObject);
        _instance = null;
    }

    private void Build()
    {
        var font = GetOsFont();

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = (RectTransform)canvasGo.transform;

        var bg = NewImage("Bg", root, new Color(0.06f, 0.06f, 0.09f, 1f));
        Stretch(bg.rectTransform);

        _titleText = NewText("Title", root, font, 64, TextAnchor.MiddleCenter, "王牌命途");
        SetRect(_titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1400f, 90f));
        _titleText.color = new Color(1f, 0.9f, 0.6f);

        _statusText = NewText("Status", root, font, 34, TextAnchor.MiddleCenter, "正在检查更新…");
        SetRect(_statusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1400f, 60f));

        var barBg = NewImage("BarBg", root, new Color(0.16f, 0.16f, 0.2f, 1f));
        SetRect(barBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(760f, 26f));
        _barBg = barBg.rectTransform;

        _barFill = NewImage("BarFill", barBg.transform, new Color(0.4f, 0.72f, 1f, 1f));
        Stretch(_barFill.rectTransform);
        _barFill.type = Image.Type.Filled;
        _barFill.fillMethod = Image.FillMethod.Horizontal;
        _barFill.fillOrigin = 0;
        _barFill.fillAmount = 0f;

        _detailText = NewText("Detail", root, font, 26, TextAnchor.MiddleCenter, "");
        SetRect(_detailText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(1400f, 40f));
        _detailText.color = new Color(0.8f, 0.8f, 0.85f);

        // 两个按钮：左边 yes（更新/进入），右边 no（跳过）
        _yesBtn = MakeButton(root, font, new Vector2(-150f, -140f), new Vector2(260f, 66f),
            new Color(0.22f, 0.5f, 0.9f, 1f), out _yesLabel);
        _yesBtn.onClick.AddListener(() => { Choice = true; HideButtons(); });

        _noBtn = MakeButton(root, font, new Vector2(150f, -140f), new Vector2(220f, 66f),
            new Color(0.28f, 0.28f, 0.34f, 1f), out _noLabel);
        _noBtn.onClick.AddListener(() => { Choice = false; HideButtons(); });

        HideButtons();
    }

    private void HideButtons()
    {
        if (_yesBtn != null) _yesBtn.gameObject.SetActive(false);
        if (_noBtn != null) _noBtn.gameObject.SetActive(false);
        _confirming = false;
    }

    private void Update()
    {
        if (_statusText == null || _confirming) return;

        if (Res.Downloading)
        {
            _statusText.text = "发现新资源，正在更新，请稍候…";
            _barBg.gameObject.SetActive(true);
            _barFill.fillAmount = Mathf.Clamp01(Res.DownloadProgress);
            _detailText.text = $"{Res.DownloadCurrent} / {Res.DownloadTotal}   " +
                               $"{Mathf.RoundToInt(Res.DownloadProgress * 100f)}%   " +
                               $"{Mb(Res.DownloadCurrentBytes)} / {Mb(Res.DownloadTotalBytes)}";
        }
        else
        {
            _barBg.gameObject.SetActive(false);
            _detailText.text = "";

            _dotsTimer += Time.unscaledDeltaTime;
            if (_dotsTimer >= 0.4f) { _dotsTimer = 0f; _dots = (_dots + 1) % 4; }
            string phase = string.IsNullOrEmpty(Res.Phase) ? "正在准备" : Res.Phase;
            _statusText.text = phase + new string('.', _dots);
        }
    }

    private static string Mb(long bytes) => (bytes / 1048576f).ToString("0.0") + " MB";

    private static Font GetOsFont()
    {
        var f = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "SimSun", "宋体", "Arial" }, 32);
        if (f != null) return f;

        var aria = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (aria != null) return aria;
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // ---- UI helpers ----
    private static Button MakeButton(Transform parent, Font font, Vector2 pos, Vector2 size, Color color, out Text label)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        label = NewText("Label", go.transform, font, 30, TextAnchor.MiddleCenter, "OK");
        Stretch(label.rectTransform);
        return go.GetComponent<Button>();
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = Color.white;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
