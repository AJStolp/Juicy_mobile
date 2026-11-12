using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(20)]
public class HeistCombativeManager : MonoBehaviour
{
    public static HeistCombativeManager Instance { get; private set; }

    [Header("Barrier")]
    [SerializeField] private float maxTowerHealth = 50f;
    [SerializeField] private int barriersOnRoute = 2;
    [SerializeField] private Image towerHealthFill;
    [SerializeField] private RectTransform[] towers;
    [SerializeField] private JuicyUIPop[] towerPops;
    [SerializeField] private Color fullHealthColor = new Color(0.96f, 0.76f, 0.22f, 1f);
    [SerializeField] private Color lowHealthColor = new Color(0.90f, 0.16f, 0.22f, 1f);

    [Header("Escape")]
    [SerializeField] private RectTransform characterTransform;
    [SerializeField] private Vector2[] escapeStops;
    [SerializeField] private float stepDuration = 0.35f;
    [SerializeField] private TextMeshProUGUI runLabel;
    [SerializeField] private int maxRunLeg = 6;
    [SerializeField] private float paceStep = 0.12f;
    [SerializeField] private float healthStep = 25f;

    [Header("Shot")]
    [SerializeField] private RectTransform leftMuzzle;
    [SerializeField] private RectTransform rightMuzzle;
    [SerializeField] private RectTransform missileLayer;
    [SerializeField] private RectTransform coinLayer;
    [SerializeField] private Sprite shotSprite;
    [SerializeField] private JuicyWinTicker winTicker;

    const int MissilePool = 4;
    const int SparkPool = 8;
    const int CoinPool = 8;
    const float ChipStep = 0.22f;

    // Right of the sort tubes. The last tube center is viewport x 0.84, about
    // anchored x 367 on the 1080-wide canvas, so 468 keeps a 120-wide car in
    // the margin. The column runs bottom to top: thief, first door, second
    // door, getaway.
    const float StageColumnX = 468f;
    const float ThiefColumnY = -480f;
    const float FirstDoorY = -160f;
    const float SecondDoorY = 140f;
    const float GetawayColumnY = 430f;
    const float LastStopY = 360f;

