using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Verification run for the alley game. Touch /tmp/juicy-alley-bot-run and the
// open editor enters Play mode with the director on auto play at 4x speed. It
// plays until the first vault cracks (or 25 game minutes), captures frames at
// the big moments, writes /tmp/juicy-bot.txt, then stops. Times in the report
// are game seconds, so they read as a real player's pace.
[InitializeOnLoad]
public static class AlleyBotRun
{
    const string RunFlag = "/tmp/juicy-alley-bot-run";
    const string AutoPlayFlag = "/tmp/juicy-alley-bot";
    const string Report = "/tmp/juicy-bot.txt";
    const string Progress = "/tmp/juicy-bot-progress.txt";
    const float Speed = 4f;
    const float MaxGameSeconds = 1500f;
    const float AfterCrack = 6f;

    static int errors;
    static string firstError = "";

    // Play session state. Statics reset on entering Play, so these start clean.
    static bool hooked;
    static readonly List<string> events = new List<string>();
    static readonly List<float> lockTimes = new List<float>();
    static float crackTime = -1f;
    static int maxDynamite;
    static readonly Dictionary<string, float> shots = new Dictionary<string, float>();
    static float nextProgress;

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
            SessionState.SetBool("JuicyBot", true);
            EditorApplication.isPlaying = true;
            return;
        }

        if (!SessionState.GetBool("JuicyBot", false)) return;
        var director = Object.FindAnyObjectByType<AlleyChuteDirector>();
        if (director == null || director.Rules == null) return;
        if (!hooked) Hook(director);

        float now = Time.time;
        var plinko = Object.FindAnyObjectByType<AlleyPlinko>();
        if (now >= nextProgress)
        {
            nextProgress = now + 10f;
            File.AppendAllText(Progress, System.DateTime.Now.ToString("HH:mm:ss") + " game " + now.ToString("0") + "s stage " + director.Vault.Stage +
                " turns " + director.Vault.Turns + " leg " + director.Rules.Leg + " plinko " + (plinko != null && plinko.Running) + " errors " + errors + "\n");
        }
        maxDynamite = Mathf.Max(maxDynamite, director.Rules.GearCount(GearKind.Dynamite));
        if (plinko != null && plinko.Running && plinko.CoinsDropped > 0 && !shots.ContainsKey("plinko"))
        {
            Shoot("plinko", now, 0.3f);
            Camera cam = Camera.main;
            var view = GameObject.Find("Plinko_View");
            events.Add(now.ToString("0") + "s plinko camera " + cam.transform.position.ToString("F2") + " fov " + cam.fieldOfView.ToString("0") +
                " view " + (view != null ? view.transform.position.ToString("F2") : "none"));
        }
        if (now >= 12f) Shoot("street", now, 0f);

        var due = new List<string>();
        foreach (var pair in shots)
            if (pair.Value >= 0f && now >= pair.Value) due.Add(pair.Key);
        foreach (string name in due)
        {
            Capture("/tmp/juicy-bot-" + name + ".png");
            shots[name] = -1f;
        }

        bool done = (crackTime >= 0f && now >= crackTime + AfterCrack) || now >= MaxGameSeconds;
        if (!done) return;
        File.WriteAllText(Report, Describe(director, plinko, now));
        SessionState.SetBool("JuicyBot", false);
        Time.timeScale = 1f;
        if (File.Exists(AutoPlayFlag)) File.Delete(AutoPlayFlag);
        EditorApplication.isPlaying = false;
    }

    static void Hook(AlleyChuteDirector director)
    {
        hooked = true;
        Time.timeScale = Speed;
        // Sloppier than the default so the run sees busts and rollbacks.
        director.AutoMistakes = 0.3f;
        director.Vault.StageLocked += stage =>
        {
            lockTimes.Add(Time.time);
            events.Add(Time.time.ToString("0") + "s stage " + stage + " locked");
            if (stage == 0) Shoot("bolt", Time.time, 0.35f);
        };
        director.Vault.RolledBack += (stage, fraction) => events.Add(Time.time.ToString("0") + "s rollback on stage " + stage + " to " + fraction.ToString("0.00"));
        director.Vault.Cracked += heist =>
        {
            crackTime = Time.time;
            events.Add(Time.time.ToString("0") + "s vault " + heist + " cracked");
            Shoot("crack", Time.time, 2.4f);
        };
        director.Rules.LegAdvanced += (leg, chutes) => events.Add(Time.time.ToString("0") + "s leg " + leg + ", plinko coins " + director.Rules.LastLegCoins);
    }

    // Queues a capture once per name, delay seconds from now.
    static void Shoot(string name, float now, float delay)
    {
        if (shots.ContainsKey(name)) return;
        shots[name] = now + delay;
    }

    static string Describe(AlleyChuteDirector director, AlleyPlinko plinko, float now)
    {
        var reactions = Object.FindAnyObjectByType<AlleyReactions>();
        var door = Object.FindAnyObjectByType<AlleyVaultDoor>();
        HeistSortRules rules = director.Rules;
        VaultProgress vault = director.Vault;
        var log = new System.Text.StringBuilder();
        log.AppendLine("game seconds " + now.ToString("0") + " at " + Speed + "x");
        log.AppendLine("spawned " + director.Spawned + " flicks " + director.Flicks + " misses " + director.Misses);
        log.AppendLine("balance " + rules.Balance.ToString("0") + " leg " + rules.Leg + " vat fills " + rules.VatFills);
        log.AppendLine("vault heist " + vault.Heist + " stage " + vault.Stage + " turns " + vault.Turns + "/" + vault.TurnsForStage);
        log.AppendLine("vault 1 " + (crackTime >= 0f ? "cracked at " + crackTime.ToString("0") + "s (" + (crackTime / 60f).ToString("0.0") + " min)" : "not cracked"));
        var gaps = new System.Text.StringBuilder();
        float last = 0f;
        foreach (float t in lockTimes) { gaps.Append((t - last).ToString("0") + "s "); last = t; }
        log.AppendLine("seconds per stage " + gaps);
        if (door != null) log.AppendLine("door pops " + door.Pops + " rollbacks " + door.Rollbacks + " cracks " + door.Cracks);
        if (plinko != null)
        {
            log.AppendLine("plinko rounds " + plinko.Rounds + " coins " + plinko.CoinsDropped + " vault hits " + plinko.VaultHits +
                " slots " + string.Join(",", plinko.SlotHits));
        }
        log.AppendLine("gear dynamite " + rules.GearCount(GearKind.Dynamite) + " (max held " + maxDynamite + ", cap " + rules.DynamiteCap + ")" +
            " drill " + rules.GearCount(GearKind.Drill) + " bribe " + rules.GearCount(GearKind.Bribe));
        if (reactions != null) log.AppendLine("raccoon pushHeavy " + reactions.PushHeavies + " busts " + reactions.Busts);
        log.AppendLine("particles " + ParticleCount());
        log.AppendLine("raccoon " + RaccoonFeet());
        log.AppendLine("saved " + PlayerPrefs.GetString("Juicy.HeistSave.Bot", "none"));
        log.AppendLine("errors " + errors + (errors > 0 ? " first: " + firstError : ""));
        log.AppendLine("events:");
        foreach (string e in events) log.AppendLine("  " + e);
        return log.ToString();
    }

    // Lowest point of the posed raccoon mesh and the torch's tilt from upright.
    static string RaccoonFeet()
    {
        GameObject raccoon = GameObject.Find("Raccoon_Mascot");
        if (raccoon == null) return "missing";
        var skin = raccoon.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh();
        skin.BakeMesh(baked, true);
        float lowest = float.MaxValue, highest = float.MinValue;
        foreach (Vector3 v in baked.vertices)
        {
            float y = skin.transform.TransformPoint(v).y;
            lowest = Mathf.Min(lowest, y);
            highest = Mathf.Max(highest, y);
        }
        Object.DestroyImmediate(baked);
        var torch = raccoon.GetComponentInChildren<TorchGrip>();
        string tilt = torch != null ? Vector3.Angle(torch.transform.up, Vector3.up).ToString("0") : "none";
        var animator = raccoon.GetComponentInChildren<Animator>();
        string bones = "";
        if (animator != null && animator.isHuman)
        {
            Transform l = animator.GetBoneTransform(HumanBodyBones.LeftFoot), r = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (l != null && r != null) bones = " feet bones " + ((l.position + r.position) * 0.5f).ToString("F2");
            var stand = raccoon.GetComponent<StandOnSpot>();
            if (stand != null) bones += " ankle " + stand.AnkleHeight.ToString("0.00");
        }
        string cover = "";
        GameObject manhole = GameObject.Find("Manhole");
        if (manhole != null)
        {
            var mr = manhole.GetComponentsInChildren<MeshRenderer>();
            Bounds mb = mr[0].bounds;
            foreach (var m in mr) mb.Encapsulate(m.bounds);
            cover = " cover top " + mb.max.y.ToString("0.00") + " centre " + mb.center.ToString("F2");
        }
        return "human " + (animator != null && animator.isHuman) + bones + cover + " feet y " + lowest.ToString("0.00") + " top y " + highest.ToString("0.00") + " torch tilt " + tilt;
    }

    static string ParticleCount()
    {
        var parts = new System.Text.StringBuilder();
        foreach (var system in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            parts.Append(system.name + "=" + system.particleCount + (system.isPlaying ? "" : " (stopped)") + " ");
        return parts.Length > 0 ? parts.ToString() : "none";
    }

    static void Capture(string path)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        RenderTexture buffer = new RenderTexture(1080, 1920, 24);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = buffer;
        camera.Render();
        RenderTexture.active = buffer;
        Texture2D image = new Texture2D(buffer.width, buffer.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, buffer.width, buffer.height), 0, 0);
        image.Apply();
        camera.targetTexture = previous;
        RenderTexture.active = null;
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image);
        buffer.Release();
    }
}
