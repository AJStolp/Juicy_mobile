using UnityEngine;
using UnityEngine.UI;

public class MissilePowerUpController : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image fill;
    [SerializeField] private JuicyUIPop pop;
    [SerializeField] private int redCost = 5;
    [SerializeField] private int blueCost = 5;
    [SerializeField] private float remainingShare = 0.25f;
    [SerializeField] private Color shotColor = new Color(0.98f, 0.55f, 0.12f, 1f);

    public float FillAmount => fill == null ? 0f : fill.fillAmount;
    public int FreeCharges { get; private set; }

    int dressPasses;
    bool loggedFrame;

    public void GrantFreeCharge()
    {
        FreeCharges++;
    }

    void Awake()
    {
        EnsureRefs();
        if (fill != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
        }

        if (button != null) button.onClick.AddListener(Launch);
        DressFrame();
    }

    void LateUpdate()
    {
        if (dressPasses >= 3) return;
        dressPasses++;
        DressFrame();
    }

    void DressFrame()
    {
        Image plate = GetComponent<Image>();
        if (plate == null) return;
        Sprite frame = Resources.Load<Sprite>("UIHeroes/missile_frame");
        if (frame == null)
        {
            if (!loggedFrame)
            {
                loggedFrame = true;
                Debug.LogWarning("Missile frame sprite missing at Resources/UIHeroes/missile_frame.");
            }
            return;
        }

        plate.sprite = frame;
        plate.type = Image.Type.Sliced;
        plate.color = Color.white;
        plate.pixelsPerUnitMultiplier = 5f;
        if (button != null) button.transition = Selectable.Transition.None;
        RectTransform rect = plate.rectTransform;
        if (rect.sizeDelta.y < 130f)
            rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 420f), 140f);
        if (!loggedFrame)
        {
            loggedFrame = true;
            Vector4 border = frame.border;
            Debug.Log("Missile frame sliced. border " + border.x.ToString("0") + " " + border.y.ToString("0") + " " + border.z.ToString("0") + " " + border.w.ToString("0"));
        }

        if (fill == null) return;
        RectTransform fillRect = fill.rectTransform;
        fillRect.offsetMin = new Vector2(48f, 36f);
        fillRect.offsetMax = new Vector2(-52f, -36f);
    }

    void EnsureRefs()
    {
        if (button == null) button = GetComponent<Button>();
        if (pop == null) pop = GetComponent<JuicyUIPop>();
        if (fill != null) return;
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject.name == "Fill")
            {
                fill = images[i];
                return;
            }
        }
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Launch);
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        EnsureRefs();
        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        int red = sort != null ? sort.CraftRed : 0;
        int blue = sort != null ? sort.CraftBlue : 0;
        float redReady = redCost <= 0 ? 1f : Mathf.Clamp01((float)red / redCost);
        float blueReady = blueCost <= 0 ? 1f : Mathf.Clamp01((float)blue / blueCost);
        bool free = FreeCharges > 0;
        if (fill != null) fill.fillAmount = free ? 1f : (redReady + blueReady) * 0.5f;

        bool inChute = GameCoreManager.Instance == null
            || GameCoreManager.Instance.CurrentState == GameCoreManager.GameState.PuzzleLoop;
        bool routeOpen = HeistCombativeManager.Instance != null && !HeistCombativeManager.Instance.RouteCleared;
        bool panic = sort != null && sort.PanicHeld;
        bool armed = free || (red >= redCost && blue >= blueCost);
        if (button != null) button.interactable = armed && inChute && routeOpen && !panic;
    }

    public bool ContainsScreenPoint(Vector2 screen)
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null) return false;
        Camera cam = null;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screen, cam);
    }

    public void Launch()
    {
        if (HeistCombativeManager.Instance == null || HeistCombativeManager.Instance.RouteCleared) return;
        if (GameCoreManager.Instance != null && GameCoreManager.Instance.CurrentState != GameCoreManager.GameState.PuzzleLoop) return;

        ActionSortController sort = FindFirstObjectByType<ActionSortController>();
        if (sort != null && sort.PanicHeld) return;

        bool free = FreeCharges > 0;
        if (free) FreeCharges--;
        else if (sort == null || !sort.TrySpendCraft(redCost, blueCost)) return;

        if (pop != null) pop.TriggerPunch(1.18f);
        HeistCombativeManager.Instance.LaunchMissileAtBarrier(remainingShare, shotColor, true, true, transform as RectTransform, true);
        if (free) Debug.Log("Launch missile used a free charge.");
        else Debug.Log("Launch missile spent " + redCost + " thermite and " + blueCost + " coolant.");
    }
}
