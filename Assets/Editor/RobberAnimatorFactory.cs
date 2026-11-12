using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds the robber clips and controller in the editor. Runtime cannot create
// an AnimatorController. This does not save the open scene.
[InitializeOnLoad]
public static class RobberAnimatorFactory
{
    const string Version = "rig-2";
    const string Dir = "Assets/Resources/UIHeroes/Robber";

    const string Torso = "Rig/Torso";
    const string Head = "Rig/Torso/Head";
    const string ArmBack = "Rig/Torso/ArmBack";
    const string ArmFront = "Rig/Torso/ArmFront";
    const string Bag = "Rig/Torso/ArmFront/Bag";
    const string LegFront = "Rig/Torso/LegFront";
    const string LegBack = "Rig/Torso/LegBack";

    static bool done;

    static RobberAnimatorFactory()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (done)
        {
            EditorApplication.update -= Tick;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        try
        {
            if (!Build()) return;
            done = true;
            EditorApplication.update -= Tick;
        }
        catch (System.Exception ex)
        {
            done = true;
            EditorApplication.update -= Tick;
            Note("Robber factory failed: " + ex.Message);
        }
    }

    static void Note(string message)
    {
        File.AppendAllText("/tmp/juicy-robber-factory.txt", message + "\n");
        Debug.Log(message);
    }

    static bool Build()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
        {
            Note("Robber factory waiting on the folder");
            return false;
        }

