using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The title: the game's name over the alley, with the raccoon on his manhole
// behind it. A tap goes on to the map. Drawn in front of the camera
// (CameraOverlay). The name is a working title until the store listing picks
// one (T-046).
public class AlleyTitle : MonoBehaviour
{
    public const string WorkingName = "JUICY HEIST";

    static readonly Color Neon = new Color(1f, 0.85f, 0.25f);
    const float Distance = 0.32f;
    const float TapGuard = 0.5f;

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Material panelMaterial;

    CameraOverlay overlay;
    GameObject content;
    TextMeshPro prompt;
    float shownAt;

    public bool Showing => content != null && content.activeSelf;

    public void Configure(AlleyChuteDirector chute, Material material)
    {
        director = chute;
        panelMaterial = material;
    }

    void Start()
    {
        if (!director.UsesTitle)
        {
            gameObject.SetActive(false);
            return;
        }
        Camera view = Camera.main;
        overlay = new CameraOverlay(view, panelMaterial, Distance, CameraOverlay.LayerTitle);
        transform.SetParent(view.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        Show();
    }

    void Show()
    {
        director.Paused = true;
        JuicyMusic.Play(JuicyMusic.Track.Map);
        content = new GameObject("Title");
        content.transform.SetParent(transform, false);
        Transform root = content.transform;
        // Light at the middle so the raccoon shows; dark at top and bottom for the words.
        overlay.Quad(root, "Top_Shade", new Vector2(0.5f, 0.86f), new Vector2(1.3f, 0.36f), new Color(0.01f, 0.01f, 0.04f, 0.82f), 0.006f);
        overlay.Quad(root, "Bottom_Shade", new Vector2(0.5f, 0.1f), new Vector2(1.3f, 0.24f), new Color(0.01f, 0.01f, 0.04f, 0.82f), 0.006f);
        TextMeshPro name = overlay.Text(root, "Name", new Vector2(0.5f, 0.85f), new Vector2(0.92f, 0.11f), Neon);
        name.text = WorkingName;
        TextMeshPro line = overlay.Text(root, "Line", new Vector2(0.5f, 0.765f), new Vector2(0.8f, 0.03f), Color.white);
        line.text = "SORT THE LOOT. CRACK THE VAULT.";
        prompt = overlay.Text(root, "Prompt", new Vector2(0.5f, 0.12f), new Vector2(0.7f, 0.045f), Neon);
        prompt.text = "TAP TO PLAY";
        TextMeshPro version = overlay.Text(root, "Version", new Vector2(0.5f, 0.035f), new Vector2(0.4f, 0.018f), new Color(1f, 1f, 1f, 0.6f));
        version.text = "v" + Application.version;
        shownAt = Time.unscaledTime;
    }

    void Update()
    {
        if (!Showing) return;
        prompt.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.4f));
        if (Time.unscaledTime < shownAt + TapGuard) return;
        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasReleasedThisFrame) Continue();
    }

    // On to the map.
    public void Continue()
    {
        if (!Showing) return;
        JuicyAudioHooks.Tap();
        content.SetActive(false);
        director.OpenMap();
    }
}
