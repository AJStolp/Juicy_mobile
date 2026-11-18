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
        var clueFaces = new Renderer[WordSignSlots];
        Material clueLook = ClueSymbolMaterial();
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

            // Option B: a clue covers the tile's face, in front of the letter.
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.name = "Clue_Face";
            face.transform.SetParent(cube.transform, false);
            face.transform.localPosition = new Vector3(0f, 0f, -0.62f);
            face.transform.localScale = new Vector3(1.05f, 1.05f, 1f);
            Renderer faceRenderer = face.GetComponent<Renderer>();
            faceRenderer.sharedMaterial = clueLook;
            faceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            faceRenderer.receiveShadows = false;
            face.SetActive(false);
            clueFaces[i] = faceRenderer;
        }

        AlleyWordSign sign = root.AddComponent<AlleyWordSign>();
        sign.Configure(director, tiles, glyphs, clueFaces);
        sign.Show("HEIST", _ => false);
        return sign;
    }

    // The clue trip: the spots the raccoon visits in turn, the dynamite blast,
    // and both places clues can be kept (the pick is Juicy/Alley/Clues).
    const string ClueKeeperPref = "Juicy.Alley.ClueKeeper";
    const string ClueSymbolMaterialPath = MaterialRoot + "/M_ClueSymbol.mat";
    static readonly string[] ClueProps = { "Dumpster", "Heavy_Crates", "Trash_Bag_03", "Wall_Lamp_Left" };

    static ClueKeeper SavedClueKeeper => (ClueKeeper)EditorPrefs.GetInt(ClueKeeperPref, (int)ClueKeeper.Notebook);

    static Material ClueSymbolMaterial()
    {
        Material material = TransparentMaterial(ClueSymbolMaterialPath, "M_ClueSymbol", "Universal Render Pipeline/Particles/Unlit", null, Color.white);
        material.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static AlleyClueTrip BuildClueTrip(Transform parent, AlleyChuteDirector director, AlleyVaultDoor vaultDoor, AlleyCameraRig rig, AlleyWordSign sign, GameObject raccoon)
    {
        if (raccoon == null) return null;
        StandOnSpot stand = raccoon.GetComponent<StandOnSpot>();
        Vector3 home = RaccoonSpot();
        var spots = new List<ClueSpot>();
        foreach (string name in ClueProps)
        {
            GameObject prop = GameObject.Find(name);
            Bounds b;
            if (prop == null || !TryBounds(prop, out b)) continue;
            spots.Add(SpotFor(prop.transform, b, home));
        }

        GameObject root = Empty("Clue_Trip", parent, Vector3.zero);
        ParticleSystem burst = Burst(root.transform);
        GameObject flashObject = Empty("Clue_Blast_Light", root.transform, Vector3.zero);
        Light flash = flashObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.6f, 0.25f);
        flash.range = 5f;
        flash.intensity = 0f;
        flash.shadows = LightShadows.None;

        AlleyClueNotebook notebook = BuildClueNotebook();
        AlleyClueTrip trip = root.AddComponent<AlleyClueTrip>();
        trip.Configure(director, vaultDoor, rig, raccoon.GetComponentInChildren<Animator>(), stand, spots.ToArray(), burst, flash,
            ClueSymbolMaterial(), notebook, sign);
        ApplyClueKeeper(trip, SavedClueKeeper);
        EditorUtility.SetDirty(root);
        return trip;
    }

    // He stands just off the prop's near side, toward the manhole; the clue
    // rises over a prop on the ground, or out of a lamp on the wall.
    static ClueSpot SpotFor(Transform prop, Bounds b, Vector3 home)
    {
        Vector3 near = b.ClosestPoint(new Vector3(home.x, b.center.y, home.z));
        Vector3 toward = new Vector3(home.x - near.x, 0f, home.z - near.z);
        toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.back;
        Vector3 standAt = near + toward * 0.38f;
        standAt = new Vector3(Mathf.Clamp(standAt.x, -1.5f, 1.5f), home.y, standAt.z);
        bool onWall = b.min.y > 1f;
        Vector3 rise = Vector3.Lerp(new Vector3(b.center.x, 0f, b.center.z), new Vector3(standAt.x, 0f, standAt.z), onWall ? 0.25f : 0.3f);
        rise.y = onWall ? b.center.y + 0.15f : b.max.y + 0.55f;
        return new ClueSpot { Prop = prop, Stand = standAt, Rise = rise };
    }

    // Option A: the project's first screen canvas, a notebook in the top
    // left corner with one slot per clue.
    static AlleyClueNotebook BuildClueNotebook()
    {
        GameObject canvasObject = new GameObject("Clue_Notebook_Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 0.3f;
        canvas.sortingOrder = 10;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var page = UiBox("Notebook", canvasObject.transform, new Color(0.93f, 0.88f, 0.74f), new Vector2(0f, 1f), new Vector2(40f, -150f), new Vector2(480f, 200f));
        page.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
        UiBox("Binding", page, new Color(0.55f, 0.12f, 0.1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(26f, 200f));
        UiBox("Rule_Top", page, new Color(0.35f, 0.5f, 0.75f, 0.35f), new Vector2(0f, 1f), new Vector2(26f, -62f), new Vector2(454f, 3f));

        var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        var titleRect = (RectTransform)title.transform;
        titleRect.SetParent(page, false);
        Pin(titleRect, new Vector2(0f, 1f), new Vector2(48f, -10f), new Vector2(400f, 50f));
        var text = title.GetComponent<TextMeshProUGUI>();
        text.text = "CLUES";
        text.fontSize = 38f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.22f, 0.14f, 0.08f);
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;

        var slots = new UnityEngine.UI.RawImage[ClueRules.CluesPerHackLater];
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = new GameObject("Clue_" + i, typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            var rect = (RectTransform)slot.transform;
            rect.SetParent(page, false);
            Pin(rect, new Vector2(0f, 1f), new Vector2(48f + i * 106f, -74f), new Vector2(100f, 100f));
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition += new Vector2(50f, -50f);
            var image = slot.GetComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            image.color = new Color(0.16f, 0.14f, 0.12f, 0.35f);
            slots[i] = image;
        }

        AlleyClueNotebook notebook = canvasObject.AddComponent<AlleyClueNotebook>();
        notebook.Configure(canvas, page, slots);
        return notebook;
    }

    static RectTransform UiBox(string name, Transform parent, Color color, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var box = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = (RectTransform)box.transform;
        rect.SetParent(parent, false);
        Pin(rect, anchor, position, size);
        var image = box.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    static void Pin(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // Edit mode: the scene keeps the pick and shows it. Play mode: switches live.
    static void ApplyClueKeeper(AlleyClueTrip trip, ClueKeeper keeper)
    {
        if (trip == null) return;
        if (Application.isPlaying)
        {
            trip.Keeper = keeper;
            return;
        }
        var serialized = new SerializedObject(trip);
        serialized.FindProperty("keeper").enumValueIndex = (int)keeper;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var notebook = Object.FindAnyObjectByType<AlleyClueNotebook>();
        if (notebook != null) notebook.Canvas.enabled = keeper == ClueKeeper.Notebook;
    }

    public static void SetClueKeeper(ClueKeeper keeper)
    {
        EditorPrefs.SetInt(ClueKeeperPref, (int)keeper);
        var trip = Object.FindAnyObjectByType<AlleyClueTrip>();
        ApplyClueKeeper(trip, keeper);
        if (Application.isPlaying || trip == null) return;
        EditorUtility.SetDirty(trip);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(trip.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(trip.gameObject.scene);
    }

    [MenuItem("Juicy/Alley/Clues/Option A Notebook")]
    static void UseClueNotebook() => SetClueKeeper(ClueKeeper.Notebook);

    [MenuItem("Juicy/Alley/Clues/Option B Word Sign")]
    static void UseClueWordSign() => SetClueKeeper(ClueKeeper.WordSign);

    [MenuItem("Juicy/Alley/Clues/Option A Notebook", true)]
    static bool CheckClueNotebook()
    {
        Menu.SetChecked("Juicy/Alley/Clues/Option A Notebook", CurrentClueKeeper() == ClueKeeper.Notebook);
        return true;
    }

    [MenuItem("Juicy/Alley/Clues/Option B Word Sign", true)]
    static bool CheckClueWordSign()
    {
        Menu.SetChecked("Juicy/Alley/Clues/Option B Word Sign", CurrentClueKeeper() == ClueKeeper.WordSign);
        return true;
    }

    static ClueKeeper CurrentClueKeeper()
    {
        var trip = Object.FindAnyObjectByType<AlleyClueTrip>();
        return trip != null ? trip.Keeper : SavedClueKeeper;
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

    static AlleyVaultDoor BuildVaultDoor(Transform parent, AlleyChuteDirector director, AlleyCameraRig rig)
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
