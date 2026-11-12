using UnityEngine;
using UnityEngine.UI;

// Center playfield robber. The narrative thief on the right column stays put.
// Nodes replace the old square patrol. The leap to the car still owns the rect.
public class RobberBehaviorController : MonoBehaviour
{
    public static RobberBehaviorController Instance { get; private set; }

    const float CalmSpeed = 320f;
    const float PanicSpeed = 760f;
    const float LeapSpeed = 1700f;
    const float PanicHeat = 50f;
    const float ShimmySpeed = 80f;
    const float SortVisit = 2.2f;
    const float MilestoneVisit = 1.8f;

    static readonly Vector2 Window = new Vector2(-55f, -482f);

    RectTransform rect;
    Image image;
    RobberAnimatorController rig;
    Robber3DAnimatorController rig3d;
    RobberWaypointController nodes;
    Transform car;
    Vector2 place;
    float visit;
    float leapStart = 1f;
    bool leaping;
    bool boarded;
    bool rigged;
    string poseLabel = "";

    public static void Ensure()
    {
        if (Instance != null) return;
        new GameObject("Robber").AddComponent<RobberBehaviorController>();
    }

    public static void NotifySort(int side)
    {
        if (Instance != null) Instance.OnSort(side);
    }

    public static void NotifyMilestone()
    {
        if (Instance != null) Instance.OnMilestone();
    }

    public static void Extract()
    {
        if (Instance != null) Instance.Leap();
    }

    public static void Board()
    {
        if (Instance != null) Instance.Hide();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Canvas canvas = Overlay();
        if (canvas == null) return;

        rect = gameObject.AddComponent<RectTransform>();
        transform.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(190f, 250f);

        image = gameObject.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UIHeroes/heist_robber");
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        if (image.sprite == null)
        {
            image.enabled = false;
            Debug.LogWarning("Robber sprite missing.");
        }

        rig3d = gameObject.AddComponent<Robber3DAnimatorController>();
        rigged = rig3d.Build(rect);
        if (!rigged)
        {
            Destroy(rig3d);
            rig3d = null;
            rig = gameObject.AddComponent<RobberAnimatorController>();
            rigged = rig.Build(rect);
        }

        if (rigged) image.enabled = false;

        nodes = gameObject.AddComponent<RobberWaypointController>();
        nodes.Bind(rect);
        nodes.MoveToNode((int)RobberWaypointController.Node.Car);
        place = rect.anchoredPosition;

        if (transform.parent.childCount > 1)
            transform.SetSiblingIndex(1);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (rect == null || boarded) return;
        if (!rigged && (image == null || !image.enabled)) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        bool panic = HeatManager.Instance != null && HeatManager.Instance.CurrentHeat > PanicHeat;
        float shown = HeatManager.Instance != null ? HeatManager.Instance.ShownHeat / HeatManager.MaxHeat : 0f;
        if (rig3d != null) rig3d.SetHeat(shown);
        else if (rig != null) rig.SetHeat(shown);

        if (leaping)
        {
            if (nodes != null) nodes.Suspend(true);
            StepLeap(dt);
            return;
        }

        if (nodes != null) nodes.Suspend(false);
        if (visit > 0f) visit -= dt;
        if (visit <= 0f && nodes != null)
        {
            int home = panic
                ? (int)RobberWaypointController.Node.Ledge
                : (int)RobberWaypointController.Node.Car;
            if ((int)nodes.Current != home) nodes.MoveToNode(home);
        }

        float travel = panic ? PanicSpeed : CalmSpeed;
        if (nodes != null) nodes.SetSpeed(travel);
        ApplyClip(panic, travel);
    }

    void OnSort(int side)
    {
        if (rect == null || leaping || boarded || nodes == null) return;
        int node = side == 0
            ? (int)RobberWaypointController.Node.LeftBin
            : (int)RobberWaypointController.Node.RightBin;
        nodes.MoveToNode(node);
        visit = SortVisit;
    }

    void OnMilestone()
    {
        if (rect == null || leaping || boarded || nodes == null) return;
        nodes.MoveToNode((int)RobberWaypointController.Node.Chute);
        visit = MilestoneVisit;
    }

