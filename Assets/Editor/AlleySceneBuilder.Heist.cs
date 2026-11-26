using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The vault door progress meter and the hack keypad. The vault is the
// graffiti facade's own door, split into moving parts by import_glb.py
// --vault-parts. Both are wired to the chute director's rules.
public static partial class AlleySceneBuilder
{
    // The facade recess is the doorway plus 2mm a side, in model meters.
    const float RecessModelWidth = 0.36f;
    // The door frame's top edge stands this far above the recess, in model meters.
    const float FrameModelBand = 0.023f;

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

        // One lamp per clue to enter, lit as each goes in.
        var lamps = new Renderer[AlleyHack.Lamps];
        const float lampStep = 0.064f;
        for (int i = 0; i < AlleyHack.Lamps; i++)
        {
            Vector3 at = new Vector3((i - (AlleyHack.Lamps - 1) * 0.5f) * lampStep, 0.255f, -0.03f);
            lamps[i] = KeypadBlock("Lamp_" + i, root.transform, at, new Vector3(lampStep - 0.012f, 0.06f, 0.03f), tileMaterial, false);
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
        hack.Configure(director, rig, HudPanelMaterial(), keys, glyphs, lamps, viewObject.transform, padLight, alarm);
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
}
