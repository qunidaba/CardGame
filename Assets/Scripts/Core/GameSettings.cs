using UnityEngine;

/// <summary>
/// 游戏设置：主音量 / 全屏 / 分辨率。用 PlayerPrefs 持久化，启动时 Load+Apply。
/// </summary>
public static class GameSettings
{
    private const string KeyVolume = "settings.volume";
    private const string KeyFullscreen = "settings.fullscreen";
    private const string KeyResolution = "settings.resolution";

    /// <summary>可选分辨率（按从小到大）</summary>
    public static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
    };

    public static float Volume { get; private set; } = 1f;
    public static bool Fullscreen { get; private set; } = true;
    public static int ResolutionIndex { get; private set; } = 2; // 默认 1920x1080

    private static bool loaded;

    public static void Load()
    {
        Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 1f));
        Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) != 0;
        ResolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyResolution, DefaultIndex()), 0, Resolutions.Length - 1);
        loaded = true;
    }

    /// <summary>把当前设置应用到运行时（音量 / 分辨率 / 全屏）</summary>
    public static void Apply()
    {
        AudioListener.volume = Volume;

        var r = Resolutions[ResolutionIndex];
        Screen.SetResolution(r.x, r.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    private static void Save()
    {
        PlayerPrefs.SetFloat(KeyVolume, Volume);
        PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KeyResolution, ResolutionIndex);
        PlayerPrefs.Save();
    }

    // ===== 修改（立即应用 + 保存）=====

    public static void SetVolume(float v)
    {
        Volume = Mathf.Clamp01(v);
        Apply();
        Save();
    }

    public static void SetFullscreen(bool fullscreen)
    {
        Fullscreen = fullscreen;
        Apply();
        Save();
    }

    public static void SetResolutionIndex(int index)
    {
        ResolutionIndex = Mathf.Clamp(index, 0, Resolutions.Length - 1);
        Apply();
        Save();
    }

    public static void CycleResolution(int delta)
    {
        int count = Resolutions.Length;
        ResolutionIndex = ((ResolutionIndex + delta) % count + count) % count;
        Apply();
        Save();
    }

    public static void ResetToDefault()
    {
        Volume = 1f;
        Fullscreen = true;
        ResolutionIndex = 2;
        Apply();
        Save();
    }

    public static string ResolutionLabel()
    {
        var r = Resolutions[ResolutionIndex];
        return $"{r.x} × {r.y}";
    }

    /// <summary>默认分辨率：不超过当前屏幕的最大预设</summary>
    private static int DefaultIndex()
    {
        int w = Screen.currentResolution.width;
        int h = Screen.currentResolution.height;
        for (int i = Resolutions.Length - 1; i >= 0; i--)
            if (Resolutions[i].x <= w && Resolutions[i].y <= h) return i;
        return 0;
    }
}
