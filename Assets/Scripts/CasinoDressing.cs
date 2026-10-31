using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Buttons stay clickable. Claim stays a flat plate. The launch plate is dressed by MissilePowerUpController.
public class CasinoDressing : MonoBehaviour
{
    public static CasinoDressing Instance { get; private set; }

    bool styledClaim;
    bool styledLaunch;

    public bool ButtonsStyled => styledClaim && styledLaunch;
    public int Celebrations { get; private set; }

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("CasinoDressing").AddComponent<CasinoDressing>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildOverlay();
    }

    void Start()
    {
        StyleButtons();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Celebrate(bool shower, bool popLabel)
    {
        Celebrations++;
        if (GameCoreManager.Instance != null) GameCoreManager.Instance.PlayWin();
        if (StageModelDressing.Instance != null) StageModelDressing.Instance.OpenChest();
        if (shower)
        {
            JuicyCoinBurstManager burst = FindFirstObjectByType<JuicyCoinBurstManager>();
            if (burst != null) burst.BurstFrom(Vector3.zero);
        }

        if (popLabel && WordVaultManager.Instance != null)
            StartCoroutine(Pop(WordVaultManager.Instance));
    }

    public static ParticleSystem CreateSparkle(Transform parent)
    {
        if (parent == null) return null;
        var go = new GameObject("Sparkle");
        go.transform.SetParent(parent, false);
        var particles = go.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = 0.42f;
        main.startSpeed = 0.08f;
        main.startSize = 0.045f;
        main.startColor = new Color(1f, 0.86f, 0.38f, 1f);
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission;
        emission.rateOverTime = 16f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;
        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.7f, 0.2f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            var material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            renderer.material = material;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    void BuildOverlay()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null) return;
        DestroyNamed(canvas.transform, "FiligreeFrame");
        DestroyNamed(canvas.transform, "CasinoPlaque");
        SeatVaultBanner();
    }

    static void DestroyNamed(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null) Destroy(old.gameObject);
    }

    static void SeatVaultBanner()
    {
        GameObject banner = GameObject.Find("GrandVaultBanner");
        if (banner == null) return;
        Image image = banner.GetComponent<Image>();
        if (image == null) return;
        Color color = image.color;
        color.a = 0f;
        image.color = color;
        image.raycastTarget = false;
    }

    void StyleButtons()
    {
        styledClaim = Style("ClaimRewardButton");
        styledLaunch = true;
    }

    bool Style(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) return false;
        Image image = go.GetComponent<Image>();
        if (image == null) return false;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.10f, 0.11f, 0.13f, 0.94f);
        image.raycastTarget = true;
        return true;
    }

    static Canvas FindCanvas()
    {
        GameObject ticker = GameObject.Find("CoinTickerText");
        if (ticker == null) return null;
        return ticker.GetComponentInParent<Canvas>();
    }

    static IEnumerator Pop(WordVaultManager vault)
    {
        if (vault == null) yield break;
        TextMeshProUGUI label = null;
        TextMeshProUGUI[] labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].gameObject.name == "WordVault")
            {
                label = labels[i];
                break;
            }
        }

        if (label == null) yield break;
        RectTransform rect = label.rectTransform;
        Vector3 start = Vector3.one;
        Vector3 peak = start * 1.12f;
        float half = 0.09f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            rect.localScale = Vector3.Lerp(start, peak, Mathf.SmoothStep(0f, 1f, elapsed / half));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            rect.localScale = Vector3.Lerp(peak, start, Mathf.SmoothStep(0f, 1f, elapsed / half));
            yield return null;
        }

        rect.localScale = start;
    }

}
