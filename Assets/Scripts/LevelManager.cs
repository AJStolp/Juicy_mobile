using UnityEngine;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Level Database")]
    [SerializeField] private List<LevelConfig> levelDatabase;
    [SerializeField] private int currentLevelIndex = 0;

    [Header("References")]
    [SerializeField] private SortingGridManager gridManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        LoadCurrentLevel();
    }

    public void LoadCurrentLevel()
    {
        if (levelDatabase == null || levelDatabase.Count == 0)
        {
            Debug.LogWarning("Level database is empty!");
            return;
        }

        // Wrap around or handle end of game loop
        int index = currentLevelIndex % levelDatabase.Count;
        LevelConfig config = levelDatabase[index];
        if (config == null)
        {
            Debug.LogWarning("Level entry is missing.");
            return;
        }

        Debug.Log($"Loading Level {config.levelNumber}: {config.themeTitle}");
        
        if (gridManager != null) gridManager.LoadLevelData(config);
    }

    public void AdvanceToNextLevel()
    {
        currentLevelIndex++;
        SaveProgress();
        LoadCurrentLevel();
    }

    private void SaveProgress()
    {
        // Save persistent player progress locally using PlayerPrefs or secure storage
        PlayerPrefs.SetInt("SavedLevelIndex", currentLevelIndex);
        PlayerPrefs.Save();
    }
}