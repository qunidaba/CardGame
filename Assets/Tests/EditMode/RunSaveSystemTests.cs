using NUnit.Framework;
using Roguelike;

/// <summary>存档系统的 RNG 状态保存/恢复测试（反射读写 UnityEngine.Random.State）</summary>
public class RunSaveSystemTests
{
    [Test]
    public void CaptureAndRestoreRng_ReproducesSameSequence()
    {
        UnityEngine.Random.InitState(12345);

        var saved = RunSaveSystem.CaptureRng();
        Assert.AreEqual(4, saved.Count, "RNG 状态应为 4 个 int");

        int a = UnityEngine.Random.Range(0, 1000000);
        int b = UnityEngine.Random.Range(0, 1000000);
        int c = UnityEngine.Random.Range(0, 1000000);

        RunSaveSystem.RestoreRng(saved);

        Assert.AreEqual(a, UnityEngine.Random.Range(0, 1000000));
        Assert.AreEqual(b, UnityEngine.Random.Range(0, 1000000));
        Assert.AreEqual(c, UnityEngine.Random.Range(0, 1000000));
    }

    [Test]
    public void CaptureRng_ChangesAfterDraw()
    {
        UnityEngine.Random.InitState(7);
        var before = RunSaveSystem.CaptureRng();
        UnityEngine.Random.Range(0, 1000000);
        var after = RunSaveSystem.CaptureRng();

        bool changed = false;
        for (int i = 0; i < 4; i++) if (before[i] != after[i]) changed = true;
        Assert.IsTrue(changed, "取随机数后 RNG 状态应发生变化（说明反射读写有效）");
    }
}
