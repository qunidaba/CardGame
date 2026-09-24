using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 单个药水槽：显示药水名/图标，点击使用，悬停显示说明（显示在上方）。
/// </summary>
public class PotionSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public System.Action OnClick;
    public float tooltipDelay = 0.3f;

    private PotionData potion;
    private float hoverTimer;
    private bool hovering;

    public bool IsEmpty => potion == null;

    public void SetPotion(PotionData p)
    {
        potion = p;

        if (nameText != null)
        {
            nameText.text = p != null ? p.name : "";
            nameText.enabled = p != null;
        }
        if (iconImage != null)
        {
            iconImage.color = p != null
                ? new Color(0.30f, 0.42f, 0.55f, 0.95f)
                : new Color(0.20f, 0.20f, 0.25f, 0.60f);
        }
    }

    public void Clear() => SetPotion(null);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (potion != null)
            OnClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        hoverTimer = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        TooltipManager.Instance?.Hide();
    }

    private void Update()
    {
        if (!hovering || potion == null) return;

        hoverTimer += Time.unscaledDeltaTime;
        if (hoverTimer >= tooltipDelay)
        {
            TooltipManager.Instance?.Show(potion.name, potion.description, GetComponent<RectTransform>(), true);
        }
    }
}
