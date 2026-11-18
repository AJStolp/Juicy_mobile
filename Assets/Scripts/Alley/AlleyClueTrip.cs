using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Where clues are kept for the player to see. A design pick, both built and
// switchable under Juicy/Alley/Clues until Anthony picks one.
public enum ClueKeeper { Notebook, WordSign }

// One place in the alley the raccoon visits for a clue: the prop he moves or
// blasts, where he stands to do it, and where the clue rises.
[System.Serializable]
public struct ClueSpot
{
    public Transform Prop;
    public Vector3 Stand;
    public Vector3 Rise;
}

// The trip between legs. A finished leg pauses and clears the sort (as Plinko
// did), waits out a vault crack, then the raccoon walks from the manhole to
// the next clue spot, shoves the prop aside or spends a stick of dynamite on
// it, and a clue symbol rises at the clarity ClueRules gives the leg's best
// tier. The clue flies to where clues are kept, he walks back, and the sort
// resumes. The trip never touches the vault.
public class AlleyClueTrip : MonoBehaviour
{
    [SerializeField] AlleyChuteDirector director;
    [SerializeField] AlleyVaultDoor vaultDoor;
    [SerializeField] AlleyCameraRig cameraRig;
    [SerializeField] Animator raccoon;
    [SerializeField] StandOnSpot stand;
    [SerializeField] ClueSpot[] spots;
    [SerializeField] ParticleSystem burst;
    [SerializeField] Light flash;
    [SerializeField] Material symbolMaterial;
    [SerializeField] AlleyClueNotebook notebook;
    [SerializeField] AlleyWordSign sign;
    [SerializeField] ClueKeeper keeper = ClueKeeper.Notebook;
    [Tooltip("Metres per second. There is no walk clip yet, so he slides on the idle.")]
    [SerializeField] float walkSpeed = 1.3f;
    [SerializeField] float turnTime = 0.25f;
    [SerializeField] float revealSize = 0.9f;
    [SerializeField] float flightTime = 0.9f;
    [SerializeField] float viewFov = 50f;
    [SerializeField] float viewDistance = 4.2f;

    const float FacingCamera = 180f;

    readonly List<string> log = new List<string>();
    Vector3[] propRest;
    Quaternion[] propRestRotation;
    Transform symbol;
    Renderer symbolRenderer;
    MaterialPropertyBlock block;

    public bool Running { get; private set; }
    public int Trips { get; private set; }
    public int Blasts { get; private set; }
    public int Shoves { get; private set; }
    public int Skipped { get; private set; }
    public IReadOnlyList<string> Log => log;
    // Set while a clue is up, for captures of the reveal.
    public bool Revealing { get; private set; }
    public int RevealTier { get; private set; } = -1;
    public ClueKeeper Keeper
    {
        get => keeper;
        set
        {
            keeper = value;
            ShowKept();
        }
    }

    public void Configure(AlleyChuteDirector chute, AlleyVaultDoor door, AlleyCameraRig rig, Animator body, StandOnSpot feet,
        ClueSpot[] places, ParticleSystem blast, Light blastFlash, Material symbolLook, AlleyClueNotebook book, AlleyWordSign wordSign)
    {
        director = chute;
        vaultDoor = door;
        cameraRig = rig;
        raccoon = body;
        stand = feet;
        spots = places;
        burst = blast;
        flash = blastFlash;
        symbolMaterial = symbolLook;
        notebook = book;
        sign = wordSign;
    }

    void Awake()
    {
        block = new MaterialPropertyBlock();
        propRest = new Vector3[spots.Length];
        propRestRotation = new Quaternion[spots.Length];
        for (int i = 0; i < spots.Length; i++)
        {
            if (spots[i].Prop == null) continue;
            propRest[i] = spots[i].Prop.position;
            propRestRotation[i] = spots[i].Prop.rotation;
        }
        if (flash != null) flash.intensity = 0f;
    }

    void Start()
    {
        director.Rules.LegAdvanced += OnLegAdvanced;
        ShowKept();
    }

