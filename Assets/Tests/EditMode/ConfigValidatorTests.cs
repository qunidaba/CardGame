using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Data;

/// <summary>配置校验测试（ConfigValidator）
/// 注意：用 log:false 静默调用，否则 Debug.LogError 会触发 Unity Test 的日志失败规则。</summary>
public class ConfigValidatorTests
{
    private static int Validate(AllConfig cfg) => ConfigValidator.Validate(cfg, log: false);

    [Test]
    public void CleanConfig_HasNoErrors()
    {
        var cfg = new AllConfig();
        cfg.enemies.Add(new EnemyData
        {
            id = 1,
            name = "哥布林",
            hp = 10,
            maxHp = 10,
            intents = new List<IntentData> { new IntentData { type = "Attack", value = 5, hitCount = 1 } }
        });

        Assert.AreEqual(0, Validate(cfg));
    }

    [Test]
    public void BadEnemy_StatsAreFlagged()
    {
        var cfg = new AllConfig();
        cfg.enemies.Add(new EnemyData { id = 1, name = "", hp = -5, maxHp = 0 });
        Assert.Greater(Validate(cfg), 0);
    }

    [Test]
    public void BadIntentType_IsFlagged()
    {
        var cfg = new AllConfig();
        cfg.enemies.Add(new EnemyData
        {
            id = 1, name = "X", hp = 10, maxHp = 10,
            intents = new List<IntentData> { new IntentData { type = "Atak" } }
        });
        Assert.Greater(Validate(cfg), 0);
    }

    [Test]
    public void SummonMissingEnemy_IsFlagged()
    {
        var cfg = new AllConfig();
        cfg.enemies.Add(new EnemyData
        {
            id = 1, name = "巫师", hp = 10, maxHp = 10,
            intents = new List<IntentData> { new IntentData { type = "Summon", value = 999, hitCount = 2 } }
        });
        Assert.Greater(Validate(cfg), 0);
    }

    [Test]
    public void EncounterWithMissingEnemy_IsFlagged()
    {
        var cfg = new AllConfig();
        cfg.encounters.Add(new EncounterData
        {
            id = 1, name = "e", pool = "act1_common",
            enemies = new List<int> { 999 }
        });
        Assert.Greater(Validate(cfg), 0);
    }

    [Test]
    public void DuplicateIds_AreFlagged()
    {
        var cfg = new AllConfig();
        cfg.enemies.Add(new EnemyData { id = 1, name = "A", hp = 10, maxHp = 10 });
        cfg.enemies.Add(new EnemyData { id = 1, name = "B", hp = 10, maxHp = 10 });
        Assert.Greater(Validate(cfg), 0);
    }
}
