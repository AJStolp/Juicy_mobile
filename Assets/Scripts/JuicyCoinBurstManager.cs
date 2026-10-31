using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class JuicyCoinBurstManager : MonoBehaviour
{
    public static JuicyCoinBurstManager Instance { get; private set; }

    struct ScreenCoin
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Velocity;
        public float Spin;
        public float Life;
        public float Duration;
    }

    readonly List<ScreenCoin> screenCoins = new List<ScreenCoin>(28);
    [System.Serializable]
    public struct CoinSpawnConfig
    {
        public Vector3 spawnPosition;
        public Vector3 targetUIPosition;
        public int coinCount;
        public float spreadRadius;
        public float explosionForce;
    }

    [Header("Pool Configuration")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int initialPoolSize = 100;
    [SerializeField] private Transform coinContainer;

    [Header("Juice Dynamics")]
    [SerializeField] private float flightDuration = 1.2f;
    [SerializeField] private float coinScale = 0.22f;

    private Queue<CoinInstance> availableCoins = new Queue<CoinInstance>();
    private List<CoinInstance> activeCoins = new List<CoinInstance>(100);

    private class CoinInstance
    {
        public GameObject gameObject;
        public Transform transform;
        public Vector3 startPos;
        public Vector3 peakPos;
        public Vector3 targetPos;
        public float elapsedTime;
        public float totalDuration;
        public bool isActive;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("A second coin burst manager was ignored.");
            return;
        }

        Instance = this;
        InitializePool();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayPayoutBurst(RectTransform parent, Sprite coin, Transform keepOnTop)
    {
        if (parent == null || coin == null) return;
        Sprite gem = Resources.Load<Sprite>("UIHeroes/gem_ruby");
        int count = 26;
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
            float speed = Random.Range(680f, 1280f);
            Sprite sprite = (i % 7 == 0 && gem != null) ? gem : coin;
            Image image = MakeScreenCoin(parent, sprite, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 48f, 84f);
            if (keepOnTop != null)
                image.transform.SetSiblingIndex(keepOnTop.GetSiblingIndex());
        if (keepOnTop != null) keepOnTop.SetAsLastSibling();
            screenCoins.Add(new ScreenCoin
            {
                Rect = image.rectTransform,
                Image = image,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                Spin = Random.Range(-280f, 280f),
                Life = 0f,
                Duration = Random.Range(0.55f, 0.9f)
            });
        }
    }

    static Image MakeScreenCoin(RectTransform parent, Sprite sprite, Vector2 anchored, float size)
    {
        GameObject go = new GameObject("BurstCoin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = new Vector2(size, size);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    public void BurstFrom(Vector3 worldPosition)
    {
        TriggerCoinBurst(new CoinSpawnConfig
        {
            spawnPosition = worldPosition,
            targetUIPosition = worldPosition + new Vector3(0f, 6f, 0f),
            coinCount = 12,
            spreadRadius = 0.5f,
            explosionForce = 1.4f
        });
    }

    private void InitializePool()
    {
        if (coinPrefab == null) return;
        if (coinContainer == null) coinContainer = transform;

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(coinPrefab, coinContainer);
            obj.SetActive(false);
            availableCoins.Enqueue(new CoinInstance { gameObject = obj, transform = obj.transform, isActive = false });
        }
    }

    public void TriggerCoinBurst(CoinSpawnConfig config)
    {
        if (coinPrefab == null) return;

        for (int i = 0; i < config.coinCount; i++)
        {
            CoinInstance coin = GetPooledCoin();
            if (coin == null) break;

            Vector3 randomSpread = Random.insideUnitSphere * config.spreadRadius;
            randomSpread.z = 0f;
            
            coin.startPos = config.spawnPosition;
            coin.peakPos = config.spawnPosition + randomSpread + (Vector3.up * config.explosionForce);
            coin.targetPos = config.targetUIPosition;
            coin.elapsedTime = 0f;
            coin.totalDuration = flightDuration + Random.Range(-0.2f, 0.2f);
            coin.isActive = true;
            
            coin.gameObject.SetActive(true);
            activeCoins.Add(coin);
        }
    }

    private CoinInstance GetPooledCoin()
    {
        if (availableCoins.Count > 0) return availableCoins.Dequeue();
        if (coinPrefab == null) return null;

        GameObject obj = Instantiate(coinPrefab, coinContainer);
        return new CoinInstance { gameObject = obj, transform = obj.transform, isActive = false };
    }

    private void Update()
    {
        for (int i = screenCoins.Count - 1; i >= 0; i--)
        {
            ScreenCoin coin = screenCoins[i];
            coin.Life += Time.deltaTime;
            float life = Mathf.Clamp01(coin.Life / coin.Duration);
            if (life >= 1f || coin.Rect == null)
            {
                if (coin.Rect != null) Destroy(coin.Rect.gameObject);
                screenCoins.RemoveAt(i);
                continue;
            }

            coin.Velocity *= 1f - 1.8f * Time.deltaTime;
            coin.Rect.anchoredPosition += coin.Velocity * Time.deltaTime;
            coin.Rect.Rotate(0f, 0f, coin.Spin * Time.deltaTime);
            float fade = 1f - life * life;
            Color color = coin.Image.color;
            color.a = fade;
            coin.Image.color = color;
            float scale = Mathf.Lerp(1.15f, 0.35f, life);
            coin.Rect.localScale = new Vector3(scale, scale, 1f);
            screenCoins[i] = coin;
        }

        for (int i = activeCoins.Count - 1; i >= 0; i--)
        {
            CoinInstance coin = activeCoins[i];
            coin.elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(coin.elapsedTime / coin.totalDuration);

            Vector3 m1 = Vector3.Lerp(coin.startPos, coin.peakPos, t);
            Vector3 m2 = Vector3.Lerp(coin.peakPos, coin.targetPos, t);
            Vector3 currentPos = Vector3.Lerp(m1, m2, Mathf.SmoothStep(0f, 1f, t));

            float scaleJuice = 1f + Mathf.Sin(t * Mathf.PI * 3f) * 0.25f * (1f - t);
            coin.transform.localScale = Vector3.one * coinScale * scaleJuice;
            coin.transform.position = currentPos;
            coin.transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.Self);

            if (t >= 1f)
            {
                coin.gameObject.SetActive(false);
                coin.isActive = false;
                availableCoins.Enqueue(coin);
                activeCoins.RemoveAt(i);
            }
        }
    }
}