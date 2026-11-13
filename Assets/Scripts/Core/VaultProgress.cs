using System;
using UnityEngine;

// How far the crew is into the vault, with no presentation. A vault has seven
// stages: four corner bolts, the outer ring, the inner ring and the wheel.
// Turns fill the current stage; a full stage locks for good and the next one
// starts. A bust takes back part of the current stage only. Locking the last
// stage cracks the vault, and the next heist starts on a fresh door.
public class VaultProgress
{
    public const int Stages = 7;

    // Turns per stage for heist 1, 2, 3...; the last entry holds from then on.
    // The first vault opens in one sitting; later ones grow.
    public int[] TurnsByHeist = { 9, 16, 28 };
    [Range(0f, 1f)] public float RollbackFraction = 0.5f;

    public event Action<int, float> Turned;
    public event Action<int> StageLocked;
    public event Action<int, float> RolledBack;
    public event Action<int> Cracked;

    public int Heist { get; private set; } = 1;
    public int Stage { get; private set; }
    public int Turns { get; private set; }

    public int TurnsForStage => TurnsFor(Heist);
    public float StageFraction => (float)Turns / TurnsForStage;

    public int TurnsFor(int heist)
    {
        int index = Mathf.Clamp(heist - 1, 0, TurnsByHeist.Length - 1);
        return Mathf.Max(1, TurnsByHeist[index]);
    }

    public void Turn(int amount)
    {
        if (amount <= 0) return;
        Turns += amount;
        while (Turns >= TurnsForStage)
        {
            Turns -= TurnsForStage;
            int locked = Stage;
            Stage++;
            StageLocked?.Invoke(locked);
            if (Stage < Stages) continue;

            int cracked = Heist;
            Heist++;
            Stage = 0;
            Turns = 0;
            Cracked?.Invoke(cracked);
            return;
        }
        Turned?.Invoke(Stage, StageFraction);
    }

    public void Bust()
    {
        int lost = Mathf.FloorToInt(Turns * RollbackFraction);
        if (lost <= 0) return;
        Turns -= lost;
        RolledBack?.Invoke(Stage, StageFraction);
    }

    public void Restore(int heist, int stage, int turns)
    {
        Heist = Mathf.Max(1, heist);
        Stage = Mathf.Clamp(stage, 0, Stages - 1);
        Turns = Mathf.Clamp(turns, 0, TurnsForStage - 1);
    }
}
