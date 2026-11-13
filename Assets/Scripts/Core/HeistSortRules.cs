using System;
using UnityEngine;

public enum ItemKind { Gem, Bomb, Gold, Letter }

public enum SortResult { Correct, Wrong, Bomb, Gold, Letter }

// The heist sort rules with no presentation. A view reports what the player
// did with each falling item; the rules keep score, heat, streak, vats and
// progression, and announce every change through events.
//
// Two bins: side 0 takes color 0, side 1 takes color 1. Chutes open one per
// leg, and a leg advances after VatFillsPerLeg vat fills.
public class HeistSortRules
{
    public const int Sides = 2;
    public const float MaxHeat = 100f;
    public const float MisSortHeat = 22f;
    public const float HazardHeat = 34f;
    public const float ErrorHeat = 14f;
    public const float BleedPerSecond = 10f;
    public const int TierStreak1 = 5;
    public const int TierStreak2 = 10;
    public const int TierStreak3 = 15;
    public const double StartingBalance = 1000d;

    static readonly string[] Words = { "HEIST", "VAULT", "ESCAPE" };
    const double WordBonus = 2500d;
    const double HeistWordBonus = 25000d;

    public float BombChance = 0.15f;
    public float LetterChance = 0.10f;
    public float GoldChance = 0.10f;
    public int CorrectsToFillVat = 3;
    public float PenaltyStep = 0.1f;
    public int VatFillsPerLeg = 4;
    public int MaxChutes = 4;
    public double CashPerSort = 50d;
    public double CashPerGold = 150d;
    public int DynamiteCap = 3;

    public event Action<SortResult, int> Sorted;
    public event Action<int> VatFilled;
    public event Action<int, int> LegAdvanced;
    public event Action<float> HeatChanged;
    public event Action Busted;
    public event Action<double, double> CashWon;
    public event Action<int, int> StreakChanged;
    public event Action<char, string, int> LetterBanked;
    public event Action<string, double> WordCompleted;
    public event Action<GearKind, int> GearChanged;

    readonly int[] craft = new int[Sides];
    readonly float[] vat = new float[Sides];
    readonly int[] gear = new int[Enum.GetValues(typeof(GearKind)).Length];
    int legBestTier;
    bool[] filled;
    bool cooling;
    int wordIndex;

    public int Leg { get; private set; } = 1;
    public int VatFills { get; private set; }
    public float Heat { get; private set; }
    public int Streak { get; private set; }
    public int Tier { get; private set; }
    public double Balance { get; private set; } = StartingBalance;
    public int Chutes => Mathf.Clamp(Leg, 1, MaxChutes);
    public float HeatNormalized => Heat / MaxHeat;
    public string ActiveWord => Words[wordIndex];
    public int LettersFilled { get; private set; }
    // Plinko coins earned by the leg that just finished.
    public int LastLegCoins { get; private set; } = 1;

    public HeistSortRules()
    {
        LoadWord();
    }

    public int Craft(int side) => craft[side];
    public float Vat(int side) => vat[side];
    public int GearCount(GearKind kind) => gear[(int)kind];

    public float CashMultiplier
    {
        get
        {
            if (Tier >= 3) return 5f;
            if (Tier == 2) return 3f;
            if (Tier == 1) return 2f;
            return 1f;
        }
    }

    public static int TierFor(int streak)
    {
        if (streak >= TierStreak3) return 3;
        if (streak >= TierStreak2) return 2;
        if (streak >= TierStreak1) return 1;
        return 0;
    }

    // -1 left, +1 right, 2 up (trash), 0 nothing.
    public static int FlickDirection(Vector2 start, Vector2 end, float minDistance)
    {
        Vector2 delta = end - start;
        if (delta.magnitude < minDistance) return 0;
        if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
            return delta.y > 0f ? 2 : 0;
        return delta.x < 0f ? -1 : 1;
    }

    public ItemKind KindForRoll(float roll)
    {
        if (roll < BombChance) return ItemKind.Bomb;
        if (roll < BombChance + LetterChance) return ItemKind.Letter;
        if (roll < BombChance + LetterChance + GoldChance) return ItemKind.Gold;
        return ItemKind.Gem;
    }

    // Letters are drawn from the word in progress, preferring ones still missing.
    public char PickLetter(float roll)
    {
        string word = ActiveWord;
        int missing = word.Length - LettersFilled;
        int pick = Mathf.Clamp((int)(roll * Mathf.Max(1, missing)), 0, Mathf.Max(0, missing - 1));
        for (int i = 0; i < word.Length; i++)
        {
            if (filled[i]) continue;
            if (pick-- == 0) return word[i];
        }
        return word[0];
    }

    public void Tick(float dt)
    {
        if (!cooling || Heat <= 0f || dt <= 0f) return;
        SetHeat(Mathf.Max(0f, Heat - BleedPerSecond * dt));
    }

