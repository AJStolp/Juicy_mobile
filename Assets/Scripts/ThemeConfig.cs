using UnityEngine;

// One manifest per world. Heist and Climber live in Assets/Themes.
[CreateAssetMenu(fileName = "NewThemeConfig", menuName = "Architect/Theme Config")]
public class ThemeConfig : ScriptableObject
{
    public string themeName;
    public ItemCatalog.Theme catalogTheme = ItemCatalog.Theme.Heist;

    // Heist intake vaults are the baseline for every theme: riveted brass, beveled steel,
    // contact shadows baked into the albedo, and an emission map only where a surface should bloom.
    // Mesh props also carry a normal map for bolts and bevels. The background plate is a sprite,
    // so those bevels stay baked into the albedo. Juicy/TowerEmission does not sample normals.
    // Juicy/VaultPortal does, and only the heist door uses it.
    // plateSprite is a full bleed opaque portrait. The center bay stays darker than the side beams.
    // plateEmission stays black except for the overhead light shafts.
    // leftBinSprite and rightBinSprite swap the housing art. Catch boxes stay put.
    // vaultClosedSprite is the heist center track. Climber keeps its own steps.
    [Header("Environment & Tracks")]
    public Sprite backgroundSprite;
    public Sprite trackModuleSprite;
    public Material backgroundMaterial;
    public Material trackMaterial;
    public Texture2D plateEmission;
    public Sprite leftBinSprite;
    public Sprite rightBinSprite;

    [Header("Vault Portal")]
    public Sprite vaultClosedSprite;
    public Sprite vaultOpeningSprite;
    public Sprite vaultOpenSprite;
    public Texture2D vaultClosedEmission;
    public Texture2D vaultOpeningEmission;
    public Texture2D vaultOpenEmission;
    public Texture2D vaultClosedNormal;
    public Texture2D vaultOpeningNormal;
    public Texture2D vaultOpenNormal;

    [Header("Interactive Elements")]
    public Sprite containerSprite;
    public Sprite gemAtlasSprite;
    public Sprite targetVaultSprite;
    public Sprite plateSprite;
    public Sprite towerCapSprite;
    public Sprite towerTileSprite;
    public Sprite towerBaseSprite;
    public Texture2D towerCapEmission;
    public Texture2D towerTileEmission;
    public Texture2D towerBaseEmission;
    public Sprite[] collectibleSprites;

    [Header("Audio Bank")]
    public AudioClip themeMusic;
    public AudioClip ambientLoop;
    public AudioClip winJingle;
    public AudioClip impactSound;

    [Header("FX & Color Tokens")]
    public Color primaryNeonColor = new Color(1f, 221f / 255f, 0f, 1f);
    public Color emissionColor = new Color(0.62f, 0.22f, 1f, 1f);

    // Scales the chute bloom base of 0.7. The view clamps the result for the phone.
    [Range(0.5f, 1.6f)] public float bloomIntensityMultiplier = 1f;
}
