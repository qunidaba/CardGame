using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 奖励面板：战斗胜利后显示遗物 + 附魔三选一（同时显示）
/// </summary>
public class RewardPanel : BasePanel
{
    [Header("奖励容器")]
    public Transform rewardContainer;
    public GameObject rewardButtonPrefab;      // 附魔选项按钮预制体
    public GameObject relicRewardPrefab;       // 遗物奖励预制体（可选）

    [Header("标题/金币")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI goldText;

    private System.Action<CombatReward> onRewardConfirmed;
    private CombatReward currentReward;
    private List<EnchantmentRewardOption> selectedEnchantments = new List<EnchantmentRewardOption>();
    private int enchantmentSelectionCount = 0;
    private int maxEnchantmentSelections = 1;

    // 自适应高度参数
    private const float ContentWidth = 900f;
    private const float TopAreaHeight = 104f;   // 标题 / 金币区域高度
    private const float BottomPadding = 36f;
    private const float MinPanelHeight = 500f;
    private const float FallbackRowHeight = 150f;
    private const float RowSpacing = 22f;       // 内容行之间的间距（含「选择一个附魔:」与卡牌之间）

    public override void Init()
    {
        base.Init();

        // 标题 / 金币改成顶部对齐，这样面板高度变化时它们跟着顶部走
        SetTopAnchored(titleText != null ? titleText.rectTransform : null, new Vector2(0f, -26f));
        SetTopAnchored(goldText != null ? goldText.rectTransform : null, new Vector2(350f, -30f));
    }

    private static void SetTopAnchored(RectTransform rt, Vector2 pos)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
        rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
    }

    /// <summary>
    /// 显示战斗奖励（遗物 + 附魔三选一，同时显示）
    /// </summary>
    public void ShowCombatReward(CombatReward reward, System.Action<CombatReward> callback)
    {
        currentReward = reward;
        onRewardConfirmed = callback;
        selectedEnchantments.Clear();

        titleText.text = "战斗胜利!";
        goldText.text = $"获得金币: <color=#FFD700>{reward.gold}</color>";

        // 清空旧按钮：先脱离父级再销毁，否则 Destroy 延迟到帧末，
        // 本次高度统计会把上一批还没销毁的子物体也算进去（面板会越来越长）
        for (int i = rewardContainer.childCount - 1; i >= 0; i--)
        {
            var child = rewardContainer.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        // 1. 如果有遗物，显示遗物（只读展示，不可选）
        if (reward.relicId > 0)
        {
            ShowRelicDisplay(reward.relicId);
        }

        // 1.5 如果有药水，显示药水（只读展示，不可选）
        if (reward.potionId > 0)
        {
            ShowPotionDisplay(reward.potionId);
        }

        // 2. 显示附魔三选一（可选）
        if (reward.enchantmentOptions.Count > 0)
        {
            ShowEnchantmentOptions(reward.enchantmentOptions);
        }

        // 3. 按内容量调整面板高度
        UpdatePanelHeight();

        Show();
    }

    /// <summary>按奖励内容量调整面板高度（遗物 / 药水 / 附魔 各自占用不同高度）</summary>
    private void UpdatePanelHeight()
    {
        var box = GetComponent<RectTransform>();
        var containerRt = rewardContainer as RectTransform;
        if (box == null || containerRt == null) return;

        // 内容容器：顶部对齐、固定宽度、高度由内容决定
        containerRt.anchorMin = new Vector2(0.5f, 1f);
        containerRt.anchorMax = new Vector2(0.5f, 1f);
        containerRt.pivot = new Vector2(0.5f, 1f);
        containerRt.anchoredPosition = new Vector2(0f, -TopAreaHeight);
        containerRt.sizeDelta = new Vector2(ContentWidth, 0f);

        var vlg = rewardContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childForceExpandHeight = false;
            vlg.spacing = RowSpacing;   // 预制体默认 50，标题与卡牌之间空隙太大
        }

        float contentH = MeasureContentHeight(vlg != null ? vlg.spacing : 50f);
        float totalH = Mathf.Max(MinPanelHeight, TopAreaHeight + contentH + BottomPadding);

        box.sizeDelta = new Vector2(box.sizeDelta.x, totalH);
    }

    /// <summary>
    /// 附魔选项行的高度：取按钮本体高度与「牌面下方附魔说明」的垂直占用范围的较大值，
    /// 否则说明文字会跑到框外（RewardButton 的 EnchantmentText 比按钮本身高）。
    /// </summary>
    private float GetRewardRowHeight()
    {
        float rowHeight = 200f;
        if (rewardButtonPrefab == null) return rowHeight;

        var prt = rewardButtonPrefab.transform as RectTransform;
        if (prt != null && prt.sizeDelta.y > 0f) rowHeight = prt.sizeDelta.y;

        var et = rewardButtonPrefab.transform.Find("EnchantmentText") as RectTransform;
        if (et != null)
        {
            float bottom = et.anchoredPosition.y - et.pivot.y * et.sizeDelta.y;
            float top = et.anchoredPosition.y + (1f - et.pivot.y) * et.sizeDelta.y;

            float extent = Mathf.Max(top, rowHeight * 0.5f) - Mathf.Min(bottom, -rowHeight * 0.5f);
            rowHeight = Mathf.Max(rowHeight, extent);
        }

        return rowHeight;
    }

