using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class VaultHeistHookup
{
    const string HookFlag = "/tmp/juicy-hook-vault";
    const string SeatFlag = "/tmp/juicy-seat-vault";
    const string ShowFlag = "/tmp/juicy-show-vault";
    const string HookLog = "/tmp/juicy-vault-hook.txt";
    const string PreviewLog = "/tmp/juicy-vault-preview.txt";
    const string ShotPath = "/tmp/juicy-vault-game.png";

    static bool enterQueued;
    static bool ticking;
    static int waits;

    static VaultHeistHookup()
    {
        EditorApplication.update += Watch;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("Juicy/Preview Vault")]
    public static void Preview()
    {
        File.WriteAllText(ShowFlag, "1");
        enterQueued = false;
        EditorApplication.delayCall += EnterFromMenu;
    }

    static void EnterFromMenu()
    {
        File.WriteAllText("/tmp/juicy-vault-enter.txt",
            "playing=" + EditorApplication.isPlaying
            + " compiling=" + EditorApplication.isCompiling + "\n");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnterFromMenu;
            return;
        }

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.AppendAllText("/tmp/juicy-vault-enter.txt", "delay enter\n");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Juicy/Hook Vault Heist")]
    public static void Hook()
    {
        try
        {
            if (EditorApplication.isPlaying)
            {
                File.WriteAllText(HookLog, "skipped, editor is playing\n");
                return;
            }

            Sprite closedDoor = LoadSprite("Assets/Resources/UIHeroes/vault_door_closed.png");
            Sprite openDoor = LoadSprite("Assets/Art/UIHeroes/vault_door_open.png");
            Sprite wheel1 = LoadSprite("Assets/Art/UIHeroes/vault_wheel_01.png");
            Sprite wheel2 = LoadSprite("Assets/Art/UIHeroes/vault_wheel_02.png");
            Sprite wheel3 = LoadSprite("Assets/Art/UIHeroes/vault_wheel_03.png");
            Sprite wheel4 = LoadSprite("Assets/Art/UIHeroes/vault_wheel_04.png");
            Sprite ruby = LoadSprite("Assets/Resources/UIHeroes/gem_ruby.png");
            Sprite topaz = LoadSprite("Assets/Resources/UIHeroes/gem_topaz.png");
            if (closedDoor == null || openDoor == null || wheel1 == null || ruby == null || topaz == null)
            {
                File.WriteAllText(HookLog, "missing sprite\n");
                return;
            }

            Directory.CreateDirectory("Assets/Resources/Vault");
            RuntimeAnimatorController animatorController = BuildController(closedDoor, wheel1, wheel2, wheel3, wheel4, openDoor);
            GameObject rubyPrefab = BuildGemPrefab("RubyGemDrop", ruby);
            GameObject topazPrefab = BuildGemPrefab("TopazGemDrop", topaz);
            Material particleMaterial = BuildParticleMaterial();

            GameObject managerObject = GameObject.Find("VaultHeistManager");
            if (managerObject == null) managerObject = new GameObject("VaultHeistManager");
            VaultHeistController manager = managerObject.GetComponent<VaultHeistController>();
            if (manager == null) manager = managerObject.AddComponent<VaultHeistController>();

            GameObject door = NewRect("VaultDoor2D");
            RectTransform doorRect = door.GetComponent<RectTransform>();
            doorRect.anchorMin = new Vector2(0.5f, 0.5f);
            doorRect.anchorMax = new Vector2(0.5f, 0.5f);
            doorRect.pivot = new Vector2(0.5f, 0.5f);
            doorRect.sizeDelta = new Vector2(420f, 504f);
            doorRect.localScale = new Vector3(VaultHeistController.DoorScale, VaultHeistController.DoorScale, VaultHeistController.DoorScale);
            doorRect.anchoredPosition = new Vector2(0f, VaultHeistController.DoorAnchoredY);

            Canvas canvas = FindCanvas();
            if (canvas != null) door.transform.SetParent(canvas.transform, false);

            Image glow = CreateImage("VaultGlow", door.transform, Knob(), new Vector2(560f, 640f), new Color(1f, 0.8f, 0.32f, 0f));
            glow.rectTransform.anchoredPosition = Vector2.zero;
            Image face = CreateImage("VaultFace", door.transform, closedDoor, new Vector2(420f, 504f), Color.white);
            face.preserveAspect = true;
            face.rectTransform.anchoredPosition = Vector2.zero;

            Animator animator = face.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GameObject nodes = GameObject.Find("Nodes");
            Transform laneParent = nodes != null ? nodes.transform : (canvas != null ? canvas.transform : managerObject.transform);
            GameObject spawn = NewRect("GemDropSpawn");
            RectTransform spawnRect = spawn.GetComponent<RectTransform>();
            spawn.transform.SetParent(laneParent, false);
            spawnRect.anchorMin = new Vector2(0.5f, 0.5f);
            spawnRect.anchorMax = new Vector2(0.5f, 0.5f);
            spawnRect.anchoredPosition = new Vector2(0f, 460f);
            spawnRect.sizeDelta = Vector2.zero;

            GameObject lightObject = Rebuild("VaultGoldenLight");
            lightObject.transform.SetParent(door.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, -0.4f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.35f);
            light.intensity = 2f;
            light.range = 2.2f;
            light.shadows = LightShadows.None;

            GameObject particleObject = Rebuild("VaultGlowParticles");
            particleObject.transform.SetParent(door.transform, false);
            particleObject.transform.localPosition = Vector3.zero;
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            ConfigureParticles(particles, particleMaterial);

            manager.vaultDoorAnimator = animator;
            manager.rubyPrefab = rubyPrefab;
            manager.topazPrefab = topazPrefab;
            manager.dropLaneSpawnPoint = spawnRect;
            manager.vaultGlowParticles = particles;
            manager.vaultGoldenLight = light;
            EditorUtility.SetDirty(manager);

            if (canvas != null) door.transform.SetSiblingIndex(Mathf.Min(2, canvas.transform.childCount - 1));

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            File.WriteAllText(HookLog, "hooked IsOpen ruby topaz\n");
            Debug.Log("Vault heist hooked. IsOpen drives the wheel and the open door.");
        }
        catch (Exception error)
        {
            File.WriteAllText(HookLog, error.ToString());
            Debug.LogError(error);
        }
    }

    [MenuItem("Juicy/Seat Vault Layout")]
    public static void SeatLayout()
    {
        try
        {
            if (EditorApplication.isPlaying)
            {
                File.WriteAllText("/tmp/juicy-seat-vault.txt", "skipped, editor is playing\n");
                return;
            }

            Sprite crimson = FindChest("deposit_chest_crimson");
            Sprite indigo = FindChest("deposit_chest_indigo");
            AssignChest("LeftBin", crimson);
            AssignChest("RightBin", indigo);

            GameObject door = GameObject.Find("VaultDoor2D");
            if (door != null)
            {
                RectTransform rect = door.GetComponent<RectTransform>();
                rect.localScale = new Vector3(VaultHeistController.DoorScale, VaultHeistController.DoorScale, VaultHeistController.DoorScale);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, VaultHeistController.DoorAnchoredY);
                EditorUtility.SetDirty(door);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            File.WriteAllText("/tmp/juicy-seat-vault.txt",
                "seated scale " + VaultHeistController.DoorScale
                + " y " + VaultHeistController.DoorAnchoredY
                + " crimson " + (crimson != null)
                + " indigo " + (indigo != null) + "\n");
        }
        catch (Exception error)
        {
            File.WriteAllText("/tmp/juicy-seat-vault.txt", error.ToString());
            Debug.LogError(error);
        }
    }

    static Sprite FindChest(string spriteName)
    {
        Sprite direct = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UIHeroes/" + spriteName + ".png");
        if (direct != null) return direct;
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/UIHeroes/deposit_chests.png");
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite != null && sprite.name == spriteName) return sprite;
        }
        return null;
    }

    static void AssignChest(string objectName, Sprite sprite)
    {
        if (sprite == null) return;
        GameObject bin = GameObject.Find(objectName);
        if (bin == null) return;
        Image image = bin.GetComponent<Image>();
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        EditorUtility.SetDirty(image);
    }

    static void Watch()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        if (File.Exists(SeatFlag))
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (FindChest("deposit_chest_crimson") == null || FindChest("deposit_chest_indigo") == null) return;
            File.Delete(SeatFlag);
            SeatLayout();
            return;
        }

        if (File.Exists(HookFlag))
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }

            File.Delete(HookFlag);
            Hook();
            string hooked = File.Exists(HookLog) ? File.ReadAllText(HookLog) : "";
            if (hooked.StartsWith("hooked")) File.WriteAllText(ShowFlag, "1");
            return;
        }

        if (!File.Exists(ShowFlag)) return;
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (enterQueued) return;
        enterQueued = true;
        EditorApplication.delayCall += EnterWhenReady;
    }

    static void EnterWhenReady()
    {
        enterQueued = false;
        if (!File.Exists(ShowFlag)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            enterQueued = true;
            EditorApplication.delayCall += EnterWhenReady;
            return;
        }

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.AppendAllText("/tmp/juicy-vault-enter.txt", "entering play\n");
        FocusGameView();
        SetPhoneGameView();
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        enterQueued = false;
    }

    static void BeginTick()
    {
        if (ticking) return;
        ticking = true;
        enterQueued = false;
        waits = 0;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            ticking = false;
            EditorApplication.update -= Tick;
            return;
        }

        waits++;
        VaultHeistController vault = UnityEngine.Object.FindFirstObjectByType<VaultHeistController>();
        if (waits == 18)
        {
            FocusGameView();
            SetPhoneGameView();
            if (vault != null)
            {
                vault.SetVaultState(true);
                vault.SpawnGemPrefab(true);
            }
            return;
        }

        if (waits < 42) return;
        ticking = false;
        EditorApplication.update -= Tick;
        string report = vault == null ? "manager missing" : "open " + vault.IsOpen;
        File.WriteAllText(PreviewLog, report + "\n");
        ScreenCapture.CaptureScreenshot(ShotPath);
    }

    static RuntimeAnimatorController BuildController(Sprite closedDoor, Sprite wheel1, Sprite wheel2, Sprite wheel3, Sprite wheel4, Sprite openDoor)
    {
        const string path = "Assets/Resources/Vault/VaultDoor.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("IsOpen", AnimatorControllerParameterType.Bool);

        AnimationClip closed = MakeClip("VaultDoor_Closed", new[] { closedDoor }, 0.1f);
        AnimationClip open = MakeClip("VaultDoor_Open", new[] { closedDoor, wheel1, wheel2, wheel3, wheel4, openDoor }, 0.64f);
        AssetDatabase.AddObjectToAsset(closed, controller);
        AssetDatabase.AddObjectToAsset(open, controller);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        ChildAnimatorState[] existing = machine.states;
        for (int i = 0; i < existing.Length; i++)
            machine.RemoveState(existing[i].state);

        AnimatorState closedState = machine.AddState("Closed");
        closedState.motion = closed;
        AnimatorState openState = machine.AddState("Open");
        openState.motion = open;
        machine.defaultState = closedState;

        AnimatorStateTransition toOpen = closedState.AddTransition(openState);
        toOpen.AddCondition(AnimatorConditionMode.If, 0f, "IsOpen");
        toOpen.hasExitTime = false;
        toOpen.duration = 0f;
        toOpen.hasFixedDuration = true;

        AnimatorStateTransition toClosed = openState.AddTransition(closedState);
        toClosed.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsOpen");
        toClosed.hasExitTime = false;
        toClosed.duration = 0.05f;
        toClosed.hasFixedDuration = true;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimationClip MakeClip(string clipName, Sprite[] frames, float length)
    {
        AnimationClip clip = new AnimationClip { name = clipName, frameRate = 12f, legacy = false };
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite");
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
        float step = frames.Length <= 1 ? 0f : length / (frames.Length - 1);
        for (int i = 0; i < frames.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = step * i, value = frames[i] };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.stopTime = Mathf.Max(length, 1f / 12f);
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    static GameObject BuildGemPrefab(string prefabName, Sprite sprite)
    {
        string path = "Assets/Resources/Vault/" + prefabName + ".prefab";
        GameObject temp = new GameObject(prefabName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = temp.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(136f, 136f);
        Image image = temp.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        UnityEngine.Object.DestroyImmediate(temp);
        return prefab;
    }

    static Material BuildParticleMaterial()
    {
        const string path = "Assets/Materials/VaultGlowParticle.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) return null;
        Material material = new Material(shader) { name = "VaultGlowParticle" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void ConfigureParticles(ParticleSystem particles, Material material)
    {
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = 0.55f;
        main.startSpeed = 0.12f;
        main.startSize = 0.06f;
        main.startColor = new Color(1f, 0.84f, 0.35f, 0.9f);
        main.maxParticles = 32;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 18f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.45f;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (material != null) renderer.sharedMaterial = material;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        return image;
    }

    static GameObject Rebuild(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        return new GameObject(name);
    }

    static GameObject NewRect(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        return new GameObject(name, typeof(RectTransform));
    }

    static Sprite Knob()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite found) return found;
        }

        return null;
    }

    static Canvas FindCanvas()
    {
        GameObject ticker = GameObject.Find("CoinTickerText");
        if (ticker == null) return null;
        return ticker.GetComponentInParent<Canvas>();
    }

    static void FocusGameView()
    {
        Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (gameView == null) return;
        EditorWindow.GetWindow(gameView).Focus();
    }

    static void SetPhoneGameView()
    {
        try
        {
            Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            bool applied = false;
            Type cursor = gameViewType;
            while (cursor != null && cursor != typeof(object))
            {
                foreach (MethodInfo method in cursor.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.Name != "SetCustomResolution" || applied) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length < 2 || parameters[0].ParameterType != typeof(Vector2) || parameters[1].ParameterType != typeof(string))
                        continue;
                    object[] args = new object[parameters.Length];
                    args[0] = new Vector2(1080f, 1920f);
                    args[1] = "Phone 1080x1920";
                    method.Invoke(method.IsStatic ? null : gameView, args);
                    applied = true;
                }

                cursor = cursor.BaseType;
            }

            gameView.Repaint();
        }
        catch (Exception error)
        {
            File.AppendAllText(PreviewLog, error + "\n");
        }
    }
}
