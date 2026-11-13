using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The vault door progress meter and the Plinko board. Both are built from
// ArtSource/Alley (build_vault_door.py, build_plinko_board.py) and wired to
// the chute director's rules.
public static partial class AlleySceneBuilder
{
    const string VaultDoorModel = Art + "SM_VaultDoor.fbx";
    const string PlinkoBoardModel = Art + "SM_PlinkoBoard.fbx";
    const string PlinkoCoinModel = Art + "SM_PlinkoCoin.fbx";
    const string CoinPhysicsPath = MaterialRoot + "/PM_PlinkoCoin.physicMaterial";
    static readonly string[] DoorParts = { "Bolt_0", "Bolt_1", "Bolt_2", "Bolt_3", "Ring_Outer", "Ring_Inner", "Wheel", "Keyhole" };

    // Two door sizes, switchable to compare: set into the painted doorway of
    // the graffiti facade, or a big door mounted proud of it so the bolts and
    // rings read on a phone.
    const string VaultFitPref = "Juicy.Alley.VaultFitLarge";
    static bool VaultLarge { get { return EditorPrefs.GetBool(VaultFitPref, false); } }

    [MenuItem("Juicy/Alley/Vault/Use Doorway Size")]
    static void UseDoorwayVault() { EditorPrefs.SetBool(VaultFitPref, false); WireGameplayInScene(); }
    [MenuItem("Juicy/Alley/Vault/Use Large Door")]
    static void UseLargeVault() { EditorPrefs.SetBool(VaultFitPref, true); WireGameplayInScene(); }

    public static void SetVaultLarge(bool large) { EditorPrefs.SetBool(VaultFitPref, large); }

    public static void Capture(string path) { CaptureAlley(path); }

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

    static AlleyVaultDoor BuildVaultDoor(Transform parent, AlleyChuteDirector director, AlleyCameraRig rig)
    {
        GameObject facade = GameObject.Find("Vault_Facade");
        Bounds fb;
        if (!TryBounds(facade, out fb)) return null;

        RemapMaterials(VaultDoorModel, VaultDoorMaterials());
        // The painted doorway is about a third of the facade's width.
        float width = VaultLarge ? fb.size.x * 0.56f : fb.size.x * 0.33f;
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
        bounce.bounciness = 0.45f;
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
        coinCollider.radius = 0.04f;
        coinCollider.sharedMaterial = bounce;
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
