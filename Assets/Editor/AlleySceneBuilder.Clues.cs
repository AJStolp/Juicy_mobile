using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The clue trip: the spots the raccoon visits in turn, the marks painted
// there, the dynamite blast and the torch's UV light, and the three ways a
// clue can be found (the pick is Juicy/Alley/Clues). Every mark sits where the
// sort camera sees it, so found clues stay readable during the next legs.
public static partial class AlleySceneBuilder
{
    const string ClueFindPref = "Juicy.Alley.ClueFind";
    const string ClueGrimeMaterialPath = MaterialRoot + "/M_ClueGrime.mat";
    const string MenuWallRub = "Juicy/Alley/Clues/Option 1 Wall Rub";
    const string MenuTorch = "Juicy/Alley/Clues/Option 2 Torch Reveal";
    const string MenuUnderProp = "Juicy/Alley/Clues/Option 3 Under The Prop";

    static ClueFind SavedClueFind => (ClueFind)EditorPrefs.GetInt(ClueFindPref, (int)ClueFind.WallRub);

    // Mud over a clue, lit like the alley: alpha clipped, so a cutoff wipes
    // the patch away.
    static Material ClueGrimeMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ClueGrimeMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_ClueGrime" };
            AssetDatabase.CreateAsset(material, ClueGrimeMaterialPath);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", new Color(0.22f, 0.18f, 0.13f));
        material.SetFloat("_Smoothness", 0.1f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetFloat("_Cull", 0f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(material);
        return material;
    }

    static AlleyClueTrip BuildClueTrip(Transform parent, AlleyChuteDirector director, AlleyVaultDoor vaultDoor, AlleyCameraRig rig, GameObject raccoon)
    {
        if (raccoon == null) return null;
        StandOnSpot stand = raccoon.GetComponent<StandOnSpot>();
        Vector3 home = RaccoonSpot();
        GameObject root = Empty("Clue_Trip", parent, Vector3.zero);
        Material grime = ClueGrimeMaterial();
        int seed = 1;

        // Wall rub and torch: faces of things that look at the sort camera,
        // at hand height. The two near props first, then the building front
        // right of the vault door and the getaway car's side.
        var walls = new List<ClueSpot>();
        GameObject facade = GameObject.Find("Vault_Facade");
        Bounds fb, door;
        float doorRight = TryBounds(vaultDoor != null ? vaultDoor.gameObject : null, out door) ? door.max.x : 0.84f;
        TryBounds(facade, out fb);
        Vector3 center = new Vector3(home.x, home.y, 8.9f);
        AddWallSpot(walls, root.transform, grime, ref seed, "Dumpster", new Vector3(-1.4f, 0.42f, 0f), 0.5f, home, null);
        AddWallSpot(walls, root.transform, grime, ref seed, "Heavy_Crates", new Vector3(1.5f, 0.34f, 0f), 0.4f, home, null);
        AddWallSpot(walls, root.transform, grime, ref seed, "Vault_Facade", new Vector3((doorRight + fb.max.x) * 0.5f, 0.5f, 0f), 0.5f, home, new[] { center });
        AddWallSpot(walls, root.transform, grime, ref seed, "Getaway_Car", new Vector3(1.0f, 0.36f, 0f), 0.4f, home, null);

        // Under the prop: painted on the road where the prop stood, uncovered
        // when it slides off. Each slide moves the prop away from the camera
        // or to the side, so it never stands between the camera and the mark.
        var props = new List<ClueSpot>();
        AddGroundSpot(props, root.transform, grime, ref seed, "Dumpster", 1.2f, new Vector3(0f, 0f, 1.2f), home);
        AddGroundSpot(props, root.transform, grime, ref seed, "Heavy_Crates", 0.7f, new Vector3(0f, 0f, 0.8f), home);
        AddGroundSpot(props, root.transform, grime, ref seed, "Trash_Bag_03", 0.65f, new Vector3(-0.1f, 0f, 0.7f), home);
        AddGroundSpot(props, root.transform, grime, ref seed, "Trash_Bag_02", 0.65f, new Vector3(0.55f, 0f, 0.25f), home);

        ParticleSystem burst = Burst(root.transform);
        Light flash = PointLight("Clue_Blast_Light", root.transform, new Color(1f, 0.6f, 0.25f), 5f);
        Light uv = PointLight("Clue_UV_Light", root.transform, new Color(0.55f, 0.2f, 1f), 2.2f);
        TorchGrip torch = raccoon.GetComponentInChildren<TorchGrip>();

        AlleyClueTrip trip = root.AddComponent<AlleyClueTrip>();
        trip.Configure(director, vaultDoor, rig, raccoon.GetComponentInChildren<Animator>(), stand, torch != null ? torch.transform : null,
            walls.ToArray(), props.ToArray(), burst, flash, uv);
        ApplyClueFind(trip, SavedClueFind);
        EditorUtility.SetDirty(root);
        return trip;
    }

    static Light PointLight(string name, Transform parent, Color color, float range)
    {
        Light light = Empty(name, parent, Vector3.zero).AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        return light;
    }

    // A mark on the face of a prop or wall, found by a ray from the sort
    // camera, so it lies on the surface the camera sees. He stands in front
    // of it, facing it.
    static void AddWallSpot(List<ClueSpot> spots, Transform parent, Material grime, ref int seed, string propName, Vector3 at, float size, Vector3 home, Vector3[] via)
    {
        GameObject prop = GameObject.Find(propName);
        Bounds b;
        if (prop == null || !TryBounds(prop, out b)) return;
        Camera camera = Camera.main;
        Vector3 guess = new Vector3(at.x, at.y, b.center.z);
        Vector3 origin = camera.transform.position;
        if (!SurfaceHit(prop, origin, (guess - origin).normalized, out RaycastHit hit)) return;
        Vector3 normal = hit.normal;
        normal.y = 0f;
        normal = normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.back;
        // +Z into the surface: text reads from behind its +Z.
        Quaternion rotation = Quaternion.LookRotation(-normal, Vector3.up);
        AlleyClueMark mark = BuildMark("Clue_Mark_" + propName, parent, hit.point, rotation, size, grime, seed++, 0.04f);
        Vector3 standAt = hit.point + normal * 0.5f;
        standAt = new Vector3(Mathf.Clamp(standAt.x, -1.45f, 1.45f), home.y, standAt.z);
        spots.Add(new ClueSpot { Prop = prop.transform, Via = via ?? new Vector3[0], Stand = standAt, Mark = mark, Slide = Vector3.zero });
    }

    // A mark painted flat on the road under a prop, top of the glyph away
    // from the camera so it reads the right way up.
    static void AddGroundSpot(List<ClueSpot> spots, Transform parent, Material grime, ref int seed, string propName, float size, Vector3 slide, Vector3 home)
    {
        GameObject prop = GameObject.Find(propName);
        GameObject road = GameObject.Find("Alley_Road");
        Bounds b;
        if (prop == null || road == null || !TryBounds(prop, out b)) return;
        // Inside the frame: the crates reach past the right edge of the view.
        float x = Mathf.Clamp(b.center.x, -1.5f, 1.45f);
        Vector3 top = new Vector3(x, 3f, b.center.z);
        Vector3 point = SurfaceHit(road, top, Vector3.down, out RaycastHit hit) ? hit.point : new Vector3(x, 0.02f, b.center.z);
        Quaternion rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        AlleyClueMark mark = BuildMark("Clue_Mark_Under_" + propName, parent, point, rotation, size, grime, seed++, 0.03f);
        Vector3 standAt = new Vector3(Mathf.Clamp(b.center.x, -1.45f, 1.45f), home.y, b.min.z - 0.42f);
        spots.Add(new ClueSpot { Prop = prop.transform, Via = new Vector3[0], Stand = standAt, Mark = mark, Slide = slide });
    }

    static bool SurfaceHit(GameObject target, Vector3 origin, Vector3 direction, out RaycastHit best)
    {
        best = default;
        var added = new List<MeshCollider>();
        foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>())
        {
            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            added.Add(collider);
        }
        Physics.SyncTransforms();
        bool found = false;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, 100f))
        {
            if (!added.Contains(hit.collider as MeshCollider)) continue;
            if (found && hit.distance >= best.distance) continue;
            best = hit;
            found = true;
        }
        foreach (MeshCollider collider in added) Object.DestroyImmediate(collider);
        return found;
    }

    // The glyph, its grime and its glow, lifted just off the surface.
    static AlleyClueMark BuildMark(string name, Transform parent, Vector3 point, Quaternion rotation, float size, Material grime, int seed, float lift)
    {
        GameObject root = Empty(name, parent, point);
        root.transform.rotation = rotation;
        root.transform.localScale = Vector3.one * size;

        GameObject glyphObject = new GameObject("Glyph");
        glyphObject.transform.SetParent(root.transform, false);
        glyphObject.transform.localPosition = new Vector3(0f, 0f, -lift / size);
        TextMeshPro text = glyphObject.AddComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = new Vector2(1f, 1f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.05f;
        text.fontSizeMax = 20f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.text = "?";
        glyphObject.SetActive(false);

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.name = "Grime";
        quad.transform.SetParent(root.transform, false);
        quad.transform.localPosition = new Vector3(0f, 0f, -lift * 1.6f / size);
        quad.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = grime;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        quad.SetActive(false);

        Light glow = PointLight("Glow", root.transform, Color.white, 1f);
        glow.transform.localPosition = new Vector3(0f, 0f, -1.2f);
        glow.enabled = false;

        AlleyClueMark mark = root.AddComponent<AlleyClueMark>();
        mark.Configure(text, renderer, glow, seed);
        return mark;
    }

    // Edit mode: the scene keeps the pick. Play mode: switches live.
    static void ApplyClueFind(AlleyClueTrip trip, ClueFind how)
    {
        if (trip == null) return;
        if (Application.isPlaying)
        {
            trip.Find = how;
            return;
        }
        var serialized = new SerializedObject(trip);
        serialized.FindProperty("find").enumValueIndex = (int)how;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetClueFind(ClueFind how)
    {
        EditorPrefs.SetInt(ClueFindPref, (int)how);
        var trip = Object.FindAnyObjectByType<AlleyClueTrip>();
        ApplyClueFind(trip, how);
        if (Application.isPlaying || trip == null) return;
        EditorUtility.SetDirty(trip);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(trip.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(trip.gameObject.scene);
    }

    static ClueFind CurrentClueFind()
    {
        var trip = Object.FindAnyObjectByType<AlleyClueTrip>();
        return trip != null ? trip.Find : SavedClueFind;
    }

    [MenuItem(MenuWallRub)]
    static void UseWallRub() => SetClueFind(ClueFind.WallRub);

    [MenuItem(MenuTorch)]
    static void UseTorchReveal() => SetClueFind(ClueFind.TorchReveal);

    [MenuItem(MenuUnderProp)]
    static void UseUnderProp() => SetClueFind(ClueFind.UnderProp);

    [MenuItem(MenuWallRub, true)]
    static bool CheckWallRub()
    {
        Menu.SetChecked(MenuWallRub, CurrentClueFind() == ClueFind.WallRub);
        return true;
    }

    [MenuItem(MenuTorch, true)]
    static bool CheckTorchReveal()
    {
        Menu.SetChecked(MenuTorch, CurrentClueFind() == ClueFind.TorchReveal);
        return true;
    }

    [MenuItem(MenuUnderProp, true)]
    static bool CheckUnderProp()
    {
        Menu.SetChecked(MenuUnderProp, CurrentClueFind() == ClueFind.UnderProp);
        return true;
    }
}
