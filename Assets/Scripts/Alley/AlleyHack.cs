using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The hack at the vault. When the clue cycle is full the sort pauses, the
// camera holds on the keypad beside the vault door and its six keys light up:
// the cycle's clues plus decoys, shuffled each hack. The player taps the clues
// in the order they were found. A right tap beeps and lights the next ladder
// step; a wrong tap buzzes, flashes the alley red and ends the hack. ClueRules
// scores the taps, the payout is applied with the camera back on the vault so
// a perfect hack is seen turning it, the game saves and the sort resumes.
public class AlleyHack : MonoBehaviour
{
    public const int Keys = 6;
    public const int LadderLamps = ClueRules.CluesPerHackLater;

    // How each clue symbol reads on a key and around the alley.
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

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] AlleyVaultDoor vaultDoor;
    [SerializeField] AlleyCameraRig cameraRig;
    [SerializeField] Renderer[] keys = new Renderer[Keys];
    [SerializeField] TextMeshPro[] glyphs = new TextMeshPro[Keys];
    [SerializeField] Renderer[] lamps = new Renderer[LadderLamps];
    [SerializeField] TextMeshPro[] lampLabels = new TextMeshPro[LadderLamps];
    [SerializeField] Transform viewPoint;
    [SerializeField] Light keypadLight;
    [SerializeField] Light alarmLight;
    [SerializeField] float viewFov = 40f;
    [SerializeField] float keypadLightIntensity = 0.5f;
    [SerializeField] float alarmIntensity = 30f;
    [SerializeField] float alarmSeconds = 1.2f;
    [Tooltip("Chance the bot taps each step right, so bot runs see both perfect and failed hacks.")]
    [Range(0f, 1f)] [SerializeField] float botAccuracy = 0.85f;
    [SerializeField] float botTapGap = 0.6f;

    readonly int[] keySymbols = new int[Keys];
    readonly float[] keyPunch = new float[Keys];
    readonly List<int> taps = new List<int>();
    Vector3[] keyScale;
    MaterialPropertyBlock block;
    Camera view;
    System.Random shuffle;
    bool awaitingTap;
    int tappedKey = -1;
    float autoTimer;
    int autoWrongAt = -1;
    int scriptedWrongAt = -2;

    public bool Running { get; private set; }
    public int Hacks { get; private set; }
    public int Perfects { get; private set; }
    public HackReward LastReward { get; private set; }
    public int LastClueCount { get; private set; }
    public IReadOnlyList<int> KeySymbols => keySymbols;
    public float BotAccuracy { get => botAccuracy; set => botAccuracy = Mathf.Clamp01(value); }

    // Fires once a hack's payout has been applied: the reward and how many
    // clues the cycle held.
    public event Action<HackReward, int> Finished;

    public void Configure(AlleyChuteDirector chute, AlleyVaultDoor door, AlleyCameraRig rig, Renderer[] keyRenderers, TextMeshPro[] keyGlyphs,
        Renderer[] ladderLamps, TextMeshPro[] ladderLabels, Transform cameraSpot, Light padLight, Light alarm)
    {
        director = chute;
        vaultDoor = door;
        cameraRig = rig;
        keys = keyRenderers;
        glyphs = keyGlyphs;
        lamps = ladderLamps;
        lampLabels = ladderLabels;
        viewPoint = cameraSpot;
        keypadLight = padLight;
        alarmLight = alarm;
    }

    // The next hack taps on its own: every step right, or wrong at the given
    // tap (0 based). Used by the batch captures.
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

    void Update()
    {
        if (!Running)
        {
            if (Ready()) StartCoroutine(Run());
            return;
        }
        for (int i = 0; i < keys.Length; i++)
        {
            keyPunch[i] = Mathf.MoveTowards(keyPunch[i], 0f, Time.deltaTime * 6f);
            keys[i].transform.localScale = new Vector3(keyScale[i].x, keyScale[i].y, keyScale[i].z * (1f - 0.6f * keyPunch[i]));
        }
        if (awaitingTap) ReadTap();
    }

    // The hack starts once the cycle is full and nothing else holds the alley:
    // no other round paused the sort and no vault crack is playing.
    bool Ready()
    {
        if (director == null || director.Clues == null || !director.Clues.HackDue) return false;
        if (director.Paused) return false;
        return vaultDoor == null || !vaultDoor.Cracking;
    }

    IEnumerator Run()
    {
        Running = true;
        director.Paused = true;
        ClueRules clues = director.Clues;
        int count = clues.Count;
        LastClueCount = count;
        bool auto = scriptedWrongAt != -2 || director.AutoPlay;
        autoWrongAt = scriptedWrongAt != -2 ? scriptedWrongAt : BotWrongAt(count);
        scriptedWrongAt = -2;

        LayOut(clues);
        cameraRig.Hold(viewPoint.position, viewPoint.position + viewPoint.forward, viewFov);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            if (keypadLight != null) keypadLight.intensity = keypadLightIntensity * (t / 0.5f);
            yield return null;
        }
        if (keypadLight != null) keypadLight.intensity = keypadLightIntensity;
        while (!cameraRig.Settled) yield return null;

        taps.Clear();
        bool failed = false;
        while (taps.Count < count && !failed)
        {
            tappedKey = -1;
            autoTimer = botTapGap;
            awaitingTap = true;
            while (tappedKey < 0)
            {
                if (auto) TickAuto(clues);
                yield return null;
            }
            awaitingTap = false;
            int step = taps.Count;
            int symbol = keySymbols[tappedKey];
            keyPunch[tappedKey] = 1f;
            taps.Add(symbol);
            if (symbol == clues.Clues[step].Symbol)
            {
                JuicyAudioHooks.Beep();
                PaintLamp(step, LampLit, 1.2f);
            }
            else
            {
                failed = true;
                JuicyAudioHooks.Buzz();
                PaintLamp(step, LampMissed, 1.2f);
                cameraRig.Shake(0.4f);
                yield return Alarm();
            }
        }

        // Scored now so the keypad clears with the cycle; paid once the camera
        // is back on the vault.
        HackReward reward = clues.Hack(taps);
        if (!failed) yield return new WaitForSeconds(0.8f);
        cameraRig.Release();
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            if (keypadLight != null) keypadLight.intensity = keypadLightIntensity * (1f - t / 0.5f);
            yield return null;
        }
        Dim();
        while (!cameraRig.Settled) yield return null;

        reward.Apply(director.Rules, director.Vault);
        if (reward.Cash > 0d || reward.Bribes > 0 || reward.Drills > 0) JuicyAudioHooks.Chime();
        director.SaveNow();
        Hacks++;
        if (reward.Perfect) Perfects++;
        LastReward = reward;
        Finished?.Invoke(reward, count);
        // Let a perfect hack's turn land on the door before items fall again.
        if (reward.Turns > 0) yield return new WaitForSeconds(1.2f);
        director.Paused = false;
        Running = false;
    }

    // The bot taps every step right with botAccuracy each, so the first miss
    // is where its hack ends. -1 means a perfect hack.
    int BotWrongAt(int count)
    {
        for (int i = 0; i < count; i++)
            if (UnityEngine.Random.value >= botAccuracy) return i;
        return -1;
    }

    void TickAuto(ClueRules clues)
    {
        autoTimer -= Time.deltaTime;
        if (autoTimer > 0f) return;
        int step = taps.Count;
        int expected = clues.Clues[step].Symbol;
        bool wrong = step == autoWrongAt;
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

    // The cycle's clues plus decoys from the rest of the pool, in shuffled
    // key positions, and the ladder's rungs labeled with what each pays.
    void LayOut(ClueRules clues)
    {
        var symbols = new List<int>();
        for (int i = 0; i < clues.Count && symbols.Count < Keys; i++) symbols.Add(clues.Clues[i].Symbol);
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
            bool used = i < clues.Count;
            lamps[i].gameObject.SetActive(used);
            if (!used) continue;
            lampLabels[i].text = RungLabel(clues.Count, i);
            PaintLamp(i, LampDark, 0f);
        }
    }

    // What the step after i correct taps adds, read from ClueRules' ladder.
    public static string RungLabel(int clueCount, int index)
    {
        HackReward before = ClueRules.RewardFor(clueCount, index);
        HackReward after = ClueRules.RewardFor(clueCount, index + 1);
        var parts = new List<string>();
        if (after.Cash > before.Cash) parts.Add("$" + (after.Cash - before.Cash).ToString("0"));
        if (after.Bribes > before.Bribes) parts.Add("BRIBE");
        if (after.Drills > before.Drills) parts.Add("DRILL");
        if (after.Turns > before.Turns) parts.Add("VAULT");
        return string.Join("\n", parts);
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
        for (int i = 0; i < lamps.Length; i++) PaintLamp(i, LampDark, 0f);
    }

    void PaintKey(int index, Color color, float glow)
    {
        Paint(keys[index], color, glow);
    }

    void PaintLamp(int index, Color color, float glow)
    {
        Paint(lamps[index], color, glow);
    }

    void Paint(Renderer target, Color color, float glow)
    {
        target.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(EmissionId, color * glow);
        target.SetPropertyBlock(block);
    }
}
