using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The city's map: its six levels in order and the vault after them. Play
// starts here, a won level comes back here with the next one open, and a tap
// on an open stop plays it. Drawn in front of the camera (CameraOverlay) over
// the alley.
//
// Prototype for T-035: two looks, switched by the bar along the bottom.
//   Board:   the crime board. Stops are photos pinned to cork and joined by
//            red string; a beaten stop's photo shows the clue found there.
//   Streets: a night street map with the stops as gem shaped pins on a route.
public class AlleyMap : MonoBehaviour
{
    public enum Look { Board, Streets }

    // Level 1 at the bottom, climbing to the vault at the top.
    static readonly Vector2[] Stops =
    {
        new Vector2(0.30f, 0.17f), new Vector2(0.70f, 0.27f), new Vector2(0.30f, 0.37f),
        new Vector2(0.70f, 0.47f), new Vector2(0.30f, 0.57f), new Vector2(0.70f, 0.67f),
        new Vector2(0.50f, 0.815f),
    };

    static readonly Color Ink = new Color(0.08f, 0.07f, 0.06f);
    static readonly Color Paper = new Color(0.97f, 0.95f, 0.9f);
    static readonly Color Neon = new Color(1f, 0.85f, 0.25f);
    static readonly Color Done = new Color(0.35f, 0.95f, 0.5f);
    static readonly Color Locked = new Color(0.32f, 0.34f, 0.4f);

    const float Distance = 0.35f;
    const float TapGuard = 0.3f;

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Material panelMaterial;
    [SerializeField] Look look = Look.Board;
    [SerializeField] AlleySettings settings;

    CameraOverlay overlay;
    // The out of lives panel draws in the popup band, above every map label.
    CameraOverlay popup;
    GameObject content;
    Renderer[] stopHits;
    Renderer vaultHit;
    Renderer switchHit;
    Renderer settingsHit;
    // Lives and cash along the top, and the panel shown when a level is
    // tapped with no lives left.
    TextMeshPro livesText;
    TextMeshPro cashText;
    GameObject outOfLives;
    TextMeshPro nextLifeText;
    Renderer refillHit;
    TextMeshPro refillText;
    Renderer closeLivesHit;
    float livesShownAt;
    Transform pulse;
    Vector3 pulseBase;
    float shownAt;

    public bool Showing => content != null && content.activeSelf;

    public void Configure(AlleyChuteDirector chute, Material material, AlleySettings settingsPanel)
    {
        director = chute;
        panelMaterial = material;
        settings = settingsPanel;
    }

