using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// BattleManager 分部类 —— 敌人相关：
/// 意图选择/执行、召唤、吞噬、弱点、嘲讽、序列队列。
/// （纯拆分，逻辑未改动）
/// </summary>
public partial class BattleManager
{
    /// <summary>
    /// 根据权重从敌人数据中选择意图
    /// </summary>
    // 每个敌人各自的「序列行动」剩余队列（本场战斗内有效）
    public readonly Dictionary<BattleUnit, List<Roguelike.Data.IntentData>> queuedIntents =
        new Dictionary<BattleUnit, List<Roguelike.Data.IntentData>>();

    /// <summary>敌人被 DoT 打死时，等受击表现播完再结算胜负的延时</summary>
    private const float deathVisualDelay = 0.5f;

    /// <summary>当前目标的序列行动还剩几步（0 = 不在序列中）</summary>
    public int QueuedIntentCount => GetQueuedCount(GetEnemy());

    /// <summary>指定敌人还剩几步序列行动</summary>
    public int GetQueuedCount(BattleUnit unit)
        => (unit != null && queuedIntents.TryGetValue(unit, out var q)) ? q.Count : 0;

    /// <summary>
    /// 为指定敌人选择一个意图（每个敌人各自一份）。
    /// preferBattleStart = true 时（战斗第一回合），勾了 battleStart 的意图必定被选中；
    /// 这些意图平时也留在随机池里，后续回合仍可能被抽到。
    /// </summary>
    private Roguelike.Data.IntentData SelectEnemyIntent(BattleUnit unit, bool preferBattleStart = false)
    {
        if (unit == null) return null;

        // 0) 第一回合：勾了 battleStart 的意图必定出场
        if (preferBattleStart)
        {
            var firstData = GetEnemyData(unit);
            if (firstData != null && firstData.intents != null)
            {
                var forced = firstData.intents.Find(i => i != null && i.battleStart);
                if (forced != null)
                {
                    Debug.Log($"[敌人] {unit.Name} 第一回合必定使用「{forced.type}」");
                    return forced;
                }
            }
        }

        // 1) 该敌人的序列行动还没走完：按顺序取下一个
        if (queuedIntents.TryGetValue(unit, out var queue) && queue.Count > 0)
        {
            var next = queue[0];
            queue.RemoveAt(0);
            Debug.Log($"[敌人] {unit.Name} 序列行动：执行「{next.type}」（剩余 {queue.Count} 步）");
            return next;
        }

        var data = GetEnemyData(unit);
        if (data == null || data.intents == null || data.intents.Count == 0)
        {
            // 默认行为：普通攻击
            return new Roguelike.Data.IntentData { type = "Attack", value = 5, hitCount = 1 };
        }

        // 2) 遁地：未遁地时优先使用遁地；已遁地时不能再用（从随机池里排除）
        bool burrowed = unit.GetStatusAmount(StatusEffectType.Burrow) > 0;
        if (!burrowed)
        {
            var burrowIntent = data.intents.Find(i => i != null && i.type == "Burrow");
            if (burrowIntent != null)
            {
                Debug.Log($"[敌人] {unit.Name} 优先使用「遁地」");
                return burrowIntent;
            }
        }

        // 3) 权重随机（已遁地时排除「遁地」；敌人满员时排除「召唤」；「第一回合必定出现」的那些后续也能随机到）
        bool enemiesFull = enemies.Count >= MaxEnemyCount;
        var candidates = data.intents.FindAll(i =>
        {
            if (i == null) return false;
            if (burrowed && i.type == "Burrow") return false;
            if (enemiesFull && i.type == "Summon") return false;
            return true;
        });
        if (candidates.Count == 0) candidates = data.intents;

        int totalWeight = 0;
        foreach (var intent in candidates) totalWeight += Mathf.Max(0, intent.weight);
        if (totalWeight <= 0) return candidates[0];

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;
        Roguelike.Data.IntentData picked = candidates[0];
        foreach (var intent in candidates)
        {
            cumulative += Mathf.Max(0, intent.weight);
            if (roll < cumulative)
            {
                picked = intent;
                break;
            }
        }

        // 4) 抽到「序列行动」：整段排入队列，本回合先执行第一步，之后几回合按顺序固定执行
        if (picked.type == "Sequence" && picked.actions != null && picked.actions.Count > 0)
        {
            var newQueue = new List<Roguelike.Data.IntentData>(picked.actions);
            var first = newQueue[0];
            newQueue.RemoveAt(0);
            queuedIntents[unit] = newQueue;
            Debug.Log($"[敌人] {unit.Name} 触发序列行动：共 {picked.actions.Count} 步，本回合执行「{first.type}」");
            return first;
        }

        return picked;
    }

