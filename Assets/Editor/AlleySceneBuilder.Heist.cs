using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The vault door progress meter, built from ArtSource/Alley
// (build_vault_door.py) and wired to the chute director's rules.
public static partial class AlleySceneBuilder
{
    const string VaultDoorModel = Art + "SM_VaultDoor.fbx";
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
}
