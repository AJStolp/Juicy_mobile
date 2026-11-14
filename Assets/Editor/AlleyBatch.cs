using UnityEditor;
using UnityEditor.SceneManagement;

// Entry points for running alley tools with Unity closed:
//   Unity -batchmode -projectPath . -executeMethod AlleyBatch.Wire -quit
// Batch mode still renders, so captures land where the flag hooks put them.
public static class AlleyBatch
{
    const string ScenePath = "Assets/Scenes/DiegeticAlley.unity";

    // In the open editor, write a method name (for example WordSignPreviews)
    // to /tmp/juicy-batch and it runs here; "done" or the error lands in
    // /tmp/juicy-batch.txt.
    const string RunFlag = "/tmp/juicy-batch";

    [InitializeOnLoadMethod]
    static void WatchRunFlag()
    {
        EditorApplication.update += () =>
        {
            if (!System.IO.File.Exists(RunFlag) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            // Pick up any script changes first; the flag waits out the
            // compile and runs on the editor's next pass with the new code.
            AssetDatabase.Refresh();
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string name = System.IO.File.ReadAllText(RunFlag).Trim();
            System.IO.File.Delete(RunFlag);
            try
            {
                typeof(AlleyBatch).GetMethod(name).Invoke(null, null);
                System.IO.File.WriteAllText("/tmp/juicy-batch.txt", "done " + name + "\n");
            }
            catch (System.Exception error)
            {
                System.IO.File.WriteAllText("/tmp/juicy-batch.txt", error.ToString());
            }
        };
    }

    // Leaves an already open alley scene as it is, so running in the open
    // editor never discards anything.
    static void OpenAlley()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
    }

    public static void Wire()
    {
        OpenAlley();
        AlleySceneBuilder.WireGameplayInScene();
    }

