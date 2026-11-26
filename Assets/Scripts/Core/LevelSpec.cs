using System;
using UnityEngine;

// One level's numbers. A place lists its levels in order (PlaceData), and
// LevelRules and the chute director play whichever one they are given.
[Serializable]
public class LevelSpec
{
    public string Name = "Level";
    [Tooltip("Shown at the start of the level, for the first time an item falls. Empty for none.")]
    public string Hint = "";
    [Tooltip("Correct sorts that win the level. A cash stack counts as two.")]
    public int Target = 75;
    [Tooltip("Gems the player can lose and still win, before any cash.")]
    public int Misses = 15;
    [Tooltip("Sets the order everything falls in, the same on every attempt.")]
    public int Seed = 1;
    [Tooltip("Dynamite in the level, which must be flicked up.")]
    public int Dynamite;
    [Tooltip("Cash stacks in the level, each worth two gems in either chest.")]
    public int Cash;
    [Tooltip("Seconds between drops.")]
    public float DropEvery = 0.85f;
    [Tooltip("How fast a drop falls, in metres a second.")]
    public float FallSpeed = 1.6f;
    [Tooltip("Most chutes that open. One opens at the start and one more each leg.")]
    public int MaxChutes = 4;

    // Gems in the level: enough for the target with Misses to spare.
    public int Gems => Target + Math.Max(0, Misses);
}
