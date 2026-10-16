using System.Collections;
using UnityEngine;
using Roguelike;

/// <summary>
/// 游戏内入口（随游戏场景一起，来自热更程序集）。
/// 真机：资源初始化已在 Boot 启动器完成，这里只做游戏内初始化。
/// 编辑器：若直接 Play 游戏场景（Res 未就绪），这里兜底初始化一次。
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

        if (!Res.Ready)
        {
            // 编辑器直接 Play 游戏场景时的兜底（真机由 Boot 启动器处理）
            Debug.Log($"[GameBootstrap] 兜底初始化（Res 未就绪）");
            StartCoroutine(BootThenStart());
            return;
        }

        StartGame();
    }

    private IEnumerator BootThenStart()
    {
        // 兜底（主要给编辑器直接 Play 游戏场景用）：只初始化资源，不再重复弹确认
        UpdatePanel.Show();
        yield return Res.Init();
        UpdatePanel.Hide();

        if (!Res.Ready) { Debug.LogError("[GameBootstrap] 资源初始化失败。"); yield break; }
        StartGame();
    }

    private void StartGame()
    {
        // 配置从 YooAsset 读取（资源已就绪）
        Roguelike.Data.ConfigLoader.LoadAll();

        GameSettings.Load();
        GameSettings.Apply();

        if (GetComponent<PauseController>() == null)
            gameObject.AddComponent<PauseController>();

        if (AudioManager.Instance == null)
            gameObject.AddComponent<AudioManager>();
        AudioManager.Instance?.PlayBgmFromResources();

        UIManager.Instance.ShowPanel<MainMenuPanel>();
    }
}
