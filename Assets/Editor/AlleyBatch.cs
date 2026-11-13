using UnityEditor;
using UnityEditor.SceneManagement;

// Entry points for running alley tools with Unity closed:
//   Unity -batchmode -projectPath . -executeMethod AlleyBatch.Wire -quit
// Batch mode still renders, so captures land where the flag hooks put them.
public static class AlleyBatch
{
    const string ScenePath = "Assets/Scenes/DiegeticAlley.unity";

    public static void Wire()
    {
        EditorSceneManager.OpenScene(ScenePath);
        AlleySceneBuilder.WireGameplayInScene();
    }

    // Wires the scene with each vault door size and renders the street, a
    // close look at the door, and the Plinko board swung open. Leaves the
    // scene on the doorway size.
    public static void Previews()
    {
        EditorSceneManager.OpenScene(ScenePath);
        foreach (bool large in new[] { true, false })
        {
            string tag = large ? "large" : "doorway";
            AlleySceneBuilder.SetVaultLarge(large);
            AlleySceneBuilder.WireGameplayInScene();
            System.IO.File.Copy("/tmp/juicy-alley-light.png", "/tmp/juicy-vault-" + tag + ".png", true);
            var camera = UnityEngine.Camera.main;
            var home = camera.transform.position;
            var homeRotation = camera.transform.rotation;
            var door = UnityEngine.Object.FindAnyObjectByType<AlleyVaultDoor>();
            camera.transform.position = door.Focus + new UnityEngine.Vector3(0.6f, 0.2f, -3.2f);
            camera.transform.LookAt(door.Focus);
            AlleySceneBuilder.Capture("/tmp/juicy-vault-" + tag + "-close.png");
            if (!large)
            {
                var hinge = UnityEngine.GameObject.Find("Plinko_Hinge").transform;
                var closed = hinge.rotation;
                hinge.rotation = UnityEngine.Quaternion.Euler(0f, 152f, 0f);
                var view = UnityEngine.GameObject.Find("Plinko_View").transform;
                var floor = UnityEngine.GameObject.Find("Plinko_Board").transform.Find("Floor");
                camera.transform.position = view.position;
                camera.transform.LookAt(floor.position + UnityEngine.Vector3.up * 0.715f);
                float fov = camera.fieldOfView;
                camera.fieldOfView = 58f;
                UnityEngine.GameObject.Find("Plinko_Light").GetComponent<UnityEngine.Light>().intensity = 2.5f;
                AlleySceneBuilder.Capture("/tmp/juicy-plinko-open.png");
                camera.fieldOfView = fov;
                UnityEngine.GameObject.Find("Plinko_Light").GetComponent<UnityEngine.Light>().intensity = 0f;
                hinge.rotation = closed;
            }
            camera.transform.SetPositionAndRotation(home, homeRotation);
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    // Plays the bot run headless. Leave off -quit: AlleyBotRun exits the
    // editor once the report is written.
    //   Unity -batchmode -projectPath . -executeMethod AlleyBatch.Bot
    public static void Bot()
    {
        EditorSceneManager.OpenScene(ScenePath);
        System.IO.File.Delete("/tmp/juicy-bot.txt");
        SessionState.SetBool("JuicyBotBatch", true);
        System.IO.File.WriteAllText("/tmp/juicy-alley-bot-run", "batch");
    }

    // Checks that a save carries over: writes a known save, enters Play as a
    // normal player, reads back the vault, cash, gear and door, then puts the
    // real save back. Result in /tmp/juicy-save-check.txt. Works in batch mode
    // or, through the flag below, in the open editor.
    //   Unity -batchmode -projectPath . -executeMethod AlleyBatch.SaveCheck
    const string SaveKey = "Juicy.HeistSave";
    const string SaveCheckReport = "/tmp/juicy-save-check.txt";
    const string Planted = "{\"heist\":1,\"stage\":5,\"turns\":4,\"balance\":54321.0,\"gear\":[2,0,1,1]}";

    public static void SaveCheck()
    {
        EditorSceneManager.OpenScene(ScenePath);
        System.IO.File.Delete("/tmp/juicy-alley-bot");
        SessionState.SetString("JuicySaveBackup", UnityEngine.PlayerPrefs.GetString(SaveKey, ""));
        UnityEngine.PlayerPrefs.SetString(SaveKey, Planted);
        UnityEngine.PlayerPrefs.Save();
        SessionState.SetInt("JuicySaveCheck", 1);
        EditorApplication.update += SaveCheckTick;
        EditorApplication.isPlaying = true;
    }

    // In the open editor, touch /tmp/juicy-save-check to run the same check.
    const string SaveCheckFlag = "/tmp/juicy-save-check";

    [InitializeOnLoadMethod]
    static void ResumeSaveCheck()
    {
        if (SessionState.GetInt("JuicySaveCheck", 0) != 0) EditorApplication.update += SaveCheckTick;
        else EditorApplication.update += WatchSaveCheckFlag;
    }

    static void WatchSaveCheckFlag()
    {
        if (!System.IO.File.Exists(SaveCheckFlag) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        System.IO.File.Delete(SaveCheckFlag);
        EditorApplication.update -= WatchSaveCheckFlag;
        SaveCheck();
    }

    static void SaveCheckTick()
    {
        int step = SessionState.GetInt("JuicySaveCheck", 0);
        if (step == 1 && EditorApplication.isPlaying && UnityEngine.Time.frameCount > 5)
        {
            var director = UnityEngine.Object.FindAnyObjectByType<AlleyChuteDirector>();
            var door = UnityEngine.Object.FindAnyObjectByType<AlleyVaultDoor>();
            var vault = director.Vault;
            var rules = director.Rules;
            int hiddenBolts = 0;
            foreach (var t in door.GetComponentsInChildren<UnityEngine.Transform>(true))
                if (t.name.StartsWith("Bolt_") && !t.gameObject.activeSelf) hiddenBolts++;
            var ring = door.transform.Find("Door/Ring_Outer").localEulerAngles;
            bool ok = vault.Heist == 1 && vault.Stage == 5 && vault.Turns == 4 && rules.Balance == 54321d &&
                rules.GearCount(GearKind.Dynamite) == 2 && rules.GearCount(GearKind.Drill) == 1 && hiddenBolts == 4;
            System.IO.File.WriteAllText(SaveCheckReport, (ok ? "PASS" : "FAIL") +
                " heist " + vault.Heist + " stage " + vault.Stage + " turns " + vault.Turns + "/" + vault.TurnsForStage +
                " balance " + rules.Balance + " dynamite " + rules.GearCount(GearKind.Dynamite) + " drill " + rules.GearCount(GearKind.Drill) +
                " bribe " + rules.GearCount(GearKind.Bribe) + " bolts popped " + hiddenBolts + " outer ring " + ring.ToString("F0") + "\n");
            SessionState.SetInt("JuicySaveCheck", 2);
            EditorApplication.isPlaying = false;
            return;
        }
        if (step == 2 && !EditorApplication.isPlaying)
        {
            SessionState.SetInt("JuicySaveCheck", 0);
            EditorApplication.update -= SaveCheckTick;
            string backup = SessionState.GetString("JuicySaveBackup", "");
            if (backup.Length > 0) UnityEngine.PlayerPrefs.SetString(SaveKey, backup);
            else UnityEngine.PlayerPrefs.DeleteKey(SaveKey);
            UnityEngine.PlayerPrefs.Save();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }
    }

    // Prints where the named children of the built models sit, so code can
    // be checked against how Blender axes arrive in Unity.
    public static void Inspect()
    {
        string[] models = { "Assets/Art/Alley/SM_VaultDoor.fbx", "Assets/Art/Alley/SM_PlinkoBoard.fbx" };
        var log = new System.Text.StringBuilder();
        foreach (string path in models)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
            log.AppendLine(path + (prefab == null ? " MISSING" : ""));
            if (prefab == null) continue;
            foreach (UnityEngine.Transform t in prefab.GetComponentsInChildren<UnityEngine.Transform>())
            {
                if (t.name.StartsWith("Pin_") && t.name != "Pin_0_1") continue;
                log.AppendLine("  " + t.name + " parent " + (t.parent ? t.parent.name : "-") + " local " + t.localPosition.ToString("F3") +
                    " rot " + t.localEulerAngles.ToString("F0") + " scale " + t.localScale.ToString("F2"));
            }
        }
        System.IO.File.WriteAllText("/tmp/juicy-inspect.txt", log.ToString());
    }
}
