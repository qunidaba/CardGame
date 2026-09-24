using System;
using System.Collections.Generic;
using Roguelike.Data;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// 遗物系统：槽位管理、获取/丢弃/效果查询
    /// 纯查询层，不包含任何应用逻辑，不直接操作 BattleUnit
    /// BattleManager 负责根据查询结果应用效果并触发事件
    /// </summary>
    public class RelicSystem
    {
        public const int MaxSlots = 6;

        private RunData runData;

        /// <summary>
        /// 遗物列表变更通知
        /// </summary>
        public event Action OnDataChanged;

        public void Initialize(RunData runData)
        {
            this.runData = runData;
        }

        // ===== CRUD =====

        public bool TryAddRelic(int relicId, out string error)
        {
            error = null;
            var relic = ConfigLoader.GetRelic(relicId);
            if (relic == null) { error = "遗物不存在"; return false; }
            if (runData.relicIds.Contains(relicId)) { error = "已拥有该遗物"; return false; }

            if (runData.relicIds.Count >= MaxSlots)
            {
                error = $"遗物槽已满（最多 {MaxSlots} 个）";
                return false;
            }

            runData.TryAddRelic(relicId);   // 内部会应用 OnAcquire 效果
            Debug.Log($"[RelicSystem] 获得遗物: {relic.name}");
            NotifyDataChanged();
            return true;
        }

        public bool TryRemoveRelic(int relicId)
        {
            bool removed = runData.relicIds.Remove(relicId);
            if (removed) NotifyDataChanged();
            return removed;
        }

        public bool TryReplaceRelic(int oldRelicId, int newRelicId, out string error)
        {
            error = null;
            if (!runData.relicIds.Contains(oldRelicId))
            {
                error = "未拥有该遗物";
                return false;
            }

            var newRelic = ConfigLoader.GetRelic(newRelicId);
            if (newRelic == null) { error = "新遗物不存在"; return false; }

            runData.relicIds.Remove(oldRelicId);
            runData.TryAddRelic(newRelicId);   // 应用 OnAcquire 效果
            Debug.Log($"[RelicSystem] 替换遗物: {ConfigLoader.GetRelic(oldRelicId)?.name} -> {ConfigLoader.GetRelic(newRelicId)?.name}");
            NotifyDataChanged();
            return true;
        }

        public List<RelicData> GetOwnedRelics()
        {
            var list = new List<RelicData>();
            foreach (int id in runData.relicIds)
            {
                var relic = ConfigLoader.GetRelic(id);
                if (relic != null) list.Add(relic);
            }
            return list;
        }

        public bool HasRelic(int relicId)
        {
            return runData.relicIds.Contains(relicId);
        }

        // ===== 查询接口 =====

        /// <summary>
        /// 获取指定触发器的所有遗物效果（BattleManager 根据 trigger 决定如何应用）
        /// </summary>
        public List<RelicEffectData> GetEffectsByTrigger(string trigger)
        {
            var effects = new List<RelicEffectData>();
            foreach (int id in runData.relicIds)
            {
                var relic = ConfigLoader.GetRelic(id);
                if (relic != null)
                {
                    foreach (var eff in relic.effects)
                    {
                        if (eff.trigger == trigger)
                            effects.Add(eff);
                    }
                }
            }
            return effects;
        }

        /// <summary>
        /// 获取遗物提供的数值倍率（金币、商店价格、药水效果等）
        /// </summary>
        public float GetMultiplier(string effectType, float defaultValue = 1f)
        {
            float mult = defaultValue;
            foreach (int id in runData.relicIds)
            {
                var relic = ConfigLoader.GetRelic(id);
                if (relic != null)
                {
                    foreach (var eff in relic.effects)
                    {
                        if (eff.type == effectType)
                        {
                            mult *= GetFloatValue(eff.value);
                        }
                    }
                }
            }
            return mult;
        }

        /// <summary>
        /// 获取遗物提供的固定数值加成（每回合防御、临时防御、抽牌、治疗、伤害等）
        /// </summary>
        public int GetFlatBonus(string effectType, int defaultValue = 0)
        {
            int bonus = defaultValue;
            foreach (int id in runData.relicIds)
            {
                var relic = ConfigLoader.GetRelic(id);
                if (relic != null)
                {
                    foreach (var eff in relic.effects)
                    {
                        if (eff.type == effectType)
                        {
                            bonus += GetIntValue(eff.value);
                        }
                    }
                }
            }
            return bonus;
        }

        /// <summary>
        /// 获得金币（应用倍率）
        /// </summary>
        public int GainGold(int baseGold)
        {
            float mult = GetMultiplier("MultiplyGold", 1f);
            int finalGold = Mathf.RoundToInt(baseGold * mult);
            if (runData != null)
                runData.Gold += finalGold;
            return finalGold;
        }

        // ===== 内部工具 =====

        private void NotifyDataChanged()
        {
            OnDataChanged?.Invoke();
        }

        private float GetFloatValue(object value)
        {
            if (value == null) return 1f;
            if (value is float f) return f;
            if (value is double d) return (float)d;
            if (value is int i) return i;
            if (float.TryParse(value.ToString(), out var parsed)) return parsed;
            return 1f;
        }

        private int GetIntValue(object value)
        {
            if (value == null) return 0;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (int.TryParse(value.ToString(), out var parsed)) return parsed;
            return 0;
        }
    }
}