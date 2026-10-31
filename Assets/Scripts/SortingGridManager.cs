using UnityEngine;
using System.Collections.Generic;

public class SortingGridManager : MonoBehaviour
{
    [System.Serializable]
    public class TubeData
    {
        // Represents a single tube or column holding a stack of color IDs/gems
        public Stack<int> gemStack = new Stack<int>();
        public int capacity = 4; // Max gems per tube
    }

    [Header("Grid Configuration")]
    [SerializeField] private int totalTubes = 8;
    [SerializeField] private int maxCapacityPerTube = 4;

    // Runtime state representing the board matrix
    private List<TubeData> tubes = new List<TubeData>();
    private int selectedTubeIndex = -1;

    public delegate void OnMoveCompletedHandler(bool isPuzzleSolved);
    public event OnMoveCompletedHandler MoveCompleted;
    public event System.Action BoardLoaded;

    public int TubeCount => tubes.Count;

    /// <summary>
    /// Sets up initial tube data structures (can be loaded from a level JSON config later).
    /// </summary>
    public void InitializeBoard()
    {
        tubes.Clear();
        for (int i = 0; i < totalTubes; i++)
        {
            tubes.Add(new TubeData { capacity = maxCapacityPerTube });
        }
        
    }

    /// <summary>
    /// Loads a level. Color id 0 in each tube list is the bottom gem.
    /// </summary>
    public void LoadLevelData(LevelConfig config)
    {
        tubes.Clear();
        selectedTubeIndex = -1;

        if (config == null || config.initialTubes == null)
        {
            BoardLoaded?.Invoke();
            return;
        }

        for (int i = 0; i < config.initialTubes.Count; i++)
        {
            LevelConfig.TubeSetup setup = config.initialTubes[i];
            TubeData tube = new TubeData { capacity = maxCapacityPerTube };
            if (setup.gemColorIDs != null)
            {
                for (int g = 0; g < setup.gemColorIDs.Count && tube.gemStack.Count < tube.capacity; g++)
                    tube.gemStack.Push(setup.gemColorIDs[g]);
            }
            tubes.Add(tube);
        }

        totalTubes = tubes.Count;
        BoardLoaded?.Invoke();
    }

    public int GemCount(int tubeIndex)
    {
        if (tubeIndex < 0 || tubeIndex >= tubes.Count) return 0;
        return tubes[tubeIndex].gemStack.Count;
    }

    public int PeekTop(int tubeIndex)
    {
        if (GemCount(tubeIndex) == 0) return -1;
        return tubes[tubeIndex].gemStack.Peek();
    }

    public void CopyColorsBottomToTop(int tubeIndex, List<int> into)
    {
        into.Clear();
        if (tubeIndex < 0 || tubeIndex >= tubes.Count) return;

        int[] topFirst = tubes[tubeIndex].gemStack.ToArray();
        for (int i = topFirst.Length - 1; i >= 0; i--) into.Add(topFirst[i]);
    }

    public TubeClickOutcome HandleTubeClick(int clickedTubeIndex)
    {
        if (clickedTubeIndex < 0 || clickedTubeIndex >= tubes.Count)
            return new TubeClickOutcome { Kind = TubeClickKind.Ignored, FromTube = -1, ToTube = clickedTubeIndex };

        if (selectedTubeIndex == -1)
        {
            if (tubes[clickedTubeIndex].gemStack.Count == 0)
                return new TubeClickOutcome { Kind = TubeClickKind.Ignored, FromTube = -1, ToTube = clickedTubeIndex };

            selectedTubeIndex = clickedTubeIndex;
            Debug.Log($"Selected Tube: {selectedTubeIndex}");
            return new TubeClickOutcome { Kind = TubeClickKind.Selected, FromTube = -1, ToTube = clickedTubeIndex };
        }

        if (selectedTubeIndex == clickedTubeIndex)
        {
            int cleared = selectedTubeIndex;
            selectedTubeIndex = -1;
            Debug.Log("Deselected tube.");
            return new TubeClickOutcome { Kind = TubeClickKind.Deselected, FromTube = cleared, ToTube = cleared };
        }

        int fromTube = selectedTubeIndex;
        bool moveSuccessful = ExecuteMove(fromTube, clickedTubeIndex);
        selectedTubeIndex = -1;

        if (!moveSuccessful)
            return new TubeClickOutcome { Kind = TubeClickKind.Rejected, FromTube = fromTube, ToTube = clickedTubeIndex };

        bool solved = CheckWinCondition();
        MoveCompleted?.Invoke(solved);
        return new TubeClickOutcome { Kind = TubeClickKind.Moved, FromTube = fromTube, ToTube = clickedTubeIndex, Solved = solved };
    }

    private bool ExecuteMove(int fromIndex, int toIndex)
    {
        TubeData sourceTube = tubes[fromIndex];
        TubeData destTube = tubes[toIndex];

        // Validation rules:
        // 1. Destination must not be full
        if (destTube.gemStack.Count >= destTube.capacity) return false;

        // 2. If destination is not empty, top gem color must match moving gem color (standard sorting rule)
        int movingGemColor = sourceTube.gemStack.Peek();
        if (destTube.gemStack.Count > 0 && destTube.gemStack.Peek() != movingGemColor)
        {
            return false;
        }

        // Execute move data transaction
        sourceTube.gemStack.Pop();
        destTube.gemStack.Push(movingGemColor);

        Debug.Log($"Moved gem {movingGemColor} from Tube {fromIndex} to Tube {toIndex}");
        return true;
    }

    private bool CheckWinCondition()
    {
        foreach (var tube in tubes)
        {
            // A tube is either completely empty or completely filled with identical items up to capacity
            if (tube.gemStack.Count == 0) continue;
            if (tube.gemStack.Count < tube.capacity) return false;

            int firstColor = -1;
            foreach (int gem in tube.gemStack)
            {
                if (firstColor == -1) firstColor = gem;
                else if (firstColor != gem) return false;
            }
        }

        Debug.Log("Puzzle solved.");
        return true;
    }
}

public enum TubeClickKind
{
    Ignored,
    Selected,
    Deselected,
    Moved,
    Rejected
}

public struct TubeClickOutcome
{
    public TubeClickKind Kind;
    public int FromTube;
    public int ToTube;
    public bool Solved;
}