    /// <summary>取指定敌人的配置数据</summary>
    public Roguelike.Data.EnemyData GetEnemyData(BattleUnit unit)
    {
        int idx = enemies.IndexOf(unit);
        return (idx >= 0 && idx < enemyDatas.Count) ? enemyDatas[idx] : null;
    }

    /// <summary>取当前目标的配置数据</summary>
    public Roguelike.Data.EnemyData GetEnemyData() => GetEnemyData(GetEnemy());

    /// <summary>为所有敌人选择本回合意图</summary>
    private void SelectAllEnemyIntents(bool preferBattleStart = false)
    {
        foreach (var unit in enemies)
        {
            if (unit == null || unit.IsDead) continue;
            enemyIntents[unit] = SelectEnemyIntent(unit, preferBattleStart);
        }
    }

    /// <summary>
    /// 执行指定敌人的意图（type=Multi 会依次执行其子行动）
    /// </summary>
    private IEnumerator ExecuteOneIntent(Roguelike.Data.IntentData intent, BattleUnit attacker)
    {
        if (intent == null || attacker == null) yield break;

        // 一回合多行动：依次执行子行动
        if (intent.type == "Multi")
        {
            if (intent.actions != null && intent.actions.Count > 0)
            {
                for (int a = 0; a < intent.actions.Count; a++)
                {
                    yield return ExecuteOneIntent(intent.actions[a], attacker);
                    if (a < intent.actions.Count - 1)
                        yield return new WaitForSeconds(hitInterval);
                }
            }
            yield break;
        }

// 通过 IntentRegistry 分发执行（所有具体意图逻辑已移至对应 Handler）
        var handler = Roguelike.IntentRegistry.Get(intent.type);
        if (handler != null)
        {
            yield return handler.Execute(this, intent, attacker);
        }
        else
        {
            Debug.LogWarning($"[BattleManager] 敌人意图类型无效（请在配置里选择类型）: '{intent.type}'");
        }
    }

    /// <summary>字符串转 StatusEffectType（不区分大小写），失败返回 fallback</summary>
        public static StatusEffectType ParseStatus(string status, StatusEffectType fallback)
    {
        if (!string.IsNullOrEmpty(status) &&
            System.Enum.TryParse(status, true, out StatusEffectType parsed))
            return parsed;
        return fallback;
    }

    /// <summary>
    /// 蓄力：攻击行动伤害翻倍，并消耗 1 层（每层对应一次攻击行动）
    /// </summary>
    public int ApplyCharge(BattleUnit attacker, int damage)
    {
        if (attacker == null || damage <= 0) return damage;
        if (attacker.GetStatusAmount(StatusEffectType.Charge) <= 0) return damage;

        attacker.RemoveStatus(StatusEffectType.Charge, 1);
        Debug.Log($"[蓄力] {attacker.Name} 伤害翻倍：{damage} → {damage * 2}");
        return damage * 2;
    }

