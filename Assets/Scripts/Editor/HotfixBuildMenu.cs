using System;
using System.IO;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 热更发布流程（固化）：
///  ① 「生成全部」= HybridCLR/Generate/All（link.xml / AOTGenericReferences / 裁剪 AOT dll）；
///  ② 「编译DLL并发布」= 编译热更 dll + 拷补充元数据，并同时输出到：
///       - Assets/StreamingAssets/*.dll.bytes （兜底：资源取不到时用）
///       - Assets/GameRes/Hotfix/*.bytes      （作为资源，走网络下发，**本次启动即可生效**）
/// </summary>
public static class HotfixBuildMenu
{
    // 与 Boot/HotfixLoader.cs 的 HotAssemblies 保持一致
    private static readonly string[] HotAssemblies = { "Roguelike" };

    // 与 Boot/HotfixLoader.cs 的 AOTMetadataAssemblies 保持一致
    private static readonly string[] AOTMetadataAssemblies =
    {
        "mscorlib", "System.Core", "UnityEngine.CoreModule", "YooAsset", "Boot",
    };

    private const string StreamingAssetsDir = "Assets/StreamingAssets";

    [MenuItem("工具/热更：① 生成全部（Generate/All）", false, 1)]
    public static void GenerateAll()
    {
        PrebuildCommand.GenerateAll();
        AssetDatabase.Refresh();
        Debug.Log("[Hotfix] 已执行 ①：HybridCLR/Generate/All（更新 link.xml / AOTGenericReferences / 裁剪 AOT dll）");
    }

    [MenuItem("工具/热更：② 编译DLL并发布到GameRes", false, 2)]
    public static void CompileAndCopy()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;

        CompileDllCommand.CompileDll(target);

        string hotDir = $"HybridCLRData/HotUpdateDlls/{target}";
        string aotDir = $"HybridCLRData/AssembliesPostIl2CppStrip/{target}";

        Directory.CreateDirectory(StreamingAssetsDir);
        Directory.CreateDirectory("Assets/GameRes/Hotfix");

        // 1) 热更 dll → StreamingAssets（兜底）+ GameRes/Hotfix（资源，走网络下发）
        foreach (var name in HotAssemblies)
        {
            string src = $"{hotDir}/{name}.dll";
            if (!File.Exists(src)) { Debug.LogError($"[Hotfix] 找不到热更 dll：{src}"); continue; }
            File.Copy(src, $"{StreamingAssetsDir}/{name}.dll.bytes", true);
            File.Copy(src, $"Assets/GameRes/Hotfix/{name}.bytes", true);
            Debug.Log($"[Hotfix] 热更 dll：{name} → StreamingAssets(.dll.bytes) + GameRes/Hotfix(.bytes)");
        }

        // 2) 补充元数据 AOT dll → StreamingAssets（供 RuntimeApi.LoadMetadataForAOTAssembly）
        foreach (var name in AOTMetadataAssemblies)
        {
            string src = $"{aotDir}/{name}.dll";
            if (!File.Exists(src)) { Debug.LogError($"[Hotfix] 找不到 AOT 元数据 dll：{src}（请先执行「① 生成全部」）"); continue; }
            File.Copy(src, $"{StreamingAssetsDir}/{name}.dll.bytes", true);
        }
        Debug.Log("[Hotfix] 补充元数据 dll 已拷入 StreamingAssets：" + string.Join("、", AOTMetadataAssemblies));

        // 3) 写"构建戳"：重新打包后客户端自动丢弃本地那份"过期的暂存 dll"
        File.WriteAllText($"{StreamingAssetsDir}/hotfix_stamp.txt", Guid.NewGuid().ToString("N"));
        Debug.Log("[Hotfix] 已写入构建戳 hotfix_stamp.txt");

        AssetDatabase.Refresh();
    }
}
