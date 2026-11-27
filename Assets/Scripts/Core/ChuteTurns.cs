using System;
using System.Collections.Generic;

// Which chute drops next. Every open chute drops once, in a shuffled order,
// before any of them drops again, and no chute drops twice in a row, so the
// drops spread evenly across the street. Seeded, so an attempt's chutes
// repeat. When another chute opens, a fresh round starts that includes it.
public class ChuteTurns
{
    readonly Random random;
    readonly List<int> round = new List<int>();
    int open;
    int last = -1;

    public ChuteTurns(int seed)
    {
        random = new Random(seed);
    }

    // A chute index from 0 to openChutes - 1.
    public int Next(int openChutes)
    {
        openChutes = Math.Max(1, openChutes);
        if (openChutes != open)
        {
            open = openChutes;
            round.Clear();
        }
        if (round.Count == 0) Deal();
        int chute = round[round.Count - 1];
        round.RemoveAt(round.Count - 1);
        last = chute;
        return chute;
    }

    void Deal()
    {
        for (int i = 0; i < open; i++) round.Add(i);
        for (int i = round.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (round[i], round[j]) = (round[j], round[i]);
        }
        // Rounds are drawn from the end; the first draw must not repeat the last chute.
        if (round.Count > 1 && round[round.Count - 1] == last)
            (round[0], round[round.Count - 1]) = (round[round.Count - 1], round[0]);
    }
}
