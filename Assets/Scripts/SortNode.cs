using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SortNode : MonoBehaviour
{
    static readonly int BlurId = Shader.PropertyToID("_MotionBlurIntensity");

    [SerializeField] private Image image;
    [SerializeField] private RectTransform rect;

    Material blurMaterial;
    float worldSizeBoost;
    Color baseColor;
    TextMeshProUGUI letterLabel;
    Sprite stockSprite;
    Vector2 stockSize;

    public enum NodeKind
    {
        Gem = 0,
        Bomb = 1,
        Gold = 2,
        Letter = 3
    }

    public int ColorId { get; private set; }
    public int Direction { get; private set; }
    public NodeKind Kind { get; private set; }
    public char Letter { get; private set; }
    public int PropSlot { get; set; } = -1;
    public bool HideSprite { get; set; }
    public RectTransform Rect => rect;

    void Awake()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (image == null) image = GetComponent<Image>();
        if (image != null) stockSprite = image.sprite;
        if (rect != null) stockSize = rect.sizeDelta;

        Shader shader = Shader.Find("Juicy/SortNodeBlur");
        if (shader != null && image != null)
        {
            blurMaterial = new Material(shader);
            image.material = blurMaterial;
        }
    }

    void OnDestroy()
    {
        if (blurMaterial != null) Destroy(blurMaterial);
    }

    public void Launch(int colorId, Color color, Vector2 start, NodeKind kind = NodeKind.Gem, char letter = '\0')
    {
        ColorId = colorId;
        Kind = kind;
        Letter = letter;
        Direction = 0;
        PropSlot = -1;
        HideSprite = false;
        worldSizeBoost = 0f;
        baseColor = color;
        Sprite hero = ItemCatalog.HeroSprite(kind, colorId);
        if (image != null)
        {
            image.raycastTarget = false;
            if (hero != null)
            {
                image.sprite = hero;
                image.preserveAspect = true;
                image.color = Color.white;
                image.material = null;
                if (rect != null) rect.sizeDelta = HeroSize(kind);
            }
            else
            {
                image.sprite = stockSprite;
                image.preserveAspect = false;
                image.color = color;
                if (blurMaterial != null) image.material = blurMaterial;
                if (rect != null && stockSize.sqrMagnitude > 1f) rect.sizeDelta = stockSize;
            }
        }

        ShowLetter('\0', null);
        rect.anchoredPosition = start;
        rect.localScale = Vector3.one;
        SetBlur(0f);
        gameObject.SetActive(true);
    }

    static Vector2 HeroSize(NodeKind kind)
    {
        Vector2 size = kind == NodeKind.Gold ? new Vector2(176f, 156f) : new Vector2(168f, 168f);
        return size * ItemCatalog.ItemPresentationScale;
    }

    public bool Hits(Vector2 screen)
    {
        if (rect == null) return false;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float minX = Mathf.Min(corners[0].x, corners[2].x);
        float maxX = Mathf.Max(corners[0].x, corners[2].x);
        float minY = Mathf.Min(corners[0].y, corners[2].y);
        float maxY = Mathf.Max(corners[0].y, corners[2].y);
        float grow = (ItemCatalog.TouchPadding - 1f) * 0.5f;
        float padX = (maxX - minX) * grow;
        float padY = (maxY - minY) * grow;
        return screen.x >= minX - padX && screen.x <= maxX + padX
            && screen.y >= minY - padY && screen.y <= maxY + padY;
    }

    public void ApplyCatalogLook()
    {
        Sprite hero = ItemCatalog.HeroSprite(Kind, ColorId);
        if (image != null)
        {
            image.raycastTarget = false;
            if (hero != null)
            {
                image.sprite = hero;
                image.preserveAspect = true;
                image.color = Color.white;
                image.material = null;
                if (rect != null) rect.sizeDelta = HeroSize(Kind);
            }
            else
            {
                image.sprite = stockSprite;
                image.preserveAspect = false;
                image.color = baseColor;
                if (blurMaterial != null) image.material = blurMaterial;
                if (rect != null && stockSize.sqrMagnitude > 1f) rect.sizeDelta = stockSize;
            }
        }

        if (ItemPropPool.Instance != null) ItemPropPool.Instance.Attach(this);
        SetBlur(worldSizeBoost);
    }

    public void ShowLetter(char letter, TMP_FontAsset font)
    {
        Letter = letter == '\0' ? Letter : letter;
        if (letter == '\0')
        {
            if (letterLabel != null) letterLabel.gameObject.SetActive(false);
            return;
        }

        if (letterLabel == null && font != null && rect != null)
        {
            var go = new GameObject("Letter", typeof(RectTransform));
            go.transform.SetParent(rect, false);
            var mark = go.GetComponent<RectTransform>();
            mark.anchorMin = Vector2.zero;
            mark.anchorMax = Vector2.one;
            mark.offsetMin = Vector2.zero;
            mark.offsetMax = Vector2.zero;
            letterLabel = go.AddComponent<TextMeshProUGUI>();
            letterLabel.font = font;
            letterLabel.fontSize = 42f;
            letterLabel.alignment = TextAlignmentOptions.Center;
            letterLabel.raycastTarget = false;
            letterLabel.color = new Color(1f, 0.95f, 0.72f, 1f);
        }

        if (letterLabel == null) return;
        letterLabel.text = letter.ToString();
        letterLabel.gameObject.SetActive(true);
    }

    public void Fling(int direction)
    {
        Direction = direction;
    }

    void Update()
    {
        if (image == null || HideSprite || Kind == NodeKind.Gem || Kind == NodeKind.Letter || Kind == NodeKind.Gold) return;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * (Kind == NodeKind.Bomb ? 9f : 6f));
        if (Kind == NodeKind.Bomb)
            image.color = Color.Lerp(new Color(0.08f, 0.04f, 0.04f), new Color(0.86f, 0.12f, 0.1f), wave);
        else
            image.color = Color.Lerp(baseColor, Color.white, wave * 0.55f);
    }

    public void SetBlur(float amount)
    {
        worldSizeBoost = amount;
        if (HideSprite && image != null)
        {
            Color hidden = image.color;
            hidden.a = 0f;
            image.color = hidden;
        }

        if (blurMaterial != null) blurMaterial.SetFloat(BlurId, amount);
        float stretch = Direction == 0 ? 1f + amount * 0.45f : 1f;
        float wide = Direction == 0 ? 1f : 1f + amount * 0.35f;
        rect.localScale = new Vector3(wide, stretch, 1f);
    }
}
