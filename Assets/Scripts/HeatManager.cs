using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Police heat. Mistakes push it toward a lockdown. Clean sorts bleed it off.
public class HeatManager : MonoBehaviour
{
    public const float MaxHeat = 100f;
    public const float MisSortHeat = 22f;
    public const float HazardHeat = 34f;
    public const float ErrorHeat = 14f;
    public const float BleedPerSecond = 10f;
    public const float SirenHzAtMax = 9f;
    public const float PulseHzBase = 0.45f;
    public const float PulseHzSpan = 3.6f;

    public static HeatManager Instance { get; private set; }

    public event Action<float> OnHeatChanged;
    public event Action OnLockdownTriggered;

    float currentHeat;
    float shownHeat;
    float sirenPhase;
    bool cooling;
    bool lockdownActive;
    bool sirenOn;
    int flashCount;

    Image barFill;
    Image redLamp;
    Image blueLamp;
    TextMeshProUGUI heatLabel;
    static Sprite whiteSprite;

    public float CurrentHeat => currentHeat;
    public float ShownHeat => shownHeat;
    public float Normalized => currentHeat / MaxHeat;
    public bool LockdownActive => lockdownActive;
    public int FlashCount => flashCount;
    public bool SirensReady => redLamp != null && blueLamp != null;
    public float MeterFill => barFill == null ? shownHeat / MaxHeat : barFill.fillAmount;

    // Rate follows the real heat so a mistake speeds the siren on that same sort.
    public float SirenFlashRate => Normalized * SirenHzAtMax;
    public float PulseSpeed => PulseHzBase + Normalized * PulseHzSpan;

    public float AlarmAlpha
    {
        get
        {
            float intensity = Mathf.Clamp01(Normalized);
            return sirenOn ? intensity * 0.48f : intensity * 0.05f;
        }
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("HeatManager").AddComponent<HeatManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildHud();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        Tick(Time.deltaTime);
    }

    public void Tick(float dt)
    {
        if (dt < 0f) dt = 0f;

        if (cooling && !lockdownActive && InPuzzle())
        {
            float next = Mathf.Max(0f, currentHeat - BleedPerSecond * dt);
            if (!Mathf.Approximately(next, currentHeat))
            {
                currentHeat = next;
                OnHeatChanged?.Invoke(currentHeat);
            }
        }

        float rise = shownHeat < currentHeat ? 90f : 36f;
        shownHeat = Mathf.MoveTowards(shownHeat, currentHeat, rise * dt);
        TickSirens(dt);
        TickPulse();
        RefreshHud();
    }

    public void ReportSuccess()
    {
        if (lockdownActive) return;
        cooling = true;
    }

    public void ReportMisSort()
    {
        Fault(MisSortHeat, "wrong sort");
    }

    public void ReportHazard()
    {
        Fault(HazardHeat, "hazard");
    }

    public void ReportError()
    {
        Fault(ErrorHeat, "error");
    }

    public void ClearHeat()
    {
        cooling = false;
        if (Mathf.Approximately(currentHeat, 0f)) return;
        currentHeat = 0f;
        OnHeatChanged?.Invoke(currentHeat);
    }

    public void ResolveLockdown()
    {
        lockdownActive = false;
        ClearHeat();
        Debug.Log("Lockdown cleared. Heat reset.");
    }

    void Fault(float amount, string reason)
    {
        if (lockdownActive || amount <= 0f) return;

        cooling = false;
        float before = currentHeat;
        currentHeat = Mathf.Min(MaxHeat, currentHeat + amount);
        OnHeatChanged?.Invoke(currentHeat);
        Debug.Log("Heat " + currentHeat.ToString("0") + " after " + reason + ".");

        if (!lockdownActive && before < MaxHeat && currentHeat >= MaxHeat)
            TriggerLockdown();
    }

    void TriggerLockdown()
    {
        if (lockdownActive) return;
        lockdownActive = true;
        cooling = false;
        Debug.Log("Lockdown triggered at heat " + currentHeat.ToString("0") + ".");
        OnLockdownTriggered?.Invoke();
    }

    static bool InPuzzle()
    {
        if (GameCoreManager.Instance == null) return true;
        return GameCoreManager.Instance.CurrentState == GameCoreManager.GameState.PuzzleLoop;
    }

