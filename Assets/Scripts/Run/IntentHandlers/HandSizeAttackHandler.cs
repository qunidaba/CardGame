using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>
    /// 抽髓：伤害 = value + 玩家当前手牌数，命中 hitCount 次。
    /// 专门克制"囤手牌"（如缚茧蛛保留手牌机制）。
    /// </summary>
    public class HandSizeAttackHandler : BaseIntentHandler
    {
        public override string TypeName => "HandSizeAttack";
        public override string DisplayName => "抽髓（伤害 + 玩家手牌数，多段）";
        public override bool RequiresTarget => false;

        private int CalcDamage(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            int hand = mgr.GetHandArea() != null ? mgr.GetHandArea().Count : 0;
            return attacker.DealDamage(GetValue(intent) + hand);
        }

        public override int GetPreviewDamage(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            if (attacker == null) return 0;
            int dmg = CalcDamage(mgr, intent, attacker);
            if (dmg > 0 && attacker.GetStatusAmount(StatusEffectType.Charge) > 0)
                dmg *= 2;
            return mgr.GetPlayer()?.StatusEffects.PreviewTakeDamage(dmg) ?? dmg;
        }

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            var target = mgr.GetPlayer();
            if (target == null) yield break;

            int damage = mgr.ApplyCharge(attacker, CalcDamage(mgr, intent, attacker));
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
