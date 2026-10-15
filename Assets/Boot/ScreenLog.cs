using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把 Debug.Log 直接显示在屏幕左上角（打包后的游戏没有控制台窗口，用它看日志）。
/// 自动创建，无需挂到场景。正式项目别带这个。
/// </summary>
public class ScreenLog : MonoBehaviour
{
    private const int MaxLines = 30;
    private static readonly List<string> Lines = new List<string>();
    private static bool _subscribed;

    // 最早时机订阅日志（早于任何 BeforeSceneLoad 的逻辑，保证不漏）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        Lines.Clear();
        Application.logMessageReceived += OnLog;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateDisplay()
    {
        var go = new GameObject("[ScreenLog]");
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenLog>();
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        Lines.Add(message);
        if (Lines.Count > MaxLines)
            Lines.RemoveRange(0, Lines.Count - MaxLines);
    }

    private void OnGUI()
    {
        if (Lines.Count == 0) return;

        float w = Screen.width;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        var rect = new Rect(8f, 8f, w - 16f, Lines.Count * 20f + 12f);

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f),
            string.Join("\n", Lines), style);
    }
}
