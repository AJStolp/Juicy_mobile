using System.Collections.Generic;
using UnityEngine;

// Every sound effect in the game, as one call each from anywhere. The clips
// are synthesized by ArtSource/Audio/build_audio.py into Resources/Audio, so
// they are original and owe no license or credit; the keypad's beep and buzz
// are made here. Nothing plays while sound is off (AudioPrefs).
public class JuicyAudioHooks : MonoBehaviour
{
    public static readonly string[] ClipNames =
    {
        "sort_ding", "sort_wrong", "miss", "flick", "boom", "cash", "streak_up", "level_won", "level_failed",
        "scratch", "reveal", "pin", "tap", "vault_unbolt", "vault_crack",
    };

    // A correct sort rises a little in pitch with each sort of a streak.
    const float PitchPerStreak = 0.025f;
    const int PitchStreakCap = 20;

    public static JuicyAudioHooks Instance { get; private set; }

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    // Dings each get their own source, round robin, so a new pitch never
    // bends the tail of one still ringing.
    const int PitchedVoices = 4;

    AudioSource source;
    AudioSource[] pitched;
    int nextPitched;

    public static void Ensure()
    {
        // Only in play: outside it a new object would be left in the scene.
        if (Instance != null || !Application.isPlaying) return;
        new GameObject("JuicyAudioHooks").AddComponent<JuicyAudioHooks>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        pitched = new AudioSource[PitchedVoices];
        for (int i = 0; i < PitchedVoices; i++)
        {
            pitched[i] = gameObject.AddComponent<AudioSource>();
            pitched[i].playOnAwake = false;
            pitched[i].spatialBlend = 0f;
        }
        foreach (string name in ClipNames)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null) Debug.LogWarning("Sound missing: Resources/Audio/" + name);
            else clips[name] = clip;
        }
        clips["keypad_beep"] = Tone("keypad_beep", 1320f, 0.09f, false);
        clips["keypad_buzz"] = Tone("keypad_buzz", 110f, 0.45f, true);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Sort(int streak)
    {
        Ensure();
        if (Instance == null) return;
        Instance.PlayPitched("sort_ding", 0.75f, 1f + PitchPerStreak * Mathf.Min(streak, PitchStreakCap));
    }

    public static void Wrong() => Play("sort_wrong", 0.8f);
    public static void Miss() => Play("miss", 0.7f);
    public static void Flick() => Play("flick", 0.7f);
    public static void Boom() => Play("boom", 0.9f);
    public static void Cash() => Play("cash", 0.8f);
    public static void StreakUp() => Play("streak_up", 0.7f);
    public static void Won() => Play("level_won", 0.85f);
    public static void Failed() => Play("level_failed", 0.8f);
    public static void Scratch() => Play("scratch", 0.45f);
    public static void Reveal() => Play("reveal", 0.8f);
    public static void Pin() => Play("pin", 0.8f);
    public static void Tap() => Play("tap", 0.6f);
    public static void Vault() => Play("vault_unbolt", 0.85f);
    public static void Crack() => Play("vault_crack", 0.95f);
    // A right key on the vault keypad.
    public static void Beep() => Play("keypad_beep", 0.6f);
    // A wrong key on the vault keypad.
    public static void Buzz() => Play("keypad_buzz", 0.7f);

    static void Play(string name, float volume)
    {
        Ensure();
        if (Instance == null) return;
        Instance.PlayClip(Instance.source, name, volume);
    }

    void PlayPitched(string name, float volume, float pitch)
    {
        AudioSource voice = pitched[nextPitched];
        nextPitched = (nextPitched + 1) % PitchedVoices;
        voice.pitch = pitch;
        PlayClip(voice, name, volume);
    }

    void PlayClip(AudioSource on, string name, float volume)
    {
        if (!AudioPrefs.SoundOn || on == null) return;
        if (clips.TryGetValue(name, out AudioClip clip)) on.PlayOneShot(clip, volume);
    }

    // A short tone with a quick fade out: sine for the beep, square for the buzz.
    static AudioClip Tone(string name, float frequency, float seconds, bool square)
    {
        const int rate = 44100;
        int length = Mathf.CeilToInt(rate * seconds);
        var samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            float wave = Mathf.Sin(2f * Mathf.PI * frequency * i / rate);
            if (square) wave = Mathf.Sign(wave) * 0.6f;
            float fade = Mathf.Min(1f, (length - i) / (rate * 0.03f));
            samples[i] = wave * fade;
        }
        AudioClip clip = AudioClip.Create(name, length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
