using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Roguelike;

public class CardUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image bgImage;
    public Image artworkImage;  // 卡牌图案图片
    public TextMeshProUGUI enchantmentText;  // 附魔显示文本
    public Transform enchantmentIconContainer;  // 附魔图标容器（可选）
    public GameObject enchantmentIconPrefab;  // 附魔图标预制体（可选）

    [Header("附魔提示")]
    public float tooltipDelay = 0.3f;
    public int maxNamesOnCard = 2;   // 牌面上最多显示的附魔名数量，超出显示 +N

    public System.Action OnClick;

    private CardData cardData;
    private float hoverTimer;
    private bool isHovering;
    public CardData GetCardData() => cardData;

    public void Init(CardData card)
    {
        cardData = card;

        // 加载卡牌图片
        if (artworkImage != null)
        {
            string resourcePath = GetResourcePath(card);
            Sprite sprite = Resources.Load<Sprite>($"Poker/{resourcePath}");
            if (sprite != null)
            {
                artworkImage.sprite = sprite;
                artworkImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[CardUI] 未找到卡牌图片: Poker/{resourcePath}");
                artworkImage.enabled = false;
            }
        }

        // 显示附魔
        RefreshEnchantmentDisplay();

        // 初始化时默认白色
        bgImage.color = Color.white;
    }

    /// <summary>重新读取牌面图片（花色/点数被临时改变后刷新用）</summary>
    public void RefreshArtwork()
    {
        if (cardData == null || artworkImage == null) return;

        string resourcePath = GetResourcePath(cardData);
        Sprite sprite = Resources.Load<Sprite>($"Poker/{resourcePath}");
        if (sprite != null)
        {
            artworkImage.sprite = sprite;
            artworkImage.enabled = true;
        }
    }

    private string GetResourcePath(CardData card)
    {
        string rankStr = card.EffectiveRank switch
        {
            11 => "J",
            12 => "Q",
            13 => "K",
            14 => "A",
            _ => card.EffectiveRank.ToString()
        };

        string suitLetter = card.EffectiveSuit switch
        {
            Suit.Spade => "S",
            Suit.Heart => "H",
            Suit.Club => "C",
            Suit.Diamond => "D",
            _ => ""
        };

        return $"{rankStr}-{suitLetter}";
    }

    /// <summary>
    /// 刷新附魔显示
    /// </summary>
    public void RefreshEnchantmentDisplay()
    {
        if (cardData == null) return;

        bool polluted = Roguelike.RunDirector.Instance?.RunData?.IsPolluted(cardData.rank, cardData.suit) ?? false;

        // 污染：立绘变暗紫
        if (artworkImage != null)
            artworkImage.color = polluted ? new Color(0.55f, 0.35f, 0.7f) : Color.white;

        // 文本显示附魔名称（污染时显示「污染」，否则最多显示 maxNamesOnCard 个，超出显示 +N）
        if (enchantmentText != null)
        {
            if (polluted)
            {
                enchantmentText.text = "<color=#C060FF>污染</color>";
                enchantmentText.enabled = true;
            }
            else
            {
                var names = cardData.GetEnchantmentNames();
                if (names.Count > 0)
                {
                    int show = Mathf.Min(names.Count, Mathf.Max(1, maxNamesOnCard));
                    string text = string.Join("\n", names.GetRange(0, show));
                    if (names.Count > show)
                        text += $"\n<size=80%>+{names.Count - show}</size>";
                    enchantmentText.text = text;
                    enchantmentText.enabled = true;
                }
                else
                {
                    enchantmentText.text = "";
                    enchantmentText.enabled = false;
                }
            }
        }

        // 图标显示（如果有容器和预制体）；污染时不显示附魔图标
        if (enchantmentIconContainer != null && enchantmentIconPrefab != null)
        {
            // 清空旧图标
            foreach (Transform child in enchantmentIconContainer)
                Destroy(child.gameObject);

            if (!polluted)
            {
                foreach (int enchId in cardData.GetLiveEnchantmentIds())
                {
                    var ench = Roguelike.Data.ConfigLoader.GetEnchantment(enchId);
                    if (ench != null)
                    {
                        var iconObj = Instantiate(enchantmentIconPrefab, enchantmentIconContainer);
                        var iconImage = iconObj.GetComponent<Image>();
                        var nameText = iconObj.GetComponentInChildren<TextMeshProUGUI>();
                        if (iconImage != null)
                        {
                            // 这里可以加载附魔图标 Sprite
                            // iconImage.sprite = Resources.Load<Sprite>($"Enchantments/{ench.name}");
                        }
                        if (nameText != null)
                            nameText.text = ench.name;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 刷新选中状态
    /// </summary>
    public void SetSelected(bool selected)
    {
        bgImage.color = selected ? Color.yellow : Color.white;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        AudioManager.Instance?.Play(Sfx.CardSelect);
        OnClick?.Invoke();
    }

    // ===== 悬停显示附魔详情（显示在牌的上方）=====

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
        if (!isHovering || cardData == null) return;

        hoverTimer += Time.unscaledDeltaTime;
        if (hoverTimer >= tooltipDelay)
        {
            TooltipManager.Instance?.Show(cardData.DisplayName, BuildTooltip(), GetComponent<RectTransform>(), true);
        }
    }

    private string BuildTooltip()
    {
        bool polluted = Roguelike.RunDirector.Instance?.RunData?.IsPolluted(cardData.rank, cardData.suit) ?? false;

        var sb = new System.Text.StringBuilder();
        if (polluted)
            sb.Append("<color=#C060FF>污染</color>\n附魔失效；参与牌型伤害 -1；打出后解除污染");

        var ids = cardData.GetLiveEnchantmentIds();
        if (ids != null)
        {
            foreach (int id in ids)
            {
                var ench = Roguelike.Data.ConfigLoader.GetEnchantment(id);
                if (ench == null) continue;
                if (sb.Length > 0) sb.Append("\n");
                sb.Append($"<color=#FFD700>{ench.name}</color>\n{ench.description}");
            }
        }
        return sb.Length > 0 ? sb.ToString() : "无附魔";
    }
}