using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ThemeHookup
{
    const string HeistPath = "Assets/Themes/Theme_Heist.asset";
    const string ClimberPath = "Assets/Themes/Theme_Climber.asset";
    const string ShowFlag = "/tmp/juicy-show-climber";
    const string HeistFlag = "/tmp/juicy-show-heist";
    const string PayoutFlag = "/tmp/juicy-show-payout";
    const string StopFlag = "/tmp/juicy-stop-play";
    const string LogPath = "/tmp/juicy-theme-play.txt";

    static bool enterQueued;
    static bool ticking;
    static bool applied;
    static bool payoutHandsOff;
    static int waits;
    static float simAtApply;

    static ThemeHookup()
    {
        EditorApplication.update += Watch;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("Juicy/Theme/Apply Heist")]
    public static void ApplyHeist()
    {
        Apply(HeistPath);
    }

    [MenuItem("Juicy/Theme/Apply Climber")]
    public static void ApplyClimber()
    {
        Apply(ClimberPath);
    }

    static void Watch()
    {
        if (File.Exists(StopFlag))
        {
            enterQueued = false;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                File.Delete(StopFlag);
            return;
        }

        if (!WantShot())
        {
            enterQueued = false;
            return;
        }

        if (EditorApplication.isPlaying)
        {
            BeginTick();
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            enterQueued = true;
            return;
        }

        // Enter on the second quiet update. A delayCall never fired on a bare
        // refresh, which left the queued flag stuck and play never started.
        if (!enterQueued)
        {
            enterQueued = true;
            return;
        }

        enterQueued = false;
        File.AppendAllText(LogPath, "entering play\n");
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        FocusGameView();
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        BeginTick();
    }

    static void BeginTick()
    {
        if (File.Exists(ShowFlag) || File.Exists(HeistFlag)) payoutHandsOff = false;
        if (ticking || payoutHandsOff || !WantShot()) return;
        ticking = true;
        applied = false;
        waits = 0;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        // A console error pause, or the editor losing focus, freezes the sim
        // on the first frame. The still then shows the rest pose.
        Application.runInBackground = true;
        if (EditorApplication.isPaused)
            EditorApplication.isPaused = false;

        if (!EditorApplication.isPlaying)
        {
            ticking = false;
            EditorApplication.update -= Tick;
            return;
        }

        waits++;
        if (waits == 12) FocusGameView();
        if (GameCoreManager.Instance == null)
        {
            if (waits < 180) return;
            File.WriteAllText(LogPath, "manager missing\n");
            ticking = false;
            EditorApplication.update -= Tick;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            return;
        }

        if (!applied)
        {
            if (File.Exists(ShowFlag)) Apply(ClimberPath);
            applied = true;
            waits = 0;
            simAtApply = Time.time;
            string name = GameCoreManager.Instance.ActiveTheme != null
                ? GameCoreManager.Instance.ActiveTheme.themeName
                : "none";
            File.WriteAllText(LogPath, "applied " + name + "\n");
            return;
        }

        if (File.Exists(PayoutFlag) && !File.Exists(ShowFlag) && !File.Exists(HeistFlag))
        {
            // The payout shot is taken by the ticker. The game view has to be
            // in front or that capture never lands on disk.
            if (waits == 30 || waits == 90 || waits == 150) FocusGameView();
            if (waits < 240) return;
            payoutHandsOff = true;
            ticking = false;
            EditorApplication.update -= Tick;
            File.AppendAllText(LogPath, "payout handoff\n");
            return;
        }

        // Editor ticks can all land inside the first frame. Wait until the
        // sim itself has moved, or the still is the rest pose every time.
        if (waits < 30 || Time.time < simAtApply + 0.6f) return;
        ticking = false;
        EditorApplication.update -= Tick;
        string shot = File.Exists(ShowFlag) ? "/tmp/juicy-theme-climber.png" : "/tmp/juicy-theme-heist.png";
        if (File.Exists(ShowFlag)) File.Delete(ShowFlag);
        if (File.Exists(HeistFlag)) File.Delete(HeistFlag);
        FocusGameView();
        ScreenCapture.CaptureScreenshot(shot);
        File.AppendAllText(LogPath, "shot " + shot + "\n");
    }

    static void FocusGameView()
    {
        Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (gameView == null) return;
        EditorWindow.GetWindow(gameView).Focus();
    }

    static bool WantShot()
    {
        return File.Exists(ShowFlag) || File.Exists(HeistFlag) || File.Exists(PayoutFlag);
    }

    static void Apply(string path)
    {
        ThemeConfig theme = AssetDatabase.LoadAssetAtPath<ThemeConfig>(path);
        if (theme == null)
        {
            Debug.LogWarning("Missing theme asset " + path);
            return;
        }

        if (Application.isPlaying)
        {
            if (GameCoreManager.Instance != null) GameCoreManager.Instance.ApplyTheme(theme);
            return;
        }

        GameCoreManager manager = UnityEngine.Object.FindFirstObjectByType<GameCoreManager>();
        if (manager == null)
        {
            Debug.LogWarning("GameCoreManager is not in the open scene.");
            return;
        }

        SerializedObject data = new SerializedObject(manager);
        data.FindProperty("activeTheme").objectReferenceValue = theme;
        data.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Debug.Log("Theme assigned: " + theme.themeName);
    }
}
