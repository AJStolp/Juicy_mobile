using NUnit.Framework;

public class PlaceProgressTests
{
    [Test]
    public void OnlyTheFirstLevelIsOpenAtTheStart()
    {
        var place = new PlaceProgress(6);
        Assert.IsTrue(place.IsOpen(0));
        Assert.IsFalse(place.IsOpen(1));
        Assert.IsFalse(place.IsBeaten(0));
        Assert.AreEqual(0, place.Next);
    }

    [Test]
    public void WinningOpensTheNextAndBeatenLevelsStayOpen()
    {
        var place = new PlaceProgress(6);
        place.Won(0);
        Assert.IsTrue(place.IsBeaten(0));
        Assert.IsTrue(place.IsOpen(0));
        Assert.IsTrue(place.IsOpen(1));
        Assert.IsFalse(place.IsOpen(2));
        Assert.AreEqual(1, place.Next);
    }

    [Test]
    public void ALockedOrReplayedLevelChangesNothing()
    {
        var place = new PlaceProgress(6);
        place.Won(3);
        Assert.AreEqual(0, place.Beaten, "locked");
        place.Won(0);
        place.Won(1);
        place.Won(0);
        Assert.AreEqual(2, place.Beaten, "a replay never moves progress back");
    }

    [Test]
    public void BeatingTheLastLevelBeatsThePlace()
    {
        var place = new PlaceProgress(2);
        place.Won(0);
        place.Won(1);
        Assert.IsTrue(place.AllBeaten);
        Assert.AreEqual(1, place.Next);
        Assert.IsFalse(place.IsOpen(2));
    }

    [Test]
    public void RestoreClampsToThePlace()
    {
        var place = new PlaceProgress(6);
        place.Restore(40);
        Assert.AreEqual(6, place.Beaten);
        place.Restore(-3);
        Assert.AreEqual(0, place.Beaten);
    }

    [Test]
    public void TheVaultOpensOnceEveryLevelIsBeatenAndCracksForGood()
    {
        var place = new PlaceProgress(2);
        place.CrackVault();
        Assert.IsFalse(place.VaultCracked, "a closed vault cannot be cracked");
        place.Won(0);
        Assert.IsFalse(place.VaultOpen);
        place.Won(1);
        Assert.IsTrue(place.VaultOpen);
        place.CrackVault();
        Assert.IsTrue(place.VaultCracked);
        place.Restore(1, true);
        Assert.IsFalse(place.VaultCracked, "a save cannot crack a vault that is not open");
        place.Restore(2, true);
        Assert.IsTrue(place.VaultCracked);
    }
}
