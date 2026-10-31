using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Five second hack. The main clock keeps running. Physics is not paused.
public class LockdownEscapeController : MonoBehaviour
{
    public const float EscapeSeconds = 5f;
    public const int EscapeSorts = 4;

    public static LockdownEscapeController Instance { get; private set; }

    const float SpawnY = 220f;
    const float MissY = -320f;
    const float TrashY = 400f;
    const float CatchX = 240f;
    const float FallSpeed = 980f;
    const float SideSpeed = 2400f;
    const float SpawnGap = 0.48f;

    readonly List<int> plan = new List<int>(EscapeSorts);
    readonly List<Drop> pool = new List<Drop>(EscapeSorts);
    readonly List<Drop> live = new List<Drop>(EscapeSorts);

    bool subscribed;
    bool running;
    bool escaped;
    bool failed;
    int correct;
    int leftColorId;
    int rightColorId;
    float timeLeft;
    float timeScaleAtStart = 1f;
    float bannerSeconds = 0.55f;
    float spawnWait;

    RectTransform board;
    Image doorFill;
    Image leftBinImage;
    Image rightBinImage;
    TextMeshProUGUI timerLabel;
    TextMeshProUGUI countLabel;
    TextMeshProUGUI bannerLabel;
    float leftFlash;
    float rightFlash;
    bool pressing;
    Vector2 pressStart;

    public bool Running => running;
    public bool Escaped => escaped;
    public bool Failed => failed;
    public int CorrectSorts => correct;
    public float TimeLeft => timeLeft;
    public float TimeScaleAtStart => timeScaleAtStart;
    public float BannerSeconds
    {
        get => bannerSeconds;
        set => bannerSeconds = Mathf.Max(0.05f, value);
    }

    class Drop
    {
        public RectTransform Rect;
        public Image Image;
        public int ColorId;
        public int Direction;
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("LockdownEscape").AddComponent<LockdownEscapeController>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnEnable()
    {
        Subscribe();
    }

    void Start()
    {
        Subscribe();
    }

    void OnDisable()
    {
        if (!subscribed || HeatManager.Instance == null) return;
        HeatManager.Instance.OnLockdownTriggered -= HandleLockdown;
        subscribed = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Subscribe()
    {
        if (subscribed || HeatManager.Instance == null) return;
        HeatManager.Instance.OnLockdownTriggered += HandleLockdown;
        subscribed = true;
    }

    void HandleLockdown()
    {
        if (running) return;
        StartCoroutine(RunPanic());
    }

    public int FrontCorrectDirection()
    {
        Drop front = Front();
        if (front == null) return 0;
        if (front.ColorId == leftColorId) return -1;
        if (front.ColorId == rightColorId) return 1;
        return 0;
    }

    public void SubmitSort(int direction)
    {
        if (!running || direction == 0) return;
        Drop front = Front();
        if (front == null) return;
        front.Direction = direction == 2 ? 2 : direction < 0 ? -1 : 1;
    }

    IEnumerator RunPanic()
    {
        running = true;
        escaped = false;
        failed = false;
        correct = 0;
        timeLeft = EscapeSeconds;
        timeScaleAtStart = Time.timeScale;
        ReadColors();
        BuildBoard();
        BuildPlan();
        DespawnAll();
        spawnWait = 0f;

        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) sort.SetPanicHold(true);
        SetBoardVisible(true);
        Debug.Log("Lockdown started. Time scale " + timeScaleAtStart.ToString("0.00") + ".");

        int spawned = 0;
        while (running)
        {
            float dt = Time.deltaTime;
            timeLeft -= dt;
            spawnWait -= dt;
            if (spawned < plan.Count && spawnWait <= 0f)
            {
                Spawn(plan[spawned]);
                spawned++;
                spawnWait = SpawnGap;
            }

            TickNodes();
            TickInput();
            TickBins(dt);
            RefreshHud();

            if (correct >= EscapeSorts) escaped = true;
            else if (timeLeft <= 0f || !CanStillClear(spawned)) failed = true;

            if (escaped || failed) break;
            yield return null;
        }

        if (escaped) yield return FinishEscape();
        else yield return FinishBust(timeLeft <= 0f);
        running = false;
    }

    IEnumerator FinishEscape()
    {
        ShowBanner("DOOR OPEN", new Color(1f, 0.86f, 0.35f, 1f));
        if (HeatManager.Instance != null) HeatManager.Instance.ResolveLockdown();
        yield return new WaitForSeconds(bannerSeconds);
        SetBoardVisible(false);
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) sort.SetPanicHold(false);
    }

