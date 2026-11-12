using System.IO;
using UnityEditor;
using UnityEngine;

// Verification run for the alley game. Touch /tmp/juicy-alley-bot-run and the
// open editor enters Play mode with the director on auto play, captures frames
// at 8, 25 and 50 seconds, writes /tmp/juicy-bot.txt at 60 seconds, then stops.
[InitializeOnLoad]
public static class AlleyBotRun
{
    const string RunFlag = "/tmp/juicy-alley-bot-run";
    const string AutoPlayFlag = "/tmp/juicy-alley-bot";
    const string Report = "/tmp/juicy-bot.txt";
    const float Duration = 60f;
    static readonly float[] Shots = { 8f, 25f, 50f };

    static int errors;
    static string firstError = "";

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
        if (!EditorApplication.isPlaying)
        {
            if (!File.Exists(RunFlag) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(RunFlag);
            File.WriteAllText(AutoPlayFlag, "on");
            SessionState.SetBool("JuicyBot", true);
            SessionState.SetFloat("JuicyBotStart", -1f);
            SessionState.SetInt("JuicyBotShot", 0);
            EditorApplication.isPlaying = true;
            return;
        }

        if (!SessionState.GetBool("JuicyBot", false)) return;
        float now = (float)EditorApplication.timeSinceStartup;
        float start = SessionState.GetFloat("JuicyBotStart", -1f);
        if (start < 0f)
        {
            SessionState.SetFloat("JuicyBotStart", now);
            return;
        }
        float elapsed = now - start;

        int shot = SessionState.GetInt("JuicyBotShot", 0);
        if (shot < Shots.Length && elapsed >= Shots[shot])
        {
            Capture("/tmp/juicy-bot-" + shot + ".png");
            SessionState.SetInt("JuicyBotShot", shot + 1);
        }

        if (elapsed < Duration) return;
        File.WriteAllText(Report, Describe(elapsed));
        SessionState.SetBool("JuicyBot", false);
        if (File.Exists(AutoPlayFlag)) File.Delete(AutoPlayFlag);
        EditorApplication.isPlaying = false;
    }

    static string Describe(float elapsed)
    {
        var director = Object.FindAnyObjectByType<AlleyChuteDirector>();
        var reactions = Object.FindAnyObjectByType<AlleyReactions>();
        var display = Object.FindAnyObjectByType<AlleyScoreDisplay>();
        if (director == null || director.Rules == null) return "no director in the scene\nerrors " + errors + " " + firstError;
        HeistSortRules rules = director.Rules;
        return
            "seconds " + elapsed.ToString("0") + "\n" +
            "spawned " + director.Spawned + " flicks " + director.Flicks + " misses " + director.Misses + "\n" +
            "balance " + rules.Balance.ToString("0") + " shown " + (display != null ? display.Shown.ToString("0") : "none") + "\n" +
            "leg " + rules.Leg + " chutes " + rules.Chutes + " vat fills " + rules.VatFills + "\n" +
            "heat " + rules.Heat.ToString("0") + " streak " + rules.Streak + " tier " + rules.Tier + "\n" +
            "word " + rules.ActiveWord + " letters " + rules.LettersFilled + "\n" +
            (reactions != null
                ? "raccoon pushLight " + reactions.PushLights + " pushHeavy " + reactions.PushHeavies + " legs " + reactions.Legs + " busts " + reactions.Busts + "\n"
                : "no reactions\n") +
            "particles " + ParticleCount() + "\n" +
            "raccoon " + RaccoonFeet() + "\n" +
            "errors " + errors + (errors > 0 ? " first: " + firstError : "") + "\n";
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
