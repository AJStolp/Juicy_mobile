using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RewardArtPreview
{
    const string Flag = "/tmp/juicy-show-reward-art";
    static int waits;

    static RewardArtPreview()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("Juicy/Preview Reward Art")]
    public static void Preview()
    {
        File.WriteAllText(Flag, "1");
        FocusGameView();
        SetPhoneGameView();
        File.WriteAllText("/tmp/juicy-preview-branch.txt",
            "playing=" + EditorApplication.isPlaying + " paused=" + EditorApplication.isPaused);
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += EnterWhenReady;
    }

    static void EnterWhenReady()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += EnterWhenReady;
            return;
        }

        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        if (!File.Exists(Flag)) return;
        File.Delete(Flag);
        waits = 0;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= Tick;
            return;
        }

        waits++;
        if (waits < 8) return;
        EditorApplication.update -= Tick;
        FocusGameView();
        SetPhoneGameView();

        int shown = 0;
        JuicyUIManager ui = UnityEngine.Object.FindFirstObjectByType<JuicyUIManager>();
        if (ui != null) shown = ui.ShowRewardPortrait();

        HeistCombativeManager heist = UnityEngine.Object.FindFirstObjectByType<HeistCombativeManager>();
        if (heist != null) heist.PreviewHeroCoins();

        File.WriteAllText("/tmp/juicy-reward-preview.txt", "sprites " + shown + "\n");
        ScreenCapture.CaptureScreenshot("/tmp/juicy-game-view.png");
    }

    static void FocusGameView()
    {
        System.Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (gameView == null) return;
        EditorWindow.GetWindow(gameView).Focus();
    }

    static void SetPhoneGameView()
    {
        try
        {
            Assembly editor = typeof(Editor).Assembly;
            Type gameViewType = editor.GetType("UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            var notes = new System.Text.StringBuilder();
            bool applied = false;

            Type cursor = gameViewType;
            while (cursor != null && cursor != typeof(object))
            {
                foreach (MethodInfo method in cursor.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.Name != "SetCustomResolution") continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    notes.Append(cursor.Name).Append(" SetCustomResolution");
                    foreach (ParameterInfo parameter in parameters)
                        notes.Append(" ").Append(parameter.ParameterType.Name);
                    notes.AppendLine();

                    if (applied) continue;
                    if (parameters.Length == 2 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType == typeof(int))
                    {
                        method.Invoke(method.IsStatic ? null : gameView, new object[] { 1080, 1920 });
                        applied = true;
                    }
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Vector2))
                    {
                        method.Invoke(method.IsStatic ? null : gameView, new object[] { new Vector2(1080f, 1920f) });
                        applied = true;
                    }
                    else if (parameters.Length >= 2 && parameters[0].ParameterType == typeof(Vector2) && parameters[1].ParameterType == typeof(string))
                    {
                        object[] args = new object[parameters.Length];
                        args[0] = new Vector2(1080f, 1920f);
                        args[1] = "Phone 1080x1920";
                        method.Invoke(method.IsStatic ? null : gameView, args);
                        applied = true;
                    }
                }

                cursor = cursor.BaseType;
            }

            notes.Append("applied ").Append(applied);
            File.WriteAllText("/tmp/juicy-gameview-size.txt", notes.ToString());
            gameView.Repaint();
        }
        catch (Exception error)
        {
            File.WriteAllText("/tmp/juicy-gameview-size.txt", error.ToString());
        }
    }
}
