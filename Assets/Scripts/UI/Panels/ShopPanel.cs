using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 商店界面：附魔区（随机/自定义）、遗物区、药水区。运行时构建 UI。
/// </summary>
public class ShopPanel : BasePanel
{
    private ShopSystem shopSystem;
    private ShopInventory inventory;
    private RunData runData;
    private RelicSystem relicSystem;
    private Action onCloseCb;

    private TextMeshProUGUI goldText;
    private Transform content;

    private bool built;

    public void ShowShop(ShopInventory inv, RunData rd, RelicSystem rs, Action onClose)
    {
        inventory = inv;
        runData = rd;
        relicSystem = rs;
        onCloseCb = onClose;
        shopSystem = new ShopSystem();

        EnsureBuilt();
        Refresh();
        Show();
    }

    private void Refresh()
    {
        if (runData == null || inventory == null) return;

        goldText.text = $"金币：{runData.Gold}";

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // ===== 附魔区 =====
        MakeHeader("附魔区");
        foreach (var e in inventory.enchantments)
        {
            var entry = e;
            var ench = ConfigLoader.GetEnchantment(entry.enchantmentId);
            if (ench == null) continue;

            var row = MakeRow();
            MakeText(row, $"{ench.name}：{ench.description}", 19, TextAlignmentOptions.Left, 340);

            var btnRandom = MakeButton(row, entry.sold ? "已售" : $"随机牌 {entry.priceRandom}", 120, () =>
            {
                if (shopSystem.BuyEnchant(entry, runData, false, 0, Suit.Spade, out int rr, out Suit rs))
                {
                    // 随机牌：弹面板告诉玩家附魔到了哪张牌
                    ShowEnchantResult(entry.enchantmentId, rr, rs);
                    Refresh();
                }
            });
            btnRandom.interactable = !entry.sold && runData.Gold >= entry.priceRandom;

            var btnCustom = MakeButton(row, entry.sold ? "已售" : $"指定牌 {entry.priceCustom}", 120, () =>
            {
                // 指定牌：显示全部 52 张牌让玩家挑（和事件选牌一致）
                if (UIManager.Instance == null) return;
                UIManager.Instance.ShowPanel<CardPickerPanel>()?.ShowPicker(
                    $"选择要附魔的牌（{ench.name}）",
                    (rank, suit) =>
                    {
                        if (shopSystem.BuyEnchant(entry, runData, true, rank, suit, out _, out _))
                            Refresh();
                    },
                    () => { });
            });
            btnCustom.interactable = !entry.sold && runData.Gold >= entry.priceCustom;
        }

        // ===== 遗物区（横向，图标优先）=====
        MakeHeader("遗物区");
        var relicBar = MakeItemBar();
        foreach (var e in inventory.relics)
            MakeShopItemSlot(relicBar, e);

        // ===== 药水区（横向，图标优先）=====
        MakeHeader("药水区");
        var potionBar = MakeItemBar();
        foreach (var e in inventory.potions)
            MakeShopItemSlot(potionBar, e);

        // ===== 底部：查看牌组附魔 / 离开商店 =====
        var bottomRow = MakeRow();
        MakeButton(bottomRow, "查看牌组附魔", 190, () =>
        {
            UIManager.Instance?.ShowPanel<CardPickerPanel>()?.ShowViewer("牌组附魔", null);
        });
        MakeButton(bottomRow, "离开商店", 190, () =>
        {
            var cb = onCloseCb;
            onCloseCb = null;
            Hide();
            cb?.Invoke();
        });
    }

    /// <summary>随机牌附魔结果：用事件结算面板展示「哪张牌获得了什么附魔」</summary>
    private void ShowEnchantResult(int enchantmentId, int rank, Suit suit)
    {
        if (UIManager.Instance == null) return;

        var outcome = new EventOutcome();
        outcome.enchantments.Add(new GrantedEnchantment
        {
            cardKey = RunData.GetCardKey(rank, suit),
            cardDisplayName = $"{RankLabel(rank)}{SuitSymbol(suit)}",
            enchantmentId = enchantmentId
        });

        UIManager.Instance.ShowPanel<EventResultPanel>()?.ShowOutcome(outcome, null);
    }

