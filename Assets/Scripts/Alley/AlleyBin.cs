using UnityEngine;

// A color chest at the bottom of the alley. It glows in its color, brighter as
// its vat fills, punches when something lands, and flashes hot on a mistake.
public class AlleyBin : MonoBehaviour
{
    [SerializeField] int side;
    [SerializeField] Color color = Color.red;
    [SerializeField] Light glow;
    [SerializeField] Transform mouth;
    [SerializeField] Transform marker;
    [SerializeField] float restGlow = 1.2f;
    [SerializeField] float vatGlow = 3f;

    static readonly Color MistakeFlash = new Color(1f, 0.55f, 0.1f);

    HeistSortRules rules;
    Vector3 baseScale;
    float punch;
    float mistake;

    public int Side => side;
    public Color BinColor => color;
    public Vector3 Mouth => mouth != null ? mouth.position : transform.position;

    public void Configure(int binSide, Color binColor, Light binGlow, Transform binMouth, Transform binMarker)
    {
        side = binSide;
        color = binColor;
        glow = binGlow;
        mouth = binMouth;
        marker = binMarker;
    }

    public void Bind(HeistSortRules sortRules)
    {
        rules = sortRules;
    }

    void Awake()
    {
        baseScale = transform.localScale;
    }

    public void Receive(SortResult result)
    {
        bool bad = result == SortResult.Wrong || result == SortResult.Bomb;
        punch = bad ? 0.6f : 1f;
        if (bad) mistake = 1f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        punch = Mathf.MoveTowards(punch, 0f, dt * 3.5f);
        mistake = Mathf.MoveTowards(mistake, 0f, dt * 2.5f);
        float bounce = Mathf.Sin(punch * Mathf.PI * 3f) * punch * 0.18f;
        transform.localScale = baseScale * (1f + bounce);

        if (marker != null)
        {
            marker.Rotate(0f, 70f * dt, 0f, Space.World);
            marker.localScale = Vector3.one * (1f + punch * 0.4f);
        }

        if (glow == null) return;
        float vat = rules != null ? rules.Vat(side) : 0f;
        glow.color = Color.Lerp(color, MistakeFlash, mistake);
        glow.intensity = restGlow + vat * vatGlow + punch * 2.5f + mistake * 3f;
    }
}
