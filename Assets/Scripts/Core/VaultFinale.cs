using System;
using System.Collections.Generic;

public enum FinaleTap { Right, Wrong, Cracked, Tripped }

// The end of a vault boss, with no presentation: the player taps the place's
// clues on the keypad in the order they were found. A right tap moves on; a
// wrong tap sounds an alarm and the same clue is still wanted. The last right
// tap cracks the vault; running out of alarms trips it.
public class VaultFinale
{
    public const int Alarms = 3;

    readonly List<int> order = new List<int>();

    public VaultFinale(IEnumerable<int> symbols)
    {
        order.AddRange(symbols);
        AlarmsLeft = Alarms;
        if (order.Count == 0) Cracked = true;
    }

    public int Count => order.Count;
    public IReadOnlyList<int> Code => order;
    public int Step { get; private set; }
    public int AlarmsLeft { get; private set; }
    public bool Cracked { get; private set; }
    public bool Tripped { get; private set; }
    public bool Done => Cracked || Tripped;
    public int Expected => Done ? -1 : order[Step];

    public FinaleTap Tap(int symbol)
    {
        if (Done) throw new InvalidOperationException("The finale is over");
        if (symbol == order[Step])
        {
            Step++;
            if (Step < order.Count) return FinaleTap.Right;
            Cracked = true;
            return FinaleTap.Cracked;
        }
        AlarmsLeft--;
        if (AlarmsLeft > 0) return FinaleTap.Wrong;
        Tripped = true;
        return FinaleTap.Tripped;
    }
}
