using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds Robber3DAnimator from the clips inside Robber_Rig.fbx.
[InitializeOnLoad]
public static class Robber3DFactory
{
    const string Version = "rig-3d-2";
    const string Fbx = "Assets/Resources/Heroes/Robber_Rig.fbx";
    const string ControllerPath = "Assets/Resources/Heroes/Robber3DAnimator.controller";
    const string VersionPath = "Assets/Resources/Heroes/robber3d-version.txt";

    static bool done;

    static Robber3DFactory()
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
            Note("Robber 3D factory failed: " + ex.Message);
        }
    }

    static void Note(string message)
    {
        File.AppendAllText("/tmp/juicy-robber-3d.txt", message + "\n");
        Debug.Log(message);
    }

    static bool Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Fbx) == null) return false;

        TextAsset written = AssetDatabase.LoadAssetAtPath<TextAsset>(VersionPath);
        RuntimeAnimatorController ready = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (written != null && written.text.Trim() == Version && ready != null) return true;

        LoopClips();
        AnimationClip idle = Clip("Idle_Ledge");
        AnimationClip run = Clip("Scurry_Run");
        AnimationClip panic = Clip("Panic_Look");
        AnimationClip leap = Clip("Leap_To_Car");
        if (idle == null || run == null || panic == null || leap == null)
        {
            Note("Robber 3D factory waiting on clips");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
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

        AnimatorStateTransition any = machine.AddAnyStateTransition(leapState);
        any.AddCondition(AnimatorConditionMode.If, 0f, "Leap");
        any.hasExitTime = false;
        any.duration = 0.05f;
        any.canTransitionToSelf = false;

        Link(idleState, panicState, new Cond(AnimatorConditionMode.If, 0f, "Panic"));
        Link(runState, panicState, new Cond(AnimatorConditionMode.If, 0f, "Panic"));
        Link(idleState, runState,
            new Cond(AnimatorConditionMode.Greater, 20f, "Speed"),
            new Cond(AnimatorConditionMode.IfNot, 0f, "Panic"));
        Link(runState, idleState,
            new Cond(AnimatorConditionMode.Less, 20f, "Speed"),
            new Cond(AnimatorConditionMode.IfNot, 0f, "Panic"));
        Link(panicState, runState,
            new Cond(AnimatorConditionMode.IfNot, 0f, "Panic"),
            new Cond(AnimatorConditionMode.Greater, 20f, "Speed"));
        Link(panicState, idleState,
            new Cond(AnimatorConditionMode.IfNot, 0f, "Panic"),
            new Cond(AnimatorConditionMode.Less, 20f, "Speed"));

        File.WriteAllText(Path.Combine(Application.dataPath, "Resources/Heroes/robber3d-version.txt"), Version + "\n");
        AssetDatabase.ImportAsset(VersionPath);
        AssetDatabase.SaveAssets();
        Note("Robber 3D animator ready");
        return true;
    }

    static void LoopClips()
    {
        ModelImporter importer = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (importer == null) return;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;
        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            bool leap = clips[i].name.Contains("Leap");
            if (clips[i].loopTime == !leap) continue;
            clips[i].loopTime = !leap;
            changed = true;
        }

        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static AnimationClip Clip(string name)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(Fbx);
        AnimationClip fallback = null;
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__")) continue;
            if (clip.name == name || clip.name.EndsWith("|" + name)) return clip;
            if (clip.name.Contains(name)) fallback = clip;
        }

        return fallback;
    }

    struct Cond
    {
        public AnimatorConditionMode mode;
        public float threshold;
        public string param;

        public Cond(AnimatorConditionMode mode, float threshold, string param)
        {
            this.mode = mode;
            this.threshold = threshold;
            this.param = param;
        }
    }

    static void Link(AnimatorState from, AnimatorState to, params Cond[] conds)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        transition.canTransitionToSelf = false;
        for (int i = 0; i < conds.Length; i++)
            transition.AddCondition(conds[i].mode, conds[i].threshold, conds[i].param);
    }
}
