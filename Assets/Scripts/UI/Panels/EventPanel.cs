using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 事件面板：显示事件标题/描述/选项，右上角可查看牌组附魔。
/// 完全在运行时构建 UI，无需预制体。
/// </summary>
public class EventPanel : BasePanel
{
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI descText;
    private TextMeshProUGUI infoText;
    private Transform optionContainer;
    private Action<int> onOptionChosen;
    private bool built;

    /// <summary>
    /// 显示一个事件。onChosen 返回被点击的选项索引。
    /// </summary>
    public void ShowEvent(EventData ev, Action<int> onChosen)
    {
        onOptionChosen = onChosen;
        EnsureBuilt();

        titleText.text = ev != null ? ev.title : "事件";
        descText.text = ev != null ? ev.description : "";

        // 清空旧选项
        for (int i = optionContainer.childCount - 1; i >= 0; i--)
            Destroy(optionContainer.GetChild(i).gameObject);

        var runData = RunDirector.Instance != null ? RunDirector.Instance.RunData : null;

        if (ev != null)
        {
            int enchantedCount = runData != null ? runData.GetEnchantedCardKeys().Count : 0;

            for (int i = 0; i < ev.options.Count; i++)
            {
                int idx = i;
                var opt = ev.options[i];

                // 多阶段事件：非当前阶段的选项直接不显示
                if (EventSystem.IsOptionHidden(opt, runData)) continue;

                // 需要「清空若干张牌的附魔」但附魔牌数量不够时，禁用该选项
                int needEnchanted = EventSystem.GetRequiredEnchantedCardCount(opt);
                bool enchantedOk = enchantedCount >= needEnchanted;

                // 选牌限制（需拥有指定附魔 / 某稀有度附魔 / 任意附魔）：一张都没有时禁用该选项
                string cardRequire = EventSystem.GetCardRequire(opt);
                bool requiredOk = EventSystem.HasAnyCardMatching(runData, cardRequire);

                // 需要丢弃遗物但没有遗物时，禁用该选项
                bool relicOk = !EventSystem.NeedsRelicPick(opt) || (runData != null && runData.RelicIds.Count > 0);

                // 选项条件（如需要某件遗物）
                bool conditionOk = EventSystem.MeetsOptionCondition(opt, runData);

                // 需要花钱但金币不足时，禁用该选项
                int cost = EventSystem.GetGoldCost(opt);
                bool goldOk = runData == null || runData.Gold >= cost;

                // 选项文本（可能附上预抽到的具体拍品，如拍卖会）
                string display = RunDirector.Instance != null && RunDirector.Instance.Events != null
                    ? RunDirector.Instance.Events.GetOptionDisplayText(opt)
                    : opt.text;

                CreateOptionButton(display, () => OnOptionClicked(idx), enchantedOk && conditionOk && goldOk && requiredOk && relicOk);
            }
        }

        // 生命 / 金币
        if (infoText != null)
            infoText.text = runData != null
                ? $"生命 {runData.CurrentHp}/{runData.MaxHp}\n金币 {runData.Gold}"
                : "";

        Show();
    }

    private void OnOptionClicked(int idx)
    {
        var cb = onOptionChosen;
        onOptionChosen = null;
        cb?.Invoke(idx);
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        // 全屏遮罩（拦截点击）
        var overlay = gameObject.GetComponent<Image>();
        if (overlay == null) overlay = gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.75f);
        overlay.raycastTarget = true;

