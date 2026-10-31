using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Flawless sorts light the tracks and scale cash. A wrong sort or a bomb wipes the streak.
public class StreakMultiplierManager : MonoBehaviour
{
    public const int TierStreak1 = 5;
    public const int TierStreak2 = 10;
    public const int TierStreak3 = 15;

    public static StreakMultiplierManager Instance { get; private set; }

    public event Action<int, int> OnFeverChanged;

    static readonly float[] TrackX = { -200f, 0f, 200f };

    int streak;
    int tier;
    bool flamesLive;
    RectTransform emberRoot;
    readonly List<Image> embers = new List<Image>(18);
    readonly List<float> emberY = new List<float>(18);
    ParticleSystem leftFlame;
    ParticleSystem rightFlame;
    Transform leftRail;
    Transform rightRail;
    TextMeshProUGUI feverLabel;
    MaterialPropertyBlock railBlock;

    public int Streak => streak;
    public int Tier => tier;
    public bool FeverActive => tier > 0;
    public bool FlamesLive => flamesLive;

    public float CashMultiplier
    {
        get
        {
            if (tier >= 3) return 5f;
            if (tier == 2) return 3f;
            if (tier == 1) return 2f;
            return 1f;
        }
    }

    public static int TierFor(int value)
    {
        if (value >= TierStreak3) return 3;
        if (value >= TierStreak2) return 2;
        if (value >= TierStreak1) return 1;
        return 0;
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("StreakMultiplier").AddComponent<StreakMultiplierManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildEmbers();
        BuildLabel();
        EnsureFlames();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        EnsureFlames();
        bool show = tier > 0 && (HeatManager.Instance == null || !HeatManager.Instance.LockdownActive);
        TickEmbers(show, Time.deltaTime);
        TickFlames(show);
        TintRails(show);
        RefreshLabel();
        flamesLive = show && (EmbersVisible() || FlamePlaying(leftFlame) || FlamePlaying(rightFlame));
    }

    public void ReportSuccess()
    {
        streak++;
        int next = TierFor(streak);
        if (next == tier) return;
        tier = next;
        Debug.Log("Fever tier " + tier + ". Streak " + streak + ". Cash x" + CashMultiplier.ToString("0") + ".");
        OnFeverChanged?.Invoke(streak, tier);
    }

    public void Break()
    {
        if (streak == 0 && tier == 0) return;
        streak = 0;
        tier = 0;
        Debug.Log("Streak reset.");
        OnFeverChanged?.Invoke(0, 0);
    }

    void TickEmbers(bool show, float dt)
    {
        if (emberRoot != null) emberRoot.gameObject.SetActive(show);
        if (!show) return;

        int lit = tier >= 3 ? embers.Count : tier == 2 ? 12 : 6;
        float speed = tier >= 3 ? 460f : tier == 2 ? 320f : 220f;
        for (int i = 0; i < embers.Count; i++)
        {
            Image ember = embers[i];
            if (ember == null) continue;
            bool on = i < lit;
            ember.enabled = on;
            if (!on) continue;

            float y = emberY[i] + speed * dt;
            if (y > 460f) y = -420f;
            emberY[i] = y;
            var rect = ember.rectTransform;
            rect.anchoredPosition = new Vector2(TrackX[i % TrackX.Length], y);
            float flicker = 0.55f + 0.45f * Mathf.Sin(Time.time * 16f + i);
            Color color = Color.Lerp(new Color(1f, 0.42f, 0.08f, 1f), new Color(1f, 0.92f, 0.45f, 1f), flicker);
            color.a = 0.35f + 0.6f * flicker;
            ember.color = color;
            float height = Mathf.Lerp(28f, 64f, flicker);
            rect.sizeDelta = new Vector2(18f + tier * 4f, height);
        }
    }

    void TickFlames(bool show)
    {
        ApplyFlame(leftFlame, leftRail, show);
        ApplyFlame(rightFlame, rightRail, show);
    }

