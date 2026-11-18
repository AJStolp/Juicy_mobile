using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The vault door progress meter and the Plinko board. Both are built from
// ArtSource/Alley (build_vault_door.py, build_plinko_board.py) and wired to
// the chute director's rules.
//
// The vault has three options while Anthony picks one, switched under
// Juicy/Alley/Vault: the code built door over the facade (the baseline), the
// graffiti facade's own door with its wheel, bars and gear moving (A), and the
// facade's door kept still with a lamp per stage on its frame (B). The facade
// parts come from import_glb.py --vault-parts.
public enum VaultDoorOption { CodeDoor, FacadeDoor, FacadeLights }

public static partial class AlleySceneBuilder
{
    const string VaultMenu = "Juicy/Alley/Vault/";
    const string CodeDoorMenu = VaultMenu + "Code Built Door (baseline)";
    const string FacadeDoorMenu = VaultMenu + "Option A Facade Door Moves";
    const string FacadeLightsMenu = VaultMenu + "Option B Facade Stage Lamps";
    // The facade recess is the doorway plus 2mm a side, in model meters.
    const float RecessModelWidth = 0.36f;

    static VaultDoorOption? chosenVaultOption;

    // The scene is the record of which option is in: a rewire keeps the door
    // that is there, and only the menu changes it.
    public static VaultDoorOption VaultOption
    {
        get
        {
            if (chosenVaultOption.HasValue) return chosenVaultOption.Value;
            AlleyVaultDoor door = Object.FindAnyObjectByType<AlleyVaultDoor>(FindObjectsInactive.Include);
            if (door == null) return VaultDoorOption.CodeDoor;
            return door.FacadeLamps ? VaultDoorOption.FacadeLights : door.FacadeSlides ? VaultDoorOption.FacadeDoor : VaultDoorOption.CodeDoor;
        }
    }

    public static void UseVaultOption(VaultDoorOption option)
    {
        chosenVaultOption = option;
        try { WireGameplayInScene(); }
        finally { chosenVaultOption = null; }
    }

    [MenuItem(CodeDoorMenu)] static void UseCodeDoor() { UseVaultOption(VaultDoorOption.CodeDoor); }
    [MenuItem(FacadeDoorMenu)] static void UseFacadeDoor() { UseVaultOption(VaultDoorOption.FacadeDoor); }
    [MenuItem(FacadeLightsMenu)] static void UseFacadeLights() { UseVaultOption(VaultDoorOption.FacadeLights); }
    [MenuItem(CodeDoorMenu, true)] static bool CheckCodeDoor() { return CheckVaultOption(CodeDoorMenu, VaultDoorOption.CodeDoor); }
    [MenuItem(FacadeDoorMenu, true)] static bool CheckFacadeDoor() { return CheckVaultOption(FacadeDoorMenu, VaultDoorOption.FacadeDoor); }
    [MenuItem(FacadeLightsMenu, true)] static bool CheckFacadeLights() { return CheckVaultOption(FacadeLightsMenu, VaultDoorOption.FacadeLights); }

    static bool CheckVaultOption(string menu, VaultDoorOption option)
    {
        Menu.SetChecked(menu, VaultOption == option);
        return true;
    }

    const string VaultDoorModel = Art + "SM_VaultDoor.fbx";
    const string PlinkoBoardModel = Art + "SM_PlinkoBoard.fbx";
    const string PlinkoCoinModel = Art + "SM_PlinkoCoin.fbx";
    const string CoinPhysicsPath = MaterialRoot + "/PM_PlinkoCoin.physicMaterial";
    static readonly string[] DoorParts = { "Bolt_0", "Bolt_1", "Bolt_2", "Bolt_3", "Ring_Outer", "Ring_Inner", "Wheel", "Keyhole" };

    public static void Capture(string path) { CaptureAlley(path); }

    const int WordSignSlots = 6;

