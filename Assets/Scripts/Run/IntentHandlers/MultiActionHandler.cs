using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>多重行动（一回合执行多个子行动）</summary>
    public class MultiActionHandler : BaseIntentHandler
    {
        public override string TypeName => "Multi";
        public override string DisplayName => "多重行动（一回合多动作）";
        public override bool RequiresTarget => false; // 子行动决定

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            if (intent.actions == null || intent.actions.Count == 0) yield break;

            for (int i = 0; i < intent.actions.Count; i++)
            {
                var sub = intent.actions[i];
                var handler = IntentRegistry.Get(sub.type);
                if (handler != null)
                    yield return handler.Execute(mgr, sub, attacker);
                else
                    UnityEngine.Debug.LogWarning($"[意图] 未知子行动类型: {sub.type}");

                if (i < intent.actions.Count - 1)
                    yield return new WaitForSeconds(mgr.hitInterval);
            }
        }
    }
}