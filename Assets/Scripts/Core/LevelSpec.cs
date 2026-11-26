using System;

// One level's numbers. A place lists its levels in order (PlaceData), and
// LevelRules and the chute director play whichever one they are given.
[Serializable]
public class LevelSpec
{
    public string Name = "Level";
    [UnityEngine.Tooltip("Correct sorts that win the level.")]
    public int Target = 75;
    [UnityEngine.Tooltip("Gems that fall in all. Running out short of the target fails the level.")]
    public int Gems = 90;
    [UnityEngine.Tooltip("Sets the order the gems fall in, the same on every attempt.")]
    public int Seed = 1;
    [UnityEngine.Tooltip("Seconds between gems.")]
    public float DropEvery = 0.85f;
    [UnityEngine.Tooltip("How fast a gem falls, in metres a second.")]
    public float FallSpeed = 1.6f;
    [UnityEngine.Tooltip("Most chutes that open. One opens at the start and one more each leg.")]
    public int MaxChutes = 4;

    // Most misses a player can give and still win.
    public int Slack => Math.Max(0, Gems - Target);
}
