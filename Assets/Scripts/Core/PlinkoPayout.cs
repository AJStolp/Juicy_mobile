public enum GearKind { Dynamite, Smoke, Bribe, Drill }

public enum PlinkoPrize { Cash, Gear, Vault }

public struct PlinkoReward
{
    public PlinkoPrize Prize;
    public double Cash;
    public GearKind Gear;
    public int Turns;
}

// What each Plinko slot pays, left to right. Physics decides where a coin
// lands; this only pays it out. Cash in most slots, gear in two, and one
// narrow vault slot in the middle that turns the door a big step.
public static class PlinkoPayout
{
    public const double BaseCash = 100d;
    public const int VaultTurns = 4;
    public const int DrillTurns = 2;
    public const int VaultSlot = 4;

    static readonly PlinkoReward[] Slots =
    {
        Cash(10), Gear(GearKind.Drill), Cash(2), Cash(1),
        new PlinkoReward { Prize = PlinkoPrize.Vault, Turns = VaultTurns },
        Cash(1), Cash(2), Gear(GearKind.Bribe), Cash(10),
    };

    public static int SlotCount => Slots.Length;

    public static PlinkoReward For(int slot)
    {
        if (slot < 0) slot = 0;
        if (slot >= Slots.Length) slot = Slots.Length - 1;
        return Slots[slot];
    }

    // Coins for a round: one, plus one for each streak tier reached that leg.
    public static int CoinsFor(int bestTier) => 1 + System.Math.Max(0, System.Math.Min(3, bestTier));

    static PlinkoReward Cash(double multiplier) => new PlinkoReward { Prize = PlinkoPrize.Cash, Cash = BaseCash * multiplier };
    static PlinkoReward Gear(GearKind kind) => new PlinkoReward { Prize = PlinkoPrize.Gear, Gear = kind };
}
