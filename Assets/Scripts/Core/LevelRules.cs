using System;
using System.Collections.Generic;

public enum LevelState { Playing, Won, Failed }

// What wins or fails one level, with no presentation. A level (LevelSpec)
// asks for a number of gems sorted correctly, and drops enough gems for that
// with some to spare, mixed with its dynamite and cash stacks (ItemKind.Gold). Everything falls in the
// same order on every attempt, set by the level's seed, so a fail is the
// player's doing and not the draw's.
//
// Misses left is how many more gems the player can lose and still reach the
// target. A lost gem costs one. Dynamite must be flicked up: letting it land
// or sorting it into a chest costs one. Cash in either chest, or flicked up,
// counts as two sorts, which gives two back. Cash still to fall is a bonus and
// is not counted ahead. The level fails the moment misses left would drop
// below zero, so a lost level ends straight away and the player saw it coming
// on the counter.
public class LevelRules
{
    public const int GoldWorth = 2;

    public struct Item
    {
        public ItemKind Kind;
        public int ColorId;
        public float ChuteRoll;
    }

    readonly HeistSortRules rules;
    readonly List<Item> order = new List<Item>();
    int gemsResolved;
    int penalties;

    public LevelSpec Spec { get; }
    public int Target => Spec.Target;
    public int Drops => order.Count;
    public int Gems { get; }

    public LevelState State { get; private set; } = LevelState.Playing;
    public int Sorted { get; private set; }
    // Gems lost plus dynamite let off, for the report.
    public int Lost { get; private set; }
    public int GoldCaught { get; private set; }
    // Best streak tier (0 to 3) reached in this attempt; sets how clearly the
    // level's clue shows on a win.
    public int BestTier { get; private set; }
    public int Dropped { get; private set; }
    public float Elapsed { get; private set; }

    public event Action<LevelRules> Ended;

    public LevelRules(HeistSortRules sortRules, LevelSpec spec)
    {
        rules = sortRules;
        Spec = spec;
        BuildOrder(spec);
        foreach (Item item in order)
            if (item.Kind == ItemKind.Gem) Gems++;
        rules.HeatOn = false;
        rules.Sorted += OnSorted;
        rules.Missed += OnMissed;
        rules.Trashed += OnTrashed;
        rules.StreakChanged += OnStreak;
    }

    // The first drops are always gems, so the level's hint is read before
    // anything new falls.
    public const int OpeningGems = 4;

    // Target plus Misses gems, the level's dynamite and cash, shuffled by the
    // seed after the opening gems.
    void BuildOrder(LevelSpec spec)
    {
        var random = new Random(spec.Seed);
        var kinds = new List<ItemKind>();
        for (int i = 0; i < spec.Gems; i++) kinds.Add(ItemKind.Gem);
        for (int i = 0; i < Math.Max(0, spec.Dynamite); i++) kinds.Add(ItemKind.Bomb);
        for (int i = 0; i < Math.Max(0, spec.Cash); i++) kinds.Add(ItemKind.Gold);
        int fixedStart = Math.Min(OpeningGems, spec.Gems);
        for (int i = kinds.Count - 1; i > fixedStart; i--)
        {
            int j = fixedStart + random.Next(i - fixedStart + 1);
            (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
        }
        foreach (ItemKind kind in kinds)
        {
            int color = random.Next(HeistSortRules.Sides);
            float chute = (float)random.NextDouble();
            order.Add(new Item { Kind = kind, ColorId = kind == ItemKind.Gem ? color : 0, ChuteRoll = chute });
        }
    }

    // Stops listening, so a finished level never reacts to the next attempt.
    public void Dispose()
    {
        rules.Sorted -= OnSorted;
        rules.Missed -= OnMissed;
        rules.Trashed -= OnTrashed;
        rules.StreakChanged -= OnStreak;
    }

    void OnStreak(int streak, int tier)
    {
        if (Playing && tier > BestTier) BestTier = tier;
    }

    public bool Playing => State == LevelState.Playing;

    public int MissesLeft => Math.Max(0, Room);

    int Room => Sorted + (Gems - gemsResolved) - penalties - Target;

    public bool CanDrop => Playing && Dropped < order.Count;

    // How many more correct sorts would have won it. 0 on a win.
    public int ShortBy => State == LevelState.Won ? 0 : Math.Max(0, Target - Sorted);

    // The next item in the level's order. False once everything has fallen.
    public bool Drop(out Item item)
    {
        item = default;
        if (!CanDrop) return false;
        item = order[Dropped++];
        return true;
    }

    public void Tick(float dt)
    {
        if (Playing && dt > 0f) Elapsed += dt;
    }

    void OnSorted(SortResult result, int side)
    {
        if (!Playing) return;
        switch (result)
        {
            case SortResult.Correct: GemDone(true); break;
            case SortResult.Wrong: GemDone(false); break;
            case SortResult.Gold: Gold(); break;
            case SortResult.Bomb: Penalty(); break;
        }
    }

    void OnMissed(ItemKind kind)
    {
        if (!Playing) return;
        if (kind == ItemKind.Gem) GemDone(false);
        else if (kind == ItemKind.Bomb) Penalty();
    }

    void OnTrashed(ItemKind kind)
    {
        if (!Playing) return;
        if (kind == ItemKind.Gem) GemDone(false);
        else if (kind == ItemKind.Gold) Gold();
    }

    void GemDone(bool correct)
    {
        gemsResolved++;
        if (correct) Sorted++;
        else Lost++;
        Check();
    }

    void Gold()
    {
        GoldCaught++;
        Sorted += GoldWorth;
        Check();
    }

    void Penalty()
    {
        penalties++;
        Lost++;
        Check();
    }

    void Check()
    {
        if (Sorted >= Target) End(LevelState.Won);
        else if (Room < 0) End(LevelState.Failed);
    }

    void End(LevelState state)
    {
        if (!Playing) return;
        State = state;
        Ended?.Invoke(this);
    }
}