    void TickSirens(float dt)
    {
        float rate = SirenFlashRate;
        if (rate <= 0.05f)
        {
            sirenOn = false;
            PaintLamp(redLamp, 0.12f, new Color(0.55f, 0.05f, 0.08f, 1f));
            PaintLamp(blueLamp, 0.12f, new Color(0.08f, 0.18f, 0.7f, 1f));
            return;
        }

        sirenPhase += dt * rate;
        float frac = sirenPhase - Mathf.Floor(sirenPhase);
        bool on = frac < 0.42f;
        if (on && !sirenOn) flashCount++;
        sirenOn = on;
        bool redTurn = Mathf.FloorToInt(sirenPhase) % 2 == 0;
        PaintLamp(redLamp, on && redTurn ? 1f : 0.16f, new Color(1f, 0.08f, 0.1f, 1f));
        PaintLamp(blueLamp, on && !redTurn ? 1f : 0.16f, new Color(0.25f, 0.45f, 1f, 1f));
    }

    void TickPulse()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed * Mathf.PI * 2f);
        if (ChuteContrast.Instance != null)
            ChuteContrast.Instance.SetHeatPulse(Normalized, pulse);
    }

    void RefreshHud()
    {
        float fill = Mathf.Clamp01(shownHeat / MaxHeat);
        if (barFill != null)
        {
            barFill.fillAmount = fill;
            barFill.color = Color.Lerp(new Color(1f, 0.78f, 0.22f, 1f), new Color(0.95f, 0.08f, 0.1f, 1f), fill);
        }

        if (heatLabel != null)
            heatLabel.text = "HEAT " + Mathf.RoundToInt(shownHeat).ToString("0");
    }

    static void PaintLamp(Image lamp, float alpha, Color color)
    {
        if (lamp == null) return;
        color.a = alpha;
        lamp.color = color;
        float scale = Mathf.Lerp(0.85f, 1.2f, alpha);
        lamp.rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    void BuildHud()
    {
        Canvas canvas = FindHud();
        if (canvas == null) return;

        Sprite sprite = WhiteSprite();
        var root = new GameObject("HeatMeter", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, 678f);
        rootRect.sizeDelta = new Vector2(520f, 36f);
        PlaceUnderFade(canvas, root.transform);

        var back = MakeImage("HeatBack", root.transform, sprite, new Vector2(0f, 0f), new Vector2(360f, 18f));
        back.color = new Color(0.12f, 0.05f, 0.06f, 0.9f);

        barFill = MakeImage("HeatFill", root.transform, sprite, new Vector2(0f, 0f), new Vector2(360f, 18f));
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFill.fillAmount = 0f;
        barFill.color = new Color(1f, 0.78f, 0.22f, 1f);

        redLamp = MakeImage("SirenRed", root.transform, sprite, new Vector2(-230f, 0f), new Vector2(28f, 28f));
        blueLamp = MakeImage("SirenBlue", root.transform, sprite, new Vector2(230f, 0f), new Vector2(28f, 28f));
        redLamp.color = new Color(0.55f, 0.05f, 0.08f, 0.12f);
        blueLamp.color = new Color(0.08f, 0.18f, 0.7f, 0.12f);

        heatLabel = MakeLabel("HeatLabel", root.transform, new Vector2(0f, 22f), 22f);
        if (heatLabel != null)
        {
            heatLabel.text = "HEAT 0";
            heatLabel.rectTransform.sizeDelta = new Vector2(360f, 48f);
            if (heatLabel.fontSharedMaterial == null && heatLabel.font != null)
                heatLabel.fontSharedMaterial = heatLabel.font.material;
            JuicyWinTicker.Engrave(heatLabel, false);
        }
    }

    static Image MakeImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI MakeLabel(string name, Transform parent, Vector2 position, float size)
    {
        TMP_FontAsset font = DefaultFont();
        if (font == null) return null;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 28f);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.9f, 0.86f, 1f);
        label.raycastTarget = false;
        return label;
    }

    public static void PlaceUnderFade(Canvas canvas, Transform target)
    {
        if (canvas == null || target == null) return;
        Transform fade = canvas.transform.Find("FadeCover");
        if (fade != null) target.SetSiblingIndex(fade.GetSiblingIndex());
        else target.SetAsLastSibling();
    }

    public static Canvas FindHud()
    {
        GameObject ticker = GameObject.Find("CoinTickerText");
        if (ticker != null)
        {
            Canvas canvas = ticker.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas;
        }

        return FindFirstObjectByType<Canvas>();
    }

    public static TMP_FontAsset DefaultFont()
    {
        if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
        TMP_Text sample = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return sample != null ? sample.font : null;
    }

    public static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }
}
