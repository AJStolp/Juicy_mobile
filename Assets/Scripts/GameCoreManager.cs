using UnityEngine;
using System;

public class GameCoreManager : MonoBehaviour
{
    public static GameCoreManager Instance { get; private set; }

    public enum GameState
    {
        PuzzleLoop,
        BonusMiniGame,
        Victory
    }

    [Header("Current Active State")]
    [SerializeField] private GameState currentState = GameState.PuzzleLoop;

    public GameState CurrentState => currentState;

    [Header("Theme Configuration")]
    [SerializeField] private ThemeConfig activeTheme;

    public ThemeConfig ActiveTheme => activeTheme;

    public event Action<GameState> OnStateChanged;
    public event Action<ThemeConfig> OnThemeChanged;
    public event Action<bool, bool> OnVaultCue;

    public void RaiseVaultCue(bool open, bool indigo)
    {
        OnVaultCue?.Invoke(open, indigo);
    }

    AudioSource bed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (activeTheme != null) ApplyTheme(activeTheme);
    }

    public void ChangeState(GameState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        Debug.Log($"State changed to: {currentState}");
        OnStateChanged?.Invoke(currentState);
        if (newState == GameState.Victory && JuicyWinTicker.Instance != null)
            JuicyWinTicker.Instance.ReachLevelClear();
    }

    // Swaps the catalog, the bed, and bloom, then tells the views.
    public void ApplyTheme(ThemeConfig newTheme)
    {
        if (newTheme == null) return;

        activeTheme = newTheme;
        ItemCatalog.Current = newTheme.catalogTheme;
        PlayBed(newTheme.ambientLoop != null ? newTheme.ambientLoop : newTheme.themeMusic);
        if (ChuteContrast.Instance != null)
            ChuteContrast.Instance.ApplyBloom(newTheme.bloomIntensityMultiplier);
        Debug.Log($"Theme applied: {activeTheme.themeName}");
        OnThemeChanged?.Invoke(activeTheme);
    }

    public void PlayWin()
    {
        if (activeTheme == null) return;
        PlayShot(activeTheme.winJingle);
    }

    public void PlayImpact()
    {
        if (activeTheme == null) return;
        PlayShot(activeTheme.impactSound);
    }

    void PlayBed(AudioClip clip)
    {
        if (clip == null)
        {
            if (bed != null) bed.Stop();
            return;
        }

        EnsureBed();
        if (bed.clip == clip && bed.isPlaying) return;
        bed.clip = clip;
        bed.loop = true;
        bed.Play();
    }

    void PlayShot(AudioClip clip)
    {
        if (clip == null) return;
        EnsureBed();
        bed.PlayOneShot(clip);
    }

    void EnsureBed()
    {
        if (bed != null) return;
        bed = gameObject.AddComponent<AudioSource>();
        bed.playOnAwake = false;
        bed.spatialBlend = 0f;
    }
}
