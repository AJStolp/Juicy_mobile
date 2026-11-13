using NUnit.Framework;

public class VaultProgressTests
{
    static VaultProgress Vault(int turnsPerStage)
    {
        return new VaultProgress { TurnsByHeist = new[] { turnsPerStage } };
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
        vault.Turn(PlinkoPayout.VaultTurns);
        Assert.AreEqual(2, locks);
        Assert.AreEqual(2, vault.Stage);
        Assert.AreEqual(0, vault.Turns);
    }

    [Test]
    public void BustTakesBackHalfOfTheCurrentStageOnly()
    {
        var vault = Vault(10);
        vault.Turn(10);
        vault.Turn(6);
        float rolledTo = -1f;
        vault.RolledBack += (_, f) => rolledTo = f;
        vault.Bust();
        Assert.AreEqual(1, vault.Stage);
        Assert.AreEqual(3, vault.Turns);
        Assert.AreEqual(0.3f, rolledTo, 1e-4f);
    }

    [Test]
    public void BustWithNothingToLoseDoesNothing()
    {
        var vault = Vault(10);
        vault.Turn(10);
        bool rolled = false;
        vault.RolledBack += (_, __) => rolled = true;
        vault.Bust();
        Assert.IsFalse(rolled);
        Assert.AreEqual(1, vault.Stage);
    }

    [Test]
    public void LockingTheLastStageCracksTheVaultAndStartsTheNextHeist()
    {
        var vault = Vault(2);
        int cracked = 0;
        vault.Cracked += h => cracked = h;
        vault.Turn(2 * VaultProgress.Stages - 1);
        Assert.AreEqual(0, cracked);
        vault.Turn(1 + PlinkoPayout.VaultTurns);
        Assert.AreEqual(1, cracked);
        Assert.AreEqual(2, vault.Heist);
        Assert.AreEqual(0, vault.Stage);
        Assert.AreEqual(0, vault.Turns);
    }

    [Test]
    public void LaterVaultsTakeMoreTurns()
    {
        var vault = new VaultProgress();
        Assert.Less(vault.TurnsFor(1), vault.TurnsFor(2));
        Assert.Less(vault.TurnsFor(2), vault.TurnsFor(3));
        Assert.AreEqual(vault.TurnsFor(3), vault.TurnsFor(9));
    }
}
