using System;
using NUnit.Framework;
using Roguelike;
using Roguelike.Data;

/// <summary>弱点分类 + 状态注册表完整性测试</summary>
public class RegistryTests
{
    [Test]
    public void Weakness_GroupsByFamily_NotByCardCount()
    {
        Assert.AreEqual(WeaknessType.Straight, WeaknessInfo.FromHandType(HandType.Straight3));
        Assert.AreEqual(WeaknessType.Straight, WeaknessInfo.FromHandType(HandType.Straight4));
        Assert.AreEqual(WeaknessType.Straight, WeaknessInfo.FromHandType(HandType.Straight5));
        Assert.AreEqual(WeaknessType.Flush, WeaknessInfo.FromHandType(HandType.Flush3));
        Assert.AreEqual(WeaknessType.Flush, WeaknessInfo.FromHandType(HandType.Flush5));
        Assert.AreEqual(WeaknessType.StraightFlush, WeaknessInfo.FromHandType(HandType.StraightFlush5));
        Assert.AreEqual(WeaknessType.FullHouse, WeaknessInfo.FromHandType(HandType.FullHouse));
    }

    [Test]
    public void StatusRegistry_KnownNames()
    {
        Assert.AreEqual("中毒", StatusEffectRegistry.Name(StatusEffectType.Poison));
        Assert.AreEqual("遁地", StatusEffectRegistry.Name(StatusEffectType.Burrow));
        Assert.AreEqual("吞噬", StatusEffectRegistry.Name(StatusEffectType.Swallow));
    }

    [Test]
    public void StatusRegistry_NegativeFlags()
    {
        Assert.IsTrue(StatusEffectRegistry.IsNegative(StatusEffectType.Poison));
        Assert.IsTrue(StatusEffectRegistry.IsNegative(StatusEffectType.Vulnerable));
        Assert.IsFalse(StatusEffectRegistry.IsNegative(StatusEffectType.Strength));
        Assert.IsFalse(StatusEffectRegistry.IsNegative(StatusEffectType.Burrow));
    }

    /// <summary>每个枚举状态都必须有注册信息（新增状态忘了注册 → 这个测试立刻红）</summary>
    [Test]
    public void StatusRegistry_AllDeclaredTypesRegistered()
    {
        foreach (StatusEffectType t in Enum.GetValues(typeof(StatusEffectType)))
        {
            if (t == StatusEffectType.None) continue;
            Assert.IsNotNull(StatusEffectRegistry.Get(t), $"状态 {t} 没有注册信息");
        }
    }

    /// <summary>每个意图类型都必须有注册 Handler（新增意图忘了注册 → 立刻红）</summary>
    [Test]
    public void IntentRegistry_AllDeclaredTypesRegistered()
    {
        foreach (IntentType t in Enum.GetValues(typeof(IntentType)))
        {
            Assert.IsTrue(IntentRegistry.Has(t.ToString()), $"意图 {t} 没有注册 Handler");
        }
    }
}
