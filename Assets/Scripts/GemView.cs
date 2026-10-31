using UnityEngine;

public class GemView : MonoBehaviour
{
    [SerializeField] private float worldSize = 0.26f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    Renderer gemRenderer;
    MaterialPropertyBlock block;

    public int GemColorId { get; private set; }

    void Awake()
    {
        CacheRenderer();
    }

    public void ApplyColor(int colorId, Color color)
    {
        CacheRenderer();
        GemColorId = colorId;
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        gemRenderer.SetPropertyBlock(block);
    }

    public void AttachTo(Transform tube)
    {
        transform.SetParent(tube, true);
        Vector3 lossy = tube.lossyScale;
        transform.localScale = new Vector3(
            SafeDivide(worldSize, lossy.x),
            SafeDivide(worldSize, lossy.y),
            SafeDivide(worldSize, lossy.z));
    }

    void CacheRenderer()
    {
        if (gemRenderer == null) gemRenderer = GetComponent<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();
    }

    static float SafeDivide(float size, float scale)
    {
        return Mathf.Abs(scale) < 0.0001f ? size : size / scale;
    }
}
