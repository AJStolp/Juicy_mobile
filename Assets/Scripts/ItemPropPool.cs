using UnityEngine;

public class ItemPropPool : MonoBehaviour
{
    public static ItemPropPool Instance { get; private set; }

    const int Pool = 16;

    class Prop
    {
        public GameObject Go;
        public Transform Transform;
        public MeshFilter Filter;
        public MeshRenderer Renderer;
        public TrailRenderer Trail;
        public ParticleSystem Sparkle;
        public Transform Halo;
        public MeshRenderer HaloRenderer;
        public Transform Core;
        public MeshRenderer CoreRenderer;
        public bool Alive;
        public string MeshName;
        public SortNode.NodeKind Kind;
        public int ColorId;
    }

    readonly Prop[] props = new Prop[Pool];
    Material material;
    MaterialPropertyBlock block;
    bool ready;

    public string LastMeshName { get; private set; } = "";
    public Color LastEmission { get; private set; }
    public Color LastTrail { get; private set; }
    public Color LastBody { get; private set; }
    public Color LastCore { get; private set; }
    public float LastScaleBoost { get; private set; }
    public float CorePeak { get; private set; }
    public bool LastHalo { get; private set; }
    public bool LastSparkle { get; private set; }
    public Transform LastTransform { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Ensure();
    }

    void OnEnable()
    {
        WatchTheme();
    }

    void Start()
    {
        WatchTheme();
    }