    void Leap()
    {
        if (rect == null || boarded || leaping) return;
        leaping = true;
        if (nodes != null) nodes.Suspend(true);
        place = rect.anchoredPosition;
        leapStart = Vector2.Distance(place, Window);
        if (leapStart < 1f) leapStart = 1f;
        Tuck(false);
        transform.SetAsLastSibling();
        if (rig3d != null) rig3d.TriggerLeap();
        else if (rig != null) rig.TriggerLeap();
        Debug.Log("Robber leaping for the car.");
    }

    void StepLeap(float dt)
    {
        Vector2 next = Vector2.MoveTowards(place, Window, LeapSpeed * dt);
        Face(next.x - place.x);
        place = next;
        float dist = Vector2.Distance(place, Window);
        float along = 1f - Mathf.Clamp01(dist / leapStart);
        float scale = Mathf.Lerp(0.38f, 1f, Mathf.Clamp01(dist / 480f));
        float face = rect.localScale.x < 0f ? -1f : 1f;
        rect.localScale = new Vector3(face * scale, scale, 1f);
        rect.anchoredPosition = place + new Vector2(0f, Mathf.Sin(along * Mathf.PI) * 160f);
        NotePose("leap");
        SetLocomotion(LeapSpeed, false);
        if (dist > 10f) return;
        Hide();
        Debug.Log("Robber in the car.");
    }

    void Hide()
    {
        boarded = true;
        if (nodes != null) nodes.Suspend(true);
        if (image != null) image.enabled = false;
        if (rig3d != null) rig3d.Hide();
        else if (rig != null) rig.Hide();
    }

    void ApplyClip(bool panic, float travel)
    {
        bool arrived = nodes != null && nodes.Arrived;
        RobberWaypointController.Node node = nodes != null
            ? nodes.Current
            : RobberWaypointController.Node.Car;

        if (!arrived)
        {
            NotePose("run");
            SetLocomotion(travel, false);
            Tuck(false);
            return;
        }

        switch (node)
        {
            case RobberWaypointController.Node.LeftBin:
            case RobberWaypointController.Node.RightBin:
                NotePose("shimmy");
                SetLocomotion(ShimmySpeed, false);
                Tuck(false);
                break;
            case RobberWaypointController.Node.Ledge:
                NotePose("peek");
                SetLocomotion(0f, true);
                Tuck(false);
                break;
            case RobberWaypointController.Node.Chute:
                if (panic)
                {
                    NotePose("peek");
                    SetLocomotion(0f, true);
                }
                else
                {
                    NotePose("idle");
                    SetLocomotion(0f, false);
                }
                Tuck(false);
                break;
            default:
                NotePose("hide");
                SetLocomotion(0f, false);
                Tuck(false);
                break;
        }
    }

    void NotePose(string label)
    {
        if (label == poseLabel) return;
        poseLabel = label;
        Debug.Log("Robber pose " + label);
    }

    void SetLocomotion(float speed, bool panic)
    {
        if (rig3d != null) rig3d.SetLocomotion(speed, panic);
        else if (rig != null) rig.SetLocomotion(speed, panic);
    }

    void Face(float dx)
    {
        if (Mathf.Abs(dx) < 3f) return;
        float y = rect.localScale.y;
        float x = dx < 0f ? -Mathf.Abs(y) : Mathf.Abs(y);
        rect.localScale = new Vector3(x, y, 1f);
    }

    // Keep the robber in front of the getaway. The car image covers a body
    // placed on the roof, and the launch button covers the center.
    void Tuck(bool behind)
    {
        if (rect == null || rect.parent == null) return;
        if (car == null)
        {
            GameObject found = GameObject.Find("ExtractionCar");
            if (found != null) car = found.transform;
        }

        if (car == null || car.parent != rect.parent) return;
        int carIndex = car.GetSiblingIndex();
        int mine = rect.GetSiblingIndex();
        if (behind)
        {
            if (mine > carIndex) rect.SetSiblingIndex(carIndex);
        }
        else if (mine < carIndex)
        {
            rect.SetSiblingIndex(carIndex);
        }
    }

    static Canvas Overlay()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas fallback = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (canvas.name == "Canvas") return canvas;
            if (fallback == null) fallback = canvas;
        }

        return fallback;
    }
}
