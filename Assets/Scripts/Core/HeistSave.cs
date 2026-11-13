using System;
using UnityEngine;

// What carries over between sittings: where the vault is, the cash and the
// gear. Legs, chutes, heat and streak start fresh each sitting by design.
[Serializable]
public class HeistSave
{
    public int heist = 1;
    public int stage;
    public int turns;
    public double balance = HeistSortRules.StartingBalance;
    public int[] gear = new int[Enum.GetValues(typeof(GearKind)).Length];

    public static HeistSave Capture(VaultProgress vault, HeistSortRules rules)
    {
        var save = new HeistSave
        {
            heist = vault.Heist,
            stage = vault.Stage,
            turns = vault.Turns,
            balance = rules.Balance,
        };
        for (int i = 0; i < save.gear.Length; i++) save.gear[i] = rules.GearCount((GearKind)i);
        return save;
    }

    public void Apply(VaultProgress vault, HeistSortRules rules)
    {
        vault.Restore(heist, stage, turns);
        rules.Restore(balance, gear);
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
