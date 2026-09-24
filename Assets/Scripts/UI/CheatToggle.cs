using UnityEngine;
using Roguelike;

/// <summary>
/// 调试用：按快捷键开关游戏内作弊面板。
/// 由 UIManager 在启动时创建（挂在 UIManager 自己身上，不随面板开关而消失）。
/// 只在战斗中可以打开；已经打开时随时可以关掉。
/// </summary>
public class CheatToggle : MonoBehaviour
{
    public KeyCode key = KeyCode.F1;

    /// <summary>当前是否在战斗中（战斗管理器存在且战斗未结束）</summary>
    public static bool InBattle
    {
        get
        {
            var bm = RunDirector.Instance != null ? RunDirector.Instance.BattleManager : null;
            return bm != null && !bm.IsBattleOver;
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(key)) return;
        if (UIManager.Instance == null) return;

        // 已打开 → 随时可以关闭
        if (UIManager.Instance.IsPanelActive("CheatPanel"))
        {
            UIManager.Instance.HidePanel("CheatPanel");
            return;
        }

        // 未打开 → 只在战斗中允许打开
        if (!InBattle)
        {
            Debug.Log("[Cheat] 只能在战斗中打开作弊面板");
            return;
        }

        UIManager.Instance.ShowPanel<CheatPanel>();
    }
}
