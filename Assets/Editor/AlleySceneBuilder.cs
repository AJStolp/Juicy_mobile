using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using TMPro;

// Builds Assets/Scenes/DiegeticAlley.unity from the textured alley models.
// SampleScene is not opened and not saved.
public static class AlleySceneBuilder
{
    const string ScenePath = "Assets/Scenes/DiegeticAlley.unity";
    const string Art = "Assets/Art/Alley/";
    const string Raccoon = Art + "Raccoon/";
    const string IdlePath = Raccoon + "Raccoon_Torch_Idle.fbx";
    const string ControllerPath = Raccoon + "Raccoon_Alley.controller";
    const string TextureRoot = Art + "Textures/";
    const string MaterialRoot = Art + "Materials";
    const float ColumnZ = 0.15f;
    const float DropY = 3.15f;
    static readonly float[] ColumnX = { -1.05f, -0.35f, 0.35f, 1.05f };
    static readonly Color WarmAmber = new Color(1f, 184f / 255f, 77f / 255f);
    static readonly Color FillBlue = new Color(176f / 255f, 196f / 255f, 222f / 255f);
    static readonly Color NavyClear = new Color(26f / 255f, 28f / 255f, 35f / 255f);
    static readonly Color AmbientGray = new Color(0.5f, 0.5f, 0.5f);
    const float KeyIntensity = 1.8f;

    [MenuItem("Juicy/Alley/Build Diegetic Scene")]
    public static void Build()
    {
        ConfigureRaccoonImport();
        AnimatorController controller = BuildController();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "DiegeticAlley";
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientGray;
        RenderSettings.ambientIntensity = 1.2f;

        GameObject environment = Group("01_Environment_Static");
        GameObject ui = Group("02_Diegetic_UI_Elements");
        GameObject grid = Group("03_Gameplay_Grid_System");
        GameObject actors = Group("04_Actors_Dynamic");

        TuneMoon();
        EnsureFillLight();
        SetupCamera(environment.transform);

        GameObject framing = Empty("Alley_Framing", environment.transform, Vector3.zero);

        // Positions come from LayoutAlley below.
        PlaceUniform(Art + "SM_CountdownTimer.fbx", "Vault_Score_Counter", ui.transform, Vector3.zero, 0.42f, 180f);
        PlaceWide(Art + "SM_PoliceLightBar.fbx", "Police_Lightbar_Alarm", ui.transform, Vector3.zero, 1.1f, 180f);
        GameObject spots = Empty("Wall_Spotlights", ui.transform, Vector3.zero);
        Place(Art + "SM_PoliceSpotlight.fbx", "Spotlight_Left", spots.transform, new Vector3(-3.3f, 2.6f, 3.4f), new Vector3(0.55f, 0.45f, 0.4f), 25f);
        Place(Art + "SM_PoliceSpotlight.fbx", "Spotlight_Right", spots.transform, new Vector3(3.3f, 2.6f, 3.4f), new Vector3(0.55f, 0.45f, 0.4f), -25f);

        BuildDrops(grid.transform);

        GameObject raccoon = PlaceUniform(IdlePath, "Raccoon_Mascot", actors.transform, Vector3.zero, 1.05f, 90f);
        DressRaccoon(raccoon, controller);

        Place(Art + "SM_WoodenCargoCrates.fbx", "Heavy_Crates", framing.transform, new Vector3(1.35f, 0f, 3.1f), new Vector3(0.7f, 0.62f, 0.7f), 8f);
        PlaceUniform(Art + "SM_RustyPadlock.fbx", "Vault_Padlock", framing.transform, Vector3.zero, 0.16f, 0f);

        LayoutAlley();
        EditorSceneManager.SaveScene(scene, ScenePath);
        ApplyGrittyNight();
        BuildGameplay();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Diegetic alley saved to " + ScenePath);
    }

    // Gives the two police spotlights their beams.
    [MenuItem("Juicy/Alley/Dress Spec Gaps")]
    public static void DressSpecGaps()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new System.InvalidOperationException("Active scene is " + scene.path);

