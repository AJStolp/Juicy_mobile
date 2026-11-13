using NUnit.Framework;

public class PlinkoPayoutTests
{
    [Test]
    public void OnlyTheMiddleSlotTurnsTheVault()
    {
        for (int slot = 0; slot < PlinkoPayout.SlotCount; slot++)
        {
            bool vault = PlinkoPayout.For(slot).Prize == PlinkoPrize.Vault;
            Assert.AreEqual(slot == PlinkoPayout.VaultSlot, vault, "slot " + slot);
        }
        Assert.AreEqual(PlinkoPayout.SlotCount / 2, PlinkoPayout.VaultSlot);
    }

    [Test]
    public void CashFillsMostSlotsAndDrillIsTheOnlyWayToEarnADrill()
    {
        int cash = 0, drills = 0;
        for (int slot = 0; slot < PlinkoPayout.SlotCount; slot++)
        {
            PlinkoReward reward = PlinkoPayout.For(slot);
            if (reward.Prize == PlinkoPrize.Cash) cash++;
            if (reward.Prize == PlinkoPrize.Gear && reward.Gear == GearKind.Drill) drills++;
        }
        Assert.Greater(cash, PlinkoPayout.SlotCount / 2);
        Assert.AreEqual(1, drills);
    }

    [Test]
    public void OutOfRangeSlotsClampToTheEdges()
    {
        Assert.AreEqual(PlinkoPayout.For(0).Cash, PlinkoPayout.For(-3).Cash);
        Assert.AreEqual(PlinkoPayout.For(PlinkoPayout.SlotCount - 1).Cash, PlinkoPayout.For(99).Cash);
    }

    [Test]
    public void CoinsRunFromOneToFour()
    {
        Assert.AreEqual(1, PlinkoPayout.CoinsFor(0));
        Assert.AreEqual(4, PlinkoPayout.CoinsFor(3));
        Assert.AreEqual(4, PlinkoPayout.CoinsFor(7));
    }
}
