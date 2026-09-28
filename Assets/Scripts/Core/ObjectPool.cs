using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Core
{
    /// <summary>
    /// 通用对象池：Get() 取一个、Return() 还回去，自动扩容、自动开关。
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly Action<T> _onGet;
        private readonly Action<T> _onReturn;
        private int _maxSize;

        public ObjectPool(T prefab, Transform parent = null, int initialSize = 0, int maxSize = 100,
                          Action<T> onGet = null, Action<T> onReturn = null)
        {
            _prefab = prefab;
            _parent = parent;
            _maxSize = maxSize;
            _onGet = onGet;
            _onReturn = onReturn;

            // 预热
            for (int i = 0; i < initialSize; i++)
            {
                var obj = CreateInstance();
                obj.gameObject.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        public T Get()
        {
            T obj;
            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
            }
            else
            {
                obj = CreateInstance();
            }
            obj.gameObject.SetActive(true);
            _onGet?.Invoke(obj);
            return obj;
        }

        public void Return(T obj)
        {
            if (obj == null) return;
            _onReturn?.Invoke(obj);
            obj.gameObject.SetActive(false);
            if (_parent != null) obj.transform.SetParent(_parent, false);
            if (_pool.Count < _maxSize)
                _pool.Enqueue(obj);
            else
                UnityEngine.Object.Destroy(obj.gameObject);
        }

        private T CreateInstance()
        {
            var obj = UnityEngine.Object.Instantiate(_prefab);
            if (_parent != null) obj.transform.SetParent(_parent, false);
            return obj;
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                if (obj != null) UnityEngine.Object.Destroy(obj.gameObject);
            }
        }
    }

    /// <summary>
    /// 全局池管理器：按类型/预制体键管理多个池，提供统一 Get/Return 入口。
    /// </summary>
    public static class PoolManager
    {
        private static readonly Dictionary<string, object> _pools = new Dictionary<string, object>();
        private static Transform _root;

        // 预制体与池配置的绑定（运行时首次 Get 时自动创建）
        private static readonly Dictionary<string, PoolConfig> _configs = new Dictionary<string, PoolConfig>();

        private class PoolConfig
        {
            public GameObject prefab;
            public int initialSize;
            public int maxSize;
            public Transform parent;
            public Action<Component> onGet;
            public Action<Component> onReturn;
        }

        public static void Initialize(Transform root = null)
        {
            if (_root == null)
            {
                var go = new GameObject("PoolManager");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _root = go.transform;
                if (root != null) _root.SetParent(root, false);
            }
        }

        /// <summary>注册预制体的池配置（建议在启动时一次性注册）</summary>
        public static void RegisterPool<T>(string key, GameObject prefab, int initialSize = 0, int maxSize = 50,
                                            Transform parent = null,
                                            Action<T> onGet = null, Action<T> onReturn = null) where T : Component
        {
            _configs[key] = new PoolConfig
            {
                prefab = prefab,
                initialSize = initialSize,
                maxSize = maxSize,
                parent = parent,
                onGet = onGet as Action<Component>,
                onReturn = onReturn as Action<Component>
            };
        }

        private static ObjectPool<T> GetOrCreatePool<T>(string key) where T : Component
        {
            if (_pools.TryGetValue(key, out var existing))
                return (ObjectPool<T>)existing;

            if (!_configs.TryGetValue(key, out var cfg) || cfg.prefab == null)
            {
                Debug.LogError($"[PoolManager] 未注册池配置: {key}");
                return null;
            }

            var parent = cfg.parent != null ? cfg.parent : PoolManager._root;
            var pool = new ObjectPool<T>(cfg.prefab.GetComponent<T>(), cfg.parent, cfg.initialSize, cfg.maxSize,
                                         cfg.onGet as Action<T>, cfg.onReturn as Action<T>);
            _pools[key] = pool;
            return pool;
        }

        /// <summary>从池取一个实例（自动激活、执行 onGet）</summary>
        public static T Get<T>(string key) where T : Component
        {
            var pool = GetOrCreatePool<T>(key);
            return pool?.Get();
        }

        /// <summary>归还实例到池（自动关闭、执行 onReturn）</summary>
        public static void Return<T>(string key, T obj) where T : Component
        {
            if (_pools.TryGetValue(key, out var existing))
            {
                ((ObjectPool<T>)existing).Return(obj);
            }
            else
            {
                UnityEngine.Object.Destroy(obj.gameObject);
            }
        }

        /// <summary>清空并销毁某个池</summary>
        public static void ClearPool(string key)
        {
            if (_pools.TryGetValue(key, out var existing))
            {
                ((ObjectPool<Component>)existing).Clear();
                _pools.Remove(key);
            }
        }

        /// <summary>清空所有池</summary>
        public static void ClearAll()
        {
            foreach (var pool in _pools.Values)
                ((ObjectPool<Component>)pool).Clear();
            _pools.Clear();
        }
    }
}