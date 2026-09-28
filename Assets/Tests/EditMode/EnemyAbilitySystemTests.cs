using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike;
using Roguelike.Data;

/// <summary>敌人能力系统测试（吞噬 / 弱点）</summary>
public class EnemyAbilitySystemTests
{
    private class FakeCtx : IEnemyAbilityContext
    {
        public List<BattleUnit> Units = new List<BattleUnit>();
        public DeckPile DeckPileField;
        public BattleUnit Target;
        public string Passive;

        public IReadOnlyList<BattleUnit> Enemies => Units;
        public DeckPile Deck => DeckPileField;
        public BattleUnit CurrentTarget => Target;
        public EnemyData GetEnemyData(BattleUnit unit)
            => (unit == Target && !string.IsNullOrEmpty(Passive)) ? new EnemyData { passive = Passive } : null;
    }

    private static DeckPile MakeDeck(int count)
    {
        var cards = new List<CardData>();
        for (int i = 0; i < count; i++)
            cards.Add(new CardData { rank = 2 + (i % 13), suit = (Suit)(i / 13) });
        var deck = new DeckPile();
        deck.Init(cards);
        return deck;
    }

    [Test]
    public void Swallow_RemovesCardsFromDeck_AndReturnsOnDeath()
    {
        var deck = MakeDeck(20);
        var enemy = new BattleUnit("吞噬怪", 30);
        var ctx = new FakeCtx { DeckPileField = deck, Target = enemy, Units = { enemy } };
        var sys = new EnemyAbilitySystem(ctx);

        int before = deck.Count;
        sys.ApplySwallow(3, enemy);

        Assert.AreEqual(before - 3, deck.Count, "应吞掉 3 张");
        Assert.IsNotNull(sys.GetSwallowedCards(enemy));
        Assert.AreEqual(3, sys.GetSwallowedCards(enemy).Count);
        Assert.AreEqual(3, enemy.GetStatusAmount(StatusEffectType.Swallow), "吞噬 buff 层数 = 张数");

        // 敌人阵亡 → 归还
        enemy.TakeDamage(999, null);
        sys.ReturnSwallowedCardsOfDeadEnemies();

        Assert.AreEqual(before, deck.Count, "阵亡后牌应全部归还");
        Assert.IsNull(sys.GetSwallowedCards(enemy));
        Assert.AreEqual(0, enemy.GetStatusAmount(StatusEffectType.Swallow));
    }

    [Test]
    public void Swallow_EmptyDeck_DoesNothing()
    {
        var deck = MakeDeck(0);
        var enemy = new BattleUnit("吞噬怪", 30);
        var ctx = new FakeCtx { DeckPileField = deck, Target = enemy };
        var sys = new EnemyAbilitySystem(ctx);

        sys.ApplySwallow(3, enemy);
        Assert.AreEqual(0, deck.Count);
        Assert.IsNull(sys.GetSwallowedCards(enemy));
    }

    [Test]
    public void Weakness_RefreshesTwoTypes()
    {
        var skull = new BattleUnit("愤怒骷髅头", 50);
        var ctx = new FakeCtx { Target = skull, Passive = "Weakness", Units = { skull } };
        var sys = new EnemyAbilitySystem(ctx);

        sys.RefreshWeaknesses();

        var ws = sys.GetWeaknesses(skull);
        Assert.IsNotNull(ws);
        Assert.AreEqual(2, ws.Count, "每回合刷新 2 个弱点");
        Assert.AreEqual(2, skull.GetStatusAmount(StatusEffectType.Weakness));
    }

    [Test]
    public void Weakness_NonWeaknessHand_GainsStrength()
    {
        var skull = new BattleUnit("愤怒骷髅头", 50);
        var ctx = new FakeCtx { Target = skull, Passive = "Weakness", Units = { skull } };
        var sys = new EnemyAbilitySystem(ctx);
        sys.RefreshWeaknesses();
        var ws = sys.GetWeaknesses(skull);

        // 找一个不属于当前弱点的牌型
        HandType other = HandType.OnePair;
        foreach (HandType h in Enum.GetValues(typeof(HandType)))
            if (!ws.Contains(WeaknessInfo.FromHandType(h))) { other = h; break; }

        sys.ApplyWeaknessOnPlay(other);
        Assert.AreEqual(1, skull.GetStatusAmount(StatusEffectType.Strength), "非弱点牌型攻击 → +1 力量");
    }

    [Test]
    public void Weakness_WeaknessHand_NoStrength()
    {
        var skull = new BattleUnit("愤怒骷髅头", 50);
        var ctx = new FakeCtx { Target = skull, Passive = "Weakness", Units = { skull } };
        var sys = new EnemyAbilitySystem(ctx);
        sys.RefreshWeaknesses();
        var ws = sys.GetWeaknesses(skull);

        // 用弱点里的牌型攻击 → 不获得力量
        HandType hit = HandType.OnePair;
        foreach (HandType h in Enum.GetValues(typeof(HandType)))
            if (WeaknessInfo.FromHandType(h) == ws[0]) { hit = h; break; }

        sys.ApplyWeaknessOnPlay(hit);
        Assert.AreEqual(0, skull.GetStatusAmount(StatusEffectType.Strength), "打中弱点 → 不获得力量");
    }
}
