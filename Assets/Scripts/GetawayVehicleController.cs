using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Owns the level clear peel out: one squeal, one camera shake, tire smoke,
// then the drive off the right side.
public class GetawayVehicleController : MonoBehaviour
{
    public static GetawayVehicleController Instance { get; private set; }

    static readonly Vector2 FrontWheel = new Vector2(-95f, -28f);
    static readonly Vector2 RearWheel = new Vector2(74f, -28f);

    const int PuffCount = 12;

    RectTransform car;
    Image carImage;
    Vector2 home;
    bool burning;
    Sprite puffSprite;
    readonly Puff[] puffs = new Puff[PuffCount];

    class Puff
    {
        public RectTransform rect;
        public Image image;
        public float age;
        public float life;
        public Vector2 origin;
        public Vector2 vel;
        public bool live;
    }

    public static void Burnout(RectTransform car, Image carImage, Vector2 home)
    {
        Ensure();
        if (Instance != null) Instance.Begin(car, carImage, home);
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("GetawayVehicle").AddComponent<GetawayVehicleController>();
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

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Begin(RectTransform target, Image image, Vector2 parked)
    {
        if (burning || target == null) return;
        car = target;
        carImage = image;
        home = parked;
        burning = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        JuicyAudioHooks.Getaway();
        if (HeistCombativeManager.Instance != null)
            HeistCombativeManager.Instance.Shake(0.48f);
        RobberBehaviorController.Extract();
        Debug.Log("Getaway burnout.");

        float elapsed = 0f;
        const float shakeFor = 0.48f;
        float nextPuff = 0f;
        while (elapsed < shakeFor)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            car.anchoredPosition = home + Random.insideUnitCircle * 10f;
            if (elapsed >= nextPuff)
            {
                Spawn(FrontWheel);
                Spawn(RearWheel);
                nextPuff = elapsed + 0.06f;
            }

            StepPuffs(dt);
            yield return null;
        }

        RobberBehaviorController.Board();
        Vector2 from = home;
        Vector2 to = home + new Vector2(980f, 30f);
        elapsed = 0f;
        nextPuff = 0f;
        const float driveFor = 0.55f;
        while (elapsed < driveFor)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / driveFor);
            car.anchoredPosition = Vector2.Lerp(from, to, t * t);
            if (elapsed < 0.18f && elapsed >= nextPuff)
            {
                Spawn(RearWheel);
                nextPuff = elapsed + 0.05f;
            }

            StepPuffs(dt);
            yield return null;
        }

        if (carImage != null) carImage.enabled = false;
        while (AnyPuff())
        {
            StepPuffs(Time.deltaTime);
            yield return null;
        }

        burning = false;
    }

    void Spawn(Vector2 wheel)
    {
        if (car == null || car.parent == null) return;
        Puff puff = NextPuff();
        if (puff.rect == null)
        {
            GameObject go = new GameObject("TireSmoke");
            puff.rect = go.AddComponent<RectTransform>();
            puff.rect.SetParent(car.parent, false);
            puff.rect.anchorMin = new Vector2(0.5f, 0.5f);
            puff.rect.anchorMax = new Vector2(0.5f, 0.5f);
            puff.rect.pivot = new Vector2(0.5f, 0.5f);
            puff.image = go.AddComponent<Image>();
            puff.image.sprite = SmokeSprite();
            puff.image.raycastTarget = false;
        }

        puff.live = true;
        puff.age = 0f;
        puff.life = Random.Range(0.45f, 0.7f);
        puff.origin = car.anchoredPosition + wheel + new Vector2(0f, -36f);
        puff.vel = new Vector2(Random.Range(-70f, -10f), Random.Range(10f, 46f));
        puff.rect.anchoredPosition = puff.origin;
        puff.rect.sizeDelta = new Vector2(70f, 70f);
        int ahead = Mathf.Min(car.GetSiblingIndex() + 1, car.parent.childCount - 1);
        puff.rect.SetSiblingIndex(ahead);
        puff.image.color = new Color(0.86f, 0.86f, 0.88f, 0.9f);
        puff.image.enabled = true;
    }

    Puff NextPuff()
    {
        for (int i = 0; i < puffs.Length; i++)
        {
            if (puffs[i] == null) puffs[i] = new Puff();
            if (!puffs[i].live) return puffs[i];
        }

        return puffs[0];
    }

    void StepPuffs(float dt)
    {
        if (dt < 0f) dt = 0f;
        for (int i = 0; i < puffs.Length; i++)
        {
            Puff puff = puffs[i];
            if (puff == null || !puff.live || puff.rect == null) continue;
            puff.age += dt;
            float t = Mathf.Clamp01(puff.age / puff.life);
            puff.rect.anchoredPosition = puff.origin + puff.vel * puff.age;
            float size = Mathf.Lerp(70f, 150f, t);
            puff.rect.sizeDelta = new Vector2(size, size);
            Color color = puff.image.color;
            color.a = Mathf.Lerp(0.9f, 0f, t);
            puff.image.color = color;
            if (t < 1f) continue;
            puff.live = false;
            puff.image.enabled = false;
        }
    }

    bool AnyPuff()
    {
        for (int i = 0; i < puffs.Length; i++)
        {
            if (puffs[i] != null && puffs[i].live) return true;
        }

        return false;
    }

    Sprite SmokeSprite()
    {
        if (puffSprite != null) return puffSprite;
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        puffSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return puffSprite;
    }
}
