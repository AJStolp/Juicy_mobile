using System;
using UnityEngine;

// How far the crew is into the vault during one boss attempt, with no
// presentation. A vault has seven stages: four corner bolts, the outer ring,
// the inner ring and the wheel. Turns fill the current stage; a full stage
// locks and the next one starts. Locking the last stage cracks the vault.
// Every boss attempt starts on a fresh door (Reset).
public class VaultProgress
{
    public const int Stages = 7;

    public event Action<int> StageLocked;
    public event Action Cracked;
    // A fresh door: every stage open again.
    public event Action Restarted;

    public int TurnsPerStage { get; private set; } = 1;
    public int Stage { get; private set; }
    public int Turns { get; private set; }
    public bool IsCracked => Stage >= Stages;

    public float StageFraction => IsCracked ? 1f : (float)Turns / TurnsPerStage;

    public void Reset(int turnsPerStage)
    {
        TurnsPerStage = Mathf.Max(1, turnsPerStage);
        Stage = 0;
        Turns = 0;
        Restarted?.Invoke();
    }

    public void Turn(int amount)
    {
        if (amount <= 0 || IsCracked) return;
        Turns += amount;
        while (Turns >= TurnsPerStage)
        {
            Turns -= TurnsPerStage;
            int locked = Stage;
            Stage++;
            StageLocked?.Invoke(locked);
            if (Stage < Stages) continue;
            Turns = 0;
            Cracked?.Invoke();
            return;
        }
    }

    // Locks every stage up to (not including) `stage`, for a door that must
    // show the sort done before the finale.
    public void LockThrough(int stage)
    {
        while (Stage < Mathf.Min(stage, Stages) && !IsCracked) Turn(TurnsPerStage - Turns);
    }

    // Poses progress directly, for editor stills.
    public void Restore(int stage, int turns)
    {
        Stage = Mathf.Clamp(stage, 0, Stages - 1);
        Turns = Mathf.Clamp(turns, 0, TurnsPerStage - 1);
    }
}
