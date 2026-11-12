using UnityEngine;

// The alley answers the game: the raccoon pushes on sorts and vat fills, the police
// strobe and vault alarms run hotter with heat, the camera shakes on a bust,
// and the existing sound hooks play.
public class AlleyReactions : MonoBehaviour
{
    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Animator raccoon;
    [SerializeField] LightAnimator[] heatLights;
    [SerializeField] Transform shakeTarget;
    [SerializeField] double bigWinChime = 150d;

    HeistSortRules rules;
    float shownHeat;
    float shake;
    Vector3 shakeHome;

    public int PushLights { get; private set; }
    public int PushHeavies { get; private set; }
    public int Legs { get; private set; }
    public int Busts { get; private set; }
    public float ShownHeat => shownHeat;

    public void Configure(AlleyChuteDirector chute, Animator mascot, LightAnimator[] lights, Transform shaken)
    {
        director = chute;
        raccoon = mascot;
        heatLights = lights;
        shakeTarget = shaken;
    }

    void Start()
    {
        rules = director.Rules;
        rules.Sorted += OnSorted;
        rules.VatFilled += OnVatFilled;
        rules.LegAdvanced += OnLegAdvanced;
        rules.CashWon += OnCashWon;
        rules.Busted += OnBusted;
        if (shakeTarget != null) shakeHome = shakeTarget.localPosition;
    }

    void OnDestroy()
    {
        if (rules == null) return;
        rules.Sorted -= OnSorted;
        rules.VatFilled -= OnVatFilled;
        rules.LegAdvanced -= OnLegAdvanced;
        rules.CashWon -= OnCashWon;
        rules.Busted -= OnBusted;
    }

    void OnSorted(SortResult result, int side)
    {
        if (result == SortResult.Wrong || result == SortResult.Bomb)
        {
            shake = Mathf.Max(shake, result == SortResult.Bomb ? 0.35f : 0.15f);
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
        shake = 0.6f;
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

    void LateUpdate()
    {
        if (shakeTarget == null) return;
        shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 1.4f);
        Vector3 offset = shake > 0f ? Random.insideUnitSphere * (shake * shake * 0.12f) : Vector3.zero;
        shakeTarget.localPosition = shakeHome + offset;
    }
}
