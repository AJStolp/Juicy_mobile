using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Runs one level of the heist sort in the alley. Gems, dynamite and gold fall
// from the open chutes; the player swipes left or right to send one into that
// side's color chest, or up to flick it away. HeistSortRules decides what each
// outcome is worth, and LevelRules decides the order things fall in and when
// the attempt is won or failed. After the city's six levels it plays the
// vault, the boss: its sort turns the vault door, and the keypad finale
// (AlleyHack) takes the city's clues as the code. Cash, gear, progress and
// clues are saved between sittings.
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
    [SerializeField] float groundY = 0.1f;
    [Tooltip("Shortest swipe that counts, as a fraction of screen height.")]
    [SerializeField] float minSwipe = 0.04f;
    [Tooltip("How close a press must be to an item to grab it, as a fraction of screen height.")]
    [SerializeField] float pickRadius = 0.12f;
    [SerializeField] float flightTime = 0.32f;
    [SerializeField] float trashTime = 0.45f;
    [SerializeField] PlaceData place;
    [SerializeField] bool autoPlay;
    [Range(0f, 1f)] [SerializeField] float autoMistakes = 0.12f;
    [Tooltip("Auto play waits this long after a drop appears before it can swipe it, like a person.")]
    [SerializeField] float autoReact = 0.45f;
    [Tooltip("Auto play's shortest gap between two swipes.")]
    [SerializeField] float autoSwipeGap = 0.3f;

    readonly List<AlleyItem> live = new List<AlleyItem>();
    readonly Queue<AlleyItem> idle = new Queue<AlleyItem>();
    readonly HashSet<AlleyItem> botSkipped = new HashSet<AlleyItem>();
    readonly Dictionary<AlleyItem, float> launchedAt = new Dictionary<AlleyItem, float>();
    float botReadyAt;
    Camera view;
    float spawnTimer;
    bool pressing;
    Vector2 pressStart;

    const string SaveKey = "Juicy.HeistSave";
    const string BotSaveKey = "Juicy.HeistSave.Bot";

    bool paused;
    bool clearQueued;
    // Which of the place's levels is playing, counting from 0. Starts at the
    // next unbeaten one.
    int levelIndex;

    public HeistSortRules Rules { get; private set; }
    public VaultProgress Vault { get; private set; }
    public LevelRules Level { get; private set; }
    public PlaceProgress Progress { get; private set; }
    public CaseFile CaseFile { get; private set; }
    public Lives Lives { get; private set; } = new Lives();

    // A failed attempt can be kept going with more gems, for cash earned in
    // play; the price doubles each time in the same attempt. Leaving a failed
    // attempt any other way costs a life. A full refill of lives also costs
    // cash. Prices are the lead's starting call (T-038), to tune.
    public const int KeepGoingGems = 5;
    public const double KeepGoingBasePrice = 5000d;
    public const double RefillPrice = 15000d;

    // The clock, in Unix seconds; checks can replace it.
    public System.Func<long> Clock = () => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long Now => Clock();
    public int LivesLeft => Lives.Count(Now);
    public event System.Action AttemptResumed;
    // The life a failed attempt took, until the player keeps it going (which
    // gives it back) or leaves. Taken at the fail, so closing the game on the
    // fail panel cannot dodge it.
    bool failPending;
    // The level whose street was cleared, so its items in the air go back to it.
    LevelRules clearedLevel;
    public PlaceData Place => place;
    public int LevelIndex => levelIndex;
    // True while the vault (the place's boss) is being played.
    public bool IsBoss { get; private set; }
    // The keypad finale of the current vault attempt, once its sort is done.
    public VaultFinale Finale { get; private set; }
    // Fires when a vault attempt reaches its target: the keypad takes over.
    public event System.Action<VaultFinale> FinaleStarted;
    public event System.Action<LevelRules> AttemptStarted;
    public event System.Action<LevelRules> AttemptEnded;
    // True when a map is in the scene and this is not auto play: play then
    // starts from the map, and a won level goes back to it. Set in Awake,
    // before any Start reads it.
    public bool UsesMap { get; private set; }
    // True when a title is in the scene and the map is used: play opens on
    // the title, which goes on to the map.
    public bool UsesTitle { get; private set; }
    public event System.Action MapOpened;
    // True when a clue reveal is in the scene and this is not auto play: a
    // won level's clue is scratched off before going back to the map.
    public bool UsesReveal { get; private set; }
    public event System.Action<int, Clue> RevealOpened;
    public bool AutoPlay => autoPlay;
    public float AutoMistakes { get => autoMistakes; set => autoMistakes = Mathf.Clamp01(value); }

    // Paused between attempts, on the map or in a bonus round: the street
    // clears and nothing falls.
    public bool Paused
    {
        get => paused;
        set
        {
            if (value && !paused) ClearLive();
            paused = value;
        }
    }
    public IReadOnlyList<AlleyItem> Live => live;
    public int Spawned { get; private set; }
    public int Flicks { get; private set; }
    public int Misses { get; private set; }

    public static Color GemColor(int colorId) => colorId == 0 ? Ruby : Sapphire;

    public void Configure(Transform[] dropPoints, AlleyBin[] colorBins, AlleyItem template, PlaceData levels)
    {
        drops = dropPoints;
        bins = colorBins;
        itemTemplate = template;
        place = levels;
    }

    void Awake()
    {
        Rules = new HeistSortRules();
        Vault = new VaultProgress();
        if (place == null) throw new System.InvalidOperationException("The chute director has no place to play; rebuild the scene with Wire Gameplay");
        Progress = new PlaceProgress(place.Count);
        CaseFile = new CaseFile(place.Count, place.ClueSeed);
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
        UsesMap = !autoPlay && FindAnyObjectByType<AlleyMap>() != null;
        UsesTitle = UsesMap && FindAnyObjectByType<AlleyTitle>() != null;
        UsesReveal = !autoPlay && FindAnyObjectByType<AlleyClueReveal>() != null;
        // Bot runs keep their own save and start each run fresh.
        if (autoPlay) PlayerPrefs.DeleteKey(BotSaveKey);
        HeistSave saved = HeistSave.FromJson(PlayerPrefs.GetString(CurrentSaveKey, ""));
        if (saved != null)
        {
            saved.Apply(Rules, Progress, CaseFile);
            Lives = saved.LoadLives();
        }
        Rules.GearChanged += (_, __) => SaveNow();
        Rules.Sorted += TurnVault;
        levelIndex = Progress.Next;
        BeginLevel();
    }

    // After a win: the clue reveal when there is one (not after the vault,
    // which holds no clue), else the map, else straight into another attempt.
    public void AfterWin()
    {
        if (UsesReveal && !IsBoss) OpenReveal();
        else if (UsesMap) OpenMap();
        else Retry();
    }

    // Shows the level just played's clue to scratch off. Nothing falls.
    public void OpenReveal()
    {
        Paused = true;
        RevealOpened?.Invoke(levelIndex, CaseFile.ClueAt(levelIndex));
    }

    // Leaves play for the map. Nothing falls until a level is picked.
    public void OpenMap()
    {
        Concede();
        Paused = true;
        MapOpened?.Invoke();
    }

    public double KeepGoingPrice => KeepGoingBasePrice * System.Math.Pow(2, Level != null ? Level.Continues : 0);

    // A failed attempt (not a tripped vault alarm) that cash can keep going.
    public bool CanKeepGoing => failPending && Level.State == LevelState.Failed && !Level.FailedAtFinale && Rules.Balance >= KeepGoingPrice;

    public bool KeepGoing()
    {
        if (!CanKeepGoing || !Rules.Spend(KeepGoingPrice)) return false;
        Level.Continue(KeepGoingGems);
        failPending = false;
        Lives.Give(Now);
        spawnTimer = 0f;
        Paused = false;
        SaveNow();
        AttemptResumed?.Invoke();
        return true;
    }

    // Leaving a failed attempt without keeping it going: its life, taken at
    // the fail, stays gone.
    void Concede()
    {
        failPending = false;
    }

    public bool RefillLives()
    {
        if (LivesLeft >= Lives.Max || !Rules.Spend(RefillPrice)) return false;
        Lives.Fill();
        SaveNow();
        return true;
    }

    // Starts a fresh attempt at the level (or the vault). A fail costs only
    // the attempt and a life: cash and gear carry over. With no lives left
    // it goes to the map, which offers the refill.
    public void Retry()
    {
        Concede();
        bool started = IsBoss ? PlayBoss() : Play(levelIndex);
        if (!started && UsesMap) OpenMap();
    }

    // Starts a fresh attempt at one of the place's levels. False with no
    // lives left (auto play, which tunes levels, never runs out).
    public bool Play(int index)
    {
        Concede();
        if (LivesLeft <= 0 && !autoPlay) return false;
        IsBoss = false;
        levelIndex = Mathf.Clamp(index, 0, place.Count - 1);
        Restart();
        return true;
    }

    // Starts a fresh attempt at the vault. Only once every level is beaten,
    // or on auto play, which tunes it.
    public bool PlayBoss()
    {
        Concede();
        if (!Progress.VaultOpen && !autoPlay) return false;
        if (LivesLeft <= 0 && !autoPlay) return false;
        IsBoss = true;
        Restart();
        return true;
    }

    void Restart()
    {
        Rules.StartAttempt();
        // Every attempt starts on a fresh vault door, so a cracked or half
        // turned one never carries into the next play.
        if (!IsBoss) Vault.Reset(1);
        BeginLevel();
        spawnTimer = 0f;
        Paused = false;
        AttemptStarted?.Invoke(Level);
    }

    void BeginLevel()
    {
        if (Level != null) Level.Dispose();
        Finale = null;
        LevelSpec spec = IsBoss ? place.Boss : place.Level(levelIndex);
        Rules.MaxChutes = Mathf.Clamp(spec.MaxChutes, 1, drops.Length);
        Level = new LevelRules(Rules, spec) { HoldsWin = IsBoss };
        if (!IsBoss) return;
        // The sort locks the first six stages; the finale turns the wheel.
        Vault.Reset(Mathf.CeilToInt(spec.Target / (float)(VaultProgress.Stages - 1)));
        Level.TargetMet += StartFinale;
    }

    // During the vault's sort every correct sort turns the door, cash twice.
    void TurnVault(SortResult result, int side)
    {
        if (!IsBoss || Level == null || !Level.Playing || Level.TargetReached) return;
        if (result == SortResult.Correct) Vault.Turn(1);
        else if (result == SortResult.Gold) Vault.Turn(LevelRules.GoldWorth);
    }

    // Every level's clue, in board order, is the keypad code. A clue never
    // found still counts: it shows blank on the case file, like a barely
    // readable one, and replaying its level finds it.
    void StartFinale(LevelRules level)
    {
        Vault.LockThrough(VaultProgress.Stages - 1);
        var code = new List<int>();
        for (int i = 0; i < place.Count; i++) code.Add(CaseFile.ClueAt(i).Symbol);
        Finale = new VaultFinale(code);
        Paused = true;
        FinaleStarted?.Invoke(Finale);
    }

    // The keypad reports the finale's end: the wheel turns and the door
    // swings open on a crack, and either way the attempt ends.
    public void FinishFinale()
    {
        if (!IsBoss || Finale == null || !Finale.Done || !Level.Playing) return;
        if (Finale.Cracked)
        {
            Vault.Turn(Vault.TurnsPerStage);
            Progress.CrackVault();
            Rules.Award(VaultPrize);
        }
        Level.Finish(Finale.Cracked);
        EndAttempt();
    }

    public const double VaultPrize = 25000d;

    void EndAttempt()
    {
        Paused = true;
        failPending = Level.State == LevelState.Failed;
        if (failPending) Lives.Lose(Now);
        if (Level.State == LevelState.Won && !IsBoss)
        {
            Progress.Won(levelIndex);
            CaseFile.Record(levelIndex, Level.BestTier);
        }
        SaveNow();
        AttemptEnded?.Invoke(Level);
    }

    string CurrentSaveKey => autoPlay ? BotSaveKey : SaveKey;

    public void SaveNow()
    {
        PlayerPrefs.SetString(CurrentSaveKey, HeistSave.Capture(Rules, Progress, CaseFile, Lives, Now).ToJson());
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
        Level.Tick(dt);
        TickSpawn(dt);
        TickInput();
        if (autoPlay) TickBot();
        TickItems(dt);
        FlushClear();
        // A safety net: an attempt with nothing left to fall and nothing on
        // the street can never be decided, so it ends as a fail.
        if (Level.Playing && !Level.TargetReached && !Level.CanDrop && live.Count == 0) Level.Exhaust();
        if (!Level.Playing) EndAttempt();
    }

    void TickSpawn(float dt)
    {
        spawnTimer += dt;
        if (spawnTimer < Level.Spec.DropEvery || idle.Count == 0) return;
        if (!Level.Drop(out LevelRules.Item next)) return;
        spawnTimer = 0f;

        AlleyItem item = idle.Dequeue();
        int chute = Mathf.Min((int)(next.ChuteRoll * Rules.Chutes), Rules.Chutes - 1);
        Transform drop = drops[ChuteOrder[chute]];
        item.Launch(next.Kind, next.ColorId, GemColor(next.ColorId), '\0', drop.position);
        live.Add(item);
        launchedAt[item] = Time.time;
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
        float drop = Level.Spec.FallSpeed * dt;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            // Once the attempt ends, nothing more is played this frame: what
            // is still moving goes back to the level when the street clears.
            if (!Level.Playing) break;
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
        launchedAt.Remove(item);
        item.Park();
        idle.Enqueue(item);
    }

    // The end of an attempt clears everything still in the air. It can come
    // from inside TickItems' loop over live items, so the clear waits until
    // that loop is done.
    void ClearLive()
    {
        clearedLevel = Level;
        clearQueued = true;
        spawnTimer = -1f;
    }

    void FlushClear()
    {
        if (!clearQueued) return;
        clearQueued = false;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            // Anything not yet scored goes back to its level to fall again.
            AlleyItem item = live[i];
            if (clearedLevel != null && item.State != AlleyItem.Phase.Popping) clearedLevel.Return(item.Kind, item.ColorId);
            Recycle(i);
        }
    }

    // Plays for verification: sorts most items correctly, trashes bombs,
    // and makes a deliberate mistake now and then.
    void TickBot()
    {
        // Paced like a person: a drop has to have been seen for autoReact
        // before it can be swiped, one swipe per autoSwipeGap, lowest first.
        if (Time.time < botReadyAt) return;
        AlleyItem target = null;
        for (int i = 0; i < live.Count; i++)
        {
            AlleyItem item = live[i];
            if (item.State != AlleyItem.Phase.Falling || botSkipped.Contains(item)) continue;
            if (!launchedAt.TryGetValue(item, out float at) || Time.time - at < autoReact) continue;
            if (target == null || item.transform.position.y < target.transform.position.y) target = item;
        }
        if (target == null) return;
        botReadyAt = Time.time + autoSwipeGap;
        if (Random.value < autoMistakes * 0.3f)
        {
            botSkipped.Add(target);
            return;
        }
        bool mistake = Random.value < autoMistakes;
        int direction;
        if (target.Kind == ItemKind.Bomb) direction = mistake ? -1 : 2;
        else if (target.Kind == ItemKind.Gem) direction = (target.ColorId == 0) != mistake ? -1 : 1;
        else direction = Random.value < 0.5f ? -1 : 1;
        Fling(target, direction);
    }
}
