using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WordVaultManager : MonoBehaviour
{
    public static WordVaultManager Instance { get; private set; }

    static readonly string[] Words = { "HEIST", "VAULT", "ESCAPE" };
    const double Bonus = 2500d;

    struct Slot
    {
        public char Letter;
        public bool Filled;
    }

    Slot[] slots = System.Array.Empty<Slot>();
    TextMeshProUGUI label;
    int wordIndex;

    public static TMP_FontAsset SharedFont { get; private set; }
    public bool Ready => slots != null && slots.Length > 0 && label != null;
    public string ActiveWord => wordIndex >= 0 && wordIndex < Words.Length ? Words[wordIndex] : "";
    public int Filled { get; private set; }
    public int Completions { get; private set; }
    public double LastBonus { get; private set; }
    public bool BurstFired { get; private set; }
    public int BannerPunches { get; private set; }
    public float PeakPunch { get; private set; }
    public string Display => label != null ? label.text : "";

    Coroutine punchRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadWord(Words[0]);
    }

    void Start()
    {
        SharedFont = FindFont();
        EnsureLabel();
        RefreshLabel();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public char PickLetter()
    {
        if (slots == null || slots.Length == 0) return 'E';
        return slots[Random.Range(0, slots.Length)].Letter;
    }

    public bool Bank(char letter)
    {
        char upper = char.ToUpperInvariant(letter);
        if (slots == null) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Filled || slots[i].Letter != upper) continue;
            slots[i].Filled = true;
            Filled++;
            RefreshLabel();
            Debug.Log("Letter " + upper + " banked. " + ActiveWord + " " + Filled + "/" + slots.Length + ".");
            if (Filled >= slots.Length) Complete();
            return true;
        }

        return false;
    }

    void Complete()
    {
        string finished = ActiveWord;
        bool heist = finished == "HEIST";
        double bonus = heist ? 25000d : Bonus;
        Completions++;
        LastBonus = bonus;
        if (JuicyWinTicker.Instance != null) JuicyWinTicker.Instance.AddWin(bonus);

        JuicyCoinBurstManager burst = FindFirstObjectByType<JuicyCoinBurstManager>();
        if (burst != null)
        {
            burst.BurstFrom(Vector3.zero);
            BurstFired = true;
        }

        MissilePowerUpController missile = FindFirstObjectByType<MissilePowerUpController>();
        if (missile != null) missile.GrantFreeCharge();

        Debug.Log("Word " + finished + " complete. Bonus " + bonus.ToString("0") + ". Free missile ready.");
        if (CasinoDressing.Instance != null) CasinoDressing.Instance.Celebrate(false, !heist);
        if (heist) PunchHeistBanner();
        else AdvanceWord();
    }

    void AdvanceWord()
    {
        wordIndex = (wordIndex + 1) % Words.Length;
        Filled = 0;
        LoadWord(Words[wordIndex]);
        RefreshLabel();
    }

    void PunchHeistBanner()
    {
        BannerPunches++;
        JuicyUIPop[] pops = FindObjectsByType<JuicyUIPop>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < pops.Length; i++)
        {
            if (pops[i] == null || pops[i].name != "GrandVaultBanner") continue;
            if (pops[i].gameObject.activeInHierarchy) pops[i].TriggerPunch(1.18f);
        }

        if (label == null)
        {
            AdvanceWord();
            return;
        }

        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchLabel());
    }

    System.Collections.IEnumerator PunchLabel()
    {
        if (label == null) yield break;
        RectTransform rect = label.rectTransform;
        Vector3 start = Vector3.one;
        Vector3 peak = start * 1.18f;
        float half = 0.1f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            rect.localScale = Vector3.Lerp(start, peak, Mathf.SmoothStep(0f, 1f, t));
            if (rect.localScale.x > PeakPunch) PeakPunch = rect.localScale.x;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            rect.localScale = Vector3.Lerp(peak, start, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        rect.localScale = start;
        punchRoutine = null;
        AdvanceWord();
    }

    void LoadWord(string word)
    {
        slots = new Slot[word.Length];
        for (int i = 0; i < word.Length; i++)
            slots[i] = new Slot { Letter = word[i], Filled = false };
    }

    void RefreshLabel()
    {
        if (label == null || slots == null) return;
        var text = new System.Text.StringBuilder(slots.Length * 24);
        for (int i = 0; i < slots.Length; i++)
        {
            if (i > 0) text.Append(' ');
            if (slots[i].Filled)
                text.Append("<color=#F6D56A>").Append(slots[i].Letter).Append("</color>");
            else
                text.Append("<color=#6E675C>").Append(slots[i].Letter).Append("</color>");
        }

        label.text = text.ToString();
    }

    void EnsureLabel()
    {
        if (label != null) return;
        Canvas canvas = RootHud();
        if (canvas == null || SharedFont == null) return;

        var go = new GameObject("WordVault", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 636f);
        rect.sizeDelta = new Vector2(640f, 36f);
        label = go.AddComponent<TextMeshProUGUI>();
        label.font = SharedFont;
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.color = new Color(0.98f, 0.94f, 0.84f, 1f);

        go.transform.SetAsLastSibling();
        Transform fade = canvas.transform.Find("FadeCover");
        if (fade != null) fade.SetAsLastSibling();
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

    static TMP_FontAsset FindFont()
    {
        if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
        TMP_Text sample = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return sample != null ? sample.font : null;
    }
}
