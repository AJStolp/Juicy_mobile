using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Looks for the Plinko board, switchable to compare under Juicy/Alley/Plinko
// while Anthony picks one (T-002):
//   Gold: the brass pachinko board from build_plinko_board.py, the baseline.
//   Arcade: a street arcade cabinet in weathered steel, pink and cyan neon
//     like the building front, graffiti on the playfield and a bulb on every
//     pin that flashes when a coin strikes it.
//   Scrap: a plywood sheet bolted to the wall, sprayed with tags, rusted
//     frame, hazard tape and one caged work lamp.
// Every look keeps the board's geometry and physics. A look swaps the board's
// materials and adds dressing with no colliders, so the coins fall the same.
// The textures are painted here, from code, into Textures/Plinko.
public static partial class AlleySceneBuilder
{
    public enum PlinkoLook { Gold, Arcade, Scrap }

    const string PlinkoLookPref = "Juicy.Alley.PlinkoLook";
    const string PlinkoTextureRoot = Art + "Textures/Plinko/";
    const string GraffitiAtlas = Art + "Textures/SM_GraffitiVaultDoor_basecolor.jpg";

    public static PlinkoLook CurrentPlinkoLook => (PlinkoLook)EditorPrefs.GetInt(PlinkoLookPref, (int)PlinkoLook.Gold);
    public static void SetPlinkoLook(PlinkoLook look) { EditorPrefs.SetInt(PlinkoLookPref, (int)look); }

    [MenuItem("Juicy/Alley/Plinko/Use Gold Baseline")]
    static void UseGoldPlinko() { SetPlinkoLook(PlinkoLook.Gold); WireGameplayInScene(); }
    [MenuItem("Juicy/Alley/Plinko/Use Street Arcade")]
    static void UseArcadePlinko() { SetPlinkoLook(PlinkoLook.Arcade); WireGameplayInScene(); }
    [MenuItem("Juicy/Alley/Plinko/Use Scrap Plywood")]
    static void UseScrapPlinko() { SetPlinkoLook(PlinkoLook.Scrap); WireGameplayInScene(); }

    static Color PlinkoLightColor(PlinkoLook look)
    {
        switch (look)
        {
            case PlinkoLook.Arcade: return new Color(1f, 0.55f, 0.85f);
            case PlinkoLook.Scrap: return new Color(1f, 0.7f, 0.38f);
            default: return new Color(1f, 0.88f, 0.7f);
        }
    }

    static readonly Color NeonPink = new Color(1f, 0.2f, 0.6f);
    static readonly Color NeonCyan = new Color(0.15f, 0.9f, 1f);

    // Points on the board in build_plinko_board.py's units: x across (rail
    // left to rail right), depth out of the backboard toward the street, z up
    // from the board's bottom.
    struct BoardSpace
    {
        public Vector3 Origin, Right, Up, Front;
        public float Scale;

        public Vector3 Point(float x, float depth, float z)
        {
            return Origin + (Right * x + Front * depth + Up * z) * Scale;
        }
    }

    static BoardSpace MeasureBoard(Transform board, Transform floor, Transform railLeft, Transform railRight)
    {
        var space = new BoardSpace
        {
            Right = (railRight.position - railLeft.position).normalized,
            Up = Vector3.up,
            Front = board.forward,
            Scale = board.lossyScale.x,
        };
        // The floor's center is 0.04 out from the back and 0.05 up.
        space.Origin = floor.position - (space.Front * 0.04f + space.Up * 0.05f) * space.Scale;
        return space;
    }

    static void DressPlinko(PlinkoLook look, Transform board, Transform floor, Transform railLeft, Transform railRight)
    {
        if (look == PlinkoLook.Gold) return;
        BoardSpace space = MeasureBoard(board, floor, railLeft, railRight);
        Transform dressing = new GameObject("Plinko_Dressing_" + look).transform;
        dressing.SetParent(board, false);
        if (look == PlinkoLook.Arcade) DressArcade(board, dressing, space);
        else DressScrap(board, dressing, space);
    }

    // Board layout from build_plinko_board.py.
    const float BoardHalfWidth = 0.64f;
    const float BoardHeight = 1.62f;
    const float PlayHalfWidth = 0.54f;

