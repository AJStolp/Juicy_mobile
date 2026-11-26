using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

// Verification run for the level. Touch /tmp/juicy-alley-bot-run and the open
// editor enters Play mode with the director on auto play. It plays level
// attempts in two parts, then writes /tmp/juicy-bot.txt and stops:
//   1. Numbers: Attempts tries by a careful bot (10% mistakes) and a sloppy
//      one (30%), at 4x. Each attempt's result, gems to spare or short, and
//      length in game seconds.
//   2. Film: one won and one failed attempt at 1x with a fixed 30 frames per
//      game second. The last FilmSeconds of the attempt and the result panel
//      after it land in /tmp/juicy-level-film/<won|failed>/ as numbered jpgs,
//      ready for ffmpeg.
// Times in the report are game seconds, so they read as a real player's pace.
[InitializeOnLoad]
public static class AlleyBotRun
{
    const string RunFlag = "/tmp/juicy-alley-bot-run";
    const string AutoPlayFlag = "/tmp/juicy-alley-bot";
    const string Report = "/tmp/juicy-bot.txt";
    const string Progress = "/tmp/juicy-bot-progress.txt";
    const string FilmRoot = "/tmp/juicy-level-film";
    const string HomeStill = "/tmp/juicy-level-home.png";
    const float Speed = 4f;
    const int Attempts = 8;
    const float Careful = 0.1f;
    const float Sloppy = 0.3f;
    const float FilmWinMistakes = 0.05f;
    const float FilmFailMistakes = 0.4f;
    const int FilmTries = 3;
    const int FilmFps = 30;
    const float FilmSeconds = 12f;
    const float ResultSeconds = 3f;
    const float MaxGameSeconds = 7200f;

    struct Run
    {
        public float Mistakes;
        public bool Film;
        public bool WantWin;
    }

    static int errors;
    static string firstError = "";

    // Play session state. Statics reset on entering Play, so these start clean.
    static bool hooked;
    static readonly List<Run> plan = new List<Run>();
    static readonly List<string> lines = new List<string>();
    static readonly List<string> filmLines = new List<string>();
    static int current = -1;
    static int filmTry;
    static float nextAt = -1f;
    static float nextProgress;
    static float attemptStart;
    static int ring;
    static int afterEnd = -1;
    static bool homeTaken;
    static RenderTexture filmBuffer;
    static Texture2D filmImage;

    static AlleyBotRun()
    {
        EditorApplication.update += Watch;
        Application.logMessageReceived += Count;
    }

