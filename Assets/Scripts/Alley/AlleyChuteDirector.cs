using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Runs the heist sort in the alley. Items fall from the open chutes; the
// player swipes left or right to send one into that side's color chest, or up
// to trash it. HeistSortRules decides what each outcome is worth, and every
// vat fill turns the vault. Vault, cash and gear are saved between sittings.
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
    [Tooltip("Height where an unswiped item starts drifting toward the front of the street, so it never drops behind a chest.")]
    [SerializeField] float approachY = 1.6f;
    [Tooltip("Z of the street in front of the chests, where a missed item lands.")]
    [SerializeField] float streetZ = -0.75f;
    [Tooltip("How far across a missed item lands, as a fraction of its chute's x, so the outer chutes stay on screen.")]
    [SerializeField] float streetSpread = 0.75f;
    [SerializeField] float streetY = 0f;
    [SerializeField] float landTime = 0.75f;
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

    const string SaveKey = "Juicy.HeistSave";
    const string BotSaveKey = "Juicy.HeistSave.Bot";
    const int TurnsPerVatFill = 1;

    bool paused;
    bool clearQueued;

    public HeistSortRules Rules { get; private set; }
    public VaultProgress Vault { get; private set; }
    public bool AutoPlay => autoPlay;
    public float AutoMistakes { get => autoMistakes; set => autoMistakes = Mathf.Clamp01(value); }

    // Paused for a bonus round: the street clears and nothing falls.
    public bool Paused
    {
        get => paused;
        set
        {
            if (value && !paused) ClearLive();
            paused = value;
        }
    }
    public int Spawned { get; private set; }
    public int Flicks { get; private set; }
    public int Misses { get; private set; }

    // A missed item has hit the street in front of the chests. The miss is
    // already counted when this fires.
    public event System.Action<AlleyItem> MissLanded;

    public static Color GemColor(int colorId) => colorId == 0 ? Ruby : Sapphire;

    public void Configure(Transform[] dropPoints, AlleyBin[] colorBins, AlleyItem template)
    {
        drops = dropPoints;
        bins = colorBins;
        itemTemplate = template;
    }

    void Awake()
    {
        view = Camera.main;
        Wire(new HeistSortRules(), new VaultProgress());
#if UNITY_EDITOR
        if (System.IO.File.Exists("/tmp/juicy-alley-bot")) autoPlay = true;
#endif
        // Bot runs keep their own save and start each run fresh.
        if (autoPlay) PlayerPrefs.DeleteKey(BotSaveKey);
        HeistSave saved = HeistSave.FromJson(PlayerPrefs.GetString(CurrentSaveKey, ""));
        if (saved != null) saved.Apply(Vault, Rules);
        Vault.Turned += (_, __) => SaveNow();
        Vault.StageLocked += _ => SaveNow();
        Vault.RolledBack += (_, __) => SaveNow();
        Vault.Cracked += _ => SaveNow();
        Rules.GearChanged += (_, __) => SaveNow();
    }

    // Hooks the rules to the vault and fills the item pool. Awake calls it;
    // tests call it to drive items without a scene or a save.
    public void Wire(HeistSortRules rules, VaultProgress vault)
    {
        Rules = rules;
        Vault = vault;
        Rules.Busted += ClearLive;
        Rules.Busted += Vault.Bust;
        Rules.VatFilled += _ => Vault.Turn(TurnsPerVatFill);
        itemTemplate.gameObject.SetActive(false);
        for (int i = 0; i < poolSize; i++)
        {
            AlleyItem item = Instantiate(itemTemplate, transform);
            item.name = "Item_" + i;
            item.Park();
            idle.Enqueue(item);
        }
        for (int i = 0; i < bins.Length; i++) bins[i].Bind(Rules);
    }

    string CurrentSaveKey => autoPlay ? BotSaveKey : SaveKey;

    public void SaveNow()
    {
        PlayerPrefs.SetString(CurrentSaveKey, HeistSave.Capture(Vault, Rules).ToJson());
        PlayerPrefs.Save();
    }

    void OnApplicationPause(bool pausing)
    {
        if (pausing && Rules != null) SaveNow();
    }

    void OnApplicationQuit()
    {
        if (Rules != null) SaveNow();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        FlushClear();
        if (paused) return;
        Rules.Tick(dt);
        TickSpawn(dt);
        TickInput();
        if (autoPlay) TickBot();
        TickItems(dt);
        FlushClear();
    }

    void TickSpawn(float dt)
    {
        spawnTimer += dt;
        if (spawnTimer < spawnInterval || idle.Count == 0) return;
        spawnTimer = 0f;

        ItemKind kind = Rules.KindForRoll(Random.value);
        int colorId = kind == ItemKind.Gem ? Random.Range(0, HeistSortRules.Sides) : 0;
        char letter = kind == ItemKind.Letter ? Rules.PickLetter(Random.value) : '\0';
        Drop(kind, colorId, letter, ChuteOrder[Random.Range(0, Rules.Chutes)]);
    }

    // Sends one item down the given chute (0 to 3, left to right).
    public AlleyItem Drop(ItemKind kind, int colorId, char letter, int chute)
    {
        if (idle.Count == 0) return null;
        AlleyItem item = idle.Dequeue();
        item.Launch(kind, colorId, GemColor(colorId), letter, drops[chute].position);
        live.Add(item);
        Spawned++;
        return item;
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

    public void TickItems(float dt)
    {
        float drop = fallSpeed * dt;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            AlleyItem item = live[i];
            switch (item.State)
            {
                case AlleyItem.Phase.Falling:
                    item.Fall(drop, dt);
                    // Dynamite keeps its old fall: straight down and gone.
                    if (item.Kind != ItemKind.Bomb) item.Approach(StreetSpot(item.Origin), ApproachFraction(item.transform.position.y));
                    if (item.transform.position.y > groundY) break;
                    Misses++;
                    Rules.Miss(item.Kind);
                    if (item.Kind == ItemKind.Bomb)
                    {
                        Recycle(i);
                        break;
                    }
                    item.BeginLanding(streetY, landTime);
                    MissLanded?.Invoke(item);
                    break;
                case AlleyItem.Phase.Landed:
                    if (item.Advance(dt)) Recycle(i);
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

    // Where an item from this chute comes to rest if nobody swipes it: on the
    // street in front of the chests, pulled in a little from the outer chutes.
    Vector3 StreetSpot(Vector3 origin) => new Vector3(origin.x * streetSpread, streetY, streetZ);

    // 0 above approachY, easing to 1 at the ground. Eased out, so the item is
    // already clear of the chests by the time it is level with their lids.
    float ApproachFraction(float y)
    {
        float k = Mathf.Clamp01(Mathf.InverseLerp(approachY, groundY, y));
        return 1f - (1f - k) * (1f - k);
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

    // A bust or a bonus round clears everything still in the air. Both can
    // fire from inside TickItems' loop over live items, so the clear waits
    // until that loop is done.
    void ClearLive()
    {
        clearQueued = true;
        spawnTimer = -1f;
    }

    void FlushClear()
    {
        if (!clearQueued) return;
        clearQueued = false;
        for (int i = live.Count - 1; i >= 0; i--) Recycle(i);
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
