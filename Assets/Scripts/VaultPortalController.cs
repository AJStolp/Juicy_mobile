using UnityEngine;

// Runtime door on the heist center track. The scene does not store this component.
public class VaultPortalController : MonoBehaviour
{
    [Header("Renderers & Materials")]
    [SerializeField] SpriteRenderer portalRenderer;
    [SerializeField] Material portalMaterial;

    [Header("State Sprites")]
    [SerializeField] Sprite closedSprite;
    [SerializeField] Sprite openingSprite;
    [SerializeField] Sprite openSprite;

    [Header("Emission Settings")]
    [ColorUsage(true, true)]
    [SerializeField] Color crimsonGlow = new Color(2f, 0.2f, 0.2f);
    [ColorUsage(true, true)]
    [SerializeField] Color indigoGlow = new Color(0.2f, 0.5f, 2f);

    Texture closedEmission;
    Texture openingEmission;
    Texture openEmission;
    Texture closedNormal;
    Texture openingNormal;
    Texture openNormal;

    bool open;
    bool indigo;
    float openingTime = -1f;
    const float OpeningBeat = 0.16f;
    EmissionPulse pulse;

    public void Configure(SpriteRenderer renderer, Material material, ThemeConfig theme)
    {
        portalRenderer = renderer;
        portalMaterial = material;
        closedSprite = theme.vaultClosedSprite;
        openingSprite = theme.vaultOpeningSprite;
        openSprite = theme.vaultOpenSprite;
        closedEmission = theme.vaultClosedEmission;
        openingEmission = theme.vaultOpeningEmission;
        openEmission = theme.vaultOpenEmission;
        closedNormal = theme.vaultClosedNormal;
        openingNormal = theme.vaultOpeningNormal;
        openNormal = theme.vaultOpenNormal;
        open = false;
        indigo = false;
        openingTime = -1f;
        Show(closedSprite, closedEmission, closedNormal);
        pulse = GetComponent<EmissionPulse>();
        if (pulse == null) pulse = gameObject.AddComponent<EmissionPulse>();
        pulse.Bind(portalMaterial, crimsonGlow);
        SetTargetColor(false);
    }

    void OnEnable()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnVaultCue -= OnVaultCue;
        GameCoreManager.Instance.OnVaultCue += OnVaultCue;
    }

    void OnDisable()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnVaultCue -= OnVaultCue;
    }

    void Update()
    {
        if (openingTime < 0f) return;
        openingTime += Time.deltaTime;
        if (openingTime < OpeningBeat) return;
        openingTime = -1f;
        Show(openSprite, openEmission, openNormal);
    }

    public void SetDoorState(bool isOpen)
    {
        OnVaultCue(isOpen, indigo);
    }

    public void SetTargetColor(bool isIndigo)
    {
        indigo = isIndigo;
        Color glow = isIndigo ? indigoGlow : crimsonGlow;
        if (pulse != null) pulse.SetBaseColor(glow);
        if (portalMaterial == null) return;
        portalMaterial.SetColor("_EmissionColor", glow);
    }

    void OnVaultCue(bool isOpen, bool isIndigo)
    {
        SetTargetColor(isIndigo);
        if (!isOpen)
        {
            open = false;
            openingTime = -1f;
            Show(closedSprite, closedEmission, closedNormal);
            Debug.Log("vault closed");
            return;
        }

        if (open) return;

        open = true;
        if (openingSprite == null)
        {
            openingTime = -1f;
            Show(openSprite, openEmission, openNormal);
        }
        else
        {
            openingTime = 0f;
            Show(openingSprite, openingEmission, openingNormal);
        }

        Debug.Log(isIndigo ? "vault open indigo" : "vault open crimson");
        JuicyAudioHooks.Vault();
    }

    void Show(Sprite sprite, Texture emission, Texture normalMap)
    {
        if (portalRenderer != null && sprite != null) portalRenderer.sprite = sprite;
        if (portalMaterial == null) return;
        if (emission != null) portalMaterial.SetTexture("_EmissionMap", emission);
        if (normalMap != null) portalMaterial.SetTexture("_NormalMap", normalMap);
    }
}

