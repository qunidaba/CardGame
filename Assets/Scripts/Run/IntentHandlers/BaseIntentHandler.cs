using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 基础意图处理器：提供通用字段读取、预览伤害计算等公共逻辑
    /// </summary>
    public abstract class BaseIntentHandler : IIntentHandler
    {
        public abstract string TypeName { get; }
        public abstract string DisplayName { get; }
        public virtual bool RequiresTarget => true;

        /// <summary>获取意图配置的数值（value 字段）</summary>
        protected int GetValue(IntentData intent) => intent.value;

        /// <summary>获取意图配置的命中次数</summary>
        protected int GetHitCount(IntentData intent) => Mathf.Max(1, intent.hitCount);

        /// <summary>获取意图配置的描述文本</summary>
        protected string GetDescription(IntentData intent) => intent.description;

        /// <summary>获取意图配置的状态名</summary>
        protected string GetStatus(IntentData intent) => intent.status;

        /// <summary>获取意图配置的持续回合</summary>
        protected int GetDuration(IntentData intent) => intent.duration;

        /// <summary>判断是否为多重攻击</summary>
        protected bool IsMultiHit(IntentData intent) => intent.multiHit;

        /// <summary>获取子行动列表</summary>
        protected List<IntentData> GetSubActions(IntentData intent) => intent.actions;

        /// <summary>解析状态字符串为 StatusEffectType</summary>
        protected StatusEffectType ParseStatus(string status, StatusEffectType fallback)
            => BattleManager.ParseStatus(status, fallback);

        /// <summary>预览伤害：基础实现（可被子类重写）</summary>
        public virtual int GetPreviewDamage(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            if (attacker == null) return 0;
            int dmg = attacker.DealDamage(GetValue(intent));
            if (dmg > 0 && attacker.GetStatusAmount(StatusEffectType.Charge) > 0)
                dmg *= 2;
            return mgr.GetPlayer()?.StatusEffects.PreviewTakeDamage(dmg) ?? dmg;
        }

        /// <summary>执行意图（默认抛出 NotImplemented，子类必须实现）</summary>
        public virtual IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            throw new NotImplementedException($"{TypeName} 必须实现 Execute 方法");
        }
    }
}