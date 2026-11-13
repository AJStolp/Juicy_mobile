using UnityEngine;

// The alley answers the game: the raccoon pushes on sorts, vat fills and vault
// stages, the police strobe and vault alarms run hotter with heat, the camera
// shakes on mistakes and busts, and the existing sound hooks play.
public class AlleyReactions : MonoBehaviour
{
    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Animator raccoon;
    [SerializeField] LightAnimator[] heatLights;
    [SerializeField] AlleyCameraRig cameraRig;
    [SerializeField] double bigWinChime = 150d;

    HeistSortRules rules;
    VaultProgress vault;
    float shownHeat;

    public int PushLights { get; private set; }
    public int PushHeavies { get; private set; }
    public int Legs { get; private set; }
    public int Busts { get; private set; }
    public float ShownHeat => shownHeat;

    public void Configure(AlleyChuteDirector chute, Animator mascot, LightAnimator[] lights, AlleyCameraRig rig)
    {
        director = chute;
        raccoon = mascot;
        heatLights = lights;
        cameraRig = rig;
    }

    void Start()
    {
        rules = director.Rules;
        rules.Sorted += OnSorted;
        rules.VatFilled += OnVatFilled;
        rules.LegAdvanced += OnLegAdvanced;
        rules.CashWon += OnCashWon;
        rules.Busted += OnBusted;
        vault = director.Vault;
        vault.StageLocked += OnStageLocked;
        vault.Cracked += OnCracked;
    }

    void OnDestroy()
    {
        if (rules == null) return;
        rules.Sorted -= OnSorted;
        rules.VatFilled -= OnVatFilled;
        rules.LegAdvanced -= OnLegAdvanced;
        rules.CashWon -= OnCashWon;
        rules.Busted -= OnBusted;
        if (vault == null) return;
        vault.StageLocked -= OnStageLocked;
        vault.Cracked -= OnCracked;
    }

    void OnSorted(SortResult result, int side)
    {
        if (result == SortResult.Wrong || result == SortResult.Bomb)
        {
            Shake(result == SortResult.Bomb ? 0.35f : 0.15f);
            return;
        }
        JuicyAudioHooks.Sort();
        if (result != SortResult.Correct && result != SortResult.Gold) return;
        // Only start a light push from idle, so pushes do not stack up.
        if (raccoon == null || !raccoon.GetCurrentAnimatorStateInfo(0).IsName("Torch_Idle") || raccoon.IsInTransition(0)) return;
        raccoon.SetTrigger("onPushLight");
        PushLights++;
    }

    void OnVatFilled(int side)
    {
        JuicyAudioHooks.Vault();
        PushHeavy();
    }

    // A stage locking on the vault is a big moment; so is cracking it. He
    // has no celebration clip yet, so both get the heavy push.
    void OnStageLocked(int stage) => PushHeavy();

    void OnCracked(int heist)
    {
        PushHeavy();
        JuicyAudioHooks.Getaway();
    }

    void PushHeavy()
    {
        if (raccoon == null) return;
        raccoon.ResetTrigger("onPushLight");
        raccoon.SetTrigger("onPushHeavy");
        PushHeavies++;
    }

    // A leg advance always lands on a vat fill, which already plays the
    // heavy push; the raccoon stands in the open, so there is nothing to climb.
    void OnLegAdvanced(int leg, int chutes)
    {
        Legs++;
    }

    void OnCashWon(double amount, double balance)
    {
        if (amount >= bigWinChime) JuicyAudioHooks.Chime();
    }

    void OnBusted()
    {
        Busts++;
        Shake(0.6f);
    }

    void Shake(float amount)
    {
        if (cameraRig != null) cameraRig.Shake(amount);
    }

    void Update()
    {
        float target = rules != null ? rules.HeatNormalized : 0f;
        shownHeat = Mathf.MoveTowards(shownHeat, target, Time.deltaTime * 1.2f);
        for (int i = 0; i < heatLights.Length; i++)
        {
            if (heatLights[i] != null) heatLights[i].SetDrive(1f + shownHeat * 2.5f, 0.3f + shownHeat * 1.5f);
        }
    }
}