    // The player flicked an item into a bin.
    public SortResult Catch(ItemKind kind, int colorId, int side, char letter = '\0')
    {
        SortResult result;
        switch (kind)
        {
            case ItemKind.Bomb:
                LoseCraft(1);
                BreakStreak();
                Fault(HazardHeat);
                result = SortResult.Bomb;
                break;
            case ItemKind.Letter:
                Bank(letter);
                result = SortResult.Letter;
                break;
            case ItemKind.Gold:
                GrantGold();
                result = SortResult.Gold;
                break;
            default:
                if (colorId == side)
                {
                    FillVat(side);
                    Succeed();
                    Pay(CashPerSort);
                    result = SortResult.Correct;
                }
                else
                {
                    vat[side] = Mathf.Max(0f, vat[side] - PenaltyStep);
                    BreakStreak();
                    Fault(MisSortHeat);
                    result = SortResult.Wrong;
                }
                break;
        }
        Sorted?.Invoke(result, side);
        return result;
    }

    // The player flicked an item up and away. Dynamite goes in the pocket.
    public void Trash(ItemKind kind)
    {
        if (kind == ItemKind.Gold) GrantGold();
        else if (kind == ItemKind.Gem) Fault(ErrorHeat);
        else if (kind == ItemKind.Bomb) AddGear(GearKind.Dynamite, 1);
    }

    public int GearCap(GearKind kind) => kind == GearKind.Dynamite ? DynamiteCap : int.MaxValue;

    public void AddGear(GearKind kind, int amount)
    {
        int index = (int)kind;
        int next = Mathf.Clamp(gear[index] + amount, 0, GearCap(kind));
        if (next == gear[index]) return;
        gear[index] = next;
        GearChanged?.Invoke(kind, next);
    }

    // Cash from outside the sort, such as a Plinko slot. No streak multiplier.
    public void Award(double amount)
    {
        if (amount <= 0d) return;
        Balance += amount;
        CashWon?.Invoke(amount, Balance);
    }

    public void Restore(double balance, int[] savedGear)
    {
        Balance = balance;
        for (int i = 0; i < gear.Length; i++)
            gear[i] = savedGear != null && i < savedGear.Length ? Mathf.Clamp(savedGear[i], 0, GearCap((GearKind)i)) : 0;
    }

    // An item reached the ground untouched.
    public void Miss(ItemKind kind)
    {
        if (kind == ItemKind.Gem) Fault(ErrorHeat);
    }

    void FillVat(int side)
    {
        craft[side]++;
        vat[side] = Mathf.Min(1f, vat[side] + 1f / Mathf.Max(1, CorrectsToFillVat));
        if (vat[side] < 0.999f) return;

        vat[side] = 0f;
        VatFills++;
        VatFilled?.Invoke(side);
        if (VatFills % Mathf.Max(1, VatFillsPerLeg) != 0) return;
        LastLegCoins = PlinkoPayout.CoinsFor(legBestTier);
        legBestTier = Tier;
        Leg++;
        LegAdvanced?.Invoke(Leg, Chutes);
    }

    void GrantGold()
    {
        craft[0] += 2;
        craft[1] += 2;
        Pay(CashPerGold);
    }

    void LoseCraft(int amount)
    {
        for (int i = 0; i < Sides; i++) craft[i] = Mathf.Max(0, craft[i] - amount);
    }

    void Pay(double amount)
    {
        double won = amount * CashMultiplier;
        Balance += won;
        CashWon?.Invoke(won, Balance);
    }

    void Succeed()
    {
        cooling = true;
        Streak++;
        int next = TierFor(Streak);
        if (next != Tier) Tier = next;
        legBestTier = Mathf.Max(legBestTier, Tier);
        StreakChanged?.Invoke(Streak, Tier);
    }

    void BreakStreak()
    {
        if (Streak == 0 && Tier == 0) return;
        Streak = 0;
        Tier = 0;
        StreakChanged?.Invoke(0, 0);
    }

    void Fault(float amount)
    {
        cooling = false;
        SetHeat(Mathf.Min(MaxHeat, Heat + amount));
        if (Heat >= MaxHeat) Bust();
    }

    void SetHeat(float value)
    {
        if (Mathf.Approximately(value, Heat)) return;
        Heat = value;
        HeatChanged?.Invoke(Heat);
    }

    // Max heat: the stage loot is lost and the chute restarts on this leg.
    void Bust()
    {
        LoseCraft(int.MaxValue / 2);
        vat[0] = vat[1] = 0f;
        BreakStreak();
        cooling = false;
        SetHeat(0f);
        Busted?.Invoke();
    }

    void Bank(char letter)
    {
        char upper = char.ToUpperInvariant(letter);
        string word = ActiveWord;
        for (int i = 0; i < word.Length; i++)
        {
            if (filled[i] || word[i] != upper) continue;
            filled[i] = true;
            LettersFilled++;
            LetterBanked?.Invoke(upper, word, LettersFilled);
            if (LettersFilled >= word.Length) CompleteWord();
            return;
        }
    }

    void CompleteWord()
    {
        string word = ActiveWord;
        double bonus = word == "HEIST" ? HeistWordBonus : WordBonus;
        Balance += bonus;
        WordCompleted?.Invoke(word, bonus);
        CashWon?.Invoke(bonus, Balance);
        wordIndex = (wordIndex + 1) % Words.Length;
        LoadWord();
    }

    void LoadWord()
    {
        filled = new bool[ActiveWord.Length];
        LettersFilled = 0;
    }

    public bool IsFilled(int index) => filled[index];
}
