using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;

/// <summary>
/// 牌型效果一览（从暂停菜单打开）：列出全部牌型及其效果。纯代码构建。
/// </summary>
public class HandTypePanel : BasePanel
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

        var card = new GameObject("HandTypeCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(920f, 780f);
        crt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        var title = CreateText(card.transform, "Title", font, 42, TextAlignmentOptions.Center);
        title.text = "牌型效果";
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -28));
        title.rectTransform.sizeDelta = new Vector2(600, 56);

        // 滚动区
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(840f, 600f);
        srt.anchoredPosition = new Vector2(0f, -100f);

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
        ccr.offsetMin = Vector2.zero;
        ccr.offsetMax = Vector2.zero;
        contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var body = contentGo.AddComponent<TextMeshProUGUI>();
        if (font != null) body.font = font;
        body.fontSize = 26;
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

        // 关闭
        var btnGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(card.transform, false);
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0, 22);
        brt.sizeDelta = new Vector2(220, 54);
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
            if (UIManager.Instance != null) UIManager.Instance.Hide<HandTypePanel>();
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
        var sb = new StringBuilder();
        foreach (HandType t in System.Enum.GetValues(typeof(HandType)))
        {
            var effects = HandEffectTable.GetEffects(HandTypeResult.Create(t));

            sb.Append("<b>").Append(HandTypeNames.Full(t)).Append("</b>：");
            if (effects == null || effects.Count == 0)
            {
                sb.Append("无");
            }
            else
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    if (i > 0) sb.Append("，");
                    sb.Append(HandEffectTable.Describe(effects[i].effectType, effects[i].value));
                }
            }
            sb.Append('\n');
        }
        return sb.ToString();
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
