using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

/// <summary>
/// 资源门面：把原先的 Res.Load 统一走 YooAsset。
/// key 与旧的 Resources 路径一致（如 "Fonts/simhei SDF"、"Poker/A-S"），
/// 内部映射到 "Assets/GameRes/{key}"（采集器开了 Support Extensionless，不用写扩展名）。
/// </summary>
public static class Res
{
    public const string PackageName = "GameRes";
    public const string Root = "Assets/GameRes/";

    private static ResourcePackage _package;
    private static bool _ready;
    public static bool Ready => _ready;

    private static readonly Dictionary<string, AssetHandle> _handles = new Dictionary<string, AssetHandle>();
    private static readonly Dictionary<string, SubAssetsHandle> _subHandles = new Dictionary<string, SubAssetsHandle>();

    private static string Loc(string key) => Root + key;

    /// <summary>初始化 YooAsset 并加载清单（在进入游戏逻辑前 await 完）。</summary>
    public static IEnumerator Init()
    {
        _ready = false;
        _package = null;
        _handles.Clear();
        _subHandles.Clear();

        if (!YooAssets.IsInitialized)
            YooAssets.Initialize();

        if (!YooAssets.TryGetPackage(PackageName, out _package))
            _package = YooAssets.CreatePackage(PackageName);

        InitializePackageOperation initOp;
#if UNITY_EDITOR
        // 编辑器：模拟模式，直接读工程里的 Assets/GameRes，不用构建资源包
        var buildResult = EditorSimulateBuildInvoker.Build(PackageName, (int)EBundleType.VirtualAssetBundle);
        var simOpt = new EditorSimulateModeOptions();
        simOpt.EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
        initOp = _package.InitializePackageAsync(simOpt);
#else
        // 真机：离线模式，读打进包体里的资源包
        var offlineOpt = new OfflinePlayModeOptions();
        offlineOpt.BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
        initOp = _package.InitializePackageAsync(offlineOpt);
#endif

        yield return initOp;
        if (initOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[Res] YooAsset 初始化失败：{initOp.Error}");
            yield break;
        }

        var versionOp = _package.RequestPackageVersionAsync();
        yield return versionOp;
        if (versionOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[Res] 请求资源版本失败：{versionOp.Error}");
            yield break;
        }

        var manifestOp = _package.LoadPackageManifestAsync(new LoadPackageManifestOptions(versionOp.PackageVersion, 60));
        yield return manifestOp;
        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[Res] 加载资源清单失败：{manifestOp.Error}");
            yield break;
        }

        _ready = true;
        Debug.Log("[Res] YooAsset 就绪");
    }

    /// <summary>同步加载单个资源（等价于原来的 Res.Load&lt;T&gt;）。</summary>
    public static T Load<T>(string key) where T : Object
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (!_ready)
        {
            Debug.LogWarning($"[Res] 尚未就绪，无法加载 '{key}'");
            return null;
        }

        if (_handles.TryGetValue(key, out var cached) && cached != null)
            return cached.AssetObject as T;

        var handle = _package.LoadAssetSync<T>(Loc(key));
        if (handle.Status != EOperationStatus.Succeeded)
        {
            Debug.LogWarning($"[Res] 加载失败 '{key}'：{handle.Error}");
            return null;
        }

        _handles[key] = handle;
        return handle.AssetObject as T;
    }

    /// <summary>同步加载一个资源上的全部子资源（等价于原来的 Res.LoadAll&lt;T&gt;，用于切片/多帧）。</summary>
    public static T[] LoadAll<T>(string key) where T : Object
    {
        if (string.IsNullOrEmpty(key)) return System.Array.Empty<T>();
        if (!_ready) return System.Array.Empty<T>();

        if (_subHandles.TryGetValue(key, out var cached) && cached != null)
            return Collect<T>(cached.SubAssetObjects);

        var handle = _package.LoadSubAssetsSync<T>(Loc(key));
        if (handle.Status == EOperationStatus.Succeeded)
        {
            _subHandles[key] = handle;
            var arr = Collect<T>(handle.SubAssetObjects);
            if (arr.Length > 0) return arr;
        }

        // 回退：把该路径当单资源处理
        var one = Load<T>(key);
        return one != null ? new[] { one } : System.Array.Empty<T>();
    }

    private static T[] Collect<T>(IReadOnlyList<Object> objs) where T : Object
    {
        var list = new List<T>();
        if (objs != null)
            foreach (var o in objs)
                if (o is T t) list.Add(t);
        return list.ToArray();
    }
}
