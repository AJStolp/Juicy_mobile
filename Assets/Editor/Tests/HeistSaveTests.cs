using NUnit.Framework;

public class HeistSaveTests
{
    [Test]
    public void RoundTripRestoresVaultCashAndGear()
    {
        var vault = new VaultProgress { TurnsByHeist = new[] { 5 } };
        var rules = new HeistSortRules();
        vault.Turn(5 * 7);
        vault.Turn(5 * 2 + 3);
        rules.Award(4321d);
        rules.AddGear(GearKind.Drill, 2);
        rules.Trash(ItemKind.Bomb);

        string json = HeistSave.Capture(vault, rules).ToJson();

        var vault2 = new VaultProgress { TurnsByHeist = new[] { 5 } };
        var rules2 = new HeistSortRules();
        HeistSave.FromJson(json).Apply(vault2, rules2);
        Assert.AreEqual(2, vault2.Heist);
        Assert.AreEqual(2, vault2.Stage);
        Assert.AreEqual(3, vault2.Turns);
        Assert.AreEqual(rules.Balance, rules2.Balance);
        Assert.AreEqual(2, rules2.GearCount(GearKind.Drill));
        Assert.AreEqual(1, rules2.GearCount(GearKind.Dynamite));
    }

    [Test]
    public void BadOrMissingSaveReadsAsNone()
    {
        Assert.IsNull(HeistSave.FromJson(null));
        Assert.IsNull(HeistSave.FromJson(""));
        Assert.IsNull(HeistSave.FromJson("{not json"));
    }

    [Test]
    public void RestoreClampsDynamiteToTheCap()
    {
        var rules = new HeistSortRules();
        rules.Restore(10d, new[] { 99, 0, 0, 0 });
        Assert.AreEqual(rules.DynamiteCap, rules.GearCount(GearKind.Dynamite));
    }
}
