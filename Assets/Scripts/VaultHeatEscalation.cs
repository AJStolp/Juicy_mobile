using UnityEngine;

// Structural heat. The full screen flash stays off. Bolts, sirens, and the
// chute rim carry the warning instead.
public class VaultHeatEscalation : MonoBehaviour
{
    public static VaultHeatEscalation Instance { get; private set; }

    const float SirenOn = 0.15f;
    static readonly float[] BoltAt = { 0.20f, 0.40f, 0.65f, 0.85f };

    EmissionPulse[] pulses = System.Array.Empty<EmissionPulse>();
    float nextScan;
    bool built;
    bool sirenLogged;
    Transform rig;
    Bolt[] bolts;
    Siren[] sirens;

    struct Bolt
    {
        public Transform bar;
        public Vector3 openPos;
        public Vector3 shutPos;
        public float shown;
        public bool latched;
    }

    struct Siren
    {
        public Transform beam;
        public SpriteRenderer housing;
        public SpriteRenderer beamRenderer;
        public Light lamp;
        public float spin;
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("VaultHeatEscalation").AddComponent<VaultHeatEscalation>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        float shown = 0f;
        if (HeatManager.Instance != null)
            shown = Mathf.Clamp01(HeatManager.Instance.ShownHeat / HeatManager.MaxHeat);

        PushHeat(shown);
        if (!built) TryBuild();
        if (!built) return;

        float dt = Time.deltaTime;
        StepBolts(shown, dt);
        StepSirens(shown, dt);
    }

    void PushHeat(float shown)
    {
        if (Time.unscaledTime >= nextScan || pulses.Length == 0)
        {
            pulses = FindObjectsByType<EmissionPulse>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            nextScan = Time.unscaledTime + 0.5f;
        }

        for (int i = 0; i < pulses.Length; i++)
        {
            if (pulses[i] != null) pulses[i].SetHeat(shown);
        }
    }

    void TryBuild()
    {
        VaultPortalController portal = FindFirstObjectByType<VaultPortalController>();
        if (portal == null) return;
        SpriteRenderer door = portal.GetComponent<SpriteRenderer>();
        if (door == null || door.sprite == null) return;

        Sprite sprite = door.sprite;
        float halfW = sprite.rect.width / sprite.pixelsPerUnit * 0.5f;
        float halfH = sprite.rect.height / sprite.pixelsPerUnit * 0.5f;
        if (halfH < 0.01f) return;

        // One child pixel matches one reference pixel on the 830px door.
        float ppu = 415f / halfH;
        float unit = 1f / ppu;

        rig = new GameObject("VaultHeatRig").transform;
        rig.SetParent(portal.transform, false);
        rig.localPosition = Vector3.zero;
        rig.localRotation = Quaternion.identity;
        rig.localScale = Vector3.one;

        Sprite brass = Capsule(92, 26, ppu, new Color(0.86f, 0.66f, 0.28f, 1f), new Color(0.38f, 0.24f, 0.08f, 1f), new Vector2(0.5f, 0.5f));
        Sprite slot = Capsule(118, 20, ppu, new Color(0.16f, 0.13f, 0.12f, 1f), new Color(0.05f, 0.04f, 0.04f, 1f), new Vector2(0.5f, 0.5f));
        Sprite lamp = Disc(64, ppu, new Color(1f, 0.16f, 0.06f, 1f), new Color(1f, 0.92f, 0.75f, 1f));
        Sprite beamSprite = Beam(48, 200, ppu);

        float slide = 78f * unit;
        float jamb = halfW * 0.31f;
        float[] signX = { -1f, 1f, -1f, 1f };
        float[] rowY = { halfH * 0.46f, halfH * 0.46f, -halfH * 0.46f, -halfH * 0.46f };
        bolts = new Bolt[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 shut = new Vector3(signX[i] * jamb, rowY[i], -0.02f);
            Vector3 open = shut + new Vector3(signX[i] * slide, 0f, 0f);
            Transform housing = Piece("BoltSlot" + i, rig, slot, "Tracks", 5);
            housing.localPosition = (shut + open) * 0.5f;
            Transform bar = Piece("Bolt" + i, rig, brass, "Tracks", 6);
            bar.localPosition = open;
            bolts[i] = new Bolt { bar = bar, openPos = open, shutPos = shut };
        }

        sirens = new Siren[2];
        // Beside the door, on the wall panels the header does not cover.
        float side = halfW + 96f * unit;
        float lampY = halfH * 0.42f;
        float[] lampX = { -side, side };
        for (int i = 0; i < 2; i++)
        {
            Transform housing = Piece("Siren" + i, rig, lamp, "Background", 2);
            housing.localPosition = new Vector3(lampX[i], lampY, 0.04f);
            Transform beam = Piece("Beam" + i, housing, beamSprite, "Background", 1);
            beam.localPosition = new Vector3(0f, -16f * unit, 0f);
            var lampGo = new GameObject("SirenLight");
            lampGo.transform.SetParent(housing, false);
            lampGo.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            Light sirenLight = lampGo.AddComponent<Light>();
            sirenLight.type = LightType.Point;
            sirenLight.shadows = LightShadows.None;
            sirenLight.range = 9f;
            sirenLight.intensity = 0f;
            sirenLight.color = i == 0 ? new Color(1f, 0.08f, 0.06f) : new Color(0.25f, 0.45f, 1f);
            sirens[i] = new Siren
            {
                beam = beam,
                housing = housing.GetComponent<SpriteRenderer>(),
                beamRenderer = beam.GetComponent<SpriteRenderer>(),
                lamp = sirenLight,
                spin = i == 0 ? 18f : 200f
            };
        }

        built = true;
        Debug.Log("Vault heat rig on the door.");
    }