    // Wires the scene with each vault door size and renders the street, a
    // close look at the door, and the Plinko board swung open. Leaves the
    // scene on the doorway size.
    public static void Previews()
    {
        OpenAlley();
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

    // Renders the word sign in both places with HEIST part spelled, then
    // leaves the scene on the vault placement.
    public static void WordSignPreviews()
    {
        OpenAlley();
        foreach (bool street in new[] { true, false })
        {
            AlleySceneBuilder.SetWordSignStreet(street);
            AlleySceneBuilder.WireGameplayInScene();
            var sign = UnityEngine.Object.FindAnyObjectByType<AlleyWordSign>();
            sign.Show("HEIST", i => i == 0 || i == 2);
            AlleySceneBuilder.Capture("/tmp/juicy-word-" + (street ? "street" : "vault") + ".png");
            sign.Show("HEIST", _ => false);
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    // Plays the bot run headless. Leave off -quit: AlleyBotRun exits the
    // editor once the report is written.
    //   Unity -batchmode -projectPath . -executeMethod AlleyBatch.Bot
    public static void Bot()
    {
        OpenAlley();
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
        OpenAlley();
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

    // Drops 200 coins on the Plinko board at 8x with the bot's aim and counts
    // the slots they land in. Result in /tmp/juicy-plinko-sim.txt.
    const string SimReport = "/tmp/juicy-plinko-sim.txt";
    const int SimCoins = 200;

    public static void PlinkoSim()
    {
        OpenAlley();
        System.IO.File.WriteAllText("/tmp/juicy-alley-bot", "sim");
        SessionState.SetInt("JuicyPlinkoSim", 1);
        EditorApplication.update += PlinkoSimTick;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void ResumePlinkoSim()
    {
        if (SessionState.GetInt("JuicyPlinkoSim", 0) != 0) EditorApplication.update += PlinkoSimTick;
    }

    static void PlinkoSimTick()
    {
        int step = SessionState.GetInt("JuicyPlinkoSim", 0);
        var plinko = EditorApplication.isPlaying ? UnityEngine.Object.FindAnyObjectByType<AlleyPlinko>() : null;
        if (step == 1 && plinko != null && UnityEngine.Time.frameCount > 5)
        {
            UnityEngine.Time.timeScale = 8f;
            plinko.Simulate(SimCoins);
            SessionState.SetInt("JuicyPlinkoSim", 2);
            return;
        }
        if (step == 2 && plinko != null && !plinko.Running && plinko.Rounds > 0)
        {
            var log = new System.Text.StringBuilder("coins " + plinko.CoinsDropped + " jostles " + plinko.Jostles + " pin hits " + plinko.PinHits + "\n" + plinko.Settings + "\n");
            for (int slot = 0; slot < PlinkoPayout.SlotCount; slot++)
            {
                PlinkoReward reward = PlinkoPayout.For(slot);
                string prize = reward.Prize == PlinkoPrize.Cash ? "$" + reward.Cash : reward.Prize == PlinkoPrize.Gear ? reward.Gear.ToString() : "VAULT";
                log.AppendLine("slot " + slot + " " + prize + " " + plinko.SlotHits[slot] + " (" + (100f * plinko.SlotHits[slot] / UnityEngine.Mathf.Max(1, plinko.CoinsDropped)).ToString("0") + "%)");
            }
            System.IO.File.WriteAllText(SimReport, log.ToString());
            UnityEngine.Time.timeScale = 1f;
            SessionState.SetInt("JuicyPlinkoSim", 3);
            EditorApplication.isPlaying = false;
            return;
        }
        if (step == 3 && !EditorApplication.isPlaying)
        {
            SessionState.SetInt("JuicyPlinkoSim", 0);
            EditorApplication.update -= PlinkoSimTick;
            System.IO.File.Delete("/tmp/juicy-alley-bot");
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }
    }

    // Measures the editor's CPU use in Play mode, 15 seconds each: uncapped,
    // at the frame rate cap, and at 30. Plays as a person would (no bot,
    // normal speed, nothing touched), and splits each frame into game, render
    // and editor time. Result in /tmp/juicy-play-cpu.txt.
    const string CpuReport = "/tmp/juicy-play-cpu.txt";
    const float CpuWindow = 15f;
    static readonly int[] CpuCaps = { -1, FrameRateCap.Target, 30 };
    static readonly string[] CpuMarkers = { "PlayerLoop", "EditorLoop", "RenderPipelineManager.DoRenderLoop_Internal()", "Gfx.WaitForPresentOnGfxThread" };
    static Unity.Profiling.ProfilerRecorder[] cpuRecorders;

    public static void MeasurePlayCpu()
    {
        OpenAlley();
        System.IO.File.Delete("/tmp/juicy-alley-bot");
        SessionState.SetInt("JuicyCpu", 1);
        EditorApplication.update += CpuTick;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void ResumeCpu()
    {
        if (SessionState.GetInt("JuicyCpu", 0) != 0) EditorApplication.update += CpuTick;
    }

    static void CpuTick()
    {
        int step = SessionState.GetInt("JuicyCpu", 0);
        double now = EditorApplication.timeSinceStartup;
        double cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
        // Odd steps start a window, even steps end it; window k is step 2k+1.
        int window = (step - 1) / 2;
        if (step % 2 == 1 && window < CpuCaps.Length && EditorApplication.isPlaying && UnityEngine.Time.frameCount > 30)
        {
            UnityEngine.Application.targetFrameRate = CpuCaps[window];
            if (cpuRecorders == null)
            {
                cpuRecorders = new Unity.Profiling.ProfilerRecorder[CpuMarkers.Length];
                for (int i = 0; i < CpuMarkers.Length; i++)
                    cpuRecorders[i] = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, CpuMarkers[i], 200);
            }
            SessionState.SetFloat("JuicyCpuStart", (float)now);
            SessionState.SetFloat("JuicyCpuStartCpu", (float)cpu);
            SessionState.SetInt("JuicyCpuFrames", UnityEngine.Time.frameCount);
            SessionState.SetInt("JuicyCpu", step + 1);
            return;
        }
        if (step % 2 == 0 && step > 0 && EditorApplication.isPlaying && now - SessionState.GetFloat("JuicyCpuStart", 0f) >= CpuWindow)
        {
            window = step / 2 - 1;
            double wall = now - SessionState.GetFloat("JuicyCpuStart", 0f);
            double used = cpu - SessionState.GetFloat("JuicyCpuStartCpu", 0f);
            int frames = UnityEngine.Time.frameCount - SessionState.GetInt("JuicyCpuFrames", 0);
            var line = new System.Text.StringBuilder((CpuCaps[window] < 0 ? "uncapped" : "capped at " + CpuCaps[window]) + ": " +
                (used / wall).ToString("0.00") + " cores, " + (frames / wall).ToString("0") + " fps;");
            for (int i = 0; i < CpuMarkers.Length; i++)
            {
                var r = cpuRecorders[i];
                double sum = 0;
                for (int k = 0; k < r.Count; k++) sum += r.GetSample(k).Value;
                line.Append(" " + CpuMarkers[i].Split('.')[0] + " " + (r.Count > 0 ? sum / r.Count / 1e6 : 0).ToString("0.0") + "ms");
            }
            System.IO.File.AppendAllText(CpuReport, line + "\n");
            if (window + 1 < CpuCaps.Length)
            {
                SessionState.SetInt("JuicyCpu", step + 1);
                SessionState.SetInt("JuicyCpuFrames", UnityEngine.Time.frameCount);
                return;
            }
            SessionState.SetInt("JuicyCpu", 99);
            EditorApplication.isPlaying = false;
            return;
        }
        if (step == 99 && !EditorApplication.isPlaying)
        {
            SessionState.SetInt("JuicyCpu", 0);
            EditorApplication.update -= CpuTick;
            if (cpuRecorders != null) foreach (var r in cpuRecorders) r.Dispose();
            cpuRecorders = null;
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