    /// <summary>
    /// 给玩家随机 N 张牌附加指定诅咒（本场战斗临时，战斗结束自动消失）。
    /// curseId = 附魔表里 Curse 稀有度的附魔 id（如 68 禁锢 / 69 割裂 / 71 无力）。
    /// </summary>
    /// <summary>给玩家随机 N 张牌附加指定诅咒（供 Handler 调用）</summary>
        public void ApplyRandomCurse(int curseId, int count, BattleUnit attacker)
    {
        if (runData == null || count <= 0) return;

        var curse = Roguelike.Data.ConfigLoader.GetEnchantment(curseId);
        if (curse == null)
        {
            Debug.LogWarning($"[诅咒] 附魔 id {curseId} 不存在，敌人能力无效（请在配置里填 Curse 稀有度的附魔 id）");
            return;
        }

        // 候选：52 张里还没被这个诅咒附过的牌
        var candidates = new List<(int rank, Suit suit)>();
        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            for (int rank = 2; rank <= 14; rank++)
            {
                if (runData.GetCardEnchantments(rank, suit).Contains(curseId)) continue;
                candidates.Add((rank, suit));
            }
        }
        if (candidates.Count == 0) return;

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int pick = UnityEngine.Random.Range(0, candidates.Count);
            var (rank, suit) = candidates[pick];
            candidates.RemoveAt(pick);

            runData.AddTempEnchantment(rank, suit, curseId);
            Debug.Log($"{attacker?.Name} 诅咒：{CardData.RankLabel(rank)}{suit} 获得「{curse.name}」（本场战斗）");
        }
    }

    // ===== 召唤 =====

    /// <summary>
    /// 召唤敌人：最多补到上限（MaxEnemyCount）。满员时召唤失败。
    /// </summary>
    public void SummonEnemies(int enemyId, int count, BattleUnit summoner)
    {
        int room = MaxEnemyCount - enemies.Count;
        if (room <= 0)
        {
            Debug.Log($"{summoner?.Name} 召唤失败：敌人已满（{enemies.Count}/{MaxEnemyCount}）");
            return;
        }

        var data = Roguelike.Data.ConfigLoader.GetEnemy(enemyId);
        if (data == null)
        {
            Debug.LogWarning($"[召唤] 找不到敌人 id {enemyId}（请在配置里填正确的敌人 id）");
            return;
        }

        int n = Mathf.Min(Mathf.Max(1, count), room);
        for (int i = 0; i < n; i++)
            AddEnemyUnit(data);

        OnEnemyListChanged?.Invoke();   // UI 重建槽位
        Debug.Log($"{summoner?.Name} 召唤了 {n} 个「{data.name}」（敌人 {enemies.Count}/{MaxEnemyCount}）");
    }

    /// <summary>把一个敌人配置加入战斗（召唤用；会订阅它的事件）</summary>
    private BattleUnit AddEnemyUnit(Roguelike.Data.EnemyData data)
    {
        if (data == null) return null;

        // 同名敌人加编号，便于区分
        string displayName = data.name;
        int sameCount = 0;
        foreach (var d in enemyDatas)
            if (d != null && d.name == data.name) sameCount++;
        if (sameCount > 0) displayName = $"{data.name} {sameCount + 1}";

        var unit = new BattleUnit(displayName, data.hp);
        unit.MaxHp = data.maxHp > 0 ? data.maxHp : data.hp;   // maxHp 没配就按 hp
        unit.OnTakeDamageCallback = dmg => enchantmentSystem?.OnDealDamage(dmg);

        enemies.Add(unit);
        enemyDatas.Add(data);
        SubscribeEnemyUnit(unit);
        return unit;
    }

    /// <summary>当前生效的嘲讽者下标（没有嘲讽/嘲讽者已阵亡则返回 -1）</summary>
    public int GetTauntTargetIndex()
    {
        if (player == null) return -1;

        int amount = player.GetStatusAmount(StatusEffectType.Taunt);
        if (amount <= 0) return -1;

        int idx = amount - 1;   // 存的是 敌人下标 + 1
        if (idx < 0 || idx >= enemies.Count) return -1;
        if (enemies[idx] == null || enemies[idx].IsDead) return -1;
        return idx;
    }

    /// <summary>嘲讽者阵亡后移除玩家身上的嘲讽状态（避免状态栏残留）</summary>
    private void RefreshTauntStatus()
    {
        if (player == null) return;
        if (player.GetStatusAmount(StatusEffectType.Taunt) <= 0) return;
        if (GetTauntTargetIndex() >= 0) return;   // 嘲讽者还活着

        player.RemoveStatus(StatusEffectType.Taunt);
        Debug.Log("[嘲讽] 嘲讽者已阵亡，移除嘲讽");
    }

}