    static void DressArcade(Transform board, Transform dressing, BoardSpace space)
    {
        Texture2D steel = PaintedTexture("T_PlinkoSteel", 512, 512, SteelPixels);
        Texture2D art = ArcadeArt();
        Texture2D marquee = MarqueeArt();
        SwapPlinkoMaterials(board, new Dictionary<string, Material>
        {
            { "M_PlinkoBack", TexturedMaterial("M_PlinkoArcadeBack", steel, new Color(0.35f, 0.36f, 0.42f), 0.6f, 0.3f) },
            { "M_PlinkoBrass", TexturedMaterial("M_PlinkoArcadeFrame", steel, new Color(0.8f, 0.82f, 0.88f), 0.7f, 0.42f) },
            { "M_PlinkoPin", LitMaterial(MaterialRoot + "/M_PlinkoArcadePin.mat", "M_PlinkoArcadePin", new Color(0.7f, 0.72f, 0.76f), 0.8f, 0.6f) },
        });
        Material pink = BrightMaterial("M_PlinkoNeonPink", NeonPink * 1.6f);
        Material cyan = BrightMaterial("M_PlinkoNeonCyan", NeonCyan * 1.6f);

        // Graffiti over the playfield, behind the pins.
        DressQuad(dressing, "Playfield_Art", space, new Vector3(0f, 0.003f, 0.83f), 2f * PlayHalfWidth, 1.46f, space.Up,
            TexturedMaterial("M_PlinkoArcadeArt", art, Color.white, 0.3f, 0.35f));

        // Marquee box on top, sprayed, framed in pink neon.
        const float MarqueeBottom = BoardHeight, MarqueeHeight = 0.24f;
        DressPiece(dressing, "Marquee", PrimitiveType.Cube, space, new Vector3(0f, 0.05f, MarqueeBottom + MarqueeHeight / 2f),
            new Vector3(2f * BoardHalfWidth, 0.1f, MarqueeHeight), TexturedMaterial("M_PlinkoArcadeFrame", steel, new Color(0.8f, 0.82f, 0.88f), 0.7f, 0.42f));
        DressQuad(dressing, "Marquee_Art", space, new Vector3(0f, 0.102f, MarqueeBottom + MarqueeHeight / 2f), 2f * BoardHalfWidth - 0.08f, MarqueeHeight - 0.05f,
            space.Up, TexturedMaterial("M_PlinkoArcadeMarquee", marquee, Color.white, 0.2f, 0.4f));
        float mx = BoardHalfWidth - 0.02f, mLow = MarqueeBottom + 0.015f, mHigh = MarqueeBottom + MarqueeHeight - 0.015f;
        DressTube(dressing, "Neon_Marquee_Top", space, new Vector3(-mx, 0.11f, mHigh), new Vector3(mx, 0.11f, mHigh), 0.011f, pink);
        DressTube(dressing, "Neon_Marquee_Left", space, new Vector3(-mx, 0.11f, mLow), new Vector3(-mx, 0.11f, mHigh), 0.011f, pink);
        DressTube(dressing, "Neon_Marquee_Right", space, new Vector3(mx, 0.11f, mLow), new Vector3(mx, 0.11f, mHigh), 0.011f, pink);
        DressTube(dressing, "Neon_Marquee_Bottom", space, new Vector3(-mx, 0.11f, mLow), new Vector3(mx, 0.11f, mLow), 0.011f, cyan);

        // Pink tubes up both rails, cyan under the slots.
        float rail = PlayHalfWidth + 0.03f;
        DressTube(dressing, "Neon_Rail_Left", space, new Vector3(-rail, 0.095f, 0.06f), new Vector3(-rail, 0.095f, 1.5f), 0.012f, pink);
        DressTube(dressing, "Neon_Rail_Right", space, new Vector3(rail, 0.095f, 0.06f), new Vector3(rail, 0.095f, 1.5f), 0.012f, pink);
        DressTube(dressing, "Neon_Base", space, new Vector3(-BoardHalfWidth + 0.03f, 0.09f, -0.015f), new Vector3(BoardHalfWidth - 0.03f, 0.09f, -0.015f), 0.012f, cyan);

        // A bulb over every pin head.
        Color idle = new Color(0.7f, 0.3f, 0.08f), flash = new Color(12f, 10f, 7f);
        // The saved scene shows the idle glow; the bulbs set their own at play.
        Material bulbMaterial = BrightMaterial("M_PlinkoBulb", idle);
        var pins = new List<Transform>();
        var bulbs = new List<Renderer>();
        Transform bulbRoot = new GameObject("Pin_Bulbs").transform;
        bulbRoot.SetParent(dressing, false);
        foreach (Transform child in board)
        {
            if (!child.name.StartsWith("Pin_")) continue;
            GameObject bulb = Primitive(PrimitiveType.Sphere, "Bulb_" + child.name.Substring(4), bulbRoot, bulbMaterial);
            bulb.transform.position = child.position + space.Front * (0.055f * space.Scale);
            bulb.transform.localScale = Vector3.one * (0.034f * space.Scale / bulbRoot.lossyScale.x);
            pins.Add(child);
            bulbs.Add(bulb.GetComponent<Renderer>());
        }
        AlleyPlinkoBulbs component = board.gameObject.AddComponent<AlleyPlinkoBulbs>();
        component.Configure(pins.ToArray(), bulbs.ToArray(), idle, flash);
    }

