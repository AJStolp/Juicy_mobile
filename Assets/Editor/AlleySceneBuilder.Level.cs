using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The level's goal display and result panel, and the menu that picks which
// goal an attempt starts with. Prototype for T-028: the three goals stay
// switchable until Anthony picks one.
public static partial class AlleySceneBuilder
{
    const string MenuGoalHeat = "Juicy/Alley/Level/Goal A Heat";
    const string MenuGoalClock = "Juicy/Alley/Level/Goal B Clock";
    const string MenuGoalGems = "Juicy/Alley/Level/Goal C Gems";

    [MenuItem(MenuGoalHeat)] static void GoalHeat() => SetGoal(LevelGoal.Heat);
    [MenuItem(MenuGoalClock)] static void GoalClock() => SetGoal(LevelGoal.Clock);
    [MenuItem(MenuGoalGems)] static void GoalGems() => SetGoal(LevelGoal.Gems);

    [MenuItem(MenuGoalHeat, true)] static bool CheckHeat() => CheckGoal(MenuGoalHeat, LevelGoal.Heat);
    [MenuItem(MenuGoalClock, true)] static bool CheckClock() => CheckGoal(MenuGoalClock, LevelGoal.Clock);
    [MenuItem(MenuGoalGems, true)] static bool CheckGems() => CheckGoal(MenuGoalGems, LevelGoal.Gems);

    static bool CheckGoal(string menu, LevelGoal goal)
    {
        var director = Object.FindAnyObjectByType<AlleyChuteDirector>();
        Menu.SetChecked(menu, director != null && director.Goal == goal);
        return director != null && !EditorApplication.isPlaying;
    }

    // Sets the goal the first attempt starts with and saves the scene.
    public static void SetGoal(LevelGoal goal)
    {
        var director = Object.FindAnyObjectByType<AlleyChuteDirector>();
        if (director == null) throw new System.InvalidOperationException("No chute director in the open scene");
        var serialized = new SerializedObject(director);
        serialized.FindProperty("goal").enumValueIndex = (int)goal;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Scene scene = director.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
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
