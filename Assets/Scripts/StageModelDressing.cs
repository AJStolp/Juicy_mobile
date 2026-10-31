using UnityEngine;

public class StageModelDressing : MonoBehaviour
{
    public static StageModelDressing Instance { get; private set; }

    const int ShardCount = 8;

    class Shard
    {
        public Transform Transform;
        public Vector3 Velocity;
        public float Life;
    }

    RectTransform avatarRect;
    RectTransform carRect;
    RectTransform[] wallRects;
    Transform avatar;
    Transform car;
    Transform[] walls;
    bool[] broken;
    readonly Shard[] shards = new Shard[ShardCount];
    readonly System.Collections.Generic.List<MeshRenderer> lasers = new System.Collections.Generic.List<MeshRenderer>(8);
    Transform chest;
    Transform lid;
    Transform path;
    Transform pathCore;
    Transform bag;
    ParticleSystem rays;
    Material material;
    Material glow;
    MaterialPropertyBlock block;
    bool ready;
    float lidPitch;
    float chestHold;

    public string AvatarMeshName { get; private set; } = "";
    public string WallMeshName { get; private set; } = "";
    public int Fractures { get; private set; }
    public bool ChestReady => chest != null;
    public int ChestOpens { get; private set; }
    public bool LasersLive => lasers.Count > 0;
    public bool PathLive => path != null && path.gameObject.activeSelf;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnEnable()
    {
        BindTheme();
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

    public void Bind(RectTransform character, RectTransform[] towers, RectTransform getaway)
    {
        Ensure();
        if (avatar != null) return;
        avatarRect = character;
        wallRects = towers;
        carRect = getaway;
        HideGraphic(character);
        HideGraphic(getaway);
        if (towers != null)
        {
            walls = new Transform[towers.Length];
            broken = new bool[towers.Length];
            for (int i = 0; i < towers.Length; i++)
            {
                HideGraphic(towers[i]);
                walls[i] = MakeProp("SecurityWall", ItemCatalog.WallMesh, new Color(0.42f, 0.3f, 0.12f), ItemCatalog.WallMaterial, ItemCatalog.WallMaterials);
                AddLasers(walls[i]);
            }
        }

        Mesh avatarMesh = ItemCatalog.Avatar(ItemCatalog.Current);
        avatar = MakeProp("StageAvatar", avatarMesh, new Color(0.1f, 0.09f, 0.12f));
        AvatarMeshName = avatarMesh != null ? avatarMesh.name : "";
        if (avatar != null)
            bag = AddAccent(avatar, ItemCatalog.BagMesh, new Color(0.95f, 0.72f, 0.2f), new Vector3(0.34f, -0.02f, 0.04f));
        car = MakeProp("StageCar", ItemCatalog.CarMesh, new Color(0.95f, 0.72f, 0.18f));
        WallMeshName = ItemCatalog.WallMesh != null ? ItemCatalog.WallMesh.name : "";
        BuildFinish();
        BindTheme();
    }

    public void OpenChest()
    {
        if (chest == null) return;
        ChestOpens++;
        chestHold = 1.2f;
        if (rays != null)
        {
            rays.gameObject.SetActive(true);
            rays.Clear();
            rays.Play();
        }
    }

    public void Fracture(RectTransform tower)
    {
        Ensure();
        if (wallRects == null || tower == null) return;
        for (int i = 0; i < wallRects.Length; i++)
        {
            if (wallRects[i] != tower) continue;
            if (broken != null && i < broken.Length) broken[i] = true;
            if (walls != null && walls[i] != null) walls[i].gameObject.SetActive(false);
            BurstShards(WorldFollow.UiToWorld(tower));
            Fractures++;
            Debug.Log("Security wall fractures.");
            return;
        }
    }

    public void Restore()
    {
        Fractures = 0;
        if (broken != null)
        {
            for (int i = 0; i < broken.Length; i++) broken[i] = false;
        }

        for (int i = 0; i < shards.Length; i++)
        {
            if (shards[i] == null) continue;
            shards[i].Life = 0f;
            if (shards[i].Transform != null) shards[i].Transform.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        HideProp(avatar);
        HideProp(car);
        HideProp(bag);
        HideProp(path);
        HideProp(pathCore);
        HideProp(chest);
        if (walls == null) return;
        for (int i = 0; i < walls.Length; i++)
            HideProp(walls[i]);
    }

    void Update()
    {
        for (int i = 0; i < shards.Length; i++)
        {
            Shard shard = shards[i];
            if (shard == null || shard.Life <= 0f || shard.Transform == null) continue;
            shard.Life -= Time.deltaTime;
            shard.Velocity += Vector3.down * 9f * Time.deltaTime;
            shard.Transform.position += shard.Velocity * Time.deltaTime;
            shard.Transform.Rotate(180f * Time.deltaTime, 90f * Time.deltaTime, 0f, Space.Self);
            if (shard.Life > 0f) continue;
            shard.Transform.gameObject.SetActive(false);
        }
    }

    void BurstShards(Vector3 origin)
    {
        Ensure();
        for (int i = 0; i < shards.Length; i++)
        {
            if (shards[i] == null || shards[i].Transform == null) continue;
            shards[i].Transform.gameObject.SetActive(true);
            shards[i].Transform.position = origin;
            shards[i].Transform.localScale = Vector3.one * 0.18f;
            shards[i].Velocity = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(1.2f, 3.2f), 0f);
            shards[i].Life = 0.55f;
        }
    }

    Transform MakeProp(string name, Mesh mesh, Color color, Material authored = null, Material[] slots = null)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        GameObject host = root;
        bool painted = IsVaultMaterial(authored) || (slots != null && slots.Length > 0 && IsVaultMaterial(slots[0]));
        bool vault = painted || (slots != null && slots.Length > 1);
        if (authored != null || vault)
        {
            host = new GameObject(name + "Mesh");
            host.transform.SetParent(root.transform, false);
            SeatDoor(host.transform, mesh);
        }

        var filter = host.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = host.AddComponent<MeshRenderer>();
        if (vault && mesh != null && mesh.subMeshCount > 1)
            renderer.sharedMaterials = slots;
        else
            renderer.sharedMaterial = authored != null ? authored : material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (authored == null && !vault && block != null)
        {
            block.Clear();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        if (vault) AddLockGlow(root.transform);
        return root.transform;
    }

    static bool IsVaultMaterial(Material candidate)
    {
        return candidate != null && candidate.name.StartsWith("M_Vault");
    }

    static void AddLockGlow(Transform root)
    {
        var glow = new GameObject("LockGlow");
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = new Vector3(0f, 0f, -0.16f);
        var point = glow.AddComponent<Light>();
        point.type = LightType.Point;
        point.color = new Color(0f, 1f, 0.4f);
        point.intensity = 3.5f;
        point.range = 0.7f;
        point.shadows = LightShadows.None;
    }

    // The imported door is whatever size the FBX came in as. Seat it on the
    // same 1.22 tall footprint as the old box door, with the face just behind
    // the laser bars.
    static void SeatDoor(Transform visual, Mesh mesh)
    {
        if (visual == null || mesh == null) return;
        Bounds bounds = mesh.bounds;
        float height = bounds.size.y;
        if (height < 0.0001f) return;
        float scale = 1.22f / height;
        visual.localScale = Vector3.one * scale;
        visual.localPosition = new Vector3(
            -bounds.center.x * scale,
            -bounds.center.y * scale,
            -0.06f - bounds.min.z * scale);
    }

    void Ensure()
    {
        if (ready) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
        }

        block = new MaterialPropertyBlock();
        glow = ItemCatalog.CreateUnlit();
        Mesh shardMesh = ItemCatalog.ShardMesh;
        for (int i = 0; i < ShardCount; i++)
        {
            var go = new GameObject("WallShard");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = shardMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.enabled = false;
            if (block != null)
            {
                block.Clear();
                block.SetColor("_BaseColor", new Color(0.62f, 0.64f, 0.7f));
                renderer.SetPropertyBlock(block);
            }

            go.SetActive(false);
            shards[i] = new Shard { Transform = go.transform };
        }

        ready = true;
    }

