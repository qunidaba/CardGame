using UnityEngine;
using Roguelike;

/// <summary>
/// 战斗入口控制器
/// 负责初始化战斗、处理战斗结束和重新开始
/// UI 更新由 BattlePanel 通过事件驱动，不再每帧轮询
/// </summary>
public class BattleUI : MonoBehaviour
{
    private BattleManager battleManager;

    void Start()
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("UIManager不存在！请确保场景中有UIManager实例。");
            return;
        }

        if (RunDirector.Instance == null)
        {
            Debug.LogError("RunDirector不存在！请确保场景中有RunDirector实例。");
            return;
        }

        // 通过 RunDirector 开始新游戏（会自动初始化战斗序列并进入第一场战斗）
        RunDirector.Instance.StartNewRun();
    }

    /// <summary>
    /// 重新开始战斗（新游戏）
    /// </summary>
    private void RestartBattle()
    {
        UIManager.Instance.HideAll();
        RunDirector.Instance.StartNewRun();
    }
}