using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The end of a won level: its clue, on a card under a silver scratch layer.
// The player rubs the layer off; once most of it is gone the rest falls away
// and the clue shows, as clearly as the attempt's best streak allowed (grime
// that will not scratch off covers part of a less clear clue). A tap then
// pins it to the crime board and goes back to the map. Drawn in front of the
// camera (CameraOverlay), above the HUD and the map.
public class AlleyClueReveal : MonoBehaviour
{
    static readonly Color Neon = new Color(1f, 0.85f, 0.25f);
    static readonly Color Paper = new Color(0.97f, 0.95f, 0.9f);
    static readonly Color[] Silver = { new Color(0.72f, 0.72f, 0.76f), new Color(0.8f, 0.8f, 0.83f) };
    static readonly Color Grime = new Color(0.27f, 0.22f, 0.17f, 0.93f);
    static readonly string[] Clarity = { "BARELY READABLE", "SMUDGED", "CLEAR", "CRYSTAL CLEAR" };

    const float Distance = 0.3f;
    const int Columns = 12;
    const int Rows = 6;
    // Share of the silver to rub off before the rest falls away.
    const float RevealAt = 0.65f;
    // Brush radius, in viewport heights.
    const float Brush = 0.035f;
    const float TapGuard = 0.4f;

    static readonly Vector2 CardAt = new Vector2(0.5f, 0.52f);
    static readonly Vector2 CardSize = new Vector2(0.76f, 0.3f);
    static readonly Vector2 CoverSize = new Vector2(0.7f, 0.26f);

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Material panelMaterial;

    CameraOverlay overlay;
    GameObject content;
    readonly List<Renderer> cells = new List<Renderer>();
    readonly List<Vector2> cellCenters = new List<Vector2>();
    TextMeshPro instruction;
    GameObject result;
    int cleared;
    float revealedAt;

    public bool Showing => content != null && content.activeSelf;
    public bool Revealed { get; private set; }
    public float Cleared => cells.Count == 0 ? 0f : cleared / (float)cells.Count;

    public void Configure(AlleyChuteDirector chute, Material material)
    {
        director = chute;
        panelMaterial = material;
    }

    void Start()
    {
        if (!director.UsesReveal)
        {
            gameObject.SetActive(false);
            return;
        }
        Camera view = Camera.main;
        overlay = new CameraOverlay(view, panelMaterial, Distance, CameraOverlay.LayerReveal);
        transform.SetParent(view.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        director.RevealOpened += Show;
    }

    void OnDestroy()
    {
        if (director != null) director.RevealOpened -= Show;
    }

    public void Show(int levelIndex, Clue clue)
    {
        if (content != null) Destroy(content);
        content = new GameObject("Reveal");
        content.transform.SetParent(transform, false);
        Transform root = content.transform;
        cells.Clear();
        cellCenters.Clear();
        cleared = 0;
        Revealed = false;

        overlay.Quad(root, "Dim", new Vector2(0.5f, 0.5f), new Vector2(1.3f, 1.3f), new Color(0.01f, 0.01f, 0.03f, 0.88f), 0.008f);
        TextMeshPro title = overlay.Text(root, "Title", new Vector2(0.5f, 0.8f), new Vector2(0.8f, 0.05f), Neon);
        title.text = director.Place.Level(levelIndex).Name.ToUpperInvariant() + " CLUE";
        instruction = overlay.Text(root, "Instruction", new Vector2(0.5f, 0.735f), new Vector2(0.8f, 0.03f), Color.white);
        instruction.text = "RUB TO SCRATCH IT OFF";

        overlay.Quad(root, "Card", CardAt, CardSize, Paper, 0.006f);
        TextMeshPro glyph = overlay.Text(root, "Glyph", CardAt, new Vector2(0.4f, 0.22f), AlleyHack.SymbolColors[clue.Symbol], 0.004f);
        glyph.text = AlleyHack.Glyphs[clue.Symbol];
        // Over the card, under the grime and the silver, unlike other overlay text.
        glyph.sortingOrder = overlay.Order(0.004f);

        Grimes(root, clue);
        Vector2 cell = new Vector2(CoverSize.x / Columns, CoverSize.y / Rows);
        Vector2 corner = CardAt - CoverSize * 0.5f + cell * 0.5f;
        for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                Vector2 at = corner + new Vector2(column * cell.x, row * cell.y);
                cells.Add(overlay.Quad(root, "Silver_" + row + "_" + column, at, cell * 1.02f, Silver[(row + column) % 2], 0.001f));
                cellCenters.Add(at);
            }

        result = new GameObject("Result");
        result.transform.SetParent(root, false);
        TextMeshPro clarity = overlay.Text(result.transform, "Clarity", new Vector2(0.5f, 0.335f), new Vector2(0.8f, 0.045f), AlleyHack.SymbolColors[clue.Symbol]);
        clarity.text = Clarity[ClueRules.ClampTier(clue.Tier)];
        if (clue.Tier < ClueRules.MaxTier)
        {
            TextMeshPro hint = overlay.Text(result.transform, "Hint", new Vector2(0.5f, 0.29f), new Vector2(0.8f, 0.025f), Color.white);
            hint.text = "A LONGER STREAK SHOWS IT CLEARER";
        }
        TextMeshPro next = overlay.Text(result.transform, "Continue", new Vector2(0.5f, 0.2f), new Vector2(0.8f, 0.035f), Neon);
        next.text = "TAP TO PIN IT TO THE BOARD";
        result.SetActive(false);
    }