    Transform AddAccent(Transform host, Mesh mesh, Color color, Vector3 localPos)
    {
        if (host == null || mesh == null) return null;
        Transform accent = MakeGlow("LootBag", mesh, color);
        if (accent == null) return null;
        accent.SetParent(host, false);
        accent.localPosition = localPos;
        accent.localRotation = Quaternion.identity;
        accent.localScale = Vector3.one;
        return accent;
    }

    void BindTheme()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnThemeChanged -= HandleTheme;
        GameCoreManager.Instance.OnThemeChanged += HandleTheme;
        if (GameCoreManager.Instance.ActiveTheme != null)
            HandleTheme(GameCoreManager.Instance.ActiveTheme);
    }

    void HandleTheme(ThemeConfig theme)
    {
        if (theme == null) return;
        if (avatar != null)
        {
            Mesh mesh = ItemCatalog.Avatar(theme.catalogTheme);
            MeshFilter filter = avatar.GetComponent<MeshFilter>();
            if (filter != null && mesh != null)
            {
                filter.sharedMesh = mesh;
                AvatarMeshName = mesh.name;
            }
        }

        if (bag != null)
            bag.gameObject.SetActive(theme.catalogTheme == ItemCatalog.Theme.Heist);
        if (theme.trackMaterial != null)
        {
            ApplyBarMaterial(path, theme.trackMaterial);
            ApplyBarMaterial(pathCore, theme.trackMaterial);
        }
        else
        {
            TintBar(path, theme.primaryNeonColor * 2.2f);
            TintBar(pathCore, theme.emissionColor * 2.4f);
        }

        DressWalls(theme.backgroundMaterial);
    }

    void ApplyBarMaterial(Transform bar, Material material)
    {
        if (bar == null || material == null) return;
        MeshRenderer renderer = bar.GetComponent<MeshRenderer>();
        if (renderer == null) return;
        renderer.sharedMaterial = material;
        renderer.SetPropertyBlock(null);
    }

    void DressWalls(Material material)
    {
        if (material == null || walls == null) return;
        for (int i = 0; i < walls.Length; i++)
            DressWall(walls[i], material);
    }

    void DressWall(Transform wall, Material material)
    {
        if (wall == null || material == null) return;
        MeshRenderer[] renderers = wall.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].gameObject.name == "ThemeCard") continue;
            renderers[i].enabled = false;
        }

        Transform card = wall.Find("ThemeCard");
        if (card == null)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ThemeCard";
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            quad.transform.SetParent(wall, false);
            card = quad.transform;
            card.localPosition = new Vector3(0f, 0f, -0.04f);
            card.localRotation = Quaternion.Euler(0f, 180f, 0f);
            card.localScale = new Vector3(0.82f, 1.22f, 1f);
        }

        MeshRenderer cardRenderer = card.GetComponent<MeshRenderer>();
        if (cardRenderer == null) return;
        cardRenderer.sharedMaterial = material;
        cardRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cardRenderer.enabled = true;
    }

    void TintBar(Transform bar, Color color)
    {
        if (bar == null || block == null) return;
        MeshRenderer renderer = bar.GetComponent<MeshRenderer>();
        if (renderer == null) return;
        block.Clear();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", color);
        renderer.SetPropertyBlock(block);
    }

    void AddLasers(Transform wall)
    {
        if (wall == null) return;
        for (int i = 0; i < 3; i++)
        {
            Transform beam = MakeGlow("Laser", ItemCatalog.ShardMesh, ItemCatalog.CrimsonNeon * 3.2f);
            if (beam == null) continue;
            beam.SetParent(wall, false);
            beam.localPosition = new Vector3(0f, -0.28f + i * 0.28f, -0.12f);
            beam.localRotation = Quaternion.identity;
            beam.localScale = new Vector3(4.4f, 0.16f, 0.25f);
            MeshRenderer renderer = beam.GetComponent<MeshRenderer>();
            if (renderer != null) lasers.Add(renderer);
        }
    }

    void BuildFinish()
    {
        path = MakeGlow("GoldPath", ItemCatalog.LiquidMesh, ItemCatalog.GoldNeon * 2.2f);
        pathCore = MakeGlow("PurplePath", ItemCatalog.LiquidMesh, new Color(0.62f, 0.22f, 1f) * 2.4f);
        chest = MakeGlow("TreasureChest", ItemCatalog.ChestMesh, new Color(0.72f, 0.46f, 0.12f));
        if (chest != null)
        {
            lid = MakeGlow("ChestLid", ItemCatalog.LidMesh, new Color(0.95f, 0.74f, 0.22f));
            if (lid != null)
            {
                lid.SetParent(chest, false);
                lid.localPosition = Vector3.zero;
                lid.localRotation = Quaternion.identity;
                lid.localScale = Vector3.one;
            }

            rays = CasinoDressing.CreateSparkle(chest);
            if (rays != null)
            {
                var main = rays.main;
                main.startSpeed = 1.4f;
                main.startSize = 0.08f;
                main.startLifetime = 0.7f;
                var shape = rays.shape;
                shape.radius = 0.05f;
                var emission = rays.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)28) });
                rays.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    void PlaceFinish()
    {
        bool showPath = avatar != null && car != null
            && avatar.gameObject.activeInHierarchy && car.gameObject.activeInHierarchy;
        if (showPath)
        {
            Vector3 from = avatar.position + new Vector3(0f, -0.18f, 0.35f);
            Vector3 to = car.position + new Vector3(0f, -0.18f, 0.35f);
            PlaceBar(path, from, to, 0.09f, 0.04f);
            Vector3 front = new Vector3(0f, 0f, -0.03f);
            PlaceBar(pathCore, from + front, to + front, 0.034f, 0.02f);
        }
        else
        {
            if (path != null) path.gameObject.SetActive(false);
            if (pathCore != null) pathCore.gameObject.SetActive(false);
        }

        if (chest == null || car == null || !car.gameObject.activeInHierarchy)
        {
            if (chest != null) chest.gameObject.SetActive(false);
            return;
        }

        chest.gameObject.SetActive(true);
        chest.position = car.position + new Vector3(-0.22f, -0.32f, -0.08f);
        chest.rotation = Quaternion.identity;
        float size = Mathf.Max(0.2f, car.localScale.y * 0.62f);
        chest.localScale = new Vector3(size, size, size);
        float target = chestHold > 0f ? 68f : 0f;
        lidPitch = Mathf.MoveTowards(lidPitch, target, 160f * Time.deltaTime);
        if (lid != null) lid.localRotation = Quaternion.Euler(lidPitch, 0f, 0f);
        if (chestHold > 0f)
        {
            chestHold -= Time.deltaTime;
            if (chestHold <= 0f && rays != null) rays.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    static void PlaceBar(Transform bar, Vector3 from, Vector3 to, float thickness, float depth)
    {
        if (bar == null) return;
        Vector3 span = to - from;
        float length = span.magnitude;
        bar.gameObject.SetActive(length > 0.05f);
        if (length <= 0.05f) return;
        bar.position = (from + to) * 0.5f;
        bar.rotation = Quaternion.FromToRotation(Vector3.right, span);
        bar.localScale = new Vector3(length, thickness, depth);
    }

    void PulseLasers()
    {
        if (block == null) return;
        float wave = 0.55f + 0.45f * Mathf.Sin(Time.time * 7f);
        Color color = ItemCatalog.CrimsonNeon * (2.2f + wave * 2.4f);
        for (int i = 0; i < lasers.Count; i++)
        {
            MeshRenderer renderer = lasers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
            block.Clear();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color);
            renderer.SetPropertyBlock(block);
        }
    }

    Transform MakeGlow(string name, Mesh mesh, Color color)
    {
        if (mesh == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = glow != null ? glow : material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (block != null)
        {
            block.Clear();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color);
            renderer.SetPropertyBlock(block);
        }

        return go.transform;
    }

    static void HideProp(Transform prop)
    {
        if (prop != null) prop.gameObject.SetActive(false);
    }

    static void HideGraphic(RectTransform rect)
    {
        if (rect == null) return;
        UnityEngine.UI.Graphic[] graphics = rect.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null) continue;
            Color color = graphics[i].color;
            color.a = 0f;
            graphics[i].color = color;
        }
    }
}
