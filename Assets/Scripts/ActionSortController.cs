using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class ActionSortController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private GameObject chuteRoot;
    [SerializeField] private GameObject sortingBoard;
    [SerializeField] private GameObject vaultBanner;
    [SerializeField] private GameObject plinkoRoot;
    [SerializeField] private PlinkoMiniGameManager plinko;

    [Header("Bins")]
    [SerializeField] private int leftColorId = 0;
    [SerializeField] private int rightColorId = 1;
    [SerializeField] private JuicyUIPop leftBin;
    [SerializeField] private JuicyUIPop rightBin;
    [SerializeField] private Image leftVatFill;
    [SerializeField] private Image rightVatFill;
    [SerializeField] private Image alarmFlash;

    [Header("Pool")]
    [SerializeField] private SortNode nodePrefab;
    [SerializeField] private RectTransform nodeParent;
    [SerializeField] private int poolSize = 12;

    [Header("Motion")]
    [SerializeField] private float spawnInterval = 0.85f;
    [SerializeField] private float fallSpeed = 700f;
    [SerializeField] private float sideSpeed = 2200f;
    [SerializeField] private float spawnY = 460f;
    [SerializeField] private float missY = -520f;
    [SerializeField] private float catchX = 280f;
    [SerializeField] private float flickDistance = 80f;
    [SerializeField] private int correctsToFillVat = 3;
    [SerializeField] private float missileDamage = 25f;
    [SerializeField] private float penaltyStep = 0.1f;
    [SerializeField] private float bombChance = 0.15f;
    [SerializeField] private float letterChance = 0.10f;
    [SerializeField] private float goldChance = 0.10f;
    [SerializeField] private float trashY = 860f;

    static readonly float[] LaneX = { -190f, 0f, 190f };
    Image[] laneMarkers;

    static readonly Color[] Palette =
    {
        new Color(0.62f, 0.05f, 0.10f),
        new Color(0.16f, 0.14f, 0.52f),
        new Color(0.93f, 0.28f, 0.22f),
        new Color(0.42f, 0.70f, 0.96f)
    };

    static readonly Color BombColor = new Color(0.10f, 0.07f, 0.07f);
    static readonly Color GoldColor = new Color(0.98f, 0.78f, 0.18f);
    static readonly Color LetterColor = new Color(1f, 0.9f, 0.45f);

    MissilePowerUpController launchButton;

    readonly Queue<SortNode> inactive = new Queue<SortNode>(12);
    readonly List<SortNode> live = new List<SortNode>(12);

    float spawnTimer;
    float leftVat;
    float rightVat;
    float alarmTimer;
    bool pressing;
    bool vaultOpen;
    bool slingHold;
    Vector2 pressStart;
    readonly bool[] vatLocked = new bool[2];
    readonly int[] bankedSorts = new int[2];
    bool listening;
    bool panicHold;
    float baseFallSpeed;
    float baseSideSpeed;
    int craftRed;
    int craftBlue;

    public float FallSpeed => fallSpeed;
    public int LaneCount { get; private set; } = 1;
    public int CraftRed => craftRed;
    public int CraftBlue => craftBlue;
    public bool AlarmLit => alarmTimer > 0.001f;
    public bool PanicHeld => panicHold;
    public int LeftColorId => leftColorId;
    public int RightColorId => rightColorId;
    public float BombChance => bombChance;
    public float LetterChance => letterChance;
    public float GoldChance => goldChance;

    public bool TubesHidden => sortingBoard == null || !sortingBoard.activeSelf;
    public bool BannerHidden => vaultBanner == null || !vaultBanner.activeSelf;
    public bool ChuteVisible => chuteRoot != null && chuteRoot.activeSelf;
    public bool PlinkoVisible => plinkoRoot != null && plinkoRoot.activeSelf;

    public static int FlickDirection(Vector2 start, Vector2 end, float minDistance)
    {
        Vector2 delta = end - start;
        if (delta.magnitude < minDistance) return 0;
        if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
            return delta.y > 0f ? 2 : 0;
        return delta.x < 0f ? -1 : 1;
    }

    void Awake()
    {
        baseFallSpeed = fallSpeed;
        baseSideSpeed = sideSpeed;
        if (nodePrefab == null || nodeParent == null) return;

        for (int i = 0; i < poolSize; i++)
        {
            SortNode node = Instantiate(nodePrefab, nodeParent);
            node.gameObject.SetActive(false);
            inactive.Enqueue(node);
        }
    }

    void OnEnable()
    {
        ListenForRoundReset();
    }

    void Start()
    {
        ListenForRoundReset();
        launchButton = FindFirstObjectByType<MissilePowerUpController>();
        if (nodeParent != null)
            VaultHeistController.SeatOnLayer(nodeParent.gameObject, "Gameplay", 12);
        EnsurePresentation();
        TintBins();
        EnsureLaneMarkers();
        ApplyLanes();
        if (sortingBoard != null) sortingBoard.SetActive(false);
        if (vaultBanner != null) vaultBanner.SetActive(false);
        if (plinkoRoot != null) plinkoRoot.SetActive(false);
        spawnTimer = spawnInterval;
    }

    void OnDisable()
    {
        if (!listening || GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnStateChanged -= HandleRoundState;
        listening = false;
    }

    void ListenForRoundReset()
    {
        if (listening || GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnStateChanged += HandleRoundState;
        listening = true;
    }

    void HandleRoundState(GameCoreManager.GameState state)
    {
        if (state == GameCoreManager.GameState.PuzzleLoop)
            ResetChute();
    }

    public void ResetChute()
    {
        StopAllCoroutines();
        vaultOpen = false;
        leftVat = 0f;
        rightVat = 0f;
        vatLocked[0] = false;
        vatLocked[1] = false;
        bankedSorts[0] = 0;
        bankedSorts[1] = 0;
        alarmTimer = 0f;
        slingHold = false;
        panicHold = false;
        spawnTimer = spawnInterval;
        craftRed = 0;
        craftBlue = 0;
        if (VaultHeistController.Instance != null)
            VaultHeistController.Instance.SetVaultState(false);

        for (int i = live.Count - 1; i >= 0; i--)
            Recycle(i);

        if (sortingBoard != null) sortingBoard.SetActive(false);
        if (vaultBanner != null) vaultBanner.SetActive(false);
        if (plinkoRoot != null) plinkoRoot.SetActive(false);
        if (chuteRoot != null) chuteRoot.SetActive(true);
        ApplyLanes();
        if (SlingModeSpawner.Instance != null && SlingModeSpawner.Instance.Running)
            SlingModeSpawner.Instance.End();
        Debug.Log("Chute reset for the next round. Lanes " + LaneCount + ".");
    }

    public void SetSlingHold(bool held)
    {
        slingHold = held;
        if (held)
        {
            for (int i = live.Count - 1; i >= 0; i--)
                Recycle(i);
            if (chuteRoot != null) chuteRoot.SetActive(false);
            return;
        }

        if (!vaultOpen && chuteRoot != null) chuteRoot.SetActive(true);
    }

    public void SetPanicHold(bool held)
    {
        panicHold = held;
        if (held) pressing = false;
    }

    public void DiscardStageLoot()
    {
        Debug.Log("Busted. Stage loot lost: " + craftRed + " thermite and " + craftBlue + " coolant.");
        craftRed = 0;
        craftBlue = 0;
    }

    public Color BinColor(int colorId)
    {
        return Palette[Mathf.Clamp(colorId, 0, Palette.Length - 1)];
    }

    void EnsurePresentation()
    {
        ChuteContrast.Ensure();
        CasinoDressing.Ensure();
        Camera view = Camera.main;
        if (view != null)
        {
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.07f, 0.065f, 0.06f, 1f);
        }
        if (WordVaultManager.Instance == null)
            new GameObject("WordVault").AddComponent<WordVaultManager>();
        if (ItemPropPool.Instance == null)
            new GameObject("ItemProps").AddComponent<ItemPropPool>();
        if (LiquidVatView.Instance == null)
            new GameObject("LiquidVats").AddComponent<LiquidVatView>();
        if (LiquidVatView.Instance != null)
            LiquidVatView.Instance.Bind(leftBin, rightBin);
        HeatManager.Ensure();
        StreakMultiplierManager.Ensure();
        LockdownEscapeController.Ensure();
        VaultHeistController.Ensure();
        GameObject lane = GameObject.Find("Lane");
        if (lane != null) lane.SetActive(false);
        GameObject escapeTrack = GameObject.Find("EscapeTrack");
        if (escapeTrack != null) escapeTrack.SetActive(false);
    }

    public SortNode.NodeKind KindForRoll(float roll)
    {
        if (roll < bombChance) return SortNode.NodeKind.Bomb;
        if (roll < bombChance + letterChance) return SortNode.NodeKind.Letter;
        if (roll < bombChance + letterChance + goldChance) return SortNode.NodeKind.Gold;
        return SortNode.NodeKind.Gem;
    }

    public string Present(SortNode.NodeKind kind, int colorId, char letter)
    {
        if (inactive.Count == 0) return "";
        SortNode node = inactive.Dequeue();
        if (kind == SortNode.NodeKind.Letter && letter == '\0' && WordVaultManager.Instance != null)
            letter = WordVaultManager.Instance.PickLetter();
        Color color = ColorFor(kind, colorId);
        node.Launch(colorId, color, new Vector2(NextSpawnX(), spawnY), kind, letter);
        live.Add(node);
        CueVault(true, colorId);
        if (ItemPropPool.Instance != null) ItemPropPool.Instance.Attach(node);
        return ItemPropPool.Instance != null ? ItemPropPool.Instance.LastMeshName : "";
    }

    public float NextSpawnX()
    {
        if (LaneCount <= 1) return 0f;
        if (LaneCount == 2) return Random.value < 0.5f ? LaneX[0] : LaneX[2];
        return LaneX[Random.Range(0, LaneX.Length)];
    }

    void ApplyLanes()
    {
        int leg = HeistCombativeManager.Instance != null ? HeistCombativeManager.Instance.RunLeg : 1;
        LaneCount = leg <= 1 ? 1 : leg == 2 ? 2 : 3;
        RefreshLaneMarkers();
    }

    void EnsureLaneMarkers()
    {
        if (laneMarkers != null || nodeParent == null) return;
        Sprite sprite = null;
        if (nodePrefab != null)
        {
            Image source = nodePrefab.GetComponent<Image>();
            if (source == null) source = nodePrefab.GetComponentInChildren<Image>(true);
            if (source != null) sprite = source.sprite;
        }

        laneMarkers = new Image[LaneX.Length];
        for (int i = 0; i < LaneX.Length; i++)
        {
            var go = new GameObject("LaneMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(nodeParent, false);
            go.transform.SetAsFirstSibling();
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(8f, 760f);
            rect.anchoredPosition = new Vector2(LaneX[i], spawnY - 380f);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, 0.14f);
            image.raycastTarget = false;
            laneMarkers[i] = image;
            go.SetActive(false);
        }
    }

    void RefreshLaneMarkers()
    {
        if (laneMarkers == null) return;
        for (int i = 0; i < laneMarkers.Length; i++)
        {
            if (laneMarkers[i] == null) continue;
            bool show = LaneCount >= 3 || (LaneCount == 1 && i == 1) || (LaneCount == 2 && i != 1);
            laneMarkers[i].gameObject.SetActive(show);
        }
    }

    public void TakeCraftStash(out int red, out int blue)
    {
        red = craftRed;
        blue = craftBlue;
        craftRed = 0;
        craftBlue = 0;
    }

    public void AddCraft(int red, int blue)
    {
        if (red > 0) craftRed += red;
        if (blue > 0) craftBlue += blue;
    }

    public bool TrySpendCraft(int red, int blue)
    {
        if (craftRed < red || craftBlue < blue) return false;
        craftRed -= red;
        craftBlue -= blue;
        return true;
    }

    public void SetRunPace(float scale)
    {
        float pace = Mathf.Max(0.5f, scale);
        fallSpeed = baseFallSpeed * pace;
        sideSpeed = baseSideSpeed * pace;
    }

    void Update()
    {
        if (vaultOpen || slingHold || panicHold || JuicyWinTicker.PayoutOpen) return;

        TickAlarm();
        TickVats();
        TickPointer();
        TickNodes();
        TickSpawn();
    }

    public void ResolveCatch(SortNode.NodeKind kind, int colorId, int side, char letter = '\0')
    {
        if (vaultOpen) return;
        if (LiquidVatView.Instance != null) LiquidVatView.Instance.Splash(side);

        if (kind == SortNode.NodeKind.Bomb)
        {
            LoseCraft(1, 1);
            alarmTimer = 0.28f;
            if (StreakMultiplierManager.Instance != null) StreakMultiplierManager.Instance.Break();
            if (HeatManager.Instance != null) HeatManager.Instance.ReportHazard();
            Debug.Log("Bomb sorted into a bin. Stash " + craftRed + " thermite, " + craftBlue + " coolant.");
            return;
        }

        if (kind == SortNode.NodeKind.Letter)
        {
            if (WordVaultManager.Instance != null) WordVaultManager.Instance.Bank(letter);
            JuicyUIPop letterBin = side == 0 ? leftBin : rightBin;
            if (letterBin != null) letterBin.TriggerPunch(1.12f);
            return;
        }

        if (kind == SortNode.NodeKind.Gold)
        {
            GrantGold();
            return;
        }

        bool correct = side == 0 ? colorId == leftColorId : colorId == rightColorId;
        if (correct)
        {
            JuicyUIPop bin = side == 0 ? leftBin : rightBin;
            if (bin != null) bin.TriggerPunch(1.12f);
            NotifyCorrectSort(colorId);
            if (StreakMultiplierManager.Instance != null) StreakMultiplierManager.Instance.ReportSuccess();
            if (HeatManager.Instance != null) HeatManager.Instance.ReportSuccess();
            return;
        }

        DrainVat(side);
        if (StreakMultiplierManager.Instance != null) StreakMultiplierManager.Instance.Break();
        if (HeatManager.Instance != null) HeatManager.Instance.ReportMisSort();
    }

    public void ResolveTrash(SortNode.NodeKind kind)
    {
        if (vaultOpen) return;

        if (kind == SortNode.NodeKind.Bomb)
        {
            Debug.Log("Bomb trashed.");
            return;
        }

        if (kind == SortNode.NodeKind.Gold)
            GrantGold();
        else if (kind == SortNode.NodeKind.Gem && HeatManager.Instance != null)
            HeatManager.Instance.ReportError();
    }

    public void ResolveMiss(SortNode.NodeKind kind)
    {
        if (vaultOpen) return;
        if (kind == SortNode.NodeKind.Bomb)
            Debug.Log("Bomb dropped off the chute.");
        else if (kind == SortNode.NodeKind.Gem && HeatManager.Instance != null)
            HeatManager.Instance.ReportError();
    }

    void GrantGold()
    {
        AddCraft(2, 2);
        if (PlayerInventory.Instance != null) PlayerInventory.Instance.ShowBoost("+2");
        if (JuicyWinTicker.Instance != null) JuicyWinTicker.Instance.Punch();
        Debug.Log("Gold collected. Stash " + craftRed + " thermite, " + craftBlue + " coolant.");
    }

    void LoseCraft(int red, int blue)
    {
        craftRed = Mathf.Max(0, craftRed - Mathf.Max(0, red));
        craftBlue = Mathf.Max(0, craftBlue - Mathf.Max(0, blue));
    }

    void TintBins()
    {
        ThemeConfig theme = GameCoreManager.Instance != null ? GameCoreManager.Instance.ActiveTheme : null;
        TintBin(leftBin, "deposit_chest_crimson", "CRIMSON", theme != null ? theme.leftBinSprite : null);
        TintBin(rightBin, "deposit_chest_indigo", "INDIGO", theme != null ? theme.rightBinSprite : null);
    }

    static Sprite[] chestSprites;

    static Sprite ChestSprite(string spriteName)
    {
        Sprite direct = Resources.Load<Sprite>("UIHeroes/" + spriteName);
        if (direct != null) return direct;
        if (chestSprites == null)
            chestSprites = Resources.LoadAll<Sprite>("UIHeroes/deposit_chests");
        if (chestSprites == null) return null;
        for (int i = 0; i < chestSprites.Length; i++)
        {
            if (chestSprites[i] != null && chestSprites[i].name == spriteName)
                return chestSprites[i];
        }
        return null;
    }

    static void TintBin(JuicyUIPop bin, string spriteName, string label, Sprite housing)
    {
        if (bin == null) return;
        Image image = bin.GetComponent<Image>();
        string intake = spriteName == "deposit_chest_crimson" ? "intake_bin_crimson" : "intake_bin_indigo";
        Sprite sprite = housing != null ? housing : Resources.Load<Sprite>("UIHeroes/" + intake);
        if (sprite == null) sprite = ChestSprite(spriteName);
        if (image != null && sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }
        else if (sprite == null)
            Debug.LogWarning("Deposit chest sprite missing: " + spriteName);
        VaultHeistController.SeatOnLayer(bin.gameObject, "Gameplay", 8);
        TextMeshProUGUI[] labels = bin.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (sprite != null)
            {
                labels[i].enabled = false;
                continue;
            }

            labels[i].text = label;
            labels[i].fontSize = 32f;
        }
    }

    public void NotifyCorrectSort(int colorId)
    {
        if (vaultOpen) return;

        int side = colorId == rightColorId ? 1 : 0;
        if (vatLocked[side])
        {
            bankedSorts[side]++;
            return;
        }

        if (side == 0) craftRed++;
        else craftBlue++;

        float step = 1f / Mathf.Max(1, correctsToFillVat);
        if (side == 0) leftVat = Mathf.Min(1f, leftVat + step);
        else rightVat = Mathf.Min(1f, rightVat + step);

        if ((side == 0 ? leftVat : rightVat) >= 0.999f)
            StartCoroutine(FireVat(side));
    }

    public void RegisterSort(bool correct)
    {
        if (vaultOpen || correct) return;
    }

    void TickPointer()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            Vector2 screen = pointer.position.ReadValue();
            if (launchButton != null && launchButton.ContainsScreenPoint(screen))
            {
                pressing = false;
                return;
            }

            pressing = true;
            pressStart = screen;
            return;
        }

        if (!pressing || !pointer.press.wasReleasedThisFrame) return;

        pressing = false;
        int direction = FlickDirection(pressStart, pointer.position.ReadValue(), flickDistance);
        if (direction != 0) FlingFrontNode(direction);
    }

    void TickSpawn()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer < spawnInterval) return;
        spawnTimer = 0f;
        SpawnNode();
    }

    void SpawnNode()
    {
        if (inactive.Count == 0) return;

        SortNode node = inactive.Dequeue();
        SortNode.NodeKind kind = KindForRoll(Random.value);
        int colorId = kind == SortNode.NodeKind.Gem ? Random.Range(0, Palette.Length) : 0;
        char letter = '\0';
        if (kind == SortNode.NodeKind.Letter && WordVaultManager.Instance != null)
            letter = WordVaultManager.Instance.PickLetter();
        node.Launch(colorId, ColorFor(kind, colorId), new Vector2(NextSpawnX(), spawnY), kind, letter);
        live.Add(node);
        CueVault(true, colorId);
        if (ItemPropPool.Instance != null) ItemPropPool.Instance.Attach(node);
    }

    Color ColorFor(SortNode.NodeKind kind, int colorId)
    {
        if (kind == SortNode.NodeKind.Bomb) return BombColor;
        if (kind == SortNode.NodeKind.Gold) return GoldColor;
        if (kind == SortNode.NodeKind.Letter) return LetterColor;
        return Palette[Mathf.Clamp(colorId, 0, Palette.Length - 1)];
    }

    void TickNodes()
    {
        float blurReference = Mathf.Max(1f, sideSpeed);

        for (int i = live.Count - 1; i >= 0; i--)
        {
            SortNode node = live[i];
            Vector2 position = node.Rect.anchoredPosition;
            float speed;

            if (node.Direction == 0)
            {
                position.y -= fallSpeed * Time.deltaTime;
                speed = fallSpeed;
                if (position.y <= missY)
                {
                    SortNode.NodeKind kind = node.Kind;
                    Recycle(i);
                    ResolveMiss(kind);
                    if (vaultOpen || panicHold) return;
                    continue;
                }
            }
            else if (node.Direction == 2)
            {
                position.y += sideSpeed * Time.deltaTime;
                speed = sideSpeed;
                if (position.y >= trashY)
                {
                    SortNode.NodeKind kind = node.Kind;
                    Recycle(i);
                    ResolveTrash(kind);
                    if (vaultOpen || panicHold) return;
                    continue;
                }
            }
            else
            {
                position.x += node.Direction * sideSpeed * Time.deltaTime;
                speed = sideSpeed;
                if (Mathf.Abs(position.x) >= catchX)
                {
                    SortNode.NodeKind kind = node.Kind;
                    int colorId = node.ColorId;
                    char letter = node.Letter;
                    int side = node.Direction < 0 ? 0 : 1;
                    Recycle(i);
                    ResolveCatch(kind, colorId, side, letter);
                    if (vaultOpen || panicHold) return;
                    continue;
                }
            }

            node.Rect.anchoredPosition = position;
            node.SetBlur(Mathf.Clamp01(speed / blurReference));
            if (ItemPropPool.Instance != null) ItemPropPool.Instance.Sync(node);
        }
    }

    void FlingFrontNode(int direction)
    {
        SortNode front = null;
        float lowest = float.MaxValue;

        for (int i = 0; i < live.Count; i++)
        {
            SortNode node = live[i];
            if (node.Direction != 0) continue;
            float y = node.Rect.anchoredPosition.y;
            if (y < lowest)
            {
                lowest = y;
                front = node;
            }
        }

        if (front != null) front.Fling(direction);
    }

    void CueVault(bool openDoor, int colorId)
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.RaiseVaultCue(openDoor, colorId == rightColorId);
    }

    void Recycle(int liveIndex)
    {
        SortNode node = live[liveIndex];
        int last = live.Count - 1;
        live[liveIndex] = live[last];
        live.RemoveAt(last);
        if (live.Count == 0) CueVault(false, leftColorId);
        if (ItemPropPool.Instance != null) ItemPropPool.Instance.Release(node);
        node.gameObject.SetActive(false);
        inactive.Enqueue(node);
    }

    void TickVats()
    {
        float wobble = 1f + Mathf.Sin(Time.time * 7f) * 0.04f;
        ApplyVat(leftVatFill, leftVat, wobble);
        ApplyVat(rightVatFill, rightVat, wobble);
        if (LiquidVatView.Instance == null) return;
        LiquidVatView.Instance.SetFill(0, leftVat);
        LiquidVatView.Instance.SetFill(1, rightVat);
    }

    static void ApplyVat(Image fill, float amount, float wobble)
    {
        if (fill == null) return;
        fill.fillAmount = amount;
        fill.rectTransform.localScale = Vector3.one * (amount > 0.01f && amount < 0.999f ? wobble : 1f);
    }

    void DrainVat(int side)
    {
        if (vatLocked[side]) return;
        if (side == 0) leftVat = Mathf.Max(0f, leftVat - penaltyStep);
        else rightVat = Mathf.Max(0f, rightVat - penaltyStep);
    }

    IEnumerator FireVat(int side)
    {
        vatLocked[side] = true;
        if (side == 0) leftVat = 1f;
        else rightVat = 1f;

        JuicyUIPop bin = side == 0 ? leftBin : rightBin;
        if (bin != null) bin.TriggerPunch(1.28f);

        int colorId = side == 0 ? leftColorId : rightColorId;
        if (HeistCombativeManager.Instance != null)
            HeistCombativeManager.Instance.LaunchMissileAtBarrier(missileDamage, Palette[colorId]);

        yield return new WaitForSeconds(0.2f);

        if (side == 0) leftVat = 0f;
        else rightVat = 0f;
        vatLocked[side] = false;

        int extra = bankedSorts[side];
        bankedSorts[side] = 0;
        for (int i = 0; i < extra; i++)
            NotifyCorrectSort(colorId);
    }

    void TickAlarm()
    {
        if (alarmFlash == null) return;

        Color color = alarmFlash.color;
        float bomb = 0f;
        if (alarmTimer > 0f)
        {
            alarmTimer -= Time.deltaTime;
            bomb = Mathf.Clamp01(alarmTimer / 0.28f) * 0.45f;
        }

        float heat = HeatManager.Instance != null ? HeatManager.Instance.AlarmAlpha : 0f;
        color.a = Mathf.Max(bomb, heat);
        alarmFlash.color = color;
    }

    public void BeginBonusRound()
    {
        vaultOpen = true;
        if (VaultHeistController.Instance != null)
            VaultHeistController.Instance.SetVaultState(true);
        if (chuteRoot != null) chuteRoot.SetActive(false);
        if (plinkoRoot != null) plinkoRoot.SetActive(true);

        if (GameCoreManager.Instance != null)
            GameCoreManager.Instance.ChangeState(GameCoreManager.GameState.BonusMiniGame);

        if (plinko == null || plinko.SlotCount == 0) return;
        plinko.BeginRound();
    }
}
