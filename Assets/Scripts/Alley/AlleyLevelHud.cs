using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The level's goal and result, drawn as text just in front of the camera so
// every capture of the camera includes it. Top: gems sorted against the
// target, and how many more gems the player can lose and still win. When an
// attempt ends: won with the misses to spare, or how many gems short, and a
// tap to try again.
public class AlleyLevelHud : MonoBehaviour
{
    static readonly Color Neon = new Color(1f, 0.85f, 0.25f);
    static readonly Color Win = new Color(0.35f, 1f, 0.45f);
    static readonly Color Fail = new Color(1f, 0.28f, 0.3f);

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Material panelMaterial;

    const float Distance = 0.4f;

    Camera view;
    TextMeshPro gemsText;
    TextMeshPro limitText;
    GameObject panel;
    TextMeshPro titleText;
    TextMeshPro detailText;
    TextMeshPro retryText;
    TextMeshPro bannerText;
    TextMeshPro hintText;
    float bannerUntil;
    MaterialPropertyBlock block;
    float punch;
    int lastSorted;
    float shownAt;

    public bool ShowingResult => panel != null && panel.activeSelf;

    public void Configure(AlleyChuteDirector chute, Material material)
    {
        director = chute;
        panelMaterial = material;
    }

    void Start()
    {
        view = Camera.main;
        block = new MaterialPropertyBlock();
        transform.SetParent(view.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        gemsText = Text("Gems", new Vector2(0.5f, 0.935f), new Vector2(0.9f, 0.06f), Neon);
        limitText = Text("Limit", new Vector2(0.5f, 0.885f), new Vector2(0.9f, 0.04f), Color.white);

        panel = new GameObject("Result_Panel");
        panel.transform.SetParent(transform, false);
        Quad(panel.transform, "Back", new Vector2(0.5f, 0.57f), new Vector2(0.86f, 0.24f), new Color(0.02f, 0.02f, 0.05f, 0.82f), 0.002f);
        titleText = Text("Title", new Vector2(0.5f, 0.64f), new Vector2(0.8f, 0.07f), Win, panel.transform);
        detailText = Text("Detail", new Vector2(0.5f, 0.575f), new Vector2(0.8f, 0.04f), Color.white, panel.transform);
        retryText = Text("Retry", new Vector2(0.5f, 0.505f), new Vector2(0.8f, 0.035f), Neon, panel.transform);
        retryText.text = "TAP TO TRY AGAIN";
        panel.SetActive(false);

        // The level's name, and its hint when it brings in something new.
        bannerText = Text("Banner", new Vector2(0.5f, 0.66f), new Vector2(0.8f, 0.06f), Neon);
        hintText = Text("Hint", new Vector2(0.5f, 0.6f), new Vector2(0.86f, 0.04f), Color.white);

        director.AttemptEnded += ShowResult;
        director.AttemptStarted += ShowBanner;
        ShowBanner(director.Level);
    }

    const float BannerSeconds = 3f;

    void ShowBanner(LevelRules level)
    {
        panel.SetActive(false);
        if (level == null) return;
        bannerText.text = level.Spec.Name.ToUpperInvariant();
        hintText.text = level.Spec.Hint.ToUpperInvariant();
        bannerUntil = Time.time + BannerSeconds;
    }

    void OnDestroy()
    {
        if (director == null) return;
        director.AttemptEnded -= ShowResult;
        director.AttemptStarted -= ShowBanner;
    }

    void Update()
    {
        LevelRules level = director.Level;
        if (level == null) return;
        if (level.Sorted != lastSorted)
        {
            if (level.Sorted > lastSorted) punch = 1f;
            lastSorted = level.Sorted;
        }
        punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 5f);
        gemsText.text = "GEMS " + level.Sorted + " / " + level.Target;
        gemsText.transform.localScale = Vector3.one * (1f + punch * 0.08f);
        limitText.text = "MISSES LEFT " + level.MissesLeft;
        limitText.color = level.MissesLeft <= 3 ? Fail : Color.white;
        bool banner = Time.time < bannerUntil && !ShowingResult;
        bannerText.gameObject.SetActive(banner);
        hintText.gameObject.SetActive(banner && hintText.text.Length > 0);
        if (ShowingResult) TickRetry();
    }

    void ShowResult(LevelRules level)
    {
        bool won = level.State == LevelState.Won;
        titleText.text = won ? "LEVEL WON" : "TOO MANY MISSES";
        titleText.color = won ? Win : Fail;
        detailText.text = won
            ? level.MissesLeft + (level.MissesLeft == 1 ? " MISS TO SPARE" : " MISSES TO SPARE")
            : level.ShortBy + (level.ShortBy == 1 ? " GEM SHORT" : " GEMS SHORT");
        panel.SetActive(true);
        shownAt = Time.unscaledTime;
    }

    // A tap anywhere tries the level again.
    void TickRetry()
    {
        // The swipe that ended the attempt must not also start the next one.
        if (director.AutoPlay || Time.unscaledTime < shownAt + 0.6f) return;
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasReleasedThisFrame) return;
        director.Retry();
    }

    // Size of the view at the HUD's distance, in metres. The game is portrait,
    // so the layout never takes a wider aspect than 9:16 (a batch run's screen
    // is landscape even though its captures are portrait).
    Vector2 ViewSize()
    {
        float height = 2f * Distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return new Vector2(height * Mathf.Min(view.aspect, 9f / 16f), height);
    }

    Vector3 Place(Vector2 viewport, float back)
    {
        Vector2 size = ViewSize();
        return new Vector3((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y, Distance + back);
    }

    TextMeshPro Text(string name, Vector2 viewport, Vector2 extent, Color color, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.localPosition = Place(viewport, 0f);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        Vector2 size = ViewSize();
        text.rectTransform.sizeDelta = new Vector2(extent.x * size.x, extent.y * size.y);
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.01f;
        text.fontSizeMax = 4f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        text.sortingOrder = 10;
        return text;
    }

    Renderer Quad(Transform parent, string name, Vector2 viewport, Vector2 extent, Color color, float back)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = Place(viewport, back);
        Vector2 size = ViewSize();
        quad.transform.localScale = new Vector3(extent.x * size.x, extent.y * size.y, 1f);
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = panelMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
        return renderer;
    }
}
