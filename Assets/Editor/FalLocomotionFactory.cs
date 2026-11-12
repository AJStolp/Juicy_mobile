using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds FalLocomotion and, once a rigged FBX arrives, maps Idle, Walk, and Run onto Speed 0, 1, 2.
[InitializeOnLoad]
public static class FalLocomotionFactory
{
    const string Version = "fal-loco-1";
    const string ControllerPath = "Assets/Art/FalClean/FalLocomotion.controller";
    const string VersionPath = "Assets/Art/FalClean/fal-locomotion-version.txt";
    const string ClipFolder = "Assets/Art/FalClean";

    static readonly string[] PreferredSources =
    {
        "Assets/Art/FalClean/SM_Robber_Fal.fbx",
        "Assets/Art/FalClean/SM_Hiker_Fal.fbx",
    };

    static bool done;
    static bool syncing;
    static bool scheduled;

    static FalLocomotionFactory()
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
            Debug.LogError("Fal locomotion factory failed: " + ex.Message);
        }
    }

    public static void ScheduleSync()
    {
        if (scheduled) return;
        scheduled = true;
        EditorApplication.delayCall += () =>
        {
            scheduled = false;
            SyncImportedClips();
        };
    }

    public static void SyncImportedClips()
    {
        if (syncing) return;
        syncing = true;
        try
        {
            EnsureController();
            List<string> models = ListModels();
            for (int i = 0; i < models.Count; i++)
                ConfigureModel(models[i]);
            AssignClips(models);
        }
        finally
        {
            syncing = false;
        }
    }

    static bool Build()
    {
        SyncImportedClips();
        return AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null;
    }

    static void EnsureController()
    {
        TextAsset written = AssetDatabase.LoadAssetAtPath<TextAsset>(VersionPath);
        AnimatorController ready = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ready != null && TreeReady(ready))
        {
            if (written == null || written.text.Trim() != Version)
                WriteVersion();
            return;
        }

        if (ready != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        BlendTree tree = new BlendTree
        {
            name = "Locomotion",
            blendParameter = "Speed",
            blendType = BlendTreeType.Simple1D,
            useAutomaticThresholds = false,
            minThreshold = 0f,
            maxThreshold = 2f
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(null, 0f);
        tree.AddChild(null, 1f);
        tree.AddChild(null, 2f);
        tree.minThreshold = 0f;
        tree.maxThreshold = 2f;
        tree.useAutomaticThresholds = false;

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.AddState("Locomotion", new Vector3(300f, 0f, 0f));
        state.motion = tree;
        state.iKOnFeet = true;
        machine.defaultState = state;

        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
        WriteVersion();
        AssetDatabase.SaveAssets();
        Debug.Log("Fal locomotion controller ready. Speed 0 Idle, 1 Walk, 2 Run.");
    }

    static void WriteVersion()
    {
        File.WriteAllText(Path.Combine(Application.dataPath, "Art/FalClean/fal-locomotion-version.txt"), Version + "\n");
        AssetDatabase.ImportAsset(VersionPath);
    }

    static bool TreeReady(AnimatorController controller)
    {
        if (controller.layers == null || controller.layers.Length == 0) return false;
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        if (machine.defaultState == null || machine.defaultState.motion is not BlendTree tree)
            return false;
        if (tree.blendParameter != "Speed" || tree.useAutomaticThresholds)
            return false;
        ChildMotion[] children = tree.children;
        return children != null
            && children.Length == 3
            && Mathf.Approximately(children[0].threshold, 0f)
            && Mathf.Approximately(children[1].threshold, 1f)
            && Mathf.Approximately(children[2].threshold, 2f);
    }

    static List<string> ListModels()
    {
        List<string> models = new List<string>();
        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < PreferredSources.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PreferredSources[i]) == null) continue;
            models.Add(PreferredSources[i]);
            seen.Add(PreferredSources[i]);
        }

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ClipFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!seen.Add(path)) continue;
            models.Add(path);
        }

        return models;
    }

    static void ConfigureModel(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (importer == null || root == null) return;

        bool hips = HasHips(root);
        if (hips && importer.animationType != ModelImporterAnimationType.Human && !HumanoidAlreadyFailed(importer, path))
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.importAnimation = true;
            importer.SaveAndReimport();
            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Avatar avatar = FindAvatar(path);
            if (avatar == null || !avatar.isValid)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.userData = FailMark(path);
                importer.SaveAndReimport();
                Debug.LogError("Fal Humanoid mapping failed for " + path + ". The bind needs a T pose with Hips and separated limbs. Importer stays Generic.");
                return;
            }

            if (!string.IsNullOrEmpty(importer.userData) && importer.userData.StartsWith("fal-humanoid-failed:"))
            {
                importer.userData = "";
                importer.SaveAndReimport();
            }
        }

        if (!PrepareClips(path)) return;
    }

    static bool HumanoidAlreadyFailed(ModelImporter importer, string path)
    {
        return importer.userData == FailMark(path);
    }

    static string FailMark(string path)
    {
        string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
        long length = File.Exists(full) ? new FileInfo(full).Length : 0;
        return "fal-humanoid-failed:" + length;
    }

    static bool HasHips(GameObject root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            string name = transforms[i].name;
            if (name == "Hips" || name.EndsWith(":Hips") || name.EndsWith("_Hips"))
                return true;
        }

        return false;
    }

    static Avatar FindAvatar(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            Avatar avatar = assets[i] as Avatar;
            if (avatar != null) return avatar;
        }

        return null;
    }

    // Idle stays planted. Walk and Run keep the authored XZ travel as root motion.
    static bool PrepareClips(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return false;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return false;

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            string slot = SlotFor(clips[i].name, path);
            if (slot == null) slot = SlotFor(clips[i].takeName, path);
            if (slot == null) continue;

            bool idle = slot == "Idle";
            bool rootReady = clips[i].loopTime
                && clips[i].lockRootRotation
                && clips[i].lockRootHeightY
                && clips[i].lockRootPositionXZ == idle
                && clips[i].keepOriginalPositionY
                && clips[i].keepOriginalPositionXZ
                && clips[i].name == slot;
            if (rootReady) continue;

            clips[i].name = slot;
            clips[i].loopTime = true;
            clips[i].lockRootRotation = true;
            clips[i].lockRootHeightY = true;
            clips[i].lockRootPositionXZ = idle;
            clips[i].keepOriginalPositionY = true;
            clips[i].keepOriginalPositionXZ = true;
            changed = true;
        }

        if (!changed) return true;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return true;
    }

    static string SlotFor(string clipName, string path)
    {
        if (string.IsNullOrEmpty(clipName)) return null;
        string leaf = clipName;
        int bar = clipName.LastIndexOf('|');
        if (bar >= 0 && bar + 1 < clipName.Length)
            leaf = clipName.Substring(bar + 1);

        string slot = Slot(leaf);
        if (slot != null) return slot;
        if (leaf == "mixamo.com" || leaf == "mixamo_com")
            return Slot(Path.GetFileNameWithoutExtension(path));
        return null;
    }

    static string Slot(string name)
    {
        if (name == "Idle") return "Idle";
        if (name == "Walk" || name == "Walking") return "Walk";
        if (name == "Run" || name == "Running") return "Run";
        return null;
    }

    static void AssignClips(List<string> models)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || !TreeReady(controller)) return;
        BlendTree tree = (BlendTree)controller.layers[0].stateMachine.defaultState.motion;
        ChildMotion[] children = tree.children;

        AnimationClip idle = children[0].motion as AnimationClip;
        AnimationClip walk = children[1].motion as AnimationClip;
        AnimationClip run = children[2].motion as AnimationClip;

        for (int i = 0; i < models.Count; i++)
        {
            if (idle == null) idle = Take(models[i], "Idle");
            if (walk == null) walk = Take(models[i], "Walk");
            if (run == null) run = Take(models[i], "Run");
        }

        bool same = children[0].motion == idle && children[1].motion == walk && children[2].motion == run;
        if (same) return;
        if (idle == null && walk == null && run == null) return;

        children[0].motion = idle;
        children[0].threshold = 0f;
        children[1].motion = walk;
        children[1].threshold = 1f;
        children[2].motion = run;
        children[2].threshold = 2f;
        tree.children = children;
        tree.useAutomaticThresholds = false;
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Fal locomotion clips linked. Idle=" + NameOf(idle) + " Walk=" + NameOf(walk) + " Run=" + NameOf(run) + ".");
    }

    static AnimationClip Take(string path, string slot)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__")) continue;
            if (clip.name != slot && !clip.name.EndsWith("|" + slot)) continue;
            return clip;
        }

        return null;
    }

    static string NameOf(AnimationClip clip)
    {
        return clip == null ? "empty" : clip.name;
    }
}

public class FalLocomotionImport : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i];
            if (!path.StartsWith("Assets/Art/FalClean/") || !path.EndsWith(".fbx")) continue;
            FalLocomotionFactory.ScheduleSync();
            return;
        }
    }
}
