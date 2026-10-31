using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Vault door state, gem drops down the chute, and the open glow.
[DefaultExecutionOrder(200)]
public class VaultHeistController : MonoBehaviour
{
    public static VaultHeistController Instance { get; private set; }

    [Header("UI & Vault References")]
    public Animator vaultDoorAnimator;
    public GameObject rubyPrefab;
    public GameObject topazPrefab;
    public Transform dropLaneSpawnPoint;

    [Header("Lighting & Particle FX")]
    public ParticleSystem vaultGlowParticles;
    public Light vaultGoldenLight;

    public const float TowerTop = 580f;
    public const float TowerBottom = -250f;
    public const float DoorScale = (TowerTop - TowerBottom) / 504f;
    public const float DoorAnchoredY = (TowerTop + TowerBottom) * 0.5f;

    const float DropSpeed = 700f;
    const float DropFloor = -520f;

    readonly List<RectTransform> drops = new List<RectTransform>(8);
    Image glowImage;
    Sprite themeFace;
    bool open;
    int previewFrame;

    public bool IsOpen => open;

    public static void Ensure()
    {
        if (Instance != null) return;
        VaultHeistController existing = FindFirstObjectByType<VaultHeistController>(FindObjectsInactive.Include);
        if (existing != null) return;
        new GameObject("VaultHeistManager").AddComponent<VaultHeistController>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (rubyPrefab == null) rubyPrefab = Resources.Load<GameObject>("Vault/RubyGemDrop");
        if (topazPrefab == null) topazPrefab = Resources.Load<GameObject>("Vault/TopazGemDrop");
        CacheGlow();
    }

    void OnEnable()
    {
        WatchTheme();
    }

    void OnDisable()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnThemeChanged -= ApplyTheme;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        WatchTheme();
        PlaceDoor();
        SeatBehindHud();
        StageLayout.Ensure();
        SetVaultState(false);
    }

    public static void SeatOnLayer(GameObject target, string layer, int order)
    {
        if (target == null) return;
        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas == null) canvas = target.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.Normal
            | AdditionalCanvasShaderChannels.Tangent;
        int layerId;
        if (TryLayer(layer, out layerId))
        {
            canvas.sortingLayerID = layerId;
            canvas.sortingOrder = order;
            return;
        }

        canvas.sortingLayerID = 0;
        canvas.sortingOrder = FallbackOrder(layer, order);
    }

    static bool TryLayer(string layer, out int id)
    {
        id = 0;
        SortingLayer[] layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name != layer) continue;
            id = layers[i].id;
            return true;
        }
        return false;
    }

    static int FallbackOrder(string layer, int order)
    {
        if (layer == "Background") return -40 + order;
        if (layer == "Tracks") return -20 + order;
        if (layer == "Gameplay") return 20 + order;
        if (layer == "UI") return 40 + order;
        return order;
    }

    void PlaceDoor()
    {
        if (vaultDoorAnimator == null) return;
        Transform door = vaultDoorAnimator.transform;
        if (door.parent != null && door.parent.name == "VaultDoor2D")
            door = door.parent;
        RectTransform rect = door as RectTransform;
        if (rect != null)
        {
            rect.localScale = new Vector3(DoorScale, DoorScale, DoorScale);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, DoorAnchoredY);
        }
        SeatOnLayer(door.gameObject, "Tracks", 4);
    }

    void LateUpdate()
    {
        if (!open) ApplyFace();
    }

    void Update()
    {
        TickDrops();
#if UNITY_EDITOR
        TickPreview();
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            SetVaultState(!open);
#endif
    }

#if UNITY_EDITOR
    void TickPreview()
    {
        const string flag = "/tmp/juicy-show-vault";
        if (!File.Exists(flag)) return;
        previewFrame++;
        if (previewFrame == 24)
        {
            SetVaultState(true);
            SpawnGemPrefab(true);
        }

        if (previewFrame != 48) return;
        File.WriteAllText("/tmp/juicy-vault-preview.txt", "open " + open + "\n");
        ScreenCapture.CaptureScreenshot("/tmp/juicy-vault-game.png");
        File.Delete(flag);
    }
