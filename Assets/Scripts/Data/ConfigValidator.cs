using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Data
{
    /// <summary>
    /// 配置校验：加载完成后跑一遍，把「id 冲突 / 引用不存在 / 数值非法 / 枚举拼错」等问题一次性报出来。
    /// 目的：让配置错误在启动时立刻暴露，而不是玩到一半才莫名其妙地不生效。
    /// </summary>
    public static class ConfigValidator
    {
        /// <summary>校验全部配置，返回错误数量（0 = 通过，>0 有错误）</summary>
        public static int Validate(AllConfig cfg)
        {
            if (cfg == null) return 0;

            var errors = new List<string>();
            var warns = new List<string>();

            ValidateEnemies(cfg, errors, warns);
            ValidateEncounters(cfg, errors, warns);
            ValidateActs(cfg, errors, warns);
            ValidateRelics(cfg, errors, warns);
            ValidateEnchantments(cfg, errors, warns);
            ValidateEvents(cfg, errors, warns);
            ValidatePotions(cfg, errors, warns);

            // 先打印警告（提示），再打印错误
            if (warns.Count > 0)
                Debug.LogWarning($"[ConfigValidator] 发现 {warns.Count} 个提示：\n  " + string.Join("\n  ", warns));
            if (errors.Count > 0)
            {
                Debug.LogError($"[ConfigValidator] 发现 {errors.Count} 个错误：\n  " + string.Join("\n  ", errors));
            }
            if (errors.Count == 0)
            {
                if (warns.Count == 0)
                    Debug.Log("[ConfigValidator] 配置校验通过");
                else
                    Debug.Log("[ConfigValidator] 校验通过（含提示）");
            }
            return errors.Count; // 只返回错误数
        }

        // ===== 各类配置 =====

        private static void ValidateEnemies(AllConfig cfg, List<string> errors, List<string> warns)
        {
            var seen = new HashSet<int>();
            foreach (var e in cfg.enemies)
            {
                if (e == null) continue;

                if (!seen.Add(e.id)) errors.Add($"敌人 id 重复：{e.id}");
                if (string.IsNullOrEmpty(e.name)) errors.Add($"敌人 id={e.id} 没有名字");
                if (e.hp <= 0) errors.Add($"敌人「{e.name}」(id={e.id}) 的 hp <= 0");
                if (e.maxHp <= 0) errors.Add($"敌人「{e.name}」(id={e.id}) 的 maxHp <= 0");
                if (e.intents == null || e.intents.Count == 0)
                    errors.Add($"敌人「{e.name}」(id={e.id}) 没有任何意图（战斗时只能默认普攻）");

                if (!string.IsNullOrEmpty(e.passive) && !IsValidEnum<EnemyPassive>(e.passive))
                    errors.Add($"敌人「{e.name}」(id={e.id}) 的 passive='{e.passive}' 不是合法被动");

                foreach (var intent in e.intents ?? new List<IntentData>())
                    ValidateIntent(cfg, intent, $"敌人「{e.name}」(id={e.id})", errors, warns);
            }
        }

        private static void ValidateIntent(AllConfig cfg, IntentData intent, string owner, List<string> errors, List<string> warns)
        {
            if (intent == null) return;

            if (!IsValidEnum<IntentType>(intent.type))
            {
                errors.Add($"{owner} 的意图 type='{intent.type}' 不是合法意图类型（拼写错误？）");
                return;
            }

            if (intent.weight < 0) warns.Add($"{owner} 的意图「{intent.type}」权重为负");

            switch (intent.type)
            {
                case "Summon":
                    if (FindEnemy(cfg, intent.value) == null)
                        errors.Add($"{owner} 的召唤意图引用了不存在的敌人 id={intent.value}");
                    if (intent.hitCount < 1)
                        warns.Add($"{owner} 的召唤数量 hitCount={intent.hitCount} < 1");
                    break;

                case "Curse":
                {
                    var ench = FindEnchantment(cfg, intent.value);
                    if (ench == null)
                        errors.Add($"{owner} 的诅咒意图引用了不存在的附魔 id={intent.value}");
                    else if (ench.rarity != "Curse")
                        warns.Add($"{owner} 的诅咒意图附魔「{ench.name}」不是 Curse 稀有度");
                    break;
                }

                case "Buff":
                case "Debuff":
                    if (!string.IsNullOrEmpty(intent.status) && !IsValidEnum<StatusEffectType>(intent.status))
                        errors.Add($"{owner} 的 {intent.type} 状态 '{intent.status}' 不是合法状态");
                    break;
            }

            foreach (var sub in intent.actions ?? new List<IntentData>())
                ValidateIntent(cfg, sub, owner + " 的子行动", errors, warns);
        }

        private static void ValidateEncounters(AllConfig cfg, List<string> errors, List<string> warns)
        {
            var seen = new HashSet<int>();
            foreach (var enc in cfg.encounters)
            {
                if (enc == null) continue;

                if (!seen.Add(enc.id)) errors.Add($"遭遇 id 重复：{enc.id}");
                if (string.IsNullOrEmpty(enc.pool)) warns.Add($"遭遇「{enc.name}」(id={enc.id}) 没有 pool");
                if (enc.enemies == null || enc.enemies.Count == 0)
                    errors.Add($"遭遇「{enc.name}」(id={enc.id}) 没有配置敌人");
                else
                    foreach (int id in enc.enemies)
                        if (FindEnemy(cfg, id) == null)
                            errors.Add($"遭遇「{enc.name}」(id={enc.id}) 引用了不存在的敌人 id={id}");

                if (enc.weight < 0) warns.Add($"遭遇「{enc.name}」(id={enc.id}) 权重为负");
            }
        }

        private static void ValidateActs(AllConfig cfg, List<string> errors, List<string> warns)
        {
            var seen = new HashSet<int>();
            foreach (var act in cfg.acts)
            {
                if (act == null) continue;

                if (!seen.Add(act.actId)) errors.Add($"章节 actId 重复：{act.actId}");
                if (string.IsNullOrEmpty(act.name)) errors.Add($"章节 actId={act.actId} 没有名字");

                if (FindEnemy(cfg, act.bossEnemyId) == null)
                    errors.Add($"章节 {act.actId} 的 bossEnemyId={act.bossEnemyId} 不存在");

                // 普通怪池：必须能开战（有固定组合，或有敌人挂了该池）
                if (!PoolHasContent(cfg, act.commonPool))
                    errors.Add($"章节 {act.actId} 的 commonPool '{act.commonPool}' 里没有任何遭遇/敌人（战斗开不了）");

                // 精英怪池：有精英概率时必须能开战，否则只提示
                bool eliteNeeded = act.eliteChancePerBattle != null && act.eliteChancePerBattle.Exists(c => c > 0f);
                if (!PoolHasContent(cfg, act.elitePool))
                {
                    if (eliteNeeded)
                        errors.Add($"章节 {act.actId} 配了精英概率，但 elitePool '{act.elitePool}' 里没有任何遭遇/敌人");
                    else
                        warns.Add($"（提示）章节 {act.actId} 的 elitePool '{act.elitePool}' 为空，当前不会出精英");
                }

                // 后半普通池：可选，空则沿用前半
                if (!string.IsNullOrEmpty(act.commonPoolLate) && !PoolHasContent(cfg, act.commonPoolLate))
                    warns.Add($"（提示）章节 {act.actId} 的 commonPoolLate '{act.commonPoolLate}' 为空，后半段会沿用前半池");
            }
        }

        private static void ValidateRelics(AllConfig cfg, List<string> errors, List<string> warns)
        {
            var seen = new HashSet<int>();
            foreach (var r in cfg.relics)
            {
                if (r == null) continue;

                if (!seen.Add(r.id)) errors.Add($"遗物 id 重复：{r.id}");
                if (string.IsNullOrEmpty(r.name)) errors.Add($"遗物 id={r.id} 没有名字");
                if (r.effects == null || r.effects.Count == 0)
                    warns.Add($"（提示）遗物「{r.name}」(id={r.id}) 没有任何效果");

                // 注意：遗物的 trigger / type 是「开放字符串」（由 RelicEffectProcessor 的字符串分支处理），
                // 不在 RelicEffectType 枚举里，所以这里不做枚举校验，否则会大量误报。
            }
        }

        private static void ValidateEnchantments(AllConfig cfg, List<string> errors, List<string> warns)
        {
            var seen = new HashSet<int>();
            foreach (var e in cfg.enchantments)
            {
                if (e == null) continue;

                if (!seen.Add(e.id)) errors.Add($"附魔 id 重复：{e.id}");
                if (string.IsNullOrEmpty(e.name)) errors.Add($"附魔 id={e.id} 没有名字");

                // 同遗物：trigger / effect type 是开放字符串，不校验枚举。
                // 但 effect 的 status 必须是真正的状态名（会被 ParseStatus 解析）。
                foreach (var eff in e.effects ?? new List<EnchantmentEffectData>())
                {
                    if (eff == null) continue;
                    if (!string.IsNullOrEmpty(eff.status) && !IsValidEnum<StatusEffectType>(eff.status))
                        errors.Add($"附魔「{e.name}」(id={e.id}) 的状态 '{eff.status}' 不是合法状态");
                }
            }
        }

        private static void ValidateEvents(AllConfig cfg, List<string> errors, List<string> warns)
        {
            var seen = new HashSet<int>();
            foreach (var ev in cfg.events)
            {
                if (ev == null) continue;

                if (!seen.Add(ev.id)) errors.Add($"事件 id 重复：{ev.id}");
                if (string.IsNullOrEmpty(ev.title)) errors.Add($"事件 id={ev.id} 没有标题");
                if (ev.options == null || ev.options.Count == 0)
                    errors.Add($"事件「{ev.title}」(id={ev.id}) 没有任何选项");

                foreach (var opt in ev.options ?? new List<EventOptionData>())
                {
                    if (opt == null) continue;
                    foreach (var res in opt.results ?? new List<EventResultData>())
                        ValidateEventResult(cfg, res, $"事件「{ev.title}」(id={ev.id})", errors, warns);
                }
            }
        }

        private static void ValidateEventResult(AllConfig cfg, EventResultData res, string owner, List<string> errors, List<string> warns)
        {
            if (res == null) return;

            if (!IsValidEnum<EventResultType>(res.type))
            {
                errors.Add($"{owner} 的结果 type='{res.type}' 不是合法结果类型（拼写错误？）");
                return;
            }

            // StartCombatWithReward：enemyId 必须存在
            if (res.type == "StartCombatWithReward" && res.enemyId != 0 && FindEnemy(cfg, res.enemyId) == null)
                errors.Add($"{owner} 的 StartCombatWithReward 引用了不存在的敌人 id={res.enemyId}");

            foreach (var sub in res.outcomes ?? new List<EventResultData>())
                ValidateEventResult(cfg, sub, owner + " 的分支", errors, warns);
            foreach (var sub in res.win ?? new List<EventResultData>())
                ValidateEventResult(cfg, sub, owner + " 的 win", errors, warns);
            foreach (var sub in res.lose ?? new List<EventResultData>())
                ValidateEventResult(cfg, sub, owner + " 的 lose", errors, warns);
        }

        private static void ValidatePotions(AllConfig cfg, List<string> errors, List<string> warns)
        {
            var seen = new HashSet<int>();
            foreach (var p in cfg.potions)
            {
                if (p == null) continue;

                if (!seen.Add(p.id)) errors.Add($"药水 id 重复：{p.id}");
                if (string.IsNullOrEmpty(p.name)) errors.Add($"药水 id={p.id} 没有名字");

                if (p.effect != null && !IsValidEnum<PotionEffectType>(p.effect.type))
                    errors.Add($"药水「{p.name}」(id={p.id}) 的 effect type='{p.effect.type}' 不合法");
            }
        }

        // ===== 工具 =====

        private static bool IsValidEnum<T>(string s) where T : struct
            => !string.IsNullOrEmpty(s) && Enum.TryParse<T>(s, false, out _);

        private static EnemyData FindEnemy(AllConfig cfg, int id)
            => cfg.enemies.Find(e => e != null && e.id == id);

        private static EnchantmentData FindEnchantment(AllConfig cfg, int id)
            => cfg.enchantments.Find(e => e != null && e.id == id);

        /// <summary>池里有没有东西：固定组合 或 挂了该池的敌人（和 RunDirector.PoolHasContent 一致）</summary>
        private static bool PoolHasContent(AllConfig cfg, string pool)
        {
            if (string.IsNullOrEmpty(pool)) return false;
            if (cfg.encounters.Exists(e => e != null && e.pool == pool)) return true;
            return cfg.enemies.Exists(e => e != null && e.pools != null && e.pools.Contains(pool));
        }
    }
}