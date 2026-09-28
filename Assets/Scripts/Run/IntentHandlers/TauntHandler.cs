using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>嘲讽：强制玩家只能选它</summary>
    public class TauntHandler : BaseIntentHandler
    {
        public override string TypeName => "Taunt";
        public override string DisplayName => "嘲讽（强制玩家只能选它，1 回合）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            int idx = mgr.enemies.IndexOf(attacker);
            if (idx >= 0)
            {
                mgr.GetPlayer().SetStatus(StatusEffectType.Taunt, idx + 1, 1);
                mgr.SetTarget(idx);
                UnityEngine.Debug.Log($"{attacker.Name} 嘲讽：玩家本回合只能攻击它");
            }
            yield break;
        }
    }
}