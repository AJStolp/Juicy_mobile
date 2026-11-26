using UnityEngine;

// A place on the map: its levels in order, the boss last.
[CreateAssetMenu(menuName = "Juicy/Place")]
public class PlaceData : ScriptableObject
{
    public string PlaceName = "City";
    public LevelSpec[] Levels = { new LevelSpec() };

    public int Count => Levels.Length;

    public LevelSpec Level(int index) => Levels[Mathf.Clamp(index, 0, Levels.Length - 1)];
}
