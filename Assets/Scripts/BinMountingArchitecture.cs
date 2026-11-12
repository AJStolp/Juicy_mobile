using UnityEngine;
using UnityEngine.UI;

// Welds each intake bin to the wall. The bin canvas sits on Gameplay order 8,
// so this mount uses order 7 and the chest covers the inner end of each girder.
public class BinMountingArchitecture : MonoBehaviour
{
    const int SortOrder = 7;

    static Sprite plate;
    static Sprite girder;
    static Sprite pad;
    static Sprite conduit;
    static Sprite hazard;
    static Sprite shadow;

    RectTransform bin;
    RectTransform root;
    int side;

    public static void Ensure(JuicyUIPop target, int sideIndex)
    {
        if (target == null) return;
        string name = target.name + "Mount";
        Transform parent = target.transform.parent;
        if (parent != null && parent.Find(name) != null) return;

        var go = new GameObject(name);
        var mount = go.AddComponent<BinMountingArchitecture>();
        mount.Build(target, sideIndex);
    }

    void Build(JuicyUIPop target, int sideIndex)
    {
        bin = target.transform as RectTransform;
        side = sideIndex;
        EnsureSprites();

        root = gameObject.AddComponent<RectTransform>();
        if (bin.parent != null) root.SetParent(bin.parent, false);
        root.anchorMin = bin.anchorMin;
        root.anchorMax = bin.anchorMax;
        root.pivot = bin.pivot;
        root.anchoredPosition = bin.anchoredPosition;
        root.sizeDelta = bin.sizeDelta;
        root.localScale = bin.localScale;
        root.SetSiblingIndex(bin.GetSiblingIndex());

        var inner = new GameObject("Hardware");
        var hardware = inner.AddComponent<RectTransform>();
        hardware.SetParent(root, false);
        hardware.anchorMin = hardware.anchorMax = new Vector2(0.5f, 0.5f);
        hardware.pivot = new Vector2(0.5f, 0.5f);
        hardware.anchoredPosition = Vector2.zero;
        hardware.sizeDelta = root.sizeDelta;
        hardware.localScale = new Vector3(side == 0 ? 1f : -1f, 1f, 1f);

        Image shade = Piece(hardware, "ContactShadow", shadow, new Vector2(0f, -168f), new Vector2(250f, 56f), new Vector2(0.5f, 0.5f));
        shade.color = new Color(0f, 0f, 0f, 0.72f);
        Piece(hardware, "BasePlate", plate, new Vector2(0f, -152f), new Vector2(228f, 40f), new Vector2(0.5f, 0.5f));
        Piece(hardware, "GirderHigh", girder, new Vector2(-130f, 48f), new Vector2(100f, 28f), new Vector2(0.5f, 0.5f));
        Piece(hardware, "GirderLow", girder, new Vector2(-130f, -36f), new Vector2(100f, 28f), new Vector2(0.5f, 0.5f));
        Piece(hardware, "WallPadHigh", pad, new Vector2(-168f, 48f), new Vector2(40f, 40f), new Vector2(0.5f, 0.5f));
        Piece(hardware, "WallPadLow", pad, new Vector2(-168f, -36f), new Vector2(40f, 40f), new Vector2(0.5f, 0.5f));
        Piece(hardware, "Conduit", conduit, new Vector2(-18f, -158f), new Vector2(22f, 300f), new Vector2(0.5f, 1f));
        Piece(hardware, "HazardTube", hazard, new Vector2(22f, -158f), new Vector2(16f, 270f), new Vector2(0.5f, 1f));

        VaultHeistController.SeatOnLayer(gameObject, "Gameplay", SortOrder);
        Debug.Log("Bin mount welded on " + target.name);
    }

    void LateUpdate()
    {
        if (bin == null || root == null) return;
        root.anchoredPosition = bin.anchoredPosition;
        root.localScale = bin.localScale;
    }

