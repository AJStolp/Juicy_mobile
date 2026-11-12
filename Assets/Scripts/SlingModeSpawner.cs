using UnityEngine;
using UnityEngine.InputSystem;

// Upward sling prototype. The chute stays the mode that starts on Play.
public class SlingModeSpawner : MonoBehaviour
{
    public static SlingModeSpawner Instance { get; private set; }

    const int Pool = 8;
    const float CollectX = 2.35f;

    class Piece
    {
        public GameObject Go;
        public Rigidbody Body;
        public Renderer Renderer;
        public Transform Visual;
        public TrailRenderer Trail;
        public ParticleSystem Sparkle;
        public Transform Halo;
        public MeshRenderer HaloRenderer;
        public Transform Core;
        public MeshRenderer CoreRenderer;
        public SortNode.NodeKind Kind;
        public int ColorId;
        public char Letter;
        public bool Alive;
        public bool Flung;
        public bool Neutral;
        public bool Rising;
    }

    static readonly Color[] Palette =
    {
        new Color(0.62f, 0.05f, 0.10f),
        new Color(0.16f, 0.14f, 0.52f),
        new Color(0.93f, 0.28f, 0.22f),
        new Color(0.42f, 0.70f, 0.96f)
    };

    readonly Piece[] pieces = new Piece[Pool];
    Material sharedMaterial;
    MaterialPropertyBlock block;
    bool poolReady;
    bool autoSpawn;
    float spawnTimer;
    int lastLaunch = -1;
    Piece pressed;
    Vector2 pressStart;

