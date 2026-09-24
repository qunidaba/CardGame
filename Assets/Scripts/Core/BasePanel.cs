using UnityEngine;

/// <summary>
/// 所有 UI 面板的基类，统一 Show/Hide/Init 接口
/// </summary>
public abstract class BasePanel : MonoBehaviour
{
    protected GameObject panelRoot;

    /// <summary>
    /// 组件唤醒时自动初始化 panelRoot
    /// </summary>
    protected virtual void Awake()
    {
        panelRoot = gameObject;
    }

    /// <summary>
    /// 初始化，子类可重写
    /// </summary>
    public virtual void Init()
    {
        // panelRoot 已在 Awake 中初始化
    }

    /// <summary>
    /// 显示面板
    /// </summary>
    public virtual void Show()
    {
        panelRoot.SetActive(true);
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public virtual void Hide()
    {
        panelRoot.SetActive(false);
    }
}
