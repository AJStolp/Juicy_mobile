using UnityEngine;

public class LiquidVatView : MonoBehaviour
{
    public static LiquidVatView Instance { get; private set; }

    RectTransform[] mouths = new RectTransform[2];
    Transform[] shells = new Transform[2];
    Transform[] liquids = new Transform[2];
    Material[] liquidMats = new Material[2];
    Material[] shellMats = new Material[2];
    ParticleSystem[] splashes = new ParticleSystem[2];
    bool ready;

    static readonly Color[] Juice =
    {
        new Color(0.78f, 0.02f, 0.045f, 1f),
        new Color(0.045f, 0.07f, 0.48f, 1f)
    };

    static readonly Color[] ShellTint =
    {
        new Color(0.42f, 0.035f, 0.05f, 1f),
        new Color(0.045f, 0.06f, 0.34f, 1f)
    };

    public int Splashes { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Ensure();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Bind(JuicyUIPop left, JuicyUIPop right)
    {
        Ensure();
        mouths[0] = left != null ? left.transform as RectTransform : null;
        mouths[1] = right != null ? right.transform as RectTransform : null;
    }

    public void SetFill(int side, float amount)
    {
        if (side < 0 || side > 1 || liquids[side] == null) return;
        float fill = Mathf.Clamp01(amount);
        float height = Mathf.Lerp(0.04f, 0.78f, fill);
        liquids[side].localScale = new Vector3(0.7f, height, 0.7f);
        liquids[side].localPosition = new Vector3(0f, -0.46f + height * 0.5f, 0f);
        if (liquidMats[side] != null)
        {
            liquidMats[side].SetFloat("_Fill", fill);
            liquidMats[side].SetFloat("_Wobble", 0.05f + fill * 0.04f);
        }
    }

    public float FillHeight(int side)
    {
        if (side < 0 || side > 1 || liquids[side] == null) return 0f;
        return liquids[side].localScale.y;
    }

    public Color LiquidColor(int side)
    {
        if (side < 0 || side > 1 || liquidMats[side] == null) return Color.black;
        return liquidMats[side].GetColor("_BaseColor");
    }

    public Color ShellColor(int side)
    {
        if (side < 0 || side > 1 || shellMats[side] == null) return Color.black;
        return shellMats[side].GetColor("_BaseColor");
    }

    public void Splash(int side)
    {
        if (side < 0 || side > 1 || splashes[side] == null) return;
        ParticleSystem splash = splashes[side];
        if (mouths[side] != null) splash.transform.position = WorldFollow.UiToWorld(mouths[side]);
        var main = splash.main;
        main.startColor = Juice[side];
        splash.gameObject.SetActive(true);
        splash.Clear();
        splash.Play();
        Splashes++;
    }

    void LateUpdate()
    {
        for (int side = 0; side < 2; side++)
        {
            bool show = mouths[side] != null && mouths[side].gameObject.activeInHierarchy;
            if (shells[side] != null) shells[side].gameObject.SetActive(show);
            if (!show || shells[side] == null) continue;
            WorldFollow.Place(shells[side], mouths[side]);
            if (liquids[side] == null) continue;
            float fill = liquids[side].localScale.y;
            float slosh = Mathf.Clamp01((fill - 0.04f) / 0.74f) * 7f;
            liquids[side].localRotation = Quaternion.Euler(
                Mathf.Sin(Time.time * 2.6f + side) * slosh,
                0f,
                Mathf.Cos(Time.time * 2.1f + side) * slosh * 0.65f);
        }
    }

    void Ensure()
    {
        if (ready) return;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Unlit");

        Shader liquidShader = Shader.Find("Juicy/LiquidFill");
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null) particleShader = Shader.Find("Sprites/Default");

        for (int side = 0; side < 2; side++)
        {
            var shell = new GameObject(side == 0 ? "CrimsonVat" : "IndigoVat");
            shell.transform.SetParent(transform, false);
            var filter = shell.AddComponent<MeshFilter>();
            filter.sharedMesh = ItemCatalog.VatMesh;
            var renderer = shell.AddComponent<MeshRenderer>();
            if (lit != null)
            {
                shellMats[side] = new Material(lit);
                ItemCatalog.PrepareLit(shellMats[side], lit);
                shellMats[side].SetColor("_BaseColor", ShellTint[side]);
                shellMats[side].SetColor("_EmissionColor", ShellTint[side] * 0.55f);
                renderer.sharedMaterial = shellMats[side];
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            shells[side] = shell.transform;

            var liquidGo = new GameObject("Liquid");
            liquidGo.transform.SetParent(shell.transform, false);
            var liquidFilter = liquidGo.AddComponent<MeshFilter>();
            liquidFilter.sharedMesh = ItemCatalog.LiquidMesh;
            var liquidRenderer = liquidGo.AddComponent<MeshRenderer>();
            if (liquidShader != null)
            {
                liquidMats[side] = new Material(liquidShader);
                liquidMats[side].hideFlags = HideFlags.HideAndDontSave;
                liquidMats[side].SetColor("_BaseColor", Juice[side]);
                liquidMats[side].SetFloat("_Fill", 0f);
                liquidRenderer.sharedMaterial = liquidMats[side];
            }
            else if (lit != null)
            {
                liquidMats[side] = new Material(lit);
                liquidMats[side].hideFlags = HideFlags.HideAndDontSave;
                liquidMats[side].SetColor("_BaseColor", Juice[side]);
                liquidRenderer.sharedMaterial = liquidMats[side];
            }

            liquidRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            liquidRenderer.receiveShadows = false;
            liquidRenderer.enabled = false;
            liquids[side] = liquidGo.transform;
            SetFill(side, 0f);

            var splashGo = new GameObject(side == 0 ? "CrimsonSplash" : "IndigoSplash");
            splashGo.transform.SetParent(transform, false);
            var splash = splashGo.AddComponent<ParticleSystem>();
            ConfigureSplash(splash, particleShader, Juice[side]);
            splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            splashes[side] = splash;
        }

        ready = true;
    }

    static void ConfigureSplash(ParticleSystem splash, Shader shader, Color color)
    {
        var main = splash.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.35f;
        main.startLifetime = 0.4f;
        main.startSpeed = 2.4f;
        main.startSize = 0.08f;
        main.maxParticles = 24;
        main.startColor = color;
        var emission = splash.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
        var shape = splash.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.12f;
        var renderer = splash.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (shader != null)
        {
            var material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            renderer.material = material;
        }
    }
}