    void OnDestroy()
    {
        if (director != null && director.Rules != null) director.Rules.LegAdvanced -= OnLegAdvanced;
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
            // The cycle is full and the hack at the keypad (the next ticket)
            // has not run, so there is no clue to find this leg.
            Skipped++;
            Note("no trip: cycle full " + director.Clues.Count + "/" + director.Clues.CluesNeeded + ", hack due");
            return;
        }
        StartCoroutine(Trip(tier));
    }

    int CoverSeed => director.Clues.Seed * 31 + director.Clues.Cycle * 7;

    // Shows the clues kept so far on the chosen keeper and hides the other.
    public void ShowKept()
    {
        if (director == null || director.Clues == null) return;
        bool book = keeper == ClueKeeper.Notebook;
        if (notebook != null)
        {
            notebook.SetVisible(book);
            if (book) notebook.Show(director.Clues.Clues, CoverSeed);
        }
        if (sign != null)
        {
            if (book) sign.HideClues();
            else sign.ShowClues(director.Clues.Clues, CoverSeed);
        }
    }

    IEnumerator Trip(int tier)
    {
        Running = true;
        float started = Time.time;
        director.Paused = true;
        // Let a vault crack play out before the camera leaves the door.
        while (vaultDoor != null && vaultDoor.Cracking) yield return null;

        ClueRules clues = director.Clues;
        int index = clues.Count;
        int spotIndex = (clues.Cycle * ClueRules.CluesPerHackLater + index) % spots.Length;
        Clue? found = clues.ClueFound(tier);
        if (found == null)
        {
            Skipped++;
            director.Paused = false;
            Running = false;
            yield break;
        }
        Clue clue = found.Value;
        director.SaveNow();
        ClueSpot spot = spots[spotIndex];

        // Let a push from the leg's last vat fill finish before he sets off.
        for (float t = 0f; t < 2f && !Idle(); t += Time.deltaTime) yield return null;

        cameraRig.Hold(ViewPoint(spot), ViewTarget(spot), viewFov);
        yield return Walk(spot.Stand);
        yield return Turn(Yaw(spot.Stand, spot.Rise));

        bool blast = director.Rules.GearCount(GearKind.Dynamite) > 0;
        raccoon.SetTrigger("onPushHeavy");
        yield return new WaitForSeconds(blast ? 0.45f : 0.35f);
        if (blast)
        {
            director.Rules.AddGear(GearKind.Dynamite, -1);
            Blasts++;
            Blast(spot);
        }
        else Shoves++;
        StartCoroutine(Jolt(spotIndex, spot, blast));
        yield return new WaitForSeconds(0.15f);

        yield return Reveal(spot, clue, CoverSeed + index);
        cameraRig.Release();
        yield return Fly(index);

        yield return Walk(stand.Home);
        yield return Turn(FacingCamera);
        stand.SetSpot(stand.Home, false);
        while (!cameraRig.Settled) yield return null;

        Trips++;
        Note("clue trip " + Trips + ": " + Name(spot) + ", " + (blast ? "dynamite" : "shove") + ", symbol " + clue.Symbol + " " + ClueSymbols.Name(clue.Symbol) +
            " tier " + clue.Tier + " (covered " + clue.Covered.ToString("0.00") + ", shown " + clue.SecondsShown.ToString("0.00") + "s), kept on " + keeper +
            ", clues " + clues.Count + "/" + clues.CluesNeeded + ", took " + (Time.time - started).ToString("0.0") + "s");
        director.Paused = false;
        Running = false;
    }

    static string Name(ClueSpot spot) => spot.Prop != null ? spot.Prop.name : "spot";

    void Note(string line) => log.Add(Time.time.ToString("0") + "s " + line);

    bool Idle() => raccoon == null || (raccoon.GetCurrentAnimatorStateInfo(0).IsName("Torch_Idle") && !raccoon.IsInTransition(0));

    Vector3 ViewTarget(ClueSpot spot) => Vector3.Lerp(spot.Stand + Vector3.up * 0.6f, spot.Rise, 0.5f);

    // Back toward the home camera from the spot and a little above it, so the
    // raccoon walking in and the clue rising both stay in frame.
    Vector3 ViewPoint(ClueSpot spot)
    {
        Vector3 target = ViewTarget(spot);
        Vector3 home = Camera.main != null ? Camera.main.transform.position : new Vector3(0f, 2.3f, -5f);
        Vector3 back = home - target;
        back.y = 0f;
        back = back.sqrMagnitude > 0.001f ? back.normalized : Vector3.back;
        return target + back * viewDistance + Vector3.up * 0.9f;
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

    // The prop gives: a shove slides and tips it away from him, a blast
    // throws it harder. It settles back while he walks home.
    IEnumerator Jolt(int index, ClueSpot spot, bool blast)
    {
        Transform prop = spot.Prop;
        if (prop == null) yield break;
        Vector3 away = spot.Rise - spot.Stand;
        away.y = 0f;
        away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
        float push = blast ? 0.32f : 0.16f;
        Vector3 axis = Vector3.Cross(Vector3.up, away);
        Vector3 rest = propRest[index];
        Quaternion restRotation = propRestRotation[index];
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float k = Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI * 0.5f);
            prop.position = rest + away * (push * k) + Vector3.up * (blast ? 0.12f * Mathf.Sin(k * Mathf.PI) : 0f);
            prop.rotation = Quaternion.AngleAxis((blast ? 14f : 7f) * Mathf.Sin(k * Mathf.PI), axis) * restRotation;
            yield return null;
        }
        while (Running && Vector3.Distance(stand.Spot, spot.Stand) < 0.05f) yield return null;
        Vector3 moved = prop.position;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            prop.position = Vector3.Lerp(moved, rest, Mathf.SmoothStep(0f, 1f, t / 1.2f));
            yield return null;
        }
        prop.SetPositionAndRotation(rest, restRotation);
    }

    void Blast(ClueSpot spot)
    {
        Vector3 at = Vector3.Lerp(spot.Stand, spot.Rise, 0.7f);
        if (burst != null)
        {
            burst.transform.position = new Vector3(at.x, Mathf.Max(0.3f, Mathf.Min(at.y, spot.Rise.y)), at.z);
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

    void EnsureSymbol()
    {
        if (symbol != null) return;
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Clue_Symbol";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        symbolRenderer = quad.GetComponent<Renderer>();
        symbolRenderer.sharedMaterial = symbolMaterial;
        symbolRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        symbolRenderer.receiveShadows = false;
        symbol = quad.transform;
        symbol.gameObject.SetActive(false);
    }

    void FaceCamera(Transform t)
    {
        Camera view = Camera.main;
        if (view != null) t.rotation = Quaternion.LookRotation(t.position - view.transform.position, Vector3.up);
    }

    // The clue rises out of the spot and holds for the seconds its tier gives.
    IEnumerator Reveal(ClueSpot spot, Clue clue, int seed)
    {
        EnsureSymbol();
        symbolRenderer.GetPropertyBlock(block);
        block.SetTexture("_BaseMap", ClueSymbols.Texture(clue.Symbol, clue.Covered, seed));
        block.SetColor("_BaseColor", Color.white * 1.4f);
        symbolRenderer.SetPropertyBlock(block);
        symbol.gameObject.SetActive(true);
        Vector3 low = spot.Rise - Vector3.up * 0.45f;
        const float rise = 0.4f;
        for (float t = 0f; t < rise; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / rise);
            symbol.position = Vector3.Lerp(low, spot.Rise, k);
            symbol.localScale = Vector3.one * (revealSize * (0.2f + 0.8f * k) * (1f + 0.15f * Mathf.Sin(k * Mathf.PI)));
            FaceCamera(symbol);
            yield return null;
        }
        Revealing = true;
        RevealTier = clue.Tier;
        for (float t = 0f; t < clue.SecondsShown; t += Time.deltaTime)
        {
            symbol.position = spot.Rise + Vector3.up * (0.03f * Mathf.Sin(t * 4f));
            symbol.localScale = Vector3.one * revealSize;
            FaceCamera(symbol);
            yield return null;
        }
        Revealing = false;
    }

    IEnumerator Fly(int index)
    {
        if (keeper == ClueKeeper.WordSign && sign != null) yield return FlyToSign(index);
        else if (notebook != null) yield return FlyToNotebook(index);
        symbol.gameObject.SetActive(false);
        ShowKept();
        if (keeper == ClueKeeper.WordSign && sign != null) sign.Land(index);
        else if (notebook != null) notebook.Land(index);
    }

    // Option B: the symbol flies in the world onto its tile over the vault.
    IEnumerator FlyToSign(int index)
    {
        Transform anchor = sign.ClueAnchor(index);
        Vector3 from = symbol.position;
        Vector3 startScale = symbol.localScale;
        Quaternion startRotation = symbol.rotation;
        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / flightTime);
            Vector3 arc = Vector3.up * (0.8f * Mathf.Sin(k * Mathf.PI));
            symbol.position = Vector3.Lerp(from, anchor.position, k) + arc;
            symbol.localScale = Vector3.Lerp(startScale, anchor.lossyScale, k);
            symbol.rotation = Quaternion.Slerp(startRotation, anchor.rotation, k);
            yield return null;
        }
    }

    // Option A: the symbol leaves the world for the screen and lands on its
    // notebook slot.
    IEnumerator FlyToNotebook(int index)
    {
        Camera view = Camera.main;
        Canvas canvas = notebook.Canvas;
        var canvasRect = (RectTransform)canvas.transform;
        RectTransform slot = notebook.Slot(index);
        Vector3 screen = view.WorldToScreenPoint(symbol.position);
        Vector3 edge = view.WorldToScreenPoint(symbol.position + view.transform.right * (symbol.localScale.x * 0.5f));
        float pixels = Mathf.Abs(edge.x - screen.x) * 2f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, canvas.worldCamera, out Vector2 start);
        float startSize = pixels / Mathf.Max(0.01f, canvas.scaleFactor);

        var flyer = new GameObject("Clue_Flying", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        var rect = (RectTransform)flyer.transform;
        rect.SetParent(canvasRect, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        var image = flyer.GetComponent<UnityEngine.UI.RawImage>();
        image.raycastTarget = false;
        symbolRenderer.GetPropertyBlock(block);
        image.texture = block.GetTexture("_BaseMap");
        symbol.gameObject.SetActive(false);

        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / flightTime);
            Vector2 end = canvasRect.InverseTransformPoint(slot.position);
            float endSize = slot.rect.width;
            rect.anchoredPosition = Vector2.Lerp(start, end, k);
            float size = Mathf.Lerp(startSize, endSize, k);
            rect.sizeDelta = new Vector2(size, size);
            yield return null;
        }
        Destroy(flyer);
    }
}
