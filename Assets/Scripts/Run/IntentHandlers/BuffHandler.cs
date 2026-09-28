using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>给自己加 Buff（状态）</summary>
    public class BuffHandler : BaseIntentHandler
    {
        public override string TypeName => "Buff";
        public override string DisplayName => "强化";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            var statusType = !string.IsNullOrEmpty(GetStatus(intent))
                ? ParseStatus(GetStatus(intent), StatusEffectType.Strength)
                : StatusEffectType.Strength;

            int duration = GetDuration(intent);
            attacker.AddStatus(statusType, GetValue(intent), duration);
            UnityEngine.Debug.Log($"[意图] {attacker.Name} 获得状态 {statusType} x{GetValue(intent)}（持续 {duration}）");
            yield break;
        }
    }
}