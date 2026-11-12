using UnityEngine;

// Scrolls a sky layer's texture sideways so the clouds drift past the moon.
// Uses a property block, so the shared material is never modified.
public class CloudDrift : MonoBehaviour
{
    [Tooltip("Texture widths per second.")]
    [SerializeField] float speed = 0.005f;
    [SerializeField] float tiling = 1f;

    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    Renderer target;
    MaterialPropertyBlock block;
    float offset;

    public void Configure(float scrollSpeed, float horizontalTiling)
    {
        speed = scrollSpeed;
        tiling = horizontalTiling;
    }

    void Awake()
    {
        target = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        offset = Random.value;
    }

    void Update()
    {
        offset = Mathf.Repeat(offset + speed * Time.deltaTime, 1f);
        target.GetPropertyBlock(block);
        block.SetVector(BaseMapST, new Vector4(tiling, 1f, offset, 0f));
        target.SetPropertyBlock(block);
    }
}
