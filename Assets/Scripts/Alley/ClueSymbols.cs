using System.Collections.Generic;
using UnityEngine;

// Draws the 8 clue symbols from ClueRules as bold neon textures, so the same
// picture shows where the clue is found and where it is kept. Covered paints
// grime over that fraction of the symbol's stroke: a weak leg hides more of it.
public static class ClueSymbols
{
    public const int Size = 256;
    const float Stroke = 0.1f;
    const float GlowReach = 0.16f;

    static readonly string[] Names = { "Circle", "Triangle", "Square", "Diamond", "Star", "Cross", "Bolt", "Heart" };

    static readonly Color[] Colors =
    {
        new Color(0.2f, 1f, 1f),
        new Color(1f, 0.25f, 0.85f),
        new Color(0.55f, 1f, 0.2f),
        new Color(1f, 0.92f, 0.2f),
        new Color(1f, 0.55f, 0.12f),
        new Color(1f, 0.18f, 0.3f),
        new Color(0.35f, 0.6f, 1f),
        new Color(0.72f, 0.38f, 1f),
    };

    static readonly Dictionary<int, Texture2D> cache = new Dictionary<int, Texture2D>();

    public static string Name(int symbol) => Names[Mathf.Clamp(symbol, 0, Names.Length - 1)];
    public static Color Neon(int symbol) => Colors[Mathf.Clamp(symbol, 0, Colors.Length - 1)];

    // One texture per symbol and cover; the same clue always draws the same.
    public static Texture2D Texture(int symbol, float covered, int seed)
    {
        symbol = Mathf.Clamp(symbol, 0, ClueRules.SymbolPool - 1);
        int key = covered <= 0f ? symbol : (symbol * 1000 + Mathf.RoundToInt(covered * 100f)) * 7919 + seed;
        if (cache.TryGetValue(key, out Texture2D found) && found != null) return found;
        Texture2D texture = Draw(symbol, covered, seed);
        cache[key] = texture;
        return texture;
    }

    static Texture2D Draw(int symbol, float covered, int seed)
    {
        List<Vector2[]> lines = Outline(symbol);
        Color neon = Neon(symbol);
        Color hot = Color.Lerp(neon, Color.white, 0.55f);
        var pixels = new Color[Size * Size];
        var core = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                float d = Distance(p, lines);
                float aa = 2.5f / Size;
                float c = Mathf.Clamp01((Stroke + aa - d) / (2f * aa));
                float glow = d > Stroke ? Mathf.Exp(-(d - Stroke) / GlowReach) : 1f;
                // A dark disc behind the symbol keeps it readable over any prop.
                float backing = Mathf.Clamp01((1f - p.magnitude) * 6f) * 0.55f;
                Color under = new Color(0.02f, 0.02f, 0.035f, backing);
                Color halo = new Color(neon.r, neon.g, neon.b, glow * 0.75f);
                Color over = Blend(under, halo);
                Color lit = Blend(over, new Color(hot.r, hot.g, hot.b, c));
                // Fade out before the quad's edge, so no square shows.
                lit.a *= Mathf.Clamp01((0.98f - p.magnitude) / 0.12f);
                pixels[y * Size + x] = lit;
                core[y * Size + x] = c;
            }
        }
        if (covered > 0f) Grime(pixels, core, Mathf.Clamp01(covered), seed + symbol * 131);

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Clue_" + Names[symbol], wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return texture;
    }

    // Grime blots hide exactly the covered fraction of the stroke: the blot
    // threshold is the matching quantile of the noise over stroke pixels.
    static void Grime(Color[] pixels, float[] core, float covered, int seed)
    {
        var noise = new float[pixels.Length];
        var onStroke = new List<float>();
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                noise[i] = Fractal(x / (float)Size, y / (float)Size, seed);
                if (core[i] > 0.5f) onStroke.Add(noise[i]);
            }
        }
        if (onStroke.Count == 0) return;
        onStroke.Sort();
        float threshold = onStroke[Mathf.Clamp(Mathf.RoundToInt(covered * onStroke.Count), 0, onStroke.Count - 1)];
        if (covered >= 1f) threshold = float.MaxValue;
        Color mud = new Color(0.06f, 0.055f, 0.05f, 1f);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int i = y * Size + x;
                var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                if (p.magnitude > 1.02f) continue;
                float edge = Mathf.Clamp01((threshold - noise[i]) * 40f);
                if (edge <= 0f) continue;
                pixels[i] = Color.Lerp(pixels[i], mud, edge);
            }
        }
    }

    static Color Blend(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);
        Color rgb = (over * over.a + under * under.a * (1f - over.a)) / a;
        return new Color(rgb.r, rgb.g, rgb.b, a);
    }

    static float Distance(Vector2 p, List<Vector2[]> lines)
    {
        float best = float.MaxValue;
        foreach (Vector2[] line in lines)
            for (int i = 0; i + 1 < line.Length; i++)
                best = Mathf.Min(best, Segment(p, line[i], line[i + 1]));
        return best;
    }

    static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }

    // Polylines in -1..1, inside a 0.72 radius so the glow fits.
    static List<Vector2[]> Outline(int symbol)
    {
        const float r = 0.66f;
        switch (symbol)
        {
            case 0: return One(Ring(r, 48, 0f));
            case 1: return One(Ring(r, 3, 90f));
            case 2: return One(Ring(r, 4, 45f));
            case 3: return One(Squash(Ring(r, 4, 90f), 0.75f));
            case 4: return One(StarPoints(r, r * 0.42f));
            case 5: return new List<Vector2[]> { new[] { new Vector2(-r, -r) * 0.8f, new Vector2(r, r) * 0.8f }, new[] { new Vector2(-r, r) * 0.8f, new Vector2(r, -r) * 0.8f } };
            case 6: return One(new[] { new Vector2(0.15f, 0.7f), new Vector2(-0.3f, 0.02f), new Vector2(0.18f, 0.02f), new Vector2(-0.2f, -0.7f) });
            default: return One(HeartPoints(r));
        }
    }

    static List<Vector2[]> One(Vector2[] line) => new List<Vector2[]> { line };

    static Vector2[] Ring(float radius, int sides, float startDegrees)
    {
        var points = new Vector2[sides + 1];
        for (int i = 0; i <= sides; i++)
        {
            float a = (startDegrees + 360f * i / sides) * Mathf.Deg2Rad;
            points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        return points;
    }

    static Vector2[] Squash(Vector2[] points, float xScale)
    {
        for (int i = 0; i < points.Length; i++) points[i].x *= xScale;
        return points;
    }

    static Vector2[] StarPoints(float outer, float inner)
    {
        var points = new Vector2[11];
        for (int i = 0; i <= 10; i++)
        {
            float a = (90f + 36f * i) * Mathf.Deg2Rad;
            float radius = i % 2 == 0 ? outer : inner;
            points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        return points;
    }

    static Vector2[] HeartPoints(float size)
    {
        const int steps = 48;
        var points = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = Mathf.PI * 2f * i / steps;
            float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
            float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
            points[i] = new Vector2(x, y + 2.5f) * (size / 17f);
        }
        return points;
    }

    static float Fractal(float x, float y, int seed)
    {
        return Value(x * 5f, y * 5f, seed) * 0.6f + Value(x * 11f, y * 11f, seed + 17) * 0.3f + Value(x * 23f, y * 23f, seed + 41) * 0.1f;
    }

    static float Value(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