    public bool Running { get; private set; }
    public int Launched { get; private set; }
    public float LowestStartY { get; private set; } = 99f;
    public float HighestY { get; private set; } = -99f;
    public float LastAngularSpeed { get; private set; }
    public string LastMeshName { get; private set; } = "";
    public Color LastEmission { get; private set; }
    public Color LastTrail { get; private set; }
    public float LastVisualScale { get; private set; }
    public bool LastSparkle { get; private set; }
    public bool Crested { get; private set; }
    public bool NeutralCleared { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsurePool();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Enter()
    {
        SlingModeSpawner sling = Instance;
        if (sling == null)
            sling = FindFirstObjectByType<SlingModeSpawner>(FindObjectsInactive.Include);
        if (sling == null) return;
        if (!sling.gameObject.activeSelf) sling.gameObject.SetActive(true);
        sling.Begin();
    }

    public void Begin()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        EnsurePool();
        Running = true;
        autoSpawn = true;
        spawnTimer = 0f;
        Crested = false;
        NeutralCleared = false;
        HighestY = -99f;
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) sort.SetSlingHold(true);
        LaunchRoll();
        Debug.Log("Sling mode on.");
    }

    public void End()
    {
        if (!Running && !autoSpawn)
        {
            ActionSortController idle = FindFirstObjectByType<ActionSortController>();
            if (idle != null) idle.SetSlingHold(false);
            return;
        }

        Running = false;
        autoSpawn = false;
        pressed = null;
        ClearAir();
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) sort.SetSlingHold(false);
        Debug.Log("Sling mode off.");
    }

    public void SetAutoSpawn(bool enabled)
    {
        autoSpawn = enabled && Running;
    }

    public void ClearAir()
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null && pieces[i].Alive) Recycle(pieces[i]);
        }
    }

    public void LaunchOne(SortNode.NodeKind kind, int colorId, char letter = '\0')
    {
        EnsurePool();
        LaunchPiece(kind, colorId, letter);
    }

    public bool FlingLatest(int direction)
    {
        if (lastLaunch < 0 || lastLaunch >= pieces.Length) return false;
        Piece piece = pieces[lastLaunch];
        if (piece == null || !piece.Alive) return false;
        ApplyFling(piece, direction);
        return true;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.uKey.wasPressedThisFrame)
        {
            if (Running) End();
            else Begin();
        }

        if (!Running) return;

        TickPointer();
        TickPieces();

        if (!autoSpawn) return;
        spawnTimer += Time.deltaTime;
        if (spawnTimer < 0.55f) return;
        spawnTimer = 0f;
        LaunchRoll();
    }

    void LaunchRoll()
    {
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        float bomb = sort != null ? sort.BombChance : 0.15f;
        float letter = sort != null ? sort.LetterChance : 0.10f;
        float gold = sort != null ? sort.GoldChance : 0.10f;
        float roll = Random.value;
        SortNode.NodeKind kind = SortNode.NodeKind.Gem;
        int colorId = Random.Range(0, Palette.Length);
        char glyph = '\0';
        if (roll < bomb) kind = SortNode.NodeKind.Bomb;
        else if (roll < bomb + letter)
        {
            kind = SortNode.NodeKind.Letter;
            if (WordVaultManager.Instance != null) glyph = WordVaultManager.Instance.PickLetter();
        }
        else if (roll < bomb + letter + gold) kind = SortNode.NodeKind.Gold;
        if (kind != SortNode.NodeKind.Gem) colorId = 0;
        LaunchPiece(kind, colorId, glyph);
    }

    void TickPointer()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            pressStart = pointer.position.ReadValue();
            pressed = Pick(pressStart);
            return;
        }

        if (pressed == null || !pointer.press.wasReleasedThisFrame) return;

        int direction = ActionSortController.FlickDirection(pressStart, pointer.position.ReadValue(), 48f);
        Piece piece = pressed;
        pressed = null;
        if (direction != 0 && piece.Alive) ApplyFling(piece, direction);
    }

    Piece Pick(Vector2 screen)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        Piece best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < pieces.Length; i++)
        {
            Piece piece = pieces[i];
            if (piece == null || !piece.Alive) continue;
            Vector3 projected = cam.WorldToScreenPoint(piece.Body.position);
            if (projected.z < 0f) continue;
            float distance = Vector2.Distance(screen, new Vector2(projected.x, projected.y));
            float reach = TouchReach(cam, piece);
            if (distance > reach || distance >= bestDistance) continue;
            bestDistance = distance;
            best = piece;
        }

        return best;
    }

    static float TouchReach(Camera cam, Piece piece)
    {
        float floor = 140f * ItemCatalog.ItemPresentationScale * ItemCatalog.TouchPadding;
        if (cam == null || piece.Renderer == null) return floor;
        Bounds bounds = piece.Renderer.bounds;
        Vector3 center = cam.WorldToScreenPoint(bounds.center);
        Vector3 edge = cam.WorldToScreenPoint(bounds.center + cam.transform.right * bounds.extents.magnitude);
        float radius = Vector2.Distance(new Vector2(center.x, center.y), new Vector2(edge.x, edge.y));
        return Mathf.Max(floor, radius * ItemCatalog.TouchPadding);
    }

    void TickPieces()
    {
        Camera cam = Camera.main;
        float top = 8f;
        float floor = -8f;
        if (cam != null)
        {
            float distance = Mathf.Max(0.5f, Mathf.Abs(cam.transform.position.z));
            float half = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            top = cam.transform.position.y + half + 0.35f;
            floor = cam.transform.position.y - half - 0.15f;
        }

        for (int i = 0; i < pieces.Length; i++)
        {
            Piece piece = pieces[i];
            if (piece == null || !piece.Alive) continue;

            if (piece.Visual != null) ItemCatalog.Spin(piece.Visual);
            Dress(piece);

            Vector3 position = piece.Body.position;
            float rise = piece.Body.linearVelocity.y;
            if (position.y > HighestY) HighestY = position.y;
            if (rise > 0.4f) piece.Rising = true;
            if (piece.Rising && rise < 0f) Crested = true;

            if (piece.Neutral && (position.y > top || (position.y < floor && rise <= 0.1f)))
            {
                NeutralCleared = true;
                Recycle(piece);
                continue;
            }

            if (piece.Flung && !piece.Neutral)
            {
                if (position.x <= -CollectX)
                {
                    Collect(piece, 0);
                    continue;
                }

                if (position.x >= CollectX)
                {
                    Collect(piece, 1);
                    continue;
                }
            }

            if (!piece.Neutral && position.y < floor && rise <= 0.1f)
                Recycle(piece);
        }
    }

    void ApplyFling(Piece piece, int direction)
    {
        piece.Flung = true;
        if (direction == 2)
        {
            piece.Neutral = true;
            piece.Body.linearVelocity = new Vector3(0f, 18f, 0f);
            return;
        }

        float x = direction < 0 ? -9f : 9f;
        piece.Body.linearVelocity = new Vector3(x, 3.5f, 0f);
    }

    void Collect(Piece piece, int side)
    {
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) sort.ResolveCatch(piece.Kind, piece.ColorId, side, piece.Letter);
        Recycle(piece);
    }

    void LaunchPiece(SortNode.NodeKind kind, int colorId, char letter = '\0')
    {
        Piece piece = Rent();
        if (piece == null) return;

        Camera cam = Camera.main;
        float bottom = -6f;
        float apex = 1.6f;
        if (cam != null)
        {
            float distance = Mathf.Max(0.5f, Mathf.Abs(cam.transform.position.z));
            float half = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            bottom = cam.transform.position.y - half;
            apex = cam.transform.position.y + Random.Range(0.15f, 1.5f);
        }

        Vector3 start = new Vector3(Random.Range(-1.1f, 1.1f), bottom - 0.45f, 0f);
        float rise = Mathf.Max(0.8f, apex - start.y);
        float speed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * rise);
        Vector3 spin = Random.onUnitSphere * Random.Range(4f, 9f);

        piece.Kind = kind;
        piece.ColorId = colorId;
        piece.Letter = letter;
        piece.Flung = false;
        piece.Neutral = false;
        piece.Rising = false;
        piece.Alive = true;
        piece.Go.SetActive(true);
        piece.Body.position = start;
        piece.Body.linearVelocity = new Vector3(Random.Range(-1.2f, 1.2f), speed, 0f);
        piece.Body.angularVelocity = spin;
        piece.Body.WakeUp();
        Mesh mesh = ItemCatalog.MeshFor(ItemCatalog.Current, kind, colorId);
        if (piece.Visual != null)
        {
            MeshFilter filter = piece.Visual.GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = mesh;
            BoxCollider touch = piece.Visual.GetComponent<BoxCollider>();
            if (touch != null && mesh != null)
            {
                touch.center = mesh.bounds.center;
                Vector3 size = mesh.bounds.size * ItemCatalog.TouchPadding;
                size.z = Mathf.Max(size.z, 0.2f);
                touch.size = size;
            }
        }

        LastMeshName = mesh != null ? mesh.name : "";
        Tint(piece, kind, colorId);

        LowestStartY = Mathf.Min(LowestStartY, start.y);
        LastAngularSpeed = spin.magnitude;
        Launched++;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == piece) lastLaunch = i;
        }
    }

    void Tint(Piece piece, SortNode.NodeKind kind, int colorId)
    {
        if (piece.Renderer == null || block == null) return;
        LastTrail = ItemCatalog.TrailTint(kind, colorId);
        if (!ItemCatalog.PresentHero(piece.Renderer, block, kind, colorId, ItemCatalog.Current))
        {
            if (sharedMaterial != null && piece.Renderer != null) piece.Renderer.sharedMaterial = sharedMaterial;
            LastEmission = ItemCatalog.DestinationGlow(kind, colorId);
            ItemCatalog.ApplyShine(piece.Renderer, block, kind, colorId);
        }
        else
        {
            LastEmission = kind == SortNode.NodeKind.Gold
                ? ItemCatalog.CoinBody
                : ItemCatalog.GemBody(colorId);
        }
        if (piece.Trail != null) ItemCatalog.PaintTrail(piece.Trail, kind, colorId);
        if (piece.Visual != null) LastVisualScale = piece.Visual.localScale.x;
        if (piece.Sparkle != null)
        {
            piece.Sparkle.Clear();
            piece.Sparkle.Play();
            LastSparkle = piece.Sparkle.isPlaying;
        }

        Dress(piece);
    }

    void Dress(Piece piece)
    {
        if (piece == null || block == null) return;
        if (piece.Halo != null && piece.Visual != null)
        {
            if (ItemCatalog.ShowHalo(piece.Kind))
            {
                ItemCatalog.SeatHalo(piece.Halo, piece.Visual);
                if (piece.HaloRenderer != null)
                    ItemCatalog.ApplyHalo(piece.HaloRenderer, block, piece.Kind, piece.ColorId);
            }
            else
            {
                piece.Halo.gameObject.SetActive(false);
            }
        }

        bool bomb = piece.Kind == SortNode.NodeKind.Bomb;
        if (piece.Core != null) piece.Core.gameObject.SetActive(bomb);
        if (!bomb || piece.CoreRenderer == null || piece.Visual == null) return;
        float wave = ItemCatalog.BombWave();
        Color core = ItemCatalog.CoreColor(wave);
        ItemCatalog.ApplyCore(piece.CoreRenderer, block, core);
        float scale = Mathf.Lerp(0.32f, 0.6f, wave);
        piece.Core.localScale = new Vector3(scale, scale, scale);
        piece.Core.position = piece.Visual.position + new Vector3(0f, 0f, -0.1f);
        piece.Core.rotation = Quaternion.identity;
    }

    Piece Rent()
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null && !pieces[i].Alive) return pieces[i];
        }

        return null;
    }

    void Recycle(Piece piece)
    {
        piece.Alive = false;
        piece.Flung = false;
        piece.Neutral = false;
        piece.Rising = false;
        if (piece.Body != null)
        {
            piece.Body.linearVelocity = Vector3.zero;
            piece.Body.angularVelocity = Vector3.zero;
        }

        if (piece.Trail != null)
        {
            piece.Trail.emitting = false;
            piece.Trail.Clear();
        }

        if (piece.Sparkle != null)
            piece.Sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (piece.Go != null) piece.Go.SetActive(false);
        if (pressed == piece) pressed = null;
    }

    static Transform AddChild(Transform parent, string name, Mesh mesh, Material shared, out MeshRenderer renderer)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        var filter = child.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = shared;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return child.transform;
    }

    void EnsurePool()
    {
        if (poolReady) return;

        sharedMaterial = ItemCatalog.CreateUnlit();
        block = new MaterialPropertyBlock();
        Mesh haloMesh = ItemCatalog.HaloMesh;
        Mesh coreMesh = ItemCatalog.CoreMesh;

        for (int i = 0; i < Pool; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SlingPiece";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.42f;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            var body = go.AddComponent<Rigidbody>();
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezePositionZ;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.04f;
            body.angularDamping = 0.15f;
            var rootRenderer = go.GetComponent<Renderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * ItemCatalog.VisualScale * ItemCatalog.ItemPresentationScale;
            var touch = visual.AddComponent<BoxCollider>();
            touch.isTrigger = true;
            visual.AddComponent<MeshFilter>();
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            Transform halo = AddChild(visual.transform, "Halo", haloMesh, sharedMaterial, out MeshRenderer haloRenderer);
            Transform core = AddChild(visual.transform, "WarningCore", coreMesh, sharedMaterial, out MeshRenderer coreRenderer);
            if (core != null) core.gameObject.SetActive(false);
            ParticleSystem sparkle = CasinoDressing.CreateSparkle(visual.transform);
            var trail = visual.AddComponent<TrailRenderer>();
            trail.time = 0.38f;
            trail.minVertexDistance = 0.03f;
            trail.startWidth = 0.07f;
            trail.endWidth = 0.01f;
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            Shader trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (trailShader == null) trailShader = Shader.Find("Sprites/Default");
            if (trailShader != null)
            {
                var trailMaterial = new Material(trailShader);
                trailMaterial.hideFlags = HideFlags.HideAndDontSave;
                if (trailMaterial.HasProperty("_BaseColor")) trailMaterial.SetColor("_BaseColor", Color.white);
                trail.material = trailMaterial;
            }

            go.SetActive(false);

            pieces[i] = new Piece
            {
                Go = go,
                Body = body,
                Renderer = renderer,
                Visual = visual.transform,
                Trail = trail,
                Sparkle = sparkle,
                Halo = halo,
                HaloRenderer = haloRenderer,
                Core = core,
                CoreRenderer = coreRenderer
            };
        }

        poolReady = true;
    }
}
