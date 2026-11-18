using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// How the raccoon finds a clue in the alley. A design pick, all three built
// and switchable under Juicy/Alley/Clues until Anthony picks one.
public enum ClueFind { WallRub, TorchReveal, UnderProp }

// One place in the alley a clue is found: the prop there, the points he walks
// through to reach it, where he stands, the painted mark, and (under the
// prop) how far the prop slides off it.
[System.Serializable]
public struct ClueSpot
{
    public Transform Prop;
    public Vector3[] Via;
    public Vector3 Stand;
    public AlleyClueMark Mark;
    public Vector3 Slide;
}

// The trip between legs. A finished leg pauses and clears the sort, waits out
// a vault crack, then the raccoon walks from the manhole to the next clue spot
// and finds the clue there: he rubs the grime off a wall, lights hidden UV
// paint with his torch, or shoves (or blasts) a prop off a painted symbol.
// The symbol stays in the alley, in view of the sort camera, until the hack
// clears the cycle. The leg's best tier sets how bright and how complete it
// is. He walks back and the sort resumes. The trip never touches the vault.
public class AlleyClueTrip : MonoBehaviour
{
    [SerializeField] AlleyChuteDirector director;
    [SerializeField] AlleyVaultDoor vaultDoor;
    [SerializeField] AlleyCameraRig cameraRig;
    [SerializeField] Animator raccoon;
    [SerializeField] StandOnSpot stand;
    [SerializeField] Transform torch;
    [Tooltip("Spots for the wall rub and the torch: marks on walls and prop faces.")]
    [SerializeField] ClueSpot[] wallSpots;
    [Tooltip("Spots under props: marks on the ground the prop slides off.")]
    [SerializeField] ClueSpot[] propSpots;
    [SerializeField] ParticleSystem burst;
    [SerializeField] Light flash;
    [SerializeField] Light uvLight;
    [SerializeField] ClueFind find = ClueFind.WallRub;
    [Tooltip("Metres per second. There is no walk clip yet, so he slides on the idle.")]
    [SerializeField] float walkSpeed = 1.8f;
    [SerializeField] float turnTime = 0.25f;
    [SerializeField] float viewFov = 50f;
    [SerializeField] float viewDistance = 3.8f;

    const float FacingCamera = 180f;
    const int RubStrokes = 3;

    readonly List<string> log = new List<string>();
    readonly Dictionary<Transform, Vector3> propRest = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Quaternion> propRestRotation = new Dictionary<Transform, Quaternion>();
    int shownCycle = -1;
    int shownCount = -1;
    ClueFind shownFind;

    public bool Running { get; private set; }
    public int Trips { get; private set; }
    public int Blasts { get; private set; }
    public int Shoves { get; private set; }
    public int Rubs { get; private set; }
    public int Torches { get; private set; }
    public int Skipped { get; private set; }
    public IReadOnlyList<string> Log => log;
    // Set while the found clue is held on camera, for captures of the reveal.
    public bool Revealing { get; private set; }
    public int RevealTier { get; private set; } = -1;
    public ClueSpot[] Spots => find == ClueFind.UnderProp ? propSpots : wallSpots;
    public ClueFind Find
    {
        get => find;
        set
        {
            find = value;
            ShowFound();
        }
    }

    public void Configure(AlleyChuteDirector chute, AlleyVaultDoor door, AlleyCameraRig rig, Animator body, StandOnSpot feet, Transform torchTip,
        ClueSpot[] walls, ClueSpot[] props, ParticleSystem blast, Light blastFlash, Light uv)
    {
        director = chute;
        vaultDoor = door;
        cameraRig = rig;
        raccoon = body;
        stand = feet;
        torch = torchTip;
        wallSpots = walls;
        propSpots = props;
        burst = blast;
        flash = blastFlash;
        uvLight = uv;
    }

    void Awake()
    {
        foreach (ClueSpot[] list in new[] { wallSpots, propSpots })
        {
            foreach (ClueSpot spot in list)
            {
                if (spot.Prop == null || propRest.ContainsKey(spot.Prop)) continue;
                propRest[spot.Prop] = spot.Prop.position;
                propRestRotation[spot.Prop] = spot.Prop.rotation;
            }
        }
        if (flash != null) flash.intensity = 0f;
        if (uvLight != null) uvLight.intensity = 0f;
    }

    void Start()
    {
        director.Rules.LegAdvanced += OnLegAdvanced;
        ShowFound();
    }

    void OnDestroy()
    {
        if (director != null && director.Rules != null) director.Rules.LegAdvanced -= OnLegAdvanced;
    }

