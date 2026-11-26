using System;

public enum LevelState { Playing, Won, Failed }

// What wins or fails one level, with no presentation. A level drops a fixed
// number of gems and asks for a number of them sorted correctly. It fails the
// moment the target can no longer be reached: once more gems are lost than
// the level has to spare. The gems come in the same color order on every
// attempt, set by the level's seed, so a fail is the player's doing and not
// the draw's.
public class LevelRules
{
    public const int DefaultTarget = 75;
    public const int DefaultGems = 90;
    public const int DefaultSeed = 1;

    readonly HeistSortRules rules;
    readonly Random order;

    public int Target { get; }
    public int GemLimit { get; }

    public LevelState State { get; private set; } = LevelState.Playing;
    public int Sorted { get; private set; }
    public int Lost { get; private set; }
    public int Dropped { get; private set; }
    public float Elapsed { get; private set; }

    public event Action<LevelRules> Ended;

    public LevelRules(HeistSortRules sortRules, int target = DefaultTarget, int gems = DefaultGems, int seed = DefaultSeed)
    {
        rules = sortRules;
        Target = target;
        GemLimit = gems;
        order = new Random(seed);
        rules.HeatOn = false;
        rules.Sorted += OnSorted;
        rules.Missed += OnLost;
        rules.Trashed += OnLost;
    }

    // Stops listening, so a finished level never reacts to the next attempt.
    public void Dispose()
    {
        rules.Sorted -= OnSorted;
        rules.Missed -= OnLost;
        rules.Trashed -= OnLost;
    }

    public bool Playing => State == LevelState.Playing;

    // Gems still to fall.
    public int GemsLeft => Math.Max(0, GemLimit - Dropped);

    // Gems the player can still lose and win. On a win, the ones never needed.
    public int MissesLeft => Math.Max(0, GemLimit - Target - Lost);

    public bool CanDrop => Playing && Dropped < GemLimit;

    // How many more correct sorts would have won it. 0 on a win.
    public int ShortBy => State == LevelState.Won ? 0 : Math.Max(0, Target - Sorted);

    // The next gem in the level's order: its color, and a roll in [0, 1) for
    // which open chute it falls from. False once every gem has fallen.
    public bool Drop(out int colorId, out float chuteRoll)
    {
        colorId = 0;
        chuteRoll = 0f;
        if (!CanDrop) return false;
        Dropped++;
        colorId = order.Next(HeistSortRules.Sides);
        chuteRoll = (float)order.NextDouble();
        return true;
    }

    public void Tick(float dt)
    {
        if (Playing && dt > 0f) Elapsed += dt;
    }

    void OnSorted(SortResult result, int side)
    {
        if (!Playing) return;
        if (result == SortResult.Correct)
        {
            Sorted++;
            if (Sorted >= Target) End(LevelState.Won);
            return;
        }
        if (result == SortResult.Wrong) OnLost(ItemKind.Gem);
    }

    void OnLost(ItemKind kind)
    {
        if (!Playing || kind != ItemKind.Gem) return;
        Lost++;
        if (Lost > GemLimit - Target) End(LevelState.Failed);
    }

    void End(LevelState state)
    {
        if (!Playing) return;
        State = state;
        Ended?.Invoke(this);
    }
}
