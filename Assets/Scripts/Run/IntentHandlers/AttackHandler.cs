using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>普通攻击（打玩家）</summary>
    public class AttackHandler : BaseIntentHandler
    {
        public override string TypeName => "Attack";
        public override string DisplayName => "攻击";
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