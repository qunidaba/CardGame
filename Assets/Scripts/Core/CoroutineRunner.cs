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
}
}