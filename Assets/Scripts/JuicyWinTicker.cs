using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class JuicyWinTicker : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI winText;
    [SerializeField] private RectTransform textContainerTransform;

    [Header("Format Settings")]
    [SerializeField] private string currencyPrefix = "$";
    [SerializeField] private string numberFormat = "N2";

    [Header("Juice Dynamics")]
    [SerializeField] private float baseRollUpDuration = 1.6f;
    [SerializeField] private float maxExtraDuration = 1.8f;
    [SerializeField] private float punchScaleAmount = 0.15f;

    [Header("Audio Dynamic Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private float basePitch = 1.0f;
    [SerializeField] private float maxPitch = 2.0f;

    [SerializeField] private double startingBalance = 1000d;

    const string PayoutFlag = "/tmp/juicy-show-payout";
    const string PayoutShot = "/tmp/juicy-payout.png";

    public static JuicyWinTicker Instance { get; private set; }
    public static bool PayoutOpen { get; private set; }

    public double Balance => balance;

    readonly System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder(32);

    Coroutine activeTickerCoroutine;
    Coroutine punchRoutine;
    Coroutine closeRoutine;
    Vector3 originalScale;
    double balance;
    bool payoutArmed;
    GameObject modal;
    TextMeshProUGUI payoutText;
    Sprite coinSprite;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("A second win ticker was ignored.");
            return;
        }

        Instance = this;
        balance = startingBalance;
        if (textContainerTransform != null) originalScale = textContainerTransform.localScale;
        EnsureAudio();
        Engrave(winText, true);
        RefreshHud(balance);
    }

    void Start()
    {
        if (!File.Exists(PayoutFlag)) return;
        payoutArmed = true;
        AddWin(70000d);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (PayoutOpen) PayoutOpen = false;
    }

    public void Punch()
    {
        if (textContainerTransform == null) return;
        if (originalScale.sqrMagnitude < 0.001f) originalScale = textContainerTransform.localScale;
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchRoutine());
    }

    IEnumerator PunchRoutine()
    {
        float elapsed = 0f;
        const float duration = 0.22f;
        Vector3 peak = originalScale * (1f + punchScaleAmount * 2.4f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            textContainerTransform.localScale = Vector3.Lerp(originalScale, peak, k);
            yield return null;
        }

        textContainerTransform.localScale = originalScale;
        punchRoutine = null;
    }

    public void AddWin(double amount)
    {
        if (amount <= 0d) return;

        float scale = 1f;
        if (StreakMultiplierManager.Instance != null)
            scale = Mathf.Max(1f, StreakMultiplierManager.Instance.CashMultiplier);

        double paid = amount * scale;
        double start = balance;
        balance += paid;
        PresentPayout(start);
        StartRollUp(start, balance);
        Debug.Log("Win ticker " + start.ToString("0") + " to " + balance.ToString("0") + ".");
        if (scale > 1.01f)
            Debug.Log("Fever paid " + paid.ToString("0") + " from " + amount.ToString("0") + " at x" + scale.ToString("0") + ".");
    }

    public void StartRollUp(double startValue, double targetValue)
    {
        if (activeTickerCoroutine != null) StopCoroutine(activeTickerCoroutine);
        activeTickerCoroutine = StartCoroutine(RollUpRoutine(startValue, targetValue));
    }

    IEnumerator RollUpRoutine(double startValue, double targetValue)
    {
        double delta = targetValue - startValue;
        if (delta <= 0)
        {
            RefreshHud(targetValue);
            RefreshPayout(targetValue);
            LockPayout();
            yield break;
        }

        float span = baseRollUpDuration + Mathf.Min(maxExtraDuration, (float)(delta / 8000.0));
        float duration = Mathf.Clamp(span, 1.5f, 2.5f);
        float elapsedTime = 0f;
        double lastDisplayedValue = startValue;

        while (elapsedTime < duration)
        {
            elapsedTime += Mathf.Min(Time.deltaTime, 0.04f);
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easeVal = Accelerate(t);
            double currentValue = startValue + (delta * easeVal);
            RefreshHud(currentValue);
            RefreshPayout(currentValue);

            if (currentValue - lastDisplayedValue >= 1.0 || t >= 1.0f)
            {
                lastDisplayedValue = currentValue;
                PlayTick(t);

                if (textContainerTransform != null)
                {
                    float punchPulse = 1.0f + (Mathf.Sin(t * Mathf.PI * 12f) * punchScaleAmount * (1f - t));
                    textContainerTransform.localScale = originalScale * punchPulse;
                }
            }

            yield return null;
        }

        RefreshHud(targetValue);
        RefreshPayout(targetValue);
        if (textContainerTransform != null) textContainerTransform.localScale = originalScale;
        if (audioSource != null) audioSource.pitch = basePitch;
        LockPayout();
    }

    static float Accelerate(float t)
    {
        if (t < 0.78f)
        {
            float u = t / 0.78f;
            return u * u * 0.9f;
        }

        float v = (t - 0.78f) / 0.22f;
        float settle = 1f - (1f - v) * (1f - v);
        return 0.9f + settle * 0.1f;
    }

    void LockPayout()
    {
        BurstCoins();
        if (payoutArmed)
        {
            payoutArmed = false;
            StartCoroutine(CapturePayout());
        }

        if (closeRoutine != null) StopCoroutine(closeRoutine);
        closeRoutine = StartCoroutine(ClosePayout());
    }

    IEnumerator CapturePayout()
    {
        yield return new WaitForSeconds(0.2f);
        if (File.Exists(PayoutFlag)) File.Delete(PayoutFlag);
        ScreenCapture.CaptureScreenshot(PayoutShot);
    }

    IEnumerator ClosePayout()
    {
        yield return new WaitForSeconds(1.35f);
        if (modal != null) modal.SetActive(false);
        PayoutOpen = false;
        closeRoutine = null;
    }

    void PresentPayout(double startValue)
    {
        EnsureModal();
        if (modal == null) return;
        PayoutOpen = true;
        modal.SetActive(true);
        modal.transform.SetAsLastSibling();
        Transform fade = modal.transform.parent != null ? modal.transform.parent.Find("FadeCover") : null;
        if (fade != null) fade.SetAsLastSibling();
        RefreshPayout(startValue);
    }

    void EnsureAudio()
    {
        if (tickSound == null)
            tickSound = Resources.Load<AudioClip>("Audio/coin_tick");
        if (audioSource == null)
            audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.pitch = basePitch;
    }

    void PlayTick(float t)
    {
        if (audioSource == null || tickSound == null) return;
        audioSource.pitch = Mathf.Lerp(basePitch, maxPitch, t);
        audioSource.PlayOneShot(tickSound, 0.85f);
    }

    void EnsureModal()
    {
        if (modal != null) return;
        Canvas canvas = RootHud();
        if (canvas == null) return;

        coinSprite = Resources.Load<Sprite>("UIHeroes/payout_coin");
        if (coinSprite == null)
            coinSprite = Resources.Load<Sprite>("UIHeroes/coins_hero");
        TMP_FontAsset font = winText != null ? winText.font : TMP_Settings.defaultFontAsset;

        modal = new GameObject("PayoutModal", typeof(RectTransform));
        modal.transform.SetParent(canvas.transform, false);
        RectTransform root = modal.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image dim = NewImage("Dimmer", root, Vignette(), Vector2.zero, new Vector2(1080f, 1920f));
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = Vector2.zero;
        dim.rectTransform.offsetMax = Vector2.zero;
        dim.raycastTarget = true;

        if (coinSprite != null)
        {
            PlaceStack("CoinStackLeft", root, new Vector2(-300f, -20f), 150f, 8f);
            PlaceStack("CoinStackRight", root, new Vector2(300f, -36f), 168f, -10f);
        }

        GameObject figure = new GameObject("PayoutFigure", typeof(RectTransform));
        figure.transform.SetParent(root, false);
        RectTransform figureRect = figure.GetComponent<RectTransform>();
        figureRect.sizeDelta = new Vector2(980f, 180f);
        figureRect.anchoredPosition = new Vector2(0f, 40f);
        payoutText = figure.AddComponent<TextMeshProUGUI>();
        payoutText.font = font;
        payoutText.fontSize = 92f;
        payoutText.fontStyle = FontStyles.Bold;
        payoutText.alignment = TextAlignmentOptions.Center;
        payoutText.raycastTarget = false;
        payoutText.enableVertexGradient = true;
        payoutText.colorGradient = new VertexGradient(
            new Color(1f, 0.98f, 0.90f),
            new Color(1f, 0.98f, 0.90f),
            new Color(0.95f, 0.70f, 0.18f),
            new Color(0.62f, 0.36f, 0.05f));
        Engrave(payoutText, true);

        modal.SetActive(false);
    }

    void PlaceStack(string name, RectTransform parent, Vector2 anchored, float size, float angle)
    {
        int count = 5;
        for (int i = 0; i < count; i++)
        {
            float lift = i * size * 0.34f;
            float slide = (i - 2) * size * 0.05f;
            Image image = NewImage(name + i, parent, coinSprite, anchored + new Vector2(slide, lift), new Vector2(size, size));
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle + (i - 2) * 3f);
        }
    }

    public static void Engrave(TextMeshProUGUI label, bool gold)
    {
        if (label == null) return;
        if (label.fontSharedMaterial == null && label.font != null)
            label.fontSharedMaterial = label.font.material;
        if (label.fontSharedMaterial == null) return;
        Material preset = Resources.Load<Material>("UI/EngravedPlate");
        Shader shader = preset != null ? preset.shader : Shader.Find("TextMeshPro/Distance Field");
        if (shader == null) return;
        Material source = label.fontSharedMaterial;
        Material mat = new Material(shader);
        mat.CopyPropertiesFromMaterial(source);
        if (source.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", source.GetTexture("_MainTex"));
        mat.SetTexture("_FaceTex", Texture2D.whiteTexture);
        mat.SetTexture("_OutlineTex", Texture2D.whiteTexture);
        mat.EnableKeyword("BEVEL_ON");
        mat.SetFloat("_Bevel", 0.16f);
        mat.SetFloat("_BevelOffset", 0f);
        mat.SetFloat("_BevelWidth", 0.12f);
        mat.SetFloat("_BevelClamp", 0.04f);
        mat.SetFloat("_BevelRoundness", 0.35f);
        mat.SetFloat("_LightAngle", 1.15f);
        mat.SetColor("_SpecularColor", new Color(0.35f, 0.28f, 0.10f, 1f));
        mat.SetFloat("_SpecularPower", 10f);
        mat.SetFloat("_Diffuse", 0.06f);
        mat.SetFloat("_Ambient", 1f);
        mat.SetFloat("_Reflectivity", 2f);
        mat.SetColor("_ReflectFaceColor", Color.black);
        mat.SetColor("_ReflectOutlineColor", Color.black);
        mat.EnableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.92f));
        mat.SetFloat("_UnderlayOffsetX", 0.22f);
        mat.SetFloat("_UnderlayOffsetY", -0.48f);
        mat.SetFloat("_UnderlayDilate", 0.1f);
        mat.SetFloat("_UnderlaySoftness", 0.06f);
        mat.SetFloat("_OutlineWidth", gold ? 0.05f : 0.18f);
        mat.SetColor("_OutlineColor", new Color(0.08f, 0.04f, 0.02f, 1f));
        if (!gold)
        {
            mat.SetFloat("_UnderlayDilate", 0.42f);
            mat.SetFloat("_UnderlayOffsetX", 0.28f);
            mat.SetFloat("_UnderlayOffsetY", -0.62f);
        }

        label.fontMaterial = mat;
        label.extraPadding = true;
        if (!gold) return;
        label.fontStyle |= FontStyles.Bold;
        label.enableVertexGradient = true;
        label.color = Color.white;
        label.colorGradient = new VertexGradient(
            new Color(1f, 0.92f, 0.45f),
            new Color(1f, 0.92f, 0.45f),
            new Color(0.98f, 0.72f, 0.16f),
            new Color(0.82f, 0.52f, 0.08f));
    }

    void BurstCoins()
    {
        if (modal == null || coinSprite == null || !modal.activeSelf) return;
        JuicyCoinBurstManager burstManager = JuicyCoinBurstManager.Instance;
        if (burstManager == null)
            burstManager = FindFirstObjectByType<JuicyCoinBurstManager>();
        if (burstManager == null) return;
        Transform figure = payoutText != null ? payoutText.transform : null;
        burstManager.PlayPayoutBurst(modal.GetComponent<RectTransform>(), coinSprite, figure);
    }

    static Image NewImage(string name, RectTransform parent, Sprite sprite, Vector2 anchored, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        return image;
    }

    static Sprite Vignette()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float radius = Mathf.Sqrt(dx * dx * 0.72f + dy * dy) / 0.5f;
                float edge = Mathf.SmoothStep(0.2f, 1.05f, radius);
                float alpha = Mathf.Lerp(0.42f, 0.9f, edge);
                Color color = Color.Lerp(new Color(0.05f, 0.02f, 0.02f, 1f), new Color(0.28f, 0.02f, 0.05f, 1f), edge);
                color.a = alpha;
                pixels[y * size + x] = color;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Canvas RootHud()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas fallback = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (canvas.name == "Canvas") return canvas;
            if (fallback == null) fallback = canvas;
        }

        return fallback;
    }

    void RefreshHud(double value)
    {
        if (winText == null) return;
        stringBuilder.Clear();
        stringBuilder.Append(currencyPrefix);
        stringBuilder.Append(value.ToString(numberFormat));
        winText.SetText(stringBuilder);
    }

    void RefreshPayout(double value)
    {
        if (payoutText == null) return;
        payoutText.SetText(currencyPrefix + value.ToString(numberFormat));
    }
}