    struct Mote
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public bool Active;
        public Vector2 Target;
        public bool Homing;
    }

    readonly Mote[] sparks = new Mote[SparkPool];
    readonly Mote[] coins = new Mote[CoinPool];
    readonly RectTransform[] missiles = new RectTransform[MissilePool];
    readonly bool[] missileBusy = new bool[MissilePool];

    float currentTowerHealth;
    Vector3 cameraHome;
    bool cameraHomeStored;
    Coroutine stepRoutine;
    Coroutine shakeRoutine;
    bool celebrationStarted;
    bool listening;
    bool coinUsesHeroArt;
    Vector2 characterHome;
    bool characterHomeStored;
    int runLeg = 1;
    float baseTowerHealth;

    TextMeshProUGUI damageText;
    RectTransform damageRect;
    Coroutine damageRoutine;
    NarrativeStageView stage;

    public int TowersDestroyed { get; private set; }
    public int RunLeg => runLeg;
    public float TowerHealth => maxTowerHealth;
    public float BarrierHealth => currentTowerHealth;
    public float LastShakeDuration { get; private set; }
    public Vector2 LastShotStart { get; private set; }
    public int LastSparkCount { get; private set; }
    public bool RouteCleared { get; private set; }
    public bool CharacterPlaying => stage != null && stage.Playing;
    public Vector2 CharacterAnchoredPosition => characterTransform != null ? characterTransform.anchoredPosition : Vector2.zero;
    public Vector2 CharacterHome => characterHomeStored ? characterHome : CharacterAnchoredPosition;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        baseTowerHealth = maxTowerHealth;
        currentTowerHealth = maxTowerHealth;
        SeatStageColumn();
        if (characterTransform != null)
        {
            characterHome = characterTransform.anchoredPosition;
            characterHomeStored = true;
        }
        BuildShots();
        stage = GetComponent<NarrativeStageView>();
        if (stage == null) stage = gameObject.AddComponent<NarrativeStageView>();
        stage.Bind(characterTransform);
        ListenForRoundReset();
    }

    void OnEnable()
    {
        ListenForRoundReset();
    }

    void Start()
    {
        ListenForRoundReset();
        ApplyHealthBar();
        CacheCameraHome();
        ShowLeg();
        if (MissileFx.Instance == null) gameObject.AddComponent<MissileFx>();
        if (StageModelDressing.Instance == null) gameObject.AddComponent<StageModelDressing>();
        RectTransform getaway = null;
        GameObject car = GameObject.Find("Getaway");
        if (car != null) getaway = car.transform as RectTransform;
        if (getaway != null)
            getaway.anchoredPosition = new Vector2(StageColumnX, GetawayColumnY);
        if (StageModelDressing.Instance != null)
            StageModelDressing.Instance.Bind(characterTransform, towers, getaway);
    }

    void SeatStageColumn()
    {
        if (characterTransform != null)
            characterTransform.anchoredPosition = new Vector2(StageColumnX, ThiefColumnY);
        if (towers != null && towers.Length > 0 && towers[0] != null)
            towers[0].anchoredPosition = new Vector2(StageColumnX, FirstDoorY);
        if (towers != null && towers.Length > 1 && towers[1] != null)
            towers[1].anchoredPosition = new Vector2(StageColumnX, SecondDoorY);
        escapeStops = new[]
        {
            new Vector2(StageColumnX, FirstDoorY),
            new Vector2(StageColumnX, LastStopY)
        };
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
        if (state == GameCoreManager.GameState.PuzzleLoop && celebrationStarted)
            ResetRoute();
    }

    public void ResetRoute()
    {
        StopAllCoroutines();
        stepRoutine = null;
        shakeRoutine = null;
        TowersDestroyed = 0;
        RouteCleared = false;
        celebrationStarted = false;
        currentTowerHealth = maxTowerHealth;
        ApplyHealthBar();

        if (towers != null)
        {
            for (int i = 0; i < towers.Length; i++)
            {
                if (towers[i] != null) towers[i].gameObject.SetActive(true);
            }
        }

        if (stage != null && characterHomeStored)
            stage.Snap(characterHome);
        else if (characterTransform != null && characterHomeStored)
            characterTransform.anchoredPosition = characterHome;
        if (StageModelDressing.Instance != null) StageModelDressing.Instance.Restore();

        for (int i = 0; i < missiles.Length; i++)
        {
            missileBusy[i] = false;
            if (missiles[i] != null) missiles[i].gameObject.SetActive(false);
        }

        if (damageRect != null) damageRect.gameObject.SetActive(false);

        if (cameraHomeStored && Camera.main != null)
            Camera.main.transform.position = cameraHome;
    }

    public void AdvanceEscapeRun()
    {
        if (runLeg < maxRunLeg) runLeg++;
        float steps = runLeg - 1;
        maxTowerHealth = baseTowerHealth + healthStep * steps;
        float pace = 1f + paceStep * steps;

        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) sort.SetRunPace(pace);
        ShowLeg();
        Debug.Log("Escape run leg " + runLeg + ". Tower health " + maxTowerHealth.ToString("0") + ". Chute pace " + pace.ToString("0.00") + ".");
    }

    public void NotifyGateCleared()
    {
        if (RouteCleared || celebrationStarted) return;
        if (CasinoDressing.Instance != null) CasinoDressing.Instance.Celebrate(true, false);
        NudgeCharacter();
        Debug.Log("Gate clear steps the thief toward the next stop.");
    }

    void NudgeCharacter()
    {
        if (stage == null || escapeStops == null || escapeStops.Length == 0) return;
        int index = Mathf.Clamp(TowersDestroyed, 0, escapeStops.Length - 1);
        stage.NudgeToward(escapeStops[index], ChipStep, stepDuration);
    }

    void ShowLeg()
    {
        if (runLabel != null) runLabel.text = "LEG " + runLeg;
    }

    void Update()
    {
        TickMotes(sparks, false);
        TickMotes(coins, true);
    }

    public void LaunchMissileAtBarrier(float damageAmount, Color missileColor, bool arc = false, bool percentOfRemaining = false, RectTransform origin = null, bool stepOnImpact = false)
    {
        if (RouteCleared || celebrationStarted) return;
        StartCoroutine(MissileImpactRoutine(damageAmount, missileColor, arc, percentOfRemaining, origin, stepOnImpact));
    }

    IEnumerator MissileImpactRoutine(float damage, Color missileColor, bool arc, bool percentOfRemaining, RectTransform origin, bool stepOnImpact)
    {
        int slot = RentMissile();
        Vector2 start = origin != null ? LocalPoint(origin, MuzzleFor(missileColor)) : MuzzleFor(missileColor);
        LastShotStart = start;
        Vector2 end = arc ? HealthBarPosition() : ActiveTowerPosition();
        Vector2 control = origin != null
            ? new Vector2((start.x + end.x) * 0.5f + (end.x - start.x) * 0.15f, Mathf.Max(start.y, end.y) + 180f)
            : (start + end) * 0.5f + new Vector2((end.x - start.x) * 0.2f, 220f);
        float elapsed = 0f;
        float flight = arc ? 0.45f : 0.2f;
        bool trailStarted = false;

        if (slot >= 0)
        {
            RectTransform shot = missiles[slot];
            Image image = shot.GetComponent<Image>();
            if (image != null) image.color = missileColor;
            shot.anchoredPosition = start;
            shot.localRotation = Quaternion.identity;
            shot.gameObject.SetActive(true);

            while (elapsed < flight)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flight);
                Vector2 next = arc ? Quad(start, control, end, t) : Vector2.Lerp(start, end, t);
                Vector2 delta = next - shot.anchoredPosition;
                shot.anchoredPosition = next;
                if (arc && delta.sqrMagnitude > 0.25f)
                {
                    float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    shot.localEulerAngles = new Vector3(0f, 0f, angle - 90f);
                }

                if (arc && MissileFx.Instance != null)
                {
                    Vector3 world = WorldFollow.UiToWorld(shot);
                    if (!trailStarted) MissileFx.Instance.BeginArc(world);
                    else MissileFx.Instance.MoveArc(world);
                    trailStarted = true;
                }

                yield return null;
            }

            shot.localRotation = Quaternion.identity;
            shot.gameObject.SetActive(false);
            missileBusy[slot] = false;
        }
        else
        {
            yield return new WaitForSeconds(flight);
        }

        if (MissileFx.Instance != null) MissileFx.Instance.EndArc();
        if (RouteCleared) yield break;
        float applied = percentOfRemaining ? currentTowerHealth * Mathf.Clamp01(damage) : damage;
        ApplyHit(applied, missileColor, end, percentOfRemaining ? 0.15f : 0.12f, stepOnImpact);
    }

    static Vector2 Quad(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }

    void ApplyHit(float damage, Color missileColor, Vector2 impact, float shakeSeconds, bool stepOnImpact)
    {
        currentTowerHealth -= damage;
        if (currentTowerHealth < 0f) currentTowerHealth = 0f;
        ApplyHealthBar();
        PunchCurrentTower();
        int sparkCount = stepOnImpact ? SparkPool : 6;
        float sparkSpeed = stepOnImpact ? 980f : 520f;
        float sparkLife = stepOnImpact ? 0.42f : 0.32f;
        LastSparkCount = Burst(sparks, impact, missileColor, sparkCount, sparkSpeed, sparkLife, false, Vector2.zero);
        if (MissileFx.Instance != null) MissileFx.Instance.Explode(impact, missileLayer);
        ShakeCamera(shakeSeconds);
        ShowDamage(impact, damage);

        Debug.Log("Barrier hit for " + damage + ". Health " + currentTowerHealth + ". Tower " + (TowersDestroyed + 1) + ".");

        if (currentTowerHealth > 0f)
        {
            if (stepOnImpact)
            {
                NudgeCharacter();
                Debug.Log("Missile chips the barrier. Thief edges forward.");
            }

            return;
        }

        if (TowersDestroyed < towers.Length && towers[TowersDestroyed] != null)
        {
            if (StageModelDressing.Instance != null)
                StageModelDressing.Instance.Fracture(towers[TowersDestroyed]);
            towers[TowersDestroyed].gameObject.SetActive(false);
        }

        TowersDestroyed++;
        bool finalTower = TowersDestroyed >= barriersOnRoute || TowersDestroyed >= escapeStops.Length;
        if (!finalTower)
        {
            currentTowerHealth = maxTowerHealth;
            ApplyHealthBar();
            Debug.Log("Security barrier down. Thief advances.");
        }

        MoveThief(finalTower);
    }

    void MoveThief(bool finalTower)
    {
        if (characterTransform == null || escapeStops == null || escapeStops.Length == 0) 
        {
            if (finalTower) BeginCelebration();
            return;
        }

        int stopIndex = Mathf.Clamp(TowersDestroyed - 1, 0, escapeStops.Length - 1);
        Vector2 target = escapeStops[stopIndex];
        if (stage != null)
        {
            bool celebrate = finalTower;
            stage.PlayTo(target, stepDuration, () =>
            {
                if (celebrate) BeginCelebration();
            });
            return;
        }

        if (stepRoutine != null) StopCoroutine(stepRoutine);
        stepRoutine = StartCoroutine(AnimateCharacterStep(target, finalTower));
    }

    IEnumerator AnimateCharacterStep(Vector2 targetPosition, bool celebrate)
    {
        float elapsed = 0f;
        Vector2 startPos = characterTransform.anchoredPosition;
        float duration = Mathf.Max(0.05f, stepDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float elasticity = 5f;
            float easeOutBack = Mathf.Pow(t - 1f, 3f) * ((elasticity + 1f) * (t - 1f) + elasticity) + 1f;
            characterTransform.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPosition, easeOutBack);
            yield return null;
        }

        characterTransform.anchoredPosition = targetPosition;
        stepRoutine = null;
        if (celebrate) BeginCelebration();
    }

    void BeginCelebration()
    {
        if (celebrationStarted) return;
        celebrationStarted = true;
        RouteCleared = true;
        Debug.Log("Escape clear. Opening Plinko.");

        Vector2 origin = characterTransform != null ? characterTransform.anchoredPosition : Vector2.zero;
        Color coinColor = coinUsesHeroArt ? Color.white : new Color(0.96f, 0.76f, 0.22f, 1f);
        Burst(coins, origin, coinColor, CoinPool, 280f, 0.9f, true, new Vector2(0f, 750f));

        JuicyUIManager ui = FindFirstObjectByType<JuicyUIManager>();
        if (ui != null) ui.OnClaimReward();
        else if (winTicker != null) winTicker.AddWin(2500d);

        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null) sort.BeginBonusRound();
    }

    public void PreviewHeroCoins()
    {
        Vector2 origin = characterTransform != null ? characterTransform.anchoredPosition : Vector2.zero;
        Color coinColor = coinUsesHeroArt ? Color.white : new Color(0.96f, 0.76f, 0.22f, 1f);
        Burst(coins, origin, coinColor, CoinPool, 160f, 5f, true, new Vector2(0f, 420f));
    }

    void ShowDamage(Vector2 at, float damage)
    {
        if (missileLayer == null || runLabel == null || runLabel.font == null) return;

        if (damageText == null)
        {
            var go = new GameObject("DamagePop", typeof(RectTransform));
            go.transform.SetParent(missileLayer, false);
            damageRect = go.GetComponent<RectTransform>();
            damageRect.sizeDelta = new Vector2(180f, 56f);
            damageText = go.AddComponent<TextMeshProUGUI>();
            damageText.font = runLabel.font;
            damageText.fontSize = 36f;
            damageText.alignment = TextAlignmentOptions.Center;
            damageText.raycastTarget = false;
        }

        damageText.text = "-" + Mathf.RoundToInt(damage);
        damageText.color = Color.white;
        damageRect.anchoredPosition = at;
        damageRect.gameObject.SetActive(true);
        if (damageRoutine != null) StopCoroutine(damageRoutine);
        damageRoutine = StartCoroutine(FloatDamage(at));
    }

    IEnumerator FloatDamage(Vector2 origin)
    {
        float elapsed = 0f;
        const float duration = 0.55f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (damageRect != null) damageRect.anchoredPosition = origin + new Vector2(0f, 70f * t);
            if (damageText != null)
            {
                Color color = damageText.color;
                color.a = 1f - t;
                damageText.color = color;
            }
            yield return null;
        }

        if (damageRect != null) damageRect.gameObject.SetActive(false);
        damageRoutine = null;
    }

    void ApplyHealthBar()
    {
        if (towerHealthFill == null) return;
        float amount = maxTowerHealth <= 0f ? 0f : Mathf.Clamp01(currentTowerHealth / maxTowerHealth);
        towerHealthFill.fillAmount = amount;
        towerHealthFill.color = Color.Lerp(lowHealthColor, fullHealthColor, amount);
    }

    void PunchCurrentTower()
    {
        if (towerPops == null || TowersDestroyed < 0 || TowersDestroyed >= towerPops.Length) return;
        JuicyUIPop pop = towerPops[TowersDestroyed];
        if (pop != null) pop.TriggerPunch(1.3f);
    }

    Vector2 HealthBarPosition()
    {
        if (towerHealthFill == null) return ActiveTowerPosition();
        return LocalPoint(towerHealthFill.rectTransform, ActiveTowerPosition());
    }

    Vector2 LocalPoint(RectTransform source, Vector2 fallback)
    {
        if (source == null || missileLayer == null) return fallback;
        Vector3 world = source.TransformPoint(source.rect.center);
        Camera cam = null;
        Canvas canvas = missileLayer.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(missileLayer, screen, cam, out Vector2 local))
            return fallback;
        return local;
    }

    Vector2 ActiveTowerPosition()
    {
        int index = Mathf.Clamp(TowersDestroyed, 0, towers == null ? 0 : towers.Length - 1);
        if (towers == null || towers.Length == 0 || towers[index] == null) return Vector2.up * 540f;
        return towers[index].anchoredPosition;
    }

    Vector2 MuzzleFor(Color missileColor)
    {
        RectTransform muzzle = missileColor.b > missileColor.r ? rightMuzzle : leftMuzzle;
        return muzzle != null ? muzzle.anchoredPosition : Vector2.zero;
    }

    void CacheCameraHome()
    {
        if (cameraHomeStored || Camera.main == null) return;
        cameraHome = Camera.main.transform.position;
        cameraHomeStored = true;
    }

    public void Shake(float seconds)
    {
        ShakeCamera(seconds);
    }

    void ShakeCamera(float seconds)
    {
        CacheCameraHome();
        LastShakeDuration = Mathf.Max(0.05f, seconds);
        if (!cameraHomeStored || Camera.main == null) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(LastShakeDuration));
    }

    IEnumerator ShakeRoutine(float seconds)
    {
        float elapsed = 0f;
        Transform cam = Camera.main.transform;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            cam.position = cameraHome + (Vector3)(Random.insideUnitCircle * 0.07f);
            yield return null;
        }

        cam.position = cameraHome;
        shakeRoutine = null;
    }

    void BuildShots()
    {
        Sprite coinSprite = Resources.Load<Sprite>("UIHeroes/coins_hero");
        coinUsesHeroArt = coinSprite != null;
        if (!coinUsesHeroArt) coinSprite = shotSprite;

        if (shotSprite != null)
        {
            for (int i = 0; i < MissilePool; i++)
                missiles[i] = CreateImage("Missile", missileLayer, 28f, shotSprite);

            for (int i = 0; i < SparkPool; i++)
            {
                RectTransform rect = CreateImage("Spark", missileLayer, 14f, shotSprite);
                sparks[i].Rect = rect;
                sparks[i].Image = rect != null ? rect.GetComponent<Image>() : null;
            }
        }

        if (coinSprite == null) return;

        float coinSize = coinUsesHeroArt ? 64f : 22f;
        for (int i = 0; i < CoinPool; i++)
        {
            RectTransform rect = CreateImage("Coin", coinLayer, coinSize, coinSprite);
            coins[i].Rect = rect;
            coins[i].Image = rect != null ? rect.GetComponent<Image>() : null;
            if (coins[i].Image != null && coinUsesHeroArt)
                coins[i].Image.preserveAspect = true;
        }
    }

    static RectTransform CreateImage(string name, RectTransform parent, float size, Sprite sprite)
    {
        if (parent == null) return null;
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        go.SetActive(false);
        return rect;
    }

    int RentMissile()
    {
        for (int i = 0; i < missiles.Length; i++)
        {
            if (missiles[i] == null || missileBusy[i]) continue;
            missileBusy[i] = true;
            return i;
        }

        return -1;
    }

    int Burst(Mote[] pool, Vector2 origin, Color color, int count, float speed, float life, bool homing, Vector2 target)
    {
        int spawned = 0;
        for (int i = 0; i < pool.Length && spawned < count; i++)
        {
            if (pool[i].Active || pool[i].Rect == null) continue;
            pool[i].Active = true;
            pool[i].Life = life;
            pool[i].MaxLife = life;
            pool[i].Homing = homing;
            pool[i].Target = target;
            pool[i].Velocity = (Vector2)Random.insideUnitCircle * speed;
            if (homing) pool[i].Velocity += Vector2.up * 180f;
            pool[i].Rect.anchoredPosition = origin;
            pool[i].Rect.gameObject.SetActive(true);
            if (pool[i].Image != null) pool[i].Image.color = color;
            spawned++;
        }

        return spawned;
    }

    void TickMotes(Mote[] pool, bool homing)
    {
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].Active || pool[i].Rect == null) continue;

            pool[i].Life -= Time.deltaTime;
            if (homing && pool[i].Homing)
            {
                Vector2 toTarget = pool[i].Target - pool[i].Rect.anchoredPosition;
                pool[i].Velocity = Vector2.Lerp(pool[i].Velocity, toTarget * 4f, Time.deltaTime * 3f);
            }
            else
            {
                pool[i].Velocity += Vector2.down * 700f * Time.deltaTime;
            }

            pool[i].Rect.anchoredPosition += pool[i].Velocity * Time.deltaTime;
            if (pool[i].Image != null)
            {
                Color color = pool[i].Image.color;
                color.a = Mathf.Clamp01(pool[i].Life / Mathf.Max(0.01f, pool[i].MaxLife));
                pool[i].Image.color = color;
            }

            if (pool[i].Life > 0f) continue;
            pool[i].Active = false;
            pool[i].Rect.gameObject.SetActive(false);
        }
    }
}
