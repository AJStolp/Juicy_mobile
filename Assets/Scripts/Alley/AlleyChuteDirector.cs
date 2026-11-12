using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Runs the heist sort in the alley. Items fall from the open chutes; the
// player swipes left or right to send one into that side's color chest, or up
// to trash it. HeistSortRules decides what each outcome is worth.
public class AlleyChuteDirector : MonoBehaviour
{
    // Chutes open centre first: Drop_02, Drop_03, Drop_01, then Drop_04.
    static readonly int[] ChuteOrder = { 1, 2, 0, 3 };

    public static readonly Color Ruby = new Color(0.95f, 0.08f, 0.16f);
    public static readonly Color Sapphire = new Color(0.16f, 0.36f, 1f);

    [SerializeField] Transform[] drops = new Transform[4];
    [SerializeField] AlleyBin[] bins = new AlleyBin[2];
    [SerializeField] AlleyItem itemTemplate;
    [SerializeField] int poolSize = 14;
    [SerializeField] float spawnInterval = 0.85f;
    [SerializeField] float fallSpeed = 1.6f;
    [SerializeField] float groundY = 0.1f;
    [Tooltip("Shortest swipe that counts, as a fraction of screen height.")]
    [SerializeField] float minSwipe = 0.04f;
    [Tooltip("How close a press must be to an item to grab it, as a fraction of screen height.")]
    [SerializeField] float pickRadius = 0.12f;
    [SerializeField] float flightTime = 0.32f;
    [SerializeField] float trashTime = 0.45f;
    [SerializeField] bool autoPlay;
    [Range(0f, 1f)] [SerializeField] float autoMistakes = 0.12f;

    readonly List<AlleyItem> live = new List<AlleyItem>();
    readonly Queue<AlleyItem> idle = new Queue<AlleyItem>();
    readonly HashSet<AlleyItem> botSkipped = new HashSet<AlleyItem>();
    Camera view;
    float spawnTimer;
    bool pressing;
    Vector2 pressStart;

    public HeistSortRules Rules { get; private set; }
    public int Spawned { get; private set; }
    public int Flicks { get; private set; }
    public int Misses { get; private set; }

    public static Color GemColor(int colorId) => colorId == 0 ? Ruby : Sapphire;

    public void Configure(Transform[] dropPoints, AlleyBin[] colorBins, AlleyItem template)
    {
        drops = dropPoints;
        bins = colorBins;
        itemTemplate = template;
    }

    void Awake()
    {
        Rules = new HeistSortRules();
        Rules.Busted += ClearLive;
        view = Camera.main;
        itemTemplate.gameObject.SetActive(false);
        for (int i = 0; i < poolSize; i++)
        {
            AlleyItem item = Instantiate(itemTemplate, transform);
            item.name = "Item_" + i;
            item.Park();
            idle.Enqueue(item);
        }
        for (int i = 0; i < bins.Length; i++) bins[i].Bind(Rules);
#if UNITY_EDITOR
        if (System.IO.File.Exists("/tmp/juicy-alley-bot")) autoPlay = true;
#endif
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Rules.Tick(dt);
        TickSpawn(dt);
        TickInput();
        if (autoPlay) TickBot();
        TickItems(dt);
    }

    void TickSpawn(float dt)
    {
        spawnTimer += dt;
        if (spawnTimer < spawnInterval || idle.Count == 0) return;
        spawnTimer = 0f;

        AlleyItem item = idle.Dequeue();
        ItemKind kind = Rules.KindForRoll(Random.value);
        int colorId = kind == ItemKind.Gem ? Random.Range(0, HeistSortRules.Sides) : 0;
        char letter = kind == ItemKind.Letter ? Rules.PickLetter(Random.value) : '\0';
        Transform drop = drops[ChuteOrder[Random.Range(0, Rules.Chutes)]];
        item.Launch(kind, colorId, GemColor(colorId), letter, drop.position);
        live.Add(item);
        Spawned++;
    }

