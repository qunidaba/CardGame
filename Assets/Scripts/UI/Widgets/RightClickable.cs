using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 右键点击回调（左键由 Button 处理，互不干扰）。
/// </summary>
public class RightClickable : MonoBehaviour, IPointerClickHandler
{
    public Action OnRightClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            OnRightClick?.Invoke();
    }
}
