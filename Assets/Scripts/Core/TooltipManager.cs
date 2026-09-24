using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Roguelike
{
    /// <summary>
    /// 全局提示框管理器：单例，在目标UI下方显示遗物/卡牌描述
    /// 运行时从预制体实例化，避免场景引用丢失
    /// </summary>
    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [Header("预制体引用（从 Project 窗口拖）")]
        public GameObject tooltipPanelPrefab;  // TooltipPanel 预制体根对象
        public Canvas canvas;                  // 场景里的主 Canvas

        [Header("设置")]
        public Vector2 offset = new Vector2(0, -10);  // 目标下方偏移

        private GameObject tooltipPanelInstance;
        private RectTransform panelRect;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI descriptionText;
        private RectTransform targetRect;
        private bool followTarget = false;
        private bool showAbove = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 运行时实例化 Tooltip 到 Canvas 下
            if (tooltipPanelPrefab != null && canvas != null)
            {
                tooltipPanelInstance = Instantiate(tooltipPanelPrefab, canvas.transform);
                tooltipPanelInstance.name = "TooltipPanel_Runtime";
                
                panelRect = tooltipPanelInstance.GetComponent<RectTransform>();
                titleText = tooltipPanelInstance.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
                descriptionText = tooltipPanelInstance.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();

                if (titleText == null)
                    Debug.LogWarning("[TooltipManager] 找不到 TitleText 子物体");
                if (descriptionText == null)
                    Debug.LogWarning("[TooltipManager] 找不到 DescriptionText 子物体");
                if (panelRect == null)
                    Debug.LogWarning("[TooltipManager] 预制体根对象缺少 RectTransform");
                else
                {
                    // 关键：anchor 设为正中心，pivot 设为上中心
                    // 这样 anchoredPosition 就是相对于画布中心的偏移
                    panelRect.pivot = new Vector2(0.5f, 1f);
                    panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                }
            }
            else
            {
                Debug.LogWarning("[TooltipManager] 未赋值 tooltipPanelPrefab 或 canvas");
            }

            Hide();
        }

        public void Show(string title, string description, RectTransform target, bool above = false)
        {
            if (tooltipPanelInstance == null || target == null || titleText == null || descriptionText == null) return;

            titleText.text = title;
            descriptionText.text = description;

            targetRect = target;
            followTarget = true;
            showAbove = above;

            // 置顶：移到同级最后，避免被其它面板遮挡
            tooltipPanelInstance.transform.SetAsLastSibling();

            ResizePanelToContent();
            UpdatePosition();
            tooltipPanelInstance.SetActive(true);
        }

        public void Hide()
        {
            followTarget = false;
            targetRect = null;
            if (tooltipPanelInstance != null)
                tooltipPanelInstance.SetActive(false);
        }

        private void LateUpdate()
        {
            if (followTarget && targetRect != null)
            {
                UpdatePosition();
            }
        }

        /// <summary>根据标题/描述文字内容自适应面板高度，保证文字不超出面板</summary>
        private void ResizePanelToContent()
        {
            if (panelRect == null || titleText == null || descriptionText == null) return;

            float width = panelRect.sizeDelta.x;
            if (width < 1f) width = 300f;
            float pad = 14f;
            float spacing = 6f;
            float contentWidth = width - pad * 2f;

            titleText.enableWordWrapping = false;
            descriptionText.enableWordWrapping = true;

            var titleRt = titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(contentWidth, 30f);
            titleText.ForceMeshUpdate();
            float titleH = Mathf.Max(30f, titleText.preferredHeight);

            var descRt = descriptionText.rectTransform;
            descRt.anchorMin = new Vector2(0.5f, 1f);
            descRt.anchorMax = new Vector2(0.5f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.sizeDelta = new Vector2(contentWidth, 20f);
            descriptionText.ForceMeshUpdate();
            float descH = Mathf.Max(20f, descriptionText.preferredHeight);

            float panelH = pad + titleH + spacing + descH + pad;
            panelRect.sizeDelta = new Vector2(width, panelH);

            titleRt.anchoredPosition = new Vector2(0f, -pad);
            descRt.anchoredPosition = new Vector2(0f, -pad - titleH - spacing);
        }

        private void UpdatePosition()
        {
            if (targetRect == null || canvas == null || panelRect == null) return;

            // 强制刷新布局，确保 rect.width/height 正确
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            // 获取目标在屏幕上的位置（目标中心点）
            Vector2 screenPos;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                screenPos = RectTransformUtility.WorldToScreenPoint(null, targetRect.position);
            }
            else
            {
                screenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, targetRect.position);
            }

            // 目标高度
            float targetHeight = targetRect.rect.height;

            // 根据 showAbove 决定 pivot 与锚点（above=true 时显示在目标上方，向上展开）
            Vector2 tooltipPivotScreen;
            if (showAbove)
            {
                panelRect.pivot = new Vector2(0.5f, 0f); // 底部中心
                Vector2 targetTopScreen = screenPos + new Vector2(0, targetHeight * 0.5f);
                tooltipPivotScreen = targetTopScreen + new Vector2(offset.x, Mathf.Abs(offset.y));
            }
            else
            {
                panelRect.pivot = new Vector2(0.5f, 1f); // 顶部中心
                Vector2 targetBottomScreen = screenPos - new Vector2(0, targetHeight * 0.5f);
                tooltipPivotScreen = targetBottomScreen + offset;
            }

            // 转换为 Canvas 本地坐标
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                tooltipPivotScreen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out localPos
            );

            // 边界检查
            var canvasRect = canvas.transform as RectTransform;
            float halfWidth = panelRect.rect.width * 0.5f;
            float tooltipHeight = panelRect.rect.height;

            localPos.x = Mathf.Clamp(localPos.x, -canvasRect.rect.width * 0.5f + halfWidth, canvasRect.rect.width * 0.5f - halfWidth);
            if (showAbove)
                localPos.y = Mathf.Clamp(localPos.y, -canvasRect.rect.height * 0.5f, canvasRect.rect.height * 0.5f - tooltipHeight);
            else
                localPos.y = Mathf.Clamp(localPos.y, -canvasRect.rect.height * 0.5f + tooltipHeight, canvasRect.rect.height * 0.5f);

            panelRect.anchoredPosition = localPos;
        }
    }
}