    void Start()
    {
        // Auto play (the bot) goes straight to levels; the director decides.
        if (!director.UsesMap)
        {
            gameObject.SetActive(false);
            return;
        }
        Camera view = Camera.main;
        overlay = new CameraOverlay(view, panelMaterial, Distance, CameraOverlay.LayerMap);
        popup = new CameraOverlay(view, panelMaterial, 0.31f, CameraOverlay.LayerSettings);
        transform.SetParent(view.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        director.MapOpened += Show;
        director.AttemptStarted += OnAttemptStarted;
        // With a title, the title opens the map; otherwise the map is first.
        if (!director.UsesTitle) director.OpenMap();
    }

    void OnDestroy()
    {
        if (director == null) return;
        director.MapOpened -= Show;
        director.AttemptStarted -= OnAttemptStarted;
    }

    void OnAttemptStarted(LevelRules level) => Hide();

    public void Show()
    {
        JuicyMusic.Play(JuicyMusic.Track.Map);
        Build();
        shownAt = Time.unscaledTime;
    }

    void Hide()
    {
        if (content != null) content.SetActive(false);
        if (outOfLives != null) outOfLives.SetActive(false);
    }

    public void SetLook(Look next)
    {
        look = next;
        if (Showing) Build();
    }

    // Plays the vault once every level is beaten (again after it is cracked).
    public bool ChooseVault()
    {
        if (director.Progress.VaultOpen && director.LivesLeft <= 0)
        {
            ShowOutOfLives();
            return false;
        }
        if (!director.PlayBoss()) return false;
        JuicyAudioHooks.Tap();
        return true;
    }

    // Plays an open stop. False for a locked one.
    public bool Choose(int index)
    {
        if (index < 0 || index >= director.Place.Count || !director.Progress.IsOpen(index)) return false;
        if (!director.Play(index))
        {
            ShowOutOfLives();
            return false;
        }
        JuicyAudioHooks.Tap();
        return true;
    }

    void Update()
    {
        if (!Showing) return;
        if (pulse != null) pulse.localScale = pulseBase * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f));
        // Settings sit on top and take the taps while open, including the one
        // that closes them, whichever script reads it first.
        if (Time.unscaledTime < shownAt + TapGuard) return;
        if (settings != null && (settings.Showing || Time.unscaledTime < settings.ClosedAt + TapGuard)) return;
        TickTopBar();
        if (OutOfLivesShowing)
        {
            TickOutOfLives();
            return;
        }
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasReleasedThisFrame) return;
        Vector2 tap = pointer.position.ReadValue();
        if (settingsHit != null && overlay.ScreenRect(settingsHit).Contains(tap))
        {
            JuicyAudioHooks.Tap();
            settings.Open();
            return;
        }
        if (overlay.ScreenRect(switchHit).Contains(tap))
        {
            JuicyAudioHooks.Tap();
            SetLook(look == Look.Board ? Look.Streets : Look.Board);
            return;
        }
        for (int i = 0; i < stopHits.Length; i++)
            if (overlay.ScreenRect(stopHits[i]).Contains(tap) && Choose(i)) return;
        if (vaultHit != null && overlay.ScreenRect(vaultHit).Contains(tap)) ChooseVault();
    }

    void Build()
    {
        if (content != null) Destroy(content);
        content = new GameObject("Map_" + look);
        content.transform.SetParent(transform, false);
        pulse = null;
        vaultHit = null;
        stopHits = new Renderer[director.Place.Count];
        if (look == Look.Board) BuildBoard(content.transform);
        else BuildStreets(content.transform);
        BuildSwitch(content.transform);
        BuildTopBar(content.transform);
    }

    public bool OutOfLivesShowing => outOfLives != null && outOfLives.activeSelf;

    void BuildTopBar(Transform root)
    {
        overlay.Quad(root, "Top_Bar", new Vector2(0.5f, 0.982f), new Vector2(1.2f, 0.036f), new Color(0f, 0f, 0f, 0.75f), 0.0012f);
        livesText = overlay.Text(root, "Lives", new Vector2(0.27f, 0.982f), new Vector2(0.46f, 0.02f), Color.white);
        cashText = overlay.Text(root, "Cash", new Vector2(0.78f, 0.982f), new Vector2(0.36f, 0.02f), Neon);
        TickTopBar();
        if (outOfLives == null) BuildOutOfLives();
    }

    // Built once, beside the map's content rather than in it, so a rebuild
    // of the map never drops it.
    void BuildOutOfLives()
    {
        outOfLives = new GameObject("Out_Of_Lives");
        outOfLives.transform.SetParent(transform, false);
        Transform panel = outOfLives.transform;
        popup.Quad(panel, "Dim", new Vector2(0.5f, 0.5f), new Vector2(1.3f, 1.3f), new Color(0.01f, 0.01f, 0.03f, 0.88f), 0.008f);
        popup.Quad(panel, "Card", new Vector2(0.5f, 0.52f), new Vector2(0.82f, 0.36f), new Color(0.07f, 0.07f, 0.1f, 1f), 0.006f);
        TextMeshPro title = popup.Text(panel, "Title", new Vector2(0.5f, 0.64f), new Vector2(0.7f, 0.05f), new Color(1f, 0.3f, 0.3f));
        title.text = "OUT OF LIVES";
        nextLifeText = popup.Text(panel, "Next", new Vector2(0.5f, 0.58f), new Vector2(0.7f, 0.026f), Color.white);
        refillHit = popup.Quad(panel, "Refill", new Vector2(0.5f, 0.5f), new Vector2(0.7f, 0.06f), Neon, 0.004f);
        refillText = popup.Text(panel, "Refill_Label", new Vector2(0.5f, 0.5f), new Vector2(0.62f, 0.024f), Color.black);
        refillText.outlineWidth = 0f;
        closeLivesHit = popup.Quad(panel, "Close", new Vector2(0.5f, 0.42f), new Vector2(0.7f, 0.055f), new Color(1f, 1f, 1f, 0.12f), 0.004f);
        popup.Text(panel, "Close_Label", new Vector2(0.5f, 0.42f), new Vector2(0.4f, 0.026f), Color.white).text = "WAIT";
        outOfLives.SetActive(false);
    }

    static string Clock(long seconds) => (seconds / 60) + ":" + (seconds % 60).ToString("00");

    void TickTopBar()
    {
        if (livesText == null) return;
        int lives = director.LivesLeft;
        livesText.text = "LIVES " + lives + (lives < Lives.Max ? "  NEXT " + Clock(director.Lives.NextIn(director.Now)) : "");
        cashText.text = "$" + director.Rules.Balance.ToString("#,0");
        if (!OutOfLivesShowing) return;
        nextLifeText.text = "NEXT LIFE IN " + Clock(director.Lives.NextIn(director.Now));
        bool afford = director.Rules.Balance >= AlleyChuteDirector.RefillPrice;
        refillText.text = (afford ? "REFILL ALL  $" : "NEED $") + AlleyChuteDirector.RefillPrice.ToString("#,0") + (afford ? "" : " TO REFILL");
        popup.Tint(refillHit, afford ? Neon : new Color(0.4f, 0.4f, 0.42f));
        if (director.LivesLeft > 0) outOfLives.SetActive(false);
    }

    public void ShowOutOfLives()
    {
        if (outOfLives == null) return;
        outOfLives.SetActive(true);
        livesShownAt = Time.unscaledTime;
        JuicyAudioHooks.Failed();
        TickTopBar();
    }

    public bool Refill()
    {
        if (!director.RefillLives()) return false;
        JuicyAudioHooks.Cash();
        outOfLives.SetActive(false);
        TickTopBar();
        return true;
    }

    void TickOutOfLives()
    {
        if (Time.unscaledTime < livesShownAt + TapGuard) return;
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasReleasedThisFrame) return;
        Vector2 tap = pointer.position.ReadValue();
        if (popup.ScreenRect(refillHit).Contains(tap)) Refill();
        else if (popup.ScreenRect(closeLivesHit).Contains(tap))
        {
            JuicyAudioHooks.Tap();
            outOfLives.SetActive(false);
        }
    }

    // State of a stop, for both looks.
    enum StopState { Beaten, Open, Locked }

    StopState StateOf(int index)
    {
        PlaceProgress progress = director.Progress;
        if (progress.IsBeaten(index)) return StopState.Beaten;
        return progress.IsOpen(index) ? StopState.Open : StopState.Locked;
    }

    void BuildBoard(Transform root)
    {
        overlay.Quad(root, "Frame", new Vector2(0.5f, 0.5f), new Vector2(1.3f, 1.3f), new Color(0.22f, 0.13f, 0.07f), 0.006f);
        overlay.Quad(root, "Cork", new Vector2(0.5f, 0.505f), new Vector2(0.94f, 0.93f), new Color(0.62f, 0.45f, 0.27f), 0.005f);
        TextMeshPro title = overlay.Text(root, "Title", new Vector2(0.5f, 0.935f), new Vector2(0.8f, 0.045f), Paper);
        title.text = "CITY CASE FILE";

        int levels = director.Place.Count;
        for (int i = 0; i + 1 < Stops.Length && i < levels; i++)
        {
            bool live = StateOf(i) == StopState.Beaten;
            Color red = live ? new Color(0.85f, 0.05f, 0.05f) : new Color(0.5f, 0.2f, 0.17f);
            overlay.Line(root, "String_" + i, PinAt(i), PinAt(i + 1), 0.0035f, red, 0.002f);
        }
        for (int i = 0; i < levels && i < Stops.Length - 1; i++) BoardStop(root, i);
        BoardVault(root);
    }

    // Captions stay short: TextMeshPro drops a line that is too wide for its
    // box to its smallest size rather than shrinking it to fit.
    Vector2 PinAt(int index) => Stops[index] + new Vector2(0f, index == Stops.Length - 1 ? 0.06f : 0.055f);

    void BoardStop(Transform root, int index)
    {
        Vector2 at = Stops[index];
        StopState state = StateOf(index);
        float tilt = index % 2 == 0 ? -3f : 3f;
        Renderer frame = overlay.Quad(root, "Photo_" + index, at, new Vector2(0.3f, 0.12f), Paper, 0.0015f, tilt);
        stopHits[index] = frame;
        Color photo = state == StopState.Beaten ? new Color(0.13f, 0.27f, 0.3f)
            : state == StopState.Open ? new Color(0.95f, 0.72f, 0.18f) : new Color(0.3f, 0.3f, 0.32f);
        overlay.Quad(root, "Picture_" + index, at + new Vector2(0f, 0.01f), new Vector2(0.26f, 0.075f), photo, 0.001f, tilt);
        overlay.Quad(root, "Pin_" + index, PinAt(index), new Vector2(0.03f, 0.016f), new Color(0.9f, 0.1f, 0.1f), 0.0005f);

        TextMeshPro big = overlay.Text(root, "Mark_" + index, at + new Vector2(0f, 0.012f), new Vector2(0.24f, 0.05f),
            state == StopState.Open ? Ink : new Color(0.7f, 0.7f, 0.72f));
        // Locked reads as a word: "?" is one of the clue symbols.
        big.text = state == StopState.Open ? "PLAY" : "LOCKED";
        // A beaten level's photo shows the clue found there (T-036). One
        // beaten before clues existed says a replay will find it.
        if (state == StopState.Beaten && !director.CaseFile.Found(index))
        {
            big.text = "REPLAY";
            big.color = Neon;
        }
        else if (state == StopState.Beaten)
        {
            Clue clue = director.CaseFile.ClueAt(index);
            big.text = AlleyHack.Glyphs[clue.Symbol];
            big.color = AlleyHack.SymbolColors[clue.Symbol];
        }
        if (state == StopState.Open)
        {
            big.outlineWidth = 0f;
            pulse = frame.transform;
            pulseBase = frame.transform.localScale;
        }
        TextMeshPro caption = overlay.Text(root, "Caption_" + index, at + new Vector2(0f, -0.045f), new Vector2(0.24f, 0.022f), Ink);
        caption.outlineWidth = 0f;
        caption.text = director.Place.Level(index).Name.ToUpperInvariant() + (state == StopState.Beaten ? "  DONE" : "");
    }

    // The vault's caption: what it takes, or that it is done.
    string VaultCaption() => director.Progress.VaultCracked ? "NEXT PLACE SOON"
        : director.Progress.VaultOpen ? "TAP TO CRACK IT" : "BEAT ALL 6 FIRST";

    void BoardVault(Transform root)
    {
        Vector2 at = Stops[Stops.Length - 1];
        bool open = director.Progress.VaultOpen;
        bool cracked = director.Progress.VaultCracked;
        vaultHit = overlay.Quad(root, "Photo_Vault", at, new Vector2(0.42f, 0.13f), Paper, 0.0015f);
        Color picture = cracked ? new Color(0.13f, 0.27f, 0.3f) : open ? new Color(0.95f, 0.72f, 0.18f) : new Color(0.16f, 0.16f, 0.18f);
        overlay.Quad(root, "Picture_Vault", at + new Vector2(0f, 0.01f), new Vector2(0.38f, 0.085f), picture, 0.001f);
        overlay.Quad(root, "Pin_Vault", PinAt(Stops.Length - 1), new Vector2(0.03f, 0.016f), new Color(0.9f, 0.1f, 0.1f), 0.0005f);
        TextMeshPro big = overlay.Text(root, "Mark_Vault", at + new Vector2(0f, 0.016f), new Vector2(0.34f, 0.05f), open && !cracked ? Ink : Paper);
        big.text = cracked ? "CRACKED" : "THE VAULT";
        if (open && !cracked)
        {
            big.outlineWidth = 0f;
            if (pulse == null)
            {
                pulse = vaultHit.transform;
                pulseBase = vaultHit.transform.localScale;
            }
        }
        TextMeshPro caption = overlay.Text(root, "Caption_Vault", at + new Vector2(0f, -0.047f), new Vector2(0.36f, 0.022f), Ink);
        caption.outlineWidth = 0f;
        caption.text = VaultCaption();
    }

    void BuildStreets(Transform root)
    {
        overlay.Quad(root, "Night", new Vector2(0.5f, 0.5f), new Vector2(1.3f, 1.3f), new Color(0.04f, 0.06f, 0.11f), 0.006f);
        Color street = new Color(0.1f, 0.14f, 0.23f);
        for (float y = 0.08f; y < 1f; y += 0.11f)
            overlay.Line(root, "Street_" + y.ToString("0.00"), new Vector2(-0.1f, y), new Vector2(1.1f, y), 0.012f, street, 0.005f);
        for (float x = 0.1f; x < 1f; x += 0.2f)
            overlay.Line(root, "Avenue_" + x.ToString("0.0"), new Vector2(x, -0.1f), new Vector2(x, 1.1f), 0.02f, street, 0.005f);
        TextMeshPro title = overlay.Text(root, "Title", new Vector2(0.5f, 0.935f), new Vector2(0.8f, 0.045f), Neon);
        title.text = "THE CITY";

        int levels = director.Place.Count;
        for (int i = 0; i + 1 < Stops.Length && i < levels; i++)
        {
            bool live = StateOf(i) == StopState.Beaten;
            overlay.Line(root, "Route_" + i, Stops[i], Stops[i + 1], 0.008f, live ? Neon : new Color(0.3f, 0.32f, 0.38f), 0.003f);
        }
        for (int i = 0; i < levels && i < Stops.Length - 1; i++) StreetStop(root, i);
        StreetVault(root);
    }

    // A square on screen, sized by its width in viewport widths.
    static Vector2 Square(float width) => new Vector2(width, width * 9f / 16f);

    void StreetStop(Transform root, int index)
    {
        Vector2 at = Stops[index];
        StopState state = StateOf(index);
        Color fill = state == StopState.Beaten ? Done : state == StopState.Open ? Neon : Locked;
        overlay.Quad(root, "Glow_" + index, at, Square(0.17f), new Color(fill.r, fill.g, fill.b, 0.25f), 0.0025f, 45f);
        Renderer pin = overlay.Quad(root, "Pin_" + index, at, Square(0.12f), fill, 0.002f, 45f);
        stopHits[index] = pin;
        TextMeshPro number = overlay.Text(root, "Number_" + index, at, new Vector2(0.1f, 0.04f), state == StopState.Locked ? new Color(0.7f, 0.72f, 0.78f) : Ink);
        number.outlineWidth = 0f;
        number.text = (index + 1).ToString();
        TextMeshPro label = overlay.Text(root, "Label_" + index, at + new Vector2(0f, -0.06f), new Vector2(0.3f, 0.025f), Color.white);
        label.text = state == StopState.Open ? "PLAY" : director.Place.Level(index).Name.ToUpperInvariant();
        if (state == StopState.Open)
        {
            pulse = pin.transform;
            pulseBase = pin.transform.localScale;
            label.color = Neon;
        }
    }

    void StreetVault(Transform root)
    {
        Vector2 at = Stops[Stops.Length - 1];
        bool open = director.Progress.VaultOpen;
        bool cracked = director.Progress.VaultCracked;
        vaultHit = overlay.Quad(root, "Vault", at, Square(0.2f), cracked ? Done : open ? Neon : Locked, 0.002f);
        TextMeshPro big = overlay.Text(root, "Vault_Label", at, new Vector2(0.18f, 0.03f), open ? Ink : Color.white);
        big.outlineWidth = open ? 0f : big.outlineWidth;
        big.text = "VAULT";
        TextMeshPro caption = overlay.Text(root, "Vault_Caption", at + new Vector2(0f, -0.075f), new Vector2(0.4f, 0.022f), Color.white);
        caption.text = VaultCaption();
    }

    // Prototype only: switches between the two looks.
    void BuildSwitch(Transform root)
    {
        switchHit = overlay.Quad(root, "Switch", new Vector2(0.36f, 0.045f), new Vector2(0.62f, 0.045f), new Color(0f, 0f, 0f, 0.7f), 0.001f);
        TextMeshPro label = overlay.Text(root, "Switch_Label", new Vector2(0.36f, 0.045f), new Vector2(0.58f, 0.024f), Color.white);
        label.text = "LOOK: " + (look == Look.Board ? "BOARD" : "STREETS") + " (TAP)";
        settingsHit = null;
        if (settings == null) return;
        settingsHit = overlay.Quad(root, "Settings", new Vector2(0.835f, 0.045f), new Vector2(0.27f, 0.045f), new Color(0f, 0f, 0f, 0.7f), 0.001f);
        TextMeshPro gear = overlay.Text(root, "Settings_Label", new Vector2(0.835f, 0.045f), new Vector2(0.24f, 0.024f), Color.white);
        gear.text = "SETTINGS";
    }
}