    IEnumerator FinishBust(bool timerRanOut)
    {
        ShowBanner("BUSTED", new Color(1f, 0.28f, 0.22f, 1f));
        if (timerRanOut) Debug.Log("Busted. The escape timer ran out.");
        else Debug.Log("Busted. The escape chute was missed.");
        yield return new WaitForSeconds(bannerSeconds);

        // Run stash only. Saved gate stock and the damaged towers stay as they are.
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null)
        {
            sort.DiscardStageLoot();
            sort.ResetChute();
        }

        if (HeatManager.Instance != null) HeatManager.Instance.ResolveLockdown();
        SetBoardVisible(false);
        int leg = HeistCombativeManager.Instance != null ? HeistCombativeManager.Instance.RunLeg : 0;
        Debug.Log("Busted. Checkpoint restart on leg " + leg + ".");
    }

    void ReadColors()
    {
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        leftColorId = sort != null ? sort.LeftColorId : 0;
        rightColorId = sort != null ? sort.RightColorId : 1;
    }

    void BuildPlan()
    {
        plan.Clear();
        for (int i = 0; i < EscapeSorts; i++)
            plan.Add(i % 2 == 0 ? leftColorId : rightColorId);

        for (int i = plan.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int swap = plan[i];
            plan[i] = plan[j];
            plan[j] = swap;
        }
    }

    bool CanStillClear(int spawnedCount)
    {
        int possible = correct + (plan.Count - spawnedCount);
        for (int i = 0; i < live.Count; i++)
        {
            Drop drop = live[i];
            if (drop.Direction == 0) possible++;
            else if (drop.Direction < 0 && drop.ColorId == leftColorId) possible++;
            else if (drop.Direction > 0 && drop.Direction != 2 && drop.ColorId == rightColorId) possible++;
        }

        return possible >= EscapeSorts;
    }

    void TickNodes()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            Drop drop = live[i];
            Vector2 position = drop.Rect.anchoredPosition;
            if (drop.Direction == 0)
            {
                position.y -= FallSpeed * Time.deltaTime;
                if (position.y <= MissY)
                {
                    Despawn(i);
                    continue;
                }
            }
            else if (drop.Direction == 2)
            {
                position.y += SideSpeed * Time.deltaTime;
                if (position.y >= TrashY)
                {
                    Despawn(i);
                    continue;
                }
            }
            else
            {
                position.x += drop.Direction * SideSpeed * Time.deltaTime;
                if (Mathf.Abs(position.x) >= CatchX)
                {
                    int side = drop.Direction < 0 ? 0 : 1;
                    bool hit = side == 0 ? drop.ColorId == leftColorId : drop.ColorId == rightColorId;
                    if (hit)
                    {
                        correct++;
                        if (side == 0) leftFlash = 0.12f;
                        else rightFlash = 0.12f;
                    }

                    Despawn(i);
                    continue;
                }
            }

            drop.Rect.anchoredPosition = position;
        }
    }

    void TickInput()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            pressing = true;
            pressStart = pointer.position.ReadValue();
        }

        if (!pressing || !pointer.press.wasReleasedThisFrame) return;
        pressing = false;
        int direction = ActionSortController.FlickDirection(pressStart, pointer.position.ReadValue(), 70f);
        if (direction != 0) SubmitSort(direction);
    }

    void TickBins(float dt)
    {
        leftFlash = Mathf.Max(0f, leftFlash - dt);
        rightFlash = Mathf.Max(0f, rightFlash - dt);
        PaintBin(leftBinImage, LeftColor(), leftFlash);
        PaintBin(rightBinImage, RightColor(), rightFlash);
    }

    static void PaintBin(Image image, Color color, float flash)
    {
        if (image == null) return;
        image.color = Color.Lerp(color, Color.white, flash > 0f ? 0.45f : 0f);
    }

    void RefreshHud()
    {
        if (timerLabel != null)
        {
            timerLabel.text = Mathf.Max(0f, timeLeft).ToString("0.0");
            timerLabel.color = timeLeft < 2f
                ? new Color(1f, 0.35f, 0.28f, 1f)
                : new Color(1f, 0.94f, 0.88f, 1f);
        }

        if (countLabel != null) countLabel.text = correct + " / " + EscapeSorts;
        if (doorFill != null) doorFill.fillAmount = Mathf.Clamp01(correct / (float)EscapeSorts);
    }

    void ShowBanner(string text, Color color)
    {
        if (bannerLabel == null) return;
        bannerLabel.gameObject.SetActive(true);
        bannerLabel.text = text;
        bannerLabel.color = color;
    }

    Drop Front()
    {
        Drop front = null;
        float lowest = float.MaxValue;
        for (int i = 0; i < live.Count; i++)
        {
            Drop drop = live[i];
            if (drop.Direction != 0) continue;
            float y = drop.Rect.anchoredPosition.y;
            if (y < lowest)
            {
                lowest = y;
                front = drop;
            }
        }

        return front;
    }

    void Spawn(int colorId)
    {
        Drop drop = Rent();
        drop.ColorId = colorId;
        drop.Direction = 0;
        drop.Image.color = colorId == rightColorId ? RightColor() : LeftColor();
        drop.Rect.anchoredPosition = new Vector2(0f, SpawnY);
        drop.Rect.localScale = Vector3.one;
        drop.Rect.gameObject.SetActive(true);
        live.Add(drop);
    }

    Drop Rent()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].Rect.gameObject.activeSelf) return pool[i];
        }

        Sprite sprite = HeatManager.WhiteSprite();
        var go = new GameObject("PanicNode", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(board, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(72f, 72f);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        var drop = new Drop { Rect = rect, Image = image };
        pool.Add(drop);
        go.SetActive(false);
        return drop;
    }

    void Despawn(int index)
    {
        Drop drop = live[index];
        int last = live.Count - 1;
        live[index] = live[last];
        live.RemoveAt(last);
        drop.Rect.gameObject.SetActive(false);
    }

    void DespawnAll()
    {
        for (int i = 0; i < live.Count; i++)
            live[i].Rect.gameObject.SetActive(false);
        live.Clear();
    }

    Color LeftColor()
    {
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) return sort.BinColor(leftColorId);
        return new Color(0.62f, 0.05f, 0.10f, 1f);
    }

    Color RightColor()
    {
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) return sort.BinColor(rightColorId);
        return new Color(0.16f, 0.14f, 0.52f, 1f);
    }

    void SetBoardVisible(bool visible)
    {
        if (board == null) return;
        board.gameObject.SetActive(visible);
        if (!visible && bannerLabel != null) bannerLabel.gameObject.SetActive(false);
    }

    void BuildBoard()
    {
        if (board != null) return;
        Canvas canvas = HeatManager.FindHud();
        if (canvas == null) return;

        Sprite sprite = HeatManager.WhiteSprite();
        var go = new GameObject("LockdownBoard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        board = go.GetComponent<RectTransform>();
        board.anchorMin = new Vector2(0.5f, 0.5f);
        board.anchorMax = new Vector2(0.5f, 0.5f);
        board.pivot = new Vector2(0.5f, 0.5f);
        board.anchoredPosition = new Vector2(0f, -40f);
        board.sizeDelta = new Vector2(820f, 920f);
        var panel = go.GetComponent<Image>();
        panel.sprite = sprite;
        panel.color = new Color(0.07f, 0.02f, 0.04f, 0.94f);
        panel.raycastTarget = false;
        HeatManager.PlaceUnderFade(canvas, go.transform);

        var doorBack = MakeImage("DoorBack", board, sprite, new Vector2(0f, 390f), new Vector2(520f, 22f));
        doorBack.color = new Color(0.2f, 0.08f, 0.08f, 1f);
        doorFill = MakeImage("DoorFill", board, sprite, new Vector2(0f, 390f), new Vector2(520f, 22f));
        doorFill.type = Image.Type.Filled;
        doorFill.fillMethod = Image.FillMethod.Horizontal;
        doorFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        doorFill.fillAmount = 0f;
        doorFill.color = new Color(0.98f, 0.78f, 0.22f, 1f);

        leftBinImage = MakeImage("PanicLeft", board, sprite, new Vector2(-300f, 0f), new Vector2(130f, 220f));
        rightBinImage = MakeImage("PanicRight", board, sprite, new Vector2(300f, 0f), new Vector2(130f, 220f));
        leftBinImage.color = LeftColor();
        rightBinImage.color = RightColor();

        timerLabel = MakeText("PanicTimer", board, new Vector2(0f, 300f), 72f);
        countLabel = MakeText("PanicCount", board, new Vector2(0f, 230f), 32f);
        bannerLabel = MakeText("PanicBanner", board, new Vector2(0f, 40f), 78f);
        TextMeshProUGUI title = MakeText("PanicTitle", board, new Vector2(0f, 340f), 28f);
        if (title != null) title.text = "HACK THE DOOR";
        if (bannerLabel != null) bannerLabel.gameObject.SetActive(false);
        if (countLabel != null) countLabel.text = "0 / " + EscapeSorts;
        go.SetActive(false);
    }

    static Image MakeImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, Vector2 position, float size)
    {
        TMP_FontAsset font = HeatManager.DefaultFont();
        if (font == null) return null;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(640f, 90f);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }
}
