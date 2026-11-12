using UnityEngine;

// Animates a light, and optionally a glowing mesh, from a named pattern.
// The pattern is a pure function of time so editor tools can preview any
// moment without entering Play mode.
[RequireComponent(typeof(Light))]
public class LightAnimator : MonoBehaviour
{
    public enum Pattern { Flicker, Pulse, Strobe, Buzz }

    [SerializeField] Pattern pattern = Pattern.Flicker;
    [SerializeField] float baseIntensity = 1f;
    [Tooltip("Cycles per second.")]
    [SerializeField] float speed = 1f;
    [Tooltip("How far intensity dips, as a fraction of base.")]
    [Range(0f, 1f)] [SerializeField] float depth = 0.25f;
    [Tooltip("Offset in cycles so paired lights do not move in sync.")]
    [SerializeField] float phase;
    [SerializeField] Renderer glow;
    [ColorUsage(true, true)] [SerializeField] Color glowColor = Color.white;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    Light target;
    MaterialPropertyBlock block;
    float driveSpeed = 1f;
    float driveIntensity = 1f;
    float cycles;

    public void Configure(Pattern newPattern, float intensity, float cyclesPerSecond, float dip, float offset, Renderer glowRenderer, Color glowTint)
    {
        pattern = newPattern;
        baseIntensity = intensity;
        speed = cyclesPerSecond;
        depth = dip;
        phase = offset;
        glow = glowRenderer;
        glowColor = glowTint;
    }

    // Scales the pattern at runtime, for example faster and brighter with heat.
    public void SetDrive(float speedScale, float intensityScale)
    {
        driveSpeed = Mathf.Max(0f, speedScale);
        driveIntensity = Mathf.Max(0f, intensityScale);
    }

    void Update()
    {
        // Accumulate cycles so a change in drive speed never jumps the phase.
        cycles += Time.deltaTime * speed * driveSpeed;
        ApplyCycles(cycles + phase);
    }

    public void Apply(float time)
    {
        ApplyCycles(time * speed + phase);
    }

    void ApplyCycles(float t)
    {
        if (target == null) target = GetComponent<Light>();
        float level = Evaluate(pattern, t, depth) * driveIntensity;
        target.intensity = baseIntensity * level;
        if (glow == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        glow.GetPropertyBlock(block);
        block.SetColor(BaseColorId, glowColor * level);
        glow.SetPropertyBlock(block);
    }

    // t is in cycles. Returns a multiplier on base intensity.
    public static float Evaluate(Pattern pattern, float t, float depth)
    {
        switch (pattern)
        {
            case Pattern.Pulse:
                return 1f - depth * 0.5f * (1f + Mathf.Sin(t * Mathf.PI * 2f));
            case Pattern.Strobe:
                // Police double flash in the first third of each cycle.
                float cycle = t - Mathf.Floor(t);
                return cycle < 0.1f || (cycle > 0.18f && cycle < 0.28f) ? 1f : 0f;
            case Pattern.Buzz:
                // Sodium lamp: steady hum, and now and then it nearly drops out.
                float hum = 1f - depth * Mathf.PerlinNoise(t * 9f, 0.3f);
                return Mathf.PerlinNoise(t * 0.5f, 7.1f) > 0.74f ? hum * 0.2f : hum;
            default:
                return 1f - depth * Mathf.PerlinNoise(t * 3f, 1.7f);
        }
    }
}
