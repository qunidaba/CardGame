using NUnit.Framework;
using Roguelike;

/// <summary>伤害 / 状态结算测试（BattleUnit + StatusEffectSystem）</summary>
public class StatusDamageTests
{
    private static BattleUnit Unit(int hp = 100) => new BattleUnit("t", hp);

    [Test]
    public void Strength_AddsFlatDamage()
    {
        var u = Unit();
        u.AddStatus(StatusEffectType.Strength, 2, -1);
        Assert.AreEqual(7, u.DealDamage(5));
    }

    [Test]
    public void Weaken_ReducesTenPercentPerStack()
    {
        var u = Unit();
        u.AddStatus(StatusEffectType.Weaken, 1, -1);
        Assert.AreEqual(9, u.DealDamage(10));   // 10 * 0.9
    }

    [Test]
    public void Vulnerable_IncreasesDamageTaken()
    {
        var u = Unit();
        u.AddStatus(StatusEffectType.Vulnerable, 1, -1);
        u.TakeDamage(10, null);                 // 10 * 1.1 = 11
        Assert.AreEqual(89, u.CurrentHp);
    }

    [Test]
    public void Intangible_CapsDamageToOne_AndConsumesStack()
    {
        var u = Unit();
        u.AddStatus(StatusEffectType.Intangible, 1, -1);
        u.TakeDamage(50, null);
        Assert.AreEqual(99, u.CurrentHp);
        Assert.AreEqual(0, u.GetStatusAmount(StatusEffectType.Intangible));
    }

    [Test]
    public void Burrow_CapsDamageToOne_AndConsumesStack()
    {
        var u = Unit();
        u.AddStatus(StatusEffectType.Burrow, 2, -1);
        u.TakeDamage(50, null);
        Assert.AreEqual(99, u.CurrentHp);
        Assert.AreEqual(1, u.GetStatusAmount(StatusEffectType.Burrow));
    }

    [Test]
    public void Defense_AbsorbsDamage()
    {
        var u = Unit();
        u.Defense = 10;
        u.TakeDamage(6, null);
        Assert.AreEqual(4, u.Defense);
        Assert.AreEqual(100, u.CurrentHp);
    }

    [Test]
    public void Sunder_HalvesDefenseAbsorption()
    {
        var u = Unit();
        u.Defense = 10;
        u.TakeDamage(6, null, halveDefense: true);
        // 防御只能挡 ceil(10/2)=5 → 实际掉 1 血，护盾按双倍消耗到 0
        Assert.AreEqual(0, u.Defense);
        Assert.AreEqual(99, u.CurrentHp);
    }

    [Test]
    public void Thorns_ReflectsToAttacker()
    {
        var defender = Unit();
        var attacker = Unit();
        defender.AddStatus(StatusEffectType.Thorns, 3, -1);
        defender.TakeDamage(5, attacker);
        Assert.AreEqual(95, defender.CurrentHp);
        Assert.AreEqual(97, attacker.CurrentHp);
    }

    [Test]
    public void Heal_ClampsToMax()
    {
        var u = Unit(80);
        u.Heal(999);
        Assert.AreEqual(80, u.CurrentHp);
    }
}
