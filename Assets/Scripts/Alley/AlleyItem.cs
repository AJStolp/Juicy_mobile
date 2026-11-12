using TMPro;
using UnityEngine;

// One pooled falling item. It holds a model for every kind and shows the one
// it was launched as. The director moves it while it falls; once flicked, the
// item plays its own flight, trash or pop up motion.
public class AlleyItem : MonoBehaviour
{
    public enum Phase { Idle, Falling, Flying, Trashing, Popping }

    [SerializeField] GameObject gem;
    [SerializeField] GameObject bomb;
    [SerializeField] GameObject gold;
    [SerializeField] GameObject letterTile;
    [SerializeField] TextMeshPro letterText;
    [SerializeField] GemColorController gemColor;

    public Phase State { get; private set; }
    public ItemKind Kind { get; private set; }
    public int ColorId { get; private set; }
    public char Letter { get; private set; }
    public int Side { get; private set; }
    public SortResult Result { get; private set; }

    Vector3 from;
    Vector3 to;
    float t;
    float duration;
    float arc;
    float spin;

    public void Wire(GameObject gemModel, GameObject bombModel, GameObject goldModel, GameObject letterModel, TextMeshPro text, GemColorController tint)
    {
        gem = gemModel;
        bomb = bombModel;
        gold = goldModel;
        letterTile = letterModel;
        letterText = text;
        gemColor = tint;
    }

    public void Launch(ItemKind kind, int colorId, Color color, char letter, Vector3 position)
    {
        Kind = kind;
        ColorId = colorId;
        Letter = letter;
        gem.SetActive(kind == ItemKind.Gem);
        bomb.SetActive(kind == ItemKind.Bomb);
        gold.SetActive(kind == ItemKind.Gold);
        letterTile.SetActive(kind == ItemKind.Letter);
        if (kind == ItemKind.Gem) gemColor.SetGemColor(color);
        if (kind == ItemKind.Letter) letterText.text = letter.ToString();

        // Letters and bombs keep their face to the camera; loot spins.
        spin = kind == ItemKind.Gem || kind == ItemKind.Gold ? 140f : 0f;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, kind == ItemKind.Gem ? Random.Range(0f, 360f) : 0f, 0f));
        transform.localScale = Vector3.one;
        State = Phase.Falling;
        gameObject.SetActive(true);
    }

    public void Fall(float drop, float dt)
    {
        Vector3 position = transform.position;
        position.y -= drop;
        transform.position = position;
        if (spin > 0f) transform.Rotate(0f, spin * dt, 0f, Space.World);
    }

    public void BeginFlight(int side, Vector3 target, float seconds)
    {
        Side = side;
        Begin(Phase.Flying, target, seconds, 0.55f);
    }

    public void BeginTrash(float seconds)
    {
        Begin(Phase.Trashing, transform.position + new Vector3(0f, 3.2f, 3.5f), seconds, 0.4f);
    }

    // Rises out of the chest mouth, then pops away.
    public void BeginPop(Vector3 mouth, SortResult result)
    {
        Result = result;
        transform.position = mouth;
        Begin(Phase.Popping, mouth + Vector3.up * 0.7f, 0.55f, 0f);
    }

    public void Park()
    {
        State = Phase.Idle;
        gameObject.SetActive(false);
    }

    void Begin(Phase phase, Vector3 target, float seconds, float height)
    {
        State = phase;
        from = transform.position;
        to = target;
        duration = Mathf.Max(0.01f, seconds);
        arc = height;
        t = 0f;
    }

    // Returns true when the current flight, trash or pop has finished.
    public bool Advance(float dt)
    {
        t = Mathf.Min(1f, t + dt / duration);
        switch (State)
        {
            case Phase.Flying:
            {
                float e = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(from, to, e) + Vector3.up * (arc * 4f * t * (1f - t));
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, e);
                transform.Rotate(0f, 540f * dt, 0f, Space.World);
                break;
            }
            case Phase.Trashing:
                transform.position = Vector3.Lerp(from, to, t * t) + Vector3.up * (arc * 4f * t * (1f - t));
                transform.localScale = Vector3.one * (1f - t);
                transform.Rotate(220f * dt, 0f, 360f * dt, Space.World);
                break;
            case Phase.Popping:
            {
                float rise = 1f - (1f - t) * (1f - t);
                transform.position = Vector3.Lerp(from, to, rise);
                float grow = t < 0.6f ? Mathf.Lerp(0.6f, 1.35f, t / 0.6f) : Mathf.Lerp(1.35f, 0f, (t - 0.6f) / 0.4f);
                transform.localScale = Vector3.one * grow;
                transform.Rotate(0f, 900f * dt, 0f, Space.World);
                break;
            }
        }
        return t >= 1f;
    }
}
