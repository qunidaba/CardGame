using System.Collections.Generic;
using NUnit.Framework;
using Roguelike;

/// <summary>拆出的小系统测试：SuitTally / HandEffectUtil</summary>
public class ExtractionTests
{
    [Test]
    public void SuitTally_CountsAndDominant()
    {
        var t = new SuitTally();
        t.CountPlayed(new List<CardData>
        {
            new CardData { rank = 3, suit = Suit.Heart },
            new CardData { rank = 4, suit = Suit.Heart },
            new CardData { rank = 5, suit = Suit.Spade },
        });

        Assert.AreEqual(2, t.GetCount(Suit.Heart));
        Assert.AreEqual(1, t.GetCount(Suit.Spade));
        Assert.AreEqual(Suit.Heart, t.Dominant());

        t.Reset();
        Assert.AreEqual(0, t.GetCount(Suit.Heart));
    }

    [Test]
    public void SuitTally_Dominant_TiePrefersLastPlayed()
    {
        var t = new SuitTally();
        t.CountPlayed(new List<CardData>
        {
            new CardData { rank = 3, suit = Suit.Heart },
            new CardData { rank = 5, suit = Suit.Spade },
        });
        Assert.AreEqual(Suit.Spade, t.Dominant(), "平局时最后打出的花色优先");
    }

    [Test]
    public void HandEffectUtil_AddDamage_MergesIntoExisting()
    {
        var effects = new List<HandEffectTable.HandEffect>
        {
            new HandEffectTable.HandEffect { effectType = HandEffectTable.EffectType.Damage, value = 10 }
        };
        HandEffectUtil.AddDamageToEffects(effects, 5);
        Assert.AreEqual(15, effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage).value);
    }

    [Test]
    public void HandEffectUtil_MultiplyDamage()
    {
        var effects = new List<HandEffectTable.HandEffect>
        {
            new HandEffectTable.HandEffect { effectType = HandEffectTable.EffectType.Damage, value = 10 }
        };
        HandEffectUtil.MultiplyDamageEffect(effects, 2f);
        Assert.AreEqual(20, effects.Find(e => e.effectType == HandEffectTable.EffectType.Damage).value);
    }

    [Test]
    public void HandEffectUtil_StraightFlush_IsBothStraightAndFlush()
    {
        var sf = new HandTypeResult { type = HandType.StraightFlush5 };
        Assert.IsTrue(HandEffectUtil.IsStraightHand(sf));
        Assert.IsTrue(HandEffectUtil.IsFlushHand(sf));

        var flush = new HandTypeResult { type = HandType.Flush5 };
        Assert.IsFalse(HandEffectUtil.IsStraightHand(flush));
        Assert.IsTrue(HandEffectUtil.IsFlushHand(flush));
    }

    [Test]
    public void HandEffectUtil_AddEffectValue_CreatesWhenMissing()
    {
        var effects = new List<HandEffectTable.HandEffect>();
        HandEffectUtil.AddEffectValue(effects, HandEffectTable.EffectType.DrawCard, 3);
        Assert.AreEqual(1, effects.Count);
        Assert.AreEqual(3, effects[0].value);
    }
}
