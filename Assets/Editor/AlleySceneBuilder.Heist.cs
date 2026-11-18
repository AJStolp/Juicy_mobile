using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The vault door progress meter and the Plinko board. The vault is the
// graffiti facade's own door, split into moving parts by import_glb.py
// --vault-parts; the Plinko board is built by build_plinko_board.py. Both are
// wired to the chute director's rules.
public static partial class AlleySceneBuilder
{
    // The facade recess is the doorway plus 2mm a side, in model meters.
    const float RecessModelWidth = 0.36f;
    // The door frame's top edge stands this far above the recess, in model meters.
    const float FrameModelBand = 0.023f;

    const string PlinkoBoardModel = Art + "SM_PlinkoBoard.fbx";
    const string PlinkoCoinModel = Art + "SM_PlinkoCoin.fbx";
    const string CoinPhysicsPath = MaterialRoot + "/PM_PlinkoCoin.physicMaterial";

    public static void Capture(string path) { CaptureAlley(path); }

    const int WordSignSlots = 6;

    static AlleyWordSign BuildWordSign(Transform parent, AlleyChuteDirector director)
    {
        GameObject facade = GameObject.Find("Vault_Facade");
        Bounds fb;
        TryBounds(facade, out fb);
        const float tile = 0.3f;
        // Centered over the vault door, its bottom edge just above the door
        // frame, so it covers none of the door.
        Bounds way = facade.transform.Find("Facade_Recess").GetComponent<Renderer>().bounds;
        float scale = way.size.x / RecessModelWidth;
        float frameTop = way.max.y + FrameModelBand * scale;
        Vector3 position = new Vector3(way.center.x, frameTop + 0.01f * scale + tile * 0.5f, fb.min.z - 0.06f);

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

    // The facade's own door turns its wheel a quarter per stage, slides a
    // locking bar (then the handle) in for each of the first four stages,
    // turns the gear lock on the fifth and swings the door open on the crack.
    static AlleyVaultDoor BuildVaultDoor(Transform parent, AlleyChuteDirector director, AlleyCameraRig rig)
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
        // Inside the recess, so it pours out as the door opens.
        GameObject floodObject = Empty("Vault_Flood", root.transform, way.center);
        Light flood = floodObject.AddComponent<Light>();
        flood.type = LightType.Point;
        flood.color = new Color(1f, 0.82f, 0.45f);
        flood.range = 7f;
        flood.intensity = 0f;
        flood.shadows = LightShadows.None;

        var bars = new[] { door.Find("Facade_Bar_0"), door.Find("Facade_Bar_1"), door.Find("Facade_Bar_2"), door.Find("Facade_Handle") };
        var slide = new Vector3[bars.Length];
        for (int i = 0; i < 3; i++)
        {
            // Each bar draws in toward the hub, out of its keeper, and sinks
            // a little so it passes under the wheel's spokes.
            Vector3 toHub = wheel.position - bars[i].position;
            toHub.z = 0f;
            slide[i] = (toHub.normalized * 0.03f + Vector3.forward * 0.004f) * scale;
        }
        slide[3] = Vector3.back * 0.015f * scale;   // the handle pulls out

        AlleyVaultDoor component = root.AddComponent<AlleyVaultDoor>();
        component.Configure(director, door, bars, slide, gear, wheel, 90f, flood, rig, Vector3.back);
        return component;
    }

