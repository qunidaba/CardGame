using System.IO;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键：编译热更 DLL → 拷贝到 Assets/GameRes/Hotfix/HotUpdate.bytes。
/// 之后用 YooAsset 打包/发布上传，客户端就能通过网络热更这份 dll。
/// </summary>
public static class HotfixBuildMenu
{
    [MenuItem("工具/热更：编译DLL并放入GameRes")]
    public static void CompileAndCopy()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        CompileDllCommand.CompileDll(target);

        string src = $"HybridCLRData/HotUpdateDlls/{target}/HotUpdate.dll";
        const string dir = "Assets/GameRes/Hotfix";
        const string dst = dir + "/HotUpdate.bytes";

        if (!File.Exists(src))
        {
            Debug.LogError($"[Hotfix] 找不到热更 DLL：{src}");
            return;
        }

        Directory.CreateDirectory(dir);
        File.Copy(src, dst, true);
        AssetDatabase.Refresh();
        Debug.Log($"[Hotfix] 已编译热更 DLL 并拷贝到 {dst}");
    }
}
