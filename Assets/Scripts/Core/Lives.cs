using System;

// Attempts the player has, with no presentation. Up to Max; losing a level
// (and not keeping it going) costs one, and one comes back every
// RefillSeconds while below Max. Times are Unix seconds, passed in, so the
// rules are testable and the clock lives outside.
public class Lives
{
    public const int Max = 5;
    public const long RefillSeconds = 30 * 60;

    // Lives held at `since`; refills count from `since` while below Max.
    int stored;
    long since;

    public Lives(int count = Max, long sinceSeconds = 0)
    {
        stored = Math.Max(0, Math.Min(Max, count));
        since = sinceSeconds;
    }

    public int Count(long now)
    {
        if (stored >= Max) return Max;
        long gained = Math.Max(0, now - since) / RefillSeconds;
        return (int)Math.Min(Max, stored + gained);
    }

    // Seconds until the next life comes back; 0 when full.
    public long NextIn(long now)
    {
        if (Count(now) >= Max) return 0;
        return RefillSeconds - Math.Max(0, now - since) % RefillSeconds;
    }

    // Takes a life. False when there were none.
    public bool Lose(long now)
    {
        int count = Count(now);
        if (count <= 0) return false;
        Settle(now);
        if (count >= Max) since = now;
        stored = count - 1;
        return true;
    }

    public void Fill()
    {
        stored = Max;
    }

    // Gives back a life taken for an attempt the player then kept going.
    public void Give(long now)
    {
        Settle(now);
        stored = Math.Min(Max, stored + 1);
    }

    // Banks refills that have come in, keeping the part of the next one
    // already waited.
    void Settle(long now)
    {
        // A clock moved backward restarts the wait from now rather than
        // stopping refills until it catches up.
        if (now < since) since = now;
        if (stored >= Max) return;
        long gained = Math.Max(0, now - since) / RefillSeconds;
        stored = (int)Math.Min(Max, stored + gained);
        since += gained * RefillSeconds;
    }

    public int StoredFor(long now)
    {
        Settle(now);
        return stored;
    }

    public long SinceFor(long now)
    {
        Settle(now);
        return since;
    }
}
