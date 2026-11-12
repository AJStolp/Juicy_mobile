using UnityEngine;
using UnityEngine.UI;

// Level 1 getaway, parked under the vault on the center axis.
public class ExtractionCar : MonoBehaviour
{
    public static ExtractionCar Instance { get; private set; }

    const float HomeX = 0f;
    const float HomeY = -490f;

    RectTransform rect;
    Image image;
    Vector2 home;
    bool escaping;

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("ExtractionCar").AddComponent<ExtractionCar>();
    }

    public static void Escape()
    {
        Ensure();
        if (Instance != null) Instance.BeginEscape();
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

        rect = gameObject.AddComponent<RectTransform>();
        transform.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(460f, 200f);
        home = new Vector2(HomeX, HomeY);
        rect.anchoredPosition = home;

        image = gameObject.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UIHeroes/getaway_car");
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        if (image.sprite == null)
        {
            image.enabled = false;
            Debug.LogWarning("Getaway car sprite missing.");
        }

        transform.SetAsFirstSibling();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BeginEscape()
    {
        if (rect == null || escaping) return;
        escaping = true;
        transform.SetAsLastSibling();
        GetawayVehicleController.Burnout(rect, image, home);
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
