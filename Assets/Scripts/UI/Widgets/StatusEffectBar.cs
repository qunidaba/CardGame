using System.Collections.Generic;
using UnityEngine;
using Roguelike;

/// <summary>
/// 状态效果栏容器
/// </summary>
public class StatusEffectBar : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject iconPrefab;
    public Transform container;
    public float iconSize = 40f;

    [Header("图标资源（可选）")]
    public List<StatusEffectSprite> sprites = new List<StatusEffectSprite>();

    private Dictionary<string, StatusEffectIcon> icons = new Dictionary<string, StatusEffectIcon>();
    private Dictionary<StatusEffectType, Sprite> spriteMap = new Dictionary<StatusEffectType, Sprite>();
    private bool spriteMapBuilt;

    private void Awake()
    {
        EnsureSpriteMap();
    }

    /// <summary>
    /// 懒构建图标表。运行时代码是 AddComponent 之后才赋 sprites 的，
    /// 那时 Awake 已经跑过了，所以每次 Refresh 前兜底重建一次。
    /// </summary>
    private void EnsureSpriteMap()
    {
        if (spriteMapBuilt) return;
        if (sprites == null || sprites.Count == 0) return;   // 还没赋值，等下次 Refresh 再建

        spriteMapBuilt = true;
        spriteMap.Clear();
        foreach (var s in sprites)
            spriteMap[s.type] = s.sprite;
    }

    public void Refresh(BattleUnit unit)
    {
        EnsureSpriteMap();

        var currentKeys = new HashSet<string>();

        // 更新/创建图标
        foreach (var effect in unit.StatusEffects.GetAllEffects())
        {
            string key = Key(effect);
            currentKeys.Add(key);

            if (!icons.TryGetValue(key, out var icon))
            {
                var go = Instantiate(iconPrefab, container);
                icon = go.GetComponent<StatusEffectIcon>();
                icons[key] = icon;
            }
            icon.Setup(effect.type, effect.amount, effect.duration,
                spriteMap.TryGetValue(effect.type, out var spr) ? spr : null,
                effect.suit, effect.useSuit, unit);
        }

        // 清理多余图标
        var toRemove = new List<string>();
        foreach (var kvp in icons)
        {
            if (!currentKeys.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        }
        foreach (var key in toRemove)
        {
            Destroy(icons[key].gameObject);
            icons.Remove(key);
        }
    }

    private static string Key(StatusEffect effect)
    {
        return $"{(int)effect.type}_{(effect.useSuit ? ((int)effect.suit + 1) : 0)}";
    }

    public void Clear()
    {
        foreach (var icon in icons.Values)
        {
            Destroy(icon.gameObject);
        }
        icons.Clear();
    }
}

[System.Serializable]
public class StatusEffectSprite
{
    public StatusEffectType type;
    public Sprite sprite;
}