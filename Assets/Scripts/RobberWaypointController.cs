using UnityEngine;

// Canvas pixel destinations. The sample world speed of 4 is not used:
// the robber rect moves in overlay pixels, where a calm crossing is about 320.
// Runs after RobberBehaviorController so a leap can freeze this mover first.
[DefaultExecutionOrder(30)]
public class RobberWaypointController : MonoBehaviour
{
    public enum Node
    {
        Car = 0,
        LeftBin = 1,
        RightBin = 2,
        Ledge = 3,
        Chute = 4
    }

    const float Arrive = 8f;

    // Car is the right fender, clear of the launch button.
    // Bin spots are the inner corner of each chest, on screen.
    static readonly Vector2[] Spots =
    {
        new Vector2(310f, -570f),
        new Vector2(-250f, -20f),
        new Vector2(250f, -20f),
        new Vector2(-280f, 200f),
        new Vector2(0f, 210f)
    };

    static readonly string[] Names = { "Car", "LeftBin", "RightBin", "Ledge", "Chute" };

    RectTransform body;
    Transform targetNode;
    readonly RectTransform[] nodes = new RectTransform[5];
    Node node = Node.Car;
    Vector2 place;
    float speed = 320f;
    bool suspended;
    bool placed;
    string lastLogged = "";

    public Node Current => node;
    public bool Arrived { get; private set; }
    public Vector2 Place => place;

    public void Bind(RectTransform rect)
    {
        body = rect;
        if (body == null) return;
        for (int i = 0; i < nodes.Length; i++)
        {
            var go = new GameObject("node" + Names[i]);
            var nodeRect = go.AddComponent<RectTransform>();
            nodeRect.SetParent(body.parent, false);
            nodeRect.anchorMin = nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
            nodeRect.pivot = new Vector2(0.5f, 0.5f);
            nodeRect.anchoredPosition = Spots[i];
            nodeRect.sizeDelta = Vector2.zero;
            nodes[i] = nodeRect;
        }

        targetNode = nodes[0];
        place = Spots[0];
        placed = true;
        body.anchoredPosition = place;
    }

    public void MoveToNode(int nodeType)
    {
        int index = Mathf.Clamp(nodeType, 0, Spots.Length - 1);
        Node next = (Node)index;
        if (next != node)
        {
            node = next;
            Arrived = false;
        }

        targetNode = nodes[index];
        if (lastLogged == Names[index]) return;
        lastLogged = Names[index];
        Debug.Log("Robber node " + Names[index]);
    }

    public void SetSpeed(float pixelsPerSecond)
    {
        speed = Mathf.Max(0f, pixelsPerSecond);
    }

    public void Suspend(bool hold)
    {
        suspended = hold;
    }

    void OnDestroy()
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] != null) Destroy(nodes[i].gameObject);
        }
    }

    void Update()
    {
        if (suspended || body == null || targetNode == null) return;
        if (!placed)
        {
            place = body.anchoredPosition;
            placed = true;
        }

        var spot = targetNode as RectTransform;
        Vector2 target = spot != null ? spot.anchoredPosition : Spots[(int)node];
        Vector2 next = Vector2.MoveTowards(place, target, speed * Time.deltaTime);
        Face(next.x - place.x);
        place = next;
        Arrived = Vector2.Distance(place, target) <= Arrive;
        if (Arrived) Face(FaceWhenHeld());
        body.anchoredPosition = place + Pose(Time.time);
    }

    Vector2 Pose(float time)
    {
        if (!Arrived) return Vector2.zero;
        switch (node)
        {
            case Node.Car:
                return new Vector2(0f, Mathf.Sin(time * 2.1f) * 20f - 8f);
            case Node.LeftBin:
            case Node.RightBin:
                return new Vector2(Mathf.Sin(time * 9f) * 14f, Mathf.Abs(Mathf.Sin(time * 6f)) * 20f);
            case Node.Ledge:
                return new Vector2(Mathf.Sin(time * 3.2f) * 18f, 0f);
            default:
                return new Vector2(Mathf.Sin(time * 2.4f) * 12f, Mathf.Sin(time * 2f) * 10f);
        }
    }

    float FaceWhenHeld()
    {
        switch (node)
        {
            case Node.LeftBin: return -1f;
            case Node.RightBin: return 1f;
            case Node.Ledge: return 1f;
            case Node.Chute: return 1f;
            default: return place.x > 0f ? -1f : 1f;
        }
    }

    void Face(float dx)
    {
        if (body == null || Mathf.Abs(dx) < 0.5f) return;
        float y = Mathf.Abs(body.localScale.y);
        if (y < 0.05f) y = 1f;
        float x = dx < 0f ? -y : y;
        body.localScale = new Vector3(x, y, 1f);
    }
}
