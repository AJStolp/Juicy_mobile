using UnityEngine;
using UnityEngine.UI;

// Pulses an existing material. Bind with the renderer's material so this
// does not clone a second one out from under VaultPortalController.
public class EmissionPulse : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [ColorUsage(true, true)]
    [SerializeField] private Color baseColor = new Color(2f, 0.2f, 0.2f, 1f);
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float minIntensity = 0.8f;
    [SerializeField] private float maxIntensity = 1.45f;

    Material instancedMaterial;
    Graphic glowGraphic;
    Color graphicBase = Color.white;
    bool bound;
    float heat;

    // Rest ceiling stays on the serialized max. Heat may lift it to 2.1
    // and no higher, so the crimson rim does not wash out.
    const float HeatCeiling = 2.1f;

    public void Bind(Material material, Color color)
    {
        instancedMaterial = material;
        baseColor = color;
        bound = true;
    }

    public void SetBaseColor(Color color)
    {
        baseColor = color;
    }

    public void BindGraphic(Graphic graphic)
    {
        glowGraphic = graphic;
        graphicBase = graphic != null ? graphic.color : Color.white;
    }

    public void SetHeat(float normalized)
    {
        heat = Mathf.Clamp01(normalized);
    }

    void Start()
    {
        if (bound || targetRenderer == null) return;
        instancedMaterial = targetRenderer.material;
    }

    void Update()
    {
        // Squared so a moderate heat stays near the rest glow. The rim holds its red.
        float shaped = heat * heat;
        float speed = Mathf.Lerp(pulseSpeed, pulseSpeed * 2.2f, shaped);
        float wave = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
        if (instancedMaterial != null)
        {
            float floor = Mathf.Lerp(minIntensity, 1.05f, shaped);
            float cap = Mathf.Lerp(maxIntensity, HeatCeiling, shaped);
            float emissionScale = Mathf.Lerp(floor, cap, wave);
            instancedMaterial.SetColor("_EmissionColor", baseColor * emissionScale);
        }

        if (glowGraphic == null) return;
        // Intake vaults are UI images, so the thrum is brightness rather than a shader emission slot.
        float uiMin = Mathf.Lerp(0.9f, 0.72f, shaped);
        Color hot = Color.Lerp(graphicBase, new Color(1f, 0.62f, 0.48f, graphicBase.a), shaped * 0.4f);
        glowGraphic.color = hot * Mathf.Lerp(uiMin, 1f, wave);
    }
}
