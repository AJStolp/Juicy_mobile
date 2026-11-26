using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The level's goal and result, drawn just in front of the camera
// (CameraOverlay). Top: gems sorted against the target, and how many more gems
// the player can lose and still win. When an attempt ends: won with the misses
// to spare, or how many gems short. A tap after a win moves on to the clue
// reveal or the map; a tap after a fail tries the level again. Hidden while
// the reveal or the map is up.
public class AlleyLevelHud : MonoBehaviour
{
    static readonly Color Neon = new Color(1f, 0.85f, 0.25f);
    static readonly Color Win = new Color(0.35f, 1f, 0.45f);
    static readonly Color Fail = new Color(1f, 0.28f, 0.3f);

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Material panelMaterial;

    const float Distance = 0.4f;

    CameraOverlay overlay;
    GameObject content;
    TextMeshPro gemsText;
    TextMeshPro limitText;
    GameObject panel;
    TextMeshPro titleText;
    TextMeshPro detailText;
    TextMeshPro retryText;
    // The fail panel's choices: keep going for cash, try again, or the map.
    GameObject failButtons;
    Renderer keepGoingHit;
    Renderer tryAgainHit;
    Renderer mapHit;
    TextMeshPro keepGoingText;
    TextMeshPro livesNote;
    static readonly Color ButtonBack = new Color(1f, 1f, 1f, 0.12f);
    TextMeshPro bannerText;
    TextMeshPro hintText;
    float bannerUntil;
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
        Camera view = Camera.main;
        overlay = new CameraOverlay(view, panelMaterial, Distance, CameraOverlay.LayerHud);
        transform.SetParent(view.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        content = new GameObject("Content");
        content.transform.SetParent(transform, false);
        Transform root = content.transform;

        gemsText = overlay.Text(root, "Gems", new Vector2(0.5f, 0.935f), new Vector2(0.9f, 0.06f), Neon);
        limitText = overlay.Text(root, "Limit", new Vector2(0.5f, 0.885f), new Vector2(0.9f, 0.04f), Color.white);

        panel = new GameObject("Result_Panel");
        panel.transform.SetParent(root, false);
        overlay.Quad(panel.transform, "Back", new Vector2(0.5f, 0.535f), new Vector2(0.86f, 0.34f), new Color(0.02f, 0.02f, 0.05f, 0.88f), 0.003f);
        titleText = overlay.Text(panel.transform, "Title", new Vector2(0.5f, 0.655f), new Vector2(0.8f, 0.065f), Win);
        detailText = overlay.Text(panel.transform, "Detail", new Vector2(0.5f, 0.6f), new Vector2(0.8f, 0.035f), Color.white);
        retryText = overlay.Text(panel.transform, "Retry", new Vector2(0.5f, 0.52f), new Vector2(0.8f, 0.035f), Neon);
        failButtons = new GameObject("Fail_Buttons");
        failButtons.transform.SetParent(panel.transform, false);
        Transform buttons = failButtons.transform;
        keepGoingHit = overlay.Quad(buttons, "Keep_Going", new Vector2(0.5f, 0.535f), new Vector2(0.76f, 0.06f), Neon, 0.002f);
        keepGoingText = overlay.Text(buttons, "Keep_Going_Label", new Vector2(0.5f, 0.535f), new Vector2(0.72f, 0.024f), Color.black);
        keepGoingText.outlineWidth = 0f;
        tryAgainHit = overlay.Quad(buttons, "Try_Again", new Vector2(0.31f, 0.455f), new Vector2(0.37f, 0.055f), ButtonBack, 0.002f);
        overlay.Text(buttons, "Try_Again_Label", new Vector2(0.31f, 0.455f), new Vector2(0.32f, 0.028f), Color.white).text = "TRY AGAIN";
        mapHit = overlay.Quad(buttons, "Map", new Vector2(0.69f, 0.455f), new Vector2(0.37f, 0.055f), ButtonBack, 0.002f);
        overlay.Text(buttons, "Map_Label", new Vector2(0.69f, 0.455f), new Vector2(0.32f, 0.028f), Color.white).text = "MAP";
        livesNote = overlay.Text(buttons, "Lives_Note", new Vector2(0.5f, 0.395f), new Vector2(0.76f, 0.022f), new Color(1f, 1f, 1f, 0.75f));
        panel.SetActive(false);

        // The level's name, and its hint when it brings in something new.
        bannerText = overlay.Text(root, "Banner", new Vector2(0.5f, 0.66f), new Vector2(0.8f, 0.06f), Neon);
        hintText = overlay.Text(root, "Hint", new Vector2(0.5f, 0.6f), new Vector2(0.86f, 0.04f), Color.white);

        director.AttemptEnded += ShowResult;
        director.AttemptStarted += ShowBanner;
        director.MapOpened += Hide;
        director.RevealOpened += HideForReveal;
        director.FinaleStarted += HideForFinale;
        director.AttemptResumed += OnResumed;
        ShowBanner(director.Level);
        if (director.UsesMap) Hide();
    }

