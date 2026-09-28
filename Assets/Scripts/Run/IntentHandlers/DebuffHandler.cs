using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>给玩家上 Debuff</summary>
    public class DebuffHandler : BaseIntentHandler
    {
        public override string TypeName => "Debuff";
        public override string DisplayName => "减益（给玩家上状态）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            var target = mgr.GetPlayer();
            if (target == null) yield break;

            var statusType = !string.IsNullOrEmpty(GetStatus(intent))
                ? ParseStatus(GetStatus(intent), StatusEffectType.Weaken)
                : StatusEffectType.Weaken;

            target.AddStatus(statusType, GetValue(intent), GetDuration(intent));
            UnityEngine.Debug.Log($"[意图] 玩家获得状态 {statusType} x{GetValue(intent)}（持续 {GetDuration(intent)}）");
            yield break;
        }
    }
}