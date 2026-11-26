using UnityEditor;
using UnityEngine;

// The level's goal display and result panel.
public static partial class AlleySceneBuilder
{
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
