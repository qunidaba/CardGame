using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>召唤：召唤敌人（value=敌人id，hitCount=数量）</summary>
    public class SummonHandler : BaseIntentHandler
    {
        public override string TypeName => "Summon";
        public override string DisplayName => "召唤（召唤敌人，value=敌人id，hitCount=数量）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            mgr.SummonEnemies(intent.value, Mathf.Max(1, intent.hitCount), attacker);
            yield break;
        }
    }
}