        EnsureSpot("Spotlight_Left");
        EnsureSpot("Spotlight_Right");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Diegetic alley spec gaps dressed");
    }

    static void EnsureSpot(string parentName)
    {
        GameObject parent = GameObject.Find(parentName);
        if (parent == null) return;
        string lightName = parentName + "_Light";
        if (parent.transform.Find(lightName) != null) return;
        GameObject lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(parent.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        lightObject.transform.LookAt(new Vector3(0f, 0.45f, 1.3f));
        Light spot = lightObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(0.82f, 0.9f, 1f);
        spot.intensity = 4.5f;
        spot.range = 16f;
        spot.spotAngle = 42f;
        spot.innerSpotAngle = 18f;
        spot.shadows = LightShadows.None;
    }

    [MenuItem("Juicy/Alley/Apply Lighting And Lanes")]
    public static void ApplyLightingAndLanes()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new System.InvalidOperationException("Active scene is " + scene.path);

        int materials = AssignEnvironmentLit();
        TuneNightDirectional();
        EnsureWarmLamp("Hanging_Lamp_Left");
        EnsureWarmLamp("Hanging_Lamp_Right");
        AlignColumns();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        CaptureAlley();
        Debug.Log("Diegetic alley lighting and lanes applied. Materials " + materials);
    }

    static int AssignEnvironmentLit()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new System.InvalidOperationException("URP Lit shader is missing");
        if (!AssetDatabase.IsValidFolder(MaterialRoot))
            AssetDatabase.CreateFolder("Assets/Art/Alley", "Materials");

        int count = 0;
        string[] modelGuids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Art/Alley" });
        var seen = new HashSet<string>();
        for (int i = 0; i < modelGuids.Length; i++)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
            if (!modelPath.EndsWith(".fbx")) continue;
            if (modelPath.Contains("/Raccoon/")) continue;
            if (!seen.Add(modelPath)) continue;

            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            string texturePath = TextureRoot + modelName + "_basecolor.jpg";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) continue;

            TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            int textureSize = StructuralTextureSize(modelName);
            if (textureImporter != null && textureImporter.maxTextureSize != textureSize)
            {
                textureImporter.maxTextureSize = textureSize;
                textureImporter.textureCompression = TextureImporterCompression.Compressed;
                textureImporter.sRGBTexture = true;
                textureImporter.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }

            string materialPath = MaterialRoot + "/" + modelName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(lit) { name = modelName };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.shader = lit;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", modelName == "SM_GraffitiVaultDoor" ? 0.2f : 0.25f);
            EditorUtility.SetDirty(material);

            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            for (int a = 0; a < assets.Length; a++)
            {
                Material embedded = assets[a] as Material;
                if (embedded == null) continue;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();
            count++;
        }

        AssetDatabase.SaveAssets();
        PaintSceneMaterials();
        return count;
    }

    // The 15 degree camera fills the frame with these meshes. A 512 atlas
    // leaves the vault face as a grey smear. Smaller props stay at 512.
    static int StructuralTextureSize(string modelName)
    {
        if (modelName == "SM_BrickWall" || modelName == "SM_GraffitiVaultDoor" || modelName == "SM_IndustrialPipe")
            return 2048;
        return 512;
    }

    static void PaintSceneMaterials()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            string modelPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderers[i].gameObject);
            if (string.IsNullOrEmpty(modelPath) || !modelPath.StartsWith(Art) || modelPath.Contains("/Raccoon/"))
                continue;
            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "/" + modelName + ".mat");
            if (material == null) continue;
            Material[] slots = renderers[i].sharedMaterials;
            bool changed = false;
            for (int s = 0; s < slots.Length; s++)
            {
                if (slots[s] == material) continue;
                slots[s] = material;
                changed = true;
            }
            if (changed) renderers[i].sharedMaterials = slots;
        }
    }

    static void TuneNightDirectional()
    {
        Light directional = FindKeyLight();
        if (directional == null)
        {
            GameObject created = new GameObject("Night_Directional");
            directional = created.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        directional.name = "Night_Directional";
        directional.color = Color.white;
        directional.intensity = KeyIntensity;
        directional.shadows = LightShadows.Hard;
        directional.shadowStrength = 0.92f;
        directional.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.sun = directional;
    }

    static void EnsureWarmLamp(string parentName)
    {
        GameObject parent = GameObject.Find(parentName);
        if (parent == null) return;
        string lightName = parentName + "_Warm";
        Transform existing = parent.transform.Find(lightName);
        GameObject lightObject = existing != null ? existing.gameObject : new GameObject(lightName);
        if (existing == null) lightObject.transform.SetParent(parent.transform, true);
        Renderer renderer = parent.GetComponentInChildren<Renderer>();
        if (renderer != null)
            lightObject.transform.position = renderer.bounds.center + Vector3.down * (renderer.bounds.extents.y * 0.35f);
        Light point = lightObject.GetComponent<Light>();
        if (point == null) point = lightObject.AddComponent<Light>();
        point.type = LightType.Point;
        point.color = WarmAmber;
        point.intensity = 4.5f;
        point.range = 7f;
        point.shadows = LightShadows.None;
        point.renderMode = LightRenderMode.ForcePixel;
    }

    static void EnsureFillLight()
    {
        GameObject existing = GameObject.Find("Fill_Directional");
        GameObject lightObject = existing != null ? existing : new GameObject("Fill_Directional");
        Light fill = lightObject.GetComponent<Light>();
        if (fill == null) fill = lightObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = FillBlue;
        fill.intensity = 0.4f;
        fill.shadows = LightShadows.None;
        fill.renderMode = LightRenderMode.ForcePixel;
        fill.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    static Light FindKeyLight()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Light fallback = null;
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type != LightType.Directional) continue;
            if (lights[i].name == "Fill_Directional") continue;
            if (lights[i].name == "Night_Directional") return lights[i];
            if (fallback == null) fallback = lights[i];
        }
        return fallback;
    }

    static void AlignColumns()
    {
        for (int i = 0; i < ColumnX.Length; i++)
            SnapXZKeepY(GameObject.Find("Drop_0" + (i + 1)), ColumnX[i], ColumnZ);
    }

    [MenuItem("Juicy/Alley/Apply Exposure Baseline")]
    public static void ApplyExposureBaseline()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new System.InvalidOperationException("Active scene is " + scene.path);

        if (PlayerSettings.colorSpace != ColorSpace.Linear)
            PlayerSettings.colorSpace = ColorSpace.Linear;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientGray;
        RenderSettings.ambientIntensity = 1.2f;
        DynamicGI.UpdateEnvironment();
        TuneNightDirectional();
        WhitenAlleyBaseColors();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        CaptureAlley();
        Debug.Log("Exposure baseline applied");
    }

    [MenuItem("Juicy/Alley/Pop Structure")]
    public static void ApplyStructurePop()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new System.InvalidOperationException("Active scene is " + scene.path);

        EnsureWarmLamp("Hanging_Lamp_Left");
        EnsureWarmLamp("Hanging_Lamp_Right");
        EnsureFillLight();
        Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NavyClear;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        CaptureAlley();
        Debug.Log("Structure pop applied");
    }

    // The vault sits at the far end of an alley. Facade models face +Z, so
    // yaw 180 turns their painted front to the camera. Scale stays uniform
    // so every model keeps its authored proportions.
    const float VaultZ = 11f;
    static readonly Vector3 VaultAim = new Vector3(0f, 1.3f, VaultZ);
    const string BrickWallModel = Art + "SM_BrickWall.fbx";
    const string ArmoredCarModel = Art + "SM_ArmoredRaceCar.fbx";
    const string WallLampModel = Art + "SM_OutdoorWallLamp.fbx";
    const string HangingLampModel = Art + "SM_IndustrialHangingLamp.fbx";

    [MenuItem("Juicy/Alley/Layout Alley")]
    public static void LayoutAlleyInScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new System.InvalidOperationException("Active scene is " + scene.path);
        AssignEnvironmentLit();
        LayoutAlley();
        ApplyGrittyNight();
        WireGameplayInScene();
    }

    static void LayoutAlley()
    {
        Transform environment = GameObject.Find("01_Environment_Static").transform;
        string[] legacy = { "Vault_Wall_Mesh", "Facade_Options", "Catwalk_Top_Mesh", "Alley_Set", "Dumpster_Left", "Car_Fender_Right", "Gutter_Pipes_Modular", "Trash_Bag_Clutter", "Sodium_Street_Light", "Hanging_Lamp_Left", "Hanging_Lamp_Right" };
        for (int i = 0; i < legacy.Length; i++)
        {
            GameObject old = GameObject.Find(legacy[i]);
            if (old != null) Object.DestroyImmediate(old);
        }

        Transform set = Empty("Alley_Set", environment, Vector3.zero).transform;
        GameObject building = PlaceWide(BrickWallModel, "Back_Building", set, new Vector3(0f, 0f, VaultZ + 1.6f), 5.4f, 180f);
        GameObject vault = PlaceWide(Art + "SM_GraffitiVaultDoor.fbx", "Vault_Facade", set, new Vector3(0f, 0f, VaultZ), 3f, 180f);
        // The car runs along its own X with the hood at +X.
        if (WideLens)
        {
            // Walls run straight down both sides; yaw 90 turns the painted
            // face of the left wall toward the alley, -90 the right.
            float[] segments = { 0.9f, 4.5f, 8.1f };
            for (int i = 0; i < segments.Length; i++)
            {
                SideWall("Alley_Wall_Left_" + i, set, new Vector3(-1.95f, 0f, segments[i]), 90f);
                SideWall("Alley_Wall_Right_" + i, set, new Vector3(1.95f, 0f, segments[i]), -90f);
            }
            PlaceWide(Art + "SM_Dumpster.fbx", "Dumpster", set, new Vector3(-1.2f, 0f, 5.6f), 1.3f, 160f);
            PlaceWide(ArmoredCarModel, "Getaway_Car", set, new Vector3(1.15f, 0f, 7.2f), 2f, -115f);
            PlacePipe("Drainpipe_Left", set, new Vector3(-1.85f, 0f, 9.6f), 2.6f);
            PlacePipe("Drainpipe_Right", set, new Vector3(1.85f, 0f, 9.6f), 2.6f);
            PlaceUniform(Art + "SM_GarbageBag.fbx", "Trash_Bag_01", set, new Vector3(-0.55f, 0f, 5.9f), 0.42f, 15f);
            PlaceUniform(Art + "SM_GarbageBag.fbx", "Trash_Bag_02", set, new Vector3(-1.55f, 0f, 4.4f), 0.38f, -20f);
            PlaceUniform(Art + "SM_GarbageBag.fbx", "Trash_Bag_03", set, new Vector3(1.5f, 0f, 4.6f), 0.34f, 40f);
        }
        else
        {
            PlaceWide(BrickWallModel, "Alley_Wall_Left", set, new Vector3(-1.95f, 0f, 8.4f), 3.6f, 140f);
            PlaceWide(BrickWallModel, "Alley_Wall_Right", set, new Vector3(1.95f, 0f, 8.4f), 3.6f, 220f);
            PlaceWide(Art + "SM_Dumpster.fbx", "Dumpster", set, new Vector3(-1.05f, 0f, 7f), 1.3f, 195f);
            PlaceWide(ArmoredCarModel, "Getaway_Car", set, new Vector3(1.35f, 0f, 7.6f), 1.7f, -22f);
            PlacePipe("Drainpipe_Left", set, new Vector3(-1.75f, 0f, 9.4f), 2.4f);
            PlacePipe("Drainpipe_Right", set, new Vector3(1.75f, 0f, 9.4f), 2.4f);
            PlaceUniform(Art + "SM_GarbageBag.fbx", "Trash_Bag_01", set, new Vector3(-0.35f, 0f, 7.3f), 0.42f, 15f);
            PlaceUniform(Art + "SM_GarbageBag.fbx", "Trash_Bag_02", set, new Vector3(-1.35f, 0f, 6.1f), 0.38f, -20f);
            PlaceUniform(Art + "SM_GarbageBag.fbx", "Trash_Bag_03", set, new Vector3(0.5f, 0f, 9.9f), 0.34f, 40f);
        }

        // Wall lamps face into the alley: the model's head points along +X.
        PlaceWide(WallLampModel, "Wall_Lamp_Left", set, new Vector3(-1.5f, 2.2f, 4.6f), 0.6f, 0f);
        PlaceWide(WallLampModel, "Wall_Lamp_Right", set, new Vector3(1.5f, 2.2f, 7.6f), 0.6f, 180f);

        BuildRoad(GameObject.Find("Alley_Framing").transform);

        Bounds vaultBounds;
        TryBounds(vault, out vaultBounds);
        Bounds buildingBounds;
        TryBounds(building, out buildingBounds);

        // The timer fills the building window above the vault.
        Vector3 window = SurfacePoint(building, new Vector3(0f, buildingBounds.min.y + buildingBounds.size.y * 0.6f, buildingBounds.center.z));
        Face("Vault_Score_Counter", 180f);
        MoveTo("Vault_Score_Counter", window + new Vector3(0f, -0.21f, -0.08f));

        // The string lamps hang over the top corners of that window.
        Transform spots = GameObject.Find("Wall_Spotlights").transform;
        Vector3 windowTop = window + new Vector3(0f, 0.7f, -0.12f);
        PlaceWide(HangingLampModel, "Hanging_Lamp_Left", spots, windowTop + new Vector3(-0.6f, 0f, 0f), 0.7f, 180f);
        PlaceWide(HangingLampModel, "Hanging_Lamp_Right", spots, windowTop + new Vector3(0.6f, 0f, 0f), 0.7f, 180f);
        MoveTo("Police_Lightbar_Alarm", new Vector3(0f, vaultBounds.max.y, vaultBounds.center.z));
        Vector3 handle = SurfacePoint(vault, new Vector3(vaultBounds.size.x * 0.15f, vaultBounds.min.y + vaultBounds.size.y * 0.375f, vaultBounds.center.z));
        MoveTo("Vault_Padlock", handle + new Vector3(0f, -0.08f, -0.06f));

        StageRaccoon();
    }

    // The raccoon stands centered on the manhole cover, facing the camera,
    // posed on the first frame of his torch idle so the editor shows what play
    // shows. Height is measured on the skinned body itself, not the torch.
    const float RaccoonYaw = 180f;
    const float RaccoonHeight = 0.95f;
    const float TorchLength = 0.64f;

    static void StageRaccoon()
    {
        GameObject raccoon = GameObject.Find("Raccoon_Mascot");
        if (raccoon == null) return;
        Animator animator = raccoon.GetComponentInChildren<Animator>();
        AnimationClip idle = LoadClip(IdlePath);

        raccoon.transform.rotation = Quaternion.Euler(0f, RaccoonYaw, 0f);
        raccoon.transform.localScale = Vector3.one;
        if (idle != null && animator != null) idle.SampleAnimation(animator.gameObject, 0f);
        Bounds body;
        if (!BodyBounds(raccoon, out body)) return;
        raccoon.transform.localScale = Vector3.one * (RaccoonHeight / Mathf.Max(body.size.y, 0.001f));
        if (idle != null && animator != null) idle.SampleAnimation(animator.gameObject, 0f);
        BodyBounds(raccoon, out body);
        Vector3 spot = RaccoonSpot();
        // Centre on the feet, not the body: the tail and loot sack pull the
        // body's centre well off his stance.
        Vector3 stance = body.center;
        if (animator != null && animator.isHuman)
        {
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (left != null && right != null) stance = (left.position + right.position) * 0.5f;
        }
        float soleNow = animator != null && animator.isHuman ? SoleHeight(raccoon, animator) : float.NaN;
        float lowestNow = float.IsNaN(soleNow) ? body.min.y : soleNow;
        raccoon.transform.position += new Vector3(spot.x - stance.x, spot.y - lowestNow, spot.z - stance.z);

        StandOnSpot stand = raccoon.GetComponent<StandOnSpot>();
        if (stand == null) stand = raccoon.AddComponent<StandOnSpot>();
        stand.Configure(animator, raccoon.GetComponentInChildren<SkinnedMeshRenderer>(), spot);

        GripTorch(raccoon);
    }

    // Centre of the manhole cover's top surface.
    static Vector3 RaccoonSpot()
    {
        // Mesh renderers only: the steam's particle renderer would add height.
        GameObject manhole = GameObject.Find("Manhole");
        MeshRenderer[] parts = manhole != null ? manhole.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
        if (parts.Length == 0) return new Vector3(ManholeSpot.x, 0f, ManholeSpot.z);
        Bounds cover = parts[0].bounds;
        for (int i = 1; i < parts.Length; i++) cover.Encapsulate(parts[i].bounds);
        return new Vector3(cover.center.x, cover.max.y, cover.center.z);
    }

    // Lowest point of the vertices mostly skinned to the feet or toes.
    static float SoleHeight(GameObject root, Animator animator)
    {
        SkinnedMeshRenderer skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin == null) return float.NaN;
        var feet = new HashSet<Transform>();
        HumanBodyBones[] footBones = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
        foreach (HumanBodyBones bone in footBones)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null) feet.Add(t);
        }
        Transform[] bones = skin.bones;
        BoneWeight[] weights = skin.sharedMesh.boneWeights;
        Mesh baked = new Mesh();
        skin.BakeMesh(baked, true);
        Vector3[] vertices = baked.vertices;
        float lowest = float.NaN;
        for (int i = 0; i < vertices.Length && i < weights.Length; i++)
        {
            BoneWeight w = weights[i];
            float onFeet = 0f;
            if (feet.Contains(bones[w.boneIndex0])) onFeet += w.weight0;
            if (feet.Contains(bones[w.boneIndex1])) onFeet += w.weight1;
            if (feet.Contains(bones[w.boneIndex2])) onFeet += w.weight2;
            if (feet.Contains(bones[w.boneIndex3])) onFeet += w.weight3;
            if (onFeet < 0.5f) continue;
            float y = skin.transform.TransformPoint(vertices[i]).y;
            if (float.IsNaN(lowest) || y < lowest) lowest = y;
        }
        Object.DestroyImmediate(baked);
        return lowest;
    }

    // World bounds of the posed skinned mesh. SkinnedMeshRenderer.bounds can
    // lag a freshly sampled pose in the editor, so the mesh is baked.
    static bool BodyBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        SkinnedMeshRenderer skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin == null) return false;
        Mesh baked = new Mesh();
        skin.BakeMesh(baked, true);
        Vector3[] vertices = baked.vertices;
        Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
        bounds = new Bounds(toWorld.MultiplyPoint3x4(vertices[0]), Vector3.zero);
        for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(toWorld.MultiplyPoint3x4(vertices[i]));
        Object.DestroyImmediate(baked);
        return true;
    }

    // Stands the torch upright in the right hand: grip a third of the way up
    // the handle, flame on top, leaning slightly toward the camera.
    static void GripTorch(GameObject raccoon)
    {
        Transform hand = FindNamed(raccoon.transform, "RightHand");
        Transform torch = hand != null ? hand.Find("Torch") : null;
        if (torch == null) return;
        Light flame = torch.GetComponentInChildren<Light>();
        if (flame != null) flame.transform.SetParent(null, true);

        torch.rotation = Quaternion.Euler(-8f, RaccoonYaw, 0f);
        torch.localScale = Vector3.one;
        Bounds b;
        TryBounds(torch.gameObject, out b);
        torch.localScale = Vector3.one * (TorchLength / Mathf.Max(b.size.y, 0.001f) / Mathf.Max(hand.lossyScale.y, 0.0001f));
        TryBounds(torch.gameObject, out b);
        Vector3 grip = new Vector3(b.center.x, b.min.y + b.size.y * 0.33f, b.center.z);
        torch.position += hand.position - grip;

        // The humanoid idle turns the hand differently at runtime than this
        // editor pose, so TorchGrip holds the torch upright every frame.
        TorchGrip holder = torch.GetComponent<TorchGrip>();
        if (holder == null) holder = torch.gameObject.AddComponent<TorchGrip>();
        holder.Configure(hand, raccoon.transform, torch.InverseTransformPoint(hand.position), -8f);

        if (flame == null) return;
        TryBounds(torch.gameObject, out b);
        flame.transform.position = new Vector3(b.center.x, b.max.y + 0.04f, b.center.z);
        flame.transform.SetParent(torch, true);
    }

    static void Face(string name, float yaw)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // The brick wall model has a fire escape built in. Side walls are
    // flattened in depth so it sits flush instead of jutting into the alley.
    static void SideWall(string name, Transform parent, Vector3 bottomCenter, float yaw)
    {
        GameObject wall = PlaceWide(BrickWallModel, name, parent, bottomCenter, 3.6f, yaw);
        Vector3 scale = wall.transform.localScale;
        wall.transform.localScale = new Vector3(scale.x, scale.y, scale.z * 0.3f);
        SnapBottom(wall, bottomCenter);
    }

    static void MoveTo(string name, Vector3 bottomCenter)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) SnapBottom(go, bottomCenter);
    }

    static void PlacePipe(string name, Transform parent, Vector3 bottomCenter, float height)
    {
        // The pipe model runs along its own X. Roll it upright.
        GameObject go = PlaceUniform(Art + "SM_IndustrialPipe.fbx", name, parent, bottomCenter, 1f, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 180f, 90f);
        go.transform.localScale = Vector3.one;
        Renderer renderer = go.GetComponentInChildren<Renderer>();
        if (renderer == null) return;
        go.transform.localScale = Vector3.one * (height / Mathf.Max(renderer.bounds.size.y, 0.001f));
        SnapBottom(go, bottomCenter);
    }

    // Point on the target's visible surface along the camera ray through guess.
    static Vector3 SurfacePoint(GameObject target, Vector3 guess)
    {
        Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (camera == null) return guess;
        var added = new List<MeshCollider>();
        foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>())
        {
            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            added.Add(collider);
        }
        Physics.SyncTransforms();
        Vector3 origin = camera.transform.position;
        Vector3 direction = (guess - origin).normalized;
        Vector3 result = guess;
        float best = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, 100f))
        {
            if (!added.Contains(hit.collider as MeshCollider)) continue;
            if (hit.distance >= best) continue;
            best = hit.distance;
            result = hit.point;
        }
        foreach (MeshCollider collider in added) Object.DestroyImmediate(collider);
        return result;
    }

    static GameObject PlaceWide(string path, string name, Transform parent, Vector3 bottomCenter, float targetWidth, float yaw)
    {
        GameObject go = PlaceUniform(path, name, parent, bottomCenter, 1f, yaw);
        Renderer renderer = go.GetComponentInChildren<Renderer>();
        if (renderer == null) return go;
        // Width along the model's own X so angled walls keep their size.
        Vector3 local = renderer.localBounds.size;
        float modelWidth = local.x * renderer.transform.lossyScale.x / go.transform.localScale.x;
        go.transform.localScale = Vector3.one * (targetWidth / Mathf.Max(modelWidth, 0.001f));
        SnapBottom(go, bottomCenter);
        return go;
    }

    const string DiamondModel = Art + "SM_Diamond.fbx";
    const string DiamondMaterialPath = Art + "M_Diamond.mat";
    const string LetterMaterialPath = Art + "M_LetterTile.mat";
    const string ReadoutMaterialPath = Art + "M_ScoreReadoutPanel.mat";

    [MenuItem("Juicy/Alley/Wire Gameplay")]
    public static void WireGameplayInScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new System.InvalidOperationException("Active scene is " + scene.path);
        BuildGameplay();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        CaptureAlley();
        Debug.Log("Heist gameplay wired");
    }

    // The chute director, its item pool, the two color chests, the reactions
    // and the vault timer readout. Static loot placeholders give way to the
    // falling items; the crates and padlock stay as dressing.
    static void BuildGameplay()
    {
        Transform framing = GameObject.Find("Alley_Framing").transform;
        Reparent("Heavy_Crates", framing);
        Reparent("Vault_Padlock", framing);
        string[] retired = { "Loot_Items", "Hazards", "Grid_Manager", "Sorting_Lanes", "Spawner_Bottom_Ground", "Loot_Chests", "Heist_Chute_Director" };
        for (int i = 0; i < retired.Length; i++)
        {
            GameObject old = GameObject.Find(retired[i]);
            if (old != null) Object.DestroyImmediate(old);
        }

        Transform grid = GameObject.Find("03_Gameplay_Grid_System").transform;
        if (GameObject.Find("Spawner_Top_Catwalk") == null) BuildDrops(grid);
        AlignColumns();
        Transform[] drops = new Transform[ColumnX.Length];
        for (int i = 0; i < drops.Length; i++) drops[i] = GameObject.Find("Drop_0" + (i + 1)).transform;

        Material diamond = DiamondMaterial();
        GameObject directorObject = Empty("Heist_Chute_Director", grid, Vector3.zero);
        AlleyItem template = BuildItemTemplate(directorObject.transform, diamond);

        Transform chests = Empty("Loot_Chests", grid, Vector3.zero).transform;
        AlleyBin left = BuildBin(chests, 0, new Vector3(-0.72f, 0f, -0.3f), 180f + 28f, diamond);
        AlleyBin right = BuildBin(chests, 1, new Vector3(0.72f, 0f, -0.3f), 180f - 28f, diamond);

        AlleyChuteDirector director = directorObject.AddComponent<AlleyChuteDirector>();
        director.Configure(drops, new[] { left, right }, template);

        GameObject raccoon = GameObject.Find("Raccoon_Mascot");
        var heatLights = new List<LightAnimator>();
        foreach (LightAnimator light in Object.FindObjectsByType<LightAnimator>(FindObjectsSortMode.None))
            if (light.name.StartsWith("Strobe_") || light.name.StartsWith("Alarm_")) heatLights.Add(light);
        Camera camera = Camera.main;
        AlleyReactions reactions = directorObject.AddComponent<AlleyReactions>();
        reactions.Configure(director, raccoon != null ? raccoon.GetComponentInChildren<Animator>() : null, heatLights.ToArray(), camera != null ? camera.transform : null);

        BuildScoreReadout(director);
        template.gameObject.SetActive(false);
        EditorUtility.SetDirty(directorObject);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void Reparent(string name, Transform parent)
    {
        GameObject go = GameObject.Find(name);
        if (go != null && go.transform.parent != parent) go.transform.SetParent(parent, true);
    }

    static Material DiamondMaterial()
    {
        Material material = LitMaterial(DiamondMaterialPath, "M_Diamond", Color.white, 0.15f, 0.93f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        material.SetColor("_EmissionColor", Color.black);
        return material;
    }

    static Material LitMaterial(string path, string name, Color color, float metallic, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Skin(GameObject go, Material material)
    {
        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
    }

    static GemColorController Tint(GameObject gem, Color color)
    {
        GemColorController tint = gem.GetComponent<GemColorController>();
        if (tint == null) tint = gem.AddComponent<GemColorController>();
        SerializedObject serialized = new SerializedObject(tint);
        serialized.FindProperty("gemColor").colorValue = color;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        tint.SetGemColor(color);
        return tint;
    }

    // A model sized to the given height and centered on the parent.
    static GameObject Centered(string path, string name, Transform parent, float height, float yaw)
    {
        GameObject go = PlaceUniform(path, name, parent, parent.position, height, yaw);
        SnapBottom(go, parent.position + Vector3.down * (height * 0.5f));
        return go;
    }

    static AlleyItem BuildItemTemplate(Transform parent, Material diamond)
    {
        GameObject root = Empty("Alley_Item_Template", parent, new Vector3(0f, -10f, 0f));
        GameObject gem = Centered(DiamondModel, "Gem", root.transform, 0.26f, 0f);
        Skin(gem, diamond);
        GemColorController tint = Tint(gem, AlleyChuteDirector.Ruby);
        GameObject bomb = Centered(Art + "SM_DynamiteBundle.fbx", "Bomb", root.transform, 0.34f, 0f);
        GameObject gold = Centered(Art + "SM_MoneyStack.fbx", "Gold", root.transform, 0.16f, 20f);

        GameObject letter = Empty("Letter", root.transform, root.transform.position);
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(tile.GetComponent<Collider>());
        tile.name = "Tile";
        tile.transform.SetParent(letter.transform, false);
        tile.transform.localScale = new Vector3(0.28f, 0.28f, 0.07f);
        tile.GetComponent<Renderer>().sharedMaterial = LitMaterial(LetterMaterialPath, "M_LetterTile", new Color(0.98f, 0.76f, 0.22f), 0.7f, 0.6f);
        GameObject glyph = new GameObject("Glyph");
        glyph.transform.SetParent(letter.transform, false);
        glyph.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        TextMeshPro text = glyph.AddComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = new Vector2(0.26f, 0.26f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.1f;
        text.fontSizeMax = 6f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.16f, 0.08f, 0.03f);
        text.text = "H";

        AlleyItem item = root.AddComponent<AlleyItem>();
        item.Wire(gem, bomb, gold, letter, text, tint);
        return item;
    }

    static AlleyBin BuildBin(Transform parent, int side, Vector3 bottom, float yaw, Material diamond)
    {
        Color color = AlleyChuteDirector.GemColor(side);
        string model = side == 0 ? DepositChestCrimson : DepositChestIndigo;
        RemapMaterials(model, DepositChestMaterials());
        GameObject chest = PlaceWide(model, side == 0 ? "Chest_Ruby" : "Chest_Sapphire", parent, bottom, 0.55f, yaw);
        Bounds b;
        TryBounds(chest, out b);
        Transform mouth = Empty("Mouth", chest.transform, new Vector3(b.center.x, b.max.y, b.center.z)).transform;

        GameObject glowObject = Empty("Glow", chest.transform, new Vector3(b.center.x, b.max.y + 0.25f, b.center.z - 0.2f));
        Light glow = glowObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 1.8f;
        glow.shadows = LightShadows.None;
        glow.renderMode = LightRenderMode.ForcePixel;

        GameObject markerRoot = Empty("Marker", chest.transform, new Vector3(b.center.x, b.max.y + 0.32f, b.center.z));
        GameObject marker = Centered(DiamondModel, "Marker_Gem", markerRoot.transform, 0.16f, 0f);
        Skin(marker, diamond);
        Tint(marker, color);

        AlleyBin bin = chest.AddComponent<AlleyBin>();
        bin.Configure(side, color, glow, mouth, markerRoot.transform);
        return bin;
    }

    const string DepositChestCrimson = Art + "SM_DepositChest_Crimson.fbx";
    const string DepositChestIndigo = Art + "SM_DepositChest_Indigo.fbx";

    // The deposit chests are built by ArtSource/Alley/build_deposit_chests.py.
    // Their Blender materials map to URP ones here. Metals are toned down
    // because the alley has almost no environment reflection to pick up.
    static Dictionary<string, Material> DepositChestMaterials()
    {
        return new Dictionary<string, Material>
        {
            { "M_Gunmetal", LitMaterial(MaterialRoot + "/M_ChestGunmetal.mat", "M_ChestGunmetal", new Color(0.2f, 0.21f, 0.24f), 0.6f, 0.65f) },
            { "M_BrushedSteel", LitMaterial(MaterialRoot + "/M_ChestSteel.mat", "M_ChestSteel", new Color(0.5f, 0.52f, 0.55f), 0.55f, 0.55f) },
            { "M_Chrome", LitMaterial(MaterialRoot + "/M_ChestChrome.mat", "M_ChestChrome", new Color(0.85f, 0.87f, 0.9f), 0.7f, 0.88f) },
            { "M_ChestGold", LitMaterial(MaterialRoot + "/M_ChestGold.mat", "M_ChestGold", new Color(1f, 0.72f, 0.22f), 0.75f, 0.75f) },
            { "M_Keyhole", LitMaterial(MaterialRoot + "/M_ChestKeyhole.mat", "M_ChestKeyhole", new Color(0.01f, 0.012f, 0.014f), 0.6f, 0.5f) },
            { "M_CrimsonGlow", GlowMaterial(MaterialRoot + "/M_ChestCrimsonGlow.mat", "M_ChestCrimsonGlow", AlleyChuteDirector.Ruby) },
            { "M_IndigoGlow", GlowMaterial(MaterialRoot + "/M_ChestIndigoGlow.mat", "M_ChestIndigoGlow", AlleyChuteDirector.Sapphire) },
        };
    }

    // Points a model's imported materials, by source name, at project materials.
    static void RemapMaterials(string modelPath, Dictionary<string, Material> materials)
    {
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        bool changed = false;
        foreach (var pair in materials)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
            Object current;
            if (importer.GetExternalObjectMap().TryGetValue(id, out current) && current == pair.Value) continue;
            importer.AddRemap(id, pair.Value);
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    static Material GlowMaterial(string path, string name, Color color)
    {
        Material material = LitMaterial(path, name, color, 0f, 0.75f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        material.SetColor("_EmissionColor", color * 3f);
        return material;
    }

    // Green digits on the vault timer's screen, over a dark panel that hides
    // the digits painted into its texture.
    static void BuildScoreReadout(AlleyChuteDirector director)
    {
        GameObject timer = GameObject.Find("Vault_Score_Counter");
        Bounds b;
        if (!TryBounds(timer, out b)) return;
        Transform old = timer.transform.Find("Score_Readout");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Vector2 size = new Vector2(b.size.x * 0.5f, b.size.y * 0.36f);
        GameObject root = new GameObject("Score_Readout");
        root.transform.position = new Vector3(b.center.x - b.size.x * 0.04f, b.center.y + b.size.y * 0.06f, b.min.z - 0.012f);

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(panel.GetComponent<Collider>());
        panel.name = "Panel";
        panel.transform.SetParent(root.transform, false);
        panel.transform.localPosition = new Vector3(0f, 0f, 0.004f);
        panel.transform.localScale = new Vector3(size.x, size.y, 1f);
        Material dark = AssetDatabase.LoadAssetAtPath<Material>(ReadoutMaterialPath);
        if (dark == null)
        {
            dark = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_ScoreReadoutPanel" };
            AssetDatabase.CreateAsset(dark, ReadoutMaterialPath);
        }
        dark.SetColor("_BaseColor", new Color(0.01f, 0.025f, 0.015f));
        panel.GetComponent<Renderer>().sharedMaterial = dark;

        GameObject digits = new GameObject("Digits");
        digits.transform.SetParent(root.transform, false);
        TextMeshPro text = digits.AddComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = size * 0.92f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.05f;
        text.fontSizeMax = 8f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.35f, 1f, 0.45f);
        text.text = "01000";

        root.AddComponent<AlleyScoreDisplay>().Configure(director, text);
        root.transform.SetParent(timer.transform, true);
    }

    const string NightProfilePath = Art + "AlleyNightProfile.asset";
    const string NightSkyPath = TextureRoot + "T_AlleyNightSky.png";
    const string NightSkyMaterialPath = Art + "M_AlleyNightSky.mat";
    const string BulbMaterialPath = Art + "M_LampBulbGlow.mat";
    static readonly Color SodiumOrange = new Color(1f, 0.6f, 0.28f);
    static readonly Color MoonCool = new Color(0.74f, 0.83f, 1f);

    // Gritty night look: warm lamp pools on the wall, a sodium street light
    // washing the vault door, cool moon, tone mapping and bloom, and a sky
    // backdrop so the top of the frame is not a flat void.
    [MenuItem("Juicy/Alley/Gritty Night Lighting")]
    public static void ApplyGrittyNight()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new System.InvalidOperationException("Active scene is " + scene.path);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.52f, 0.7f);
        RenderSettings.ambientEquatorColor = new Color(0.44f, 0.42f, 0.46f);
        RenderSettings.ambientGroundColor = new Color(0.5f, 0.38f, 0.28f);
        // Smooth surfaces otherwise mirror Unity's bright default sky.
        RenderSettings.reflectionIntensity = 0.12f;
        DynamicGI.UpdateEnvironment();

        Light moon = FindKeyLight();
        if (moon != null)
        {
            moon.color = MoonCool;
            moon.intensity = 1.7f;
            // Hard 0.92 shadow under the catwalk crushed the back wall to black.
            moon.shadowStrength = 0.6f;
        }

        AimLampAtWall("Hanging_Lamp_Left", VaultAim + new Vector3(-0.9f, 0.3f, 0f));
        AimLampAtWall("Hanging_Lamp_Right", VaultAim + new Vector3(0.9f, 0.3f, 0f));
        WallLampLight("Wall_Lamp_Left", SodiumOrange, new Vector3(0f, 0f, 6.2f));
        WallLampLight("Wall_Lamp_Right", MercuryWhite, new Vector3(0f, 0f, 8.8f));
        EnsureNightSky();
        EnsureMoon();
        EnsureClouds();

        // Cold distance fog: the far end of the alley sinks into haze while the
        // play area up front stays clear.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.035f, 0.045f, 0.08f);
        RenderSettings.fogStartDistance = 9f;
        RenderSettings.fogEndDistance = 46f;
        EnsureNightVolume();

        Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (camera != null)
        {
            camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        AnimateLights();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        CaptureAlley();
        PreviewStrobe();
        Debug.Log("Gritty night lighting applied");
    }

    static readonly Color PoliceRed = new Color(1f, 0.12f, 0.1f);
    static readonly Color PoliceBlue = new Color(0.15f, 0.35f, 1f);
    static readonly Color AlarmPink = new Color(1f, 0.2f, 0.6f);

    // Lamps flicker, the sodium light buzzes, the police bar strobes red and
    // blue onto the vault graffiti, and the vault alarms pulse pink.
    static void AnimateLights()
    {
        for (int i = 0; i < 2; i++)
        {
            string lamp = i == 0 ? "Hanging_Lamp_Left" : "Hanging_Lamp_Right";
            GameObject parent = GameObject.Find(lamp);
            if (parent == null) continue;
            Transform warm = parent.transform.Find(lamp + "_Warm");
            Transform bulb = parent.transform.Find(lamp + "_Bulb");
            if (warm == null) continue;
            Light light = warm.GetComponent<Light>();
            Animate(light, LightAnimator.Pattern.Flicker, light.intensity, 1.3f, 0.18f, i * 0.37f,
                bulb != null ? bulb.GetComponent<Renderer>() : null, BulbMaterial().GetColor("_BaseColor"));
        }

        string[] wallLamps = { "Wall_Lamp_Left", "Wall_Lamp_Right" };
        for (int i = 0; i < wallLamps.Length; i++)
        {
            GameObject lamp = GameObject.Find(wallLamps[i]);
            if (lamp == null) continue;
            Transform lightObject = lamp.transform.Find(wallLamps[i] + "_Light");
            Transform glow = lamp.transform.Find(wallLamps[i] + "_Glow");
            if (lightObject == null) continue;
            Light light = lightObject.GetComponent<Light>();
            Color tint = light.color * 4f;
            Animate(light, LightAnimator.Pattern.Buzz, light.intensity, 1f, 0.12f, i * 0.41f,
                glow != null ? glow.GetComponent<Renderer>() : null, new Color(tint.r, tint.g, tint.b, 1f));
        }

        Bounds bar;
        GameObject lightbar = GameObject.Find("Police_Lightbar_Alarm");
        if (TryBounds(lightbar, out bar))
        {
            float y = bar.max.y + 0.05f;
            float z = bar.min.z - 0.1f;
            Light red = ChildLight(lightbar.transform, "Strobe_Red", new Vector3(bar.min.x + bar.size.x * 0.25f, y, z), PoliceRed, 3.5f);
            Light blue = ChildLight(lightbar.transform, "Strobe_Blue", new Vector3(bar.max.x - bar.size.x * 0.25f, y, z), PoliceBlue, 3.5f);
            Animate(red, LightAnimator.Pattern.Strobe, 6f, 1.4f, 1f, 0f, null, Color.white);
            Animate(blue, LightAnimator.Pattern.Strobe, 7f, 1.4f, 1f, 0.5f, null, Color.white);
        }

        Bounds vault;
        GameObject facade = GameObject.Find("Vault_Facade");
        if (TryBounds(facade, out vault))
        {
            // The pink alarm lenses sit on the outer pillars, a little above middle.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 lens = SurfacePoint(facade, new Vector3(side * vault.size.x * 0.43f, vault.min.y + vault.size.y * 0.53f, vault.center.z));
                Light alarm = ChildLight(facade.transform, side < 0 ? "Alarm_Left" : "Alarm_Right", lens + new Vector3(0f, 0f, -0.2f), AlarmPink, 2.2f);
                Animate(alarm, LightAnimator.Pattern.Pulse, 3f, 0.7f, 0.75f, side < 0 ? 0f : 0.5f, null, Color.white);
            }
        }
    }

    static Light ChildLight(Transform parent, string name, Vector3 position, Color color, float range)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null) go.transform.SetParent(parent, true);
        go.transform.position = position;
        Light light = go.GetComponent<Light>();
        if (light == null) light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        return light;
    }

    static void Animate(Light light, LightAnimator.Pattern pattern, float intensity, float speed, float depth, float phase, Renderer glow, Color glowColor)
    {
        if (light == null) return;
        LightAnimator animator = light.GetComponent<LightAnimator>();
        if (animator == null) animator = light.gameObject.AddComponent<LightAnimator>();
        animator.Configure(pattern, intensity, speed, depth, phase, glow, glowColor);
        animator.Apply(0f);
        EditorUtility.SetDirty(animator);
    }

    // Renders the red and the blue moment of the police strobe.
    static void PreviewStrobe()
    {
        LightAnimator[] animators = Object.FindObjectsByType<LightAnimator>(FindObjectsSortMode.None);
        float[] moments = { 0.02f, 0.53f };
        for (int m = 0; m < moments.Length; m++)
        {
            for (int i = 0; i < animators.Length; i++) animators[i].Apply(moments[m] / 1.4f);
            CaptureAlley("/tmp/juicy-alley-strobe-" + m + ".png");
        }
        for (int i = 0; i < animators.Length; i++) animators[i].Apply(0f);
    }

    static void AimLampAtWall(string parentName, Vector3 target)
    {
        EnsureWarmLamp(parentName);
        GameObject parent = GameObject.Find(parentName);
        if (parent == null) return;
        Transform lamp = parent.transform.Find(parentName + "_Warm");
        Light light = lamp.GetComponent<Light>();
        light.type = LightType.Spot;
        light.color = WarmAmber;
        light.intensity = 32f;
        light.range = 12f;
        light.spotAngle = 100f;
        light.innerSpotAngle = 30f;
        lamp.LookAt(target);

        string bulbName = parentName + "_Bulb";
        Transform bulb = parent.transform.Find(bulbName);
        if (bulb == null)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.name = bulbName;
            sphere.transform.SetParent(parent.transform, true);
            bulb = sphere.transform;
        }
        bulb.position = lamp.position;
        bulb.localScale = Vector3.one;
        bulb.localScale = new Vector3(0.14f / bulb.lossyScale.x, 0.14f / bulb.lossyScale.y, 0.14f / bulb.lossyScale.z);
        Renderer renderer = bulb.GetComponent<Renderer>();
        renderer.sharedMaterial = BulbMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Material BulbMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BulbMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_LampBulbGlow" };
            AssetDatabase.CreateAsset(material, BulbMaterialPath);
        }
        // HDR above 1 so bloom picks the bulb up.
        material.SetColor("_BaseColor", new Color(6f, 3.4f, 1.2f));
        EditorUtility.SetDirty(material);
        return material;
    }

    static readonly Color MercuryWhite = new Color(0.78f, 1f, 0.86f);
    const string MoonTexturePath = TextureRoot + "T_AlleyMoon.png";
    const string MoonMaterialPath = Art + "M_AlleyMoon.mat";
    static readonly Vector3 MoonPosition = new Vector3(-2f, 6.6f, 15.5f);

    // A spot under the lamp head, plus a glowing lens for bloom.
    static void WallLampLight(string lampName, Color color, Vector3 target)
    {
        GameObject lamp = GameObject.Find(lampName);
        Bounds b;
        if (!TryBounds(lamp, out b)) return;
        float headward = lamp.transform.right.x >= 0f ? 1f : -1f;
        Vector3 lens = new Vector3(headward > 0f ? b.max.x - 0.3f * b.size.x : b.min.x + 0.3f * b.size.x, b.max.y - 0.32f * b.size.y, b.center.z);

        Light light = ChildLight(lamp.transform, lampName + "_Light", lens + Vector3.down * 0.06f, color, 9f);
        light.type = LightType.Spot;
        light.intensity = 26f;
        light.spotAngle = 95f;
        light.innerSpotAngle = 35f;
        light.transform.LookAt(target);

        Transform glow = lamp.transform.Find(lampName + "_Glow");
        if (glow == null)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.name = lampName + "_Glow";
            disc.transform.SetParent(lamp.transform, true);
            glow = disc.transform;
        }
        glow.position = lens;
        glow.rotation = Quaternion.identity;
        glow.localScale = Vector3.one;
        Vector3 world = new Vector3(0.2f, 0.05f, 0.16f);
        Vector3 lossy = glow.lossyScale;
        glow.localScale = new Vector3(world.x / lossy.x, world.y / lossy.y, world.z / lossy.z);
        Renderer renderer = glow.GetComponent<Renderer>();
        renderer.sharedMaterial = BulbMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // Two drifting cloud layers bracket the moon: a dense bank behind it and
    // thin wisps in front, plus a soft halo. Textures come from
    // ArtSource/Alley/build_night_sky.py.
    static void EnsureClouds()
    {
        Transform environment = GameObject.Find("01_Environment_Static").transform;
        string[] layers = { "Clouds_Far", "Moon_Halo", "Clouds_Near" };
        for (int i = 0; i < layers.Length; i++)
        {
            GameObject old = GameObject.Find(layers[i]);
            if (old != null) Object.DestroyImmediate(old);
        }

        const string unlit = "Universal Render Pipeline/Unlit";
        SkyLayer("Clouds_Far", environment, new Vector3(0f, 7.4f, 15.85f), new Vector2(40f, 7f),
            TransparentMaterial(MaterialRoot + "/M_CloudsFar.mat", "M_CloudsFar", unlit, SkyTexture("T_AlleyClouds_Far"), new Color(1f, 1f, 1f, 0.95f)), 0.0035f);
        SkyLayer("Moon_Halo", environment, MoonPosition + new Vector3(0f, 0f, 0.15f), new Vector2(4.6f, 4.6f),
            TransparentMaterial(MaterialRoot + "/M_MoonHalo.mat", "M_MoonHalo", unlit, SkyTexture("T_AlleyMoonHalo"), new Color(0.7f, 0.78f, 1f, 0.5f)), 0f);
        SkyLayer("Clouds_Near", environment, new Vector3(0f, 6.7f, 14.85f), new Vector2(40f, 4f),
            TransparentMaterial(MaterialRoot + "/M_CloudsNear.mat", "M_CloudsNear", unlit, SkyTexture("T_AlleyClouds_Near"), new Color(1f, 1f, 1f, 0.8f)), 0.009f);
    }

    static Texture2D SkyTexture(string name)
    {
        string path = TextureRoot + name + ".png";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.wrapModeU != TextureWrapMode.Repeat || importer.wrapModeV != TextureWrapMode.Clamp || !importer.alphaIsTransparency)
        {
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void SkyLayer(string name, Transform parent, Vector3 center, Vector2 size, Material material, float drift)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.name = name;
        quad.transform.SetParent(parent, true);
        quad.transform.position = center;
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (drift > 0f) quad.AddComponent<CloudDrift>().Configure(drift, 1f);
    }

    // Glowing moon above the back building, and a soft cool rim light from
    // its direction so edges and the wet street catch some moonlight.
    static void EnsureMoon()
    {
        const int size = 256;
        Texture2D moon = new Texture2D(size, size, TextureFormat.RGB24, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float maria = Mathf.PerlinNoise(u * 5f + 3.1f, v * 5f + 1.7f);
                float craters = Mathf.PerlinNoise(u * 22f, v * 22f);
                float shade = 0.92f - 0.28f * Mathf.SmoothStep(0.45f, 0.7f, maria) - 0.08f * craters;
                moon.SetPixel(x, y, new Color(shade, shade, shade * 1.03f));
            }
        }
        File.WriteAllBytes(MoonTexturePath, moon.EncodeToPNG());
        Object.DestroyImmediate(moon);
        AssetDatabase.ImportAsset(MoonTexturePath);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MoonMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_AlleyMoon" };
            AssetDatabase.CreateAsset(material, MoonMaterialPath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MoonTexturePath));
        // HDR above 1 so bloom gives the moon a halo.
        material.SetColor("_BaseColor", new Color(1.25f, 1.27f, 1.38f));
        EditorUtility.SetDirty(material);

        GameObject body = GameObject.Find("Night_Moon");
        if (body == null)
        {
            body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Night_Moon";
            GameObject environment = GameObject.Find("01_Environment_Static");
            if (environment != null) body.transform.SetParent(environment.transform, true);
        }
        body.transform.position = MoonPosition;
        body.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
        body.transform.localScale = Vector3.one * 0.85f;
        Renderer renderer = body.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        GameObject rimObject = GameObject.Find("Moon_Rim");
        if (rimObject == null) rimObject = new GameObject("Moon_Rim");
        rimObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 4f) - MoonPosition);
        Light rim = rimObject.GetComponent<Light>();
        if (rim == null) rim = rimObject.AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = new Color(0.62f, 0.72f, 1f);
        rim.intensity = 0.8f;
        rim.shadows = LightShadows.None;
        rim.renderMode = LightRenderMode.ForcePixel;
    }

    static void EnsureNightSky()
    {
        {
            // Navy gradient with a faint warm city haze at the bottom and a
            // fixed seed starfield that thins out toward the horizon.
            const int width = 1024, height = 416;
            Texture2D starfield = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color top = new Color(0.02f, 0.03f, 0.07f);
            Color mid = new Color(0.05f, 0.06f, 0.12f);
            Color haze = new Color(0.15f, 0.11f, 0.14f);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);
                Color c = t < 0.25f ? Color.Lerp(haze, mid, t / 0.25f) : Color.Lerp(mid, top, (t - 0.25f) / 0.75f);
                for (int x = 0; x < width; x++) pixels[y * width + x] = c;
            }
            var random = new System.Random(7);
            for (int i = 0; i < 6000; i++)
            {
                int x = random.Next(width);
                float t = Mathf.Sqrt((float)random.NextDouble());
                int y = Mathf.Clamp((int)(t * (height - 1)), 0, height - 1);
                if (t < 0.3f) continue;
                float brightness = Mathf.Pow((float)random.NextDouble(), 2.5f) * 0.85f + 0.3f;
                Color star = new Color(brightness, brightness, brightness * 1.08f);
                pixels[y * width + x] = Color.Lerp(pixels[y * width + x], star, 0.9f);
                if (brightness > 0.75f && x + 1 < width && y + 1 < height)
                {
                    pixels[y * width + x + 1] = Color.Lerp(pixels[y * width + x + 1], star, 0.45f);
                    pixels[(y + 1) * width + x] = Color.Lerp(pixels[(y + 1) * width + x], star, 0.45f);
                }
            }
            starfield.SetPixels(pixels);
            File.WriteAllBytes(NightSkyPath, starfield.EncodeToPNG());
            Object.DestroyImmediate(starfield);
            AssetDatabase.ImportAsset(NightSkyPath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(NightSkyPath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(NightSkyMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_AlleyNightSky" };
            AssetDatabase.CreateAsset(material, NightSkyMaterialPath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NightSkyPath));
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);

        GameObject sky = GameObject.Find("Night_Sky_Backdrop");
        if (sky == null)
        {
            sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(sky.GetComponent<Collider>());
            sky.name = "Night_Sky_Backdrop";
            GameObject environment = GameObject.Find("01_Environment_Static");
            if (environment != null) sky.transform.SetParent(environment.transform, true);
        }
        // Bottom edge sits behind the wall top so the warm glow reads as city haze.
        sky.transform.position = new Vector3(0f, 10f, 16f);
        sky.transform.rotation = Quaternion.identity;
        sky.transform.localScale = new Vector3(40f, 16f, 1f);
        Renderer renderer = sky.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static void EnsureNightVolume()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(NightProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, NightProfilePath);
        }

        Tonemapping tonemapping = ProfileComponent<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);
        Bloom bloom = ProfileComponent<Bloom>(profile);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.65f);
        bloom.tint.Override(new Color(1f, 0.85f, 0.7f));
        ColorAdjustments adjustments = ProfileComponent<ColorAdjustments>(profile);
        adjustments.postExposure.Override(0.75f);
        adjustments.contrast.Override(14f);
        adjustments.saturation.Override(10f);
        Vignette vignette = ProfileComponent<Vignette>(profile);
        vignette.intensity.Override(0.3f);
        vignette.smoothness.Override(0.45f);
        EditorUtility.SetDirty(profile);

        GameObject volumeObject = GameObject.Find("Night_Volume");
        if (volumeObject == null) volumeObject = new GameObject("Night_Volume");
        Volume volume = volumeObject.GetComponent<Volume>();
        if (volume == null) volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    static T ProfileComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component;
        if (profile.TryGet(out component)) return component;
        component = profile.Add<T>(false);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    static void WhitenAlleyBaseColors()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new System.InvalidOperationException("URP Lit shader is missing");
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) continue;
            if (material.shader != lit) material.shader = lit;
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
        }
    }

    static void SnapXZKeepY(GameObject go, float x, float z)
    {
        if (go == null) return;
        Vector3 position = go.transform.position;
        position.x = x;
        position.z = z;
        go.transform.position = position;
    }

    static void CaptureAlley()
    {
        CaptureAlley("/tmp/juicy-alley-light.png");
    }

    static void CaptureAlley(string path)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        RenderTexture previous = camera.targetTexture;
        RenderTexture buffer = new RenderTexture(1080, 1920, 24);
        Texture2D image = new Texture2D(buffer.width, buffer.height, TextureFormat.RGB24, false);
        camera.targetTexture = buffer;
        camera.Render();
        RenderTexture.active = buffer;
        image.ReadPixels(new Rect(0f, 0f, buffer.width, buffer.height), 0, 0);
        image.Apply();
        camera.targetTexture = previous;
        RenderTexture.active = null;
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(buffer);
    }

    static void ConfigureRaccoonImport()
    {
        string[] paths =
        {
            IdlePath,
            Raccoon + "Raccoon_Climb.fbx",
            Raccoon + "Raccoon_Climb_Down.fbx",
            Raccoon + "Raccoon_Push_Light.fbx",
            Raccoon + "Raccoon_Push_Heavy.fbx",
        };
        for (int i = 0; i < paths.Length; i++)
        {
            ModelImporter importer = AssetImporter.GetAtPath(paths[i]) as ModelImporter;
            if (importer == null) continue;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.SaveAndReimport();
            Avatar avatar = FindAvatar(paths[i]);
            if (avatar != null && avatar.isValid && avatar.isHuman) continue;
            importer = AssetImporter.GetAtPath(paths[i]) as ModelImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            Debug.LogWarning("Humanoid map failed for " + paths[i] + ". Left on Generic.");
        }
    }

    static AnimatorController BuildController()
    {
        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("isIdle", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isClimbing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("onPushLight", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("onPushHeavy", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("onClimbDown", AnimatorControllerParameterType.Trigger);
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name != "isIdle") continue;
            parameters[i].defaultBool = true;
        }
        controller.parameters = parameters;

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = AddState(machine, "Torch_Idle", LoadClip(IdlePath), new Vector3(280f, 0f, 0f));
        AnimatorState climb = AddState(machine, "Climb", LoadClip(Raccoon + "Raccoon_Climb.fbx"), new Vector3(520f, 80f, 0f));
        AnimatorState climbDown = AddState(machine, "Climb_Down", LoadClip(Raccoon + "Raccoon_Climb_Down.fbx"), new Vector3(520f, 160f, 0f));
        AnimatorState pushLight = AddState(machine, "Push_Light", LoadClip(Raccoon + "Raccoon_Push_Light.fbx"), new Vector3(280f, 140f, 0f));
        AnimatorState pushHeavy = AddState(machine, "Push_Heavy", LoadClip(Raccoon + "Raccoon_Push_Heavy.fbx"), new Vector3(40f, 140f, 0f));
        machine.defaultState = idle;

        BoolTransition(idle, climb, "isClimbing", true);
        BoolTransition(climb, idle, "isClimbing", false);
        TriggerFromAny(machine, pushLight, "onPushLight");
        TriggerFromAny(machine, pushHeavy, "onPushHeavy");
        TriggerFromAny(machine, climbDown, "onClimbDown");
        ExitAfter(pushLight, idle);
        ExitAfter(pushHeavy, idle);
        ExitAfter(climbDown, idle);
        return controller;
    }

    static void DressRaccoon(GameObject raccoon, RuntimeAnimatorController controller)
    {
        if (raccoon == null) return;
        Animator animator = raccoon.GetComponentInChildren<Animator>();
        if (animator == null) animator = raccoon.AddComponent<Animator>();
        Avatar avatar = FindAvatar(IdlePath);
        if (avatar != null) animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Transform hand = FindNamed(raccoon.transform, "RightHand");
        GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "SM_MedievalTorch.fbx");
        if (hand == null || torchPrefab == null)
        {
            Debug.LogWarning("Torch was not parented. RightHand or the torch model is missing.");
            return;
        }

        GameObject torch = (GameObject)PrefabUtility.InstantiatePrefab(torchPrefab, hand);
        torch.name = "Torch";
        torch.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        torch.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        torch.transform.localScale = Vector3.one;
        Renderer torchRenderer = torch.GetComponentInChildren<Renderer>();
        if (torchRenderer != null)
        {
            float height = torchRenderer.bounds.size.y;
            float scale = 0.42f / Mathf.Max(height, 0.001f);
            torch.transform.localScale = Vector3.one * scale;
            torchRenderer = torch.GetComponentInChildren<Renderer>();
            Vector3 flame = torchRenderer.bounds.center + Vector3.up * torchRenderer.bounds.extents.y;
            GameObject lightObject = new GameObject("Torch_Light");
            lightObject.transform.SetParent(torch.transform, true);
            lightObject.transform.position = flame;
            Light point = lightObject.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = new Color(1f, 0.55f, 0.22f);
            point.intensity = 1.6f;
            point.range = 5.5f;
            point.shadows = LightShadows.None;
            lightObject.AddComponent<TorchFlicker>();
        }
    }

    // The invisible chutes: items fall from these four points.
    static void BuildDrops(Transform parent)
    {
        GameObject top = Empty("Spawner_Top_Catwalk", parent, Vector3.zero);
        for (int i = 0; i < ColumnX.Length; i++)
            Empty("Drop_0" + (i + 1), top.transform, new Vector3(ColumnX[i], DropY, ColumnZ));
    }

    // The alley floor: textured wet asphalt, concrete curbs with wet gutters
    // along both walls, worn yellow edge lines, and a steaming manhole. The
    // textures and the manhole come from ArtSource/Alley/build_alley_road.py.
    const string AsphaltAlbedo = TextureRoot + "T_AlleyAsphalt_Albedo.png";
    const string AsphaltNormal = TextureRoot + "T_AlleyAsphalt_Normal.png";
    const string AsphaltMask = TextureRoot + "T_AlleyAsphalt_Mask.png";
    const string PaintLineTexture = TextureRoot + "T_AlleyPaintLine.png";
    const string ManholeModel = Art + "SM_Manhole.fbx";
    const float RoadWidth = 7.4f;
    const float RoadStartZ = -3.9f;
    const float RoadEndZ = 13.6f;
    const float AsphaltTile = 2.4f;
    static readonly Vector3 ManholeSpot = new Vector3(0.15f, -0.01f, 3f);

    static void BuildRoad(Transform parent)
    {
        string[] old = { "Street_Pavement_Strip", "Alley_Road" };
        for (int i = 0; i < old.Length; i++)
        {
            GameObject found = GameObject.Find(old[i]);
            if (found != null) Object.DestroyImmediate(found);
        }
        if (File.Exists(Art + "M_AlleyPavement.mat")) AssetDatabase.DeleteAsset(Art + "M_AlleyPavement.mat");

        Transform road = Empty("Alley_Road", parent, Vector3.zero).transform;
        float length = RoadEndZ - RoadStartZ;
        float middle = (RoadStartZ + RoadEndZ) * 0.5f;

        Material asphalt = AsphaltMaterial();
        asphalt.SetTextureScale("_BaseMap", new Vector2(RoadWidth / AsphaltTile, length / AsphaltTile));
        FlatQuad("Asphalt", road, new Vector3(0f, 0f, middle), new Vector2(RoadWidth, length), asphalt);

        Material curb = LitMaterial(MaterialRoot + "/M_AlleyCurb.mat", "M_AlleyCurb", new Color(0.3f, 0.3f, 0.31f), 0f, 0.22f);
        Material gutter = LitMaterial(MaterialRoot + "/M_AlleyGutter.mat", "M_AlleyGutter", new Color(0.04f, 0.045f, 0.05f), 0f, 0.88f);
        Material paint = PaintMaterial();
        float lineLength = 13f;
        paint.SetTextureScale("_BaseMap", new Vector2(1f, lineLength / 2.5f));
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject curbBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(curbBox.GetComponent<Collider>());
            curbBox.name = side < 0 ? "Curb_Left" : "Curb_Right";
            curbBox.transform.SetParent(road, false);
            curbBox.transform.position = new Vector3(side * 1.8f, 0.05f, middle);
            curbBox.transform.localScale = new Vector3(0.22f, 0.1f, length);
            curbBox.GetComponent<Renderer>().sharedMaterial = curb;

            FlatQuad(side < 0 ? "Gutter_Left" : "Gutter_Right", road, new Vector3(side * 1.58f, 0.003f, middle), new Vector2(0.22f, length), gutter);
            FlatQuad(side < 0 ? "Line_Left" : "Line_Right", road, new Vector3(side * 1.36f, 0.005f, -1.5f + lineLength * 0.5f), new Vector2(0.09f, lineLength), paint);
        }

        RemapMaterials(ManholeModel, new Dictionary<string, Material>
        {
            { "M_CastIron", LitMaterial(MaterialRoot + "/M_ManholeIron.mat", "M_ManholeIron", new Color(0.13f, 0.13f, 0.14f), 0.55f, 0.5f) },
            { "M_Rust", LitMaterial(MaterialRoot + "/M_ManholeRust.mat", "M_ManholeRust", new Color(0.28f, 0.14f, 0.07f), 0.15f, 0.25f) },
        });
        GameObject manhole = PlaceWide(ManholeModel, "Manhole", road, ManholeSpot, 0.9f, 0f);
        Steam(manhole.transform);
    }

    static GameObject FlatQuad(string name, Transform parent, Vector3 center, Vector2 size, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.position = center;
        // A quad faces -Z; 90 degrees about X turns it face up with its Y along the alley.
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = material;
        return quad;
    }

    static Material AsphaltMaterial()
    {
        ImportTexture(AsphaltAlbedo, TextureImporterType.Default, true);
        ImportTexture(AsphaltNormal, TextureImporterType.NormalMap, false);
        ImportTexture(AsphaltMask, TextureImporterType.Default, false);
        Material material = LitMaterial(MaterialRoot + "/M_AlleyAsphalt.mat", "M_AlleyAsphalt", Color.white, 0f, 1f);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AsphaltAlbedo));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AsphaltNormal));
        material.SetFloat("_BumpScale", 1f);
        material.EnableKeyword("_NORMALMAP");
        // Mask alpha is smoothness: puddles and oil read wet, cracks stay dry.
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AsphaltMask));
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        return material;
    }

    static Material PaintMaterial()
    {
        ImportTexture(PaintLineTexture, TextureImporterType.Default, true);
        Material material = LitMaterial(MaterialRoot + "/M_AlleyPaintLine.mat", "M_AlleyPaintLine", Color.white, 0f, 0.35f);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PaintLineTexture));
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.45f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        return material;
    }

    static void ImportTexture(string path, TextureImporterType type, bool srgb)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType == type && importer.sRGBTexture == srgb && importer.wrapMode == TextureWrapMode.Repeat) return;
        importer.textureType = type;
        importer.sRGBTexture = srgb;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.alphaIsTransparency = srgb;
        importer.SaveAndReimport();
    }

    // Soft steam rising from the manhole.
    static void Steam(Transform manhole)
    {
        Bounds b;
        TryBounds(manhole.gameObject, out b);
        GameObject steamObject = new GameObject("Steam");
        steamObject.transform.SetParent(manhole, true);
        steamObject.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
        steamObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem steam = steamObject.AddComponent<ParticleSystem>();
        var main = steam.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.8f, 0.84f, 0.92f, 0.32f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 60;
        var emission = steam.emission;
        emission.rateOverTime = 9f;
        var shape = steam.shape;
        // A narrow cone emits along its local Z, which the -90 X rotation points up.
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.2f;
        var size = steam.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
        var color = steam.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        var noise = steam.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.6f;

        var renderer = steamObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = SteamMaterial();
        renderer.sortingFudge = 10f;
    }

    static Material SteamMaterial()
    {
        return TransparentMaterial(Art + "Materials/M_ManholeSteam.mat", "M_ManholeSteam", "Universal Render Pipeline/Particles/Unlit",
            AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"), Color.white);
    }

    static Material TransparentMaterial(string path, string name, string shader, Texture texture, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetupCamera(Transform parent)
    {
        Camera camera = Object.FindAnyObjectByType<Camera>();
        if (camera == null) return;
        camera.name = "Main_Camera";
        camera.transform.SetParent(parent, true);
        camera.orthographic = false;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 80f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = NavyClear;
        ApplyLens(camera);
    }

    // Wide: 45 degrees from 5m, so walls running down the alley show in
    // perspective. Telephoto: 15 degrees from 16m, nearly flat. Both frame
    // about 2.4m across at the front of the street, where the columns are.
    const string WideLensPref = "Juicy.Alley.WideLens";
    static bool WideLens { get { return EditorPrefs.GetBool(WideLensPref, true); } }

    static void ApplyLens(Camera camera)
    {
        if (WideLens)
        {
            camera.fieldOfView = 45f;
            camera.transform.position = new Vector3(0f, 2.3f, -5f);
            camera.transform.LookAt(new Vector3(0f, 1.1f, 4f));
        }
        else
        {
            camera.fieldOfView = 15f;
            camera.transform.position = new Vector3(0f, 3.15f, -16.2f);
            camera.transform.LookAt(new Vector3(0f, 1.45f, 2.4f));
        }
    }

    [MenuItem("Juicy/Alley/Lens/Use Wide Alley")]
    static void UseWide() { SwitchLens(true); }
    [MenuItem("Juicy/Alley/Lens/Use Telephoto")]
    static void UseTelephoto() { SwitchLens(false); }

    static void SwitchLens(bool wide)
    {
        EditorPrefs.SetBool(WideLensPref, wide);
        Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (camera != null) ApplyLens(camera);
        AlignColumns();
        LayoutAlleyInScene();
    }

    // Renders both lenses, then leaves the scene on the wide alley.
    public static void RenderLensOptions()
    {
        SwitchLens(false);
        File.Copy("/tmp/juicy-alley-light.png", "/tmp/juicy-lens-telephoto.png", true);
        SwitchLens(true);
        File.Copy("/tmp/juicy-alley-light.png", "/tmp/juicy-lens-wide.png", true);
    }

    static void TuneMoon()
    {
        Light light = FindKeyLight();
        if (light == null) return;
        light.type = LightType.Directional;
        light.name = "Night_Directional";
        light.color = Color.white;
        light.intensity = KeyIntensity;
        light.shadows = LightShadows.Hard;
        light.shadowStrength = 0.92f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.sun = light;
    }

    static GameObject Place(string path, string name, Transform parent, Vector3 bottomCenter, Vector3 worldSize, float yaw)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("Missing alley model " + path);
            return Empty(name, parent, bottomCenter);
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one;
        Renderer renderer = go.GetComponentInChildren<Renderer>();
        if (renderer == null) return go;
        Vector3 size = renderer.bounds.size;
        go.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(size.x, 0.001f),
            worldSize.y / Mathf.Max(size.y, 0.001f),
            worldSize.z / Mathf.Max(size.z, 0.001f));
        SnapBottom(go, bottomCenter);
        return go;
    }

    static GameObject PlaceUniform(string path, string name, Transform parent, Vector3 bottomCenter, float targetHeight, float yaw)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("Missing alley model " + path);
            return Empty(name, parent, bottomCenter);
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one;
        Renderer renderer = go.GetComponentInChildren<Renderer>();
        if (renderer == null) return go;
        float scale = targetHeight / Mathf.Max(renderer.bounds.size.y, 0.001f);
        go.transform.localScale = Vector3.one * scale;
        SnapBottom(go, bottomCenter);
        return go;
    }

    static bool TryBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        if (go == null) return false;
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    static void SnapBottom(GameObject go, Vector3 bottomCenter)
    {
        // Whole model bounds, so a held prop like the raccoon torch does not
        // stand in for the body.
        Bounds bounds;
        if (!TryBounds(go, out bounds))
        {
            go.transform.position = bottomCenter;
            return;
        }
        Vector3 position = go.transform.position;
        position.x += bottomCenter.x - bounds.center.x;
        position.y += bottomCenter.y - bounds.min.y;
        position.z += bottomCenter.z - bounds.center.z;
        go.transform.position = position;
    }

    static GameObject Group(string name)
    {
        GameObject go = new GameObject(name);
        return go;
    }

    static GameObject Empty(string name, Transform parent, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        return go;
    }

    static void ParentKeepWorld(GameObject child, Transform parent)
    {
        if (child == null) return;
        child.transform.SetParent(parent, true);
    }

    static AnimatorState AddState(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = clip;
        return state;
    }

    static void BoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.12f;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    static void TriggerFromAny(AnimatorStateMachine machine, AnimatorState to, string parameter)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
    }

    static void ExitAfter(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.92f;
        transition.duration = 0.1f;
    }

    static AnimationClip LoadClip(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null) continue;
            if (clip.name.StartsWith("__preview")) continue;
            return clip;
        }
        Debug.LogWarning("No clip in " + path);
        return null;
    }

    static Avatar FindAvatar(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
    }

    static Transform FindNamed(Transform root, string token)
    {
        if (root.name.Contains(token)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamed(root.GetChild(i), token);
            if (found != null) return found;
        }
        return null;
    }
}

