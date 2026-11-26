using System;
using UnityEngine;

// What carries over between sittings: the cash, the gear, how far through
// the place the player is (levels beaten, the vault cracked) and the clues
// found. Each level and vault attempt starts fresh by design.
[Serializable]
public class HeistSave
{
    public double balance = HeistSortRules.StartingBalance;
    public int[] gear = new int[Enum.GetValues(typeof(GearKind)).Length];
    public int placeBeaten;
    public bool vaultCracked;
    public int[] placeClueTiers = new int[0];

    public static HeistSave Capture(HeistSortRules rules, PlaceProgress place, CaseFile caseFile)
    {
        var save = new HeistSave
        {
            balance = rules.Balance,
            placeBeaten = place.Beaten,
            vaultCracked = place.VaultCracked,
            placeClueTiers = caseFile.TiersSnapshot(),
        };
        for (int i = 0; i < save.gear.Length; i++) save.gear[i] = rules.GearCount((GearKind)i);
        return save;
    }

    public void Apply(HeistSortRules rules, PlaceProgress place, CaseFile caseFile)
    {
        rules.Restore(balance, gear);
        place.Restore(placeBeaten, vaultCracked);
        caseFile.Restore(placeClueTiers);
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
