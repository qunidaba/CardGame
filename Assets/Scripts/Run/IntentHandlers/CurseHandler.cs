using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>诅咒：给玩家随机 N 张牌附加指定诅咒</summary>
    public class CurseHandler : BaseIntentHandler
    {
        public override string TypeName => "Curse";
        public override string DisplayName => "诅咒（给玩家随机牌加指定诅咒）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            mgr.ApplyRandomCurse(intent.value, Mathf.Max(1, intent.hitCount), attacker);
            yield break;
        }
    }
}