    /// <summary>累加内容区子物体的高度（含间距）</summary>
    private float MeasureContentHeight(float spacing)
    {
        float h = 0f;
        int n = 0;

        foreach (Transform child in rewardContainer)
        {
            var rt = child as RectTransform;
            if (rt == null) continue;

            float ch = rt.sizeDelta.y;
            if (ch <= 0f) ch = rt.rect.height;
            if (ch <= 0f) ch = FallbackRowHeight;

            h += ch;
            n++;
        }

        if (n > 1) h += (n - 1) * spacing;
        return h;
    }

    private void ShowRelicDisplay(int relicId)
    {
        var relic = ConfigLoader.GetRelic(relicId);
        if (relic == null) return;

        GameObject prefab = relicRewardPrefab != null ? relicRewardPrefab : rewardButtonPrefab;
        var btnObj = Instantiate(prefab, rewardContainer);

        // 设置遗物图标
        var iconImage = btnObj.transform.Find("IconImage")?.GetComponent<Image>();
        if (iconImage != null && !string.IsNullOrEmpty(relic.iconPath))
        {
            var sprite = Resources.Load<Sprite>(relic.iconPath);
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        // 稀有度颜色（普通/稀有/史诗）
        string color = RarityUtil.ColorHex(relic.rarity);

        // 设置遗物名字（稀有度颜色）
        var nameText = btnObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = $"<color={color}>{relic.name}</color>";
        }

        // 设置遗物描述
        var descText = btnObj.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        if (descText != null)
        {
            descText.text = relic.description;
        }

        // 移除按钮组件，只做展示（或禁用交互）
        var btn = btnObj.GetComponent<Button>();
        if (btn != null) Destroy(btn);
    }

    /// <summary>药水奖励展示（只读，不可选）</summary>
    private void ShowPotionDisplay(int potionId)
    {
        var potion = ConfigLoader.GetPotion(potionId);
        if (potion == null) return;

        GameObject prefab = relicRewardPrefab != null ? relicRewardPrefab : rewardButtonPrefab;
        var btnObj = Instantiate(prefab, rewardContainer);

        var iconImage = btnObj.transform.Find("IconImage")?.GetComponent<Image>();
        if (iconImage != null)
        {
            Sprite sprite = !string.IsNullOrEmpty(potion.icon) ? Resources.Load<Sprite>(potion.icon) : null;
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        var nameText = btnObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = $"<color=#7FE3A0>{potion.name}</color>";

        var descText = btnObj.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        if (descText != null)
            descText.text = potion.description;

        var btn = btnObj.GetComponent<Button>();
        if (btn != null) Destroy(btn);
    }

    private void ShowEnchantmentOptions(List<EnchantmentRewardOption> options)    {
        maxEnchantmentSelections = 1;
        enchantmentSelectionCount = 0;

        // 添加分隔标题
        var sepObj = new GameObject("EnchantmentHeader", typeof(RectTransform));
        sepObj.transform.SetParent(rewardContainer, false);
        sepObj.GetComponent<RectTransform>().sizeDelta = new Vector2(ContentWidth, 32f);
        var sepText = sepObj.AddComponent<TextMeshProUGUI>();
        sepText.font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        sepText.text = "选择一个附魔:";
        sepText.fontSize = 28;
        sepText.color = Color.white;
        sepText.alignment = TextAlignmentOptions.Center;
        sepText.margin = new Vector4(0, 10, 0, 5);

        // 创建横向容器放三个附魔按钮
        var horizObj = new GameObject("EnchantmentHorizontalContainer", typeof(RectTransform));
        horizObj.transform.SetParent(rewardContainer, false);
        var hRt = horizObj.GetComponent<RectTransform>();
        float rowHeight = GetRewardRowHeight();
        hRt.sizeDelta = new Vector2(ContentWidth, rowHeight);
        var hLayout = horizObj.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 250;
        // 顶部对齐：按钮贴行顶，牌面下方的附魔说明才能完整落在行高内
        hLayout.childAlignment = TextAnchor.UpperCenter;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.childForceExpandHeight = true;
        var fitter = horizObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (var opt in options)
        {
            var btnObj = Instantiate(rewardButtonPrefab, horizObj.transform);
            var btn = btnObj.GetComponent<Button>();

            // 设置卡牌图片
            var artworkImage = btnObj.transform.Find("ArtworkImage")?.GetComponent<Image>();
            if (artworkImage != null)
            {
                var card = ParseCardKey(opt.cardKey);
                if (card.HasValue)
                {
                    string resourcePath = GetResourcePath(card.Value.rank, card.Value.suit);
                    Sprite sprite = Resources.Load<Sprite>($"Poker/{resourcePath}");
                    if (sprite != null)
                    {
                        artworkImage.sprite = sprite;
                        artworkImage.enabled = true;
                    }
                }
            }

            // 牌面上叠一条「已有附魔」（和牌堆/弃牌堆预览一致）
            AddExistingEnchantmentStrip(btnObj, artworkImage, opt.cardKey);

            // 设置新附魔信息
            var ench = ConfigLoader.GetEnchantment(opt.enchantmentId);
            var enchantmentText = btnObj.transform.Find("EnchantmentText")?.GetComponent<TextMeshProUGUI>();
            if (enchantmentText != null && ench != null)
            {
                enchantmentText.text = $"<color=#FFD700>{ench.name}</color>\n<size=70%>{ench.description}</size>";
            }

            var optData = opt;
            btn.onClick.AddListener(() => OnEnchantmentSelected(optData));
        }
    }

    private void OnEnchantmentSelected(EnchantmentRewardOption option)
    {
        selectedEnchantments.Add(option);
        enchantmentSelectionCount++;

        // 禁用所有附魔按钮，显示已选标记
        foreach (Transform child in rewardContainer)
        {
            var btn = child.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = false;
                // 可选：改变颜色表示已选
                var colors = btn.colors;
                colors.normalColor = Color.gray;
                btn.colors = colors;
            }
        }

        if (enchantmentSelectionCount >= maxEnchantmentSelections)
        {
            ConfirmReward();
        }
    }

