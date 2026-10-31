using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// World meshes sit behind the chute items. An overlay panel would draw on top of them.
// The camera stays perspective. Field of view tightens in play and is not saved on the scene.
public class ChuteContrast : MonoBehaviour
{
    public static ChuteContrast Instance { get; private set; }

    public const float TargetFov = 50f;

    public float FieldOfView { get; private set; }
    public Color TrackColor { get; private set; }
    public Color RailColor { get; private set; }
    public bool OutlinesOn { get; private set; }
    public bool TrackLive { get; private set; }
    public float RailSeparation { get; private set; }
    public float PlateZ { get; private set; }
    public float GateOutline { get; private set; }
    public float LegOutline { get; private set; }

    Transform plate;
    Transform leftRail;
    Transform rightRail;
    RectTransform plateRect;
    RectTransform leftRect;
    RectTransform rightRect;
    Material unlit;
    MaterialPropertyBlock block;
    bool built;
    float heatNormalized;
    float heatPulse;

    public float HeatPulse => heatPulse;

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("ChuteContrast").AddComponent<ChuteContrast>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        TrackColor = new Color(0.1f, 0.09f, 0.11f, 1f);
        RailColor = new Color(1.45f, 1.22f, 0.78f, 1f);
        ApplyCamera();
        ApplyBloom(BloomScale());
        OutlineLabels();
        BuildTrack();
    }

    void Start()
    {
        ApplyCamera();
        OutlineLabels();
    }

    void LateUpdate()
    {
        ApplyCamera();
        Fit(plate, plateRect, 2.4f);
        Fit(leftRail, leftRect, 1.6f);
        Fit(rightRail, rightRect, 1.6f);
        ApplyHeatTint();
        if (leftRail != null && rightRail != null)
            RailSeparation = rightRail.position.x - leftRail.position.x;
        if (plate != null) PlateZ = plate.position.z;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetHeatPulse(float normalized, float pulse01)
    {
        heatNormalized = Mathf.Clamp01(normalized);
        heatPulse = Mathf.Clamp01(pulse01);
    }

    void ApplyHeatTint()
    {
        if (plate == null || block == null) return;
        var renderer = plate.GetComponent<MeshRenderer>();
        if (renderer == null) return;
        Color hot = new Color(0.48f, 0.05f, 0.07f, 1f);
        float wave = 0.55f + 0.45f * heatPulse;
        Color color = Color.Lerp(TrackColor, hot, heatNormalized * wave);
        block.Clear();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", hot * heatNormalized * (0.35f + heatPulse));
        renderer.SetPropertyBlock(block);
    }

    void ApplyCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = false;
        cam.fieldOfView = TargetFov;
        FieldOfView = cam.fieldOfView;
    }

    public void ApplyBloom(float multiplier)
    {
        float intensity = Mathf.Clamp(2f * multiplier, 0.25f, 3f);
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || !volume.isGlobal) continue;
            VolumeProfile profile = volume.profile;
            if (profile == null || !profile.TryGet(out Bloom bloom)) continue;
            bloom.active = true;
            bloom.intensity.Override(intensity);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.58f);
            if (profile.TryGet(out Vignette vignette))
            {
                vignette.active = true;
                vignette.intensity.Override(0.34f);
                vignette.smoothness.Override(0.46f);
                vignette.color.Override(new Color(0.02f, 0.015f, 0.02f, 1f));
            }
        }
    }

    static float BloomScale()
    {
        if (GameCoreManager.Instance == null || GameCoreManager.Instance.ActiveTheme == null) return 1f;
        return GameCoreManager.Instance.ActiveTheme.bloomIntensityMultiplier;
    }

    void OutlineLabels()
    {
        TextMeshProUGUI gate = FindLabel("GateProgress");
        TextMeshProUGUI leg = FindLabel("RunLegLabel");
        if (gate != null)
        {
            Stroke(gate);
            GateOutline = gate.outlineWidth;
        }

        if (leg != null)
        {
            Stroke(leg);
            LegOutline = leg.outlineWidth;
        }

        OutlinesOn = gate != null && leg != null && GateOutline > 0.15f && LegOutline > 0.15f;
    }

    static TextMeshProUGUI FindLabel(string name)
    {
        TextMeshProUGUI[] labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].gameObject.name == name) return labels[i];
        }

        return null;
    }

    static void Stroke(TextMeshProUGUI label)
    {
        label.fontStyle |= FontStyles.Bold;
        label.outlineWidth = 0.28f;
        label.outlineColor = new Color32(12, 8, 10, 255);
        label.extraPadding = true;
        if (label.fontMaterial != null)
        {
            label.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
            label.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(12f / 255f, 8f / 255f, 10f / 255f, 1f));
        }
    }

    void BuildTrack()
    {
        if (built) return;
        built = true;
        unlit = ItemCatalog.CreateUnlit();
        block = new MaterialPropertyBlock();
        GameObject chute = GameObject.Find("Chute");
        RectTransform host = chute != null ? chute.GetComponent<RectTransform>() : null;
        plateRect = Marker(host, "ChutePlateMarker", new Vector2(0f, -40f), new Vector2(620f, 1100f));
        leftRect = Marker(host, "ChuteRailLeft", new Vector2(-248f, -40f), new Vector2(26f, 1120f));
        rightRect = Marker(host, "ChuteRailRight", new Vector2(248f, -40f), new Vector2(26f, 1120f));
        plate = Bar("ChuteBackplate", TrackColor);
        leftRail = Bar("ChuteRailL", RailColor);
        rightRail = Bar("ChuteRailR", RailColor);
        TrackLive = plate != null && leftRail != null && rightRail != null;
    }

    static RectTransform Marker(RectTransform host, string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (host != null) go.transform.SetParent(host, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        go.transform.SetAsFirstSibling();
        return rect;
    }

    Transform Bar(string name, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(transform, true);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = unlit;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
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

    static void Fit(Transform target, RectTransform rect, float z)
    {
        if (target == null || rect == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 screen = rect.position;
        float depth = Mathf.Abs(z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        world.z = z;
        target.position = world;
        float worldHeight = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float screenH = Screen.height > 8 ? Screen.height : 1920f;
        float units = worldHeight / screenH;
        float width = rect.rect.width * Mathf.Abs(rect.lossyScale.x) * units;
        float height = rect.rect.height * Mathf.Abs(rect.lossyScale.y) * units;
        target.localScale = new Vector3(Mathf.Max(0.02f, width), Mathf.Max(0.02f, height), 0.05f);
    }
}
