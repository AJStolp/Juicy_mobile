using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Settings, opened from the map: sound on or off, music on or off (both kept
// between sittings, AudioPrefs), and the privacy policy. Drawn in front of
// the camera (CameraOverlay), above the map.
public class AlleySettings : MonoBehaviour
{
    static readonly Color Neon = new Color(1f, 0.85f, 0.25f);
    static readonly Color On = new Color(0.35f, 0.95f, 0.5f);
    static readonly Color Off = new Color(0.45f, 0.45f, 0.5f);
    const float Distance = 0.31f;
    const float TapGuard = 0.25f;

    [SerializeField] Material panelMaterial;
    // The public privacy policy page (docs/privacy-policy.md, once hosted).
    // The row hides while this is empty. Kept in code so a scene rebuild
    // never loses it.
    public const string PrivacyUrl = "";

    CameraOverlay overlay;
    GameObject content;
    Renderer soundHit;
    Renderer musicHit;
    Renderer privacyHit;
    Renderer closeHit;
    float shownAt;

    public bool Showing => content != null && content.activeSelf;
    // When settings last closed, so the map ignores the closing tap.
    public float ClosedAt { get; private set; } = -1f;

    public void Configure(Material material)
    {
        panelMaterial = material;
    }

    void Start()
    {
        Camera view = Camera.main;
        overlay = new CameraOverlay(view, panelMaterial, Distance, CameraOverlay.LayerSettings);
        transform.SetParent(view.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Open()
    {
        Build();
        shownAt = Time.unscaledTime;
    }

    public void Close()
    {
        if (content != null) content.SetActive(false);
        ClosedAt = Time.unscaledTime;
    }

    public void ToggleSound()
    {
        AudioPrefs.SoundOn = !AudioPrefs.SoundOn;
        JuicyAudioHooks.Tap();
        Build();
    }

    public void ToggleMusic()
    {
        AudioPrefs.MusicOn = !AudioPrefs.MusicOn;
        JuicyAudioHooks.Tap();
        Build();
    }

    void Build()
    {
        if (content != null) Destroy(content);
        content = new GameObject("Settings");
        content.transform.SetParent(transform, false);
        Transform root = content.transform;
        overlay.Quad(root, "Dim", new Vector2(0.5f, 0.5f), new Vector2(1.3f, 1.3f), new Color(0.01f, 0.01f, 0.03f, 0.9f), 0.008f);
        overlay.Quad(root, "Card", new Vector2(0.5f, 0.52f), new Vector2(0.84f, 0.6f), new Color(0.07f, 0.07f, 0.1f, 1f), 0.006f);
        TextMeshPro title = overlay.Text(root, "Title", new Vector2(0.5f, 0.75f), new Vector2(0.7f, 0.05f), Neon);
        title.text = "SETTINGS";
        soundHit = Row(root, "Sound", 0.62f, "SOUND", AudioPrefs.SoundOn);
        musicHit = Row(root, "Music", 0.52f, "MUSIC", AudioPrefs.MusicOn);
        privacyHit = null;
        if (PrivacyUrl.Length > 0) privacyHit = Button(root, "Privacy", 0.42f, "PRIVACY POLICY", Color.white);
        closeHit = Button(root, "Close", 0.28f, "CLOSE", Neon);
    }

    Renderer Row(Transform root, string name, float y, string label, bool on)
    {
        Renderer back = overlay.Quad(root, name, new Vector2(0.5f, y), new Vector2(0.7f, 0.07f), new Color(1f, 1f, 1f, 0.08f), 0.004f);
        TextMeshPro text = overlay.Text(root, name + "_Label", new Vector2(0.38f, y), new Vector2(0.36f, 0.035f), Color.white);
        text.text = label;
        overlay.Quad(root, name + "_State", new Vector2(0.7f, y), new Vector2(0.2f, 0.05f), on ? On : Off, 0.003f);
        TextMeshPro state = overlay.Text(root, name + "_State_Label", new Vector2(0.7f, y), new Vector2(0.16f, 0.03f), Color.black);
        state.outlineWidth = 0f;
        state.text = on ? "ON" : "OFF";
        return back;
    }

    Renderer Button(Transform root, string name, float y, string label, Color color)
    {
        Renderer back = overlay.Quad(root, name, new Vector2(0.5f, y), new Vector2(0.7f, 0.065f), new Color(1f, 1f, 1f, 0.08f), 0.004f);
        TextMeshPro text = overlay.Text(root, name + "_Label", new Vector2(0.5f, y), new Vector2(0.6f, 0.032f), color);
        text.text = label;
        return back;
    }

    void Update()
    {
        if (!Showing || Time.unscaledTime < shownAt + TapGuard) return;
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasReleasedThisFrame) return;
        Vector2 tap = pointer.position.ReadValue();
        if (overlay.ScreenRect(soundHit).Contains(tap)) ToggleSound();
        else if (overlay.ScreenRect(musicHit).Contains(tap)) ToggleMusic();
        else if (privacyHit != null && overlay.ScreenRect(privacyHit).Contains(tap)) Application.OpenURL(PrivacyUrl);
        else if (overlay.ScreenRect(closeHit).Contains(tap))
        {
            JuicyAudioHooks.Tap();
            Close();
        }
    }
}