    private void ConfirmReward()
    {
        // 将选中的附魔加入奖励
        currentReward.enchantmentOptions = selectedEnchantments;
        Hide();
        onRewardConfirmed?.Invoke(currentReward);
    }

    /// <summary>在牌面上叠一条「已有附魔」名条（和牌堆预览一致）</summary>
    private void AddExistingEnchantmentStrip(GameObject btnObj, Image artworkImage, string cardKey)
    {
        var existing = GetCardEnchantmentNames(cardKey);
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var parent = artworkImage != null ? artworkImage.transform : btnObj.transform;

        var strip = new GameObject("ExistingEnchantStrip", typeof(RectTransform), typeof(Image));
        strip.transform.SetParent(parent, false);
        var srt = strip.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.sizeDelta = new Vector2(0f, existing.Count > 2 ? 50f : 34f);
        srt.anchoredPosition = Vector2.zero;
        var simg = strip.GetComponent<Image>();
        simg.color = new Color(0f, 0f, 0f, 0.72f);
        simg.raycastTarget = false;

        string text = existing.Count > 0
            ? string.Join("\n", existing.GetRange(0, Mathf.Min(2, existing.Count)))
            : "无附魔";
        if (existing.Count > 2) text += $"\n+{existing.Count - 2}";

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(strip.transform, false);
        var t = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = 16;
        t.alignment = TextAlignmentOptions.Center;
        t.color = existing.Count > 0 ? new Color(1f, 0.92f, 0.55f) : new Color(0.62f, 0.62f, 0.68f);
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.text = text;
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(2f, 0f);
        trt.offsetMax = new Vector2(-2f, 0f);
    }

    /// <summary>目标牌当前已有的附魔名（含本场临时附魔，临时加「（临）」）</summary>
    private static List<string> GetCardEnchantmentNames(string cardKey)
    {
        var names = new List<string>();
        var runData = RunDirector.Instance?.RunData;
        if (runData == null || string.IsNullOrEmpty(cardKey)) return names;

        var parts = cardKey.Split('_');
        if (parts.Length != 2) return names;
        if (!System.Enum.TryParse(parts[0], out Suit suit)) return names;
        if (!int.TryParse(parts[1], out int rank)) return names;

        var temp = runData.GetTempEnchantments(rank, suit);
        foreach (var id in runData.GetCardEnchantments(rank, suit))
        {
            var e = ConfigLoader.GetEnchantment(id);
            if (e == null) continue;
            names.Add((temp != null && temp.Contains(id)) ? $"{e.name}（临）" : e.name);
        }
        return names;
    }

    private (int rank, Suit suit)? ParseCardKey(string cardKey)
    {
        var parts = cardKey.Split('_');
        if (parts.Length == 2 && 
            System.Enum.TryParse(parts[0], out Suit suit) && 
            int.TryParse(parts[1], out int rank))
        {
            return (rank, suit);
        }
        return null;
    }

    private string GetResourcePath(int rank, Suit suit)
    {
        string rankStr = rank switch
        {
            11 => "J",
            12 => "Q",
            13 => "K",
            14 => "A",
            _ => rank.ToString()
        };

        string suitLetter = suit switch
        {
            Suit.Spade => "S",
            Suit.Heart => "H",
            Suit.Club => "C",
            Suit.Diamond => "D",
            _ => ""
        };

        return $"{rankStr}-{suitLetter}";
    }
}