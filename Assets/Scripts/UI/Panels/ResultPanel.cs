using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 结算面板（战斗失败 / 通关胜利）：回顾这一局的信息，可重新开始或返回主菜单。
/// 纯代码构建：会清掉预制体自带的子物体再自建（无论是否走预制体都成立）。
/// </summary>
public class ResultPanel : BasePanel
{
    private bool built;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI infoText;
    private bool isWin;

    public override void Init()
    {
        base.Init();
        EnsureBuilt();
    }

    /// <summary>显示结算结果</summary>
    public void ShowResult(bool win)
    {
        isWin = win;
        EnsureBuilt();
        if (titleText != null)
        {
            titleText.text = win ? "通关胜利！" : "战斗失败";
            titleText.color = win ? new Color(1f, 0.85f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
        }
        if (infoText != null) infoText.text = BuildSummary(win);
        Show();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/simhei SDF");

        // 清掉预制体自带的子物体（隐藏后销毁，避免闪一帧）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

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

        var card = new GameObject("ResultCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(820f, 680f);
        crt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

        titleText = CreateText(card.transform, "Title", font, 48, TextAlignmentOptions.Center);
        titleText.text = "战斗失败";
        Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -34));
        titleText.rectTransform.sizeDelta = new Vector2(700, 64);

        // 一局回顾
        infoText = CreateText(card.transform, "Info", font, 25, TextAlignmentOptions.TopLeft);
        infoText.color = new Color(0.88f, 0.88f, 0.92f);
        infoText.enableWordWrapping = true;
        Anchor(infoText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120));
        infoText.rectTransform.sizeDelta = new Vector2(720, 420);

        // 底部按钮（横向）
        var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttons.transform.SetParent(card.transform, false);
        var brt = buttons.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0, 40);
        brt.sizeDelta = new Vector2(560f, 64f);
        var hlg = buttons.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 28;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        AddButton(buttons.transform, "重新开始", font, () =>
        {
            if (UIManager.Instance != null) UIManager.Instance.Hide<ResultPanel>();
            RunDirector.Instance?.StartNewRun();
        }, true);

        AddButton(buttons.transform, "返回主菜单", font, () =>
        {
            RunDirector.Instance?.ReturnToMainMenu();
        }, false);
    }

    private string BuildSummary(bool win)
    {
        var run = RunDirector.Instance != null ? RunDirector.Instance.RunData : null;
        if (run == null) return "（无对局数据）";

        var sb = new StringBuilder();
        sb.AppendLine($"结果：{(win ? "通关胜利" : "战斗失败")}");
        sb.AppendLine($"章节：第 {run.actId} 章");
        sb.AppendLine($"到达：第 {run.battleIndex} 场战斗");
        sb.AppendLine($"生命：{run.CurrentHp} / {run.MaxHp}");
        sb.AppendLine($"金币：{run.Gold}    种子：{run.seed}");
        sb.AppendLine();

        if (run.destinyPoints != null && run.destinyPoints.Count >= 4)
        {
            sb.AppendLine($"命格：♠{run.destinyPoints[(int)Suit.Spade]}  " +
                          $"♥{run.destinyPoints[(int)Suit.Heart]}  " +
                          $"♣{run.destinyPoints[(int)Suit.Club]}  " +
                          $"♦{run.destinyPoints[(int)Suit.Diamond]}");
        }
        if (run.HasMainDestiny)
            sb.AppendLine($"主命格：{DestinyInfo.SuitName((Suit)run.mainDestinySuit)}（命运之力 {run.fatePower}/{RunData.FatePowerMax}）");

        sb.AppendLine($"遗物（{run.RelicIds.Count}/6）：{JoinRelicNames(run)}");
        sb.AppendLine($"药水（{run.PotionIds.Count}/3）：{JoinPotionNames(run)}");
        sb.AppendLine();
        sb.AppendLine($"统计：战斗胜利 {run.battlesWon} 场 · 总伤害 {run.totalDamageDealt} · 总金币 {run.totalGoldGained}");
        return sb.ToString();
    }

    private static string JoinRelicNames(RunData run)
    {
        if (run.RelicIds.Count == 0) return "无";
        var sb = new StringBuilder();
        for (int i = 0; i < run.RelicIds.Count; i++)
        {
            var r = ConfigLoader.GetRelic(run.RelicIds[i]);
            if (i > 0) sb.Append("、");
            sb.Append(r != null ? r.name : $"#{run.RelicIds[i]}");
        }
        return sb.ToString();
    }

    private static string JoinPotionNames(RunData run)
    {
        if (run.PotionIds.Count == 0) return "无";
        var sb = new StringBuilder();
        for (int i = 0; i < run.PotionIds.Count; i++)
        {
            var p = ConfigLoader.GetPotion(run.PotionIds[i]);
            if (i > 0) sb.Append("、");
            sb.Append(p != null ? p.name : $"#{run.PotionIds[i]}");
        }
        return sb.ToString();
    }

    private void AddButton(Transform parent, string text, TMP_FontAsset font, Action onClick, bool primary)
    {
        var go = new GameObject("Btn_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = primary ? new Color(0.30f, 0.42f, 0.30f, 1f) : new Color(0.22f, 0.24f, 0.32f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = primary ? new Color(0.40f, 0.55f, 0.40f, 1f) : new Color(0.32f, 0.36f, 0.48f, 1f);
        colors.pressedColor = primary ? new Color(0.22f, 0.32f, 0.22f, 1f) : new Color(0.16f, 0.18f, 0.24f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => { AudioManager.Instance?.Play(Sfx.Click); onClick?.Invoke(); });

        var label = CreateText(go.transform, "Label", font, 28, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.text = text;
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
