using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The level's goal and result, drawn as text just in front of the camera so
// every capture of the camera includes it. Top: gems sorted against the
// target and the goal's limit. When an attempt ends: won with the room to
// spare, or how many gems short, a tap to try again, and the three goals to
// switch between (prototype, T-028).
public class AlleyLevelHud : MonoBehaviour
{
    static readonly LevelGoal[] Goals = { LevelGoal.Heat, LevelGoal.Clock, LevelGoal.Gems };
    static readonly string[] GoalNames = { "HEAT", "CLOCK", "GEMS" };
    static readonly Color Neon = new Color(1f, 0.85f, 0.25f);
    static readonly Color Win = new Color(0.35f, 1f, 0.45f);
    static readonly Color Fail = new Color(1f, 0.28f, 0.3f);
    static readonly Color Idle = new Color(0.75f, 0.75f, 0.8f);

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
    readonly TextMeshPro[] goalTexts = new TextMeshPro[3];
    readonly Renderer[] goalBacks = new Renderer[3];
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
        Quad(panel.transform, "Back", new Vector2(0.5f, 0.52f), new Vector2(0.86f, 0.34f), new Color(0.02f, 0.02f, 0.05f, 0.82f), 0.002f);
        titleText = Text("Title", new Vector2(0.5f, 0.64f), new Vector2(0.8f, 0.07f), Win, panel.transform);
        detailText = Text("Detail", new Vector2(0.5f, 0.575f), new Vector2(0.8f, 0.04f), Color.white, panel.transform);
        retryText = Text("Retry", new Vector2(0.5f, 0.505f), new Vector2(0.8f, 0.035f), Neon, panel.transform);
        retryText.text = "TAP TO TRY AGAIN";
        for (int i = 0; i < Goals.Length; i++)
        {
            Vector2 at = new Vector2(0.24f + i * 0.26f, 0.415f);
            goalBacks[i] = Quad(panel.transform, "Goal_" + GoalNames[i], at, new Vector2(0.23f, 0.055f), Idle, 0.001f);
            goalTexts[i] = Text("Goal_" + GoalNames[i] + "_Label", at, new Vector2(0.2f, 0.035f), Color.black, panel.transform);
            goalTexts[i].text = GoalNames[i];
        }
        panel.SetActive(false);

        director.AttemptEnded += ShowResult;
        director.AttemptStarted += _ => panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (director == null) return;
        director.AttemptEnded -= ShowResult;
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
        limitText.text = Limit(level);
        limitText.color = Danger(level) ? Fail : Color.white;
        if (ShowingResult) TickChoice();
    }

    string Limit(LevelRules level)
    {
        switch (level.Goal)
        {
            case LevelGoal.Heat: return "HEAT " + Mathf.RoundToInt(director.Rules.Heat) + " / 100";
            case LevelGoal.Clock: return "TIME " + Clock(level.TimeLeft);
            default: return "GEMS LEFT TO FALL " + level.GemsLeft;
        }
    }

    bool Danger(LevelRules level)
    {
        switch (level.Goal)
        {
            case LevelGoal.Heat: return director.Rules.Heat >= 60f;
            case LevelGoal.Clock: return level.TimeLeft <= 20f;
            default: return level.GemsLeft + level.Sorted < level.Target + 10;
        }
    }

    static string Clock(float seconds)
    {
        int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return (whole / 60) + ":" + (whole % 60).ToString("00");
    }

    void ShowResult(LevelRules level)
    {
        bool won = level.State == LevelState.Won;
        titleText.text = won ? "LEVEL WON" : FailTitle(level.Goal);
        titleText.color = won ? Win : Fail;
        detailText.text = won ? WinDetail(level) : level.ShortBy + (level.ShortBy == 1 ? " GEM SHORT" : " GEMS SHORT");
        for (int i = 0; i < Goals.Length; i++)
        {
            block.SetColor("_BaseColor", Goals[i] == level.Goal ? Neon : Idle);
            goalBacks[i].SetPropertyBlock(block);
        }
        panel.SetActive(true);
        shownAt = Time.unscaledTime;
    }

    static string FailTitle(LevelGoal goal)
    {
        switch (goal)
        {
            case LevelGoal.Heat: return "BUSTED";
            case LevelGoal.Clock: return "OUT OF TIME";
            default: return "OUT OF GEMS";
        }
    }

    static string WinDetail(LevelRules level)
    {
        switch (level.Goal)
        {
            case LevelGoal.Heat: return "CLOSEST CALL " + Mathf.RoundToInt(level.PeakHeat) + " HEAT";
            case LevelGoal.Clock: return Clock(level.TimeLeft) + " TO SPARE";
            default: return level.GemsLeft + (level.GemsLeft == 1 ? " GEM TO SPARE" : " GEMS TO SPARE");
        }
    }

    // A tap on a goal tries that goal; a tap anywhere else tries this one again.
    void TickChoice()
    {
        // The swipe that ended the attempt must not also start the next one.
        if (director.AutoPlay || Time.unscaledTime < shownAt + 0.6f) return;
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasReleasedThisFrame) return;
        Vector2 tap = pointer.position.ReadValue();
        LevelGoal next = director.Goal;
        for (int i = 0; i < Goals.Length; i++)
            if (ScreenRect(goalBacks[i]).Contains(tap)) next = Goals[i];
        director.Retry(next);
    }

    Rect ScreenRect(Renderer renderer)
    {
        Bounds b = renderer.bounds;
        Vector3 min = view.WorldToScreenPoint(b.min);
        Vector3 max = view.WorldToScreenPoint(b.max);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
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
