using UnityEngine;

// The alley answers the game: the raccoon pushes on sorts, vat fills and vault
// stages, the police strobe and vault alarms run hotter with heat, the camera
// shakes on mistakes and blasts, and every sort, miss, flick and blast has its
// sound (JuicyAudioHooks).
public class AlleyReactions : MonoBehaviour
{
    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Animator raccoon;
    [SerializeField] LightAnimator[] heatLights;
    [SerializeField] AlleyCameraRig cameraRig;

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
        rules.Missed += OnMissed;
        rules.Trashed += OnTrashed;
        rules.StreakChanged += OnStreak;
        rules.VatFilled += OnVatFilled;
        rules.LegAdvanced += OnLegAdvanced;
        rules.Busted += OnBusted;
        director.AttemptStarted += OnAttemptStarted;
        vault = director.Vault;
        vault.StageLocked += OnStageLocked;
        vault.Cracked += OnCracked;
    }

    void OnDestroy()
    {
        if (rules == null) return;
        director.AttemptStarted -= OnAttemptStarted;
        rules.Sorted -= OnSorted;
        rules.Missed -= OnMissed;
        rules.Trashed -= OnTrashed;
        rules.StreakChanged -= OnStreak;
        rules.VatFilled -= OnVatFilled;
        rules.LegAdvanced -= OnLegAdvanced;
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
            if (result == SortResult.Bomb) JuicyAudioHooks.Boom();
            else JuicyAudioHooks.Wrong();
            return;
        }
        if (result == SortResult.Correct) JuicyAudioHooks.Sort(rules.Streak);
        else if (result == SortResult.Gold) JuicyAudioHooks.Cash();
        if (result != SortResult.Correct && result != SortResult.Gold) return;
        // Only start a light push from idle, so pushes do not stack up.
        if (raccoon == null || !raccoon.GetCurrentAnimatorStateInfo(0).IsName("Torch_Idle") || raccoon.IsInTransition(0)) return;
        raccoon.SetTrigger("onPushLight");
        PushLights++;
    }

    // Dynamite left to land goes off in the street; a gem just drops.
    void OnMissed(ItemKind kind)
    {
        if (kind == ItemKind.Bomb)
        {
            Shake(0.35f);
            JuicyAudioHooks.Boom();
        }
        else if (kind == ItemKind.Gem) JuicyAudioHooks.Miss();
    }

    // Flicked up: dynamite is pocketed, cash is caught, a gem is thrown away.
    void OnTrashed(ItemKind kind)
    {
        if (kind == ItemKind.Bomb) JuicyAudioHooks.Flick();
        else if (kind == ItemKind.Gold) JuicyAudioHooks.Cash();
        else if (kind == ItemKind.Gem) JuicyAudioHooks.Miss();
    }

    int lastTier;

    // A streak reaching its next cash tier gets its own little climb.
    void OnStreak(int streak, int tier)
    {
        if (tier > lastTier) JuicyAudioHooks.StreakUp();
        lastTier = tier;
    }

    void OnVatFilled(int side) => PushHeavy();

    // Levels have their own driving loop; the vault a tenser one.
    void OnAttemptStarted(LevelRules level) => JuicyMusic.Play(director.IsBoss ? JuicyMusic.Track.Vault : JuicyMusic.Track.Level);

    // A stage locking on the vault is a big moment; so is cracking it. He
    // has no celebration clip yet, so both get the heavy push.
    void OnStageLocked(int stage) => PushHeavy();

    void OnCracked()
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
