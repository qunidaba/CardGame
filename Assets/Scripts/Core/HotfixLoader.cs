using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 热更 DLL 加载器：从 YooAsset（**网络下发**的 TextAsset/bytes）取热更 dll，加载并执行。
/// 这样改热更代码后：CompileDll → 放进 Assets/GameRes/Hotfix → 发布到 OSS → 客户端重启自动下载生效。
/// 编辑器下热更程序集已被正常编译加载，直接取。
/// </summary>
public static class HotfixLoader
{
    public static void Run()
    {
        var asm = LoadHotUpdateAssembly();
        if (asm == null)
        {
            Debug.LogError("[Hotfix] 未能加载 HotUpdate 程序集");
            return;
        }
        asm.GetType("Hello")?.GetMethod("Run")?.Invoke(null, null);
    }

    private static Assembly LoadHotUpdateAssembly()
    {
#if UNITY_EDITOR
        // 编辑器下热更程序集已被正常编译并加载，直接取
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "HotUpdate");
#else
        // 真机：从 YooAsset 取（首次/更新时已由 Res.Init 下载到本地）
        var ta = Res.Load<TextAsset>("Hotfix/HotUpdate");
        if (ta == null)
        {
            Debug.LogError("[Hotfix] 下载热更 dll 失败（Hotfix/HotUpdate）");
            return null;
        }
        Debug.Log($"[Hotfix] 热更 dll 大小 {ta.bytes.Length} 字节");
        return Assembly.Load(ta.bytes);
#endif
    }
}
