using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The vault finale, at the keypad beside the vault door. When a vault attempt
// reaches its target (AlleyChuteDirector.FinaleStarted) the camera holds on
// the keypad and its six keys light up with the place's clues, plus decoys
// when fewer were found, shuffled each attempt. The case file runs along the
// top of the screen: clear clues stay readable, a smudged one fades after a
// few seconds, a barely readable one never shows (ClueRules). The player taps
// the clues in board order (VaultFinale): a right tap beeps and lights the
// next lamp; a wrong tap buzzes and flashes the alley red, and the third trips
// the alarm. Then the camera goes back to the vault and the director finishes
// the attempt: the wheel turns and the door swings open on a crack.
public class AlleyHack : MonoBehaviour
{
    public const int Keys = 6;
    public const int Lamps = 6;

    // How each clue symbol reads on a key, a scratch card and the board.
    public static readonly string[] Glyphs = { "$", "#", "%", "&", "@", "?", "!", "+" };
    public static readonly Color[] SymbolColors =
    {
        new Color(0.3f, 0.95f, 0.4f),
        new Color(1f, 0.32f, 0.28f),
        new Color(0.3f, 0.6f, 1f),
        new Color(1f, 0.8f, 0.2f),
        new Color(0.85f, 0.4f, 1f),
        new Color(0.25f, 0.95f, 0.95f),
        new Color(1f, 0.55f, 0.15f),
        new Color(1f, 0.45f, 0.75f),
    };

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color KeyDark = new Color(0.08f, 0.085f, 0.1f);
    static readonly Color LampDark = new Color(0.12f, 0.1f, 0.06f);
    static readonly Color LampLit = new Color(0.35f, 1f, 0.45f);
    static readonly Color LampMissed = new Color(1f, 0.12f, 0.1f);
    static readonly Color GlyphInk = new Color(0.03f, 0.03f, 0.04f);
    static readonly Color Neon = new Color(1f, 0.85f, 0.25f);
    static readonly Color Paper = new Color(0.97f, 0.95f, 0.9f);

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] AlleyCameraRig cameraRig;
    [SerializeField] Material panelMaterial;
    [SerializeField] Renderer[] keys = new Renderer[Keys];
    [SerializeField] TextMeshPro[] glyphs = new TextMeshPro[Keys];
    [SerializeField] Renderer[] lamps = new Renderer[Lamps];
    [SerializeField] Transform viewPoint;
    [SerializeField] Light keypadLight;
    [SerializeField] Light alarmLight;
    [SerializeField] float viewFov = 40f;
    [SerializeField] float keypadLightIntensity = 0.5f;
    [SerializeField] float alarmIntensity = 30f;
    [SerializeField] float alarmSeconds = 1.2f;
    [Tooltip("Chance auto play taps each step right, so bot runs see cracks and trips.")]
    [Range(0f, 1f)] [SerializeField] float botAccuracy = 0.85f;
    [SerializeField] float botTapGap = 0.6f;

    readonly int[] keySymbols = new int[Keys];
    readonly float[] keyPunch = new float[Keys];
    Vector3[] keyScale;
    MaterialPropertyBlock block;
    Camera view;
    System.Random shuffle;
    CameraOverlay overlay;
    GameObject strip;
    TextMeshPro[] stripGlyphs;
    Renderer[] stripCards;
    float[] stripHideAt;
    bool[] stripEntered;
    TextMeshPro alarmText;
    float startedAt;
    bool awaitingTap;
    int tappedKey = -1;
    float autoTimer;
    int scriptedWrongAt = -2;
    int autoWrongAt = -1;

    public bool Running { get; private set; }

    public void Configure(AlleyChuteDirector chute, AlleyCameraRig rig, Material overlayMaterial, Renderer[] keyRenderers, TextMeshPro[] keyGlyphs,
        Renderer[] progressLamps, Transform cameraSpot, Light padLight, Light alarm)
    {
        director = chute;
        cameraRig = rig;
        panelMaterial = overlayMaterial;
        keys = keyRenderers;
        glyphs = keyGlyphs;
        lamps = progressLamps;
        viewPoint = cameraSpot;
        keypadLight = padLight;
        alarmLight = alarm;
    }

    // The next finale taps on its own: every step right, or one wrong tap at
    // the given step (0 based). Used by the batch checks.
    public void ScriptNext(int wrongAt) => scriptedWrongAt = wrongAt;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        view = Camera.main;
        shuffle = new System.Random(Environment.TickCount);
        keyScale = new Vector3[keys.Length];
        for (int i = 0; i < keys.Length; i++) keyScale[i] = keys[i].transform.localScale;
        if (keypadLight != null) keypadLight.intensity = 0f;
        if (alarmLight != null) alarmLight.intensity = 0f;
        Dim();
    }

    void Start()
    {
        overlay = new CameraOverlay(view, panelMaterial, 0.38f, CameraOverlay.LayerFinale);
        director.FinaleStarted += Begin;
    }

    void OnDestroy()
    {
        if (director != null) director.FinaleStarted -= Begin;
    }

    void Begin(VaultFinale finale)
    {
        if (!Running) StartCoroutine(Run(finale));
    }

    void Update()
    {
        if (!Running) return;
        for (int i = 0; i < keys.Length; i++)
        {
            keyPunch[i] = Mathf.MoveTowards(keyPunch[i], 0f, Time.deltaTime * 6f);
            keys[i].transform.localScale = new Vector3(keyScale[i].x, keyScale[i].y, keyScale[i].z * (1f - 0.6f * keyPunch[i]));
        }
        FadeStrip();
        if (awaitingTap) ReadTap();
    }

    IEnumerator Run(VaultFinale finale)
    {
        Running = true;
        bool auto = scriptedWrongAt != -2 || director.AutoPlay;
        autoWrongAt = scriptedWrongAt;
        scriptedWrongAt = -2;

        LayOut(finale);
        cameraRig.Hold(viewPoint.position, viewPoint.position + viewPoint.forward, viewFov);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            if (keypadLight != null) keypadLight.intensity = keypadLightIntensity * (t / 0.5f);
            yield return null;
        }
        if (keypadLight != null) keypadLight.intensity = keypadLightIntensity;
        while (!cameraRig.Settled) yield return null;
        // Built once the camera has zoomed in on the keypad, since the strip
        // is sized to the view it is drawn in; clarity timing starts here.
        startedAt = Time.time;
        BuildStrip(finale);

        while (!finale.Done)
        {
            tappedKey = -1;
            autoTimer = botTapGap;
            awaitingTap = true;
            while (tappedKey < 0)
            {
                if (auto) TickAuto(finale);
                yield return null;
            }
            awaitingTap = false;
            int step = finale.Step;
            keyPunch[tappedKey] = 1f;
            FinaleTap result = finale.Tap(keySymbols[tappedKey]);
            if (result == FinaleTap.Right || result == FinaleTap.Cracked)
            {
                JuicyAudioHooks.Beep();
                if (step < lamps.Length) PaintLamp(step, LampLit, 1.2f);
                MarkStrip(step);
                continue;
            }
            JuicyAudioHooks.Buzz();
            cameraRig.Shake(0.4f);
            alarmText.text = finale.Tripped ? "ALARM TRIPPED" : AlarmsLine(finale.AlarmsLeft);
            yield return Alarm();
        }

        yield return new WaitForSeconds(0.8f);
        ClearStrip();
        cameraRig.Release();
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            if (keypadLight != null) keypadLight.intensity = keypadLightIntensity * (1f - t / 0.5f);
            yield return null;
        }
        Dim();
        while (!cameraRig.Settled) yield return null;
        Running = false;
        director.FinishFinale();
    }

    static string AlarmsLine(int left) => left == 1 ? "1 ALARM LEFT" : left + " ALARMS LEFT";

    // Auto play taps the wanted key, or a wrong one: once at the scripted
    // step, or by chance at botAccuracy when not scripted.
    void TickAuto(VaultFinale finale)
    {
        autoTimer -= Time.deltaTime;
        if (autoTimer > 0f) return;
        bool wrong = autoWrongAt >= -1
            ? finale.Step == autoWrongAt && finale.AlarmsLeft == VaultFinale.Alarms
            : UnityEngine.Random.value >= botAccuracy;
        int expected = finale.Expected;
        for (int i = 0; i < Keys; i++)
        {
            if ((keySymbols[i] == expected) != wrong)
            {
                tappedKey = i;
                return;
            }
        }
    }

    void ReadTap()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame || view == null) return;
        Ray ray = view.ScreenPointToRay(pointer.position.ReadValue());
        float best = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, 50f))
        {
            int index = Array.IndexOf(keys, hit.collider.GetComponent<Renderer>());
            if (index < 0 || hit.distance >= best) continue;
            best = hit.distance;
            tappedKey = index;
        }
    }

    // The code's symbols plus decoys from the rest of the pool, in shuffled
    // key positions, and one dark lamp per clue to enter.
    void LayOut(VaultFinale finale)
    {
        var symbols = new List<int>();
        foreach (int s in finale.Code)
            if (!symbols.Contains(s) && symbols.Count < Keys) symbols.Add(s);
        var decoys = new List<int>();
        for (int s = 0; s < ClueRules.SymbolPool; s++)
            if (!symbols.Contains(s)) decoys.Add(s);
        Shuffle(decoys);
        for (int i = 0; symbols.Count < Keys && i < decoys.Count; i++) symbols.Add(decoys[i]);
        Shuffle(symbols);
        for (int i = 0; i < Keys; i++)
        {
            keySymbols[i] = symbols[i];
            glyphs[i].text = Glyphs[symbols[i]];
            glyphs[i].color = GlyphInk;
            PaintKey(i, SymbolColors[symbols[i]], 0.45f);
        }
        for (int i = 0; i < lamps.Length; i++)
        {
            bool used = i < finale.Count;
            lamps[i].gameObject.SetActive(used);
            if (used) PaintLamp(i, LampDark, 0f);
        }
    }

    // The case file along the top: one card per clue, readable for as long as
    // its clarity allows, lit once it is entered.
    void ClearStrip()
    {
        if (strip != null) Destroy(strip);
        strip = null;
        stripGlyphs = null;
        stripCards = null;
    }

    void BuildStrip(VaultFinale finale)
    {
        ClearStrip();
        strip = new GameObject("Finale_Strip");
        strip.transform.SetParent(view.transform, false);
        Transform root = strip.transform;
        overlay.Quad(root, "Back", new Vector2(0.5f, 0.885f), new Vector2(1.2f, 0.17f), new Color(0.01f, 0.01f, 0.03f, 0.75f), 0.006f);
        TextMeshPro title = overlay.Text(root, "Title", new Vector2(0.5f, 0.945f), new Vector2(0.9f, 0.03f), Neon);
        title.text = "ENTER YOUR CLUES IN ORDER";
        alarmText = overlay.Text(root, "Alarms", new Vector2(0.5f, 0.825f), new Vector2(0.6f, 0.022f), Color.white);
        alarmText.text = AlarmsLine(finale.AlarmsLeft);

        int count = finale.Count;
        stripGlyphs = new TextMeshPro[count];
        stripCards = new Renderer[count];
        stripHideAt = new float[count];
        stripEntered = new bool[count];
        int clue = 0;
        for (int level = 0; level < director.Place.Count && clue < count; level++)
        {
            Clue found = director.CaseFile.ClueAt(level);
            float x = count == 1 ? 0.5f : 0.15f + 0.7f * clue / (count - 1);
            Vector2 at = new Vector2(x, 0.885f);
            stripCards[clue] = overlay.Quad(root, "Card_" + clue, at, new Vector2(0.1f, 0.06f), Paper, 0.004f);
            stripGlyphs[clue] = overlay.Text(root, "Glyph_" + clue, at, new Vector2(0.08f, 0.04f), SymbolColors[found.Symbol]);
            stripGlyphs[clue].text = Glyphs[found.Symbol];
            // A clue never found shows like a barely readable one: not at all.
            stripHideAt[clue] = director.CaseFile.Found(level) ? startedAt + ClueRules.FinaleSecondsFor(found.Tier) : startedAt;
            clue++;
        }
        FadeStrip();
    }

    void FadeStrip()
    {
        if (stripGlyphs == null) return;
        for (int i = 0; i < stripGlyphs.Length; i++)
            stripGlyphs[i].enabled = stripEntered[i] || Time.time < stripHideAt[i];
    }

    void MarkStrip(int step)
    {
        if (stripGlyphs == null || step >= stripGlyphs.Length) return;
        // An entered clue stays readable.
        stripEntered[step] = true;
        overlay.Tint(stripCards[step], LampLit);
        FadeStrip();
    }

    void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = shuffle.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    IEnumerator Alarm()
    {
        for (float t = 0f; t < alarmSeconds; t += Time.deltaTime)
        {
            bool on = Mathf.Sin(t * 22f) > 0f;
            if (alarmLight != null) alarmLight.intensity = alarmIntensity * (on ? 1f : 0.15f);
            for (int i = 0; i < keys.Length; i++) PaintKey(i, on ? LampMissed : SymbolColors[keySymbols[i]], on ? 1.2f : 0.45f);
            yield return null;
        }
        if (alarmLight != null) alarmLight.intensity = 0f;
        for (int i = 0; i < keys.Length; i++) PaintKey(i, SymbolColors[keySymbols[i]], 0.45f);
    }

    void Dim()
    {
        for (int i = 0; i < keys.Length; i++)
        {
            glyphs[i].color = new Color(1f, 1f, 1f, 0.15f);
            PaintKey(i, KeyDark, 0f);
        }
        for (int i = 0; i < lamps.Length; i++)
        {
            lamps[i].gameObject.SetActive(true);
            PaintLamp(i, LampDark, 0f);
        }
    }

    void PaintKey(int index, Color color, float glow) => Paint(keys[index], color, glow);

    void PaintLamp(int index, Color color, float glow) => Paint(lamps[index], color, glow);

    void Paint(Renderer target, Color color, float glow)
    {
        target.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(EmissionId, color * glow);
        target.SetPropertyBlock(block);
    }
}