    static AlleyWordSign BuildWordSign(Transform parent, AlleyChuteDirector director)
    {
        GameObject facade = GameObject.Find("Vault_Facade");
        Bounds fb;
        TryBounds(facade, out fb);
        // On the balcony railing above the vault door.
        Vector3 position = new Vector3(fb.center.x, fb.max.y - 0.5f, fb.min.z - 0.06f);
        const float tile = 0.3f;

        GameObject root = Empty("Word_Sign", parent, position);
        // Text reads from behind its +Z, so +Z points away from the camera.
        root.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        Material material = LitMaterial(MaterialRoot + "/M_WordTile.mat", "M_WordTile", Color.white, 0.4f, 0.6f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);

        var tiles = new Renderer[WordSignSlots];
        var glyphs = new TextMeshPro[WordSignSlots];
        float step = tile * 1.18f;
        for (int i = 0; i < WordSignSlots; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = "Tile_" + i;
            cube.transform.SetParent(root.transform, false);
            cube.transform.localPosition = new Vector3((i - (WordSignSlots - 1) * 0.5f) * step, 0f, 0f);
            cube.transform.localScale = new Vector3(tile, tile, tile * 0.25f);
            Renderer renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject glyph = new GameObject("Glyph");
            glyph.transform.SetParent(cube.transform, false);
            glyph.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            TextMeshPro text = glyph.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(0.9f, 0.9f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.5f;
            text.fontSizeMax = 12f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.text = "H";
            tiles[i] = renderer;
            glyphs[i] = text;
        }

        AlleyWordSign sign = root.AddComponent<AlleyWordSign>();
        sign.Configure(director, tiles, glyphs);
        sign.Show("HEIST", _ => false);
        return sign;
    }

    static AlleyCameraRig EnsureCameraRig()
    {
        Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (camera == null) return null;
        AlleyCameraRig rig = camera.GetComponent<AlleyCameraRig>();
        return rig != null ? rig : camera.gameObject.AddComponent<AlleyCameraRig>();
    }

    static Dictionary<string, Material> VaultDoorMaterials()
    {
        Material glow = GlowMaterial(MaterialRoot + "/M_VaultKeyGlow.mat", "M_VaultKeyGlow", new Color(0.35f, 1f, 0.45f));
        return new Dictionary<string, Material>
        {
            { "M_VaultSteel", LitMaterial(MaterialRoot + "/M_VaultSteel.mat", "M_VaultSteel", new Color(0.3f, 0.33f, 0.38f), 0.55f, 0.55f) },
            { "M_VaultGold", LitMaterial(MaterialRoot + "/M_VaultGold.mat", "M_VaultGold", new Color(1f, 0.7f, 0.22f), 0.75f, 0.72f) },
            { "M_VaultSilver", LitMaterial(MaterialRoot + "/M_VaultSilver.mat", "M_VaultSilver", new Color(0.82f, 0.84f, 0.88f), 0.7f, 0.82f) },
            { "M_VaultDark", LitMaterial(MaterialRoot + "/M_VaultDark.mat", "M_VaultDark", new Color(0.02f, 0.022f, 0.028f), 0.4f, 0.4f) },
            { "M_VaultKeyGlow", glow },
        };
    }

    static AlleyVaultDoor BuildVaultDoor(Transform parent, AlleyChuteDirector director, AlleyCameraRig rig, VaultDoorOption option)
    {
        switch (option)
        {
            case VaultDoorOption.FacadeDoor: return BuildFacadeVaultDoor(parent, director, rig, false);
            case VaultDoorOption.FacadeLights: return BuildFacadeVaultDoor(parent, director, rig, true);
            default: return BuildCodeVaultDoor(parent, director, rig);
        }
    }

    // Options A and B: the facade's own door. A turns its wheel a quarter per
    // stage, slides a locking bar (then the handle) in for each of the first
    // four stages, turns the gear lock on the fifth and swings the door open on
    // the crack. B leaves the door still and lights a lamp per stage up the
    // frame beside the handle.
    static AlleyVaultDoor BuildFacadeVaultDoor(Transform parent, AlleyChuteDirector director, AlleyCameraRig rig, bool lamps)
    {
        GameObject facade = GameObject.Find("Vault_Facade");
        Transform door = facade != null ? facade.transform.Find("Facade_Door") : null;
        Transform recess = facade != null ? facade.transform.Find("Facade_Recess") : null;
        if (door == null || recess == null)
        {
            Debug.LogWarning("Vault_Facade has no door parts; rebuild SM_GraffitiVaultDoor with import_glb.py --vault-parts");
            return null;
        }
        Transform wheel = door.Find("Facade_Wheel");
        Transform gear = door.Find("Facade_Gear");
        Bounds way = recess.GetComponent<Renderer>().bounds;
        float scale = way.size.x / RecessModelWidth;
        Vector3 doorway = new Vector3(way.center.x, way.center.y, way.min.z);

        GameObject root = Empty("Vault_Door", parent, doorway);
        // A: inside the recess, so it pours out as the door opens. B: in
        // front of the shut door, so the crack still flashes it gold.
        GameObject floodObject = Empty("Vault_Flood", root.transform, lamps ? doorway + Vector3.back * 0.8f : way.center);
        Light flood = floodObject.AddComponent<Light>();
        flood.type = LightType.Point;
        flood.color = new Color(1f, 0.82f, 0.45f);
        flood.range = 7f;
        flood.intensity = 0f;
        flood.shadows = LightShadows.None;

        AlleyVaultDoor component = root.AddComponent<AlleyVaultDoor>();
        if (!lamps)
        {
            var bolts = new[] { door.Find("Facade_Bar_0"), door.Find("Facade_Bar_1"), door.Find("Facade_Bar_2"), door.Find("Facade_Handle") };
            var slide = new Vector3[bolts.Length];
            for (int i = 0; i < 3; i++)
            {
                // Each bar draws in toward the hub, out of its keeper, and
                // sinks a little so it passes under the wheel's spokes.
                Vector3 toHub = wheel.position - bolts[i].position;
                toHub.z = 0f;
                slide[i] = (toHub.normalized * 0.03f + Vector3.forward * 0.004f) * scale;
            }
            slide[3] = Vector3.back * 0.015f * scale;   // the handle pulls out
            component.Configure(director, door, bolts, gear, null, wheel, null, flood, rig, Vector3.back);
            component.ConfigureFacade(slide, 90f, new Renderer[0]);
            return component;
        }

        // Dark glass, so a lamp that has not started reads as off; the door
        // drives its glow.
        Material lampMaterial = LitMaterial(MaterialRoot + "/M_VaultStageLamp.mat", "M_VaultStageLamp", new Color(0.04f, 0.07f, 0.05f), 0f, 0.85f);
        lampMaterial.EnableKeyword("_EMISSION");
        lampMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        lampMaterial.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(lampMaterial);
        // Up the frame on the handle's side, bottom to top.
        float side = Mathf.Sign(gear.position.x - wheel.position.x);
        float x = (side > 0f ? way.max.x : way.min.x) + side * 0.0165f * scale;
        var stageLamps = new Renderer[VaultProgress.Stages];
        for (int k = 0; k < stageLamps.Length; k++)
        {
            float y = way.min.y + (0.05f + k * 0.048f) * scale;
            Vector3 face = SurfacePoint(facade, new Vector3(x, y, way.center.z));
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            lamp.name = "Stage_Lamp_" + k;
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.position = face + Vector3.back * 0.004f * scale;
            lamp.transform.localScale = new Vector3(0.024f, 0.024f, 0.008f) * scale;
            Renderer renderer = lamp.GetComponent<Renderer>();
            renderer.sharedMaterial = lampMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stageLamps[k] = renderer;
        }
        component.Configure(director, null, new Transform[0], null, null, null, null, flood, rig, Vector3.back);
        component.ConfigureFacade(new Vector3[0], 0f, stageLamps);
        return component;
    }

    // The baseline: the code built door mounted over the facade's door.
    static AlleyVaultDoor BuildCodeVaultDoor(Transform parent, AlleyChuteDirector director, AlleyCameraRig rig)
    {
        GameObject facade = GameObject.Find("Vault_Facade");
        Bounds fb;
        if (!TryBounds(facade, out fb)) return null;

        RemapMaterials(VaultDoorModel, VaultDoorMaterials());
        // A big door mounted proud of the graffiti facade, so the bolts and
        // rings read on a phone.
        float width = fb.size.x * 0.56f;
        Vector3 face = SurfacePoint(facade, new Vector3(fb.center.x, fb.min.y + fb.size.y * 0.3f, fb.center.z));
        GameObject vault = PlaceWide(VaultDoorModel, "Vault_Door", parent, new Vector3(fb.center.x, fb.min.y, face.z), width, 180f);
        Bounds vb;
        TryBounds(vault, out vb);
        // Stand the door's back on the facade's surface.
        vault.transform.position += Vector3.forward * (face.z - vb.max.z + vb.size.z * 0.35f);

        PrefabUtility.UnpackPrefabInstance(vault, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Transform door = vault.transform.Find("Door");
        var bolts = new Transform[4];
        foreach (string part in DoorParts)
        {
            Transform t = vault.transform.Find(part);
            t.SetParent(door, true);
            if (part.StartsWith("Bolt_")) bolts[part[5] - '0'] = t;
        }
        Transform keyhole = door.Find("Keyhole");

        GameObject floodObject = Empty("Vault_Flood", vault.transform, keyhole.position + Vector3.forward * 0.6f);
        Light flood = floodObject.AddComponent<Light>();
        flood.type = LightType.Point;
        flood.color = new Color(1f, 0.82f, 0.45f);
        flood.range = 7f;
        flood.intensity = 0f;
        flood.shadows = LightShadows.None;

        AlleyVaultDoor component = vault.AddComponent<AlleyVaultDoor>();
        component.Configure(director, door, bolts, door.Find("Ring_Outer"), door.Find("Ring_Inner"), door.Find("Wheel"),
            keyhole.GetComponent<Renderer>(), flood, rig, Vector3.back);
        return component;
    }

    static Dictionary<string, Material> PlinkoMaterials()
    {
        return new Dictionary<string, Material>
        {
            { "M_PlinkoBack", LitMaterial(MaterialRoot + "/M_PlinkoBack.mat", "M_PlinkoBack", new Color(0.07f, 0.08f, 0.1f), 0.5f, 0.45f) },
            { "M_PlinkoBrass", LitMaterial(MaterialRoot + "/M_PlinkoBrass.mat", "M_PlinkoBrass", new Color(0.95f, 0.66f, 0.25f), 0.75f, 0.7f) },
            { "M_PlinkoPin", LitMaterial(MaterialRoot + "/M_PlinkoPin.mat", "M_PlinkoPin", new Color(0.82f, 0.84f, 0.88f), 0.7f, 0.85f) },
            { "M_PlinkoVaultGlow", GlowMaterial(MaterialRoot + "/M_PlinkoVaultGlow.mat", "M_PlinkoVaultGlow", new Color(0.35f, 1f, 0.45f)) },
        };
    }

    // The board hangs on a hinge at the left alley wall, just short of the
    // vault, folded flat against the bricks. A round swings it out to face the
    // street with the vault still in view to its right.
    static void BuildPlinko(Transform parent, AlleyChuteDirector director, AlleyVaultDoor vaultDoor, AlleyCameraRig rig)
    {
        RemapMaterials(PlinkoBoardModel, PlinkoMaterials());
        RemapMaterials(PlinkoCoinModel, new Dictionary<string, Material>
        {
            { "M_PlinkoCoin", LitMaterial(MaterialRoot + "/M_PlinkoCoin.mat", "M_PlinkoCoin", new Color(1f, 0.75f, 0.25f), 0.8f, 0.8f) },
        });

        GameObject root = Empty("Plinko", parent, Vector3.zero);
        GameObject hinge = Empty("Plinko_Hinge", root.transform, new Vector3(-1.5f, 0.25f, 9.0f));
        hinge.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        GameObject board = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlinkoBoardModel), hinge.transform);
        board.name = "Plinko_Board";
        PrefabUtility.UnpackPrefabInstance(board, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        // Board origin is the back's center; its bottom is 0.81 below and its
        // edge 0.64 to the side. Put that edge on the hinge, back to the wall.
        board.transform.localRotation = Quaternion.identity;
        board.transform.localScale = Vector3.one;
        board.transform.localPosition = new Vector3(-0.64f, 0.81f, 0.04f);

        PhysicsMaterial bounce = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(CoinPhysicsPath);
        if (bounce == null)
        {
            bounce = new PhysicsMaterial("PM_PlinkoCoin");
            AssetDatabase.CreateAsset(bounce, CoinPhysicsPath);
        }
        bounce.bounciness = 0.2f;
        bounce.dynamicFriction = 0.05f;
        bounce.staticFriction = 0.05f;
        bounce.bounceCombine = PhysicsMaterialCombine.Maximum;
        bounce.frictionCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(bounce);

        foreach (Transform child in board.transform)
        {
            if (child.name.StartsWith("Pin_"))
            {
                SphereCollider pin = child.gameObject.AddComponent<SphereCollider>();
                pin.center = Vector3.zero;
                pin.radius = 0.016f;
                pin.sharedMaterial = bounce;
            }
            else if (child.name.StartsWith("Divider_") || child.name.StartsWith("Rail_") || child.name == "Floor")
            {
                child.gameObject.AddComponent<BoxCollider>().sharedMaterial = bounce;
            }
        }
        Transform floor = board.transform.Find("Floor");
        Transform railLeft = board.transform.Find("Rail_L");
        Transform railRight = board.transform.Find("Rail_R");

        GameObject coinObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlinkoCoinModel), root.transform);
        coinObject.name = "Plinko_Coin_Template";
        PrefabUtility.UnpackPrefabInstance(coinObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        coinObject.transform.position = new Vector3(0f, -10f, 0f);
        SphereCollider coinCollider = coinObject.AddComponent<SphereCollider>();
        coinCollider.center = Vector3.zero;
        coinCollider.radius = 0.032f;
        coinCollider.sharedMaterial = bounce;
        coinObject.AddComponent<AlleyPlinkoCoin>();
        Rigidbody coin = coinObject.AddComponent<Rigidbody>();
        coin.mass = 0.02f;
        coin.useGravity = false;
        coin.interpolation = RigidbodyInterpolation.Interpolate;
        coin.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        coin.isKinematic = true;

        // Lay out the camera spot and labels with the board swung open.
        const float OpenYaw = 62f;
        hinge.transform.rotation = Quaternion.Euler(0f, 90f + OpenYaw, 0f);
        Vector3 right = (railRight.position - railLeft.position).normalized;
        Vector3 front = board.transform.forward;
        Vector3 center = floor.position + Vector3.up * 0.72f;
        // Back down the street, clear of the fire escapes and the car, looking
        // up the alley at the board with the vault beside it.
        GameObject view = Empty("Plinko_View", root.transform, center + new Vector3(0.45f, 0.35f, -2.6f));
        // Aimed now, while the board is open; at round start it is still shut.
        view.transform.LookAt(center);
        for (int slot = 0; slot < PlinkoPayout.SlotCount; slot++)
        {
            float x = (slot - (PlinkoPayout.SlotCount - 1) * 0.5f) * 0.12f;
            SlotLabel(board.transform, PlinkoPayout.For(slot), floor.position + right * x + front * 0.05f);
        }
        GameObject lightObject = Empty("Plinko_Light", root.transform, center + front * 0.9f + Vector3.up * 0.6f);
        Light boardLight = lightObject.AddComponent<Light>();
        boardLight.type = LightType.Point;
        boardLight.color = new Color(1f, 0.88f, 0.7f);
        boardLight.range = 3f;
        boardLight.shadows = LightShadows.None;
        hinge.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        // The light rides with the board, so it can't light the wall behind.
        lightObject.transform.SetParent(hinge.transform, true);

        AlleyPlinko plinko = root.AddComponent<AlleyPlinko>();
        plinko.Configure(director, vaultDoor, rig, hinge.transform, board.transform, floor, railLeft, railRight, coin, view.transform, boardLight);
        EditorUtility.SetDirty(root);
    }

    static void SlotLabel(Transform board, PlinkoReward reward, Vector3 position)
    {
        string text;
        Color color = new Color(1f, 0.85f, 0.5f);
        switch (reward.Prize)
        {
            case PlinkoPrize.Vault: text = "VAULT"; color = new Color(0.45f, 1f, 0.55f); break;
            case PlinkoPrize.Gear: text = reward.Gear.ToString().ToUpperInvariant(); color = new Color(1f, 0.55f, 0.35f); break;
            default: text = "$" + reward.Cash.ToString("0"); break;
        }
        GameObject label = new GameObject("Label_" + text);
        label.transform.SetParent(board, false);
        label.transform.position = position;
        // Text reads from behind its +Z, so +Z points into the board.
        label.transform.rotation = Quaternion.LookRotation(-board.forward, Vector3.up);
        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = new Vector2(0.11f, 0.06f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = 1.2f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.text = text;
    }
}
