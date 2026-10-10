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
    }

    [Test]
    public void CombatStart_DiamondLv3_GoldPerStrength()
    {
        var run = RunWith(0, 0, 0, diamond: 10); // Lv3 阈值 = 10
        run.Gold = 120;
        var player = new BattleUnit("p", 50);

        new DestinyPassiveSystem().ApplyCombatStart(run, player);

        Assert.AreEqual(120 / DestinyPassiveSystem.DiamondLv3GoldPerStrength,
            player.GetStatusAmount(StatusEffectType.Strength));
    }

    [Test]
    public void BattleWin_DiamondLv2_GoldPercent()
    {
        var run = RunWith(0, 0, 0, diamond: 5); // Lv2 阈值 = 5
        run.Gold = 100;

        DestinyPassiveSystem.ApplyBattleWin(run);

        Assert.AreEqual(115, run.Gold); // 100 + 15%
    }

    [Test]
    public void ClubTurnEnchant_OnlyAtLv3()
    {
        Assert.AreEqual(0, DestinyPassiveSystem.GetClubTurnEnchantCount(RunWith(0, 0, 5, 0)));
        Assert.AreEqual(DestinyPassiveSystem.ClubLv3HandEnchantPerTurn,
            DestinyPassiveSystem.GetClubTurnEnchantCount(RunWith(0, 0, 10, 0)));
    }

    [Test]
    public void SpadePlayBonus_Lv3_FirstPlayMultiplier()
    {
        var run = RunWith(10, 0, 0, 0); // Lv3

        DestinyPassiveSystem.GetSpadePlayBonus(run, playIndex: 1, out int add, out float mult);

        Assert.AreEqual(0, add);
        Assert.AreEqual(DestinyPassiveSystem.SpadeLv3FirstPlayMultiplier, mult);
    }
}
