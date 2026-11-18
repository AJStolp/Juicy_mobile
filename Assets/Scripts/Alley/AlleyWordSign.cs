using System.Collections;
using TMPro;
using UnityEngine;

// The word in progress, spelled out in tiles that match the falling letter
// tiles. Missing letters sit dark with a faint glyph; a banked letter lights
// gold with a pop. Completing the word flashes the whole sign, then the tiles
// flip over to the next word.
public class AlleyWordSign : MonoBehaviour
{
    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Renderer[] tiles;
    [SerializeField] TextMeshPro[] glyphs;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color Dark = new Color(0.07f, 0.075f, 0.09f);
    static readonly Color Gold = new Color(0.98f, 0.76f, 0.22f);
    static readonly Color GlyphDim = new Color(0.55f, 0.5f, 0.4f, 0.35f);
    static readonly Color GlyphLit = new Color(0.16f, 0.08f, 0.03f, 1f);

    HeistSortRules rules;
    MaterialPropertyBlock block;
    Vector3[] tileScale;
    float[] punch;
    bool[] lit;
    string word = "";
    bool celebrating;

    public int LitCount { get; private set; }

    public void Configure(AlleyChuteDirector chute, Renderer[] tileRenderers, TextMeshPro[] tileGlyphs)
    {
        director = chute;
        tiles = tileRenderers;
        glyphs = tileGlyphs;
    }

    void Awake()
    {
        block = new MaterialPropertyBlock();
        tileScale = new Vector3[tiles.Length];
        punch = new float[tiles.Length];
        lit = new bool[tiles.Length];
        for (int i = 0; i < tiles.Length; i++) tileScale[i] = tiles[i].transform.localScale;
    }

    void Start()
    {
        if (director == null) return;
        rules = director.Rules;
        rules.LetterBanked += OnLetterBanked;
        rules.WordCompleted += OnWordCompleted;
        Show(rules.ActiveWord, i => rules.IsFilled(i));
    }

    void OnDestroy()
    {
        if (rules == null) return;
        rules.LetterBanked -= OnLetterBanked;
        rules.WordCompleted -= OnWordCompleted;
    }

    // Lays out a word with some letters lit. Also used to render previews in
    // the editor, where there are no rules to read.
    public void Show(string text, System.Func<int, bool> filled)
    {
        if (block == null) Awake();
        word = text;
        LitCount = 0;
        for (int i = 0; i < tiles.Length; i++)
        {
            bool used = i < word.Length;
            tiles[i].gameObject.SetActive(used);
            if (!used) continue;
            glyphs[i].text = word[i].ToString();
            lit[i] = filled(i);
            if (lit[i]) LitCount++;
            Paint(i, lit[i] ? 1f : 0f);
        }
        Center();
    }

    // Keeps a shorter word centered on the sign.
    void Center()
    {
        if (tiles.Length < 2) return;
        float step = tiles[1].transform.localPosition.x - tiles[0].transform.localPosition.x;
        float start = -step * (word.Length - 1) * 0.5f;
        for (int i = 0; i < word.Length && i < tiles.Length; i++)
        {
            Vector3 p = tiles[i].transform.localPosition;
            tiles[i].transform.localPosition = new Vector3(start + step * i, p.y, p.z);
        }
    }

    void OnLetterBanked(char letter, string current, int filledCount)
    {
        if (celebrating || current != word) return;
        for (int i = 0; i < word.Length && i < tiles.Length; i++)
        {
            if (lit[i] || !rules.IsFilled(i)) continue;
            lit[i] = true;
            LitCount++;
            punch[i] = 1f;
        }
    }

    void OnWordCompleted(string done, double bonus)
    {
        // The last letter has lit already; the rules have moved to the next word.
        for (int i = 0; i < word.Length && i < tiles.Length; i++)
        {
            if (!lit[i]) { lit[i] = true; LitCount++; punch[i] = 1f; }
        }
        StartCoroutine(Celebrate());
    }

    IEnumerator Celebrate()
    {
        celebrating = true;
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            float pulse = 1f + 1.5f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f));
            for (int i = 0; i < word.Length && i < tiles.Length; i++) Paint(i, pulse);
            yield return null;
        }
        // Flip to the next word: fold flat, swap, unfold.
        for (float t = 0f; t < 0.2f; t += Time.deltaTime) { Fold(1f - t / 0.2f); yield return null; }
        Show(rules.ActiveWord, i => rules.IsFilled(i));
        for (float t = 0f; t < 0.2f; t += Time.deltaTime) { Fold(t / 0.2f); yield return null; }
        Fold(1f);
        celebrating = false;
    }

    void Fold(float open)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            Vector3 s = tileScale[i];
            tiles[i].transform.localScale = new Vector3(s.x, s.y * Mathf.Max(0.02f, open), s.z);
        }
    }

    void Update()
    {
        if (celebrating) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < word.Length && i < tiles.Length; i++)
        {
            if (punch[i] <= 0f) continue;
            punch[i] = Mathf.MoveTowards(punch[i], 0f, dt * 2.2f);
            float k = punch[i];
            tiles[i].transform.localScale = tileScale[i] * (1f + 0.45f * k * k);
            Paint(i, 1f + 3f * k);
        }
    }

    // glow 0 is dark and unbanked; 1 is lit; above 1 flashes brighter.
    void Paint(int i, float glow)
    {
        bool on = glow > 0f;
        tiles[i].GetPropertyBlock(block);
        block.SetColor(BaseColorId, on ? Gold : Dark);
        block.SetColor(EmissionId, on ? Gold * (0.6f * glow) : Color.black);
        tiles[i].SetPropertyBlock(block);
        glyphs[i].color = on ? GlyphLit : GlyphDim;
    }
}
