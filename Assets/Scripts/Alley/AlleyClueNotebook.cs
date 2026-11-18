using UnityEngine;
using UnityEngine.UI;

// Option A for where clues are kept: a notebook in the top left corner of the
// screen, on the project's first screen canvas. Each clue found lands on the
// next empty slot, in the order found.
public class AlleyClueNotebook : MonoBehaviour
{
    [SerializeField] Canvas canvas;
    [SerializeField] RectTransform page;
    [SerializeField] RawImage[] slots;

    float[] punch;

    public Canvas Canvas => canvas;
    public RectTransform Page => page;
    public int Shown { get; private set; }

    public void Configure(Canvas screen, RectTransform notebookPage, RawImage[] clueSlots)
    {
        canvas = screen;
        page = notebookPage;
        slots = clueSlots;
    }

    void Awake()
    {
        punch = new float[slots.Length];
        if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
    }

    public void SetVisible(bool visible) => canvas.enabled = visible;

    public RectTransform Slot(int index) => slots[Mathf.Clamp(index, 0, slots.Length - 1)].rectTransform;

    // Shows the clues in the cycle, in order, with the cover each was found at.
    public void Show(System.Collections.Generic.IReadOnlyList<Clue> clues, int seed)
    {
        Shown = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            bool has = clues != null && i < clues.Count;
            slots[i].texture = has ? ClueSymbols.Texture(clues[i].Symbol, clues[i].Covered, seed + i) : null;
            slots[i].color = has ? Color.white : new Color(0.16f, 0.14f, 0.12f, 0.35f);
            if (has) Shown++;
        }
    }

    public void Land(int index)
    {
        if (punch != null && index >= 0 && index < punch.Length) punch[index] = 1f;
    }

    void Update()
    {
        if (punch == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (punch[i] <= 0f) continue;
            punch[i] = Mathf.MoveTowards(punch[i], 0f, Time.deltaTime * 2.5f);
            slots[i].rectTransform.localScale = Vector3.one * (1f + 0.35f * punch[i] * punch[i]);
        }
    }
}