        var rootRt = gameObject.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
        }

        // 中央卡片
        var card = new GameObject("EventCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(820f, 620f);
        cardRt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        // 标题
        titleText = CreateText(card.transform, "Title", font, 44, TextAlignmentOptions.Center, new Vector2(480, 70));
        var titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -25);

        // 生命 / 金币（左上角）
        infoText = CreateText(card.transform, "Info", font, 24, TextAlignmentOptions.TopLeft, new Vector2(300f, 70f));
        var infoRt = infoText.rectTransform;
        infoRt.anchorMin = new Vector2(0f, 1f);
        infoRt.anchorMax = new Vector2(0f, 1f);
        infoRt.pivot = new Vector2(0f, 1f);
        infoRt.anchoredPosition = new Vector2(25f, -28f);
        infoText.color = new Color(0.95f, 0.9f, 0.7f);

        // 「牌组附魔」按钮（右上角）
        var deckBtnGo = new GameObject("DeckButton", typeof(RectTransform), typeof(Image), typeof(Button));
        deckBtnGo.transform.SetParent(card.transform, false);
        var dbrt = deckBtnGo.GetComponent<RectTransform>();
        dbrt.anchorMin = new Vector2(1f, 1f);
        dbrt.anchorMax = new Vector2(1f, 1f);
        dbrt.pivot = new Vector2(1f, 1f);
        dbrt.anchoredPosition = new Vector2(-25f, -25f);
        dbrt.sizeDelta = new Vector2(170f, 46f);
        var dbimg = deckBtnGo.GetComponent<Image>();
        dbimg.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var dbbtn = deckBtnGo.GetComponent<Button>();
        dbbtn.targetGraphic = dbimg;
        var dbcolors = dbbtn.colors;
        dbcolors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        dbcolors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        dbbtn.colors = dbcolors;
        dbbtn.onClick.AddListener(() =>
        {
            if (UIManager.Instance == null) return;
            var viewer = UIManager.Instance.ShowPanel<CardPickerPanel>();
            viewer?.ShowViewer("牌组附魔", null);
        });
        var dblabel = CreateText(deckBtnGo.transform, "Label", font, 22, TextAlignmentOptions.Center, Vector2.zero);
        var dblrt = dblabel.rectTransform;
        dblrt.anchorMin = Vector2.zero;
        dblrt.anchorMax = Vector2.one;
        dblrt.offsetMin = Vector2.zero;
        dblrt.offsetMax = Vector2.zero;
        dblabel.text = "牌组附魔";

        // 描述
        descText = CreateText(card.transform, "Desc", font, 26, TextAlignmentOptions.TopLeft, new Vector2(720, 140));
        var descRt = descText.rectTransform;
        descRt.anchorMin = new Vector2(0.5f, 1f);
        descRt.anchorMax = new Vector2(0.5f, 1f);
        descRt.pivot = new Vector2(0.5f, 1f);
        descRt.anchoredPosition = new Vector2(0, -105);

        // 选项容器
        var container = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup));
        container.transform.SetParent(card.transform, false);
        optionContainer = container.transform;
        var cRt = container.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0f);
        cRt.anchorMax = new Vector2(0.5f, 0f);
        cRt.pivot = new Vector2(0.5f, 0f);
        cRt.sizeDelta = new Vector2(740, 430);
        cRt.anchoredPosition = new Vector2(0, 30);
        var vlg = container.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 16;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    private void CreateOptionButton(string text, Action onClick, bool interactable = true)
    {
        var go = new GameObject("OptionButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(optionContainer, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(720, 84);

        var img = go.GetComponent<Image>();
        img.color = interactable ? new Color(0.22f, 0.24f, 0.32f, 1f) : new Color(0.16f, 0.16f, 0.2f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = interactable;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        colors.disabledColor = new Color(0.16f, 0.16f, 0.2f, 0.8f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        var label = CreateText(go.transform, "Label", font, 22, TextAlignmentOptions.Center, Vector2.zero);
        var lRt = label.rectTransform;
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = new Vector2(12, 4);
        lRt.offsetMax = new Vector2(-12, -4);
        label.text = text;
        label.enableWordWrapping = true;
        if (!interactable) label.color = new Color(0.5f, 0.5f, 0.55f);
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
        t.rectTransform.sizeDelta = sizeDelta;
        return t;
    }
}
