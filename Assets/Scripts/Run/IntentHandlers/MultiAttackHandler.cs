using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>多重攻击（单目标多次）</summary>
    public class MultiAttackHandler : BaseIntentHandler
    {
        public override string TypeName => "MultiAttack";
        public override string DisplayName => "多重攻击";
        public override bool RequiresTarget => true;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            var target = mgr.GetEnemy();
            if (target == null) yield break;

            int damage = attacker.DealDamage(GetValue(intent));
            int hits = GetHitCount(intent);

            for (int i = 0; i < hits; i++)
            {
                if (attacker.IsDead || target.IsDead) yield break;
                target.TakeDamage(damage, attacker);
                mgr.RelicProcessor?.ApplyEffects("OnTakeDamage", mgr.BuildContext());
                if (i < hits - 1) yield return new WaitForSeconds(mgr.hitInterval);
            }
        }
    }
}