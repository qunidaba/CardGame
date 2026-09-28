using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>吞噬：吞掉玩家抽牌堆里随机 N 张牌，敌人死亡时归还</summary>
    public class SwallowHandler : BaseIntentHandler
    {
        public override string TypeName => "Swallow";
        public override string DisplayName => "吞噬（吞掉玩家抽牌堆随机牌，敌人死亡归还）";
        public override bool RequiresTarget => false;

        public override IEnumerator Execute(BattleManager mgr, IntentData intent, BattleUnit attacker)
        {
            // 从玩家抽牌堆随机吞 value 张牌（挂在敌人身上，死亡时归还）
            mgr.ApplySwallow(Mathf.Max(1, intent.value), attacker);
            yield break;
        }
    }
}