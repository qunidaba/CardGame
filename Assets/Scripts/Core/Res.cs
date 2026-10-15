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

    /// <summary>远端资源服务器地址（按优先级，本地测试默认指向本机 8000 端口）。</summary>
    public static string[] RemoteHosts = { "https://mygame-res1.oss-cn-shanghai.aliyuncs.com/cdn" };

    public enum PlayMode { EditorSimulate, Offline, Host }

    /// <summary>
    /// 运行模式：
    /// - 编辑器默认 EditorSimulate（直接读工程，不下载）；
    /// - 想测"资源热更"，把编辑器分支改成 Host（从 RemoteHosts 下载资源包）；
    /// - 真机默认 Host（也可改 Offline 做出"内置资源、不联网"的包）。
    /// </summary>
    public static PlayMode Mode =
#if UNITY_EDITOR
        PlayMode.EditorSimulate;
#else
        PlayMode.Host;
#endif

    // ===== 更新进度（供 UpdatePanel 读取）=====
    public static string Phase = "";                 // 当前阶段文字
    public static bool Downloading;                  // 是否正在下载资源
    public static bool HasUpdate;                    // 是否发现了需要下载的资源
    public static float DownloadProgress;            // 0..1
    public static int DownloadCurrent;               // 已下载文件数
    public static int DownloadTotal;                 // 需下载文件总数
    public static long DownloadCurrentBytes;         // 已下载字节
    public static long DownloadTotalBytes;            // 需下载总字节

    // ===== 可选更新 =====
    public static bool FirstRun;                      // 首次运行（本地没有已安装版本 → 强制下载）
    public static string InstalledVersion = "";       // 本地已安装版本
    public static string RemoteVersion = "";          // 远端最新版本
    public static bool UpdateAccepted;                // 玩家是否同意更新

    /// <summary>发现新版本时调用（由 UI 层提供）：显示"是否更新"并等玩家选择，把结果写进 UpdateAccepted。</summary>
    public static System.Func<string, IEnumerator> AskUpdate;

    private const string InstalledVersionKey = "GameRes.InstalledVersion";

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

        Phase = "初始化资源系统";
        Downloading = false;
        HasUpdate = false;
        DownloadProgress = 0f;
        DownloadCurrent = 0;
        DownloadTotal = 0;
        DownloadCurrentBytes = 0;
        DownloadTotalBytes = 0;

        if (!YooAssets.IsInitialized)
            YooAssets.Initialize();

        if (!YooAssets.TryGetPackage(PackageName, out _package))
            _package = YooAssets.CreatePackage(PackageName);

        InitializePackageOperation initOp = null;
        switch (Mode)
        {
            case PlayMode.EditorSimulate:
#if UNITY_EDITOR
                // 编辑器模拟：直接读工程里的 Assets/GameRes，不用构建资源包
                {
                    var buildResult = EditorSimulateBuildInvoker.Build(PackageName, (int)EBundleType.VirtualAssetBundle);
                    var simOpt = new EditorSimulateModeOptions();
                    simOpt.EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
                    initOp = _package.InitializePackageAsync(simOpt);
                }
#endif
                break;

            case PlayMode.Offline:
                // 离线：只读打进包体里的资源包，不联网
                {
                    var offlineOpt = new OfflinePlayModeOptions();
                    offlineOpt.BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
                    initOp = _package.InitializePackageAsync(offlineOpt);
                }
                break;

            case PlayMode.Host:
                // 联机：沙盒缓存 + 远端下载（支持资源热更）
                // 注：不使用"内置包"（Builtin），首次运行全部从服务器下载——
                //     这样不用构建内置包，StreamingAssets 也不会被资源包撑大。
                {
                    var remote = new RemoteService(RemoteHosts);
                    var hostOpt = new HostPlayModeOptions();
                    hostOpt.CacheFileSystemParameters = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remote);
                    hostOpt.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadMaxConcurrency, 10);
                    hostOpt.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadMaxRequestPerFrame, 5);
                    hostOpt.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadWatchdogTimeout, 10);
                    initOp = _package.InitializePackageAsync(hostOpt);
                }
                break;
        }

        if (initOp == null)
        {
            Debug.LogError($"[Res] 当前模式 {Mode} 无法初始化（编辑器模拟模式仅编辑器可用）");
            yield break;
        }

        yield return initOp;
        if (initOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[Res] YooAsset 初始化失败：{initOp.Error}");
            yield break;
        }

        Phase = "检查资源版本";
        var versionOp = _package.RequestPackageVersionAsync();
        yield return versionOp;
        if (versionOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[Res] 请求资源版本失败：{versionOp.Error}");
            yield break;
        }

        RemoteVersion = versionOp.PackageVersion;
        InstalledVersion = PlayerPrefs.GetString(InstalledVersionKey, "");
        FirstRun = string.IsNullOrEmpty(InstalledVersion);
        bool updateAvailable = !FirstRun && InstalledVersion != RemoteVersion;

        // 后续更新：提示玩家，是否更新由玩家决定（首次不询问，强制下载最新）
        bool accepted = true;
        if (updateAvailable)
        {
            UpdateAccepted = false;
            if (AskUpdate != null)
            {
                yield return AskUpdate(RemoteVersion);
                accepted = UpdateAccepted;
            }
            Debug.Log($"[Res] 发现新版本 {RemoteVersion}（当前 {InstalledVersion}）：玩家{(accepted ? "选择更新" : "跳过")}");
        }

        // 是否加载/采用新版本：首次强制；无更新用最新；有更新则看玩家是否同意，拒绝则回退到已安装版本
        bool useNewVersion = FirstRun || !updateAvailable || accepted;
        string loadVersion = useNewVersion ? RemoteVersion : InstalledVersion;

        Phase = "更新资源清单";
        var manifestOp = _package.LoadPackageManifestAsync(new LoadPackageManifestOptions(loadVersion, 60));
        yield return manifestOp;
        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[Res] 加载资源清单失败：{manifestOp.Error}");
            yield break;
        }

        // 下载资源包：首次强制；已最新为 0；玩家同意则下差异；玩家拒绝则跳过（用旧的运行）
        if (useNewVersion)
        {
            Phase = "检查资源更新";
            var downloader = _package.CreateResourceDownloader(new ResourceDownloaderOptions(10, 3));
            if (downloader != null && downloader.TotalDownloadCount > 0)
            {
                HasUpdate = true;
                Downloading = true;
                Phase = "正在下载资源";
                DownloadTotal = downloader.TotalDownloadCount;
                DownloadTotalBytes = downloader.TotalDownloadBytes;
                DownloadCurrent = 0;
                DownloadCurrentBytes = 0;
                DownloadProgress = 0f;
                downloader.DownloadProgressChanged += (d) =>
                {
                    DownloadProgress = d.Progress;
                    DownloadCurrent = d.CurrentDownloadCount;
                    DownloadCurrentBytes = d.CurrentDownloadBytes;
                };

                Debug.Log($"[Res] 需要下载 {DownloadTotal} 个资源包（{DownloadTotalBytes / 1024} KB），开始下载…");
                downloader.StartDownload();
                yield return downloader;
                Downloading = false;
                if (downloader.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogError($"[Res] 资源下载失败：{downloader.Error}");
                    yield break;
                }
                Debug.Log("[Res] 资源下载完成");
            }

            // 记录已安装版本
            PlayerPrefs.SetString(InstalledVersionKey, RemoteVersion);
            PlayerPrefs.Save();
        }

        // 清理不再使用的缓存包（被删除的资源 / 旧版本残留），回收客户端空间
        if (Mode != PlayMode.EditorSimulate)
        {
            Phase = "清理旧资源";
            var clearOp = _package.ClearCacheAsync(new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles));
            yield return clearOp;
            if (clearOp.Status != EOperationStatus.Succeeded)
                Debug.LogWarning($"[Res] 清理未使用缓存失败（忽略，不影响启动）：{clearOp.Error}");
            else
                Debug.Log("[Res] 已清理不再使用的缓存包");
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
