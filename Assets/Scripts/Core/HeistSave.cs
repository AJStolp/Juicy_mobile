using System;
using UnityEngine;

// What carries over between sittings: where the vault is, the cash, the gear
// and the unfinished clue cycle. Legs, chutes, heat and streak start fresh
// each sitting by design.
[Serializable]
public class HeistSave
{
    public int heist = 1;
    public int stage;
    public int turns;
    public double balance = HeistSortRules.StartingBalance;
    public int[] gear = new int[Enum.GetValues(typeof(GearKind)).Length];
    public int clueSeed;
    public int clueCycle;
    public int[] clueSymbols = new int[0];
    public int[] clueTiers = new int[0];

    public static HeistSave Capture(VaultProgress vault, HeistSortRules rules, ClueRules clues)
    {
        var save = new HeistSave
        {
            heist = vault.Heist,
            stage = vault.Stage,
            turns = vault.Turns,
            balance = rules.Balance,
            clueSeed = clues.Seed,
            clueCycle = clues.Cycle,
            clueSymbols = clues.SymbolsSnapshot(),
            clueTiers = clues.TiersSnapshot(),
        };
        for (int i = 0; i < save.gear.Length; i++) save.gear[i] = rules.GearCount((GearKind)i);
        return save;
    }

    public void Apply(VaultProgress vault, HeistSortRules rules, ClueRules clues)
    {
        vault.Restore(heist, stage, turns);
        rules.Restore(balance, gear);
        clues.Restore(clueSeed, clueCycle, clueSymbols, clueTiers);
    }

    public string ToJson() => JsonUtility.ToJson(this);

    public static HeistSave FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonUtility.FromJson<HeistSave>(json);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
