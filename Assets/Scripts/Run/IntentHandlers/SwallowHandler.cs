using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>吞噬：吞掉玩家随机 N 张牌，被攻击 N 次后吐出来</summary>
    public class SwallowHandler : BaseIntentHandler
    {
        public override string TypeName => "Swallow";
        public override string DisplayName => "吞噬（受击伤害固定为1，被攻击 N 次后吐出）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            int hits = Mathf.Max(1, intent.value);
            attacker.AddStatus(StatusEffectType.Swallow, hits, -1);
            UnityEngine.Debug.Log($"{attacker.Name} 吞地：受到的攻击伤害固定为 1，还需 {hits} 次攻击才会出来");
            yield break;
        }
    }
}