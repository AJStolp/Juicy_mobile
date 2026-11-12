using System.Collections.Generic;
using UnityEngine;

// A color deposit chest at the bottom of the alley. Its glow seams, slot and
// emblems burn brighter as its vat fills, the lid snaps open when something
// drops in, and the whole chest flashes hot on a mistake.
public class AlleyBin : MonoBehaviour
{
    [SerializeField] int side;
    [SerializeField] Color color = Color.red;
    [SerializeField] Light glow;
    [SerializeField] Transform mouth;
    [SerializeField] Transform marker;
    [SerializeField] float restGlow = 1.2f;
    [SerializeField] float vatGlow = 3f;
    [SerializeField] float lidSwing = 38f;

    static readonly Color MistakeFlash = new Color(1f, 0.55f, 0.1f);
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    HeistSortRules rules;
    Vector3 baseScale;
    float punch;
    float mistake;
    float lidOpen;
    Transform lid;
    Quaternion lidRest;
    readonly List<Renderer> glowRenderers = new List<Renderer>();
    readonly List<int> glowSlots = new List<int>();
    MaterialPropertyBlock block;

    public int Side => side;
    public Color BinColor => color;
    public Vector3 Mouth => mouth != null ? mouth.position : transform.position;

    public void Configure(int binSide, Color binColor, Light binGlow, Transform binMouth, Transform binMarker)
    {
        side = binSide;
        color = binColor;
        glow = binGlow;
        mouth = binMouth;
        marker = binMarker;
    }

    public void Bind(HeistSortRules sortRules)
    {
        rules = sortRules;
    }

    void Awake()
    {
        baseScale = transform.localScale;
        block = new MaterialPropertyBlock();
        foreach (Transform child in GetComponentsInChildren<Transform>())
            if (child.name == "Lid") lid = child;
        if (lid != null) lidRest = lid.localRotation;

        // The glow material slots: seam, slot and emblems.
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (marker != null && renderer.transform.IsChildOf(marker)) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].name.Contains("Glow")) continue;
                glowRenderers.Add(renderer);
                glowSlots.Add(i);
            }
        }
    }

    public void Receive(SortResult result)
    {
        bool bad = result == SortResult.Wrong || result == SortResult.Bomb;
        punch = bad ? 0.6f : 1f;
        lidOpen = 1f;
        if (bad) mistake = 1f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        punch = Mathf.MoveTowards(punch, 0f, dt * 3.5f);
        mistake = Mathf.MoveTowards(mistake, 0f, dt * 2.5f);
        lidOpen = Mathf.MoveTowards(lidOpen, 0f, dt * 3f);
        float bounce = Mathf.Sin(punch * Mathf.PI * 3f) * punch * 0.12f;
        transform.localScale = baseScale * (1f + bounce);

        if (lid != null)
        {
            // Snap open, then settle shut with a small overshoot.
            float swing = lidOpen > 0.6f ? 1f : Mathf.Sin(lidOpen / 0.6f * Mathf.PI * 0.5f);
            lid.localRotation = lidRest * Quaternion.Euler(-lidSwing * swing, 0f, 0f);
        }

        if (marker != null)
        {
            marker.Rotate(0f, 70f * dt, 0f, Space.World);
            marker.localScale = Vector3.one * (1f + punch * 0.4f);
        }

        float vat = rules != null ? rules.Vat(side) : 0f;
        Color tint = Color.Lerp(color, MistakeFlash, mistake);
        float heat = 1.4f + vat * 4f + punch * 4f + mistake * 3f;
        for (int i = 0; i < glowRenderers.Count; i++)
        {
            glowRenderers[i].GetPropertyBlock(block, glowSlots[i]);
            block.SetColor(EmissionId, tint * heat);
            glowRenderers[i].SetPropertyBlock(block, glowSlots[i]);
        }

        if (glow == null) return;
        glow.color = tint;
        glow.intensity = restGlow + vat * vatGlow + punch * 2.5f + mistake * 3f;
    }
}
