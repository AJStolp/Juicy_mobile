using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The Plinko bonus round. Finishing a leg pauses the sort, swings the board out
// of the alley wall beside the vault and moves the camera onto it. The player
// drags along the top rail to aim each coin and lets go to drop it. Coins are
// real rigid bodies bouncing off real pins, held to the board's face; where
// they settle is paid out by PlinkoPayout. Nothing about a drop is scripted.
public class AlleyPlinko : MonoBehaviour
{
    // Board layout in board units, mirrored from build_plinko_board.py.
    const float SlotWidth = 0.12f;
    const float DropHeight = 1.43f;   // aim rail, above the floor's center
    const float SlotTop = 0.29f;      // top of the slot dividers
    // Coins drop from the middle of the rail only, so nobody can line one up
    // over the straight lanes beside the rails. Mirrors AIM_HALF in
    // build_plinko_board.py.
    const float AimHalfWidth = 0.3f;

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] AlleyVaultDoor vaultDoor;
    [SerializeField] AlleyCameraRig cameraRig;
    [SerializeField] Transform hinge;
    [SerializeField] float openYaw = 62f;
    [SerializeField] Transform board;
    [SerializeField] Transform floor;
    [SerializeField] Transform railLeft;
    [SerializeField] Transform railRight;
    [SerializeField] Rigidbody coinTemplate;
    [SerializeField] Transform viewPoint;
    [SerializeField] Light boardLight;
    [SerializeField] float boardLightIntensity = 2.5f;
    [SerializeField] float gravity = 3.2f;
    [SerializeField] float swingTime = 0.9f;
    [SerializeField] float viewFov = 58f;
    [Tooltip("Bleeds sideways speed, like a coin dragging on the backboard, so it hops a peg or so per row instead of ricocheting to a rail.")]
    [SerializeField] float sideDrag = 10f;
    [Tooltip("Random sideways kick on each pin strike, either way with equal odds. Without it a coin repeats the same bounce row after row and walks to a rail.")]
    [SerializeField] float pinScatter = 0.2f;
    [Tooltip("Top speed on the board, in board units per second. A slow coin hops one peg at a time; a fast one skips rows and rides the rails.")]
    [SerializeField] float maxSpeed = 0.9f;

    readonly List<Rigidbody> coins = new List<Rigidbody>();
    Quaternion hingeClosed;
    Rigidbody active;
    bool aiming;
    bool released;
    float aimX;
    float botDelay;
    Camera view;

    public bool Running { get; private set; }
    public int Rounds { get; private set; }
    public int CoinsDropped { get; private set; }
    public int VaultHits { get; private set; }
    public int Jostles { get; private set; }
    public int[] SlotHits { get; } = new int[PlinkoPayout.SlotCount];

    float Scale => board.lossyScale.x;
    Vector3 Right => (railRight.position - railLeft.position).normalized;
    Vector3 Normal => Vector3.Cross(Right, Vector3.up).normalized;

    public void Configure(AlleyChuteDirector chute, AlleyVaultDoor door, AlleyCameraRig rig, Transform swing, Transform boardRoot,
        Transform boardFloor, Transform left, Transform right, Rigidbody coin, Transform cameraSpot, Light light)
    {
        director = chute;
        vaultDoor = door;
        cameraRig = rig;
        hinge = swing;
        board = boardRoot;
        floor = boardFloor;
        railLeft = left;
        railRight = right;
        coinTemplate = coin;
        viewPoint = cameraSpot;
        boardLight = light;
    }

    void Awake()
    {
        hingeClosed = hinge.localRotation;
        view = Camera.main;
        coinTemplate.gameObject.SetActive(false);
        if (boardLight != null) boardLight.intensity = 0f;
    }

    void Start()
    {
        director.Rules.LegAdvanced += OnLegAdvanced;
    }

    void OnDestroy()
    {
        if (director != null && director.Rules != null) director.Rules.LegAdvanced -= OnLegAdvanced;
    }

    bool simulating;
    public string Settings => "aim " + AimHalfWidth + " sideDrag " + sideDrag + " pinScatter " + pinScatter + " maxSpeed " + maxSpeed + " gravity " + gravity + " scale " + Scale.ToString("0.00");
    public int PinHits { get; private set; }

    Transform lastPin;

    // One kick per pin: rolling around a pin fires several contacts.
    public void OnPinHit(Rigidbody coin, Transform pin)
    {
        if (coin != active || pin == lastPin) return;
        lastPin = pin;
        PinHits++;
        coin.linearVelocity += Right * (Random.Range(-pinScatter, pinScatter) * Scale);
    }

    // Drops many coins in one round and clears each after it pays, to measure
    // where coins land (AlleyBatch.PlinkoSim).
    public void Simulate(int count)
    {
        if (Running) return;
        simulating = true;
        StartCoroutine(Round(count));
    }

    void OnLegAdvanced(int leg, int chutes)
    {
        if (!Running) StartCoroutine(Round(director.Rules.LastLegCoins));
    }

    IEnumerator Round(int count)
    {
        Running = true;
        Rounds++;
        director.Paused = true;
        // Let a vault crack play out before the camera leaves the door.
        while (vaultDoor != null && vaultDoor.Cracking) yield return null;
        // The view was aimed at the open board when the scene was built.
        cameraRig.Hold(viewPoint.position, viewPoint.position + viewPoint.forward, viewFov);
        yield return Swing(0f, 1f);

        for (int i = 0; i < count; i++)
        {
            Rigidbody coin = Spawn();
            aiming = true;
            released = false;
            aimX = 0f;
            botDelay = Random.Range(0.4f, 1.2f);
            while (!released)
            {
                PlaceOnRail(coin, aimX);
                yield return null;
            }
            aiming = false;
            Drop(coin);
            yield return Settle(coin);
        }

        yield return new WaitForSeconds(1f);
        for (int i = 0; i < coins.Count; i++) coins[i].gameObject.SetActive(false);
        yield return Swing(1f, 0f);
        cameraRig.Release();
        while (!cameraRig.Settled) yield return null;
        director.Paused = false;
        Running = false;
        simulating = false;
    }

    IEnumerator Swing(float from, float to)
    {
        for (float t = 0f; t < swingTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(from, to, t / swingTime);
            SetOpen(k);
            yield return null;
        }
        SetOpen(to);
    }

    void SetOpen(float k)
    {
        hinge.localRotation = hingeClosed * Quaternion.Euler(0f, openYaw * k, 0f);
        if (boardLight != null) boardLight.intensity = boardLightIntensity * k;
    }

    Rigidbody Spawn()
    {
        Rigidbody coin = null;
        for (int i = 0; i < coins.Count; i++)
        {
            if (!coins[i].gameObject.activeSelf) { coin = coins[i]; break; }
        }
        if (coin == null)
        {
            coin = Instantiate(coinTemplate, transform);
            coin.name = "Coin_" + coins.Count;
            coins.Add(coin);
        }
        coin.gameObject.SetActive(true);
        coin.isKinematic = true;
        return coin;
    }

    void PlaceOnRail(Rigidbody coin, float x)
    {
        Vector3 at = floor.position + (Right * x + Vector3.up * DropHeight) * Scale;
        coin.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-Normal, Vector3.up));
    }

    void Drop(Rigidbody coin)
    {
        CoinsDropped++;
        lastPin = null;
        coin.isKinematic = false;
        // A breath of sideways drift so two drops from one spot can differ.
        coin.linearVelocity = Right * Random.Range(-0.03f, 0.03f);
        coin.angularVelocity = Vector3.zero;
        active = coin;
    }

    IEnumerator Settle(Rigidbody coin)
    {
        float still = 0f;
        float stalled = 0f;
        for (float age = 0f; age < 12f; age += Time.deltaTime)
        {
            float height = Vector3.Dot(coin.position - floor.position, Vector3.up) / Scale;
            bool slow = coin.linearVelocity.magnitude < 0.06f;
            still = height < SlotTop && slow ? still + Time.deltaTime : 0f;
            if (still > 0.25f) break;
            // A coin balanced on a pin gets a jostle, like tapping the machine.
            stalled = height >= SlotTop && slow ? stalled + Time.deltaTime : 0f;
            if (stalled > 0.4f)
            {
                stalled = 0f;
                Jostles++;
                coin.AddForce(Right * (Random.value < 0.5f ? -0.25f : 0.25f) * Scale + Vector3.up * 0.1f * Scale, ForceMode.VelocityChange);
            }
            yield return null;
        }
        active = null;
        coin.isKinematic = true;
        int slot = SlotOf(coin.position);
        Pay(slot);
        if (simulating) coin.gameObject.SetActive(false);
    }

    int SlotOf(Vector3 position)
    {
        float x = Vector3.Dot(position - floor.position, Right) / Scale;
        return Mathf.Clamp(Mathf.FloorToInt(x / SlotWidth + PlinkoPayout.SlotCount * 0.5f), 0, PlinkoPayout.SlotCount - 1);
    }

    void Pay(int slot)
    {
        SlotHits[slot]++;
        PlinkoReward reward = PlinkoPayout.For(slot);
        switch (reward.Prize)
        {
            case PlinkoPrize.Cash:
                director.Rules.Award(reward.Cash);
                JuicyAudioHooks.Chime();
                break;
            case PlinkoPrize.Gear:
                director.Rules.AddGear(reward.Gear, 1);
                JuicyAudioHooks.Chime();
                break;
            case PlinkoPrize.Vault:
                VaultHits++;
                director.Vault.Turn(reward.Turns);
                break;
        }
        director.SaveNow();
    }

    void Update()
    {
        if (!aiming) return;
        if (director.AutoPlay)
        {
            botDelay -= Time.deltaTime;
            aimX = Mathf.Sin(Time.time * 1.7f) * AimHalfWidth;
            if (botDelay <= 0f) released = true;
            return;
        }
        Pointer pointer = Pointer.current;
        if (pointer == null) return;
        if (pointer.press.isPressed || pointer.press.wasReleasedThisFrame)
        {
            Ray ray = view.ScreenPointToRay(pointer.position.ReadValue());
            Plane face = new Plane(Normal, floor.position);
            if (face.Raycast(ray, out float distance))
            {
                float x = Vector3.Dot(ray.GetPoint(distance) - floor.position, Right) / Scale;
                aimX = Mathf.Clamp(x, -AimHalfWidth, AimHalfWidth);
            }
        }
        if (pointer.press.wasReleasedThisFrame) released = true;
    }

    // Gravity runs down the board's face and the coin is held to the plane
    // through the pins, so it can only bounce left, right, up and down.
    void FixedUpdate()
    {
        if (active == null) return;
        Vector3 normal = Normal;
        active.AddForce(Vector3.down * (gravity * Scale), ForceMode.Acceleration);
        float off = Vector3.Dot(active.position - floor.position, normal);
        active.position -= normal * off;
        active.linearVelocity -= normal * Vector3.Dot(active.linearVelocity, normal);
        Vector3 right = Right;
        active.linearVelocity -= right * (Vector3.Dot(active.linearVelocity, right) * Mathf.Clamp01(sideDrag * Time.fixedDeltaTime));
        active.linearVelocity = Vector3.ClampMagnitude(active.linearVelocity, maxSpeed * Scale);
        active.angularVelocity = normal * active.angularVelocity.magnitude * Mathf.Sign(Vector3.Dot(active.angularVelocity, normal));
    }
}
