using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData_", menuName = "Architect/Level Config")]
public class LevelConfig : ScriptableObject
{
    public int levelNumber;
    public string themeTitle;

    [System.Serializable]
    public struct TubeSetup
    {
        public List<int> gemColorIDs; // Index 0 is the bottom gem. The last id is the top.
    }

    public List<TubeSetup> initialTubes;
}
