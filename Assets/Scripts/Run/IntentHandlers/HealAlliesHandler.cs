using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>全体回血：给所有存活友方回血</summary>
    public class HealAlliesHandler : BaseIntentHandler
    {
        public override string TypeName => "HealAllies";
        public override string DisplayName => "全体回血（给所有友方回血）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            int amount = Mathf.Max(0, intent.value);
            int count = 0;
            foreach (var unit in mgr.enemies)
            {
                if (unit == null || unit.IsDead) continue;
                unit.Heal(amount);
                count++;
            }
            UnityEngine.Debug.Log($"{attacker.Name} 为全体队友回复 {amount} 生命（{count} 个）");
            yield break;
        }
    }
}