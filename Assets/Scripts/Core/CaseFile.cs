using System;

// The clues of one place, one per level, kept on the crime board. Each
// level's clue is a fixed symbol from the vault keypad's set, drawn by the
// place's seed with no repeats in the first eight levels (a place has six).
// Winning a level finds its clue, as clearly as the best streak tier of that
// attempt; a replay can make it clearer, never less clear.
public class CaseFile
{
    public const int Unfound = -1;

    readonly int[] symbols;
    readonly int[] tiers;

    public CaseFile(int levels, int seed)
    {
        levels = Math.Max(1, levels);
        symbols = new int[levels];
        tiers = new int[levels];
        var pool = new int[ClueRules.SymbolPool];
        for (int i = 0; i < pool.Length; i++) pool[i] = i;
        var random = new Random(seed);
        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        for (int i = 0; i < levels; i++)
        {
            symbols[i] = pool[i % pool.Length];
            tiers[i] = Unfound;
        }
    }

    public bool Found(int index) => InRange(index) && tiers[index] != Unfound;

    public Clue ClueAt(int index) => new Clue { Symbol = symbols[index], Tier = Math.Max(0, tiers[index]) };

    // Records a win's clue. True when it is new or clearer than before.
    public bool Record(int index, int tier)
    {
        if (!InRange(index)) return false;
        int clamped = ClueRules.ClampTier(tier);
        if (clamped <= tiers[index]) return false;
        tiers[index] = clamped;
        return true;
    }

    public int[] TiersSnapshot() => (int[])tiers.Clone();

    public void Restore(int[] saved)
    {
        for (int i = 0; i < tiers.Length; i++)
            tiers[i] = saved != null && i < saved.Length && saved[i] >= 0 ? ClueRules.ClampTier(saved[i]) : Unfound;
    }

    bool InRange(int index) => index >= 0 && index < tiers.Length;
}
