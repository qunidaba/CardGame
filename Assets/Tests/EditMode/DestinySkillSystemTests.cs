using System.Collections.Generic;
using NUnit.Framework;
using Roguelike;

/// <summary>命格主动技系统测试（DestinySkillSystem）</summary>
public class DestinySkillSystemTests
{
    private class FakeCtx : IDestinySkillContext
    {
        public RunData Run { get; set; }
        public BattleUnit Player { get; set; }
        public BattleUnit CurrentEnemy { get; set; }
        public bool IsBattleOver { get; set; }

        public float HitInterval => 0f;
        public List<BattleUnit> GetAliveEnemies() => new List<BattleUnit>();
        public List<CardData> DrawToHand(int count) => new List<CardData>();
        public void GrantRandomEnchants(List<CardData> cards, int count, int minTier) { }
        public void NotifyEnchantmentsChanged() { }
        public void NotifyDestinyChanged() { }
        public void NotifyHandChanged() { }
        public void CheckBattleEnd() { }
    }

    [Test]
    public void Activate_WithoutMainDestiny_ReturnsFalse()
    {
        var ctx = new FakeCtx { Run = new RunData(), Player = new BattleUnit("p", 50) };
        var sys = new DestinySkillSystem(ctx);
        Assert.IsFalse(sys.Activate());
    }

    [Test]
    public void Activate_NotEnoughFatePower_ReturnsFalse()
    {
        var run = new RunData { mainDestinySuit = (int)Suit.Heart, fatePower = 0 };
        var ctx = new FakeCtx { Run = run, Player = new BattleUnit("p", 50) };
        var sys = new DestinySkillSystem(ctx);
        Assert.IsFalse(sys.Activate());
    }

    [Test]
    public void Heart_ClearsDebuffs_AddsRegeneration_AndConsumesFatePower()
    {
        var run = new RunData { mainDestinySuit = (int)Suit.Heart, fatePower = RunData.FatePowerMax };
        var player = new BattleUnit("p", 50);
        player.AddStatus(StatusEffectType.Poison, 3, 3);
        player.AddStatus(StatusEffectType.Burn, 2, 2);
        player.AddStatus(StatusEffectType.Weaken, 1, 2);

        var ctx = new FakeCtx { Run = run, Player = player };
        var sys = new DestinySkillSystem(ctx);

        Assert.IsTrue(sys.Activate());
        Assert.AreEqual(0, player.GetStatusAmount(StatusEffectType.Poison), "中毒被清除");
        Assert.AreEqual(0, player.GetStatusAmount(StatusEffectType.Burn), "灼烧被清除");
        Assert.AreEqual(0, player.GetStatusAmount(StatusEffectType.Weaken), "虚弱被清除");
        Assert.Greater(player.GetStatusAmount(StatusEffectType.Regeneration), 0, "获得再生");
        Assert.AreEqual(0, run.fatePower, "命运之力清空");
    }

    [Test]
    public void Diamond_Lv1_AddsGold()
    {
        var run = new RunData { mainDestinySuit = (int)Suit.Diamond, fatePower = RunData.FatePowerMax, gold = 0 };
        var ctx = new FakeCtx { Run = run, Player = new BattleUnit("p", 50) };
        var sys = new DestinySkillSystem(ctx);

        Assert.IsTrue(sys.Activate());
        Assert.AreEqual(DestinyEffects.ActiveDiamondGoldLv1, run.Gold, "聚宝 Lv1 加金币");
    }
}
