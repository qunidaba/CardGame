using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩法说明面板：一页可滚动的规则说明。纯代码构建，无需预制体。
/// </summary>
public class HelpPanel : BasePanel
{
    private bool built;

    public override void Init()
    {
        base.Init();
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        var overlay = gameObject.GetComponent<Image>();
        if (overlay == null) overlay = gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.85f);
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
        var card = new GameObject("HelpCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1000f, 820f);
        crt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        var title = CreateText(card.transform, "Title", font, 44, TextAlignmentOptions.Center);
        title.text = "玩法说明";
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30));
        title.rectTransform.sizeDelta = new Vector2(600, 60);

        // 滚动区
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(900f, 640f);
        srt.anchoredPosition = new Vector2(0f, -110f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.004f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport.transform, false);
        var ccr = contentGo.GetComponent<RectTransform>();
        ccr.anchorMin = new Vector2(0f, 1f);
        ccr.anchorMax = new Vector2(1f, 1f);
        ccr.pivot = new Vector2(0.5f, 1f);
        ccr.offsetMin = new Vector2(0, 0);
        ccr.offsetMax = new Vector2(0, 0);
        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var body = contentGo.AddComponent<TextMeshProUGUI>();
        if (font != null) body.font = font;
        body.fontSize = 25;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.color = new Color(0.9f, 0.9f, 0.94f);
        body.enableWordWrapping = true;
        body.richText = true;
        body.margin = new Vector4(12, 8, 12, 8);
        body.text = BuildText();

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = vrt;
        scroll.content = ccr;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        // 返回
        var btnGo = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(card.transform, false);
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0, 24);
        brt.sizeDelta = new Vector2(220, 56);

        var bimg = btnGo.GetComponent<Image>();
        bimg.color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = bimg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            if (UIManager.Instance != null) UIManager.Instance.Hide<HelpPanel>();
            else Hide();
        });

        var blabel = CreateText(btnGo.transform, "Label", font, 26, TextAlignmentOptions.Center);
        blabel.rectTransform.anchorMin = Vector2.zero;
        blabel.rectTransform.anchorMax = Vector2.one;
        blabel.rectTransform.offsetMin = Vector2.zero;
        blabel.rectTransform.offsetMax = Vector2.zero;
        blabel.text = "返回";
    }

    private static string BuildText()
    {
        return
            "<b><size=30>目标</size></b>\n" +
            "用扑克牌组成牌型击败敌人，一路打过若干场战斗与章节 Boss。\n\n" +

            "<b><size=30>回合流程</size></b>\n" +
            "· 回合开始：护盾清零 → 结算持续伤害/回复 → 抽牌补至 10 张（手牌上限 15）。\n" +
            "· 出牌：选若干张手牌凑成有效牌型后打出；只要手牌允许，一回合可多次出牌。\n" +
            "· 弃牌重抽：每回合一次，丢弃选中的牌并抽取等量新牌。\n" +
            "· 结束回合后敌人依次行动。\n\n" +

            "<b><size=30>牌型效果</size></b>\n" +
            "一对：造成 6 伤害\n" +
            "两连对：14 伤害 + 5 防御\n" +
            "三条：9 伤害 + 4 防御\n" +
            "顺子（3/4/5 张）：8/11/14 伤害，额外抽 1/2/3，防御 0/3/4\n" +
            "同花（3/4/5 张）：12/15/18 伤害，防御 2/4/6\n" +
            "葫芦：13 伤害 + 5 防御\n" +
            "四条：20 伤害 + 回复 3 + 6 防御 + 抽 2\n" +
            "同花顺（3/4/5 张）：12/15/20 伤害，抽 2/3/4，防御 4/5/6\n\n" +

            "<b><size=30>防御</size></b>\n" +
            "防御只在本回合抵挡伤害，回合结束时清零。\n\n" +

            "<b><size=30>命格</size></b>\n" +
            "· 打出某花色的牌会累积该花色命格值；达到 2 / 5 / 10 点分别解锁该花色 Lv.1 / Lv.2 / Lv.3 被动。\n" +
            "· 第一个达到 Lv.1 的花色成为你的「主命格」。\n" +
            "· 打出主命格花色的每张牌 +1 命运之力（上限 20）；攒满后可释放主命格的主动技。\n\n" +

            "<b><size=30>花色命运</size></b>\n" +
            "每场战斗按本场主导花色触发对应事件（黑桃＝危险、红桃＝生命、梅花＝成长、方块＝财富），满足条件还可能刷出商店。\n\n" +

            "<b><size=30>附魔 / 遗物 / 药水</size></b>\n" +
            "· 附魔：给某张牌附加特殊效果，来自战斗奖励、事件或商店。\n" +
            "· 遗物：全局被动，最多持有 6 件。\n" +
            "· 药水：一次性道具，最多持有 3 瓶。\n";
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
    }
}
