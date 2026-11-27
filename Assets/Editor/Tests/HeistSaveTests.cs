using NUnit.Framework;

public class HeistSaveTests
{
    [Test]
    public void RoundTripRestoresCashGearProgressAndClues()
    {
        var rules = new HeistSortRules();
        rules.Award(4321d);
        rules.AddGear(GearKind.Drill, 2);
        rules.Trash(ItemKind.Bomb);
        var place = new PlaceProgress(2);
        place.Won(0);
        place.Won(1);
        place.CrackVault();
        var caseFile = new CaseFile(2, 7);
        caseFile.Record(1, 2);
        var lives = new Lives();
        lives.Lose(1000);
        string json = HeistSave.Capture(rules, place, caseFile, lives, 1000).ToJson();

        var rules2 = new HeistSortRules();
        var place2 = new PlaceProgress(2);
        var caseFile2 = new CaseFile(2, 7);
        HeistSave save = HeistSave.FromJson(json);
        save.Apply(rules2, place2, caseFile2);
        Assert.AreEqual(Lives.Max - 1, save.LoadLives().Count(1000));
        Assert.AreEqual(Lives.Max, save.LoadLives().Count(1000 + Lives.RefillSeconds));
        Assert.AreEqual(2, place2.Beaten);
        Assert.IsTrue(place2.VaultCracked);
        Assert.IsFalse(caseFile2.Found(0));
        Assert.AreEqual(2, caseFile2.ClueAt(1).Tier);
        Assert.AreEqual(rules.Balance, rules2.Balance);
        Assert.AreEqual(2, rules2.GearCount(GearKind.Drill));
        Assert.AreEqual(1, rules2.GearCount(GearKind.Dynamite));
    }

    [Test]
    public void AnOldSaveWithTheMultiDayVaultStillLoads()
    {
        string old = "{\"heist\":2,\"stage\":5,\"turns\":4,\"balance\":777.0,\"gear\":[1,0,0,0],\"clueSeed\":3,\"clueCycle\":1,\"clueSymbols\":[2],\"clueTiers\":[1],\"placeBeaten\":3}";
        var rules = new HeistSortRules();
        var place = new PlaceProgress(6);
        var caseFile = new CaseFile(6, 7);
        HeistSave save = HeistSave.FromJson(old);
        save.Apply(rules, place, caseFile);
        Assert.AreEqual(Lives.Max, save.LoadLives().Count(0), "a save from before lives starts full");
        Assert.AreEqual(777d, rules.Balance);
        Assert.AreEqual(3, place.Beaten);
        Assert.IsFalse(place.VaultCracked);
        Assert.IsFalse(caseFile.Found(0));
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
