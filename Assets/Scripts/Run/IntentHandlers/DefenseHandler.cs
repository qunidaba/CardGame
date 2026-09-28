using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>防御</summary>
    public class DefenseHandler : BaseIntentHandler
    {
        public override string TypeName => "Defense";
        public override string DisplayName => "防御";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            attacker.AddDefense(GetValue(intent));
            UnityEngine.Debug.Log($"[意图] {attacker.Name} 进入防御姿态，获得 {GetValue(intent)} 点防御");
            yield break;
        }
    }
}