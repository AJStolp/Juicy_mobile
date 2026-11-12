using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Green credit line from a side vault up into the cash counter.
public class MoneyTrailManager : MonoBehaviour
{
    public static MoneyTrailManager Instance { get; private set; }

    // Verification stills hold the line open longer. Real play leaves this at 0.
    public static float PreviewSpan;

    const int Pool = 4;
    const int Sparks = 8;

    RectTransform layer;
    readonly Image[] lines = new Image[Pool];
    readonly Image[] sparks = new Image[Sparks];
    readonly bool[] lineBusy = new bool[Pool];
    Sprite dot;
    int sparkCursor;

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("MoneyTrail").AddComponent<MoneyTrailManager>();
    }

    public static void Surge(RectTransform from)
    {
        Ensure();
        if (Instance != null) Instance.StartSurge(from);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Canvas canvas = Overlay();
        if (canvas == null) return;

        GameObject host = new GameObject("MoneyTrailLayer", typeof(RectTransform));
        host.transform.SetParent(canvas.transform, false);
        layer = host.GetComponent<RectTransform>();
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        dot = SoftDot();

        Sprite bar = LineBar();
        for (int i = 0; i < Pool; i++)
        {
            lines[i] = Make("Trail" + i, 18f);
            lines[i].sprite = bar;
        }

        for (int i = 0; i < Sparks; i++)
            sparks[i] = Make("Spark" + i, 16f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void StartSurge(RectTransform from)
    {
        if (layer == null || from == null) return;
        RectTransform hud = JuicyWinTicker.Instance != null ? JuicyWinTicker.Instance.HudRect : null;
        if (hud == null) return;

        int slot = -1;
        for (int i = 0; i < Pool; i++)
        {
            if (lineBusy[i]) continue;
            slot = i;
            break;
        }

        if (slot < 0) return;
        Vector2 start = layer.InverseTransformPoint(from.TransformPoint(from.rect.center));
        Vector2 end = layer.InverseTransformPoint(hud.TransformPoint(hud.rect.center));
        StartCoroutine(Run(slot, start, end));
    }

    IEnumerator Run(int slot, Vector2 start, Vector2 end)
    {
        lineBusy[slot] = true;
        Image line = lines[slot];
        RectTransform rect = line.rectTransform;
        Vector2 delta = end - start;
        float distance = delta.magnitude;
        rect.anchoredPosition = start;
        rect.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(delta.x, delta.y, 0f));
        rect.sizeDelta = new Vector2(14f, 0f);
        line.color = new Color(0.45f, 1f, 0.5f, 0.95f);
        line.gameObject.SetActive(true);
        layer.SetAsLastSibling();

        bool preview = PreviewSpan > 0.05f;
        if (preview)
        {
            // The verification still is only a few frames in, so show the finished line.
            rect.sizeDelta = new Vector2(8f, distance);
            yield return new WaitForSeconds(PreviewSpan);
        }
        else
        {
            float elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                rect.sizeDelta = new Vector2(Mathf.Lerp(22f, 8f, eased), distance * eased);
                yield return null;
            }
        }

        line.gameObject.SetActive(false);
        lineBusy[slot] = false;
        Burst(end);
        JuicyAudioHooks.Chime();
        if (JuicyWinTicker.Instance != null) JuicyWinTicker.Instance.Punch();
    }

    void Burst(Vector2 at)
    {
        for (int i = 0; i < 5; i++)
        {
            Image spark = sparks[sparkCursor];
            sparkCursor = (sparkCursor + 1) % Sparks;
            spark.rectTransform.anchoredPosition = at;
            spark.rectTransform.localRotation = Quaternion.identity;
            spark.rectTransform.sizeDelta = new Vector2(18f, 18f);
            spark.color = Color.Lerp(new Color(0.7f, 1f, 0.75f, 1f), Color.white, i / 5f);
            spark.gameObject.SetActive(true);
            Vector2 velocity = Random.insideUnitCircle.normalized * Random.Range(80f, 220f);
            StartCoroutine(Fly(spark, velocity));
        }
    }

    IEnumerator Fly(Image spark, Vector2 velocity)
    {
        float life = 0.22f;
        float elapsed = 0f;
        Color color = spark.color;
        RectTransform rect = spark.rectTransform;
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            rect.anchoredPosition += velocity * Time.deltaTime;
            velocity *= 0.92f;
            color.a = 1f - elapsed / life;
            spark.color = color;
            yield return null;
        }

        spark.gameObject.SetActive(false);
    }

    Image Make(string name, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(layer, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(size, size);
        Image image = go.GetComponent<Image>();
        image.sprite = dot;
        image.raycastTarget = false;
        go.SetActive(false);
        return image;
    }

    static Sprite LineBar()
    {
        const int width = 8;
        const int height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[width * height];
        float half = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - half) / half;
                float a = Mathf.Clamp01(1f - dx);
                pixels[y * width + x] = new Color(1f, 1f, 1f, a * a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite SoftDot()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / radius);
                pixels[y * size + x] = new Color(1f, 1f, 1f, falloff * falloff);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Canvas Overlay()
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
}
