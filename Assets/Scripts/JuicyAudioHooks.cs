using UnityEngine;

// One-shot heist effects. Theme music slots stay empty.
public class JuicyAudioHooks : MonoBehaviour
{
    public static JuicyAudioHooks Instance { get; private set; }

    AudioSource source;
    AudioClip vault;
    AudioClip sort;
    AudioClip chime;
    AudioClip squeal;

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
