using UnityEditor;
using UnityEngine;

// The level's goal display and result panel.
public static partial class AlleySceneBuilder
{
    const string LevelRoot = "Assets/Levels";
    const string CityPath = LevelRoot + "/City.asset";

    // The city's levels. Made once with starting numbers; after that the
    // asset is the place to tune them, and rebuilding the scene keeps them.
    public static PlaceData CityPlace()
    {
        PlaceData city = AssetDatabase.LoadAssetAtPath<PlaceData>(CityPath);
        if (city != null) return city;
        if (!AssetDatabase.IsValidFolder(LevelRoot)) AssetDatabase.CreateFolder("Assets", "Levels");
        city = ScriptableObject.CreateInstance<PlaceData>();
        city.PlaceName = "City";
        city.Levels = new[]
        {
            new LevelSpec { Name = "City 1", Hint = "", Target = 60, Misses = 20, Seed = 1, Dynamite = 0, Cash = 0, DropEvery = 0.85f, FallSpeed = 1.6f, MaxChutes = 4 },
            new LevelSpec { Name = "City 2", Hint = "", Target = 70, Misses = 15, Seed = 2, Dynamite = 0, Cash = 0, DropEvery = 0.75f, FallSpeed = 1.8f, MaxChutes = 4 },
            new LevelSpec { Name = "City 3", Hint = "Flick dynamite up. Never let it land.", Target = 70, Misses = 18, Seed = 3, Dynamite = 12, Cash = 0, DropEvery = 0.8f, FallSpeed = 1.7f, MaxChutes = 4 },
            new LevelSpec { Name = "City 4", Hint = "Cash counts double in either chest.", Target = 75, Misses = 10, Seed = 4, Dynamite = 10, Cash = 3, DropEvery = 0.8f, FallSpeed = 1.8f, MaxChutes = 4 },
            new LevelSpec { Name = "City 5", Hint = "", Target = 80, Misses = 8, Seed = 5, Dynamite = 16, Cash = 4, DropEvery = 0.7f, FallSpeed = 2f, MaxChutes = 4 },
            new LevelSpec { Name = "City 6", Hint = "", Target = 85, Misses = 8, Seed = 6, Dynamite = 20, Cash = 4, DropEvery = 0.62f, FallSpeed = 2.1f, MaxChutes = 4 },
        };
        AssetDatabase.CreateAsset(city, CityPath);
        AssetDatabase.SaveAssets();
        return city;
    }

    static void BuildLevelHud(AlleyChuteDirector director)
    {
        Camera camera = Camera.main;
        GameObject hud = new GameObject("Level_Hud");
        hud.transform.SetParent(camera.transform, false);
        hud.AddComponent<AlleyLevelHud>().Configure(director, HudPanelMaterial());
    }

    // Unlit and see through, tinted per quad.
    static Material HudPanelMaterial()
    {
        string path = MaterialRoot + "/M_LevelHudPanel.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_LevelHudPanel" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }
}
