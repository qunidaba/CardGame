using System.Collections.Generic;
using NUnit.Framework;

/// <summary>牌型判定测试（HandEvaluator）</summary>
public class HandEvaluatorTests
{
    private static HandTypeResult Eval(params (int rank, Suit suit)[] cards)
    {
        var list = new List<CardData>();
        foreach (var c in cards) list.Add(new CardData { rank = c.rank, suit = c.suit });
        return HandEvaluator.Evaluate(list);
    }

    [Test]
    public void OnePair_TwoCards()
        => Assert.AreEqual(HandType.OnePair, Eval((3, Suit.Spade), (3, Suit.Heart)).type);

    [Test]
    public void TwoConsecutivePairs()
        => Assert.AreEqual(HandType.TwoConsecutivePairs,
            Eval((3, Suit.Spade), (3, Suit.Heart), (4, Suit.Spade), (4, Suit.Heart)).type);

    [Test]
    public void ThreeOfAKind()
        => Assert.AreEqual(HandType.ThreeOfAKind,
            Eval((5, Suit.Spade), (5, Suit.Heart), (5, Suit.Club)).type);

    [Test]
    public void Straight3()
        => Assert.AreEqual(HandType.Straight3,
            Eval((5, Suit.Spade), (6, Suit.Heart), (7, Suit.Club)).type);

    [Test]
    public void Straight5()
        => Assert.AreEqual(HandType.Straight5,
            Eval((5, Suit.Spade), (6, Suit.Heart), (7, Suit.Club), (8, Suit.Diamond), (9, Suit.Spade)).type);

    [Test]
    public void Straight_A2345_CountsAsStraight()
        => Assert.AreEqual(HandType.Straight5,
            Eval((14, Suit.Spade), (2, Suit.Heart), (3, Suit.Club), (4, Suit.Diamond), (5, Suit.Spade)).type);

    [Test]
    public void Flush3()
        => Assert.AreEqual(HandType.Flush3,
            Eval((5, Suit.Spade), (7, Suit.Spade), (9, Suit.Spade)).type);

    [Test]
    public void FullHouse()
        => Assert.AreEqual(HandType.FullHouse,
            Eval((3, Suit.Spade), (3, Suit.Heart), (3, Suit.Club), (4, Suit.Spade), (4, Suit.Heart)).type);

    [Test]
    public void FourOfAKind()
        => Assert.AreEqual(HandType.FourOfAKind,
            Eval((3, Suit.Spade), (3, Suit.Heart), (3, Suit.Club), (3, Suit.Diamond)).type);

    [Test]
    public void StraightFlush5()
        => Assert.AreEqual(HandType.StraightFlush5,
            Eval((5, Suit.Spade), (6, Suit.Spade), (7, Suit.Spade), (8, Suit.Spade), (9, Suit.Spade)).type);

    [Test]
    public void SingleCard_IsInvalid()
        => Assert.IsFalse(Eval((5, Suit.Spade)).IsValid);

    [Test]
    public void SixCards_IsInvalid()
        => Assert.IsFalse(Eval((2, Suit.Spade), (3, Suit.Heart), (4, Suit.Club),
            (5, Suit.Diamond), (6, Suit.Spade), (7, Suit.Heart)).IsValid);
}
