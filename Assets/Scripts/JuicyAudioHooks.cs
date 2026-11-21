using UnityEngine;

// One-shot heist effects. Theme music slots stay empty. The keypad's beep and
// buzz are synthesized, so they need no audio files.
public class JuicyAudioHooks : MonoBehaviour
{
    public static JuicyAudioHooks Instance { get; private set; }

    AudioSource source;
    AudioClip vault;
    AudioClip sort;
    AudioClip chime;
    AudioClip squeal;
    AudioClip beep;
    AudioClip buzz;

    public static void Ensure()
    {
        if (Instance != null) return;
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
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        vault = Resources.Load<AudioClip>("Audio/vault_unbolt");
        sort = Resources.Load<AudioClip>("Audio/sort_clunk");
        chime = Resources.Load<AudioClip>("Audio/money_chime");
        squeal = Resources.Load<AudioClip>("Audio/tire_squeal");
        if (vault == null) Debug.LogWarning("Heist audio missing vault_unbolt.");
        if (sort == null) Debug.LogWarning("Heist audio missing sort_clunk.");
        if (chime == null) Debug.LogWarning("Heist audio missing money_chime.");
        if (squeal == null) Debug.LogWarning("Heist audio missing tire_squeal.");
        beep = Tone("keypad_beep", 1320f, 0.09f, false);
        buzz = Tone("keypad_buzz", 110f, 0.45f, true);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Vault()
    {
        Ensure();
        Instance.Play(Instance.vault, 0.9f);
    }

    public static void Sort()
    {
        Ensure();
        Instance.Play(Instance.sort, 0.8f);
    }

    public static void Chime()
    {
        Ensure();
        Instance.Play(Instance.chime, 0.7f);
    }

    public static void Getaway()
    {
        Ensure();
        Instance.Play(Instance.squeal, 1f);
    }

    // A right key on the vault keypad.
    public static void Beep()
    {
        Ensure();
        Instance.Play(Instance.beep, 0.6f);
    }

    // A wrong key on the vault keypad.
    public static void Buzz()
    {
        Ensure();
        Instance.Play(Instance.buzz, 0.7f);
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

    void Play(AudioClip clip, float volume)
    {
        if (source == null || clip == null)
        {
            Debug.LogWarning("Heist audio clip missing.");
            return;
        }

        source.pitch = 1f;
        source.PlayOneShot(clip, volume);
        Debug.Log("Heist audio " + clip.name);
    }
}
