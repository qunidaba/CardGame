using System;
using UnityEngine;

namespace Roguelike.Core
{
    /// <summary>
    /// 统一日志入口：正式包可关 Info/Debug，保留 Warn/Error。
    /// 用法：GameLog.Info("..."); GameLog.Error("...");
    /// </summary>
    public static class GameLog
    {
        public enum Level { Debug, Info, Warn, Error }
        public static Level CurrentLevel = Level.Info;

        public static void Debug(string msg) { if (CurrentLevel <= Level.Debug) UnityEngine.Debug.Log($"[DBG] {msg}"); }
        public static void Info(string msg) { if (CurrentLevel <= Level.Info) UnityEngine.Debug.Log($"[INF] {msg}"); }
        public static void Warn(string msg) { if (CurrentLevel <= Level.Warn) UnityEngine.Debug.LogWarning($"[WRN] {msg}"); }
        public static void Error(string msg) { if (CurrentLevel <= Level.Error) UnityEngine.Debug.LogError($"[ERR] {msg}"); }

        // 兼容旧调用
        public static void Log(string msg) => Info(msg);
        public static void LogWarning(string msg) => Warn(msg);
        public static void LogError(string msg) => Error(msg);
    }
}