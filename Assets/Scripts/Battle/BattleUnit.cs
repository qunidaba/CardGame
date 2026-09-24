using UnityEngine;
using System;
using System.Collections.Generic;
using Roguelike;

public class BattleUnit
{
    public string Name { get; private set; }
    public int MaxHp { get; set; }

    private int _currentHp;
    public int CurrentHp
    {
        get => _currentHp;
        set
        {
            int delta = value - _currentHp;
            _currentHp = Mathf.Clamp(value, 0, MaxHp);
            int actualDelta = _currentHp - (value - delta);
            if (actualDelta != 0) OnHpChanged?.Invoke(this, actualDelta);
        }
    }

    private int _defense;
    public int Defense
    {
        get => _defense;
        set
        {
            int delta = value - _defense;
            _defense = Mathf.Max(0, value);
            if (delta != 0) OnDefenseChanged?.Invoke(this, delta);
        }
    }

    public bool IsDead => CurrentHp <= 0;

    // 状态效果系统
    public StatusEffectSystem StatusEffects { get; private set; }

    // 事件：携带变化量
    public event Action<BattleUnit, int> OnHpChanged;
    public event Action<BattleUnit, int> OnDefenseChanged;
    public event Action<int> OnMaxHpChanged;

    /// <summary>
    /// 受到伤害：本次攻击的实际伤害总量（= 扣血 + 被防御抵消），
    /// 与是否真的掉血无关，用于伤害飘字。
    /// </summary>
    public event Action<BattleUnit, int> OnDamageTaken;

    // 受伤回调（兼容性）
    public Action<int> OnTakeDamageCallback;

    public BattleUnit(string name, int hp)
    {
        Name = name;
        MaxHp = hp;
        _currentHp = hp;
        _defense = 0;
        StatusEffects = new StatusEffectSystem(this);
    }

    /// <summary>
    /// 受到伤害（经过状态效果修正、扣除防御后结算）
    /// </summary>
    /// <param name="halveDefense">
    /// 破防：对防御的伤害翻倍——防御按双倍消耗，所以「能挡住的伤害」是 ceil(防御/2)。
    /// 例：防御 5 → 能挡 3 点，同时护盾被 3×2=6 打光，剩下的伤害才进血量。
    /// </param>
    public int TakeDamage(int damage, BattleUnit attacker = null, bool halveDefense = false)
    {
        // 状态效果修正伤害（易伤、无形、荆棘反伤等）
        int finalDamage = StatusEffects.OnTakeDamage(damage, attacker);

        int defensePool = halveDefense ? Mathf.CeilToInt(Defense / 2f) : Defense;
        int defenseLost = Mathf.Min(finalDamage, defensePool);
        int actualDamage = finalDamage - defenseLost;

        int shieldBefore = Defense;
        CurrentHp -= actualDamage;
        Defense -= halveDefense ? defenseLost * 2 : defenseLost;   // 破防按双倍消耗护盾
        int shieldConsumed = shieldBefore - Defense;               // 实际扣掉的护盾（会被 clamp 到 0）

        // 飘字数值：破防时显示「扣掉的护盾 + 扣掉的血量」的总和
        int displayDamage = halveDefense ? actualDamage + shieldConsumed : finalDamage;

        OnTakeDamageCallback?.Invoke(actualDamage);
        OnDamageTaken?.Invoke(this, displayDamage);
        return defenseLost;
    }

    /// <summary>
    /// 造成伤害（经过状态效果修正）
    /// </summary>
    public int DealDamage(int baseDamage)
    {
        return StatusEffects.OnDealDamage(baseDamage);
    }

    /// <summary>
    /// 增加防御值（经过状态效果修正）
    /// </summary>
    public void AddDefense(int value)
    {
        int finalDefense = StatusEffects.OnGainDefense(value);
        Defense += finalDefense;
    }

    /// <summary>
    /// 恢复生命值
    /// </summary>
    public void Heal(int value)
    {
        CurrentHp += value;
    }

    /// <summary>
    /// 增加最大生命值（同时回满血）
    /// </summary>
    public void AddMaxHp(int value)
    {
        MaxHp += value;
        CurrentHp += value;
        OnMaxHpChanged?.Invoke(MaxHp);
    }

    /// <summary>
    /// 回合结束处理状态效果
    /// </summary>
    public void OnTurnEnd()
    {
        StatusEffects.OnTurnEnd();
    }

    /// <summary>
    /// 战斗开始处理
    /// </summary>
    public void OnCombatStart()
    {
        StatusEffects.OnCombatStart();
    }

    /// <summary>
    /// 战斗结束清理
    /// </summary>
    public void OnCombatEnd()
    {
        StatusEffects.OnCombatEnd();
    }

    /// <summary>
    /// 清除防御值（每回合开始调用）
    /// </summary>
    public void ClearDefense()
    {
        Defense = 0;
    }

    /// <summary>
    /// 添加状态效果
    /// </summary>
    public void AddStatus(StatusEffectType type, int amount = 1, int duration = -1)
    {
        StatusEffects.AddStatus(type, amount, duration);
    }

    /// <summary>
    /// 添加带花色维度的状态效果
    /// </summary>
    public void AddStatus(StatusEffectType type, Suit suit, int amount = 1, int duration = -1)
    {
        StatusEffects.AddStatus(type, suit, amount, duration);
    }

    /// <summary>
    /// 直接设置状态层数（覆盖，允许 0；用于「挑战」这类展示型状态）
    /// </summary>
    public void SetStatus(StatusEffectType type, int amount, int duration = -1)
    {
        StatusEffects.SetStatus(type, amount, duration);
    }

    /// <summary>
    /// 移除状态效果
    /// </summary>
    public void RemoveStatus(StatusEffectType type, int amount = -1)
    {
        StatusEffects.RemoveStatus(type, amount);
    }

    /// <summary>
    /// 移除带花色维度的状态效果
    /// </summary>
    public void RemoveStatus(StatusEffectType type, Suit suit, int amount = -1)
    {
        StatusEffects.RemoveStatus(type, suit, amount);
    }

    /// <summary>
    /// 获取状态效果层数
    /// </summary>
    public int GetStatusAmount(StatusEffectType type)
    {
        return StatusEffects.GetAmount(type);
    }

    /// <summary>
    /// 获取指定花色维度的状态效果层数
    /// </summary>
    public int GetStatusAmount(StatusEffectType type, Suit suit)
    {
        return StatusEffects.GetAmount(type, suit);
    }
}