    void OnDisable()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnThemeChanged -= HandleTheme;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void WatchTheme()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnThemeChanged -= HandleTheme;
        GameCoreManager.Instance.OnThemeChanged += HandleTheme;
    }

    void HandleTheme(ThemeConfig theme)
    {
        if (theme == null) return;
        SortNode[] nodes = FindObjectsByType<SortNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] != null && nodes[i].gameObject.activeInHierarchy)
                nodes[i].ApplyCatalogLook();
        }
    }

    public void Attach(SortNode node)
    {
        if (node == null) return;
        Ensure();
        Release(node);
        if (ItemCatalog.HeroSprite(node.Kind, node.ColorId) != null)
        {
            node.HideSprite = false;
            node.PropSlot = -1;
            LastMeshName = node.Kind == SortNode.NodeKind.Gold ? "coins_hero" : "gem_hero";
            if (node.Kind == SortNode.NodeKind.Letter)
                node.ShowLetter(node.Letter, WordVaultManager.SharedFont);
            return;
        }

        int slot = Rent();
        node.PropSlot = slot;
        if (slot < 0) return;

        Mesh mesh = ItemCatalog.MeshFor(ItemCatalog.Current, node.Kind, node.ColorId);
        Prop prop = props[slot];
        prop.Filter.sharedMesh = mesh;
        prop.Alive = true;
        prop.MeshName = mesh != null ? mesh.name : "";
        prop.Kind = node.Kind;
        prop.ColorId = node.ColorId;
        prop.Go.SetActive(true);
        prop.Transform.localRotation = Quaternion.identity;
        LastMeshName = prop.MeshName;
        LastTransform = prop.Transform;
        node.HideSprite = true;
        if (node.Kind == SortNode.NodeKind.Letter)
            node.ShowLetter(node.Letter, WordVaultManager.SharedFont);
        Tint(prop, node.Kind, node.ColorId);
        Follow(prop, node.Rect);
        if (prop.Sparkle != null)
        {
            prop.Sparkle.Clear();
            prop.Sparkle.Play();
            LastSparkle = prop.Sparkle.isPlaying;
        }
        if (prop.Trail != null) ItemCatalog.PaintTrail(prop.Trail, node.Kind, node.ColorId);
    }

    public void Sync(SortNode node)
    {
        if (node == null || node.PropSlot < 0 || node.PropSlot >= props.Length) return;
        Prop prop = props[node.PropSlot];
        if (prop == null || !prop.Alive) return;
        Follow(prop, node.Rect);
    }

    public void Release(SortNode node)
    {
        if (node == null) return;
        int slot = node.PropSlot;
        node.PropSlot = -1;
        node.HideSprite = false;
        node.ShowLetter('\0', null);
        if (slot < 0 || slot >= props.Length || props[slot] == null) return;
        props[slot].Alive = false;
        if (props[slot].Trail != null)
        {
            props[slot].Trail.emitting = false;
            props[slot].Trail.Clear();
        }

        if (props[slot].Sparkle != null)
            props[slot].Sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (props[slot].Go != null) props[slot].Go.SetActive(false);
    }

    void Tint(Prop prop, SortNode.NodeKind kind, int colorId)
    {
        if (prop.Renderer == null || block == null) return;
        LastTrail = ItemCatalog.TrailTint(kind, colorId);
        if (!ItemCatalog.PresentHero(prop.Renderer, block, kind, colorId, ItemCatalog.Current))
        {
            if (material != null) prop.Renderer.sharedMaterial = material;
            LastEmission = ItemCatalog.DestinationGlow(kind, colorId);
            ItemCatalog.ApplyShine(prop.Renderer, block, kind, colorId);
        }
        else
        {
            LastEmission = kind == SortNode.NodeKind.Gold
                ? ItemCatalog.CoinBody
                : kind == SortNode.NodeKind.Letter
                    ? new Color(0.95f, 0.78f, 0.32f, 1f)
                    : ItemCatalog.GemBody(colorId);
        }

        LastBody = kind == SortNode.NodeKind.Bomb ? ItemCatalog.Charcoal : LastEmission;
        bool ring = ItemCatalog.ShowHalo(kind);
        if (prop.Halo != null && !ring) prop.Halo.gameObject.SetActive(false);
        else if (prop.HaloRenderer != null && ring)
            ItemCatalog.ApplyHalo(prop.HaloRenderer, block, kind, colorId);
        LastHalo = prop.Halo != null && prop.Halo.gameObject.activeSelf;
    }

    void Follow(Prop prop, RectTransform rect)
    {
        WorldFollow.Place(prop.Transform, rect);
        float placed = prop.Transform.localScale.y;
        ItemCatalog.BoostScale(prop.Transform);
        LastScaleBoost = placed > 0.0001f ? prop.Transform.localScale.y / placed : 0f;
        ItemCatalog.Spin(prop.Transform);
        if (prop.Halo != null)
        {
            if (ItemCatalog.ShowHalo(prop.Kind)) ItemCatalog.SeatHalo(prop.Halo, prop.Transform);
            else prop.Halo.gameObject.SetActive(false);
            LastHalo = prop.Halo.gameObject.activeInHierarchy;
        }

        PulseCore(prop);
    }

    void PulseCore(Prop prop)
    {
        bool bomb = prop.Kind == SortNode.NodeKind.Bomb;
        if (prop.Core != null) prop.Core.gameObject.SetActive(bomb);
        if (!bomb || prop.CoreRenderer == null) return;
        float wave = ItemCatalog.BombWave();
        Color core = ItemCatalog.CoreColor(wave);
        ItemCatalog.ApplyCore(prop.CoreRenderer, block, core);
        float scale = Mathf.Lerp(0.32f, 0.6f, wave);
        prop.Core.localScale = new Vector3(scale, scale, scale);
        prop.Core.position = prop.Transform.position + new Vector3(0f, 0f, -0.1f);
        prop.Core.rotation = Quaternion.identity;
        if (core.r > CorePeak) CorePeak = core.r;
        if (prop.Transform == LastTransform)
        {
            LastCore = core;
            LastBody = ItemCatalog.Charcoal;
        }
    }

    int Rent()
    {
        for (int i = 0; i < props.Length; i++)
        {
            if (props[i] != null && !props[i].Alive) return i;
        }

        return -1;
    }

    void Ensure()
    {
        if (ready) return;
        material = ItemCatalog.CreateUnlit();
        block = new MaterialPropertyBlock();
        Material trailMaterial = TrailMaterial();
        Mesh haloMesh = ItemCatalog.HaloMesh;
        Mesh coreMesh = ItemCatalog.CoreMesh;
        for (int i = 0; i < Pool; i++)
        {
            var go = new GameObject("ItemProp");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            var trail = go.AddComponent<TrailRenderer>();
            ConfigureTrail(trail, trailMaterial);
            ParticleSystem sparkle = CasinoDressing.CreateSparkle(go.transform);
            Transform haloTransform = AddChild(go.transform, "Halo", haloMesh, material, out MeshRenderer haloRenderer);
            Transform coreTransform = AddChild(go.transform, "WarningCore", coreMesh, material, out MeshRenderer coreRenderer);
            if (coreTransform != null) coreTransform.gameObject.SetActive(false);
            go.SetActive(false);
            props[i] = new Prop
            {
                Go = go,
                Transform = go.transform,
                Filter = filter,
                Renderer = renderer,
                Trail = trail,
                Sparkle = sparkle,
                Halo = haloTransform,
                HaloRenderer = haloRenderer,
                Core = coreTransform,
                CoreRenderer = coreRenderer
            };
        }

        EnsureLight();
        ready = true;
    }

    static Transform AddChild(Transform parent, string name, Mesh mesh, Material shared, out MeshRenderer renderer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = shared;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    static Material TrailMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        var material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        return material;
    }

    static void ConfigureTrail(TrailRenderer trail, Material material)
    {
        trail.time = 0.38f;
        trail.minVertexDistance = 0.03f;
        trail.startWidth = 0.07f;
        trail.endWidth = 0.01f;
        trail.numCornerVertices = 2;
        trail.autodestruct = false;
        trail.emitting = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        if (material != null) trail.material = material;
    }

    static void EnsureLight()
    {
        if (GameObject.Find("ChuteKeyLight") != null) return;
        var keyGo = new GameObject("ChuteKeyLight");
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.97f, 0.92f);
        key.intensity = 1.45f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.7f;
        keyGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.22f, -0.48f, 1f));

        var fillGo = new GameObject("ChuteFillLight");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.72f, 0.8f, 1f);
        fill.intensity = 0.42f;
        fill.shadows = LightShadows.None;
        fillGo.transform.rotation = Quaternion.LookRotation(new Vector3(-0.55f, -0.15f, 1f));
    }
}
