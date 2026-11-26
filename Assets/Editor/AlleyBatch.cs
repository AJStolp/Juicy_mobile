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

    // Wires the scene and renders the street with the vault door and a close
    // look at the door.
    public static void Previews()
    {
        OpenAlley();
        AlleySceneBuilder.WireGameplayInScene();
        System.IO.File.Copy("/tmp/juicy-alley-light.png", "/tmp/juicy-vault-large.png", true);
        var camera = UnityEngine.Camera.main;
        var home = camera.transform.position;
        var homeRotation = camera.transform.rotation;
        var door = UnityEngine.Object.FindAnyObjectByType<AlleyVaultDoor>();
        camera.transform.position = door.Focus + new UnityEngine.Vector3(0.6f, 0.2f, -3.2f);
        camera.transform.LookAt(door.Focus);
        AlleySceneBuilder.Capture("/tmp/juicy-vault-large-close.png");
        camera.transform.SetPositionAndRotation(home, homeRotation);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    // Renders the vault: the street and a close look at rest, the close look
    // with the planted save's progress (stage 5, 4 of 16 turns), and the door
    // swung open. Files are /tmp/juicy-vault-<street|close|stage5|open>.png.
    public static void VaultPreviews()
    {
        OpenAlley();
        AlleySceneBuilder.WireGameplayInScene();
        var camera = UnityEngine.Camera.main;
        var home = camera.transform.position;
        var homeRotation = camera.transform.rotation;
        AlleySceneBuilder.Capture("/tmp/juicy-vault-street.png");
        var door = UnityEngine.Object.FindAnyObjectByType<AlleyVaultDoor>();
        camera.transform.position = door.Focus + new UnityEngine.Vector3(0.6f, 0.2f, -3.2f);
        camera.transform.LookAt(door.Focus);
        AlleySceneBuilder.Capture("/tmp/juicy-vault-close.png");
        var progress = new VaultProgress();
        progress.Restore(1, 5, 4);
        door.Preview(progress, 0f);
        AlleySceneBuilder.Capture("/tmp/juicy-vault-stage5.png");
        string report = "stage 5 preview shows " + door.LockedShown() + " stages locked\n";
        door.Preview(progress, 105f);
        AlleySceneBuilder.Capture("/tmp/juicy-vault-open.png");
        door.EndPreview();
        camera.transform.SetPositionAndRotation(home, homeRotation);
        System.IO.File.WriteAllText("/tmp/juicy-vault-previews.txt", report);
    }

    // Renders the word sign over the vault with HEIST part spelled. The sign is
    // off while levels drop no letters, so it is turned on just for the shot.
    public static void WordSignPreviews()
    {
        OpenAlley();
        AlleySceneBuilder.WireGameplayInScene();
        var sign = UnityEngine.Object.FindAnyObjectByType<AlleyWordSign>(UnityEngine.FindObjectsInactive.Include);
        sign.gameObject.SetActive(true);
        sign.Show("HEIST", i => i == 0 || i == 2);
        AlleySceneBuilder.Capture("/tmp/juicy-word-vault.png");
        sign.Show("HEIST", _ => false);
        sign.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    // Plays the bot run headless. Leave off -quit: AlleyBotRun exits the
    // editor once the report is written.
    //   Unity -batchmode -projectPath . -executeMethod AlleyBatch.Bot
    public static void Bot() => StartBot(false);

    // Only the six films and the home still, without the numbers.
    public static void BotFilm() => StartBot(true);

    static void StartBot(bool filmOnly)
    {
        OpenAlley();
        SessionState.SetBool("JuicyBotFilmOnly", filmOnly);
        System.IO.File.Delete("/tmp/juicy-level-home.png");
        System.IO.File.Delete("/tmp/juicy-bot.txt");
        SessionState.SetBool("JuicyBotBatch", true);
        System.IO.File.WriteAllText("/tmp/juicy-alley-bot-run", "batch");
    }

    // Checks that a save carries over: writes a known save, enters Play as a
    // normal player, reads back the vault, cash, gear, clues and door, then puts the
    // real save back. Result in /tmp/juicy-save-check.txt. Works in batch mode
    // or, through the flag below, in the open editor.
    //   Unity -batchmode -projectPath . -executeMethod AlleyBatch.SaveCheck
    const string SaveKey = "Juicy.HeistSave";
    const string SaveCheckReport = "/tmp/juicy-save-check.txt";
    const string Planted = "{\"heist\":1,\"stage\":5,\"turns\":4,\"balance\":54321.0,\"gear\":[2,0,1,1]," +
        "\"clueSeed\":77,\"clueCycle\":3,\"clueSymbols\":[6,2],\"clueTiers\":[3,0]}";

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
            var clues = director.Clues;
            // Stages 0 to 4 are locked in the planted save, so the door must
            // show five locked: four slid bolts and the gear turned.
            int locked = door.LockedShown();
            bool ok = vault.Heist == 1 && vault.Stage == 5 && vault.Turns == 4 && rules.Balance == 54321d &&
                rules.GearCount(GearKind.Dynamite) == 2 && rules.GearCount(GearKind.Drill) == 1 && locked == 5 &&
                clues.Count == 2 && clues.Clues[0].Symbol == 6 && clues.Clues[0].Tier == 3 &&
                clues.Clues[1].Symbol == 2 && clues.Clues[1].Tier == 0 && clues.Seed == 77 && clues.Cycle == 3 && !clues.HackDue;
            System.IO.File.WriteAllText(SaveCheckReport, (ok ? "PASS" : "FAIL") +
                " heist " + vault.Heist + " stage " + vault.Stage + " turns " + vault.Turns + "/" + vault.TurnsForStage +
                " balance " + rules.Balance + " dynamite " + rules.GearCount(GearKind.Dynamite) + " drill " + rules.GearCount(GearKind.Drill) +
                " bribe " + rules.GearCount(GearKind.Bribe) + " stages shown locked " + locked +
                " clues restored " + clues.Count + "/" + clues.CluesNeeded + " symbols " + string.Join(",", clues.SymbolsSnapshot()) +
                " tiers " + string.Join(",", clues.TiersSnapshot()) + " cycle " + clues.Cycle + " seed " + clues.Seed + "\n");
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

    // Plants a full clue cycle, plays one hack with scripted taps, and saves
    // every frame at 30 fps to /tmp/juicy-hack-<name>/frame_000.png on, with
    // the result in /tmp/juicy-hack-<name>.txt (PASS or FAIL first). Runs on
    // the bot's save, so the real save is untouched. Turn the frames into a
    // video with: ffmpeg -framerate 30 -i frame_%03d.png -pix_fmt yuv420p out.mp4
    //   Unity -batchmode -projectPath . -executeMethod AlleyBatch.HackPerfect      (no -quit)
    //   Unity -batchmode -projectPath . -executeMethod AlleyBatch.HackFailSecond   (no -quit)
    public static void HackPerfect() => HackCapture("perfect", -1);
    public static void HackFailSecond() => HackCapture("fail-second", 1);

    const string HackStepKey = "JuicyHackCapture";
    const int HackTailFrames = 60;
    static int hackLastFrame = -1;
    static int hackFrames;
    static int hackTail;
    static string hackBefore = "";

    static void HackCapture(string name, int wrongAt)
    {
        OpenAlley();
        string folder = "/tmp/juicy-hack-" + name;
        if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
        System.IO.Directory.CreateDirectory(folder);
        System.IO.File.Delete(folder + ".txt");
        System.IO.File.WriteAllText("/tmp/juicy-alley-bot", "hack");
        SessionState.SetString("JuicyHackName", name);
        SessionState.SetInt("JuicyHackWrongAt", wrongAt);
        SessionState.SetInt(HackStepKey, 1);
        EditorApplication.update += HackCaptureTick;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void ResumeHackCapture()
    {
        if (SessionState.GetInt(HackStepKey, 0) != 0) EditorApplication.update += HackCaptureTick;
    }

    static void HackCaptureTick()
    {
        int step = SessionState.GetInt(HackStepKey, 0);
        string name = SessionState.GetString("JuicyHackName", "hack");
        string folder = "/tmp/juicy-hack-" + name;
        var hack = EditorApplication.isPlaying ? UnityEngine.Object.FindAnyObjectByType<AlleyHack>() : null;
        var director = EditorApplication.isPlaying ? UnityEngine.Object.FindAnyObjectByType<AlleyChuteDirector>() : null;
        if (step == 1 && hack != null && director != null && director.Clues != null && UnityEngine.Time.frameCount > 5)
        {
            UnityEngine.Time.captureFramerate = 30;
            UnityEngine.Time.timeScale = 1f;
            var clues = director.Clues;
            while (!clues.HackDue) clues.ClueFound(ClueRules.MaxTier);
            hack.ScriptNext(SessionState.GetInt("JuicyHackWrongAt", -1));
            hackBefore = "before stage " + director.Vault.Stage + " turns " + director.Vault.Turns + " balance " + director.Rules.Balance.ToString("0") +
                " bribes " + director.Rules.GearCount(GearKind.Bribe) + " drills " + director.Rules.GearCount(GearKind.Drill) +
                " clues " + string.Join(",", clues.SymbolsSnapshot());
            hackFrames = 0;
            hackTail = 0;
            hackLastFrame = -1;
            SessionState.SetInt(HackStepKey, 2);
            return;
        }
        if (step == 2 && hack != null)
        {
            if (UnityEngine.Time.frameCount == hackLastFrame) return;
            hackLastFrame = UnityEngine.Time.frameCount;
            AlleySceneBuilder.Capture(folder + "/frame_" + hackFrames.ToString("000") + ".png");
            hackFrames++;
            if (hack.Hacks == 0 || hack.Running || ++hackTail < HackTailFrames) return;
            HackReward reward = hack.LastReward;
            int count = hack.LastClueCount;
            int wrongAt = SessionState.GetInt("JuicyHackWrongAt", -1);
            int expectedSteps = wrongAt < 0 ? count : wrongAt;
            HackReward expected = ClueRules.RewardFor(count, expectedSteps);
            var saved = HeistSave.FromJson(UnityEngine.PlayerPrefs.GetString("Juicy.HeistSave.Bot", ""));
            bool ok = reward.Steps == expectedSteps && reward.Perfect == (wrongAt < 0) && reward.Cash == expected.Cash &&
                reward.Bribes == expected.Bribes && reward.Drills == expected.Drills && reward.Turns == expected.Turns &&
                director.Clues.Count == 0 && saved != null && saved.clueSymbols.Length == 0 && !director.Paused;
            System.IO.File.WriteAllText(folder + ".txt", (ok ? "PASS" : "FAIL") + " " + name + "\n" +
                "steps " + reward.Steps + "/" + count + (reward.Perfect ? " perfect" : " failed") + " (expected " + expectedSteps + ")\n" +
                "paid cash " + reward.Cash.ToString("0") + " bribes " + reward.Bribes + " drills " + reward.Drills + " vault turns " + reward.Turns + "\n" +
                hackBefore + "\n" +
                "after stage " + director.Vault.Stage + " turns " + director.Vault.Turns + " balance " + director.Rules.Balance.ToString("0") +
                " bribes " + director.Rules.GearCount(GearKind.Bribe) + " drills " + director.Rules.GearCount(GearKind.Drill) +
                " clues left " + director.Clues.Count + " saved clues " + (saved != null ? saved.clueSymbols.Length.ToString() : "none") +
                " sort resumed " + !director.Paused + "\n" +
                "keys " + string.Join(",", hack.KeySymbols) + "\n" +
                "frames " + hackFrames + " in " + folder + "\n");
            UnityEngine.Time.captureFramerate = 0;
            SessionState.SetInt(HackStepKey, 3);
            EditorApplication.isPlaying = false;
            return;
        }
        if (step == 3 && !EditorApplication.isPlaying)
        {
            SessionState.SetInt(HackStepKey, 0);
            EditorApplication.update -= HackCaptureTick;
            System.IO.File.Delete("/tmp/juicy-alley-bot");
            bool passed = System.IO.File.Exists(folder + ".txt") && System.IO.File.ReadAllText(folder + ".txt").StartsWith("PASS");
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
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
        string[] models = { "Assets/Art/Alley/SM_GraffitiVaultDoor.fbx" };
        var log = new System.Text.StringBuilder();
        foreach (string path in models)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
            log.AppendLine(path + (prefab == null ? " MISSING" : ""));
            if (prefab == null) continue;
            foreach (UnityEngine.Transform t in prefab.GetComponentsInChildren<UnityEngine.Transform>())
            {
                log.AppendLine("  " + t.name + " parent " + (t.parent ? t.parent.name : "-") + " local " + t.localPosition.ToString("F3") +
                    " rot " + t.localEulerAngles.ToString("F0") + " scale " + t.localScale.ToString("F2"));
            }
        }
        System.IO.File.WriteAllText("/tmp/juicy-inspect.txt", log.ToString());
    }
}