    void StepBolts(float shown, float dt)
    {
        for (int i = 0; i < bolts.Length; i++)
        {
            float target = shown >= BoltAt[i] ? 1f : 0f;
            bolts[i].shown = Mathf.MoveTowards(bolts[i].shown, target, dt * 3.2f);
            if (bolts[i].bar != null)
                bolts[i].bar.localPosition = Vector3.Lerp(bolts[i].openPos, bolts[i].shutPos, bolts[i].shown);
            if (target > 0.5f && !bolts[i].latched && bolts[i].shown > 0.92f)
            {
                bolts[i].latched = true;
                Debug.Log("Vault bolt " + i + " locked.");
            }
            else if (target < 0.5f)
            {
                bolts[i].latched = false;
            }
        }
    }

    void StepSirens(float shown, float dt)
    {
        float presence = Mathf.InverseLerp(0.08f, 0.22f, shown);
        float spin = Mathf.Lerp(40f, 150f, shown);
        if (shown >= SirenOn && !sirenLogged)
        {
            sirenLogged = true;
            Debug.Log("Vault sirens turning.");
        }

        if (shown < SirenOn) sirenLogged = false;

        for (int i = 0; i < sirens.Length; i++)
        {
            float dir = i == 0 ? 1f : -1f;
            if (shown >= SirenOn) sirens[i].spin += dir * spin * dt;
            if (sirens[i].beam != null)
                sirens[i].beam.localRotation = Quaternion.Euler(0f, 0f, sirens[i].spin);
            if (sirens[i].housing != null)
                sirens[i].housing.color = new Color(1f, 0.16f, 0.08f, presence);
            if (sirens[i].beamRenderer != null)
            {
                float alpha = presence * Mathf.Lerp(0.5f, 0.8f, shown);
                sirens[i].beamRenderer.color = new Color(1f, 0.1f, 0.06f, alpha);
            }

            if (sirens[i].lamp != null)
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f + i * Mathf.PI);
                sirens[i].lamp.intensity = presence * Mathf.Lerp(0.35f, 7.5f, flash);
            }
        }
    }

    static Transform Piece(string name, Transform parent, Sprite sprite, string layer, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = layer;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    static Sprite Capsule(int width, int height, float ppu, Color fill, Color rim, Vector2 pivot)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[width * height];
        float rx = (width - 1) * 0.5f;
        float ry = (height - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - rx) / rx;
                float ny = (y - ry) / ry;
                float d = nx * nx + ny * ny;
                if (d > 1f)
                {
                    pixels[y * width + x] = Color.clear;
                    continue;
                }

                Color color = d > 0.72f ? rim : Color.Lerp(fill, rim, d * 0.4f);
                if (ny > 0.15f && d < 0.65f)
                    color = Color.Lerp(color, new Color(1f, 0.92f, 0.7f, 1f), 0.22f);
                pixels[y * width + x] = color;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), pivot, ppu);
    }

    static Sprite Disc(int size, float ppu, Color rim, Color core)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                if (d > 1f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                Color color = Color.Lerp(core, rim, Mathf.Clamp01(d * 1.3f));
                color.a = 1f;
                pixels[y * size + x] = color;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), ppu);
    }

    static Sprite Beam(int width, int height, float ppu)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[width * height];
        float cx = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            float along = 1f - (y / (float)(height - 1));
            float fade = Mathf.Clamp01(1f - along);
            fade = fade * fade;
            for (int x = 0; x < width; x++)
            {
                float across = 1f - Mathf.Abs(x - cx) / cx;
                float a = fade * Mathf.Clamp01(across);
                pixels[y * width + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 1f), ppu);
    }
}
