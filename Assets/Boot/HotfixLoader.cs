using System;
using System.IO;
using System.Reflection;
using HybridCLR;
using UnityEngine;

/// <summary>
/// AOT 热更加载器。由 BootLauncher 在 **Res.Init() 之后、加载游戏场景之前** 调用。
///
/// 加载优先级（EnsureLoaded）：
///   1) 本次真的下载了增量更新（非首次）→ 用**资源里**的最新 dll（本次即生效）并暂存，供下次启动；
///   2) 否则用**上次暂存**的更新 dll（仅当它属于当前包、且版本一致）；
///   3) 都没有 → 用 **StreamingAssets 内置版**（本地重新打包时走这条，不会被 OSS 旧 dll 盖住）。
/// </summary>
public static class HotfixLoader
{
    private static readonly string[] HotAssemblies = { "Roguelike" };

    private static readonly string[] AOTMetadataAssemblies =
    {
        "mscorlib", "System.Core", "UnityEngine.CoreModule", "YooAsset", "Boot",
    };

    private const string StagedVersionKey = "HotfixStagedVersion";
    private const string StagedStampKey = "HotfixStagedStamp";
    private const string BuiltinStampFile = "hotfix_stamp.txt";

    private static bool _loaded;
    private static string _builtinStamp;

    /// <summary>随包发售的构建戳（发布菜单写入）；重新打包后它会变，用于丢弃过期的暂存 dll。</summary>
    private static string BuiltinStamp
    {
        get
        {
            if (_builtinStamp != null) return _builtinStamp;
            try
            {
                string p = Path.Combine(Application.streamingAssetsPath, BuiltinStampFile);
                _builtinStamp = File.Exists(p) ? File.ReadAllText(p).Trim() : "";
            }
            catch { _builtinStamp = ""; }
            return _builtinStamp;
        }
    }

    /// <summary>补充 AOT 元数据 + 加载热更程序集（幂等）。资源就绪后、加载游戏场景前调用。</summary>
    /// <param name="preferResource">本次是否有增量更新下载（有则用资源里最新 dll，本次即生效）。</param>
    public static void EnsureLoaded(bool preferResource)
    {
        if (_loaded) return;
        _loaded = true;

#if !UNITY_EDITOR
        LoadAOTMetadata();
        foreach (var name in HotAssemblies)
            LoadHotAssembly(name, preferResource);
#else
        // 编辑器下热更程序集已被正常编译加载，无需手动加载
#endif
    }

    private static void LoadAOTMetadata()
    {
        foreach (var name in AOTMetadataAssemblies)
        {
            var bytes = ReadStreamingDll(name);
            if (bytes == null) { Debug.LogWarning($"[Hotfix] 缺少补充元数据：{name}"); continue; }
            var err = RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
            Debug.Log($"[Hotfix] 补充元数据 {name} => {err}");
        }
    }

    private static void LoadHotAssembly(string name, bool preferResource)
    {
        byte[] bytes = null;
        string from = null;

        // 1) 本次下载了增量更新：用资源里的最新 dll，并暂存供下次启动
        if (preferResource && Res.Ready)
        {
            var ta = Res.Load<TextAsset>("Hotfix/" + name);
            if (ta != null) { bytes = ta.bytes; from = "资源(本次更新)"; SaveStaged(name, bytes); }
        }

        // 2) 上次暂存的更新 dll（构建戳与版本都匹配时）
        if (bytes == null) { bytes = LoadStaged(name); if (bytes != null) from = "暂存(上次更新)"; }

        // 3) 内置版（本地重新打包走这里）
        if (bytes == null) { bytes = ReadStreamingDll(name); if (bytes != null) from = "内置(StreamingAssets)"; }

        if (bytes == null) { Debug.LogError($"[Hotfix] 找不到热更程序集：{name}"); return; }
        Assembly.Load(bytes);
        Debug.Log($"[Hotfix] 已加载热更程序集 {name}（{from}，{bytes.Length} 字节）");
    }

    private static void SaveStaged(string name, byte[] bytes)
    {
        try
        {
            File.WriteAllBytes(Path.Combine(Application.persistentDataPath, name + ".dll.bytes"), bytes);
            PlayerPrefs.SetString(StagedVersionKey, Res.LoadedVersion);
            PlayerPrefs.SetString(StagedStampKey, BuiltinStamp);
            PlayerPrefs.Save();
        }
        catch (Exception e) { Debug.LogWarning($"[Hotfix] 暂存 dll 失败：{e.Message}"); }
    }

    private static byte[] LoadStaged(string name)
    {
        string path = Path.Combine(Application.persistentDataPath, name + ".dll.bytes");
        if (!File.Exists(path)) return null;

        bool belongToThisPackage = PlayerPrefs.GetString(StagedStampKey, "") == BuiltinStamp;
        bool versionMatches = PlayerPrefs.GetString(StagedVersionKey, "") == Res.InstalledVersion;
        if (belongToThisPackage && versionMatches) return File.ReadAllBytes(path);

        Debug.Log($"[Hotfix] 丢弃过期暂存 dll：{name}（戳匹配={belongToThisPackage}，版本匹配={versionMatches}）");
        try { File.Delete(path); } catch { }
        return null;
    }

    private static byte[] ReadStreamingDll(string name)
    {
        string streaming = Path.Combine(Application.streamingAssetsPath, name + ".dll.bytes");
        return File.Exists(streaming) ? File.ReadAllBytes(streaming) : null;
    }
}