    // A hack clears the cycle, and a save restores one, outside the trip, so
    // the alley follows the cycle whenever it changes.
    void Update()
    {
        if (Running || director == null || director.Clues == null) return;
        ClueRules clues = director.Clues;
        if (clues.Cycle != shownCycle || clues.Count != shownCount || find != shownFind) ShowFound();
    }

    void OnLegAdvanced(int leg, int chutes)
    {
        Begin(director.Rules.LastLegBestTier);
    }

    // Runs one trip for a leg with this best tier. Also how captures start one.
    public void Begin(int tier)
    {
        if (Running) return;
        if (director.Clues.HackDue)
        {
            // The cycle is full and the hack has not run yet.
            Skipped++;
            Note("no trip: cycle full " + director.Clues.Count + "/" + director.Clues.CluesNeeded + ", hack due");
            return;
        }
        StartCoroutine(Trip(tier));
    }

    // Which spot the clue at this place in the cycle is found at. Each cycle
    // starts one spot further on, so the alley does not repeat itself.
    int SpotIndex(int cycle, int index, int count) => count == 0 ? 0 : (cycle + index) % count;

    // Puts the alley in step with the cycle: the clues found so far stay where
    // they were found, every other spot is unfound, and props sit back at rest
    // unless a clue under them was found.
    public void ShowFound()
    {
        if (director == null || director.Clues == null) return;
        ClueRules clues = director.Clues;
        foreach (var pair in propRest) pair.Key.SetPositionAndRotation(pair.Value, propRestRotation[pair.Key]);
        ClueSpot[] active = Spots;
        foreach (ClueSpot[] list in new[] { wallSpots, propSpots })
        {
            if (list == active) continue;
            foreach (ClueSpot spot in list)
            {
                if (spot.Mark == null) continue;
                spot.Mark.Hide(ClueFind.TorchReveal);
            }
        }
        var found = new int[active.Length];
        for (int i = 0; i < found.Length; i++) found[i] = -1;
        for (int k = 0; k < clues.Count; k++) found[SpotIndex(clues.Cycle, k, active.Length)] = k;
        for (int i = 0; i < active.Length; i++)
        {
            ClueSpot spot = active[i];
            if (spot.Mark == null) continue;
            if (found[i] < 0)
            {
                spot.Mark.Hide(find);
                continue;
            }
            Clue clue = clues.Clues[found[i]];
            spot.Mark.ShowFound(clue.Symbol, clue.Tier, find);
            if (find == ClueFind.UnderProp && spot.Prop != null) spot.Prop.position = propRest[spot.Prop] + spot.Slide;
        }
        shownCycle = clues.Cycle;
        shownCount = clues.Count;
        shownFind = find;
    }

    IEnumerator Trip(int tier)
    {
        Running = true;
        float started = Time.time;
        director.Paused = true;
        // Let a vault crack play out before the camera leaves the door.
        while (vaultDoor != null && vaultDoor.Cracking) yield return null;

        ClueRules clues = director.Clues;
        ClueSpot[] spots = Spots;
        int index = clues.Count;
        int spotIndex = SpotIndex(clues.Cycle, index, spots.Length);
        Clue? found = clues.ClueFound(tier);
        if (found == null || spots.Length == 0)
        {
            Skipped++;
            director.Paused = false;
            Running = false;
            yield break;
        }
        Clue clue = found.Value;
        director.SaveNow();
        ClueSpot spot = spots[spotIndex];
        AlleyClueMark mark = spot.Mark;
        ClueFind how = find;

        // Let a push from the leg's last vat fill finish before he sets off.
        for (float t = 0f; t < 2f && !Idle(); t += Time.deltaTime) yield return null;

        cameraRig.Hold(ViewPoint(spot), ViewTarget(spot), viewFov);
        Vector3[] via = spot.Via ?? new Vector3[0];
        foreach (Vector3 point in via) yield return Walk(point);
        yield return Walk(spot.Stand);
        yield return Turn(Yaw(spot.Stand, mark.Center));

        string action;
        bool blast = how != ClueFind.TorchReveal && director.Rules.GearCount(GearKind.Dynamite) > 0;
        if (blast)
        {
            director.Rules.AddGear(GearKind.Dynamite, -1);
            Blasts++;
            action = "dynamite";
        }
        else if (how == ClueFind.WallRub) { Rubs++; action = "rub"; }
        else if (how == ClueFind.TorchReveal) { Torches++; action = "torch"; }
        else { Shoves++; action = "shove"; }

        switch (how)
        {
            case ClueFind.WallRub: yield return Rub(spot, clue, blast); break;
            case ClueFind.TorchReveal: yield return TorchUp(spot, clue); break;
            default: yield return Uncover(spot, clue, blast); break;
        }

        // He steps aside toward the middle of the alley so the camera sees
        // the clue, then the camera holds on it for the seconds its tier gives.
        yield return Walk(Aside(spot));
        yield return Turn(Yaw(stand.Spot, mark.Center));
        Revealing = true;
        RevealTier = clue.Tier;
        yield return new WaitForSeconds(clue.SecondsShown);
        Revealing = false;
        cameraRig.Release();

        for (int i = via.Length - 1; i >= 0; i--) yield return Walk(via[i]);
        yield return Walk(stand.Home);
        yield return Turn(FacingCamera);
        stand.SetSpot(stand.Home, false);
        while (!cameraRig.Settled) yield return null;

        Trips++;
        Note("clue trip " + Trips + ": " + how + " at " + Name(spot) + ", " + action + ", symbol " + clue.Symbol + " " + AlleyHack.Glyphs[clue.Symbol] +
            " tier " + clue.Tier + " (worn " + clue.Covered.ToString("0.00") + ", paint " + AlleyClueMark.Strength(clue.Tier, how).ToString("0.00") + ")" +
            ", clues " + clues.Count + "/" + clues.CluesNeeded + ", took " + (Time.time - started).ToString("0.0") + "s");
        ShowFound();
        director.Paused = false;
        Running = false;
    }

