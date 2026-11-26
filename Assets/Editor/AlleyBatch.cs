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
        progress.Reset(16);
        progress.Restore(5, 4);
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

    // Only the vault's numbers, then the films: for tuning the boss.
    public static void BotVault() => StartBot(false, true);

    static void StartBot(bool filmOnly, bool vaultOnly = false)
    {
        OpenAlley();
        SessionState.SetBool("JuicyBotFilmOnly", filmOnly);
        SessionState.SetBool("JuicyBotVaultOnly", vaultOnly);
        System.IO.File.Delete("/tmp/juicy-level-home.png");
        System.IO.File.Delete("/tmp/juicy-bot.txt");
        SessionState.SetBool("JuicyBotBatch", true);
        System.IO.File.WriteAllText("/tmp/juicy-alley-bot-run", "batch");
    }

    // Checks that a save carries over: writes a known save, enters Play as a
    // normal player, reads back the cash, gear, city progress and clues, then
    // puts the real save back. Result in /tmp/juicy-save-check.txt. Works in batch mode
    // or, through the flag below, in the open editor.
    //   Unity -batchmode -projectPath . -executeMethod AlleyBatch.SaveCheck
    const string SaveKey = "Juicy.HeistSave";
    const string SaveCheckReport = "/tmp/juicy-save-check.txt";
    const string Planted = "{\"balance\":54321.0,\"gear\":[2,0,1,1],\"placeBeaten\":6,\"vaultCracked\":true," +
        "\"placeClueTiers\":[3,0,2,1,3,2]}";

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
            var rules = director.Rules;
            var file = director.CaseFile;
            var tiers = file.TiersSnapshot();
            bool ok = rules.Balance == 54321d && rules.GearCount(GearKind.Dynamite) == 2 && rules.GearCount(GearKind.Drill) == 1 &&
                director.Progress.Beaten == 6 && director.Progress.VaultCracked &&
                string.Join(",", tiers) == "3,0,2,1,3,2";
            System.IO.File.WriteAllText(SaveCheckReport, (ok ? "PASS" : "FAIL") +
                " balance " + rules.Balance + " dynamite " + rules.GearCount(GearKind.Dynamite) + " drill " + rules.GearCount(GearKind.Drill) +
                " bribe " + rules.GearCount(GearKind.Bribe) + " city levels beaten " + director.Progress.Beaten +
                " vault cracked " + director.Progress.VaultCracked + " clue tiers " + string.Join(",", tiers) + "\n");
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

    // Plays the map through on the real save (backed up and put back):
    // stills of both looks with nothing and three levels beaten, a locked stop
    // refusing a tap, an open stop starting its level, a forced win, the clue
    // scratched off (half, then all), the map coming back with the next stop
    // open, the clue on the board and the save holding both; then the vault:
    // every level beaten opens it, its sort locks six stages, the keypad
    // finale enters all six clues (one wrong tap on the way), the vault
    // cracks, the save holds it and a tap goes back to the map.
    // Stills in /tmp/juicy-map-*.png, result in /tmp/juicy-map.txt (PASS or
    // FAIL first).
    //   Unity -batchmode -projectPath . -executeMethod AlleyBatch.MapCheck   (no -quit)
    const string MapStepKey = "JuicyMapCheck";
    const string MapReport = "/tmp/juicy-map.txt";
    static int mapWait;
    static float mapWaitTime;
    static float mapGiveUpAt;
    static readonly System.Text.StringBuilder mapLog = new System.Text.StringBuilder();
    static bool mapOk;
    static int mapErrors;
    static string mapFirstError = "";

    static void CountMapError(string message, string stack, UnityEngine.LogType type)
    {
        if (type != UnityEngine.LogType.Error && type != UnityEngine.LogType.Exception && type != UnityEngine.LogType.Assert) return;
        if (mapErrors++ == 0) mapFirstError = message;
    }

    public static void MapCheck()
    {
        OpenAlley();
        System.IO.File.Delete("/tmp/juicy-alley-bot");
        System.IO.File.Delete(MapReport);
        SessionState.SetString("JuicyMapBackup", UnityEngine.PlayerPrefs.GetString(SaveKey, ""));
        UnityEngine.PlayerPrefs.DeleteKey(SaveKey);
        UnityEngine.PlayerPrefs.Save();
        SessionState.SetInt(MapStepKey, 1);
        EditorApplication.update += MapCheckTick;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void ResumeMapCheck()
    {
        if (SessionState.GetInt(MapStepKey, 0) != 0) EditorApplication.update += MapCheckTick;
    }

    static void MapStep(int next, int waitFrames = 3, float waitSeconds = 0f)
    {
        SessionState.SetInt(MapStepKey, next);
        mapWait = UnityEngine.Time.frameCount + waitFrames;
        mapWaitTime = UnityEngine.Time.time + waitSeconds;
    }

    static void MapExpect(bool condition, string what)
    {
        mapLog.AppendLine((condition ? "ok   " : "FAIL ") + what);
        if (!condition) mapOk = false;
    }

    static void MapCheckTick()
    {
        int step = SessionState.GetInt(MapStepKey, 0);
        if (step == 0) return;
        if (step < 90 && (!EditorApplication.isPlaying || UnityEngine.Time.frameCount < mapWait || UnityEngine.Time.time < mapWaitTime)) return;
        var director = EditorApplication.isPlaying ? UnityEngine.Object.FindAnyObjectByType<AlleyChuteDirector>() : null;
        var map = EditorApplication.isPlaying ? UnityEngine.Object.FindAnyObjectByType<AlleyMap>() : null;
        var hud = EditorApplication.isPlaying ? UnityEngine.Object.FindAnyObjectByType<AlleyLevelHud>() : null;
        // Anything missing or throwing ends the check as a FAIL, so play
        // mode stops and the real save is always put back.
        if (step < 90 && (director == null || map == null || hud == null))
        {
            if (UnityEngine.Time.frameCount < 10) return;
            MapAbort("the director, map or HUD is missing from the scene");
            return;
        }
        try
        {
            MapCheckStep(step, director, map, hud);
        }
        catch (System.Exception error)
        {
            MapAbort(error.ToString());
        }
    }

    static void MapAbort(string why)
    {
        System.IO.File.WriteAllText(MapReport, "FAIL map check\n" + mapLog + "stopped: " + why + "\n");
        SessionState.SetInt(MapStepKey, 90);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }

    static void MapCheckStep(int step, AlleyChuteDirector director, AlleyMap map, AlleyLevelHud hud)
    {
        switch (step)
        {
            case 1:
            {
                if (UnityEngine.Time.frameCount < 10) return;
                mapOk = true;
                mapLog.Clear();
                mapErrors = 0;
                UnityEngine.Application.logMessageReceived += CountMapError;
                var title = UnityEngine.Object.FindAnyObjectByType<AlleyTitle>();
                MapExpect(title != null && title.Showing && !map.Showing, "the game opens on the title");
                MapExpect(director.Paused, "nothing falls behind the title");
                AlleySceneBuilder.Capture("/tmp/juicy-title.png");
                title.Continue();
                MapStep(101);
                return;
            }
            case 101:
            {
                MapExpect(map.Showing && director.Paused, "a tap on the title opens the map");
                var settings = UnityEngine.Object.FindAnyObjectByType<AlleySettings>();
                settings.Open();
                MapStep(102);
                return;
            }
            case 102:
            {
                var settings = UnityEngine.Object.FindAnyObjectByType<AlleySettings>();
                AlleySceneBuilder.Capture("/tmp/juicy-settings.png");
                bool sound = AudioPrefs.SoundOn;
                bool music = AudioPrefs.MusicOn;
                settings.ToggleSound();
                settings.ToggleMusic();
                MapExpect(AudioPrefs.SoundOn != sound && AudioPrefs.MusicOn != music, "settings switch sound and music");
                settings.ToggleSound();
                settings.ToggleMusic();
                MapExpect(AudioPrefs.SoundOn == sound && AudioPrefs.MusicOn == music, "and switch them back");
                settings.Close();
                MapExpect(!settings.Showing && map.Showing, "closing settings leaves the map");
                director.Progress.Restore(0);
                map.SetLook(AlleyMap.Look.Board);
                map.Show();
                MapStep(2);
                return;
            }
            case 2:
                AlleySceneBuilder.Capture("/tmp/juicy-map-board-0.png");
                director.Progress.Restore(3);
                // Clues as real wins would have left them: City 1 crystal clear, City 2 smudged.
                director.CaseFile.Restore(new[] { ClueRules.MaxTier, 1, ClueRules.MaxTier });
                map.Show();
                MapStep(3);
                return;
            case 3:
                AlleySceneBuilder.Capture("/tmp/juicy-map-board-3.png");
                director.Progress.Restore(0);
                map.SetLook(AlleyMap.Look.Streets);
                map.Show();
                MapStep(4);
                return;
            case 4:
                AlleySceneBuilder.Capture("/tmp/juicy-map-streets-0.png");
                director.Progress.Restore(3);
                map.Show();
                MapStep(5);
                return;
            case 5:
                AlleySceneBuilder.Capture("/tmp/juicy-map-streets-3.png");
                map.SetLook(AlleyMap.Look.Board);
                director.Progress.Restore(2);
                director.CaseFile.Restore(new[] { ClueRules.MaxTier, 1 });
                map.Show();
                MapExpect(!map.Choose(4), "a locked stop (City 5) does not start");
                MapExpect(map.Showing && director.Paused, "the map stays up after a locked tap");
                MapExpect(map.Choose(2), "the open stop (City 3) starts");
                MapExpect(!map.Showing && !director.Paused && director.LevelIndex == 2, "City 3 is playing with the map gone");
                MapStep(6, 40);
                return;
            case 6:
                AlleySceneBuilder.Capture("/tmp/juicy-map-playing.png");
                int target = director.Level.Target;
                for (int i = 0; i < target && director.Level.Playing; i++) director.Rules.Catch(ItemKind.Gem, i % 2, i % 2);
                MapStep(7, 20);
                return;
            case 7:
                MapExpect(hud.ShowingResult && director.Level.State == LevelState.Won, "the win panel shows");
                AlleySceneBuilder.Capture("/tmp/juicy-map-won.png");
                MapExpect(director.Progress.Beaten == 3, "winning City 3 beats it (3 beaten)");
                MapExpect(director.CaseFile.Found(2) && director.CaseFile.ClueAt(2).Tier == ClueRules.MaxTier,
                    "City 3's clue is found, crystal clear after an unbroken streak");
                hud.Continue();
                MapStep(8);
                return;
            case 8:
            {
                var reveal = UnityEngine.Object.FindAnyObjectByType<AlleyClueReveal>();
                MapExpect(reveal != null && reveal.Showing && !reveal.Revealed && !map.Showing, "a tap after the win shows the scratch card, not yet the map");
                AlleySceneBuilder.Capture("/tmp/juicy-reveal-covered.png");
                for (float x = 0.16f; x < 0.5f; x += 0.03f)
                    for (float y = 0.4f; y < 0.65f; y += 0.03f) reveal.ScratchAt(new UnityEngine.Vector2(x, y));
                MapExpect(!reveal.Revealed && reveal.Cleared > 0.3f && reveal.Cleared < 0.65f, "half a rub leaves the card covered (" + reveal.Cleared.ToString("0.00") + " cleared)");
                MapStep(9);
                return;
            }
            case 9:
            {
                var reveal = UnityEngine.Object.FindAnyObjectByType<AlleyClueReveal>();
                AlleySceneBuilder.Capture("/tmp/juicy-reveal-half.png");
                for (float x = 0.16f; x < 0.86f; x += 0.03f)
                    for (float y = 0.4f; y < 0.65f; y += 0.03f) reveal.ScratchAt(new UnityEngine.Vector2(x, y));
                MapExpect(reveal.Revealed && reveal.Cleared >= 0.999f, "rubbing most of it off reveals the clue");
                MapStep(10);
                return;
            }
            case 10:
            {
                var reveal = UnityEngine.Object.FindAnyObjectByType<AlleyClueReveal>();
                AlleySceneBuilder.Capture("/tmp/juicy-reveal-done.png");
                reveal.Continue();
                MapStep(11);
                return;
            }
            case 11:
            {
                AlleySceneBuilder.Capture("/tmp/juicy-map-after-win.png");
                MapExpect(map.Showing && director.Paused, "pinning the clue goes back to the map");
                MapExpect(director.Progress.IsOpen(3) && !director.Progress.IsOpen(4), "City 4 is open and City 5 still locked");
                var saved = HeistSave.FromJson(UnityEngine.PlayerPrefs.GetString(SaveKey, ""));
                MapExpect(saved != null && saved.placeBeaten == 3, "the save holds 3 beaten");
                MapExpect(saved != null && saved.placeClueTiers.Length == 6 && saved.placeClueTiers[2] == ClueRules.MaxTier && saved.placeClueTiers[3] == CaseFile.Unfound,
                    "the save holds City 3's clue and no clue for City 4");
                // A barely readable clue, for a still of the grime.
                var reveal = UnityEngine.Object.FindAnyObjectByType<AlleyClueReveal>();
                reveal.Show(4, new Clue { Symbol = director.CaseFile.ClueAt(4).Symbol, Tier = 0 });
                for (float x = 0.16f; x < 0.86f; x += 0.03f)
                    for (float y = 0.4f; y < 0.65f; y += 0.03f) reveal.ScratchAt(new UnityEngine.Vector2(x, y));
                MapStep(12);
                return;
            }
            case 12:
            {
                AlleySceneBuilder.Capture("/tmp/juicy-reveal-smudged.png");
                var reveal = UnityEngine.Object.FindAnyObjectByType<AlleyClueReveal>();
                reveal.Continue();
                // Every level beaten, with clues of every clarity, opens the vault.
                director.Progress.Restore(6);
                director.CaseFile.Restore(new[] { ClueRules.MaxTier, ClueRules.MaxTier, 2, 1, 0, 2 });
                map.Show();
                MapStep(13);
                return;
            }
            case 13:
            {
                AlleySceneBuilder.Capture("/tmp/juicy-map-vault-open.png");
                MapExpect(director.Progress.VaultOpen && !director.Progress.VaultCracked, "every level beaten opens the vault");
                var hack = UnityEngine.Object.FindAnyObjectByType<AlleyHack>();
                hack.ScriptNext(1);
                MapExpect(map.ChooseVault() && director.IsBoss && !director.Paused && !map.Showing, "tapping the vault starts the boss");
                MapStep(14, 30);
                return;
            }
            case 14:
            {
                int vaultTarget = director.Level.Target;
                for (int i = 0; i < vaultTarget && !director.Level.TargetReached; i++) director.Rules.Catch(ItemKind.Gem, i % 2, i % 2);
                MapExpect(director.Level.TargetReached && director.Level.Playing && director.Paused, "the vault's sort holds its win for the finale");
                MapExpect(director.Vault.Stage == VaultProgress.Stages - 1, "the sort locks the door's first six stages");
                MapExpect(director.Finale != null && director.Finale.Count == 6, "the finale asks for all six clues");
                MapStep(15, 3, 1.6f);
                return;
            }
            case 15:
                AlleySceneBuilder.Capture("/tmp/juicy-finale.png");
                mapGiveUpAt = UnityEngine.Time.time + 30f;
                MapStep(16);
                return;
            case 16:
                if (!hud.ShowingResult)
                {
                    if (UnityEngine.Time.time > mapGiveUpAt) MapAbort("the finale never finished");
                    return;
                }
                MapExpect(director.Level.State == LevelState.Won && director.Finale.Cracked && director.Finale.AlarmsLeft == VaultFinale.Alarms - 1,
                    "one wrong tap sounds an alarm and the right ones still crack it");
                MapExpect(director.Progress.VaultCracked && director.Vault.IsCracked, "the vault is cracked and the door turns its last stage");
                {
                    var saved = HeistSave.FromJson(UnityEngine.PlayerPrefs.GetString(SaveKey, ""));
                    MapExpect(saved != null && saved.vaultCracked, "the save holds the cracked vault");
                }
                MapStep(17, 3, 1.2f);
                return;
            case 17:
                AlleySceneBuilder.Capture("/tmp/juicy-vault-cracked.png");
                hud.Continue();
                MapStep(18);
                return;
            case 18:
                AlleySceneBuilder.Capture("/tmp/juicy-map-vault-cracked.png");
                MapExpect(map.Showing && !UnityEngine.Object.FindAnyObjectByType<AlleyClueReveal>().Showing, "after the vault, a tap goes straight to the map (no clue to scratch)");
                MapExpect(map.Choose(0), "City 1 replays after the crack");
                // The crack swings the door open and shut over about 6 seconds first.
                MapStep(19, 3, 7f);
                return;
            case 19:
            {
                var door = UnityEngine.Object.FindAnyObjectByType<AlleyVaultDoor>();
                MapExpect(director.Vault.Stage == 0 && door.LockedShown() == 0, "the next attempt starts on a fresh door (" + door.LockedShown() + " stages shown locked)");
                UnityEngine.Application.logMessageReceived -= CountMapError;
                MapExpect(mapErrors == 0, "no errors logged during play (" + mapErrors + (mapErrors > 0 ? ", first: " + mapFirstError : "") + ")");
                System.IO.File.WriteAllText(MapReport, (mapOk ? "PASS" : "FAIL") + " map check\n" + mapLog +
                    "stills /tmp/juicy-title.png /tmp/juicy-settings.png /tmp/juicy-map-{board-0,board-3,streets-0,streets-3,playing,won,after-win,vault-open,vault-cracked}.png " +
                    "/tmp/juicy-reveal-{covered,half,done,smudged}.png /tmp/juicy-finale.png /tmp/juicy-vault-cracked.png\n");
                SessionState.SetInt(MapStepKey, 90);
                EditorApplication.isPlaying = false;
                return;
            }
            case 90:
                if (EditorApplication.isPlaying) return;
                SessionState.SetInt(MapStepKey, 0);
                EditorApplication.update -= MapCheckTick;
                string backup = SessionState.GetString("JuicyMapBackup", "");
                if (backup.Length > 0) UnityEngine.PlayerPrefs.SetString(SaveKey, backup);
                else UnityEngine.PlayerPrefs.DeleteKey(SaveKey);
                UnityEngine.PlayerPrefs.Save();
                bool passed = System.IO.File.Exists(MapReport) && System.IO.File.ReadAllText(MapReport).StartsWith("PASS");
                if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
                return;
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
