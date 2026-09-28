using NUnit.Framework;
using Roguelike;

/// <summary>命格被动系统测试（DestinyPassiveSystem）</summary>
public class DestinyPassiveSystemTests
{
    private static RunData RunWith(int spade, int heart, int club, int diamond)
    {
        var run = new RunData();
        run.destinyPoints[(int)Suit.Spade] = spade;
        run.destinyPoints[(int)Suit.Heart] = heart;
        run.destinyPoints[(int)Suit.Club] = club;
        run.destinyPoints[(int)Suit.Diamond] = diamond;
        return run;
    }

    [Test]
    public void CombatStart_SpadeLv1_GrantsStrength()
    {
        var run = RunWith(spade: 2, heart: 0, club: 0, diamond: 0); // Lv1 阈值 = 2
        var player = new BattleUnit("p", 50);
        var sys = new DestinyPassiveSystem();

        sys.ApplyCombatStart(run, player);

        Assert.AreEqual(DestinyPassiveSystem.SpadeLv1Strength, player.GetStatusAmount(StatusEffectType.Strength));
    }

    [Test]
    public void CombatStart_ClubLv1_SetsFirstTurnDraw()
    {
        var run = RunWith(spade: 0, heart: 0, club: 2, diamond: 0);
        var player = new BattleUnit("p", 50);
        var sys = new DestinyPassiveSystem();

        sys.ApplyCombatStart(run, player);

        Assert.AreEqual(DestinyPassiveSystem.ClubLv1FirstTurnDraw, sys.FirstTurnBonusDraw);
    }

    [Test]
    public void CombatStart_NoDestiny_NoBonuses()
    {
        var run = RunWith(0, 0, 0, 0);
        var player = new BattleUnit("p", 50);
        var sys = new DestinyPassiveSystem();

        sys.ApplyCombatStart(run, player);

        Assert.AreEqual(0, player.GetStatusAmount(StatusEffectType.Strength));
        Assert.AreEqual(0, sys.FirstTurnBonusDraw);
        Assert.AreEqual(0, sys.ClubOpeningEnchant);
    }
}
