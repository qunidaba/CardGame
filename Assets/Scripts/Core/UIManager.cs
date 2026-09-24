using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [System.Serializable]
    public class PanelEntry
    {
        public string panelName;
        public GameObject panelPrefab;
    }
    [Header("Canvas 引用（在 Inspector 里拖入场景中的 Canvas）")]
    public Transform canvasTransform;

    [Header("分辨率自适应（启动时强制设置 CanvasScaler）")]
    public bool forceCanvasScaler = true;                      // 关掉就完全用场景里的 CanvasScaler 设置
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;    // 0=按宽度适配, 1=按高度适配, 0.5=折中

    [Header("面板预制体列表（在 Inspector 里配置）")]
    public List<PanelEntry> panelPrefabs = new List<PanelEntry>();

    [Header("调试作弊面板")]
    public bool enableCheatPanel = true;          // 打包时关掉即可（或删掉这行字段）
    public KeyCode cheatPanelKey = KeyCode.F1;    // 开关作弊面板的快捷键

    private Dictionary<string, GameObject> activePanels = new Dictionary<string, GameObject>();
    private Transform panelRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasTransform == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                canvasTransform = canvas.transform;
        }

        ApplyCanvasScaler();

        // 调试作弊面板的快捷键监听（常驻，不受面板开关影响）
        if (enableCheatPanel && GetComponent<CheatToggle>() == null)
        {
            var toggle = gameObject.AddComponent<CheatToggle>();
            toggle.key = cheatPanelKey;
        }
    }

    /// <summary>指定名字的面板当前是否处于激活状态</summary>
    public bool IsPanelActive(string panelName)
    {
        return activePanels.TryGetValue(panelName, out var panel) && panel != null && panel.activeSelf;
    }

    /// <summary>
    /// 分辨率自适应：把 Canvas 设成「按参考分辨率等比缩放」。
    /// 之前场景里是 Constant Pixel Size，UI 完全不会跟着分辨率缩放。
    /// </summary>
    private void ApplyCanvasScaler()
    {
        if (!forceCanvasScaler || canvasTransform == null) return;

        var scaler = canvasTransform.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasTransform.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = matchWidthOrHeight;

        Debug.Log($"[UIManager] CanvasScaler → ScaleWithScreenSize {referenceResolution.x}x{referenceResolution.y}, match={matchWidthOrHeight}");
    }

    /// <summary>
    /// 显示面板（如果已存在则直接激活，否则实例化）
    /// 使用类型名作为面板名（如 BattlePanel -> "BattlePanel"）
    /// </summary>
    public T ShowPanel<T>() where T : MonoBehaviour
    {
        return ShowPanel<T>(typeof(T).Name);
    }

    /// <summary>
    /// 显示面板（指定面板名）
    /// </summary>
    public T ShowPanel<T>(string panelName) where T : MonoBehaviour
    {
        // 如果已经存在，直接激活（并置于最前，否则会被后开的商店等面板挡住）
        if (activePanels.TryGetValue(panelName, out var existing))
        {
            existing.SetActive(true);
            existing.transform.SetAsLastSibling();
            return existing.GetComponent<T>();
        }

        // 查找预制体
        var entry = panelPrefabs.Find(e => e.panelName == panelName);
        GameObject panel;
        if (entry == null || entry.panelPrefab == null)
        {
            // 回退：运行时创建裸面板（面板自行构建 UI，如 EventPanel）
            Debug.LogWarning($"UIManager: 未配置 '{panelName}' 预制体，使用运行时创建。");
            panel = new GameObject(panelName, typeof(RectTransform));
            if (canvasTransform != null)
                panel.transform.SetParent(canvasTransform, false);
            panel.AddComponent<T>();
        }
        else
        {
            panel = Instantiate(entry.panelPrefab, canvasTransform);
            panel.name = panelName;
        }

        activePanels[panelName] = panel;
        panel.transform.SetAsLastSibling();   // 新建的面板也置于最前

        // 调用 Init（子类可重写来执行初始化逻辑）
        var basePanel = panel.GetComponent<BasePanel>();
        basePanel?.Init();

        return panel.GetComponent<T>();
    }

    /// <summary>
    /// 获取指定类型的面板（不创建，仅返回已存在的面板）
    /// </summary>
    public T GetPanel<T>() where T : MonoBehaviour
    {
        foreach (var kvp in activePanels)
        {
            if (kvp.Value != null)
            {
                var component = kvp.Value.GetComponent<T>();
                if (component != null)
                    return component;
            }
        }
        return null;
    }

    /// <summary>
    /// 隐藏指定类型的面板
    /// </summary>
    public void Hide<T>() where T : MonoBehaviour
    {
        foreach (var kvp in activePanels)
        {
            if (kvp.Value != null)
            {
                var component = kvp.Value.GetComponent<T>();
                if (component != null)
                {
                    kvp.Value.SetActive(false);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 隐藏面板（按名称）
    /// </summary>
    public void HidePanel(string panelName)
    {
        if (activePanels.TryGetValue(panelName, out var panel))
        {
            panel.SetActive(false);
        }
    }

    /// <summary>
    /// 销毁指定类型的面板
    /// </summary>
    public void Destroy<T>() where T : MonoBehaviour
    {
        foreach (var kvp in activePanels)
        {
            if (kvp.Value != null)
            {
                var component = kvp.Value.GetComponent<T>();
                if (component != null)
                {
                    Destroy(kvp.Value);
                    activePanels.Remove(kvp.Key);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 销毁面板（按名称）
    /// </summary>
    public void DestroyPanel(string panelName)
    {
        if (activePanels.TryGetValue(panelName, out var panel))
        {
            Destroy(panel);
            activePanels.Remove(panelName);
        }
    }

    /// <summary>
    /// 隐藏所有面板
    /// </summary>
    public void HideAll()
    {
        foreach (var kvp in activePanels)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(false);
        }
    }

    /// <summary>
    /// 销毁所有面板
    /// </summary>
    public void DestroyAllPanels()
    {
        foreach (var kvp in activePanels)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        activePanels.Clear();
    }
}
