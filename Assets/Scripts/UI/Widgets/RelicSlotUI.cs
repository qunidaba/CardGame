using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 单个遗物槽位UI：固定位置，可显示遗物或空状态，支持鼠标悬停提示
    /// </summary>
    public class RelicSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI 引用")]
        public Image iconImage;
        public TextMeshProUGUI nameText;     // 遗物名字（无图标时显示）
        public GameObject emptyState;        // 空槽位显示对象（可选）
        public GameObject filledState;       // 有遗物时显示对象（可选）

        [Header("提示设置")]
        public float tooltipDelay = 0.2f;    // 延迟显示时间

        private RelicData currentRelic;
        private float hoverTimer;
        private bool isHovering;

        public void SetRelic(RelicData data)
        {
            currentRelic = data;
            if (iconImage != null)
            {
                if (!string.IsNullOrEmpty(data.iconPath))
                {
                    var sprite = Resources.Load<Sprite>(data.iconPath);
                    iconImage.sprite = sprite;
                    iconImage.enabled = sprite != null;
                    if (nameText != null) nameText.enabled = false; // 有图标时隐藏名字
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.enabled = false;
                    if (nameText != null)
                    {
                        nameText.text = data.name;
                        nameText.enabled = true; // 无图标时显示名字
                    }
                }
            }
            else if (nameText != null)
            {
                nameText.text = data.name;
                nameText.enabled = true;
            }

            if (emptyState != null) emptyState.SetActive(false);
            if (filledState != null) filledState.SetActive(true);
        }

        public void Clear()
        {
            currentRelic = null;
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
            if (nameText != null)
            {
                nameText.text = "";
                nameText.enabled = false;
            }

            if (emptyState != null) emptyState.SetActive(true);
            if (filledState != null) filledState.SetActive(false);
        }

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
            if (!isHovering || currentRelic == null) return;

            hoverTimer += Time.unscaledDeltaTime;
            if (hoverTimer >= tooltipDelay)
            {
                TooltipManager.Instance?.Show(currentRelic.name, BuildDescription(), GetComponent<RectTransform>());
            }
        }

        /// <summary>遗物描述（含动态数值，如附魔共鸣的当前加成）</summary>
        private string BuildDescription()
        {
            string desc = currentRelic.description;
            if (currentRelic.effects != null && currentRelic.effects.Exists(e => e.type == "EnchantResonance"))
            {
                var rd = Roguelike.RunDirector.Instance?.RunData;
                int bonus = rd != null ? rd.GetEnchantedCardKeys().Count / 4 : 0;
                desc += $"\n（当前 +{bonus} 伤害）";
            }
            return desc;
        }

        public RelicData GetRelic() => currentRelic;
        public bool IsEmpty => currentRelic == null;
    }
}