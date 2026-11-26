using System;

// How far the player has come through one place: its levels are beaten in
// order, and the next one after the last beaten is open. Beaten levels stay
// open to replay. A fail never takes progress back.
public class PlaceProgress
{
    public int Levels { get; }
    public int Beaten { get; private set; }

    public event Action<int> Changed;

    public PlaceProgress(int levels)
    {
        Levels = Math.Max(1, levels);
    }

    public bool IsBeaten(int index) => index >= 0 && index < Beaten;
    public bool IsOpen(int index) => index >= 0 && index <= Math.Min(Beaten, Levels - 1);
    public bool AllBeaten => Beaten >= Levels;

    // The level to play next: the first not yet beaten, or the last.
    public int Next => Math.Min(Beaten, Levels - 1);

    public void Won(int index)
    {
        if (!IsOpen(index) || index < Beaten) return;
        Beaten = index + 1;
        Changed?.Invoke(Beaten);
    }

    public void Restore(int beaten)
    {
        Beaten = Math.Max(0, Math.Min(beaten, Levels));
        Changed?.Invoke(Beaten);
    }
}
