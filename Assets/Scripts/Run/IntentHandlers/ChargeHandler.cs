using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>蓄力：下次攻击伤害翻倍</summary>
    public class ChargeHandler : BaseIntentHandler
    {
        public override string TypeName => "Charge";
        public override string DisplayName => "蓄力（下次攻击伤害翻倍）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            attacker.AddStatus(StatusEffectType.Charge, 1, -1);
            UnityEngine.Debug.Log($"{attacker.Name} 蓄力：下次攻击伤害翻倍");
            yield break;
        }
    }
}