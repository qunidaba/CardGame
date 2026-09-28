using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 意图处理器接口：每种 IntentType 对应一个实现类
    /// </summary>
    public interface IIntentHandler
    {
        /// <summary>意图类型名（与 IntentData.type 字符串一致）</summary>
        string TypeName { get; }

        /// <summary>在编辑器/配置表里显示的中文名</summary>
        string DisplayName { get; }

        /// <summary>是否需要目标敌人</summary>
        bool RequiresTarget { get; }

        /// <summary>执行意图（由 BattleManager 调用）</summary>
        /// <returns>是否已处理完（false 表示需要等待/分帧）</summary>
        IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker);

        /// <summary>生成预览伤害（用于 UI 预览）</summary>
        int GetPreviewDamage(BattleManager mgr, IntentData intent, BattleUnit attacker);
    }

    /// <summary>
    /// 意图处理器注册表：单例，启动时自动注册所有内置 Handler
    /// </summary>
    public static class IntentRegistry
    {
        private static readonly Dictionary<string, IIntentHandler> _handlers = new Dictionary<string, IIntentHandler>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        /// <summary>注册一个 Handler（内部自动调用，外部也可扩展）</summary>
        public static void Register(IIntentHandler handler)
        {
            if (handler == null) return;
            _handlers[handler.TypeName] = handler;
        }

        /// <summary>按类型名获取 Handler（不区分大小写）</summary>
        public static IIntentHandler Get(string typeName)
        {
            EnsureInitialized();
            _handlers.TryGetValue(typeName, out var h);
            return h;
        }

        /// <summary>是否已注册该类型</summary>
        public static bool Has(string typeName)
        {
            EnsureInitialized();
            return _handlers.ContainsKey(typeName);
        }

        /// <summary>获取所有已注册的类型名（用于编辑器下拉）</summary>
        public static IReadOnlyList<string> AllTypes
        {
            get
            {
                EnsureInitialized();
                var list = new List<string>(_handlers.Keys);
                list.Sort();
                return list;
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            // 内置 Handler 自动注册
            Register(new AttackHandler());
            Register(new DefenseHandler());
            Register(new BuffHandler());
            Register(new DebuffHandler());
            Register(new MultiAttackHandler());
            Register(new MultiActionHandler());
            Register(new SequenceHandler());
            Register(new SpecialHandler());
            Register(new TauntHandler());
            Register(new ChargeHandler());
            Register(new SunderHandler());
            Register(new CurseHandler());
            Register(new SwallowHandler());
            Register(new BurrowHandler());
            Register(new SummonHandler());
            Register(new HealAlliesHandler());
        }
    }
}