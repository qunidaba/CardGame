using System.Collections;
using UnityEngine;
using Roguelike;

/// <summary>
/// 游戏入口：启动时先初始化 YooAsset（资源就绪），再校验依赖并显示主菜单。
/// 「首次」强制下载资源；「后续更新」只提示玩家，是否更新由玩家决定。
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

        // 发现新版本时，由这里弹出"是否更新"询问（Res.Init 内部会 yield 这个协程）
        Res.AskUpdate = AskUpdateRoutine;

        StartCoroutine(BootRoutine());
    }

    private IEnumerator BootRoutine()
    {
        UpdatePanel.Show();

        // 初始化资源：首次强制下载；有更新则询问玩家（拒绝则用旧版本运行）
        yield return Res.Init();

        // 若本次成功下载了资源（首次，或玩家同意更新），显示"点击进入"
        if (Res.Ready && Res.HasUpdate)
        {
            UpdatePanel.ShowConfirm("资源更新完成，点击进入", "进入游戏");
            yield return new WaitUntil(() => UpdatePanel.Choice == true);
        }

        UpdatePanel.Hide();

        if (!Res.Ready)
        {
            Debug.LogError("[GameBootstrap] 资源初始化失败，无法继续。");
            yield break;
        }

        // 加载热更 dll（从 YooAsset 下载/取到）并执行热更代码
        HotfixLoader.Run();

        // 应用存档设置（音量 / 分辨率 / 全屏）
        GameSettings.Load();
        GameSettings.Apply();

        // ESC 暂停（局内生效）
        if (GetComponent<PauseController>() == null)
            gameObject.AddComponent<PauseController>();

        // 音频管理器 + BGM（GameRes/Audio/BGM，没有则静默）
        if (AudioManager.Instance == null)
            gameObject.AddComponent<AudioManager>();
        AudioManager.Instance?.PlayBgmFromResources();

        // 进入主菜单（在菜单里点「开始游戏」才会 StartNewRun）
        UIManager.Instance.ShowPanel<MainMenuPanel>();
    }

    /// <summary>发现新版本：询问玩家是否更新。</summary>
    private IEnumerator AskUpdateRoutine(string newVersion)
    {
        UpdatePanel.ShowChoice($"发现新版本（{newVersion}），是否更新？", "更新", "跳过");
        yield return new WaitUntil(() => UpdatePanel.Choice != null);
        Res.UpdateAccepted = UpdatePanel.Choice == true;
    }
}
