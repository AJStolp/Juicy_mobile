using UnityEngine;

public class MissileFx : MonoBehaviour
{
    public static MissileFx Instance { get; private set; }

    TrailRenderer trail;
    Transform trailAnchor;
    ParticleSystem explosion;
    bool trailOn;

    public bool TrailPlayed { get; private set; }
    public int Explosions { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Ensure();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void BeginArc(Vector3 world)
    {
        Ensure();
        if (trailAnchor == null || trail == null) return;
        trailAnchor.gameObject.SetActive(true);
        trailAnchor.position = world;
        trail.Clear();
        trail.emitting = true;
        trailOn = true;
        TrailPlayed = true;
    }

    public void MoveArc(Vector3 world)
    {
        if (!trailOn || trailAnchor == null) return;
        trailAnchor.position = world;
    }

    public void EndArc()
    {
        if (trail == null) return;
        trail.emitting = false;
        trailOn = false;
    }

    public void Explode(Vector2 localPoint, RectTransform layer)
    {
        Ensure();
        if (explosion == null) return;
        Vector3 screen = layer != null ? layer.TransformPoint(localPoint) : new Vector3(localPoint.x, localPoint.y, 0f);
        explosion.transform.position = WorldFollow.UiToWorld(screen);
        explosion.gameObject.SetActive(true);
        explosion.Clear();
        explosion.Play();
        Explosions++;
    }

    void Ensure()
    {
        if (trail != null && explosion != null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material material = shader != null ? new Material(shader) : null;
        if (material != null)
        {
            material.hideFlags = HideFlags.HideAndDontSave;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(1f, 0.55f, 0.12f, 1f));
        }

        if (trail == null)
        {
            var go = new GameObject("MissileTrail");
            go.transform.SetParent(transform, false);
            trailAnchor = go.transform;
            trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.34f;
            trail.minVertexDistance = 0.02f;
            trail.startWidth = 0.1f;
            trail.endWidth = 0.01f;
            trail.numCornerVertices = 2;
            trail.autodestruct = false;
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            if (material != null) trail.material = material;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.72f, 0.28f), 0f),
                    new GradientColorKey(new Color(0.95f, 0.28f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = gradient;
            go.SetActive(true);
        }

        if (explosion == null)
        {
            var go = new GameObject("MissileExplosion");
            go.transform.SetParent(transform, false);
            explosion = go.AddComponent<ParticleSystem>();
            var main = explosion.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = 0.45f;
            main.startSpeed = 3.6f;
            main.startSize = 0.14f;
            main.maxParticles = 36;
            main.startColor = new Color(1f, 0.5f, 0.12f, 1f);
            var emission = explosion.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var shape = explosion.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            var renderer = explosion.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material != null) renderer.material = material;
            explosion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