    Vector3 Aside(ClueSpot spot)
    {
        Vector3 toMiddle = new Vector3(stand.Home.x - spot.Stand.x, 0f, 0f);
        float side = Mathf.Abs(toMiddle.x) > 0.05f ? Mathf.Sign(toMiddle.x) : -1f;
        return spot.Stand + new Vector3(side * 0.6f, 0f, -0.15f);
    }

    static string Name(ClueSpot spot) => spot.Mark != null ? spot.Mark.name : "spot";

    void Note(string line) => log.Add(Time.time.ToString("0") + "s " + line);

    bool Idle() => raccoon == null || (raccoon.GetCurrentAnimatorStateInfo(0).IsName("Torch_Idle") && !raccoon.IsInTransition(0));

    // Option 1: he scrubs the patch in strokes and each one wipes more mud off
    // the stencil, down to what the tier leaves. Dynamite blows it off at once.
    IEnumerator Rub(ClueSpot spot, Clue clue, bool blast)
    {
        AlleyClueMark mark = spot.Mark;
        mark.Prepare(clue.Symbol, ClueFind.WallRub);
        float strength = AlleyClueMark.Strength(clue.Tier, ClueFind.WallRub);
        mark.Paint(strength);
        float end = AlleyClueMark.GrimeLeft(clue.Covered);
        if (blast)
        {
            raccoon.SetTrigger("onPushHeavy");
            yield return new WaitForSeconds(0.45f);
            Blast(mark.Center);
            mark.SetGrime(end);
            yield return new WaitForSeconds(0.3f);
            yield break;
        }
        float cutoff = 0f;
        for (int stroke = 0; stroke < RubStrokes; stroke++)
        {
            raccoon.ResetTrigger("onPushLight");
            raccoon.SetTrigger("onPushLight");
            float from = cutoff;
            float to = Mathf.Lerp(0f, end, (stroke + 1f) / RubStrokes);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                cutoff = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.5f));
                mark.SetGrime(cutoff);
                yield return null;
            }
            cutoff = to;
            mark.SetGrime(cutoff);
            yield return new WaitForSeconds(0.15f);
        }
    }

    // Option 2: he holds the torch up to the wall, its flame turns UV and the
    // hidden paint blazes, then settles to the faint glow it keeps.
    IEnumerator TorchUp(ClueSpot spot, Clue clue)
    {
        AlleyClueMark mark = spot.Mark;
        mark.Prepare(clue.Symbol, ClueFind.TorchReveal);
        mark.SetGrime(AlleyClueMark.GrimeLeft(clue.Covered));
        raccoon.ResetTrigger("onPushLight");
        raccoon.SetTrigger("onPushLight");
        float rest = AlleyClueMark.Strength(clue.Tier, ClueFind.TorchReveal);
        float restGlow = AlleyClueMark.RestGlow(clue.Tier);
        const float rise = 0.9f;
        for (float t = 0f; t < rise; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / rise);
            PlaceUv(mark, 1.6f * k);
            mark.Paint(rest * 1.8f * k);
            mark.SetGlow(restGlow * 6f * k);
            yield return null;
        }
        const float settle = 0.8f;
        for (float t = 0f; t < settle; t += Time.deltaTime)
        {
            float k = t / settle;
            PlaceUv(mark, 1.6f * (1f - k));
            mark.Paint(Mathf.Lerp(rest * 1.8f, rest, k));
            mark.SetGlow(Mathf.Lerp(restGlow * 6f, restGlow, k));
            yield return null;
        }
        PlaceUv(mark, 0f);
        mark.Paint(rest);
        mark.SetGlow(restGlow);
    }

    void PlaceUv(AlleyClueMark mark, float intensity)
    {
        if (uvLight == null) return;
        Vector3 from = torch != null ? torch.position : stand.transform.position + Vector3.up;
        uvLight.transform.position = Vector3.Lerp(from, mark.Center, 0.35f);
        uvLight.intensity = intensity;
    }

    // Option 3: he shoves the prop off the painted mark, or blasts it off,
    // and it stays moved until the hack clears the cycle.
    IEnumerator Uncover(ClueSpot spot, Clue clue, bool blast)
    {
        AlleyClueMark mark = spot.Mark;
        raccoon.SetTrigger("onPushHeavy");
        yield return new WaitForSeconds(blast ? 0.45f : 0.35f);
        if (blast) Blast(mark.Center);
        mark.Prepare(clue.Symbol, ClueFind.UnderProp);
        mark.SetGrime(AlleyClueMark.GrimeLeft(clue.Covered));
        mark.Paint(AlleyClueMark.Strength(clue.Tier, ClueFind.UnderProp));
        Transform prop = spot.Prop;
        if (prop == null) yield break;
        Vector3 rest = propRest[prop];
        Quaternion restRotation = propRestRotation[prop];
        Vector3 axis = Vector3.Cross(Vector3.up, spot.Slide.sqrMagnitude > 1e-4f ? spot.Slide.normalized : Vector3.forward);
        float time = blast ? 0.45f : 0.8f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = blast ? 1f - (1f - t / time) * (1f - t / time) : Mathf.SmoothStep(0f, 1f, t / time);
            float hop = blast ? 0.25f * Mathf.Sin(k * Mathf.PI) : 0f;
            prop.position = rest + spot.Slide * k + Vector3.up * hop;
            prop.rotation = Quaternion.AngleAxis((blast ? 12f : 4f) * Mathf.Sin(k * Mathf.PI), axis) * restRotation;
            yield return null;
        }
        prop.SetPositionAndRotation(rest + spot.Slide, restRotation);
    }

    Vector3 ViewTarget(ClueSpot spot) => Vector3.Lerp(spot.Stand + Vector3.up * 0.6f, spot.Mark.Center, 0.55f);

    // Back toward the home camera from the spot and a little above it, so the
    // raccoon walking in and the clue both stay in frame.
    Vector3 ViewPoint(ClueSpot spot)
    {
        Vector3 target = ViewTarget(spot);
        Vector3 home = Camera.main != null ? Camera.main.transform.position : new Vector3(0f, 2.3f, -5f);
        Vector3 back = home - target;
        back.y = 0f;
        back = back.sqrMagnitude > 0.001f ? back.normalized : Vector3.back;
        return target + back * viewDistance + Vector3.up * 1.8f;
    }

    static float Yaw(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        d.y = 0f;
        return d.sqrMagnitude < 1e-4f ? FacingCamera : Quaternion.LookRotation(d).eulerAngles.y;
    }

    IEnumerator Walk(Vector3 to)
    {
        Vector3 from = stand.Spot;
        to.y = from.y;
        yield return Turn(Yaw(from, to));
        raccoon.SetBool(AlleyReactions.WalkParameter, true);
        float distance = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(to.x, 0f, to.z));
        float duration = Mathf.Max(0.2f, distance / walkSpeed);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            stand.SetSpot(Vector3.Lerp(from, to, k), true);
            yield return null;
        }
        stand.SetSpot(to, true);
        raccoon.SetBool(AlleyReactions.WalkParameter, false);
    }

    IEnumerator Turn(float yaw)
    {
        Transform body = stand.transform;
        Quaternion from = body.rotation;
        Quaternion to = Quaternion.Euler(0f, yaw, 0f);
        for (float t = 0f; t < turnTime; t += Time.deltaTime)
        {
            body.rotation = Quaternion.Slerp(from, to, t / turnTime);
            yield return null;
        }
        body.rotation = to;
    }

    void Blast(Vector3 at)
    {
        if (burst != null)
        {
            burst.transform.position = new Vector3(at.x, Mathf.Max(0.3f, at.y), at.z);
            burst.Clear(true);
            burst.Play(true);
        }
        if (flash != null) StartCoroutine(Flash(at));
        cameraRig.Shake(0.75f);
        JuicyAudioHooks.Vault();
    }

    IEnumerator Flash(Vector3 at)
    {
        flash.transform.position = at + Vector3.up * 0.4f;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            flash.intensity = 9f * (1f - t / 0.5f);
            yield return null;
        }
        flash.intensity = 0f;
    }
}