[InitializeOnLoad]
public static class AlleyDressHook
{
    const string Flag = "/tmp/juicy-alley-dress";
    const string LogPath = "/tmp/juicy-alley-dress.txt";
    const string LightFlag = "/tmp/juicy-alley-light";
    const string LightLog = "/tmp/juicy-alley-light.txt";
    const string ExposeFlag = "/tmp/juicy-alley-expose";
    const string ExposeLog = "/tmp/juicy-alley-expose.txt";
    const string StructureFlag = "/tmp/juicy-alley-structure";
    const string StructureLog = "/tmp/juicy-alley-structure.txt";

    static bool queued;
    static bool lightQueued;
    static bool exposeQueued;
    static bool structureQueued;

    static AlleyDressHook()
    {
        EditorApplication.update += Watch;
    }

    const string GrittyFlag = "/tmp/juicy-alley-gritty";
    const string GrittyLog = "/tmp/juicy-alley-gritty.txt";
    static bool grittyQueued;

    static void RunGritty()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!grittyQueued)
        {
            grittyQueued = true;
            return;
        }

        grittyQueued = false;
        try
        {
            AlleySceneBuilder.ApplyGrittyNight();
            File.WriteAllText(GrittyLog, "gritty\n");
        }
        catch (System.Exception error)
        {
            File.WriteAllText(GrittyLog, error.ToString());
        }

        if (File.Exists(GrittyFlag)) File.Delete(GrittyFlag);
    }

    const string LayoutFlag = "/tmp/juicy-alley-layout";
    const string LayoutLog = "/tmp/juicy-alley-layout.txt";
    static bool layoutQueued;

    static void RunLayout()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!layoutQueued)
        {
            layoutQueued = true;
            return;
        }
        layoutQueued = false;
        try
        {
            AlleySceneBuilder.LayoutAlleyInScene();
            File.WriteAllText(LayoutLog, "layout\n");
        }
        catch (System.Exception error)
        {
            File.WriteAllText(LayoutLog, error.ToString());
        }
        if (File.Exists(LayoutFlag)) File.Delete(LayoutFlag);
    }

    static void Watch()
    {
        if (File.Exists(LayoutFlag))
        {
            RunLayout();
            return;
        }

        if (File.Exists(GrittyFlag))
        {
            RunGritty();
            return;
        }

        if (File.Exists(StructureFlag))
        {
            RunStructure();
            return;
        }

        if (File.Exists(ExposeFlag))
        {
            RunExpose();
            return;
        }

        if (File.Exists(WireFlag))
        {
            RunWire();
            return;
        }

        if (File.Exists(LightFlag))
        {
            RunLight();
            return;
        }

        if (!File.Exists(Flag))
        {
            queued = false;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!queued)
        {
            queued = true;
            return;
        }

        queued = false;
        try
        {
            AlleySceneBuilder.DressSpecGaps();
            File.WriteAllText(LogPath, "dressed\n");
        }
        catch (System.Exception error)
        {
            File.WriteAllText(LogPath, error.ToString());
        }

        if (File.Exists(Flag)) File.Delete(Flag);
    }

    static void RunLight()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!lightQueued)
        {
            lightQueued = true;
            return;
        }

        lightQueued = false;
        try
        {
            AlleySceneBuilder.ApplyLightingAndLanes();
            File.WriteAllText(LightLog, "applied\n");
        }
        catch (System.Exception error)
        {
            File.WriteAllText(LightLog, error.ToString());
        }

        if (File.Exists(LightFlag)) File.Delete(LightFlag);
    }

    const string WireFlag = "/tmp/juicy-alley-wire";
    const string WireLog = "/tmp/juicy-alley-wire.txt";
    static bool wireQueued;

    static void RunWire()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!wireQueued)
        {
            wireQueued = true;
            return;
        }
        wireQueued = false;
        try
        {
            AlleySceneBuilder.WireGameplayInScene();
            File.WriteAllText(WireLog, "wired\n");
        }
        catch (System.Exception error)
        {
            File.WriteAllText(WireLog, error.ToString());
        }
        if (File.Exists(WireFlag)) File.Delete(WireFlag);
    }

    static void RunExpose()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!exposeQueued)
        {
            exposeQueued = true;
            return;
        }

        exposeQueued = false;
        try
        {
            AlleySceneBuilder.ApplyExposureBaseline();
            File.WriteAllText(ExposeLog, "exposed\n");
        }
        catch (System.Exception error)
        {
            File.WriteAllText(ExposeLog, error.ToString());
        }

        if (File.Exists(ExposeFlag)) File.Delete(ExposeFlag);
    }

    static void RunStructure()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!structureQueued)
        {
            structureQueued = true;
            return;
        }

        structureQueued = false;
        try
        {
            AlleySceneBuilder.ApplyStructurePop();
            File.WriteAllText(StructureLog, "popped\n");
        }
        catch (System.Exception error)
        {
            File.WriteAllText(StructureLog, error.ToString());
        }

        if (File.Exists(StructureFlag)) File.Delete(StructureFlag);
    }
}
