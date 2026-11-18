using TMPro;
using UnityEngine;

// One clue symbol painted into the alley: on a wall or prop face, or on the
// ground under a prop. It shows the same glyph and color as the hack keypad's
// key for that symbol, so what the player found is what they tap.
//
// A grime quad sits over the glyph. Its texture's alpha is a ranked noise, so
// an alpha cutoff of c leaves 1 - c of the patch: the rub wipes it away, and
// what stays afterwards is the share ClueRules covers for the leg's tier. On
// the wall rub the mud patch shows before the clue is found, so the player can
// spot it; on the torch and under the prop only the dirt left over the paint
// shows.
public class AlleyClueMark : MonoBehaviour
{
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    static readonly Color Mud = new Color(0.22f, 0.18f, 0.13f);
    const int GrimeSize = 128;

    [SerializeField] TextMeshPro glyph;
    [SerializeField] Renderer grime;
    [SerializeField] Light glow;
    [SerializeField] int seed;

    MaterialPropertyBlock block;
    Material face;
    Texture2D grimeTexture;
    Color color = Color.white;

    public TextMeshPro Glyph => glyph;
    public Vector3 Center => glyph != null ? glyph.transform.position : transform.position;
    // How big the symbol is in the world, in metres.
    public float Size => transform.lossyScale.x;

    public void Configure(TextMeshPro text, Renderer grimeQuad, Light glowLight, int noiseSeed)
    {
        glyph = text;
        grime = grimeQuad;
        glow = glowLight;
        seed = noiseSeed;
    }

    void Awake()
    {
        block = new MaterialPropertyBlock();
        grimeTexture = GrimeTexture(seed);
        if (glyph != null) face = glyph.fontMaterial;
    }

    void OnDestroy()
    {
        if (grimeTexture != null) Destroy(grimeTexture);
    }

    // Nothing found here this cycle. The wall rub leaves its mud patch in
    // view so the player can spot where the next clue hides.
    public void Hide(ClueFind find)
    {
        glyph.gameObject.SetActive(false);
        SetGlow(0f);
        bool mud = find == ClueFind.WallRub;
        grime.gameObject.SetActive(mud);
        if (mud) SetGrime(0f);
    }

    // Sets the glyph for the symbol, still unseen until Paint raises it.
    public void Prepare(int symbol, ClueFind find)
    {
        int s = Mathf.Clamp(symbol, 0, AlleyHack.Glyphs.Length - 1);
        glyph.text = AlleyHack.Glyphs[s];
        color = AlleyHack.SymbolColors[s];
        glyph.gameObject.SetActive(true);
        Paint(0f);
        grime.gameObject.SetActive(true);
        SetGrime(find == ClueFind.WallRub ? 0f : 1.01f);
    }

    // How strongly the paint shows: 0 none, 1 painted, above 1 glowing.
    public void Paint(float strength)
    {
        if (face == null) face = glyph.fontMaterial;
        float alpha = Mathf.Clamp01(strength * 1.5f);
        Color c = color * Mathf.Max(0f, strength);
        c.a = alpha;
        face.SetColor(ShaderUtilities.ID_FaceColor, c);
        // A dark edge keeps the symbol readable on a bright prop.
        face.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        face.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.02f, 0.03f, alpha));
        glyph.color = Color.white;
    }

    // Removes grime: at cutoff c, 1 - c of the patch is left.
    public void SetGrime(float cutoff)
    {
        if (block == null) block = new MaterialPropertyBlock();
        grime.GetPropertyBlock(block);
        if (grimeTexture != null) block.SetTexture(BaseMapId, grimeTexture);
        block.SetColor(BaseColorId, Mud);
        block.SetFloat(CutoffId, Mathf.Max(0.005f, cutoff));
        grime.SetPropertyBlock(block);
    }

    public void SetGlow(float intensity)
    {
        if (glow == null) return;
        glow.color = color;
        glow.intensity = intensity;
        glow.enabled = intensity > 0.001f;
    }

    // The clue as it stays in the alley after it was found. The tier sets how
    // bright the paint is and how much of it stays worn or grimed over; it is
    // never hidden entirely.
    public void ShowFound(int symbol, int tier, ClueFind find)
    {
        Prepare(symbol, find);
        float covered = ClueRules.CoveredFor(tier);
        Paint(Strength(tier, find));
        SetGrime(GrimeLeft(covered));
        SetGlow(find == ClueFind.TorchReveal ? RestGlow(tier) : 0f);
    }

    // Paint strength once found: brighter for a better leg. UV paint keeps a
    // glow, so it sits above 1.
    public static float Strength(int tier, ClueFind find)
    {
        float k = ClueRules.ClampTier(tier) / (float)ClueRules.MaxTier;
        return find == ClueFind.TorchReveal ? Mathf.Lerp(1.4f, 2.4f, k) : Mathf.Lerp(0.9f, 1.4f, k);
    }

    // The faint light UV paint keeps once found.
    public static float RestGlow(int tier) => Mathf.Lerp(0.08f, 0.25f, ClueRules.ClampTier(tier) / (float)ClueRules.MaxTier);

    // The grime cutoff that leaves this share of the patch.
    public static float GrimeLeft(float covered) => covered <= 0f ? 1.01f : 1f - covered;

    // Alpha is the rank of a blotchy noise, so a cutoff removes that share of
    // the patch in blotches spread over the whole symbol, never just its
    // middle. Outside a rough blob it is zero and never shows.
    static Texture2D GrimeTexture(int seed)
    {
        int n = GrimeSize * GrimeSize;
        var value = new float[n];
        var inside = new bool[n];
        var order = new System.Collections.Generic.List<int>(n);
        for (int y = 0; y < GrimeSize; y++)
        {
            for (int x = 0; x < GrimeSize; x++)
            {
                int i = y * GrimeSize + x;
                float u = (x + 0.5f) / GrimeSize * 2f - 1f, v = (y + 0.5f) / GrimeSize * 2f - 1f;
                float noise = Mathf.PerlinNoise(seed * 3.17f + x * 0.06f, seed * 1.93f + y * 0.06f) * 0.65f +
                              Mathf.PerlinNoise(seed * 5.11f + x * 0.17f, seed * 7.07f + y * 0.17f) * 0.35f;
                float r = Mathf.Sqrt(u * u + v * v);
                inside[i] = r + (noise - 0.5f) * 0.35f < 0.92f;
                value[i] = noise;
                if (inside[i]) order.Add(i);
            }
        }
        order.Sort((a, b) => value[a].CompareTo(value[b]));
        var pixels = new Color32[n];
        for (int k = 0; k < order.Count; k++)
        {
            byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * (k + 1) / order.Count), 1, 255);
            pixels[order[k]] = new Color32(255, 255, 255, alpha);
        }
        var texture = new Texture2D(GrimeSize, GrimeSize, TextureFormat.RGBA32, false) { name = "Clue_Grime_" + seed, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }
}
