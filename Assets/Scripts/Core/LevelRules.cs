using System;

// What wins or fails one level, with no presentation. A level asks for a
// number of gems sorted correctly; the goal decides what ends it first.
// Prototype: the three goals are switchable until Anthony picks one (T-028).
public enum LevelGoal { Heat, Clock, Gems }

public enum LevelState { Playing, Won, Failed }

public class LevelRules
{
    public const int DefaultTarget = 150;
    public const float DefaultSeconds = 220f;
    public const int DefaultGems = 178;
    public const float MistakeSeconds = 3f;

    readonly HeistSortRules rules;

    public LevelGoal Goal { get; }
    public int Target { get; }
    public float Seconds { get; }
    public int GemLimit { get; }

    public LevelState State { get; private set; } = LevelState.Playing;
    public int Sorted { get; private set; }
    public int Lost { get; private set; }
    public int Dropped { get; private set; }
    public float TimeLeft { get; private set; }
    public float Elapsed { get; private set; }
    public float PeakHeat { get; private set; }

    public event Action<LevelRules> Ended;

    public LevelRules(HeistSortRules sortRules, LevelGoal goal, int target = DefaultTarget, float seconds = DefaultSeconds, int gems = DefaultGems)
    {
        rules = sortRules;
        Goal = goal;
        Target = target;
        Seconds = seconds;
        GemLimit = gems;
        TimeLeft = seconds;
        rules.HeatOn = goal == LevelGoal.Heat;
        rules.Sorted += OnSorted;
        rules.Missed += OnLost;
        rules.Trashed += OnLost;
        rules.Busted += OnBusted;
        rules.HeatChanged += OnHeat;
    }

    // Stops listening, so a finished level never reacts to the next attempt.
    public void Dispose()
    {
        rules.Sorted -= OnSorted;
        rules.Missed -= OnLost;
        rules.Trashed -= OnLost;
        rules.Busted -= OnBusted;
        rules.HeatChanged -= OnHeat;
    }

    public bool Playing => State == LevelState.Playing;

    // Gems still to fall in the limited gems goal.
    public int GemsLeft => Math.Max(0, GemLimit - Dropped);

    public bool CanDrop => Playing && (Goal != LevelGoal.Gems || Dropped < GemLimit);

    // How many more correct sorts would have won it. 0 on a win.
    public int ShortBy => State == LevelState.Won ? 0 : Math.Max(0, Target - Sorted);

    // The room left on a win: heat below the bust at its highest, seconds on
    // the clock, or gems that never had to fall.
    public float Margin
    {
        get
        {
            switch (Goal)
            {
                case LevelGoal.Heat: return HeistSortRules.MaxHeat - PeakHeat;
                case LevelGoal.Clock: return TimeLeft;
                default: return GemsLeft;
            }
        }
    }

    public void Drop()
    {
        if (CanDrop) Dropped++;
    }

    public void Tick(float dt)
    {
        if (!Playing || dt <= 0f) return;
        Elapsed += dt;
        if (Goal != LevelGoal.Clock) return;
        TimeLeft = Math.Max(0f, TimeLeft - dt);
        if (TimeLeft <= 0f) End(LevelState.Failed);
    }

    void OnSorted(SortResult result, int side)
    {
        if (!Playing) return;
        if (result == SortResult.Correct)
        {
            Sorted++;
            if (Sorted >= Target) End(LevelState.Won);
            else CheckGemsSpent();
            return;
        }
        if (result == SortResult.Wrong) OnLost(ItemKind.Gem);
    }

    void OnLost(ItemKind kind)
    {
        if (!Playing || kind != ItemKind.Gem) return;
        Lost++;
        if (Goal == LevelGoal.Clock)
        {
            TimeLeft = Math.Max(0f, TimeLeft - MistakeSeconds);
            if (TimeLeft <= 0f) End(LevelState.Failed);
            return;
        }
        CheckGemsSpent();
    }

    // Every gem has fallen and been sorted, lost or missed.
    void CheckGemsSpent()
    {
        if (Goal == LevelGoal.Gems && Playing && Sorted + Lost >= GemLimit) End(LevelState.Failed);
    }

    void OnBusted()
    {
        if (Goal == LevelGoal.Heat) End(LevelState.Failed);
    }

    void OnHeat(float heat)
    {
        if (heat > PeakHeat) PeakHeat = Math.Min(heat, HeistSortRules.MaxHeat);
    }

    void End(LevelState state)
    {
        if (!Playing) return;
        State = state;
        Ended?.Invoke(this);
    }
}
