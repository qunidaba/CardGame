using System.Collections;
using UnityEngine;
using YooAsset;

/// <summary>
/// AOT 启动器：挂在一个**空的启动场景**里（该场景只放这个组件，不挂任何热更脚本）。
///
/// 流程：
///   1) 热更 dll 已在 `BeforeSceneLoad` 由 HotfixLoader 加载好；
///   2) 本启动器：YooAsset 资源初始化（含更新）；
///   3) 从 AB（YooAsset）加载**游戏场景** —— 这样游戏场景里挂的热更脚本才能被正确还原。
/// </summary>
public class BootLauncher : MonoBehaviour
{
    [Tooltip("游戏场景在 GameRes 下的相对路径（不含扩展名）")]
    public string gameScene = "Scenes/Scene";

    private IEnumerator Start()
    {
        // 发现新版本时弹「更新 / 跳过」（把选择写进 Res.UpdateAccepted）
        Res.AskUpdate = AskUpdateRoutine;

        UpdatePanel.Show();

        // 初始化资源；失败则提示并提供「重试 / 退出」
        while (true)
        {
            yield return Res.Init();
            if (Res.Ready) break;

            UpdatePanel.ShowChoice(
                $"资源初始化失败：\n{Res.LastError}\n请检查网络后重试。",
                "重试", "退出");
            yield return new WaitUntil(() => UpdatePanel.Choice != null);
            if (UpdatePanel.Choice != true)
            {
                Debug.LogWarning("[Boot] 玩家选择退出。");
                Quit();
                yield break;
            }
        }

        // 下载完成后，显示「更新完成，点击进入」，等玩家点击
        if (Res.HasUpdate)
        {
            UpdatePanel.ShowConfirm("资源更新完成，点击进入", "进入游戏");
            yield return new WaitUntil(() => UpdatePanel.Choice == true);
        }

        UpdatePanel.Hide();

        // 加载热更程序集（必须在加载游戏场景之前）：
        // 本次若下载了增量更新 → 用资源里的最新 dll，本次即生效；否则用暂存/内置版（本地重打包即走这条）
        HotfixLoader.EnsureLoaded(Res.HasUpdate && !Res.FirstRun);

        // 从 AB 加载游戏场景（热更脚本挂在场景上，必须走 AB 加载）
        var handle = Res.LoadSceneAsync(gameScene);
        yield return handle;
        if (handle.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[Boot] 加载游戏场景失败：{handle.Error}");
            yield break;
        }
        Debug.Log("[Boot] 游戏场景已加载");
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>发现新版本时弹出「更新 / 跳过」，等玩家点击，把结果写进 Res.UpdateAccepted。</summary>
    private static IEnumerator AskUpdateRoutine(string version)
    {
        UpdatePanel.ShowChoice($"发现新版本 {version}，是否立即更新？", "更新", "跳过");
        yield return new WaitUntil(() => UpdatePanel.Choice != null);
        Res.UpdateAccepted = UpdatePanel.Choice == true;
        Debug.Log($"[Boot] 玩家选择：{(Res.UpdateAccepted ? "更新" : "跳过")}（{version}）");
    }
}
