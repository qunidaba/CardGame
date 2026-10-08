using UnityEngine;
using Roguelike;

/// <summary>
/// 游戏入口：启动时校验依赖并显示主菜单。
/// （原名 BattleUI —— 既不负责战斗 UI，也不只做战斗，故更名并移入 Core）
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    private void Start()
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

        // 应用存档设置（音量 / 分辨率 / 全屏）
        GameSettings.Load();
        GameSettings.Apply();

        // ESC 暂停（局内生效）
        if (GetComponent<PauseController>() == null)
            gameObject.AddComponent<PauseController>();

        // 音频管理器 + BGM（Resources/Audio/BGM，没有则静默）
        if (AudioManager.Instance == null)
            gameObject.AddComponent<AudioManager>();
        AudioManager.Instance?.PlayBgmFromResources();

        // 进入主菜单（在菜单里点「开始游戏」才会 StartNewRun）
        UIManager.Instance.ShowPanel<MainMenuPanel>();
    }
}
