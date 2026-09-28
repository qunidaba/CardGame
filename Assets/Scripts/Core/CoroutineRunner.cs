using System;
using System.Collections;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// 协程运行器：为非 MonoBehaviour 类提供协程支持
    /// </summary>
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[CoroutineRunner]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<CoroutineRunner>();
                }
                return instance;
            }
        }

        /// <summary>
        /// 启动协程
        /// </summary>
        public new Coroutine StartCoroutine(IEnumerator routine)
        {
            return base.StartCoroutine(routine);
        }

        /// <summary>
        /// 停止协程
        /// </summary>
        public new void StopCoroutine(Coroutine routine)
        {
            base.StopCoroutine(routine);
        }

        /// <summary>
        /// 停止所有协程
        /// </summary>
        public new void StopAllCoroutines()
        {
            base.StopAllCoroutines();
        }

        /// <summary>
        /// 安全运行协程：捕获 MoveNext 中的异常，防止协程崩溃卡死主流程
        /// 用法：StartCoroutine(SafeCoroutine(MyRoutine()));
        /// </summary>
        public static IEnumerator SafeCoroutine(IEnumerator routine)
        {
            if (routine == null) yield break;
            var enumerator = routine;
            while (true)
            {
                bool hasNext = false;
                object current = null;
                try
                {
                    bool moved = enumerator.MoveNext();
                    hasNext = moved;
                    if (moved) current = enumerator.Current;
                }
                catch (Exception e)
                {
                    Roguelike.Core.GameLog.Error($"协程异常: {e}");
                    yield break;
                }
                if (!hasNext) yield break;
                yield return current;
            }
        }
    }
}