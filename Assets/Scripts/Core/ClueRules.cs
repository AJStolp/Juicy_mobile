using System;
using System.Collections.Generic;

// One clue the crew uncovered after a leg: which symbol, and how clearly it
// shows, from the best streak tier reached during that leg.
public struct Clue
{
    public int Symbol;
    public int Tier;

    public float SecondsShown => ClueRules.SecondsShownFor(Tier);
    public float Covered => ClueRules.CoveredFor(Tier);
}

// What one hack paid. Apply hands it to the rules and the vault.
public struct HackReward
{
    public int Steps;
    public bool Perfect;
    public double Cash;
    public int Bribes;
    public int Drills;
    public int Turns;

    public void Apply(HeistSortRules rules, VaultProgress vault)
    {
        rules.Award(Cash);
        if (Bribes > 0) rules.AddGear(GearKind.Bribe, Bribes);
        if (Drills > 0) rules.AddGear(GearKind.Drill, Drills);
        vault.Turn(Turns);
    }
}

// The clue cycle and the vault keypad hack, with no presentation. Each leg
// uncovers one clue symbol; when the cycle holds enough clues the hack is
// due, and the player taps the clues back in the order they were found. Each
// correct tap climbs the reward ladder; the first wrong tap ends the hack and
// the steps reached are kept. Finishing a hack starts a new cycle.
public class ClueRules
{
    public const int SymbolPool = 8;
    public const int CluesPerHackFirstHeist = 3;
    public const int CluesPerHackLater = 4;
    public const int MaxTier = 3;
    public const int PerfectHackTurns = 4;
    public const double LadderSmallCash = 500d;
    public const double LadderBigCash = 1000d;

    // Clarity by the leg's best streak tier, 0 to 3: how long the clue shows
    // and how much of it stays covered (0 is fully visible).
    public static readonly float[] SecondsShownByTier = { 1f, 1.75f, 2.5f, 3.5f };
    public static readonly float[] CoveredByTier = { 0.5f, 0.33f, 0.15f, 0f };

    enum Rung { SmallCash, BigCash, Bribe, Drill }

    static readonly Rung[] LadderOfThree = { Rung.SmallCash, Rung.Bribe, Rung.Drill };
    static readonly Rung[] LadderOfFour = { Rung.SmallCash, Rung.BigCash, Rung.Bribe, Rung.Drill };

    readonly VaultProgress vault;
    readonly List<Clue> clues = new List<Clue>();

    public ClueRules(VaultProgress vault, int seed)
    {
        this.vault = vault;
        Seed = seed;
    }

    public int Seed { get; private set; }
    // How many hacks have finished; each cycle draws its own symbol order.
    public int Cycle { get; private set; }
    public int Count => clues.Count;
    public IReadOnlyList<Clue> Clues => clues;
    public int CluesNeeded => CluesFor(vault.Heist);
    public bool HackDue => clues.Count >= CluesNeeded;

    public static int CluesFor(int heist) => heist <= 1 ? CluesPerHackFirstHeist : CluesPerHackLater;

    public static int ClampTier(int tier) => Math.Max(0, Math.Min(MaxTier, tier));
    public static float SecondsShownFor(int tier) => SecondsShownByTier[ClampTier(tier)];
    public static float CoveredFor(int tier) => CoveredByTier[ClampTier(tier)];

    // A leg finished with this best streak tier. Returns the clue it uncovered,
    // or null when the cycle is already full and the hack is waiting.
    public Clue? ClueFound(int tier)
    {
        if (HackDue) return null;
        var clue = new Clue { Symbol = SymbolAt(clues.Count), Tier = ClampTier(tier) };
        clues.Add(clue);
        return clue;
    }

    // Correct taps before the first wrong one, checked in order.
    public int StepsReached(IList<int> taps)
    {
        int steps = 0;
        int limit = Math.Min(clues.Count, taps?.Count ?? 0);
        while (steps < limit && taps[steps] == clues[steps].Symbol) steps++;
        return steps;
    }

    // Scores the taps against the cycle's clues, returns what the steps
    // reached pay, and clears the cycle for the next one.
    public HackReward Hack(IList<int> taps)
    {
        var reward = RewardFor(clues.Count, StepsReached(taps));
        clues.Clear();
        Cycle++;
        return reward;
    }

    public static HackReward RewardFor(int clueCount, int steps)
    {
        Rung[] ladder = clueCount >= CluesPerHackLater ? LadderOfFour : LadderOfThree;
        var reward = new HackReward { Steps = steps, Perfect = clueCount > 0 && steps >= clueCount };
        for (int i = 0; i < Math.Min(steps, ladder.Length); i++)
        {
            switch (ladder[i])
            {
                case Rung.SmallCash: reward.Cash += LadderSmallCash; break;
                case Rung.BigCash: reward.Cash += LadderBigCash; break;
                case Rung.Bribe: reward.Bribes++; break;
                case Rung.Drill: reward.Drills++; break;
            }
        }
        if (reward.Perfect) reward.Turns = PerfectHackTurns;
        return reward;
    }

    // The symbol for a position in the current cycle: a seeded shuffle of the
    // pool per cycle, so no symbol repeats within a cycle.
    int SymbolAt(int index)
    {
        var order = new int[SymbolPool];
        for (int i = 0; i < SymbolPool; i++) order[i] = i;
        var random = new Random(unchecked(Seed * 7919 + Cycle));
        for (int i = SymbolPool - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order[index % SymbolPool];
    }

    public int[] SymbolsSnapshot()
    {
        var symbols = new int[clues.Count];
        for (int i = 0; i < symbols.Length; i++) symbols[i] = clues[i].Symbol;
        return symbols;
    }

    public int[] TiersSnapshot()
    {
        var tiers = new int[clues.Count];
        for (int i = 0; i < tiers.Length; i++) tiers[i] = clues[i].Tier;
        return tiers;
    }

    // Loads a saved cycle. Anything missing or malformed loads as an empty cycle.
    public void Restore(int seed, int cycle, int[] symbols, int[] tiers)
    {
        Seed = seed;
        Cycle = Math.Max(0, cycle);
        clues.Clear();
        if (symbols == null || tiers == null || symbols.Length != tiers.Length) return;
        for (int i = 0; i < symbols.Length; i++)
        {
            if (symbols[i] < 0 || symbols[i] >= SymbolPool)
            {
                clues.Clear();
                return;
            }
            clues.Add(new Clue { Symbol = symbols[i], Tier = ClampTier(tiers[i]) });
        }
    }
}
