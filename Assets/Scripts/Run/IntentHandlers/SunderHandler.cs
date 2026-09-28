using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>破防：对玩家的防御伤害翻倍（防御只能抵消一半，向上取整）</summary>
    public class SunderHandler : BaseIntentHandler
    {
        public override string TypeName => "Sunder";
        public override string DisplayName => "破防（对防御伤害翻倍）";
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
                UnityEngine.Debug.Log($"[敌人] {attacker.Name} 破防攻击，造成 {damage} 点伤害（防御只能挡一半）");
                target.TakeDamage(damage, attacker, halveDefense: true);
                mgr.RelicProcessor?.ApplyEffects("OnTakeDamage", mgr.BuildContext());
                if (i < hits - 1) yield return new WaitForSeconds(mgr.hitInterval);
            }
        }
    }
}