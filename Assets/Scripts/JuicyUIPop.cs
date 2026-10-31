using UnityEngine;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class JuicyUIPop : MonoBehaviour
{
    private RectTransform rectTransform;
    private Vector3 initialScale;
    private Coroutine activePopRoutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        initialScale = rectTransform.localScale;
        rectTransform.localScale = Vector3.zero;
    }

    private void OnEnable()
    {
        PlayPopIn();
    }

    public void PlayPopIn()
    {
        if (activePopRoutine != null) StopCoroutine(activePopRoutine);
        activePopRoutine = StartCoroutine(PopInRoutine());
    }

    private IEnumerator PopInRoutine()
    {
        float elapsedTime = 0f;
        float duration = 0.4f;
        rectTransform.localScale = Vector3.zero;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float elasticity = 6.0f;
            float easeOutBack = Mathf.Pow(t - 1f, 3f) * ((elasticity + 1f) * (t - 1f) + elasticity) + 1f;

            rectTransform.localScale = initialScale * easeOutBack;
            yield return null;
        }

        rectTransform.localScale = initialScale;
    }

    public void TriggerPunch(float punchScaleMultiplier = 1.2f)
    {
        StopAllCoroutines();
        StartCoroutine(PunchRoutine(punchScaleMultiplier));
    }

    private IEnumerator PunchRoutine(float targetMultiplier)
    {
        float elapsed = 0f;
        float duration = 0.2f;
        Vector3 peakScale = initialScale * targetMultiplier;

        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            rectTransform.localScale = Vector3.Lerp(initialScale, peakScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            rectTransform.localScale = Vector3.Lerp(peakScale, initialScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        rectTransform.localScale = initialScale;
    }
}