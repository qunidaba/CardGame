using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 遗物UI组件：显示遗物图标、名称、描述
    /// </summary>
    public class RelicUI : MonoBehaviour
    {
        [Header("UI 引用")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;

        private RelicData relicData;

        public void Init(RelicData data)
        {
            relicData = data;
            if (nameText != null)
                nameText.text = data.name;
            if (descriptionText != null)
                descriptionText.text = data.description;

            // 加载图标
            if (iconImage != null)
            {
                if (!string.IsNullOrEmpty(data.iconPath))
                {
                    var sprite = Resources.Load<Sprite>(data.iconPath);
                    if (sprite != null)
                        iconImage.sprite = sprite;
                    else
                        Debug.LogWarning($"[RelicUI] 找不到图标: Resources/{data.iconPath}");
                }
                else
                {
                    iconImage.sprite = null;
                }
            }
        }

        public RelicData GetRelicData() => relicData;
    }
}