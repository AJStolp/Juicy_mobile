using System;

// One clue found in a level (CaseFile): which symbol, and how clearly it
// shows, from the best streak tier of the winning attempt.
public struct Clue
{
    public int Symbol;
    public int Tier;

    public float Covered => ClueRules.CoveredFor(Tier);
}

// What every clue shares: the symbol set (the vault keypad's) and what each
// clarity tier means, on the scratch card and in the vault finale.
public static class ClueRules
{
    public const int SymbolPool = 8;
    public const int MaxTier = 3;

    // How much of a clue stays under grime on its scratch card, by tier.
    public static readonly float[] CoveredByTier = { 0.5f, 0.33f, 0.15f, 0f };

    // How long a clue stays readable on the case file strip during the vault
    // finale, by tier, in seconds: 0 never shows, infinity never hides.
    public static readonly float[] FinaleSecondsByTier = { 0f, 3f, float.PositiveInfinity, float.PositiveInfinity };

    public static int ClampTier(int tier) => Math.Max(0, Math.Min(MaxTier, tier));
    public static float CoveredFor(int tier) => CoveredByTier[ClampTier(tier)];
    public static float FinaleSecondsFor(int tier) => FinaleSecondsByTier[ClampTier(tier)];
}