    void Hide() => content.SetActive(false);

    const float BannerSeconds = 3f;

    void ShowBanner(LevelRules level)
    {
        content.SetActive(true);
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
        director.MapOpened -= Hide;
        director.RevealOpened -= HideForReveal;
        director.FinaleStarted -= HideForFinale;
        director.AttemptResumed -= OnResumed;
    }

    // Kept going: the panel goes and play carries on where it stopped.
    void OnResumed() => panel.SetActive(false);

    void HideForReveal(int level, Clue clue) => Hide();

    // The keypad's case file takes the top of the screen during the finale.
    void HideForFinale(VaultFinale finale) => Hide();

    void Update()
    {
        LevelRules level = director.Level;
        if (level == null || !content.activeSelf) return;
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
        content.SetActive(true);
        bool won = level.State == LevelState.Won;
        // A cracked vault already plays its own boom and chord.
        if (!won) JuicyAudioHooks.Failed();
        else if (!director.IsBoss) JuicyAudioHooks.Won();
        titleText.color = won ? Win : Fail;
        if (director.IsBoss && won)
        {
            titleText.text = "VAULT CRACKED";
            detailText.text = "+$" + AlleyChuteDirector.VaultPrize.ToString("#,0");
        }
        else if (director.IsBoss && level.FailedAtFinale)
        {
            titleText.text = "ALARM TRIPPED";
            detailText.text = director.Finale.Step + " OF " + director.Finale.Count + " CLUES ENTERED";
        }
        else
        {
            titleText.text = won ? "LEVEL WON" : "TOO MANY MISSES";
            detailText.text = won
                ? level.MissesLeft + (level.MissesLeft == 1 ? " MISS TO SPARE" : " MISSES TO SPARE")
                : level.ShortBy + (level.ShortBy == 1 ? " GEM SHORT" : " GEMS SHORT");
        }
        retryText.text = won && (director.UsesMap || director.UsesReveal) ? "TAP TO CONTINUE" : "TAP TO TRY AGAIN";
        // A fail on the map's flow offers its choices; a win, or play
        // without the map (auto play), still taps on through.
        bool choices = !won && director.UsesMap;
        retryText.gameObject.SetActive(!choices);
        failButtons.SetActive(choices);
        if (choices) RefreshChoices();
        panel.SetActive(true);
        shownAt = Time.unscaledTime;
    }

    bool CanOfferKeepGoing => !director.Level.FailedAtFinale;

    void RefreshChoices()
    {
        bool offer = CanOfferKeepGoing;
        keepGoingHit.gameObject.SetActive(offer);
        keepGoingText.gameObject.SetActive(offer);
        if (offer)
        {
            bool afford = director.CanKeepGoing;
            keepGoingText.text = (afford ? "KEEP GOING  +" : "NEED ") + (afford ? AlleyChuteDirector.KeepGoingGems + " GEMS  $" : "$") +
                director.KeepGoingPrice.ToString("#,0") + (afford ? "" : " TO KEEP GOING");
            overlay.Tint(keepGoingHit, afford ? Neon : new Color(0.4f, 0.4f, 0.42f));
        }
        int lives = director.LivesLeft;
        livesNote.text = "LIVES " + lives + (director.CanKeepGoing ? ". KEEP GOING RETURNS 1" : "");
    }

    // After a win, a tap anywhere goes on. After a fail, the buttons:
    // keep going, try again, or back to the map.
    void TickRetry()
    {
        // The swipe that ended the attempt must not also start the next one.
        if (director.AutoPlay || Time.unscaledTime < shownAt + 0.6f) return;
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasReleasedThisFrame) return;
        if (!failButtons.activeSelf)
        {
            Continue();
            return;
        }
        Vector2 tap = pointer.position.ReadValue();
        if (keepGoingHit.gameObject.activeSelf && overlay.ScreenRect(keepGoingHit).Contains(tap)) KeepGoing();
        else if (overlay.ScreenRect(tryAgainHit).Contains(tap)) TryAgain();
        else if (overlay.ScreenRect(mapHit).Contains(tap)) ToMap();
    }

    public bool KeepGoing()
    {
        if (!ShowingResult || !director.KeepGoing()) return false;
        JuicyAudioHooks.Cash();
        return true;
    }

    public void TryAgain()
    {
        if (!ShowingResult) return;
        JuicyAudioHooks.Tap();
        director.Retry();
    }

    public void ToMap()
    {
        if (!ShowingResult) return;
        JuicyAudioHooks.Tap();
        director.OpenMap();
    }

    public void Continue()
    {
        if (!ShowingResult) return;
        if (director.Level.State == LevelState.Won) director.AfterWin();
        else director.Retry();
    }
}