    static void DressScrap(Transform board, Transform dressing, BoardSpace space)
    {
        Texture2D plywood = PaintedTexture("T_PlinkoPlywood", 512, 512, (x, y, w, h) => PlywoodPixel(x, y, w, h, 0f));
        Texture2D rust = PaintedTexture("T_PlinkoRust", 512, 512, RustPixels);
        Texture2D hazard = PaintedTexture("T_PlinkoHazard", 512, 64, HazardPixels);
        Texture2D art = ScrapArt();
        SwapPlinkoMaterials(board, new Dictionary<string, Material>
        {
            { "M_PlinkoBack", TexturedMaterial("M_PlinkoScrapBack", plywood, new Color(0.75f, 0.7f, 0.62f), 0f, 0.12f) },
            { "M_PlinkoBrass", TexturedMaterial("M_PlinkoScrapRust", rust, Color.white, 0.35f, 0.18f) },
            { "M_PlinkoPin", LitMaterial(MaterialRoot + "/M_PlinkoScrapNail.mat", "M_PlinkoScrapNail", new Color(0.38f, 0.36f, 0.34f), 0.7f, 0.3f) },
        });

        DressQuad(dressing, "Playfield_Art", space, new Vector3(0f, 0.003f, 0.83f), 2f * PlayHalfWidth, 1.46f, space.Up,
            TexturedMaterial("M_PlinkoScrapArt", art, Color.white, 0f, 0.15f));

        // Hazard tape down both rails and along the top bar.
        Material tape = TexturedMaterial("M_PlinkoScrapHazard", hazard, Color.white, 0f, 0.35f);
        float rail = PlayHalfWidth + 0.03f;
        DressQuad(dressing, "Tape_Left", space, new Vector3(-rail, 0.082f, 0.8f), 1.5f, 0.05f, space.Right, tape);
        DressQuad(dressing, "Tape_Right", space, new Vector3(rail, 0.082f, 0.8f), 1.5f, 0.05f, space.Right, tape);
        DressQuad(dressing, "Tape_Top", space, new Vector3(0f, 0.092f, BoardHeight - 0.04f), 2f * BoardHalfWidth - 0.02f, 0.06f, space.Up, tape);

        // Big bolts holding the sheet to the wall.
        Material bolt = LitMaterial(MaterialRoot + "/M_PlinkoScrapBolt.mat", "M_PlinkoScrapBolt", new Color(0.3f, 0.29f, 0.28f), 0.8f, 0.35f);
        foreach (float x in new[] { -BoardHalfWidth + 0.045f, BoardHalfWidth - 0.045f })
            foreach (float z in new[] { 0.18f, 0.83f, 1.45f })
                DressTube(dressing, "Bolt", space, new Vector3(x, 0.08f, z), new Vector3(x, 0.105f, z), 0.022f, bolt);

        // A caged work lamp clamped over the top, throwing amber down the board.
        Material housing = LitMaterial(MaterialRoot + "/M_PlinkoScrapHousing.mat", "M_PlinkoScrapHousing", new Color(0.12f, 0.12f, 0.11f), 0.6f, 0.4f);
        Material lamp = BrightMaterial("M_PlinkoScrapLamp", new Color(1f, 0.62f, 0.25f) * 3f);
        const float LampZ = BoardHeight + 0.09f;
        DressPiece(dressing, "Lamp_Clamp", PrimitiveType.Cube, space, new Vector3(0f, 0.04f, BoardHeight + 0.02f), new Vector3(0.08f, 0.1f, 0.04f), housing);
        DressPiece(dressing, "Lamp_Housing", PrimitiveType.Sphere, space, new Vector3(0f, 0.09f, LampZ + 0.02f), new Vector3(0.13f, 0.1f, 0.08f), housing);
        DressPiece(dressing, "Lamp_Bulb", PrimitiveType.Sphere, space, new Vector3(0f, 0.12f, LampZ - 0.01f), new Vector3(0.07f, 0.07f, 0.07f), lamp);
        for (int i = 0; i < 5; i++)
        {
            float x = -0.05f + i * 0.025f;
            DressTube(dressing, "Lamp_Cage", space, new Vector3(x, 0.165f, LampZ - 0.05f), new Vector3(x, 0.165f, LampZ + 0.03f), 0.003f, housing);
        }
        DressTube(dressing, "Lamp_Cage", space, new Vector3(-0.06f, 0.165f, LampZ - 0.05f), new Vector3(0.06f, 0.165f, LampZ - 0.05f), 0.003f, housing);
    }