    // The hack keypad sits on the building front beside the vault door, at
    // hand height: a steel plate with six symbol keys in two columns and the
    // reward ladder's lamps above them. The hack camera holds square on it,
    // close enough that every key reads on a phone.
    static AlleyHack BuildHack(Transform parent, AlleyChuteDirector director, AlleyVaultDoor vaultDoor, AlleyCameraRig rig)
    {
        GameObject facade = GameObject.Find("Vault_Facade");
        Bounds fb;
        if (!TryBounds(facade, out fb)) return null;
        // Left of the door: the right side has a drainpipe near the wall.
        float doorLeft = fb.center.x - fb.size.x * 0.28f;
        Bounds vb;
        if (vaultDoor != null && TryBounds(vaultDoor.gameObject, out vb)) doorLeft = vb.min.x;
        const float plateWidth = 0.42f;
        const float plateHeight = 0.66f;
        float x = doorLeft - 0.1f - plateWidth * 0.5f;
        // Under the pink lamp the facade mounts beside the door, which would
        // otherwise sit in front of the bottom keys.
        float y = fb.min.y + 0.41f;
        Vector3 face = SurfacePoint(facade, new Vector3(x, y, fb.center.z));
        Vector3 center = new Vector3(x, y, face.z - 0.03f);

        GameObject root = Empty("Vault_Keypad", parent, center);
        // Text reads from behind its +Z, so +Z points away from the camera.
        root.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        Material steel = LitMaterial(MaterialRoot + "/M_VaultSteel.mat", "M_VaultSteel", new Color(0.3f, 0.33f, 0.38f), 0.55f, 0.55f);
        Material tileMaterial = LitMaterial(MaterialRoot + "/M_WordTile.mat", "M_WordTile", Color.white, 0.4f, 0.6f);

        KeypadBlock("Plate", root.transform, Vector3.zero, new Vector3(plateWidth, plateHeight, 0.04f), steel, false);

        var keys = new Renderer[AlleyHack.Keys];
        var glyphs = new TextMeshPro[AlleyHack.Keys];
        const float key = 0.14f;
        for (int i = 0; i < AlleyHack.Keys; i++)
        {
            int column = i % 2;
            int row = i / 2;
            Vector3 at = new Vector3((column - 0.5f) * (key + 0.03f), 0.075f - row * (key + 0.015f), -0.03f);
            Renderer renderer = KeypadBlock("Key_" + i, root.transform, at, new Vector3(key, key, 0.04f), tileMaterial, true);
            keys[i] = renderer;
            glyphs[i] = KeypadText(renderer.transform, "?", Color.white);
        }

        var lamps = new Renderer[AlleyHack.LadderLamps];
        var labels = new TextMeshPro[AlleyHack.LadderLamps];
        const float lampStep = 0.096f;
        for (int i = 0; i < AlleyHack.LadderLamps; i++)
        {
            Vector3 at = new Vector3((i - (AlleyHack.LadderLamps - 1) * 0.5f) * lampStep, 0.255f, -0.03f);
            Renderer renderer = KeypadBlock("Lamp_" + i, root.transform, at, new Vector3(lampStep - 0.01f, 0.08f, 0.03f), tileMaterial, false);
            lamps[i] = renderer;
            labels[i] = KeypadText(renderer.transform, "", new Color(0.05f, 0.04f, 0.03f));
        }

        // From a little above, looking down at the keys like a person would.
        GameObject viewObject = Empty("Vault_Keypad_View", root.transform, center + new Vector3(0f, 0.42f, -1.15f));
        viewObject.transform.rotation = Quaternion.LookRotation(center + Vector3.up * 0.02f - viewObject.transform.position, Vector3.up);

        Light padLight = Empty("Vault_Keypad_Light", root.transform, center + new Vector3(0f, 0.3f, -0.6f)).AddComponent<Light>();
        padLight.type = LightType.Point;
        padLight.color = new Color(0.85f, 0.92f, 1f);
        padLight.range = 1.6f;
        padLight.intensity = 0f;
        padLight.shadows = LightShadows.None;

        // Between the hack camera and the wall, so the red floods the view.
        Light alarm = Empty("Vault_Keypad_Alarm", root.transform, center + new Vector3(0f, 0.7f, -0.9f)).AddComponent<Light>();
        alarm.type = LightType.Point;
        alarm.color = new Color(1f, 0.08f, 0.06f);
        alarm.range = 5f;
        alarm.intensity = 0f;
        alarm.shadows = LightShadows.None;

        AlleyHack hack = root.AddComponent<AlleyHack>();
        hack.Configure(director, vaultDoor, rig, keys, glyphs, lamps, labels, viewObject.transform, padLight, alarm);
        EditorUtility.SetDirty(root);
        return hack;
    }

    static Renderer KeypadBlock(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool collider)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (!collider) Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = size;
        Renderer renderer = cube.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    static TextMeshPro KeypadText(Transform block, string text, Color color)
    {
        GameObject glyph = new GameObject("Glyph");
        glyph.transform.SetParent(block, false);
        glyph.transform.localPosition = new Vector3(0f, 0f, -0.55f);
        TextMeshPro tmp = glyph.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = new Vector2(0.9f, 0.9f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = 12f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.text = text;
        return tmp;
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
