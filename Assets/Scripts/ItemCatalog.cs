using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class ItemCatalog
{
    public enum Theme
    {
        Heist = 0,
        Climber = 1
    }

    public static Theme Current = Theme.Heist;

    static readonly List<Vector3> verts = new List<Vector3>(256);
    static readonly List<Vector3> normals = new List<Vector3>(256);
    static readonly List<int> tris = new List<int>(512);
    static bool built;

    public const float VisualScale = 2.25f;
    public const float HdrBoost = 4.2f;

    public static readonly Color CrimsonNeon = new Color(1f, 0f, 85f / 255f, 1f);
    public static readonly Color IndigoNeon = new Color(0f, 136f / 255f, 1f, 1f);
    public static readonly Color GoldNeon = new Color(1f, 221f / 255f, 0f, 1f);
    public static readonly Color ScarletNeon = new Color(1f, 0.42f, 0.08f, 1f);
    public static readonly Color SkyNeon = new Color(0.22f, 0.74f, 0.92f, 1f);
    public static readonly Color Charcoal = new Color(0.15f, 0.14f, 0.13f, 1f);

    static Mesh tire, fuelCan, piton, carabiner, rope, rock;
    static Mesh thief, climber, wall, car, vatShell, liquid, shard;
    static Mesh halo, warningCore, ruby, sapphire, topaz, lootBag, chest, chestLid;
    static Mesh heroGem, heroCoin;
    static Material gemStone, coinMetal;
    static Sprite[] heroGems;
    static Sprite heroCoinSprite;

    public static Material GemStoneMaterial { get { Ensure(); return gemStone; } }
    public static Material CoinMetalMaterial { get { Ensure(); return coinMetal; } }
    public static readonly Color CoinBody = new Color(0.96f, 0.76f, 0.22f, 1f);

    public static Sprite HeroSprite(SortNode.NodeKind kind, int colorId)
    {
        Ensure();
        Sprite painted = ThemePiece(kind, colorId);
        if (painted != null) return painted;
        return null;
    }

    static Sprite ThemePiece(SortNode.NodeKind kind, int colorId)
    {
        if (kind == SortNode.NodeKind.Bomb) return null;
        ThemeConfig theme = GameCoreManager.Instance != null ? GameCoreManager.Instance.ActiveTheme : null;
        Sprite[] pieces = theme != null ? theme.collectibleSprites : null;
        if (pieces == null || pieces.Length == 0) return null;
        if (kind == SortNode.NodeKind.Gold) return pieces[pieces.Length - 1];
        if (kind != SortNode.NodeKind.Gem && kind != SortNode.NodeKind.Letter) return null;
        int lastGem = pieces.Length > 1 ? pieces.Length - 2 : 0;
        int index = kind == SortNode.NodeKind.Letter ? lastGem : Mathf.Clamp(colorId, 0, lastGem);
        return pieces[index];
    }

    public static Mesh MeshFor(Theme theme, SortNode.NodeKind kind, int colorId)
    {
        Ensure();
        if (kind == SortNode.NodeKind.Letter) return heroGem != null ? heroGem : topaz;
        if (kind == SortNode.NodeKind.Bomb) return theme == Theme.Heist ? fuelCan : rock;
        if (kind == SortNode.NodeKind.Gold)
            return theme == Theme.Heist && heroCoin != null ? heroCoin : theme == Theme.Heist ? topaz : carabiner;
        if (theme == Theme.Heist)
        {
            if (heroGem != null) return heroGem;
            if (colorId == 0) return ruby;
            if (colorId == 1) return sapphire;
            if (colorId == 2) return tire;
            return fuelCan;
        }

        if (colorId == 0) return piton;
        if (colorId == 1) return carabiner;
        if (colorId == 2) return rope;
        return rock;
    }

    public static Mesh Avatar(Theme theme)
    {
        Ensure();
        return theme == Theme.Climber ? climber : thief;
    }

    public static Mesh WallMesh { get { Ensure(); return wall; } }
    public static Material WallMaterial { get; private set; }
    public static Material[] WallMaterials { get; private set; }
    public static Mesh CarMesh { get { Ensure(); return car; } }
    public static Mesh VatMesh { get { Ensure(); return vatShell; } }
    public static Mesh LiquidMesh { get { Ensure(); return liquid; } }
    public static Mesh ShardMesh { get { Ensure(); return shard; } }
    public static Mesh HaloMesh { get { Ensure(); return halo; } }
    public static Mesh CoreMesh { get { Ensure(); return warningCore; } }
    public static Mesh BagMesh { get { Ensure(); return lootBag; } }
    public static Mesh ChestMesh { get { Ensure(); return chest; } }
    public static Mesh LidMesh { get { Ensure(); return chestLid; } }

    public static Color TintFor(SortNode.NodeKind kind, int colorId, string meshName)
    {
        if (kind == SortNode.NodeKind.Letter) return new Color(1f, 0.86f, 0.42f);
        if (kind == SortNode.NodeKind.Bomb) return new Color(0.22f, 0.08f, 0.08f);
        if (meshName == "GoldBar") return new Color(0.96f, 0.76f, 0.22f);
        if (meshName == "DiamondCluster") return new Color(0.72f, 0.94f, 1f);
        if (meshName == "Tire") return new Color(0.1f, 0.1f, 0.12f);
        if (meshName == "FuelCan") return new Color(0.78f, 0.14f, 0.16f);
        if (meshName == "Piton") return new Color(0.72f, 0.74f, 0.78f);
        if (meshName == "Carabiner") return kind == SortNode.NodeKind.Gold
            ? new Color(0.96f, 0.76f, 0.22f)
            : new Color(0.82f, 0.84f, 0.88f);
        if (meshName == "Rope") return new Color(0.48f, 0.34f, 0.18f);
        if (meshName == "Rock") return new Color(0.38f, 0.36f, 0.34f);
        if (colorId == 1) return new Color(0.35f, 0.32f, 0.85f);
        return new Color(0.85f, 0.16f, 0.2f);
    }

    public static void Spin(Transform target)
    {
        if (target == null) return;
        target.Rotate(15f * Time.deltaTime, 60f * Time.deltaTime, 0f);
    }

    public static Color Neon(SortNode.NodeKind kind, int colorId)
    {
        if (kind == SortNode.NodeKind.Letter || kind == SortNode.NodeKind.Gold)
            return GoldNeon;
        if (kind == SortNode.NodeKind.Bomb)
            return CrimsonNeon;
        if (colorId == 0) return CrimsonNeon;
        if (colorId == 1) return IndigoNeon;
        if (colorId == 2) return ScarletNeon;
        return SkyNeon;
    }

    public static Color DestinationBase(SortNode.NodeKind kind, int colorId)
    {
        if (kind == SortNode.NodeKind.Bomb) return Charcoal;
        return Neon(kind, colorId);
    }

    public static Color DestinationGlow(SortNode.NodeKind kind, int colorId)
    {
        if (kind == SortNode.NodeKind.Bomb) return Charcoal * 0.35f;
        float boost = HdrBoost;
        if (kind == SortNode.NodeKind.Gem && colorId >= 2) boost = 2.2f;
        return Neon(kind, colorId) * boost;
    }

    public static Color GemBody(int colorId)
    {
        if (colorId == 0) return new Color(0.86f, 0.04f, 0.09f, 1f);
        if (colorId == 1) return new Color(0.10f, 0.28f, 0.92f, 1f);
        if (colorId == 2) return new Color(0.95f, 0.32f, 0.06f, 1f);
        return new Color(0.25f, 0.72f, 0.98f, 1f);
    }

    public static Color TrailTint(SortNode.NodeKind kind, int colorId)
    {
        if (kind == SortNode.NodeKind.Gem) return GemBody(colorId);
        if (kind == SortNode.NodeKind.Letter) return new Color(0.95f, 0.78f, 0.32f, 1f);
        if (kind == SortNode.NodeKind.Gold) return CoinBody;
        Color color = Neon(kind, colorId);
        if (kind == SortNode.NodeKind.Bomb) color = CrimsonNeon * 0.75f;
        color.a = 1f;
        return color;
    }

    public static bool ShowHalo(SortNode.NodeKind kind)
    {
        return false;
    }

    public static bool PresentHero(Renderer renderer, MaterialPropertyBlock block, SortNode.NodeKind kind, int colorId, Theme theme)
    {
        Ensure();
        if (renderer == null || block == null) return false;
        bool letter = kind == SortNode.NodeKind.Letter;
        bool gem = kind == SortNode.NodeKind.Gem || letter;
        bool coin = kind == SortNode.NodeKind.Gold && theme == Theme.Heist && heroCoin != null && coinMetal != null;
        if (gem && heroGem != null && gemStone != null)
        {
            renderer.sharedMaterial = gemStone;
            PaintGem(renderer, block, colorId, letter);
            return true;
        }

        if (coin)
        {
            renderer.sharedMaterial = coinMetal;
            PaintCoin(renderer, block);
            return true;
        }

        return false;
    }

    public static void PaintGem(Renderer renderer, MaterialPropertyBlock block, int colorId, bool letter)
    {
        if (renderer == null || block == null) return;
        Color body = letter ? new Color(0.95f, 0.78f, 0.32f, 1f) : GemBody(colorId);
        block.Clear();
        block.SetColor("_BaseColor", body);
        block.SetColor("_Color", body);
        block.SetColor("_EmissionColor", body * 0.08f);
        block.SetFloat("_Metal", 0f);
        renderer.SetPropertyBlock(block);
    }

    public static void PaintCoin(Renderer renderer, MaterialPropertyBlock block)
    {
        if (renderer == null || block == null) return;
        block.Clear();
        block.SetColor("_BaseColor", CoinBody);
        block.SetColor("_Color", CoinBody);
        block.SetColor("_EmissionColor", CoinBody * 0.06f);
        block.SetFloat("_Metal", 1f);
        renderer.SetPropertyBlock(block);
    }

    public static float BombWave()
    {
        return 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
    }

    public static Color CoreColor(float wave)
    {
        return Color.Lerp(CrimsonNeon * 0.25f, CrimsonNeon * HdrBoost, Mathf.Clamp01(wave));
    }

    public static void BoostScale(Transform target)
    {
        if (target == null) return;
        target.localScale *= VisualScale;
    }

    public static void SeatHalo(Transform haloTransform, Transform host)
    {
        if (haloTransform == null || host == null) return;
        haloTransform.gameObject.SetActive(true);
        haloTransform.position = host.position + new Vector3(0f, -0.03f, 0.16f);
        haloTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
        haloTransform.localScale = new Vector3(1.32f, 1.32f, 1.32f);
    }

    public static void PrepareLit(Material material, Shader shader)
    {
        if (material == null) return;
        material.hideFlags = HideFlags.HideAndDontSave;
        material.EnableKeyword("_EMISSION");
        if (shader != null)
        {
            LocalKeyword keyword = new LocalKeyword(shader, "_EMISSION");
            if (keyword.isValid) material.SetKeyword(keyword, true);
        }

        if (material.HasProperty("_EmissionMap"))
            material.SetTexture("_EmissionMap", Texture2D.whiteTexture);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.white);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.72f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.45f);
    }

    public static Material CreateUnlit()
    {
        Shader shader = Shader.Find("Juicy/GemFacet");
        bool gem = shader != null;
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        bool unlit = shader != null && !gem;
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        var material = new Material(shader);
        if (gem || unlit) PrepareUnlit(material);
        else PrepareLit(material, shader);
        return material;
    }

    public static void PrepareUnlit(Material material)
    {
        if (material == null) return;
        material.hideFlags = HideFlags.HideAndDontSave;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
    }

    public static void ApplyShine(Renderer renderer, MaterialPropertyBlock block, SortNode.NodeKind kind, int colorId)
    {
        if (renderer == null || block == null) return;
        Color emission = DestinationGlow(kind, colorId);
        Color color = kind == SortNode.NodeKind.Bomb ? Charcoal : emission;
        Paint(renderer, block, color, emission);
    }

    public static void ApplyHalo(Renderer renderer, MaterialPropertyBlock block, SortNode.NodeKind kind, int colorId)
    {
        if (renderer == null || block == null) return;
        float boost = kind == SortNode.NodeKind.Bomb ? 1.8f : 2.6f;
        if (kind == SortNode.NodeKind.Gem && colorId >= 2) boost = 1.5f;
        Color color = Neon(kind, colorId) * boost;
        color.a = 1f;
        Paint(renderer, block, color, color);
    }

    public static void ApplyCore(Renderer renderer, MaterialPropertyBlock block, Color color)
    {
        if (renderer == null || block == null) return;
        color.a = 1f;
        Paint(renderer, block, color, color);
    }

    static void Paint(Renderer renderer, MaterialPropertyBlock block, Color color, Color emission)
    {
        block.Clear();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", emission);
        renderer.SetPropertyBlock(block);
    }

    public static void PaintTrail(TrailRenderer trail, SortNode.NodeKind kind, int colorId)
    {
        if (trail == null) return;
        Color color = TrailTint(kind, colorId);
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;
        trail.Clear();
        trail.emitting = true;
    }

    static void Ensure()
    {
        if (built) return;
        built = true;
        tire = Torus("Tire", 0.42f, 0.16f, 18, 8, false);
        fuelCan = Can();
        piton = Piton();
        carabiner = Torus("Carabiner", 0.4f, 0.07f, 20, 6, true);
        rope = Torus("Rope", 0.34f, 0.1f, 16, 6, false);
        rock = Rock();
        thief = MaskedThief();
        climber = Person("Climber", true);
        wall = LoadVaultDoor();
        ruby = GemCut("RubyGem");
        sapphire = GemCut("SapphireGem");
        topaz = GemCut("TopazGem");
        heroGem = LoadHeroMesh("Heroes/SM_Gem_01");
        heroCoin = LoadHeroMesh("Heroes/SM_CoinStack_01");
        gemStone = CreateGemStone();
        coinMetal = CreateCoinMetal();
        heroGems = new[]
        {
            Resources.Load<Sprite>("UIHeroes/gem_ruby"),
            Resources.Load<Sprite>("UIHeroes/gem_sapphire"),
            Resources.Load<Sprite>("UIHeroes/gem_emerald"),
            Resources.Load<Sprite>("UIHeroes/gem_topaz")
        };
        heroCoinSprite = Resources.Load<Sprite>("UIHeroes/coins_hero");
        lootBag = Box("LootBag", Vector3.zero, new Vector3(0.24f, 0.28f, 0.18f));
        chest = Box("TreasureChest", new Vector3(0f, -0.06f, 0f), new Vector3(0.72f, 0.4f, 0.5f));
        chestLid = Box("ChestLid", new Vector3(0f, 0.1f, -0.22f), new Vector3(0.76f, 0.14f, 0.52f));
        car = Car();
        vatShell = Vat();
        liquid = Box("Liquid", Vector3.zero, Vector3.one);
        shard = Box("Shard", Vector3.zero, new Vector3(0.22f, 0.08f, 0.16f));
        halo = Torus("Halo", 0.62f, 0.055f, 22, 6, false);
        warningCore = Box("WarningCore", Vector3.zero, new Vector3(0.28f, 0.28f, 0.28f));
    }

    static Mesh Box(string name, Vector3 center, Vector3 size)
    {
        Clear();
        AddBox(center, size);
        return Bake(name);
    }

    static Mesh Can()
    {
        Clear();
        AddBox(Vector3.zero, new Vector3(0.4f, 0.68f, 0.26f));
        AddBox(new Vector3(0f, 0.4f, 0f), new Vector3(0.16f, 0.14f, 0.16f));
        return Bake("FuelCan");
    }

    static Mesh Piton()
    {
        Clear();
        AddBox(new Vector3(0f, 0f, 0.05f), new Vector3(0.12f, 0.12f, 0.72f));
        AddBox(new Vector3(0f, 0.1f, -0.28f), new Vector3(0.28f, 0.08f, 0.14f));
        return Bake("Piton");
    }

    static Mesh Rock()
    {
        Clear();
        AddBox(Vector3.zero, new Vector3(0.7f, 0.42f, 0.55f));
        AddBox(new Vector3(0.22f, 0.12f, 0.04f), new Vector3(0.36f, 0.28f, 0.3f));
        AddBox(new Vector3(-0.2f, 0.04f, 0.12f), new Vector3(0.3f, 0.34f, 0.28f));
        return Bake("Rock");
    }

    static Mesh MaskedThief()
    {
        Clear();
        AddBox(new Vector3(0f, 0.08f, 0f), new Vector3(0.34f, 0.44f, 0.18f));
        AddBox(new Vector3(0f, 0.48f, 0.02f), new Vector3(0.22f, 0.22f, 0.2f));
        AddBox(new Vector3(0f, 0.5f, 0.12f), new Vector3(0.26f, 0.08f, 0.06f));
        AddBox(new Vector3(-0.1f, -0.32f, 0f), new Vector3(0.12f, 0.36f, 0.14f));
        AddBox(new Vector3(0.1f, -0.32f, 0f), new Vector3(0.12f, 0.36f, 0.14f));
        AddBox(new Vector3(0f, 0.1f, -0.14f), new Vector3(0.48f, 0.62f, 0.06f));
        AddBox(new Vector3(0.02f, 0.22f, 0.16f), new Vector3(0.16f, 0.06f, 0.06f));
        return Bake("MaskedThief");
    }

    static Mesh LoadNamedHero(string path)
    {
        GameObject model = Resources.Load<GameObject>(path);
        if (model == null) return null;
        MeshFilter filter = model.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return null;
        MeshRenderer renderer = model.GetComponentInChildren<MeshRenderer>();
        Material[] built = BuildVaultMaterials(renderer);
        if (built == null || built.Length == 0) return null;
        WallMaterials = built;
        WallMaterial = built[0];
        return filter.sharedMesh;
    }

    static Mesh LoadHeroMesh(string path)
    {
        GameObject model = Resources.Load<GameObject>(path);
        if (model == null) return null;
        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] != null && filters[i].sharedMesh != null)
                return filters[i].sharedMesh;
        }

        return null;
    }

    static Material CreateGemStone()
    {
        Shader shader = Shader.Find("Juicy/GemStone");
        if (shader == null) return null;
        var material = new Material(shader);
        material.name = "M_GemStone";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.SetFloat("_Metal", 0f);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_EmissionColor", Color.black);
        return material;
    }

    static Material CreateCoinMetal()
    {
        Shader shader = Shader.Find("Juicy/GemStone");
        if (shader == null) return null;
        var material = new Material(shader);
        material.name = "M_CoinMetal";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.SetFloat("_Metal", 1f);
        material.SetColor("_BaseColor", CoinBody);
        material.SetColor("_EmissionColor", CoinBody * 0.06f);
        return material;
    }

    static Mesh LoadVaultDoor()
    {
        WallMaterial = null;
        WallMaterials = null;
        Mesh stairs = LoadNamedHero("Heroes/SM_HeistStairs");
        if (stairs != null) return stairs;
        GameObject model = Resources.Load<GameObject>("Heroes/SM_VaultDoor_01");
        if (model == null) return VaultDoor();
        MeshFilter filter = model.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return VaultDoor();
        Mesh mesh = filter.sharedMesh;
        Texture2D paint = Resources.Load<Texture2D>("Heroes/T_VaultDoor_Paint");
        if (paint != null)
        {
            Texture2D glow = Resources.Load<Texture2D>("Heroes/T_VaultDoor_Glow");
            Material painted = VaultPaintMaterial(paint, glow);
            int count = Mathf.Max(1, mesh.subMeshCount);
            var slots = new Material[count];
            for (int i = 0; i < count; i++) slots[i] = painted;
            WallMaterials = slots;
            WallMaterial = painted;
            return mesh;
        }

        MeshRenderer renderer = model.GetComponentInChildren<MeshRenderer>();
        Material[] built = BuildVaultMaterials(renderer);
        if (built == null || built.Length == 0) return VaultDoor();
        WallMaterials = built;
        WallMaterial = built[0];
        return mesh;
    }

    static Material VaultPaintMaterial(Texture2D paint, Texture2D glow)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        var material = new Material(shader);
        material.name = "M_VaultPaint";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.SetTexture("_BaseMap", paint);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        Texture2D metal = Resources.Load<Texture2D>("Heroes/T_VaultDoor_Metal");
        if (metal != null)
        {
            material.SetTexture("_MetallicGlossMap", metal);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
        }
        else
        {
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 0.82f);
        }
        if (glow == null) return material;
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", glow);
        material.SetColor("_EmissionColor", new Color(0f, 1f, 0.4f) * 16f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return material;
    }

    // Blender join order on SM_VaultDoor_01. Used when the importer drops the names.
    static readonly string[] VaultSlotOrder = { "M_Dark", "M_Plate", "M_Gold", "M_Steel", "M_Lock" };

    static Material[] BuildVaultMaterials(MeshRenderer renderer)
    {
        Material[] source = renderer != null ? renderer.sharedMaterials : null;
        int count = source != null && source.Length > 0 ? source.Length : 1;
        var built = new Material[count];
        for (int i = 0; i < count; i++)
        {
            string name = source != null && i < source.Length && source[i] != null ? source[i].name : "";
            if (!KnownVaultSlot(name) && count == VaultSlotOrder.Length)
                name = VaultSlotOrder[i];
            built[i] = VaultSlot(name);
        }

        return built;
    }

    static bool KnownVaultSlot(string name)
    {
        string slot = name.ToLowerInvariant();
        return slot.Contains("lock") || slot.Contains("gold") || slot.Contains("dark")
            || slot.Contains("plate") || slot.Contains("steel") || slot.Contains("gem");
    }

    static Material VaultSlot(string name)
    {
        string slot = name.ToLowerInvariant();
        if (slot.Contains("gem")) return VaultLit("M_Gem", new Color(0.42f, 0.05f, 0.85f), 0.05f, 0.25f, new Color(0.55f, 0.08f, 1f) * 4f);
        if (slot.Contains("lock")) return VaultLit("M_Lock", new Color(0f, 1f, 0.4f), 0.15f, 0.92f, new Color(0f, 1f, 0.4f) * 15f);
        if (slot.Contains("gold")) return VaultLit("M_Gold", new Color(1f, 0.843f, 0.15f), 1f, 0.9f, Color.black);
        if (slot.Contains("dark")) return VaultLit("M_Dark", new Color(0.16f, 0.18f, 0.22f), 0.08f, 0.42f, Color.black);
        if (slot.Contains("plate")) return VaultLit("M_Plate", new Color(0.28f, 0.32f, 0.38f), 0.08f, 0.42f, Color.black);
        return VaultLit("M_Steel", new Color(0.62f, 0.68f, 0.74f), 0.08f, 0.42f, Color.black);
    }

    static Material VaultLit(string name, Color color, float metallic, float smoothness, Color emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        var material = new Material(shader);
        material.name = name;
        material.hideFlags = HideFlags.HideAndDontSave;
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent <= 0f) return material;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return material;
    }

    static Mesh VaultDoor()
    {
        Clear();
        AddBox(Vector3.zero, new Vector3(0.98f, 1.22f, 0.16f));
        AddBox(new Vector3(0f, -0.04f, 0.08f), new Vector3(0.62f, 0.9f, 0.1f));
        AddBox(new Vector3(0f, 0.02f, 0.16f), new Vector3(0.18f, 0.18f, 0.08f));
        AddBox(new Vector3(0f, 0.46f, 0.12f), new Vector3(0.22f, 0.08f, 0.08f));
        return Bake("VaultDoor");
    }

    static Mesh GemCut(string name)
    {
        Clear();
        const int sides = 10;
        const float tableR = 0.2f;
        const float crownR = 0.42f;
        const float girdleR = 0.52f;
        const float pavilionR = 0.3f;
        const float tableY = 0.22f;
        const float crownY = 0.1f;
        const float girdleY = 0f;
        const float pavilionY = -0.2f;
        const float culetY = -0.46f;
        Vector3 table = new Vector3(0f, tableY, 0f);
        Vector3 culet = new Vector3(0f, culetY, 0f);
        float step = Mathf.PI * 2f / sides;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * step;
            float a1 = a0 + step;
            float mid = a0 + step * 0.5f;
            Vector3 t0 = GemRing(a0, tableR, tableY);
            Vector3 t1 = GemRing(a1, tableR, tableY);
            Vector3 c0 = GemRing(a0, crownR, crownY);
            Vector3 c1 = GemRing(a1, crownR, crownY);
            Vector3 g0 = GemRing(mid, girdleR, girdleY);
            Vector3 g1 = GemRing(mid + step, girdleR, girdleY);
            Vector3 p0 = GemRing(a0, pavilionR, pavilionY);
            Vector3 p1 = GemRing(a1, pavilionR, pavilionY);
            AddTri(table, t0, t1);
            AddTri(t0, c0, t1);
            AddTri(t1, c0, c1);
            AddTri(c0, g0, c1);
            AddTri(c1, g0, g1);
            AddTri(g0, p0, g1);
            AddTri(g1, p0, p1);
            AddTri(p0, culet, p1);
        }

        return Bake(name);
    }

    static Vector3 GemRing(float angle, float radius, float y)
    {
        return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
    }

    static Mesh Person(string name, bool pack)
    {
        Clear();
        AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.36f, 0.46f, 0.2f));
        AddBox(new Vector3(0f, 0.5f, 0f), new Vector3(0.24f, 0.24f, 0.22f));
        AddBox(new Vector3(-0.12f, -0.32f, 0f), new Vector3(0.12f, 0.4f, 0.14f));
        AddBox(new Vector3(0.12f, -0.32f, 0f), new Vector3(0.12f, 0.4f, 0.14f));
        AddBox(new Vector3(0f, 0.16f, -0.12f), new Vector3(0.4f, 0.5f, 0.06f));
        if (pack) AddBox(new Vector3(0f, 0.18f, -0.22f), new Vector3(0.28f, 0.32f, 0.16f));
        return Bake(name);
    }

    static Mesh Car()
    {
        Clear();
        AddBox(new Vector3(0f, 0.08f, 0f), new Vector3(1.25f, 0.26f, 0.52f));
        AddBox(new Vector3(-0.08f, 0.3f, 0f), new Vector3(0.52f, 0.22f, 0.46f));
        AddBox(new Vector3(-0.38f, -0.08f, 0.22f), new Vector3(0.22f, 0.22f, 0.12f));
        AddBox(new Vector3(0.38f, -0.08f, 0.22f), new Vector3(0.22f, 0.22f, 0.12f));
        AddBox(new Vector3(-0.38f, -0.08f, -0.22f), new Vector3(0.22f, 0.22f, 0.12f));
        AddBox(new Vector3(0.38f, -0.08f, -0.22f), new Vector3(0.22f, 0.22f, 0.12f));
        return Bake("GetawayCar");
    }

    static Mesh Vat()
    {
        Clear();
        AddBox(new Vector3(0f, -0.48f, 0f), new Vector3(0.9f, 0.08f, 0.9f));
        AddBox(new Vector3(0f, 0f, 0.42f), new Vector3(0.9f, 0.9f, 0.08f));
        AddBox(new Vector3(0f, 0f, -0.42f), new Vector3(0.9f, 0.9f, 0.08f));
        AddBox(new Vector3(0.42f, 0f, 0f), new Vector3(0.08f, 0.9f, 0.78f));
        AddBox(new Vector3(-0.42f, 0f, 0f), new Vector3(0.08f, 0.9f, 0.78f));
        return Bake("VatShell");
    }

    static Mesh Torus(string name, float radius, float tube, int segments, int sides, bool gap)
    {
        Clear();
        int ring = sides + 1;
        int baseVert = 0;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 center = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (int j = 0; j <= sides; j++)
            {
                float around = j * Mathf.PI * 2f / sides;
                Vector3 normal = outward * Mathf.Cos(around) + Vector3.up * Mathf.Sin(around);
                verts.Add(center + normal * tube);
                normals.Add(normal);
            }
        }

        for (int i = 0; i < segments; i++)
        {
            if (gap && i > segments - 4) continue;
            for (int j = 0; j < sides; j++)
            {
                int a = baseVert + i * ring + j;
                int b = a + ring;
                tris.Add(a);
                tris.Add(b);
                tris.Add(a + 1);
                tris.Add(a + 1);
                tris.Add(b);
                tris.Add(b + 1);
            }
        }

        return Bake(name);
    }

    static void Clear()
    {
        verts.Clear();
        normals.Clear();
        tris.Clear();
    }

    static void AddBox(Vector3 center, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        Vector3 p0 = center + new Vector3(-h.x, -h.y, -h.z);
        Vector3 p1 = center + new Vector3(h.x, -h.y, -h.z);
        Vector3 p2 = center + new Vector3(h.x, -h.y, h.z);
        Vector3 p3 = center + new Vector3(-h.x, -h.y, h.z);
        Vector3 p4 = center + new Vector3(-h.x, h.y, -h.z);
        Vector3 p5 = center + new Vector3(h.x, h.y, -h.z);
        Vector3 p6 = center + new Vector3(h.x, h.y, h.z);
        Vector3 p7 = center + new Vector3(-h.x, h.y, h.z);
        AddFace(p0, p1, p2, p3);
        AddFace(p4, p7, p6, p5);
        AddFace(p0, p4, p5, p1);
        AddFace(p3, p2, p6, p7);
        AddFace(p0, p3, p7, p4);
        AddFace(p1, p5, p6, p2);
    }

    static void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int index = verts.Count;
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);
        verts.Add(d);
        Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
        if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        tris.Add(index);
        tris.Add(index + 1);
        tris.Add(index + 2);
        tris.Add(index);
        tris.Add(index + 2);
        tris.Add(index + 3);
    }

    static void AddTri(Vector3 a, Vector3 b, Vector3 c)
    {
        int index = verts.Count;
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        tris.Add(index);
        tris.Add(index + 1);
        tris.Add(index + 2);
    }

    static Mesh Bake(string name)
    {
        var mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.hideFlags = HideFlags.HideAndDontSave;
        return mesh;
    }
}

public static class WorldFollow
{
    public static Vector3 UiToWorld(Vector3 screenPoint)
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        float depth = Mathf.Max(0.5f, Mathf.Abs(cam.transform.position.z));
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
        world.z = 0f;
        return world;
    }

    public static Vector3 UiToWorld(RectTransform rect)
    {
        if (rect == null) return Vector3.zero;
        return UiToWorld(rect.position);
    }

    public static float SizeFor(RectTransform rect)
    {
        Camera cam = Camera.main;
        if (cam == null || rect == null) return 0.4f;
        float depth = Mathf.Max(0.5f, Mathf.Abs(cam.transform.position.z));
        float worldHeight = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float screen = Screen.height > 8 ? Screen.height : 1920f;
        float pixels = rect.rect.height * Mathf.Abs(rect.lossyScale.y);
        return Mathf.Clamp(pixels / screen * worldHeight, 0.16f, 1.6f);
    }

    public static void Place(Transform target, RectTransform rect)
    {
        if (target == null || rect == null) return;
        target.position = UiToWorld(rect);
        float size = SizeFor(rect);
        target.localScale = new Vector3(size, size, size);
    }
}