    static void SwapPlinkoMaterials(Transform board, Dictionary<string, Material> swaps)
    {
        foreach (Renderer renderer in board.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !swaps.TryGetValue(materials[i].name, out Material swap)) continue;
                materials[i] = swap;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // A box or ball centered at a board point, sized across, deep and tall.
    static GameObject DressPiece(Transform parent, string name, PrimitiveType type, BoardSpace space, Vector3 center, Vector3 size, Material material)
    {
        GameObject go = Primitive(type, name, parent, material);
        go.transform.position = space.Point(center.x, center.y, center.z);
        go.transform.rotation = Quaternion.LookRotation(space.Front, space.Up);
        go.transform.localScale = new Vector3(size.x, size.z, size.y) * (space.Scale / parent.lossyScale.x);
        return go;
    }

    // A flat picture facing the street. The picture's up runs along `up`.
    static GameObject DressQuad(Transform parent, string name, BoardSpace space, Vector3 center, float width, float height, Vector3 up, Material material)
    {
        GameObject go = Primitive(PrimitiveType.Quad, name, parent, material);
        go.transform.position = space.Point(center.x, center.y, center.z);
        go.transform.rotation = Quaternion.LookRotation(-space.Front, up);
        go.transform.localScale = new Vector3(width, height, 1f) * (space.Scale / parent.lossyScale.x);
        return go;
    }

    static GameObject DressTube(Transform parent, string name, BoardSpace space, Vector3 from, Vector3 to, float radius, Material material)
    {
        Vector3 a = space.Point(from.x, from.y, from.z);
        Vector3 b = space.Point(to.x, to.y, to.z);
        GameObject go = Primitive(PrimitiveType.Cylinder, name, parent, material);
        go.transform.position = (a + b) * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
        float unit = 1f / parent.lossyScale.x;
        go.transform.localScale = new Vector3(2f * radius * space.Scale * unit, (b - a).magnitude * 0.5f * unit, 2f * radius * space.Scale * unit);
        return go;
    }

    // Unlit, so a color above 1 reaches the bloom as it is: neon and bulbs.
    // Lit emission is not used here because batch runs save the glow
    // materials without their _EMISSION keyword.
    static Material BrightMaterial(string name, Color color)
    {
        string path = MaterialRoot + "/" + name + ".mat";
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(unlit) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = unlit;
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material TexturedMaterial(string name, Texture2D texture, Color tint, float metallic, float smoothness)
    {
        Material material = LitMaterial(MaterialRoot + "/" + name + ".mat", name, tint, metallic, smoothness);
        material.SetTexture("_BaseMap", texture);
        material.mainTexture = texture;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Paints a texture from a pixel function and keeps it as a PNG asset,
    // rewriting the file only when the picture changed.
    delegate Color PixelPainter(int x, int y, int width, int height);

    static Texture2D PaintedTexture(string name, int width, int height, PixelPainter paint)
    {
        return SaveTexture(name, width, height, Paint(width, height, paint));
    }

    static Color[] Paint(int width, int height, PixelPainter paint)
    {
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = paint(x, y, width, height);
        return pixels;
    }

    static Texture2D SaveTexture(string name, int width, int height, Color[] pixels)
    {
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.SetPixels(pixels);
        image.Apply();
        byte[] png = image.EncodeToPNG();
        Object.DestroyImmediate(image);
        string path = PlinkoTextureRoot + name + ".png";
        if (!File.Exists(path) || !SameBytes(File.ReadAllBytes(path), png))
        {
            Directory.CreateDirectory(PlinkoTextureRoot);
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static bool SameBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    static float Noise(float x, float y, int octaves)
    {
        float sum = 0f, amplitude = 0.5f, frequency = 1f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x * frequency + 31.7f * i, y * frequency + 17.3f * i) * amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        return sum / (1f - Mathf.Pow(0.5f, octaves));
    }

    static readonly Color RustColor = new Color(0.36f, 0.17f, 0.07f);

    // Dark steel plate: grime blotches, rust spots, streaks running down from
    // the rivets and pale scratches.
    static Color SteelPixels(int x, int y, int width, int height)
    {
        float u = (float)x / width, v = (float)y / height;
        Color c = new Color(0.36f, 0.38f, 0.42f);
        float grime = Noise(u * 5f, v * 5f, 4);
        c *= Mathf.Lerp(0.55f, 1.1f, grime);
        float streak = Noise(u * 48f, v * 1.4f, 2);
        c *= Mathf.Lerp(1f, 0.7f, Mathf.SmoothStep(0.55f, 0.8f, streak));
        float spots = Noise(u * 9f + 5f, v * 9f + 3f, 3);
        c = Color.Lerp(c, RustColor * Mathf.Lerp(0.7f, 1.3f, Noise(u * 40f, v * 40f, 2)), Mathf.SmoothStep(0.6f, 0.72f, spots) * 0.85f);
        float scratch = Mathf.Abs(Mathf.Sin((u * 1.3f + v * 0.35f + Noise(u * 3f, v * 3f, 2) * 0.4f) * 260f));
        if (scratch > 0.995f && Noise(u * 20f, v * 20f, 1) > 0.6f) c = Color.Lerp(c, new Color(0.62f, 0.64f, 0.66f), 0.6f);
        c.a = 1f;
        return c;
    }

    static Color RustPixels(int x, int y, int width, int height)
    {
        float u = (float)x / width, v = (float)y / height;
        float n = Noise(u * 7f, v * 7f, 5);
        Color c = Color.Lerp(new Color(0.2f, 0.11f, 0.06f), new Color(0.55f, 0.27f, 0.1f), n);
        float flake = Noise(u * 30f + 9f, v * 30f, 2);
        c = Color.Lerp(c, new Color(0.7f, 0.4f, 0.15f), Mathf.SmoothStep(0.65f, 0.75f, flake) * 0.6f);
        float bare = Noise(u * 4f + 2f, v * 4f + 7f, 3);
        c = Color.Lerp(c, new Color(0.3f, 0.3f, 0.31f), Mathf.SmoothStep(0.62f, 0.7f, bare) * 0.7f);
        c.a = 1f;
        return c;
    }

    // Outdoor plywood: vertical grain, a few knots, water stains rising from
    // the bottom edge.
    static Color PlywoodPixel(int x, int y, int width, int height, float stain)
    {
        float u = (float)x / width, v = (float)y / height;
        float warp = Noise(u * 3f, v * 0.8f, 3) * 6f;
        float grain = 0.5f + 0.5f * Mathf.Sin((u * 38f + warp) * Mathf.PI);
        Color c = Color.Lerp(new Color(0.5f, 0.37f, 0.24f), new Color(0.66f, 0.52f, 0.35f), grain * 0.6f + Noise(u * 12f, v * 2f, 2) * 0.4f);
        float knot = Noise(u * 6f + 40f, v * 6f, 2);
        c = Color.Lerp(c, new Color(0.3f, 0.2f, 0.12f), Mathf.SmoothStep(0.74f, 0.8f, knot));
        float wet = Mathf.Clamp01((0.35f + stain - v) * 2.5f) * Noise(u * 8f, v * 3f, 3);
        c = Color.Lerp(c, new Color(0.2f, 0.17f, 0.14f), wet * 0.8f);
        c *= Mathf.Lerp(0.7f, 1f, Noise(u * 4f + 11f, v * 4f, 3));
        c.a = 1f;
        return c;
    }

    static Color HazardPixels(int x, int y, int width, int height)
    {
        float stripe = Mathf.Repeat((x + y) / 48f, 1f);
        Color c = stripe < 0.5f ? new Color(0.95f, 0.72f, 0.08f) : new Color(0.05f, 0.05f, 0.05f);
        float u = (float)x / width, v = (float)y / height;
        float wear = Noise(u * 14f, v * 2f, 3);
        c = Color.Lerp(c, new Color(0.25f, 0.22f, 0.18f), Mathf.SmoothStep(0.62f, 0.72f, wear));
        c *= Mathf.Lerp(0.75f, 1f, Noise(u * 30f, v * 6f, 2));
        c.a = 1f;
        return c;
    }

    // Tags lifted from the vault facade's own graffiti, so the board is sprayed
    // by the same crew. Rectangles are in the facade texture's UVs.
    static readonly Rect TagsTop = new Rect(0.326f, 0.469f, 0.21f, 0.071f);
    static readonly Rect TagsBottom = new Rect(0.331f, 0.236f, 0.205f, 0.071f);
    static readonly Rect TagsSplat = new Rect(0.486f, 0.70f, 0.1f, 0.121f);

    static Texture2D LoadGraffiti()
    {
        var atlas = new Texture2D(2, 2);
        atlas.LoadImage(File.ReadAllBytes(GraffitiAtlas));
        return atlas;
    }

    // Lays the bright paint of a patch of the facade over a picture. The
    // facade's dark brick between the letters is left out.
    static void SprayTags(Color[] pixels, int width, int height, Texture2D atlas, Rect source, Rect target, float opacity)
    {
        int x0 = Mathf.FloorToInt(target.xMin * width), x1 = Mathf.CeilToInt(target.xMax * width);
        int y0 = Mathf.FloorToInt(target.yMin * height), y1 = Mathf.CeilToInt(target.yMax * height);
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(height, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(width, x1); x++)
            {
                float tu = ((float)x / width - target.xMin) / target.width;
                float tv = ((float)y / height - target.yMin) / target.height;
                Color paint = atlas.GetPixelBilinear(source.xMin + tu * source.width, source.yMin + tv * source.height);
                float bright = Mathf.Max(paint.r, Mathf.Max(paint.g, paint.b));
                float alpha = Mathf.SmoothStep(0.32f, 0.55f, bright) * opacity;
                int i = y * width + x;
                pixels[i] = Color.Lerp(pixels[i], paint, alpha);
            }
    }

    // The playfield: a darker steel plate in panels, sprayed over.
    static Texture2D ArcadeArt()
    {
        const int W = 768, H = 1024;
        Color[] pixels = Paint(W, H, (x, y, w, h) =>
        {
            Color c = SteelPixels(x, y, w, h) * 0.42f;
            if (y % 256 < 3 || x % 384 < 3) c *= 0.4f;
            c.a = 1f;
            return c;
        });
        Texture2D atlas = LoadGraffiti();
        SprayTags(pixels, W, H, atlas, TagsTop, new Rect(0.02f, 0.66f, 0.96f, 0.26f), 0.8f);
        SprayTags(pixels, W, H, atlas, TagsSplat, new Rect(0.55f, 0.36f, 0.4f, 0.3f), 0.7f);
        SprayTags(pixels, W, H, atlas, TagsBottom, new Rect(0.02f, 0.2f, 0.96f, 0.22f), 0.75f);
        Object.DestroyImmediate(atlas);
        return SaveTexture("T_PlinkoArcadeArt", W, H, pixels);
    }

    static Texture2D MarqueeArt()
    {
        const int W = 768, H = 160;
        Color[] pixels = Paint(W, H, (x, y, w, h) => { Color c = SteelPixels(x, y * 4, w, h * 4) * 0.35f; c.a = 1f; return c; });
        Texture2D atlas = LoadGraffiti();
        SprayTags(pixels, W, H, atlas, TagsTop, new Rect(0f, 0f, 1f, 1f), 1f);
        Object.DestroyImmediate(atlas);
        return SaveTexture("T_PlinkoArcadeMarquee", W, H, pixels);
    }

    // The playfield: a sheet of plywood, tagged and stained.
    static Texture2D ScrapArt()
    {
        const int W = 768, H = 1024;
        Color[] pixels = Paint(W, H, (x, y, w, h) => PlywoodPixel(x, y, w, h, 0.1f) * 0.85f);
        Texture2D atlas = LoadGraffiti();
        SprayTags(pixels, W, H, atlas, TagsBottom, new Rect(0.02f, 0.62f, 0.96f, 0.28f), 0.9f);
        SprayTags(pixels, W, H, atlas, TagsSplat, new Rect(0.05f, 0.3f, 0.42f, 0.3f), 0.85f);
        Object.DestroyImmediate(atlas);
        return SaveTexture("T_PlinkoScrapArt", W, H, pixels);
    }
}
