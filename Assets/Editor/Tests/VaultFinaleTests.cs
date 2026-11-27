using NUnit.Framework;

public class VaultFinaleTests
{
    [Test]
    public void TheCluesInOrderCrackTheVault()
    {
        var finale = new VaultFinale(new[] { 4, 1, 6 });
        Assert.AreEqual(FinaleTap.Right, finale.Tap(4));
        Assert.AreEqual(FinaleTap.Right, finale.Tap(1));
        Assert.AreEqual(FinaleTap.Cracked, finale.Tap(6));
        Assert.IsTrue(finale.Cracked && finale.Done);
        Assert.AreEqual(VaultFinale.Alarms, finale.AlarmsLeft);
    }

    [Test]
    public void AWrongTapSoundsAnAlarmAndTheSameClueIsStillWanted()
    {
        var finale = new VaultFinale(new[] { 4, 1 });
        Assert.AreEqual(FinaleTap.Wrong, finale.Tap(1));
        Assert.AreEqual(VaultFinale.Alarms - 1, finale.AlarmsLeft);
        Assert.AreEqual(0, finale.Step);
        Assert.AreEqual(4, finale.Expected);
        Assert.AreEqual(FinaleTap.Right, finale.Tap(4));
        Assert.AreEqual(FinaleTap.Cracked, finale.Tap(1));
    }

    [Test]
    public void RunningOutOfAlarmsTripsIt()
    {
        var finale = new VaultFinale(new[] { 4, 1 });
        finale.Tap(0);
        finale.Tap(0);
        Assert.AreEqual(FinaleTap.Tripped, finale.Tap(0));
        Assert.IsTrue(finale.Tripped && finale.Done && !finale.Cracked);
        Assert.Throws<System.InvalidOperationException>(() => finale.Tap(4));
    }

    [Test]
    public void NoCluesMeansNothingToEnter()
    {
        var finale = new VaultFinale(new int[0]);
        Assert.IsTrue(finale.Cracked);
    }

    [Test]
    public void ClarityDecidesHowLongAClueShowsInTheFinale()
    {
        Assert.AreEqual(0f, ClueRules.FinaleSecondsFor(0));
        Assert.AreEqual(3f, ClueRules.FinaleSecondsFor(1));
        Assert.IsTrue(float.IsPositiveInfinity(ClueRules.FinaleSecondsFor(2)));
        Assert.IsTrue(float.IsPositiveInfinity(ClueRules.FinaleSecondsFor(ClueRules.MaxTier)));
    }
}
