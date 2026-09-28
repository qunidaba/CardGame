using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>遁地：受到的攻击伤害固定为 1，层数 = 还需被攻击的次数</summary>
    public class BurrowHandler : BaseIntentHandler
    {
        public override string TypeName => "Burrow";
        public override string DisplayName => "遁地（受到的攻击伤害固定为1，被攻击 N 次后出来）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            if (attacker.GetStatusAmount(StatusEffectType.Burrow) > 0)
            {
                UnityEngine.Debug.Log($"{attacker.Name} 已经遁地，无法再次遁地");
                yield break;
            }

            int burrowHits = Mathf.Max(1, intent.value);
            attacker.AddStatus(StatusEffectType.Burrow, burrowHits, -1);
            UnityEngine.Debug.Log($"{attacker.Name} 遁地：受到的攻击伤害固定为 1，还需 {burrowHits} 次攻击才会出来");
            yield break;
        }
    }
}