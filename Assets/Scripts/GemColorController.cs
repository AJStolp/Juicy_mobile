using UnityEngine;

public class GemColorController : MonoBehaviour
{
    Renderer targetRenderer;
    MaterialPropertyBlock propertyBlock;
    [SerializeField] Color gemColor = Color.white;
    [Tooltip("Emission as a fraction of the gem color. Needs a material with emission on.")]
    [SerializeField] float glow = 0.6f;

    void Awake()
    {
        Cache();
        SetGemColor(gemColor);
    }

    void OnValidate()
    {
        Cache();
        SetGemColor(gemColor);
    }

    void Cache()
    {
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
    }

    public void SetGemColor(Color color)
    {
        gemColor = color;
        if (targetRenderer == null || propertyBlock == null) Cache();
        if (targetRenderer == null) return;
        propertyBlock.SetColor("_BaseColor", gemColor);
        propertyBlock.SetColor("_Color", gemColor);
        propertyBlock.SetColor("_EmissionColor", gemColor * glow);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}