        string versionPath = Dir + "/version.txt";
        TextAsset written = AssetDatabase.LoadAssetAtPath<TextAsset>(versionPath);
        RuntimeAnimatorController ready = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Dir + "/RobberAnimator.controller");
        if (written != null && written.text.Trim() == Version && ready != null) return true;

        AnimationClip idle = MakeClip("Idle_Ledge", true, Idle);
        AnimationClip run = MakeClip("Scurry_Run", true, Run);
        AnimationClip panic = MakeClip("Panic_Look", true, Panic);
        AnimationClip leap = MakeClip("Leap_To_Car", false, Leap);

        string controllerPath = Dir + "/RobberAnimator.controller";
        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Panic", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Leap", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle_Ledge", new Vector3(280f, 0f, 0f));
        AnimatorState runState = machine.AddState("Scurry_Run", new Vector3(280f, 80f, 0f));
        AnimatorState panicState = machine.AddState("Panic_Look", new Vector3(560f, 40f, 0f));
        AnimatorState leapState = machine.AddState("Leap_To_Car", new Vector3(280f, 160f, 0f));
        idleState.motion = idle;
        runState.motion = run;
        panicState.motion = panic;
        leapState.motion = leap;
        machine.defaultState = idleState;
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].defaultWeight = 1f;
        controller.layers = layers;

        AnimatorStateTransition any = machine.AddAnyStateTransition(leapState);
        any.AddCondition(AnimatorConditionMode.If, 0f, "Leap");
        any.hasExitTime = false;
        any.duration = 0.05f;
        any.canTransitionToSelf = false;

        Link(idleState, panicState, IfPanic());
        Link(runState, panicState, IfPanic());
        Link(idleState, runState, Moving(false));
        Link(runState, idleState, Stopped(false));
        Link(panicState, runState, Moving(true));
        Link(panicState, idleState, Stopped(true));

        File.WriteAllText(Path.Combine(Application.dataPath, "Resources/UIHeroes/Robber/version.txt"), Version + "\n");
        AssetDatabase.ImportAsset(versionPath);
        AssetDatabase.SaveAssets();
        Note("Robber animator ready");
        return true;
    }

    static (AnimatorConditionMode mode, float threshold, string param)[] IfPanic()
    {
        return new (AnimatorConditionMode, float, string)[]
        {
            (AnimatorConditionMode.If, 0f, "Panic")
        };
    }

    static (AnimatorConditionMode mode, float threshold, string param)[] Moving(bool leavingPanic)
    {
        if (!leavingPanic)
        {
            return new (AnimatorConditionMode, float, string)[]
            {
                (AnimatorConditionMode.Greater, 20f, "Speed"),
                (AnimatorConditionMode.IfNot, 0f, "Panic")
            };
        }

        return new (AnimatorConditionMode, float, string)[]
        {
            (AnimatorConditionMode.IfNot, 0f, "Panic"),
            (AnimatorConditionMode.Greater, 20f, "Speed")
        };
    }

    static (AnimatorConditionMode mode, float threshold, string param)[] Stopped(bool leavingPanic)
    {
        if (!leavingPanic)
        {
            return new (AnimatorConditionMode, float, string)[]
            {
                (AnimatorConditionMode.Less, 20f, "Speed"),
                (AnimatorConditionMode.IfNot, 0f, "Panic")
            };
        }

        return new (AnimatorConditionMode, float, string)[]
        {
            (AnimatorConditionMode.IfNot, 0f, "Panic"),
            (AnimatorConditionMode.Less, 20f, "Speed")
        };
    }

    static void Link(AnimatorState from, AnimatorState to, (AnimatorConditionMode mode, float threshold, string param)[] conds)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        transition.canTransitionToSelf = false;
        for (int i = 0; i < conds.Length; i++)
            transition.AddCondition(conds[i].mode, conds[i].threshold, conds[i].param);
    }

    static AnimationClip MakeClip(string name, bool loop, System.Action<AnimationClip> fill)
    {
        string path = Dir + "/" + name + ".anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AnimationClip clip = new AnimationClip();
        clip.name = name;
        clip.frameRate = 60f;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        fill(clip);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static void Idle(AnimationClip clip)
    {
        const float duration = 1.4f;
        Curve(clip, Head, Wave(duration, 0f, 8f, 0f));
        Curve(clip, Torso, Wave(duration, 0f, 2f, 0f));
        Curve(clip, Bag, Wave(duration, 0f, 6f, 1.2f));
        Curve(clip, ArmFront, Wave(duration, 0f, 2f, 0f));
        Curve(clip, ArmBack, Wave(duration, 0f, 2f, 3.14f));
        Curve(clip, LegFront, Wave(duration, 0f, 1.5f, 0f));
        Curve(clip, LegBack, Wave(duration, 0f, 1.5f, 3.14f));
    }

    static void Run(AnimationClip clip)
    {
        const float duration = 0.32f;
        Curve(clip, LegFront, Wave(duration, 0f, 22f, 0f));
        Curve(clip, LegBack, Wave(duration, 0f, 22f, 3.14f));
        Curve(clip, ArmFront, Wave(duration, 0f, 18f, 3.14f));
        Curve(clip, ArmBack, Wave(duration, 0f, 20f, 0f));
        Curve(clip, Torso, Wave(duration, 8f, 2f, 0f));
        Curve(clip, Bag, Wave(duration, 0f, 12f, 0.6f));
        Curve(clip, Head, Wave(duration, 0f, 5f, 1.4f));
    }

    static void Panic(AnimationClip clip)
    {
        const float duration = 0.18f;
        Curve(clip, LegFront, Wave(duration, 0f, 26f, 0f));
        Curve(clip, LegBack, Wave(duration, 0f, 26f, 3.14f));
        Curve(clip, ArmFront, Wave(duration, 0f, 28f, 3.14f));
        Curve(clip, ArmBack, Wave(duration, 0f, 28f, 0f));
        Curve(clip, Head, Wave(duration, 0f, 14f, 0.4f));
        Curve(clip, Bag, Wave(duration, 0f, 24f, 1f));
        Curve(clip, Torso, Wave(duration, 0f, 6f, 0f));
    }

    static void Leap(AnimationClip clip)
    {
        const float duration = 0.45f;
        Curve(clip, Torso, Ease(duration, -12f));
        Curve(clip, Head, Ease(duration, 8f));
        Curve(clip, ArmFront, Ease(duration, -50f));
        Curve(clip, ArmBack, Ease(duration, 48f));
        Curve(clip, LegFront, Ease(duration, -22f));
        Curve(clip, LegBack, Ease(duration, 26f));
        Curve(clip, Bag, Ease(duration, -16f));
    }

    static void Curve(AnimationClip clip, string path, AnimationCurve curve)
    {
        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", curve);
    }

    static AnimationCurve Wave(float duration, float center, float amp, float phase)
    {
        const int steps = 8;
        Keyframe[] keys = new Keyframe[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = duration * i / steps;
            float angle = center + amp * Mathf.Sin((Mathf.PI * 2f * i / steps) + phase);
            keys[i] = new Keyframe(t, angle);
        }

        AnimationCurve curve = new AnimationCurve(keys);
        for (int i = 0; i <= steps; i++)
            curve.SmoothTangents(i, 1f);

        Keyframe first = curve.keys[0];
        Keyframe last = curve.keys[steps];
        last.value = first.value;
        last.inTangent = first.inTangent;
        last.outTangent = first.outTangent;
        curve.MoveKey(steps, last);
        return curve;
    }

    static AnimationCurve Ease(float duration, float to)
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(duration * 0.55f, to, 0f, 0f),
            new Keyframe(duration, to, 0f, 0f));
    }
}