    // Grime that will not scratch off, over part of the glyph: the less clear
    // the clue, the more of it. Placed the same way for a given symbol.
    void Grimes(Transform root, Clue clue)
    {
        int count = Mathf.RoundToInt(clue.Covered * 24f);
        if (count <= 0) return;
        var spots = new List<int>();
        for (int i = 0; i < 24; i++) spots.Add(i);
        var random = new System.Random(clue.Symbol * 31 + 5);
        Vector2 cell = new Vector2(CoverSize.x / Columns, CoverSize.y / Rows);
        Vector2 corner = CardAt - new Vector2(3f * cell.x, 2f * cell.y) + cell * 0.5f;
        for (int i = 0; i < count; i++)
        {
            int pick = random.Next(spots.Count);
            int spot = spots[pick];
            spots.RemoveAt(pick);
            Vector2 at = corner + new Vector2(spot % 6 * cell.x, spot / 6 * cell.y);
            overlay.Quad(root, "Grime_" + spot, at, cell * 1.15f, Grime, 0.002f, random.Next(-20, 20));
        }
    }

    void Update()
    {
        if (!Showing) return;
        Pointer pointer = Pointer.current;
        if (!Revealed)
        {
            if (pointer != null && pointer.press.isPressed) ScratchScreen(pointer.position.ReadValue());
            return;
        }
        if (Time.unscaledTime < revealedAt + TapGuard) return;
        if (pointer != null && pointer.press.wasReleasedThisFrame) Continue();
    }

    void ScratchScreen(Vector2 screen)
    {
        float radius = Brush * Screen.height;
        for (int i = 0; i < cells.Count; i++)
        {
            if (!cells[i].gameObject.activeSelf) continue;
            Rect rect = overlay.ScreenRect(cells[i]);
            float dx = Mathf.Max(rect.xMin - screen.x, 0f, screen.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - screen.y, 0f, screen.y - rect.yMax);
            if (dx * dx + dy * dy <= radius * radius) Clear(i);
        }
        CheckRevealed();
    }

    // Scratches at a point in the overlay's viewport terms (for checks that
    // drive the card without a pointer).
    public void ScratchAt(Vector2 viewport)
    {
        if (!Showing || Revealed) return;
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2 delta = cellCenters[i] - viewport;
            delta.x *= 9f / 16f;
            if (cells[i].gameObject.activeSelf && delta.magnitude <= Brush * 1.5f) Clear(i);
        }
        CheckRevealed();
    }

    void Clear(int index)
    {
        cells[index].gameObject.SetActive(false);
        cleared++;
    }

    void CheckRevealed()
    {
        if (Revealed || Cleared < RevealAt) return;
        for (int i = 0; i < cells.Count; i++)
            if (cells[i].gameObject.activeSelf) Clear(i);
        Revealed = true;
        revealedAt = Time.unscaledTime;
        instruction.gameObject.SetActive(false);
        result.SetActive(true);
    }

    // Pins the clue to the board: back to the map.
    public void Continue()
    {
        if (!Revealed) return;
        content.SetActive(false);
        if (director.UsesMap) director.OpenMap();
        else director.Retry();
    }
}