#endif

    // Opens or closes the door. IsOpen drives the wheel and keyhole clip.
    public void SetVaultState(bool isOpen)
    {
        bool changed = open != isOpen;
        open = isOpen;

        if (vaultDoorAnimator != null && vaultDoorAnimator.runtimeAnimatorController != null)
            vaultDoorAnimator.SetBool("IsOpen", isOpen);

        if (vaultGoldenLight != null)
            vaultGoldenLight.intensity = isOpen ? 8.5f : 2.0f;

        if (vaultGlowParticles != null)
        {
            if (isOpen) vaultGlowParticles.Play();
            else vaultGlowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        CacheGlow();
        if (glowImage != null)
        {
            Color color = glowImage.color;
            color.a = isOpen ? 0.42f : 0f;
            glowImage.color = color;
        }

        if (changed)
            Debug.Log(isOpen ? "Vault door opening." : "Vault door closed.");
    }

    void WatchTheme()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnThemeChanged -= ApplyTheme;
        GameCoreManager.Instance.OnThemeChanged += ApplyTheme;
        if (GameCoreManager.Instance.ActiveTheme != null)
            ApplyTheme(GameCoreManager.Instance.ActiveTheme);
    }

    public void ApplyTheme(ThemeConfig theme)
    {
        if (theme == null) return;
        if (vaultGoldenLight != null)
            vaultGoldenLight.color = theme.primaryNeonColor;
        CacheGlow();
        if (glowImage != null)
        {
            Color color = theme.emissionColor;
            color.a = open ? 0.42f : 0f;
            glowImage.color = color;
        }

        themeFace = theme.targetVaultSprite != null ? theme.targetVaultSprite : theme.backgroundSprite;
        ApplyFace();
    }

    // The door clip writes the grey vault sprite every frame. Put the theme
    // painting back after the animator while the door is closed.
    void ApplyFace()
    {
        Image face = FaceImage();
        if (face == null) return;
        if (StageLayout.TowerReady)
        {
            Color hidden = face.color;
            hidden.a = 0f;
            face.color = hidden;
            face.raycastTarget = false;
            return;
        }

        if (themeFace == null) return;
        face.sprite = themeFace;
        face.preserveAspect = true;
        face.color = Color.white;
    }

    Image FaceImage()
    {
        if (vaultDoorAnimator != null)
        {
            Image image = vaultDoorAnimator.GetComponent<Image>();
            if (image != null) return image;
        }

        GameObject face = GameObject.Find("VaultFace");
        return face != null ? face.GetComponent<Image>() : null;
    }

    // Drops a ruby or topaz still down the center lane.
    public void SpawnGemPrefab(bool isRuby)
    {
        GameObject prefabToSpawn = isRuby ? rubyPrefab : topazPrefab;
        if (prefabToSpawn == null || dropLaneSpawnPoint == null) return;

        GameObject gem = Instantiate(prefabToSpawn, dropLaneSpawnPoint.position, Quaternion.identity);
        RectTransform spawned = gem.GetComponent<RectTransform>();
        RectTransform lane = dropLaneSpawnPoint as RectTransform;
        if (spawned == null || lane == null || lane.parent == null) return;

        spawned.SetParent(lane.parent, false);
        spawned.anchoredPosition = lane.anchoredPosition;
        spawned.localRotation = Quaternion.identity;
        spawned.localScale = Vector3.one;
        drops.Add(spawned);
    }

    void TickDrops()
    {
        float step = DropSpeed * Time.deltaTime;
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            RectTransform drop = drops[i];
            if (drop == null)
            {
                drops.RemoveAt(i);
                continue;
            }

            Vector2 position = drop.anchoredPosition;
            position.y -= step;
            drop.anchoredPosition = position;
            if (position.y > DropFloor) continue;
            Destroy(drop.gameObject);
            drops.RemoveAt(i);
        }
    }

    void CacheGlow()
    {
        if (glowImage != null || vaultDoorAnimator == null) return;
        Transform search = vaultDoorAnimator.transform;
        Transform child = search.Find("VaultGlow");
        if (child == null && search.parent != null)
            child = search.parent.Find("VaultGlow");
        if (child != null) glowImage = child.GetComponent<Image>();
    }

    void SeatBehindHud()
    {
        if (vaultDoorAnimator == null) return;
        Transform door = vaultDoorAnimator.transform;
        if (door.parent != null && door.parent.name == "VaultDoor2D")
            door = door.parent;
        if (door.parent == null) return;
        int index = Mathf.Min(2, door.parent.childCount - 1);
        door.SetSiblingIndex(index);
    }
}
