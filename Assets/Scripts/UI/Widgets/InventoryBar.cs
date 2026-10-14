using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 只读的「遗物栏 + 药水栏」，布局与战斗面板一致（中心为基准）：
/// 遗物 6 格在 (-900 + i*150, 450)，药水在 (-900 + i*150, 300)，都是 100×100。
/// 槽位背景独立（空槽也可见），图标/名字在子对象上。
/// </summary>
public class InventoryBar : MonoBehaviour
{
    private const float SlotSize = 100f;
    private const float SlotSpacing = 150f;
    private const float StartX = -900f;
    private const float RelicY = 450f;
    private const float PotionY = 300f;

    private static readonly Color RelicSlotBg = new Color(0f, 0f, 0f, 0.58f);       // 同战斗面板遗物槽
    private static readonly Color PotionSlotBg = new Color(0.20f, 0.20f, 0.25f, 0.60f); // 同战斗面板药水槽

    private RelicSlotUI[] relicSlots;
    private readonly List<PotionSlotUI> potionSlots = new List<PotionSlotUI>();
    private TMP_FontAsset font;

    public static InventoryBar Build(Transform parent)
    {
        var go = new GameObject("InventoryBar", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var bar = go.AddComponent<InventoryBar>();
        bar.Build();
        bar.Refresh();
        return bar;
    }

    private void Build()
    {
        font = Res.Load<TMP_FontAsset>("Fonts/simhei SDF");
        relicSlots = new RelicSlotUI[RelicSystem.MaxSlots];
        for (int i = 0; i < relicSlots.Length; i++)
        {
            var icon = MakeSlot($"Relic{i}", RelicY, i, RelicSlotBg);
            var slot = icon.transform.parent.gameObject.AddComponent<RelicSlotUI>();
            slot.iconImage = icon;
            slot.nameText = icon.transform.parent.Find("Name").GetComponent<TextMeshProUGUI>();
            relicSlots[i] = slot;
        }
    }

    /// <summary>创建槽位（返回图标子对象的 Image）</summary>
    private Image MakeSlot(string name, float y, int i, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SlotSize, SlotSize);
        rt.anchoredPosition = new Vector2(StartX + i * SlotSpacing, y);
        go.GetComponent<Image>().color = bg;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var irt = iconGo.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(4, 4);
        irt.offsetMax = new Vector2(-4, -4);
        var icon = iconGo.GetComponent<Image>();
        icon.enabled = false;

        var label = new GameObject("Name", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        var t = label.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = 18;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.richText = true;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        var lrt = t.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(4, 4);
        lrt.offsetMax = new Vector2(-4, -4);
        t.enabled = false;

        return icon;
    }

    /// <summary>按当前 RunData 刷新（拥有遗物 / 携带药水）</summary>
    public void Refresh()
    {
        var run = RunDirector.Instance != null ? RunDirector.Instance.RunData : null;
        if (run == null) return;

        if (relicSlots != null)
            for (int i = 0; i < relicSlots.Length; i++)
            {
                if (relicSlots[i] == null) continue;
                if (i < run.RelicIds.Count)
                {
                    var r = ConfigLoader.GetRelic(run.RelicIds[i]);
                    if (r != null) relicSlots[i].SetRelic(r); else relicSlots[i].Clear();
                }
                else relicSlots[i].Clear();
            }

        int cap = Mathf.Max(1, run.PotionSlotCap);
        if (potionSlots.Count != cap)
        {
            foreach (var s in potionSlots) if (s != null) Destroy(s.gameObject);
            potionSlots.Clear();
            for (int i = 0; i < cap; i++)
            {
                var icon = MakeSlot($"Potion{i}", PotionY, i, PotionSlotBg);
                var slot = icon.transform.parent.gameObject.AddComponent<PotionSlotUI>();
                slot.iconImage = icon;
                slot.nameText = icon.transform.parent.Find("Name").GetComponent<TextMeshProUGUI>();
                potionSlots.Add(slot);
            }
        }

        for (int i = 0; i < potionSlots.Count; i++)
        {
            if (potionSlots[i] == null) continue;
            if (i < run.PotionIds.Count) potionSlots[i].SetPotion(ConfigLoader.GetPotion(run.PotionIds[i]));
            else potionSlots[i].Clear();
        }
    }
}
