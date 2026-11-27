using NUnit.Framework;

public class VaultProgressTests
{
    static VaultProgress Vault(int turnsPerStage)
    {
        var vault = new VaultProgress();
        vault.Reset(turnsPerStage);
        return vault;
    }

    [Test]
    public void FullStageLocksAndTheNextOneStarts()
    {
        var vault = Vault(3);
        int locked = -1;
        vault.StageLocked += s => locked = s;
        vault.Turn(2);
        Assert.AreEqual(0, vault.Stage);
        Assert.AreEqual(-1, locked);
        vault.Turn(1);
        Assert.AreEqual(0, locked);
        Assert.AreEqual(1, vault.Stage);
        Assert.AreEqual(0, vault.Turns);
    }

    [Test]
    public void BigTurnCarriesIntoTheNextStage()
    {
        var vault = Vault(3);
        int locks = 0;
        vault.StageLocked += _ => locks++;
        vault.Turn(2);
        vault.Turn(4);
        Assert.AreEqual(2, locks);
        Assert.AreEqual(2, vault.Stage);
        Assert.AreEqual(0, vault.Turns);
    }

    [Test]
    public void LockingTheLastStageCracksItOnce()
    {
        var vault = Vault(2);
        int cracks = 0;
        vault.Cracked += () => cracks++;
        vault.Turn(2 * VaultProgress.Stages);
        Assert.IsTrue(vault.IsCracked);
        Assert.AreEqual(1, cracks);
        vault.Turn(5);
        Assert.AreEqual(1, cracks, "a cracked vault takes no more turns");
        Assert.AreEqual(1f, vault.StageFraction);
    }

    [Test]
    public void LockThroughShowsTheSortDoneAndLeavesTheWheel()
    {
        var vault = Vault(5);
        vault.Turn(3);
        vault.LockThrough(VaultProgress.Stages - 1);
        Assert.AreEqual(VaultProgress.Stages - 1, vault.Stage);
        Assert.AreEqual(0, vault.Turns);
        Assert.IsFalse(vault.IsCracked);
    }

    [Test]
    public void ResetStartsAFreshDoor()
    {
        var vault = Vault(2);
        vault.Turn(2 * VaultProgress.Stages);
        vault.Reset(4);
        Assert.AreEqual(0, vault.Stage);
        Assert.AreEqual(0, vault.Turns);
        Assert.AreEqual(4, vault.TurnsPerStage);
        Assert.IsFalse(vault.IsCracked);
    }
}
