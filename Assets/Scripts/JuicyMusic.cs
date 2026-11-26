using UnityEngine;

// The music: a loop for the map, one for levels and one for the vault,
// crossfading as play moves between them. The loops are synthesized by
// ArtSource/Audio/build_audio.py into Resources/Audio/Music. Silent while
// music is off (AudioPrefs).
public class JuicyMusic : MonoBehaviour
{
    public enum Track { None, Map, Level, Vault }

    const float Volume = 0.45f;
    const float FadeSeconds = 1.2f;

    public static JuicyMusic Instance { get; private set; }

    AudioSource[] sources;
    AudioClip[] clips;
    int current;
    Track playing = Track.None;

    public static Track Playing => Instance != null ? Instance.playing : Track.None;

    public static void Play(Track track)
    {
        if (Instance == null) new GameObject("JuicyMusic").AddComponent<JuicyMusic>();
        Instance.Switch(track);
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
        sources = new AudioSource[2];
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].loop = true;
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;
            sources[i].volume = 0f;
        }
        clips = new AudioClip[4];
        clips[(int)Track.Map] = Load("music_map");
        clips[(int)Track.Level] = Load("music_level");
        clips[(int)Track.Vault] = Load("music_vault");
        AudioPrefs.Changed += OnPrefs;
    }

    void OnDestroy()
    {
        AudioPrefs.Changed -= OnPrefs;
        if (Instance == this) Instance = null;
    }

    static AudioClip Load(string name)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/Music/" + name);
        if (clip == null) Debug.LogWarning("Music missing: Resources/Audio/Music/" + name);
        return clip;
    }

    void Switch(Track track)
    {
        if (track == playing) return;
        playing = track;
        current = 1 - current;
        AudioSource next = sources[current];
        next.clip = clips[(int)track];
        next.volume = 0f;
        if (next.clip != null) next.Play();
    }

    void OnPrefs()
    {
        if (!AudioPrefs.MusicOn) foreach (AudioSource s in sources) s.volume = 0f;
    }

    void Update()
    {
        float step = Time.unscaledDeltaTime / FadeSeconds * Volume;
        for (int i = 0; i < sources.Length; i++)
        {
            float target = i == current && AudioPrefs.MusicOn ? Volume : 0f;
            sources[i].volume = Mathf.MoveTowards(sources[i].volume, target, step);
            if (i != current && sources[i].volume <= 0f && sources[i].isPlaying) sources[i].Stop();
        }
    }
}
