using UnityEngine;

/// <summary>
/// 最小验证用的热更类。改 Version 后重新编译热更 DLL，即可验证"代码热更"是否生效。
/// </summary>
public static class Hello
{
    public static string Version => "v2";

    public static void Run()
    {
        Debug.Log($"[HotUpdate] Hello, HybridCLR —— {Version}");
    }
}