    static void Count(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool("JuicyBot", false)) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        errors++;
        if (firstError.Length == 0) firstError = message;
    }

    static void Watch()
    {
        // Batch runs (AlleyBatch.Bot) quit once the report is written.
        if (Application.isBatchMode && !EditorApplication.isPlaying && File.Exists(Report) && SessionState.GetBool("JuicyBotBatch", false))
        {
            SessionState.SetBool("JuicyBotBatch", false);
            EditorApplication.Exit(errors > 0 ? 1 : 0);
            return;
        }
        if (!EditorApplication.isPlaying)
        {
            if (!File.Exists(RunFlag) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(RunFlag);
            File.WriteAllText(AutoPlayFlag, "on");
            File.WriteAllText(Progress, "entering play " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
            if (Directory.Exists(FilmRoot)) Directory.Delete(FilmRoot, true);
            SessionState.SetBool("JuicyBot", true);
            EditorApplication.isPlaying = true;
            return;
        }

        if (!SessionState.GetBool("JuicyBot", false)) return;
        var director = Object.FindAnyObjectByType<AlleyChuteDirector>();
        if (director == null || director.Rules == null || director.Level == null) return;
        if (!hooked) Hook(director);

        float now = Time.time;
        if (now >= nextProgress)
        {
            nextProgress = now + 10f;
            File.AppendAllText(Progress, System.DateTime.Now.ToString("HH:mm:ss") + " game " + now.ToString("0") + "s attempt " + (current + 1) + "/" + plan.Count +
                " sorted " + director.Level.Sorted + " errors " + errors + "\n");
        }
        if (nextAt >= 0f && now >= nextAt)
        {
            nextAt = -1f;
            Next(director);
        }
        if (current >= plan.Count || now >= MaxGameSeconds) Finish(director, now);
    }

    static void Hook(AlleyChuteDirector director)
    {
        hooked = true;
        // AlleyBatch.BotFilm skips the numbers and only films.
        bool filmOnly = SessionState.GetBool("JuicyBotFilmOnly", false);
        if (!filmOnly)
        {
            for (int i = 0; i < Attempts; i++) plan.Add(new Run { Mistakes = Careful });
            for (int i = 0; i < Attempts; i++) plan.Add(new Run { Mistakes = Sloppy });
        }
        plan.Add(new Run { Mistakes = FilmWinMistakes, Film = true, WantWin = true });
        plan.Add(new Run { Mistakes = FilmFailMistakes, Film = true, WantWin = false });
        director.AttemptEnded += level => Ended(director, level);
        FilmEveryFrame(true);
        Next(director);
    }

    static void Next(AlleyChuteDirector director)
    {
        current++;
        if (current >= plan.Count) return;
        Run run = plan[current];
        director.AutoMistakes = run.Mistakes;
        if (run.Film)
        {
            Time.timeScale = 1f;
            Time.captureFramerate = FilmFps;
            ring = 0;
            afterEnd = -1;
            string folder = FilmFolder(run, false);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
        }
        else
        {
            Time.captureFramerate = 0;
            Time.timeScale = Speed;
        }
        attemptStart = Time.time;
        director.Retry();
    }

    static void Ended(AlleyChuteDirector director, LevelRules level)
    {
        Run run = plan[current];
        bool won = level.State == LevelState.Won;
        string line = "mistakes " + run.Mistakes.ToString("0.00") + " " + (won ? "won" : "failed") +
            " sorted " + level.Sorted + "/" + level.Target + " lost " + level.Lost +
            (won ? " spare " + level.GemsLeft : " short " + level.ShortBy) +
            " length " + level.Elapsed.ToString("0") + "s balance " + director.Rules.Balance.ToString("0");
        if (!run.Film)
        {
            lines.Add(line);
            nextAt = Time.time + 0.5f;
            return;
        }
        filmLines.Add("film try " + (filmTry + 1) + ": " + line);
        // Keep filming the result panel, then keep the film only if the
        // attempt came out the way this film needs.
        afterEnd = 0;
        if (won == run.WantWin || filmTry + 1 >= FilmTries)
        {
            filmTry = 0;
            return;
        }
        filmTry++;
        afterEnd = -1;
        current--;
        nextAt = Time.time + 0.5f;
    }

    static string FilmFolder(Run run, bool done) =>
        FilmRoot + "/" + (run.WantWin ? "won" : "failed") + (done ? "" : "-ring");

    // During a film attempt, every frame goes into a ring of the last
    // FilmSeconds, and the result panel is filmed after the end. Then the ring
    // is written out in order.
    static void Film()
    {
        if (!EditorApplication.isPlaying || current < 0 || current >= plan.Count || !plan[current].Film) return;
        Run run = plan[current];
        var director = Object.FindAnyObjectByType<AlleyChuteDirector>();
        if (director == null) return;
        if (!homeTaken && run.WantWin && Time.time - attemptStart >= 20f)
        {
            homeTaken = true;
            AlleySceneBuilder.Capture(HomeStill);
        }
        if (afterEnd < 0 && !director.Level.Playing) return;
        int slots = Mathf.RoundToInt(FilmSeconds * FilmFps);
        string ringFolder = FilmFolder(run, false);
        if (afterEnd < 0)
        {
            Shoot(ringFolder + "/ring_" + (ring % slots).ToString("0000") + ".jpg");
            ring++;
            return;
        }
        Shoot(ringFolder + "/after_" + afterEnd.ToString("0000") + ".jpg");
        afterEnd++;
        if (afterEnd < Mathf.RoundToInt(ResultSeconds * FilmFps)) return;

        string folder = FilmFolder(run, true);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        int frame = 0;
        int first = Mathf.Max(0, ring - slots);
        for (int i = first; i < ring; i++)
            File.Move(ringFolder + "/ring_" + (i % slots).ToString("0000") + ".jpg", folder + "/frame_" + (frame++).ToString("0000") + ".jpg");
        for (int i = 0; i < afterEnd; i++)
            File.Move(ringFolder + "/after_" + i.ToString("0000") + ".jpg", folder + "/frame_" + (frame++).ToString("0000") + ".jpg");
        Directory.Delete(ringFolder, true);
        filmLines.Add("film " + Path.GetFileName(folder) + ": " + frame + " frames");
        afterEnd = -1;
        nextAt = Time.time + 0.1f;
    }

    // A 540x960 frame of the main camera, which carries the level display.
    static void Shoot(string path)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        if (filmBuffer == null) filmBuffer = new RenderTexture(540, 960, 24);
        if (filmImage == null) filmImage = new Texture2D(540, 960, TextureFormat.RGB24, false);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = filmBuffer;
        camera.Render();
        RenderTexture.active = filmBuffer;
        filmImage.ReadPixels(new Rect(0f, 0f, 540f, 960f), 0, 0);
        filmImage.Apply();
        camera.targetTexture = previous;
        RenderTexture.active = null;
        File.WriteAllBytes(path, filmImage.EncodeToJPG(88));
    }

    // Runs Film once per game frame, after the frame's gameplay, so a film
    // gets exactly one picture per frame.
    static void FilmEveryFrame(bool on)
    {
        PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            if (loop.subSystemList[i].type != typeof(PostLateUpdate)) continue;
            var systems = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList);
            systems.RemoveAll(system => system.type == typeof(AlleyBotRun));
            if (on) systems.Add(new PlayerLoopSystem { type = typeof(AlleyBotRun), updateDelegate = Film });
            loop.subSystemList[i].subSystemList = systems.ToArray();
        }
        PlayerLoop.SetPlayerLoop(loop);
    }

    static void Finish(AlleyChuteDirector director, float now)
    {
        FilmEveryFrame(false);
        File.WriteAllText(Report, Describe(director, now));
        SessionState.SetBool("JuicyBot", false);
        Time.timeScale = 1f;
        Time.captureFramerate = 0;
        if (File.Exists(AutoPlayFlag)) File.Delete(AutoPlayFlag);
        EditorApplication.isPlaying = false;
    }

    static string Describe(AlleyChuteDirector director, float now)
    {
        var log = new System.Text.StringBuilder();
        var level = director.Level;
        log.AppendLine("level target " + level.Target + " of " + level.GemLimit + " gems, seed " + LevelRules.DefaultSeed);
        log.AppendLine("numbers at " + Speed + "x, " + Attempts + " attempts per bot; film at 1x, " + FilmFps + " fps");
        log.AppendLine("game seconds " + now.ToString("0") + "; spawned " + director.Spawned + " flicks " + director.Flicks + " misses " + director.Misses);
        log.AppendLine();
        log.AppendLine("summary:");
        foreach (float mistakes in new[] { Careful, Sloppy })
            log.AppendLine("  " + Summary(mistakes));
        log.AppendLine();
        log.AppendLine("attempts:");
        foreach (string line in lines) log.AppendLine("  " + line);
        log.AppendLine();
        log.AppendLine("film:");
        foreach (string line in filmLines) log.AppendLine("  " + line);
        log.AppendLine("home still " + (homeTaken ? HomeStill : "not taken"));
        log.AppendLine("finished all " + plan.Count + " planned attempts " + (current >= plan.Count));
        log.AppendLine("errors " + errors + (errors > 0 ? " first: " + firstError : ""));
        return log.ToString();
    }

    static string Summary(float mistakes)
    {
        string key = "mistakes " + mistakes.ToString("0.00") + " ";
        int wins = 0, total = 0;
        foreach (string line in lines)
        {
            if (!line.StartsWith(key)) continue;
            total++;
            if (line.Contains(" won ")) wins++;
        }
        return key + "won " + wins + "/" + total;
    }
}
