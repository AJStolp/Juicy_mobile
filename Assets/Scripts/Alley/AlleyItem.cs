using TMPro;
using UnityEngine;

// One pooled falling item. It holds a model for every kind and shows the one
// it was launched as. The director moves it while it falls; once flicked, the
// item plays its own flight, trash or pop up motion. An item nobody swipes
// lands on the street in front of the chests: a gem shatters there in a flash
// of its color, anything else thuds down and shrinks away.
public class AlleyItem : MonoBehaviour
{
    public enum Phase { Idle, Falling, Flying, Trashing, Popping, Landed }

    const int ShardCount = 9;
    const float ShardGravity = 7f;
    const float FlashPeak = 5f;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

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
    public Vector3 Origin { get; private set; }
    public Color ItemColor { get; private set; }

    Vector3 from;
    Vector3 to;
    float t;
    float duration;
    float arc;
    float spin;

    Transform[] shards;
    Renderer[] shardRenderers;
    Vector3[] shardVelocity;
    Vector3[] shardSpin;
    Vector3[] shardScale;
    MaterialPropertyBlock shardBlock;
    Light flash;
    float streetY;

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
        ItemColor = color;
        Origin = position;
        ShowShards(false);
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

    // Slides x and z from the chute toward spot as fraction goes 0 to 1.
    // Height is left to Fall.
    public void Approach(Vector3 spot, float fraction)
    {
        Vector3 position = transform.position;
        position.x = Mathf.Lerp(Origin.x, spot.x, fraction);
        position.z = Mathf.Lerp(Origin.z, spot.z, fraction);
        transform.position = position;
    }

    // Comes to rest with its bottom on the street. A gem breaks into shards.
    public void BeginLanding(float street, float seconds)
    {
        streetY = street;
        Bounds bounds;
        if (ModelBounds(out bounds)) transform.position += Vector3.up * (street - bounds.min.y);
        Begin(Phase.Landed, transform.position, seconds, 0f);
        if (Kind == ItemKind.Gem) Shatter();
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
        ShowShards(false);
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
            case Phase.Landed:
                if (Kind == ItemKind.Gem) AdvanceShards(dt);
                else Thud();
                break;
        }
        return t >= 1f;
    }

    // Squashes on impact, settles, then shrinks away where it landed.
    void Thud()
    {
        float squash = t < 0.2f ? Mathf.Sin(t / 0.2f * Mathf.PI) * 0.3f : 0f;
        float fade = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
        transform.localScale = new Vector3(1f + squash, 1f - squash, 1f + squash) * fade;
    }

    void Shatter()
    {
        Bounds bounds;
        if (!ModelBounds(out bounds)) bounds = new Bounds(transform.position, Vector3.one * 0.2f);
        EnsureShards();
        gem.SetActive(false);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        shardBlock.SetColor(BaseColorId, ItemColor);
        for (int i = 0; i < ShardCount; i++)
        {
            float angle = (i + Random.value * 0.6f) / ShardCount * Mathf.PI * 2f;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle) * 0.6f - 0.4f);
            shardVelocity[i] = outward * Random.Range(0.8f, 1.6f) + Vector3.up * Random.Range(1.2f, 2.2f);
            shardSpin[i] = Random.insideUnitSphere * 720f;
            shards[i].position = bounds.center + Random.insideUnitSphere * bounds.extents.y * 0.4f;
            shards[i].rotation = Random.rotation;
            shards[i].localScale = shardScale[i];
        }
        flash.color = ItemColor;
        flash.transform.position = bounds.center + Vector3.up * 0.15f;
        ShowShards(true);
        AdvanceShards(0f);
    }

    void AdvanceShards(float dt)
    {
        float shrink = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        for (int i = 0; i < ShardCount; i++)
        {
            shardVelocity[i] += Vector3.down * (ShardGravity * dt);
            Vector3 position = shards[i].position + shardVelocity[i] * dt;
            float floor = streetY + 0.02f;
            if (position.y < floor)
            {
                position.y = floor;
                shardVelocity[i] = new Vector3(shardVelocity[i].x * 0.5f, -shardVelocity[i].y * 0.3f, shardVelocity[i].z * 0.5f);
                shardSpin[i] *= 0.5f;
            }
            shards[i].position = position;
            shards[i].Rotate(shardSpin[i] * dt, Space.World);
            shards[i].localScale = shardScale[i] * shrink;
        }
        // The color flash: bright on impact, gone within a third of the landing.
        float glow = Mathf.Clamp01(1f - t / 0.35f);
        flash.intensity = FlashPeak * glow * glow;
        shardBlock.SetColor(EmissionId, ItemColor * (0.6f + 2.5f * glow));
        for (int i = 0; i < ShardCount; i++) shardRenderers[i].SetPropertyBlock(shardBlock);
    }

    // Shards are small copies of the gem's own mesh, made once per pooled item.
    void EnsureShards()
    {
        if (shards != null) return;
        MeshFilter source = gem.GetComponentInChildren<MeshFilter>(true);
        Renderer sourceRenderer = gem.GetComponentInChildren<Renderer>(true);
        shards = new Transform[ShardCount];
        shardRenderers = new Renderer[ShardCount];
        shardVelocity = new Vector3[ShardCount];
        shardSpin = new Vector3[ShardCount];
        shardScale = new Vector3[ShardCount];
        shardBlock = new MaterialPropertyBlock();
        Vector3 size = source != null ? source.transform.lossyScale : Vector3.one * 0.1f;
        for (int i = 0; i < ShardCount; i++)
        {
            GameObject shard = new GameObject("Shard_" + i, typeof(MeshFilter), typeof(MeshRenderer));
            shard.transform.SetParent(transform, false);
            if (source != null) shard.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            MeshRenderer renderer = shard.GetComponent<MeshRenderer>();
            if (sourceRenderer != null) renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shards[i] = shard.transform;
            shardRenderers[i] = renderer;
            shardScale[i] = Vector3.Scale(size, new Vector3(Random.Range(0.2f, 0.4f), Random.Range(0.25f, 0.5f), Random.Range(0.2f, 0.4f)));
        }
        GameObject flashObject = new GameObject("Shatter_Flash");
        flashObject.transform.SetParent(transform, false);
        flash = flashObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.range = 1.6f;
        flash.shadows = LightShadows.None;
        flash.renderMode = LightRenderMode.ForcePixel;
        flash.intensity = 0f;
    }

    void ShowShards(bool show)
    {
        if (shards == null) return;
        for (int i = 0; i < ShardCount; i++) shards[i].gameObject.SetActive(show);
        flash.enabled = show;
        if (!show) flash.intensity = 0f;
    }

    // World bounds of whichever model this item is showing.
    bool ModelBounds(out Bounds bounds)
    {
        bounds = default;
        GameObject model = Kind == ItemKind.Gem ? gem : Kind == ItemKind.Bomb ? bomb : Kind == ItemKind.Gold ? gold : letterTile;
        bool found = false;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        return found;
    }
}
