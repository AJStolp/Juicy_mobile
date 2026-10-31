using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PlinkoMiniGameManager : MonoBehaviour
{
    [Header("Plinko Configuration")]
    [SerializeField] private GameObject tokenPrefab;
    [SerializeField] private Transform dropSpawnPoint;
    [SerializeField] private List<Transform> slotPockets;
    [SerializeField] private int maxPegRows = 6;
    [SerializeField] private int ballsPerRound = 3;
    [SerializeField] private float aimIdleSeconds = 4f;

    [Header("Juice & Dynamics")]
    [SerializeField] private float dropGravity = -30f;
    [SerializeField] private float bounceDamping = 0.65f;

    readonly Queue<Transform> tokenPool = new Queue<Transform>();
    Transform[] pegs = System.Array.Empty<Transform>();
    Vector3[] pegBaseScales = System.Array.Empty<Vector3>();
    bool[] pegBusy = System.Array.Empty<bool>();

    bool isDropping;
    bool aiming;
    bool roundActive;
    bool aimHeld;
    float idle;
    int ballsLeft;
    int pegPunches;
    double pendingWin;

    public int SlotCount => slotPockets == null ? 0 : slotPockets.Count;
    public int BallsLanded { get; private set; }
    public bool Aiming => aiming;
    public int PegPunches => pegPunches;
    public double PendingWin => pendingWin;

    void Awake()
    {
        if (tokenPrefab == null) return;

        for (int i = 0; i < 10; i++)
        {
            GameObject obj = Instantiate(tokenPrefab, transform);
            obj.SetActive(false);
            tokenPool.Enqueue(obj.transform);
        }
    }

    void OnDisable()
    {
        RestorePegs();
    }

    void Update()
    {
        if (!roundActive || isDropping || !aiming) return;
        TickAim();
    }

    public void BeginRound()
    {
        CachePegs();
        ballsLeft = Mathf.Max(1, ballsPerRound);
        BallsLanded = 0;
        pendingWin = 0d;
        pegPunches = 0;
        aiming = true;
        aimHeld = false;
        idle = 0f;
        roundActive = true;
        isDropping = false;
        if (maxPegRows < 1) maxPegRows = 1;
        CenterHopper();
    }

    public void DropAimed()
    {
        if (!roundActive || !aiming || isDropping || ballsLeft <= 0) return;
        if (dropSpawnPoint == null || slotPockets == null || slotPockets.Count == 0) return;

        int slot = NearestSlot(dropSpawnPoint.position.x);
        aiming = false;
        aimHeld = false;
        ballsLeft--;
        StartCoroutine(TokenDropRoutine(slot));
    }

    void TickAim()
    {
        idle += Time.deltaTime;
        Pointer pointer = Pointer.current;
        if (pointer != null)
        {
            if (pointer.press.wasPressedThisFrame)
            {
                aimHeld = true;
                idle = 0f;
            }

            if (aimHeld && pointer.press.isPressed)
            {
                float dx = pointer.delta.ReadValue().x;
                if (Mathf.Abs(dx) > 0.2f)
                {
                    MoveHopper(dx);
                    idle = 0f;
                }
            }

            if (aimHeld && pointer.press.wasReleasedThisFrame)
            {
                aimHeld = false;
                DropAimed();
                return;
            }
        }

        if (idle >= aimIdleSeconds) DropAimed();
    }

    void CenterHopper()
    {
        if (dropSpawnPoint == null || slotPockets == null || slotPockets.Count == 0) return;
        Vector3 position = dropSpawnPoint.position;
        position.x = slotPockets[slotPockets.Count / 2].position.x;
        dropSpawnPoint.position = position;
    }

    void MoveHopper(float pixelsX)
    {
        if (dropSpawnPoint == null || slotPockets == null || slotPockets.Count == 0) return;

        float worldPerPixel = 0.01f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            float dist = Mathf.Abs(dropSpawnPoint.position.z - cam.transform.position.z);
            float worldHeight = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            worldPerPixel = worldHeight / Mathf.Max(1, Screen.height);
        }

        float minX = slotPockets[0].position.x;
        float maxX = slotPockets[0].position.x;
        for (int i = 1; i < slotPockets.Count; i++)
        {
            float x = slotPockets[i].position.x;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
        }

        Vector3 position = dropSpawnPoint.position;
        position.x = Mathf.Clamp(position.x + pixelsX * worldPerPixel, minX, maxX);
        dropSpawnPoint.position = position;
    }

    int NearestSlot(float worldX)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < slotPockets.Count; i++)
        {
            if (slotPockets[i] == null) continue;
            float distance = Mathf.Abs(slotPockets[i].position.x - worldX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    IEnumerator TokenDropRoutine(int targetSlotIndex)
    {
        isDropping = true;
        Transform token = GetPooledToken();
        int slotIndex = Mathf.Clamp(targetSlotIndex, 0, slotPockets.Count - 1);
        Vector3 currentPos = dropSpawnPoint.position;
        Vector3 targetPos = slotPockets[slotIndex].position;
        token.position = currentPos;
        token.gameObject.SetActive(true);

        float wobble = Mathf.Clamp(Mathf.Abs(dropGravity) * 0.008f, 0.05f, 0.35f) * Mathf.Clamp(bounceDamping, 0.2f, 1f);
        Vector3 peak = currentPos + new Vector3(Random.Range(-wobble, wobble), -0.4f, 0f);
        Vector3 mid = Vector3.Lerp(currentPos, targetPos, 0.55f) + new Vector3(Random.Range(-wobble, wobble), 0.2f, 0f);

        float elapsedTime = 0f;
        const float totalDuration = 1.5f;
        while (elapsedTime < totalDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / totalDuration);
            Vector3 m1 = Vector3.Lerp(currentPos, peak, t);
            Vector3 m2 = Vector3.Lerp(peak, mid, t);
            Vector3 m3 = Vector3.Lerp(mid, targetPos, t);
            Vector3 b1 = Vector3.Lerp(m1, m2, t);
            Vector3 b2 = Vector3.Lerp(m2, m3, t);
            token.position = Vector3.Lerp(b1, b2, Mathf.SmoothStep(0f, 1f, t));
            token.Rotate(Vector3.forward, 360f * Time.deltaTime, Space.Self);
            KissPegs(token.position);
            yield return null;
        }

        token.position = targetPos;
        double payout = ReadPayout(slotIndex);
        pendingWin += payout;
        BallsLanded++;
        Debug.Log("Plinko ball " + BallsLanded + " landed in slot " + slotIndex + " for " + payout.ToString("0") + ". Running " + pendingWin.ToString("0") + ".");

        token.gameObject.SetActive(false);
        tokenPool.Enqueue(token);
        isDropping = false;

        if (ballsLeft > 0)
        {
            aiming = true;
            idle = 0f;
            yield break;
        }

        FinishRound(targetPos);
    }

    void FinishRound(Vector3 burstFrom)
    {
        roundActive = false;
        aiming = false;
        Debug.Log("Plinko paid " + pendingWin.ToString("0") + " from " + BallsLanded + " balls.");

        if (JuicyWinTicker.Instance != null)
            JuicyWinTicker.Instance.AddWin(pendingWin);

        JuicyCoinBurstManager burst = FindFirstObjectByType<JuicyCoinBurstManager>();
        if (burst != null) burst.BurstFrom(burstFrom);

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.BeginWaveReturn();
        else
        {
            if (HeistCombativeManager.Instance != null)
                HeistCombativeManager.Instance.AdvanceEscapeRun();
            if (GameCoreManager.Instance != null)
                GameCoreManager.Instance.ChangeState(GameCoreManager.GameState.PuzzleLoop);
        }
    }

    double ReadPayout(int slotIndex)
    {
        double payout = 1000d;
        PlinkoSlot slot = slotPockets[slotIndex].GetComponent<PlinkoSlot>();
        if (slot != null) payout = slot.Payout;
        return payout;
    }

    Transform GetPooledToken()
    {
        if (tokenPool.Count > 0) return tokenPool.Dequeue();
        GameObject obj = Instantiate(tokenPrefab, transform);
        return obj.transform;
    }

    void CachePegs()
    {
        var found = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("Peg_")) found.Add(child);
        }

        pegs = found.ToArray();
        pegBaseScales = new Vector3[pegs.Length];
        pegBusy = new bool[pegs.Length];
        for (int i = 0; i < pegs.Length; i++)
            pegBaseScales[i] = pegs[i].localScale;
    }

    void KissPegs(Vector3 position)
    {
        float reach = 0.42f;
        for (int i = 0; i < pegs.Length; i++)
        {
            if (pegBusy[i] || pegs[i] == null) continue;
            Vector3 peg = pegs[i].position;
            float dx = position.x - peg.x;
            float dy = position.y - peg.y;
            if (dx * dx + dy * dy > reach * reach) continue;
            StartCoroutine(PunchPeg(i));
        }
    }

    IEnumerator PunchPeg(int index)
    {
        if (index < 0 || index >= pegs.Length || pegBusy[index] || pegs[index] == null) yield break;
        pegBusy[index] = true;
        pegPunches++;
        if (GameCoreManager.Instance != null) GameCoreManager.Instance.PlayImpact();
        Transform peg = pegs[index];
        Vector3 baseScale = pegBaseScales[index];
        float elapsed = 0f;
        const float duration = 0.16f;
        while (elapsed < duration)
        {
            if (peg == null) yield break;
            elapsed += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            peg.localScale = baseScale * (1f + 0.45f * k);
            yield return null;
        }

        if (peg != null) peg.localScale = baseScale;
        if (index < pegBusy.Length) pegBusy[index] = false;
    }

    void RestorePegs()
    {
        for (int i = 0; i < pegs.Length; i++)
        {
            if (pegs[i] == null) continue;
            pegs[i].localScale = pegBaseScales[i];
            pegBusy[i] = false;
        }
    }
}
