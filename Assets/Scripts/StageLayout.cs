using UnityEngine;
using UnityEngine.UI;

// Plate and tower live on sprites in the camera so bloom can read their
// emission. Overlay canvases in this URP are 8-bit and composite after bloom.
public class StageLayout : MonoBehaviour
{
    public static bool TowerReady { get; private set; }

    const float Distance = 8f;
    const float ReferenceHeight = 1920f;

    Camera view;
    Transform plate;
    Transform tower;

    public static void Ensure()
    {
        if (FindFirstObjectByType<StageLayout>(FindObjectsInactive.Include) != null) return;
        new GameObject("StageLayout").AddComponent<StageLayout>();
    }

    static ThemeConfig ActiveTheme()
    {
        return GameCoreManager.Instance != null ? GameCoreManager.Instance.ActiveTheme : null;
    }

    void OnEnable()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnThemeChanged -= Build;
        GameCoreManager.Instance.OnThemeChanged += Build;
    }

    void OnDisable()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnThemeChanged -= Build;
    }

    void Start()
    {
        Build(ActiveTheme());
    }

    void LateUpdate()
    {
        if (view == null) view = Camera.main;
        if (view == null) return;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(0.07f, 0.065f, 0.06f, 1f);
    }

    void Build(ThemeConfig theme)
    {
        view = Camera.main;
        if (view == null) return;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(0.07f, 0.065f, 0.06f, 1f);
        SeatHud();
        float frustum = 2f * Distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        BuildPlate(theme, frustum);
        BuildTower(theme, frustum);
    }

    void SeatHud()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            VaultHeistController.SeatOnLayer(canvas.gameObject, "UI", 0);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) continue;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    void BuildPlate(ThemeConfig theme, float frustum)
    {
        Sprite sprite = theme != null ? theme.plateSprite : null;
        if (sprite == null) return;

        plate = Fresh("StagePlate", view.transform);
        plate.localPosition = new Vector3(0f, 0f, Distance + 0.6f);
        plate.localRotation = Quaternion.identity;
        float width = sprite.rect.width / sprite.pixelsPerUnit;
        float height = sprite.rect.height / sprite.pixelsPerUnit;
        float frustumWidth = frustum * view.aspect;
        float scale = Mathf.Max(frustumWidth / width, frustum / height) * 1.2f;
        plate.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer renderer = plate.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = 0;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Texture emission = theme.plateEmission;
        float gain = emission != null ? 0.32f : 0f;
        Color shaft = new Color(1.15f, 0.92f, 0.48f, 1f);
        Material material = ModuleMaterial(sprite.texture, emission, shaft, gain);
        if (material != null) renderer.sharedMaterial = material;
    }

    void BuildTower(ThemeConfig theme, float frustum)
    {
        TowerReady = false;
        if (theme == null) return;
        if (theme.vaultClosedSprite != null && theme.vaultOpenSprite != null)
        {
            BuildPortal(theme, frustum);
            return;
        }

        if (theme.towerCapSprite == null || theme.towerTileSprite == null || theme.towerBaseSprite == null)
            return;

        Shader shader = Shader.Find("Juicy/TowerEmission");
        if (shader == null) return;

        tower = Fresh("StageTower", view.transform);
        float span = VaultHeistController.TowerTop - VaultHeistController.TowerBottom;
        float center = frustum * (VaultHeistController.DoorAnchoredY / ReferenceHeight);
        tower.localPosition = new Vector3(0f, center, Distance);
        tower.localRotation = Quaternion.identity;

        Color emission = EmissionColor(theme);
        float capH = SpriteHeight(theme.towerCapSprite);
        float tileH = SpriteHeight(theme.towerTileSprite);
        float baseH = SpriteHeight(theme.towerBaseSprite);
        float total = capH + tileH + baseH;
        float overlap = 1.5f / theme.towerCapSprite.pixelsPerUnit;
        AddModule("Cap", theme.towerCapSprite, theme.towerCapEmission, emission,
            -total * 0.5f + baseH + tileH + capH * 0.5f - overlap, 2);
        AddModule("Tile", theme.towerTileSprite, theme.towerTileEmission, emission,
            -total * 0.5f + baseH + tileH * 0.5f, 1);
        AddModule("Base", theme.towerBaseSprite, theme.towerBaseEmission, emission,
            -total * 0.5f + baseH * 0.5f + overlap, 0);

        float worldSpan = frustum * (span / ReferenceHeight);
        float uniform = total > 0.001f ? worldSpan / total : 1f;
        tower.localScale = new Vector3(uniform, uniform, 1f);
        TowerReady = true;
    }

    void BuildPortal(ThemeConfig theme, float frustum)
    {
        Sprite sprite = theme.vaultClosedSprite;
        tower = Fresh("StageTower", view.transform);
        float span = VaultHeistController.TowerTop - VaultHeistController.TowerBottom;
        float center = frustum * (VaultHeistController.DoorAnchoredY / ReferenceHeight);
        tower.localPosition = new Vector3(0f, center, Distance);
        tower.localRotation = Quaternion.identity;

        float height = SpriteHeight(sprite);
        float worldSpan = frustum * (span / ReferenceHeight);
        float uniform = height > 0.001f ? worldSpan / height : 1f;
        tower.localScale = new Vector3(uniform, uniform, 1f);

        SpriteRenderer renderer = tower.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Tracks";
        renderer.sortingOrder = 1;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Color crimson = new Color(2f, 0.2f, 0.2f, 1f);
        Material material = PortalMaterial(sprite.texture, theme.vaultClosedEmission, theme.vaultClosedNormal, crimson);
        if (material != null) renderer.sharedMaterial = material;

        VaultPortalController portal = tower.gameObject.AddComponent<VaultPortalController>();
        portal.Configure(renderer, material, theme);
        TowerReady = true;
    }

    static Material PortalMaterial(Texture albedo, Texture emission, Texture normalMap, Color emissionColor)
    {
        Shader shader = Shader.Find("Juicy/VaultPortal");
        if (shader == null) return ModuleMaterial(albedo, emission, emissionColor, 0.7f);
        Material material = new Material(shader);
        if (albedo != null) material.SetTexture("_MainTex", albedo);
        if (emission != null) material.SetTexture("_EmissionMap", emission);
        if (normalMap != null) material.SetTexture("_NormalMap", normalMap);
        material.SetColor("_EmissionColor", emissionColor);
        material.SetFloat("_EmissionGain", 0.7f);
        material.SetFloat("_Cutoff", 0.15f);
        material.SetFloat("_NormalStrength", 0.75f);
        return material;
    }

    void AddModule(string moduleName, Sprite sprite, Texture2D emission, Color emissionColor, float localY, int order)
    {
        GameObject module = new GameObject(moduleName);
        module.transform.SetParent(tower, false);
        module.transform.localPosition = new Vector3(0f, localY, 0f);
        module.transform.localRotation = Quaternion.identity;
        module.transform.localScale = Vector3.one;
        SpriteRenderer renderer = module.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Tracks";
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Material material = ModuleMaterial(sprite.texture, emission, emissionColor, 1f);
        if (material != null) renderer.sharedMaterial = material;
    }

    static Material ModuleMaterial(Texture albedo, Texture emission, Color emissionColor, float gain)
    {
        Shader shader = Shader.Find("Juicy/TowerEmission");
        if (shader == null) return null;
        Material material = new Material(shader);
        if (albedo != null) material.SetTexture("_MainTex", albedo);
        if (emission != null) material.SetTexture("_EmissionMap", emission);
        material.SetColor("_EmissionColor", emissionColor);
        material.SetFloat("_EmissionGain", gain);
        material.SetFloat("_Cutoff", gain > 0f ? 0.15f : 0.01f);
        return material;
    }

    static Color EmissionColor(ThemeConfig theme)
    {
        if (theme.backgroundMaterial != null && theme.backgroundMaterial.HasProperty("_EmissionColor"))
            return theme.backgroundMaterial.GetColor("_EmissionColor");
        return theme.emissionColor;
    }

    static float SpriteHeight(Sprite sprite)
    {
        return sprite.rect.height / sprite.pixelsPerUnit;
    }

    static Transform Fresh(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            existing.name = objectName + "_retired";
            Destroy(existing.gameObject);
        }

        GameObject created = new GameObject(objectName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }
}
