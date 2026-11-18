using System.Collections.Generic;
using UnityEngine;

// Bulbs on the Plinko pins, for the street arcade look. A pin strike lights
// its bulb white hot and it fades back to a dim glow. The bulbs share one
// unlit material; each bulb's color is set through a property block, above 1
// when lit so the bloom catches it.
public class AlleyPlinkoBulbs : MonoBehaviour
{
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] Transform[] pins;
    [SerializeField] Renderer[] bulbs;
    [ColorUsage(false, true)] [SerializeField] Color idle = new Color(0.5f, 0.3f, 0.12f);
    [ColorUsage(false, true)] [SerializeField] Color flash = new Color(8f, 7f, 5f);
    [SerializeField] float fadeTime = 0.35f;

    float[] levels;
    Dictionary<Transform, int> index;
    MaterialPropertyBlock block;

    public int Count => bulbs != null ? bulbs.Length : 0;

    public void Configure(Transform[] pinTransforms, Renderer[] bulbRenderers, Color idleColor, Color flashColor)
    {
        pins = pinTransforms;
        bulbs = bulbRenderers;
        idle = idleColor;
        flash = flashColor;
        levels = null;
        Prepare();
    }

    // How lit a bulb is, from 0 (idle glow) to 1 (just struck).
    public float Level(int bulb)
    {
        Prepare();
        return levels[bulb];
    }

    public int IndexOf(Transform pin)
    {
        Prepare();
        return index.TryGetValue(pin, out int bulb) ? bulb : -1;
    }

    public void Flash(Transform pin)
    {
        int bulb = IndexOf(pin);
        if (bulb < 0) return;
        levels[bulb] = 1f;
        Apply(bulb);
    }

    public void Fade(float seconds)
    {
        Prepare();
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] <= 0f) continue;
            levels[i] = Mathf.Max(0f, levels[i] - seconds / fadeTime);
            Apply(i);
        }
    }

    void Update()
    {
        Fade(Time.deltaTime);
    }

    void Prepare()
    {
        if (levels != null && levels.Length == Count) return;
        levels = new float[Count];
        index = new Dictionary<Transform, int>();
        block = new MaterialPropertyBlock();
        for (int i = 0; i < Count; i++)
        {
            if (pins[i] != null) index[pins[i]] = i;
            Apply(i);
        }
    }

    void Apply(int bulb)
    {
        if (bulbs[bulb] == null) return;
        float k = levels[bulb];
        block.SetColor(ColorId, Color.Lerp(idle, flash, k * k));
        bulbs[bulb].SetPropertyBlock(block);
    }
}
