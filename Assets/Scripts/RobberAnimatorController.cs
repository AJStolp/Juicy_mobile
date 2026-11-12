using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Sliced bone rig of the locked robber drawing. Overlay images do not take
// URP lights, so heat tints the parts instead.
public class RobberAnimatorController : MonoBehaviour
{
    const float SourceW = 690f;
    const float SourceH = 863f;
    const float DrawW = 190f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int PanicHash = Animator.StringToHash("Panic");
    static readonly int LeapHash = Animator.StringToHash("Leap");

    readonly List<Image> parts = new List<Image>();
    Animator animator;
    RectTransform rig;
    string lastClip = "";
    bool ready;

    public bool Ready => ready;

    public bool Build(RectTransform root)
    {
        float scale = DrawW / SourceW;
        rig = MakeRect("Rig", root, Vector2.zero, new Vector2(DrawW, SourceH * scale), new Vector2(0.5f, 0.5f));

        const float torsoX = 286f;
        const float torsoY = 430f;
        RectTransform torso = MakeRect("Torso", rig, ToRig(torsoX, torsoY, scale), Vector2.zero, new Vector2(0.5f, 0.5f));

        if (!Limb("ArmBack", torso, torsoX, torsoY, "armBack", 178f, 412f, scale)) return Fail();
        if (!Limb("LegBack", torso, torsoX, torsoY, "legBack", 348f, 528f, scale)) return Fail();
        if (!Plate(torso, scale)) return Fail();
        if (!Limb("LegFront", torso, torsoX, torsoY, "legFront", 276f, 534f, scale)) return Fail();
        if (!Limb("Head", torso, torsoX, torsoY, "head", 226f, 312f, scale)) return Fail();
        if (!Limb("ArmFront", torso, torsoX, torsoY, "armFront", 338f, 368f, scale)) return Fail();

        RectTransform arm = torso.Find("ArmFront") as RectTransform;
        if (arm == null || !Limb("Bag", arm, 338f, 368f, "bag", 368f, 230f, scale)) return Fail();

        animator = root.gameObject.AddComponent<Animator>();
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("UIHeroes/Robber/RobberAnimator");
        if (animator.runtimeAnimatorController == null)
            Debug.LogWarning("Robber animator missing. Rest pose only.");
        else
            animator.SetLayerWeight(0, 1f);

        ready = true;
        Debug.Log("Robber rig built");
        return true;
    }

    public void SetLocomotion(float speed, bool panic)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetFloat(SpeedHash, speed);
        animator.SetBool(PanicHash, panic);
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
        Color tint = Color.Lerp(Color.white, new Color(1f, 0.62f, 0.48f), shaped * 0.35f);
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] != null) parts[i].color = tint;
        }
    }

    public void Hide()
    {
        if (rig != null) rig.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!ready || animator == null || animator.runtimeAnimatorController == null) return;
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length == 0 || clips[0].clip == null) return;
        string name = clips[0].clip.name;
        if (name == lastClip) return;
        lastClip = name;
        Debug.Log("Robber state " + name);
    }

    bool Fail()
    {
        if (rig != null) Destroy(rig.gameObject);
        ready = false;
        Debug.LogWarning("Robber rig fell back to the flat sprite.");
        return false;
    }

    bool Plate(RectTransform torso, float scale)
    {
        return Limb("TorsoSprite", torso, 0f, 0f, "torso", 0f, 0f, scale, true);
    }

    bool Limb(string bone, RectTransform parent, float parentX, float parentY, string spriteName, float jointX, float jointY, float scale, bool planted = false)
    {
        Sprite sprite = Resources.Load<Sprite>("UIHeroes/Robber/" + spriteName);
        if (sprite == null)
        {
            Debug.LogWarning("Robber part missing: " + spriteName);
            return false;
        }

        Vector2 pivot = new Vector2(
            sprite.pivot.x / Mathf.Max(1f, sprite.rect.width),
            sprite.pivot.y / Mathf.Max(1f, sprite.rect.height));
        Vector2 place = planted ? Vector2.zero : Delta(jointX, jointY, parentX, parentY, scale);
        RectTransform rect = MakeRect(bone, parent, place, sprite.rect.size * scale, pivot);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = false;
        parts.Add(image);
        return true;
    }

    static Vector2 ToRig(float jointX, float jointY, float scale)
    {
        return new Vector2((jointX - SourceW * 0.5f) * scale, (SourceH * 0.5f - jointY) * scale);
    }

    static Vector2 Delta(float jointX, float jointY, float parentX, float parentY, float scale)
    {
        return new Vector2((jointX - parentX) * scale, (parentY - jointY) * scale);
    }

    static RectTransform MakeRect(string name, RectTransform parent, Vector2 anchored, Vector2 size, Vector2 pivot)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchored;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        return rect;
    }
}
