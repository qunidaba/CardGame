using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>多重攻击（对玩家多次）</summary>
    public class MultiAttackHandler : BaseIntentHandler
    {
        public override string TypeName => "MultiAttack";
        public override string DisplayName => "多重攻击";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            var target = mgr.GetPlayer();
            if (target == null) yield break;

            int damage = mgr.ApplyCharge(attacker, attacker.DealDamage(GetValue(intent)));
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