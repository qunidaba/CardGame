using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;

/// <summary>
/// 命格面板：显示主命格、命格值、各花色等级与效果、主动技能。
/// 完全运行时构建 UI。
/// </summary>
public class DestinyPanel : BasePanel
{
    private RunData runData;
    private Action onCloseCb;

    private TextMeshProUGUI mainText;
    private TextMeshProUGUI activeText;
    private TextMeshProUGUI levelText;
    private Transform rows;
    private bool built;

    public void ShowDestiny(RunData data, Action onClose)
    {
        runData = data;
        onCloseCb = onClose;
        EnsureBuilt();
        Refresh();
        Show();
    }

    private void Refresh()
    {
        if (runData == null) return;

        if (runData.HasMainDestiny)
        {
            var suit = (Suit)runData.mainDestinySuit;
            int rank = runData.GetDestinyRank(suit);
            mainText.text = $"主命格：{DestinyInfo.SuitName(suit)}  Lv.{rank}      命运之力 {runData.fatePower}/{RunData.FatePowerMax}";
            activeText.text = $"主动技能【{DestinyInfo.GetActiveName(suit)}】：{DestinyInfo.GetActiveDesc(suit, rank)}";
        }
        else
        {
            mainText.text = "主命格：未确立（第一个到达 Lv.1 的花色将成为主命格）";
            activeText.text = "";
        }

        for (int i = rows.childCount - 1; i >= 0; i--)
            Destroy(rows.GetChild(i).gameObject);

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");
        foreach (Suit s in new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond })
        {
            int rank = runData.GetDestinyRank(s);
            int points = runData.destinyPoints[(int)s];
            var passives = DestinyInfo.GetPassives(s);

            var row = new GameObject("Row", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(rows, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(820, 122);
            row.GetComponent<Image>().color = new Color(0.17f, 0.18f, 0.24f, 1f);

            var t = CreateText(row.transform, "Text", font, 21, TextAlignmentOptions.TopLeft);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(16, 8);
            rt.offsetMax = new Vector2(-16, -8);

            string title = $"{DestinyInfo.SuitSymbol(s)} {DestinyInfo.SuitName(s)}   Lv.{rank}   命格值 {points}";

            // 距离下一级还需要多少命格值
            int nextAt = rank == 0 ? RunData.DestinyLv1At
                       : rank == 1 ? RunData.DestinyLv2At
                       : rank == 2 ? RunData.DestinyLv3At
                       : -1;
            title += nextAt > 0
                ? $"<size=85%><color=#9AB0C8>（距 Lv.{rank + 1} 还需 {Mathf.Max(0, nextAt - points)}）</color></size>"
                : "<size=85%><color=#FFD700>（已满级）</color></size>";

            if (runData.IsMainSuit(s)) title = "<color=#FFD700>★ " + title + "（主命格）</color>";

            string body = "";
            for (int i = 0; i < passives.Length; i++)
            {
                bool unlocked = rank >= i + 1;
                body += unlocked ? passives[i] : $"<color=#666666>{passives[i]}</color>";
                if (i < passives.Length - 1) body += "\n";
            }
            t.text = title + "\n" + body;
        }
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

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

        var card = new GameObject("DestinyCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(880, 820);
        crt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        var title = CreateText(card.transform, "Title", font, 40, TextAlignmentOptions.Center);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0, -20);
        trt.sizeDelta = new Vector2(840, 54);
        title.text = "命格";

        mainText = CreateText(card.transform, "Main", font, 24, TextAlignmentOptions.Center);
        var mrt = mainText.rectTransform;
        mrt.anchorMin = new Vector2(0.5f, 1f);
        mrt.anchorMax = new Vector2(0.5f, 1f);
        mrt.pivot = new Vector2(0.5f, 1f);
        mrt.anchoredPosition = new Vector2(0, -74);
        mrt.sizeDelta = new Vector2(840, 34);
        mainText.color = new Color(1f, 0.85f, 0.4f);

        activeText = CreateText(card.transform, "Active", font, 21, TextAlignmentOptions.Center);
        var art = activeText.rectTransform;
        art.anchorMin = new Vector2(0.5f, 1f);
        art.anchorMax = new Vector2(0.5f, 1f);
        art.pivot = new Vector2(0.5f, 1f);
        art.anchoredPosition = new Vector2(0, -110);
        art.sizeDelta = new Vector2(840, 30);
        activeText.color = new Color(0.7f, 0.9f, 1f);

        // 等级需求（命格值阈值）
        levelText = CreateText(card.transform, "LevelReq", font, 20, TextAlignmentOptions.Center);
        var lrt = levelText.rectTransform;
        lrt.anchorMin = new Vector2(0.5f, 1f);
        lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = new Vector2(0, -140);
        lrt.sizeDelta = new Vector2(840, 28);
        levelText.color = new Color(0.72f, 0.78f, 0.86f);
        levelText.text = $"等级需求：Lv.1 = {RunData.DestinyLv1At} 点 · Lv.2 = {RunData.DestinyLv2At} 点 · Lv.3 = {RunData.DestinyLv3At} 点";

        var container = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
        container.transform.SetParent(card.transform, false);
        rows = container.transform;
        var rrt = container.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 1f);
        rrt.anchorMax = new Vector2(0.5f, 1f);
        rrt.pivot = new Vector2(0.5f, 1f);
        rrt.sizeDelta = new Vector2(840, 520);
        rrt.anchoredPosition = new Vector2(0, -176);
        var vlg = container.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var btn = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        btn.transform.SetParent(card.transform, false);
        var brt = btn.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0, 22);
        brt.sizeDelta = new Vector2(200, 52);
        btn.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.32f, 1f);
        var b = btn.GetComponent<Button>();
        b.targetGraphic = btn.GetComponent<Image>();
        b.onClick.AddListener(() =>
        {
            var cb = onCloseCb;
            onCloseCb = null;
            Hide();
            cb?.Invoke();
        });
        var bl = CreateText(btn.transform, "Label", font, 24, TextAlignmentOptions.Center);
        var blrt = bl.rectTransform;
        blrt.anchorMin = Vector2.zero;
        blrt.anchorMax = Vector2.one;
        blrt.offsetMin = Vector2.zero;
        blrt.offsetMax = Vector2.zero;
        bl.text = "关闭";
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
        return t;
    }
}
