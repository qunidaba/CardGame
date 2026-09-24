using UnityEngine;
using UnityEngine.EventSystems;
using Roguelike;

/// <summary>
/// 鼠标悬停时用 TooltipManager 显示「标题 + 说明」（延迟触发，只调用一次）。
/// </summary>
public class HoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string title;
    public string description;
    public float delay = 0.2f;

    private float timer;
    private bool hovering;
    private bool shown;

    public void Setup(string title, string description)
    {
        this.title = title;
        this.description = description;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        shown = false;
        timer = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        shown = false;
        TooltipManager.Instance?.Hide();
    }

    private void OnDisable()
    {
        hovering = false;
        shown = false;
    }

    private void Update()
    {
        if (!hovering || shown) return;

        timer += Time.unscaledDeltaTime;
        if (timer < delay) return;

        shown = true;
        TooltipManager.Instance?.Show(title, description, GetComponent<RectTransform>());
    }
}