    // ===== 行构建 =====

    // ===== 横向物品栏（遗物 / 药水）=====

    private Transform MakeItemBar()
    {
        var go = new GameObject("ItemBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(content, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(760, 128);
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        return go.transform;
    }

    /// <summary>
    /// 单个商店物品：100×100 的格子（有图标显示图标，没图标把名字居中），
    /// 金币价格显示在格子正下方；悬停显示具体效果。
    /// </summary>
    private void MakeShopItemSlot(Transform parent, ShopEntry e)
    {
        // 整个条目 = 格子 + 下方价格
        var item = new GameObject("Item", typeof(RectTransform));
        item.transform.SetParent(parent, false);
        item.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 128f);

        // ===== 100×100 格子 =====
        var slot = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button));
        slot.transform.SetParent(item.transform, false);
        var srt = slot.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = new Vector2(100f, 100f);

        var img = slot.GetComponent<Image>();
        img.color = e.sold ? new Color(0.13f, 0.13f, 0.16f, 1f) : new Color(0.19f, 0.2f, 0.27f, 1f);

        bool canBuy = !e.sold && runData.Gold >= e.price;
        var btn = slot.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = canBuy;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.33f, 0.44f, 1f);
        colors.pressedColor = new Color(0.15f, 0.16f, 0.22f, 1f);
        colors.disabledColor = e.sold ? new Color(0.13f, 0.13f, 0.16f, 0.9f) : new Color(0.19f, 0.2f, 0.27f, 0.6f);
        btn.colors = colors;

        var entry = e;
        btn.onClick.AddListener(() => OnBuyItem(entry));

        // 图标优先；没有图标就把名字居中显示
        string iconPath = GetEntryIconPath(e);
        Sprite sprite = !string.IsNullOrEmpty(iconPath) ? Resources.Load<Sprite>(iconPath) : null;

        if (sprite != null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slot.transform, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(64f, 64f);
            var iimg = iconGo.GetComponent<Image>();
            iimg.sprite = sprite;
            iimg.preserveAspect = true;
            iimg.raycastTarget = false;
        }
        else
        {
            var nameText = MakeText(slot.transform, e.label, 17f, TextAlignmentOptions.Center, 96f);
            var nrt = nameText.rectTransform;
            nrt.anchorMin = Vector2.zero;
            nrt.anchorMax = Vector2.one;
            nrt.offsetMin = new Vector2(4f, 4f);
            nrt.offsetMax = new Vector2(-4f, -4f);
            nameText.enableWordWrapping = true;
        }

        // ===== 价格（格子正下方）=====
        var priceText = MakeText(item.transform, e.sold ? "已售出" : $"{e.price} 金", 17f, TextAlignmentOptions.Center, 100f);
        var prt = priceText.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(100f, 24f);
        priceText.color = e.sold ? new Color(0.55f, 0.55f, 0.6f) : new Color(1f, 0.85f, 0.4f);

        // 悬停显示具体效果
        var tip = slot.AddComponent<HoverTooltip>();
        tip.Setup(e.label, GetEntryDescription(e) ?? "");
    }

    private void OnBuyItem(ShopEntry e)
    {
        if (e == null || runData == null || e.sold) return;

        if (e.kind == "potion")
        {
            // 药水槽没满直接买；满了让玩家选一个替换掉
            if (runData.PotionIds.Count < RunData.MaxPotions)
            {
                if (shopSystem.BuyPotion(e, runData)) Refresh();
                return;
            }

            var potion = ConfigLoader.GetPotion(e.id);
            if (potion == null || UIManager.Instance == null) return;

            var potionPanel = UIManager.Instance.ShowPanel<ReplacePickerPanel>();
            if (potionPanel == null) return;

            var pEntry = e;
            potionPanel.Show("药水槽已满",
                potion.name, potion.description, "#7FE3A0",
                ReplacePickerPanel.BuildPotionEntries(runData),
                replacedId =>
                {
                    if (shopSystem.BuyPotion(pEntry, runData, replacedId)) Refresh();
                },
                () => { },
                "取消购买");
            return;
        }

        // 遗物：槽位没满直接买；满了让玩家选一个替换掉
        if (runData.RelicIds.Count < RelicSystem.MaxSlots)
        {
            if (shopSystem.BuyRelic(e, runData, relicSystem)) Refresh();
            return;
        }

        var relic = ConfigLoader.GetRelic(e.id);
        if (relic == null || UIManager.Instance == null) return;

        var panel = UIManager.Instance.ShowPanel<ReplacePickerPanel>();
        if (panel == null) return;

        var entry = e;
        panel.Show("遗物槽已满",
            relic.name, relic.description, RarityUtil.ColorHex(relic.rarity),
            ReplacePickerPanel.BuildRelicEntries(runData),
            replacedId =>
            {
                if (shopSystem.BuyRelic(entry, runData, relicSystem, replacedId)) Refresh();
            },
            () => { },
            "取消购买");
    }

    /// <summary>商店条目的效果说明（遗物 / 药水）</summary>
    private static string GetEntryDescription(ShopEntry e)
    {
        if (e == null) return null;
        if (e.kind == "relic") return ConfigLoader.GetRelic(e.id)?.description;
        if (e.kind == "potion") return ConfigLoader.GetPotion(e.id)?.description;
        return null;
    }

    /// <summary>商店条目的图标路径（遗物 iconPath / 药水 icon）</summary>
    private static string GetEntryIconPath(ShopEntry e)
    {
        if (e == null) return null;
        if (e.kind == "relic") return ConfigLoader.GetRelic(e.id)?.iconPath;
        if (e.kind == "potion") return ConfigLoader.GetPotion(e.id)?.icon;
        return null;
    }

    private void MakeHeader(string text)
    {
        var t = MakeText(content, text, 26, TextAlignmentOptions.Left, 700);
        t.color = new Color(1f, 0.85f, 0.4f);
        t.rectTransform.sizeDelta = new Vector2(720, 40);
    }

    private Transform MakeRow()
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(content, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(720, 52);
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        return go.transform;
    }

    private TextMeshProUGUI MakeText(Transform parent, string text, float size, TextAlignmentOptions align, float width)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.text = text;
        t.rectTransform.sizeDelta = new Vector2(width, 40);
        return t;
    }

    private Button MakeButton(Transform parent, string label, float width, Action onClick)
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 46);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        colors.disabledColor = new Color(0.15f, 0.15f, 0.18f, 0.6f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var t = MakeText(go.transform, label, 20, TextAlignmentOptions.Center, width - 8);
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        return btn;
    }

    // ===== 基础 UI =====

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        var overlay = gameObject.GetComponent<Image>();
        if (overlay == null) overlay = gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.8f);
        overlay.raycastTarget = true;

        var rootRt = gameObject.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
        }

        var card = new GameObject("ShopCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(820, 900);
        crt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        var title = MakeText(card.transform, "商店", 42, TextAlignmentOptions.Center, 700);
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0, -20);
        title.rectTransform.sizeDelta = new Vector2(700, 54);

        goldText = MakeText(card.transform, "", 26, TextAlignmentOptions.Center, 700);
        goldText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        goldText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        goldText.rectTransform.pivot = new Vector2(0.5f, 1f);
        goldText.rectTransform.anchoredPosition = new Vector2(0, -74);
        goldText.rectTransform.sizeDelta = new Vector2(700, 34);
        goldText.color = new Color(1f, 0.85f, 0.4f);

        var container = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        container.transform.SetParent(card.transform, false);
        content = container.transform;
        var vrt = container.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.5f, 1f);
        vrt.anchorMax = new Vector2(0.5f, 1f);
        vrt.pivot = new Vector2(0.5f, 1f);
        vrt.sizeDelta = new Vector2(760, 740);
        vrt.anchoredPosition = new Vector2(0, -116);
        var vlg = container.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    private static string SuitSymbol(Suit s)
    {
        switch (s)
        {
            case Suit.Spade: return "♠";
            case Suit.Heart: return "♥";
            case Suit.Club: return "♣";
            case Suit.Diamond: return "♦";
            default: return "?";
        }
    }

    private static string RankLabel(int rank)
    {
        switch (rank)
        {
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            case 14: return "A";
            default: return rank.ToString();
        }
    }
}
