using UnityEngine;

// A place on the map: its levels in order, the boss last.
[CreateAssetMenu(menuName = "Juicy/Place")]
public class PlaceData : ScriptableObject
{
    public string PlaceName = "City";
    [Tooltip("Sets which clue symbol each level holds.")]
    public int ClueSeed = 7;
    public LevelSpec[] Levels = { new LevelSpec() };

    public int Count => Levels.Length;

    public LevelSpec Level(int index)
    {
        if (Levels == null || Levels.Length == 0)
            throw new System.InvalidOperationException("Place " + PlaceName + " has no levels");
        return Levels[Mathf.Clamp(index, 0, Levels.Length - 1)];
    }
}
