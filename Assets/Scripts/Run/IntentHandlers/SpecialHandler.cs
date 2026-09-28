using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>特殊技能（留给特定敌人自定义）</summary>
    public class SpecialHandler : BaseIntentHandler
    {
        public override string TypeName => "Special";
        public override string DisplayName => "特殊技能";
        public override bool RequiresTarget => true;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            UnityEngine.Debug.Log($"[敌人] {attacker.Name} 使用特殊技能: {GetDescription(intent)}");
            yield break;
        }
    }
}