    static Image Piece(RectTransform parent, string name, Sprite sprite, Vector2 anchored, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.anchoredPosition = anchored;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    static void EnsureSprites()
    {
        if (plate != null) return;
        plate = Plate(160, 40);
        girder = Plate(96, 28);
        pad = Plate(48, 48);
        conduit = Pipe(24, 128);
        hazard = Stripes(20, 128);
        shadow = Ellipse(128, 48);
    }

    static Sprite Plate(int width, int height)
    {
        var pixels = new Color[width * height];
        float rx = (width - 1) * 0.5f;
        float ry = (height - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            float ny = y / (float)(height - 1);
            for (int x = 0; x < width; x++)
            {
                float nx = (x - rx) / rx;
                float edge = Mathf.Max(Mathf.Abs(nx), Mathf.Abs((y - ry) / ry));
                if (edge > 1f)
                {
                    pixels[y * width + x] = Color.clear;
                    continue;
                }

                Color steel = Color.Lerp(new Color(0.22f, 0.2f, 0.18f, 1f), new Color(0.62f, 0.58f, 0.52f, 1f), ny);
                if (edge > 0.86f) steel = Color.Lerp(steel, new Color(0.12f, 0.11f, 0.1f, 1f), 0.65f);
                pixels[y * width + x] = steel;
            }
        }

        StampRivet(pixels, width, height, 0.16f, 0.5f);
        StampRivet(pixels, width, height, 0.84f, 0.5f);
        if (width > 70)
        {
            StampRivet(pixels, width, height, 0.38f, 0.5f);
            StampRivet(pixels, width, height, 0.62f, 0.5f);
        }

        return Finish(pixels, width, height, new Vector2(0.5f, 0.5f));
    }

    static void StampRivet(Color[] pixels, int width, int height, float u, float v)
    {
        int cx = Mathf.RoundToInt(u * (width - 1));
        int cy = Mathf.RoundToInt(v * (height - 1));
        int radius = Mathf.Max(2, height / 7);
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / radius;
                if (d > 1f) continue;
                Color rivet = Color.Lerp(new Color(0.9f, 0.82f, 0.58f, 1f), new Color(0.35f, 0.28f, 0.16f, 1f), d);
                pixels[y * width + x] = rivet;
            }
        }
    }

    static Sprite Pipe(int width, int height)
    {
        var pixels = new Color[width * height];
        float cx = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float across = Mathf.Abs(x - cx) / cx;
                if (across > 1f)
                {
                    pixels[y * width + x] = Color.clear;
                    continue;
                }

                Color pipe = Color.Lerp(new Color(0.72f, 0.68f, 0.6f, 1f), new Color(0.16f, 0.15f, 0.14f, 1f), across);
                if (y % 18 < 3) pipe = Color.Lerp(pipe, new Color(0.1f, 0.09f, 0.08f, 1f), 0.55f);
                pixels[y * width + x] = pipe;
            }
        }

        return Finish(pixels, width, height, new Vector2(0.5f, 1f));
    }

    static Sprite Stripes(int width, int height)
    {
        var pixels = new Color[width * height];
        float cx = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            bool yellow = ((y / 10) % 2) == 0;
            for (int x = 0; x < width; x++)
            {
                float across = Mathf.Abs(x - cx) / cx;
                if (across > 1f)
                {
                    pixels[y * width + x] = Color.clear;
                    continue;
                }

                Color band = yellow
                    ? new Color(0.96f, 0.74f, 0.08f, 1f)
                    : new Color(0.08f, 0.07f, 0.06f, 1f);
                if (across > 0.72f) band = Color.Lerp(band, Color.black, 0.45f);
                pixels[y * width + x] = band;
            }
        }

        return Finish(pixels, width, height, new Vector2(0.5f, 1f));
    }

    static Sprite Ellipse(int width, int height)
    {
        var pixels = new Color[width * height];
        float rx = (width - 1) * 0.5f;
        float ry = (height - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - rx) / rx;
                float ny = (y - ry) / ry;
                float d = nx * nx + ny * ny;
                float alpha = Mathf.Clamp01(1f - d);
                alpha *= alpha;
                pixels[y * width + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        return Finish(pixels, width, height, new Vector2(0.5f, 0.5f));
    }

    static Sprite Finish(Color[] pixels, int width, int height, Vector2 pivot)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixels(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, width, height), pivot, 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
