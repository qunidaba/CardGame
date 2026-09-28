using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>序列行动（接下来几回合固定执行）</summary>
    public class SequenceHandler : BaseIntentHandler
    {
        public override string TypeName => "Sequence";
        public override string DisplayName => "序列行动（接下来几回合固定执行）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            if (intent.actions != null && intent.actions.Count > 0)
            {
                var newQueue = new List<IntentData>(intent.actions);
                var first = newQueue[0];
                newQueue.RemoveAt(0);
                mgr.queuedIntents[attacker] = newQueue;
                UnityEngine.Debug.Log($"[敌人] {attacker.Name} 触发序列行动：共 {intent.actions.Count} 步，本回合执行「{first.type}」");
                var handler = IntentRegistry.Get(first.type);
                if (handler != null)
                    yield return handler.Execute(mgr, first, attacker);
                else
                {
                    UnityEngine.Debug.LogWarning($"[意图] 未知子行动类型: {first.type}");
                    yield break;
                }
            }
            else
            {
                yield break;
            }
        }
    }
}