using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Roguelike;

/// <summary>
/// 单个状态效果图标显示：名称取首字，层数+持续，鼠标悬停显示全名与效果说明
/// </summary>
public class StatusEffectIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image iconImage;
    public TextMeshProUGUI countText;      // 层数
    public TextMeshProUGUI durationText;   // 持续回合
    public TextMeshProUGUI nameText;

    [Header("提示")]
    public float tooltipDelay = 0.3f;

    private StatusEffectType currentType;
    private int currentAmount;
    private int currentDuration;
    private Suit currentSuit;
    private bool currentUseSuit;
    private BattleUnit currentOwner;

    private float hoverTimer;
    private bool isHovering;

    public void Setup(StatusEffectType type, int amount, int duration, Sprite icon, Suit suit = Suit.Spade, bool useSuit = false, BattleUnit owner = null)
    {
        currentType = type;
        currentAmount = amount;
        currentDuration = duration;
        currentSuit = suit;
        currentUseSuit = useSuit;
        currentOwner = owner;

        var info = Roguelike.StatusEffectRegistry.Get(type);
        nameText.text = (info != null && info.LabelFunc != null)
            ? info.LabelFunc(amount)
            : GetShortLabel(type, suit, useSuit);
        // 「挑战」层数为 0 时也要显示（表示还没失去生命）；是否显示层数由注册表决定
        bool allowCount = Roguelike.StatusEffectRegistry.Get(type)?.ShowCount ?? true;
        bool showCount = amount > 0 || type == StatusEffectType.Challenge;
        countText.text = (allowCount && showCount) ? amount.ToString() : "";

        if (duration <= 0)
        {
            // 无限持续：不显示回合数
            durationText.text = "";
            durationText.enabled = false;
        }
        else
        {
            durationText.text = duration.ToString();
            durationText.color = Color.white;
            durationText.enabled = true;
        }

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }

    // ===== 悬停提示 =====

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        hoverTimer = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        TooltipManager.Instance?.Hide();
    }

    private void Update()
    {
        if (!isHovering) return;

        hoverTimer += Time.unscaledDeltaTime;
        if (hoverTimer >= tooltipDelay)
        {
            TooltipManager.Instance?.Show(
                GetDisplayName(currentType, currentSuit, currentUseSuit),
                GetDescription(currentType, currentAmount, currentSuit, currentOwner),
                GetComponent<RectTransform>());
        }
    }

    // ===== 文本 =====

    /// <summary>图标上显示的单字：走状态注册表（花色类状态返回花色符号）</summary>
    private string GetShortLabel(StatusEffectType type, Suit suit, bool useSuit)
        => Roguelike.StatusEffectRegistry.Get(type)?.GetShortLabel(suit, useSuit) ?? type.ToString().Substring(0, 1);

    private string GetDisplayName(StatusEffectType type, Suit suit, bool useSuit)
        => Roguelike.StatusEffectRegistry.Get(type)?.GetName(suit, useSuit) ?? type.ToString();

    private string GetDescription(StatusEffectType type, int amount, Suit suit, BattleUnit owner = null)
        => Roguelike.StatusEffectRegistry.Get(type)?.GetDescription(amount, suit, owner) ?? "";

    private static string SuitSymbol(Suit s) => Roguelike.StatusEffectRegistry.SuitSymbol(s);

    private static string SuitName(Suit s) => Roguelike.StatusEffectRegistry.SuitName(s);
}