    void ApplyFlame(ParticleSystem flame, Transform rail, bool show)
    {
        if (flame == null) return;
        if (rail != null)
        {
            flame.transform.position = rail.position;
            var shape = flame.shape;
            float height = Mathf.Max(0.4f, rail.lossyScale.y);
            shape.scale = new Vector3(0.16f, height, 0.16f);
        }

        var emission = flame.emission;
        emission.rateOverTime = show ? 16f * Mathf.Max(1, tier) : 0f;
        if (show && !flame.isPlaying) flame.Play();
        if (!show && flame.isPlaying) flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void TintRails(bool show)
    {
        Color calm = ChuteContrast.Instance != null
            ? ChuteContrast.Instance.RailColor
            : new Color(1.45f, 1.22f, 0.78f, 1f);
        Color hot = new Color(1.55f, 0.82f, 0.22f, 1f);
        TintRail(leftRail, show ? hot : calm, show);
        TintRail(rightRail, show ? hot : calm, show);
    }

    void TintRail(Transform rail, Color color, bool glow)
    {
        if (rail == null) return;
        var renderer = rail.GetComponent<MeshRenderer>();
        if (renderer == null) return;
        if (railBlock == null) railBlock = new MaterialPropertyBlock();
        railBlock.Clear();
        railBlock.SetColor("_BaseColor", color);
        railBlock.SetColor("_Color", color);
        railBlock.SetColor("_EmissionColor", glow ? color * 1.8f : color);
        renderer.SetPropertyBlock(railBlock);
    }

    bool EmbersVisible()
    {
        for (int i = 0; i < embers.Count; i++)
        {
            if (embers[i] != null && embers[i].enabled) return true;
        }

        return false;
    }

    static bool FlamePlaying(ParticleSystem flame)
    {
        if (flame == null) return false;
        return flame.isPlaying && flame.emission.rateOverTime.constant > 0.5f;
    }

    void RefreshLabel()
    {
        if (feverLabel == null) return;
        bool show = tier > 0 && (HeatManager.Instance == null || !HeatManager.Instance.LockdownActive);
        feverLabel.gameObject.SetActive(show);
        if (!show) return;
        feverLabel.text = "FEVER x" + CashMultiplier.ToString("0");
    }

    void BuildLabel()
    {
        Canvas canvas = HeatManager.FindHud();
        TMP_FontAsset font = HeatManager.DefaultFont();
        if (canvas == null || font == null) return;

        var go = new GameObject("FeverLabel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 548f);
        rect.sizeDelta = new Vector2(420f, 40f);
        feverLabel = go.AddComponent<TextMeshProUGUI>();
        feverLabel.font = font;
        feverLabel.fontSize = 32f;
        feverLabel.fontStyle = FontStyles.Bold;
        feverLabel.alignment = TextAlignmentOptions.Center;
        feverLabel.color = new Color(1f, 0.84f, 0.28f, 1f);
        feverLabel.raycastTarget = false;
        feverLabel.text = "";
        feverLabel.gameObject.SetActive(false);
        HeatManager.PlaceUnderFade(canvas, go.transform);
    }

    void BuildEmbers()
    {
        GameObject chute = GameObject.Find("Chute");
        RectTransform host = chute != null ? chute.GetComponent<RectTransform>() : null;
        if (host == null)
        {
            Canvas canvas = HeatManager.FindHud();
            if (canvas == null) return;
            host = canvas.transform as RectTransform;
        }

        if (host == null) return;
        var rootGo = new GameObject("FeverEmbers", typeof(RectTransform));
        rootGo.transform.SetParent(host, false);
        emberRoot = rootGo.GetComponent<RectTransform>();
        emberRoot.anchorMin = new Vector2(0.5f, 0.5f);
        emberRoot.anchorMax = new Vector2(0.5f, 0.5f);
        emberRoot.anchoredPosition = Vector2.zero;
        emberRoot.sizeDelta = Vector2.zero;
        rootGo.transform.SetAsLastSibling();

        Sprite sprite = FlameSprite();
        for (int i = 0; i < 18; i++)
        {
            var go = new GameObject("Ember", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(emberRoot, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.15f);
            rect.sizeDelta = new Vector2(22f, 48f);
            float y = -420f + (880f / 18f) * i;
            emberY.Add(y);
            rect.anchoredPosition = new Vector2(TrackX[i % TrackX.Length], y);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = new Color(1f, 0.72f, 0.2f, 0.8f);
            image.enabled = false;
            embers.Add(image);
        }

        emberRoot.gameObject.SetActive(false);
    }

    void EnsureFlames()
    {
        if (leftRail == null)
        {
            GameObject found = GameObject.Find("ChuteRailL");
            if (found != null) leftRail = found.transform;
        }

        if (rightRail == null)
        {
            GameObject found = GameObject.Find("ChuteRailR");
            if (found != null) rightRail = found.transform;
        }

        if (leftFlame == null && leftRail != null) leftFlame = MakeFlame();
        if (rightFlame == null && rightRail != null) rightFlame = MakeFlame();
    }

    ParticleSystem MakeFlame()
    {
        var go = new GameObject("FeverFlame");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        var particles = go.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1.2f;
        main.startLifetime = 0.55f;
        main.startSpeed = 0.35f;
        main.startSize = 0.08f;
        main.startColor = new Color(1f, 0.78f, 0.22f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.gravityModifier = -0.35f;
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.12f, 1.4f, 0.12f);
        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f)
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

    static Sprite FlameSprite()
    {
        const int w = 8;
        const int h = 32;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1);
            byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(t));
            byte red = 255;
            byte green = (byte)Mathf.RoundToInt(Mathf.Lerp(70f, 230f, t));
            byte blue = (byte)Mathf.RoundToInt(Mathf.Lerp(12f, 80f, t));
            for (int x = 0; x < w; x++)
            {
                float edge = Mathf.Abs(x - (w - 1) * 0.5f) / ((w - 1) * 0.5f);
                byte a = (byte)Mathf.RoundToInt(alpha * (1f - edge * 0.85f));
                pixels[y * w + x] = new Color32(red, green, blue, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.15f), 32f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
