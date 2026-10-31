using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI progressLabel;
    [SerializeField] private TextMeshProUGUI boostLabel;
    [SerializeField] private Image fade;
    [SerializeField] private int baseRedGoal = 6;
    [SerializeField] private int baseBlueGoal = 4;
    [SerializeField] private int redGoalStep = 4;
    [SerializeField] private int blueGoalStep = 3;
    [SerializeField] private float fadeHalfDuration = 0.75f;

    int shownWaveRed = -1;
    int shownWaveBlue = -1;
    bool returning;
    bool boostHomeStored;
    Vector2 boostHome;
    Coroutine boostRoutine;

    public int RedEnergy { get; private set; }
    public int BlueCoolant { get; private set; }
    public int Gate { get; private set; } = 1;
    public int RedGoal => baseRedGoal + (Gate - 1) * redGoalStep;
    public int BlueGoal => baseBlueGoal + (Gate - 1) * blueGoalStep;
    public float FadeAlpha => fade == null ? 0f : fade.color.a;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SetFade(0f);
        if (fade != null) fade.raycastTarget = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        EnsureBoost();
        RefreshLabel();
    }

    public void ShowBoost(string text)
    {
        EnsureBoost();
        if (boostLabel == null) return;
        boostLabel.text = text;
        if (boostRoutine != null) StopCoroutine(boostRoutine);
        boostRoutine = StartCoroutine(BoostRoutine());
    }

    void EnsureBoost()
    {
        if (boostLabel == null)
        {
            GameObject found = GameObject.Find("BoostPop");
            if (found != null) boostLabel = found.GetComponent<TextMeshProUGUI>();
        }

        if (boostLabel == null || boostHomeStored) return;
        boostHome = boostLabel.rectTransform.anchoredPosition;
        boostHomeStored = true;
        SetBoostAlpha(0f);
    }

    IEnumerator BoostRoutine()
    {
        float elapsed = 0f;
        const float duration = 0.6f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            boostLabel.rectTransform.anchoredPosition = boostHome + new Vector2(0f, 40f * t);
            SetBoostAlpha(1f - t);
            yield return null;
        }

        boostLabel.rectTransform.anchoredPosition = boostHome;
        SetBoostAlpha(0f);
        boostRoutine = null;
    }

    void SetBoostAlpha(float alpha)
    {
        if (boostLabel == null) return;
        Color color = new Color(1f, 0.84f, 0.28f, alpha);
        boostLabel.color = color;
    }

    void Update()
    {
        if (returning || progressLabel == null) return;
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        int waveRed = sort != null ? sort.CraftRed : 0;
        int waveBlue = sort != null ? sort.CraftBlue : 0;
        if (waveRed == shownWaveRed && waveBlue == shownWaveBlue) return;
        RefreshLabel();
    }

    public void Deposit(int red, int blue)
    {
        if (red > 0) RedEnergy += red;
        if (blue > 0) BlueCoolant += blue;
        Debug.Log("Inventory deposited " + red + " thermite and " + blue + " coolant. Stock " + RedEnergy + " thermite, " + BlueCoolant + " coolant.");
        TryOpenGate();
        RefreshLabel();
    }

    public void BeginWaveReturn()
    {
        if (returning) return;

        int red = 0;
        int blue = 0;
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) sort.TakeCraftStash(out red, out blue);
        Deposit(red, blue);
        StartCoroutine(FadeAndReturn());
    }

    void TryOpenGate()
    {
        if (RedEnergy < RedGoal || BlueCoolant < BlueGoal) return;

        RedEnergy -= RedGoal;
        BlueCoolant -= BlueGoal;
        Debug.Log("Gate " + Gate + " open.");
        Gate++;
        if (HeistCombativeManager.Instance != null)
            HeistCombativeManager.Instance.NotifyGateCleared();
    }

    IEnumerator FadeAndReturn()
    {
        returning = true;
        if (fade != null) fade.raycastTarget = true;
        yield return Fade(0f, 1f, fadeHalfDuration);

        if (HeistCombativeManager.Instance != null)
            HeistCombativeManager.Instance.AdvanceEscapeRun();

        if (GameCoreManager.Instance != null)
            GameCoreManager.Instance.ChangeState(GameCoreManager.GameState.PuzzleLoop);

        yield return Fade(1f, 0f, fadeHalfDuration);
        if (fade != null) fade.raycastTarget = false;
        SetFade(0f);
        returning = false;
        RefreshLabel();
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        float span = Mathf.Max(0.05f, duration);
        while (elapsed < span)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / span);
            SetFade(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetFade(to);
    }

    void SetFade(float alpha)
    {
        if (fade == null) return;
        Color color = fade.color;
        color.a = alpha;
        fade.color = color;
    }

    void RefreshLabel()
    {
        if (progressLabel == null) return;
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        int waveRed = sort != null ? sort.CraftRed : 0;
        int waveBlue = sort != null ? sort.CraftBlue : 0;
        shownWaveRed = waveRed;
        shownWaveBlue = waveBlue;
        progressLabel.text = "RUN " + waveRed + " RED  " + waveBlue + " BLUE    GATE "
            + RedEnergy + "/" + RedGoal + " RED  "
            + BlueCoolant + "/" + BlueGoal + " BLUE";
    }
}
