using TMPro;
using UnityEngine;

// Draws flat screens (the level HUD, the map) as text and quads just in front
// of the camera, so every capture of the camera includes them. Positions are
// in viewport terms: (0, 0) bottom left, (1, 1) top right. `back` pushes a
// piece further from the camera and sets its draw order: see-through pieces
// sort by distance to their centre, so a big backdrop would otherwise cover
// small pieces near the screen's edges. Text draws on top of its own screen's
// quads. Each screen has its own band of draw orders (`layer`), so a screen
// in front (the clue reveal) always covers the ones behind (the map, the HUD).
public class CameraOverlay
{
    readonly Camera view;
    readonly Material material;
    readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    readonly int baseOrder;

    public float Distance { get; }

    public const int LayerHud = 0;
    public const int LayerMap = 1;
    public const int LayerReveal = 2;

    public CameraOverlay(Camera camera, Material panelMaterial, float distance, int layer)
    {
        baseOrder = layer * LayerSpan;
        view = camera;
        material = panelMaterial;
        Distance = distance;
    }

    // Size of the view at the overlay's distance, in metres. The game is
    // portrait, so the layout never takes a wider aspect than 9:16 (a batch
    // run's screen is landscape even though its captures are portrait).
    public Vector2 ViewSize()
    {
        float height = 2f * Distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return new Vector2(height * Mathf.Min(view.aspect, 9f / 16f), height);
    }

    const int TextOrder = 100;
    const int LayerSpan = 200;

    // Nearer pieces draw later: back 0.01 and further is 0, back 0 is 50.
    int OrderFor(float back) => baseOrder + Mathf.Clamp(Mathf.RoundToInt((0.01f - back) * 5000f), 0, TextOrder - 1);

    // For a piece of text that must sit under this screen's quads.
    public int Order(float back) => OrderFor(back);

    public Vector3 Place(Vector2 viewport, float back = 0f)
    {
        Vector2 size = ViewSize();
        return new Vector3((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y, Distance + back);
    }

    public TextMeshPro Text(Transform parent, string name, Vector2 viewport, Vector2 extent, Color color, float back = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Place(viewport, back);
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
        text.sortingOrder = baseOrder + TextOrder;
        return text;
    }

    public Renderer Quad(Transform parent, string name, Vector2 viewport, Vector2 extent, Color color, float back, float degrees = 0f)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = Place(viewport, back);
        quad.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
        Vector2 size = ViewSize();
        quad.transform.localScale = new Vector3(extent.x * size.x, extent.y * size.y, 1f);
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = OrderFor(back);
        Tint(renderer, color);
        return renderer;
    }

    // A straight bar from one viewport point to another, `thickness` in
    // viewport heights.
    public Renderer Line(Transform parent, string name, Vector2 from, Vector2 to, float thickness, Color color, float back)
    {
        Vector2 size = ViewSize();
        Vector2 a = new Vector2(from.x * size.x, from.y * size.y);
        Vector2 b = new Vector2(to.x * size.x, to.y * size.y);
        Vector2 delta = b - a;
        float degrees = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Renderer line = Quad(parent, name, (from + to) * 0.5f, Vector2.one, color, back, degrees);
        line.transform.localScale = new Vector3(delta.magnitude, thickness * size.y, 1f);
        return line;
    }

    public void Tint(Renderer renderer, Color color)
    {
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
    }

    public Rect ScreenRect(Renderer renderer)
    {
        Bounds b = renderer.bounds;
        Vector3 min = view.WorldToScreenPoint(b.min);
        Vector3 max = view.WorldToScreenPoint(b.max);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }
}
