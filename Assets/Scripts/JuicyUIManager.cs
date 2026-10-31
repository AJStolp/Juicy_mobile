using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class JuicyUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button claimRewardButton;

    [Header("Reward")]
    [SerializeField] private double claimPayout = 2500d;

    private JuicyWinTicker winTicker;
    private RectTransform buttonRect;
    private Vector3 buttonScale = Vector3.one;
    private Coroutine punchRoutine;
    private bool rewardPortraitShown;

    static readonly string[] RewardPortraitPaths =
    {
        "UIHeroes/vault_door_closed",
        "UIHeroes/gem_ruby",
        "UIHeroes/gem_sapphire",
        "UIHeroes/gem_emerald",
        "UIHeroes/gem_topaz",
        "UIHeroes/coins_hero"
    };

    private void Awake()
    {
        winTicker = FindFirstObjectByType<JuicyWinTicker>();

        if (claimRewardButton == null) return;

        buttonRect = claimRewardButton.GetComponent<RectTransform>();
        buttonScale = buttonRect.localScale;
        claimRewardButton.onClick.AddListener(OnClaimReward);
    }

    public void OnClaimReward()
    {
        if (buttonRect != null)
        {
            if (punchRoutine != null) StopCoroutine(punchRoutine);
            punchRoutine = StartCoroutine(PunchButton());
        }

        ShowRewardPortrait();

        if (winTicker == null)
            winTicker = FindFirstObjectByType<JuicyWinTicker>();

        if (winTicker == null) return;
        winTicker.AddWin(claimPayout);
    }

    private IEnumerator PunchButton()
    {
        const float duration = 0.2f;
        Vector3 peak = buttonScale * 1.18f;

        float elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / (duration * 0.5f));
            buttonRect.localScale = Vector3.Lerp(buttonScale, peak, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / (duration * 0.5f));
            buttonRect.localScale = Vector3.Lerp(peak, buttonScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        buttonRect.localScale = buttonScale;
        punchRoutine = null;
    }

    public int ShowRewardPortrait()
    {
        if (rewardPortraitShown || buttonRect == null) return 0;

        Sprite[] sprites = new Sprite[RewardPortraitPaths.Length];
        int count = 0;
        for (int i = 0; i < RewardPortraitPaths.Length; i++)
        {
            Sprite sprite = Resources.Load<Sprite>(RewardPortraitPaths[i]);
            if (sprite == null) continue;
            sprites[count] = sprite;
            count++;
        }

        if (count == 0) return 0;
        rewardPortraitShown = true;

        const float portraitHeight = 150f;
        const float gapAboveButton = 16f;
        float y = buttonRect.anchoredPosition.y
            + buttonRect.sizeDelta.y * 0.5f
            + gapAboveButton
            + portraitHeight * 0.5f;

        GameObject row = new GameObject("RewardPortrait", typeof(RectTransform));
        row.transform.SetParent(buttonRect.parent, false);
        row.transform.SetAsLastSibling();
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = buttonRect.anchorMin;
        rowRect.anchorMax = buttonRect.anchorMax;
        rowRect.pivot = buttonRect.pivot;
        rowRect.anchoredPosition = new Vector2(buttonRect.anchoredPosition.x, y);
        rowRect.sizeDelta = new Vector2(buttonRect.sizeDelta.x, portraitHeight);

        const float minGap = 8f;
        float icon = 88f;
        if (count > 1)
        {
            float fitted = (rowRect.sizeDelta.x - minGap * (count - 1)) / count;
            if (fitted < icon) icon = fitted;
        }

        float gap = count > 1 ? (rowRect.sizeDelta.x - icon * count) / (count - 1) : 0f;
        float start = -0.5f * (icon * count + gap * (count - 1)) + icon * 0.5f;

        for (int i = 0; i < count; i++)
        {
            GameObject iconObject = new GameObject(sprites[i].name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(row.transform, false);
            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(icon, icon);
            rect.anchoredPosition = new Vector2(start + i * (icon + gap), 0f);
            Image image = iconObject.GetComponent<Image>();
            image.sprite = sprites[i];
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        return count;
    }
}