    void TickInput()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;
        Vector2 position = pointer.position.ReadValue();
        if (pointer.press.wasPressedThisFrame)
        {
            pressing = true;
            pressStart = position;
        }
        if (!pressing || !pointer.press.wasReleasedThisFrame) return;
        pressing = false;
        int direction = HeistSortRules.FlickDirection(pressStart, position, Screen.height * minSwipe);
        if (direction != 0) Fling(Pick(pressStart), direction);
    }

    // The falling item under the press, or else the one closest to the ground.
    AlleyItem Pick(Vector2 screen)
    {
        AlleyItem nearest = null;
        AlleyItem lowest = null;
        float best = Screen.height * pickRadius;
        float lowestY = float.MaxValue;
        for (int i = 0; i < live.Count; i++)
        {
            AlleyItem item = live[i];
            if (item.State != AlleyItem.Phase.Falling) continue;
            Vector3 point = view.WorldToScreenPoint(item.transform.position);
            float distance = Vector2.Distance(screen, point);
            if (point.z > 0f && distance < best)
            {
                best = distance;
                nearest = item;
            }
            if (item.transform.position.y < lowestY)
            {
                lowestY = item.transform.position.y;
                lowest = item;
            }
        }
        return nearest != null ? nearest : lowest;
    }

    void Fling(AlleyItem item, int direction)
    {
        if (item == null) return;
        Flicks++;
        if (direction == 2)
        {
            item.BeginTrash(trashTime);
            return;
        }
        int side = direction < 0 ? 0 : 1;
        item.BeginFlight(side, bins[side].Mouth, flightTime);
    }

    void TickItems(float dt)
    {
        float drop = fallSpeed * dt;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            AlleyItem item = live[i];
            switch (item.State)
            {
                case AlleyItem.Phase.Falling:
                    item.Fall(drop, dt);
                    if (item.transform.position.y > groundY) break;
                    Misses++;
                    Rules.Miss(item.Kind);
                    Recycle(i);
                    break;
                case AlleyItem.Phase.Flying:
                    if (!item.Advance(dt)) break;
                    SortResult result = Rules.Catch(item.Kind, item.ColorId, item.Side, item.Letter);
                    bins[item.Side].Receive(result);
                    item.BeginPop(bins[item.Side].Mouth, result);
                    break;
                case AlleyItem.Phase.Trashing:
                    if (!item.Advance(dt)) break;
                    Rules.Trash(item.Kind);
                    Recycle(i);
                    break;
                case AlleyItem.Phase.Popping:
                    if (item.Advance(dt)) Recycle(i);
                    break;
            }
        }
    }

    void Recycle(int index)
    {
        AlleyItem item = live[index];
        live[index] = live[live.Count - 1];
        live.RemoveAt(live.Count - 1);
        botSkipped.Remove(item);
        item.Park();
        idle.Enqueue(item);
    }

    // A bust clears everything still in the air.
    void ClearLive()
    {
        for (int i = live.Count - 1; i >= 0; i--) Recycle(i);
        spawnTimer = -1f;
    }

    // Plays for verification: sorts most items correctly, trashes bombs,
    // and makes a deliberate mistake now and then.
    void TickBot()
    {
        for (int i = 0; i < live.Count; i++)
        {
            AlleyItem item = live[i];
            if (item.State != AlleyItem.Phase.Falling || item.transform.position.y > 1.5f) continue;
            if (botSkipped.Contains(item)) continue;
            if (Random.value < autoMistakes * 0.3f)
            {
                botSkipped.Add(item);
                continue;
            }
            bool mistake = Random.value < autoMistakes;
            int direction;
            if (item.Kind == ItemKind.Bomb) direction = mistake ? -1 : 2;
            else if (item.Kind == ItemKind.Gem) direction = (item.ColorId == 0) != mistake ? -1 : 1;
            else direction = Random.value < 0.5f ? -1 : 1;
            Fling(item, direction);
        }
    }
}
