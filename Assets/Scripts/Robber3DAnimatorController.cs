using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Rigged robber in the playfield slot. A portrait camera draws the mesh
// into the rect, and the clips follow the walk, the peek, and the leap.
public class Robber3DAnimatorController : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int PanicHash = Animator.StringToHash("Panic");
    static readonly int LeapHash = Animator.StringToHash("Leap");

    const float FacingYaw = 0f;
    const int PortraitLayer = 6;

    GameObject rig;
    Animator animator;
    readonly List<Material> paints = new List<Material>();
    readonly List<Color> paintColors = new List<Color>();
    RectTransform follow;
    RectTransform portraitView;
    Camera portraitCam;
    RenderTexture portrait;
    Vector3 focusLocal = new Vector3(0f, 0.8f, 0f);
    float meshHeight = 1.6f;
    string lastClip = "";
    bool ready;
    bool hiddenFromMain;

    public bool Ready => ready;

    public bool Build(RectTransform root)
    {
        follow = root;
        GameObject prefab = Resources.Load<GameObject>("Heroes/Robber_Rig");
        if (prefab == null)
        {
            Debug.LogWarning("Robber mesh missing.");
            return false;
        }

        rig = Instantiate(prefab);
        rig.name = "RobberMesh";
        animator = rig.GetComponentInChildren<Animator>();
        if (animator == null) animator = rig.AddComponent<Animator>();
        EnsureAvatar(animator);
        animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Heroes/Robber3DAnimator");
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (animator.runtimeAnimatorController == null)
            Debug.LogWarning("Robber 3D animator missing. Rest pose only.");
        else
            animator.Play("Idle_Ledge", 0, 0f);

        Lit(rig);
        Measure(rig);
        SetLayer(rig, PortraitLayer);
        MountPortrait();
        ready = true;
        Seat();
        FramePortrait();
        Debug.Log("Robber mesh is in the 3D slot. height " + meshHeight.ToString("0.00"));
        return true;
    }

    public void SetLocomotion(float speed, bool panic)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetFloat(SpeedHash, speed);
        animator.SetBool(PanicHash, panic);
        float rate = 1f;
        if (speed > 20f) rate = Mathf.Clamp(speed / 320f, 0.75f, 1.7f);
        animator.speed = rate;
    }

    public void TriggerLeap()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetTrigger(LeapHash);
    }

    public void SetHeat(float shown01)
    {
        float shaped = Mathf.Clamp01(shown01);
        shaped *= shaped;
        Color tint = Color.Lerp(Color.white, new Color(1f, 0.62f, 0.5f), shaped * 0.4f);
        for (int i = 0; i < paints.Count; i++)
        {
            if (paints[i] == null) continue;
            if (paints[i].HasProperty("_BaseColor"))
                paints[i].SetColor("_BaseColor", paintColors[i] * tint);
        }
    }

    public void Hide()
    {
        if (rig != null) rig.SetActive(false);
        if (portraitView != null) portraitView.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!ready) return;
        Seat();
        FramePortrait();
        NoteClip();
    }

    void OnDestroy()
    {
        if (portraitCam != null) Destroy(portraitCam.gameObject);
        if (portrait != null) Destroy(portrait);
        for (int i = 0; i < paints.Count; i++)
        {
            if (paints[i] != null) Destroy(paints[i]);
        }

        if (rig != null) Destroy(rig);
    }

    void NoteClip()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length == 0 || clips[0].clip == null) return;
        string name = clips[0].clip.name;
        if (name == lastClip) return;
        lastClip = name;
        Debug.Log("Robber state " + name);
    }

    static void EnsureAvatar(Animator animator)
    {
        if (animator.avatar != null && animator.avatar.isValid) return;
        Object[] assets = Resources.LoadAll("Heroes/Robber_Rig");
        for (int i = 0; i < assets.Length; i++)
        {
            Avatar avatar = assets[i] as Avatar;
            if (avatar == null || !avatar.isValid) continue;
            animator.avatar = avatar;
            return;
        }

        Debug.LogWarning("Robber avatar missing.");
    }

    void Lit(GameObject root)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer renderer = renderers[r];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material[] source = renderer.sharedMaterials;
            var built = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Material src = source[i];
                Color color = Color.white;
                Texture albedo = null;
                if (src != null)
                {
                    if (src.HasProperty("_BaseColor")) color = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color")) color = src.GetColor("_Color");
                    albedo = Map(src);
                }

                Material live = src != null ? new Material(src) : new Material(lit);
                live.hideFlags = HideFlags.HideAndDontSave;
                if (lit != null && NeedsLit(live))
                {
                    float metallic = live.HasProperty("_Metallic") ? live.GetFloat("_Metallic") : 0f;
                    float smooth = 0.35f;
                    if (live.HasProperty("_Smoothness")) smooth = live.GetFloat("_Smoothness");
                    else if (live.HasProperty("_Glossiness")) smooth = live.GetFloat("_Glossiness");
                    live.shader = lit;
                    if (live.HasProperty("_BaseColor")) live.SetColor("_BaseColor", color);
                    if (live.HasProperty("_Metallic")) live.SetFloat("_Metallic", metallic);
                    if (live.HasProperty("_Smoothness")) live.SetFloat("_Smoothness", smooth);
                }

                if (albedo != null && live.HasProperty("_BaseMap"))
                    live.SetTexture("_BaseMap", albedo);

                built[i] = live;
                paints.Add(live);
                paintColors.Add(color);
            }

            renderer.sharedMaterials = built;
        }
    }

    static Texture Map(Material material)
    {
        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
            return material.GetTexture("_BaseMap");
        if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
            return material.GetTexture("_MainTex");
        if (material.HasProperty("_BaseColorMap") && material.GetTexture("_BaseColorMap") != null)
            return material.GetTexture("_BaseColorMap");
        return null;
    }

    static bool NeedsLit(Material material)
    {
        if (material == null || material.shader == null) return true;
        string name = material.shader.name;
        if (name.Contains("Universal")) return false;
        return true;
    }

    void Measure(GameObject root)
    {
        bool any = false;
        Vector3 min = new Vector3(999f, 999f, 999f);
        Vector3 max = new Vector3(-999f, -999f, -999f);
        SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int s = 0; s < skinned.Length; s++)
        {
            Mesh mesh = skinned[s].sharedMesh;
            if (mesh == null) continue;
            Bounds box = mesh.bounds;
            Vector3 center = box.center;
            Vector3 extents = box.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + new Vector3(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);
                Vector3 local = root.transform.InverseTransformPoint(skinned[s].transform.TransformPoint(corner));
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            any = true;
        }

        if (!any)
        {
            meshHeight = 1.6f;
            focusLocal = new Vector3(0f, 0.8f, 0f);
            return;
        }

        meshHeight = Mathf.Max(0.2f, max.y - min.y);
        focusLocal = (min + max) * 0.5f;
    }

    void MountPortrait()
    {
        portrait = new RenderTexture(380, 500, 16, RenderTextureFormat.ARGB32);
        portrait.name = "RobberPortrait";
        portrait.antiAliasing = 1;
        portrait.Create();

        var camGo = new GameObject("RobberPortraitCam");
        portraitCam = camGo.AddComponent<Camera>();
        portraitCam.orthographic = true;
        portraitCam.orthographicSize = 1f;
        portraitCam.nearClipPlane = 0.05f;
        portraitCam.farClipPlane = 12f;
        portraitCam.clearFlags = CameraClearFlags.SolidColor;
        portraitCam.backgroundColor = new Color(1f, 0f, 1f, 1f);
        portraitCam.cullingMask = 1 << PortraitLayer;
        portraitCam.targetTexture = portrait;
        portraitCam.allowHDR = false;
        portraitCam.allowMSAA = false;
        portraitCam.depth = -20f;

        if (follow == null) return;
        var viewGo = new GameObject("RobberPortrait", typeof(RectTransform));
        portraitView = viewGo.GetComponent<RectTransform>();
        portraitView.SetParent(follow, false);
        portraitView.anchorMin = Vector2.zero;
        portraitView.anchorMax = Vector2.one;
        portraitView.offsetMin = Vector2.zero;
        portraitView.offsetMax = Vector2.zero;
        portraitView.localScale = Vector3.one;
        var raw = viewGo.AddComponent<RawImage>();
        raw.texture = portrait;
        raw.raycastTarget = false;
        raw.color = Color.white;
        Shader keyed = Shader.Find("Juicy/KeyedPortrait");
        if (keyed != null)
        {
            var material = new Material(keyed);
            material.name = "M_RobberPortrait";
            material.hideFlags = HideFlags.HideAndDontSave;
            material.SetColor("_Key", new Color(1f, 0f, 1f, 1f));
            material.SetFloat("_Range", 0.32f);
            raw.material = material;
        }
        else Debug.LogWarning("Robber portrait shader missing.");
    }

    void FramePortrait()
    {
        HideFromMain();
        if (portraitCam == null || rig == null) return;
        if (portraitView != null && follow != null)
        {
            float undo = follow.localScale.x < 0f ? -1f : 1f;
            portraitView.localScale = new Vector3(undo, 1f, 1f);
        }

        float worldH = meshHeight * Mathf.Abs(rig.transform.lossyScale.y);
        Vector3 focus = rig.transform.TransformPoint(focusLocal);
        portraitCam.orthographicSize = Mathf.Max(0.2f, worldH * 0.62f);
        portraitCam.aspect = 380f / 500f;
        portraitCam.transform.position = focus + new Vector3(0f, 0f, -3.2f);
        portraitCam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
    }

    void HideFromMain()
    {
        if (hiddenFromMain) return;
        Camera main = Camera.main;
        if (main == null) return;
        main.cullingMask &= ~(1 << PortraitLayer);
        hiddenFromMain = true;
    }

    static void SetLayer(GameObject root, int layer)
    {
        if (root == null) return;
        root.layer = layer;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            children[i].gameObject.layer = layer;
    }

    void Seat()
    {
        if (rig == null || follow == null) return;
        float worldHeight = HeightFor(follow);
        float fit = worldHeight / Mathf.Max(0.05f, meshHeight);
        float face = follow.localScale.x < 0f ? -1f : 1f;
        Vector3 world = WorldFollow.UiToWorld(follow);
        world.z = -0.35f;
        rig.transform.position = world;
        rig.transform.rotation = Quaternion.Euler(0f, face < 0f ? FacingYaw + 180f : FacingYaw, 0f);
        rig.transform.localScale = new Vector3(fit, fit, fit);
    }

    static float HeightFor(RectTransform rect)
    {
        Camera cam = Camera.main;
        if (cam == null || rect == null) return 1.2f;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float depth = Mathf.Max(0.5f, Mathf.Abs(cam.transform.position.z - (-0.35f)));
        Vector3 bottom = cam.ScreenToWorldPoint(new Vector3(corners[0].x, corners[0].y, depth));
        Vector3 top = cam.ScreenToWorldPoint(new Vector3(corners[1].x, corners[1].y, depth));
        return Mathf.Max(0.2f, Mathf.Abs(top.y - bottom.y));
    }
}
