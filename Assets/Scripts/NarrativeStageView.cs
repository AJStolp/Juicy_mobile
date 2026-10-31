using System;
using System.Collections;
using UnityEngine;

// Drives the existing heist track under the cash ticker. Does not spawn a second stage.
public class NarrativeStageView : MonoBehaviour
{
    public static NarrativeStageView Instance { get; private set; }

    RectTransform character;
    Coroutine stepRoutine;

    public bool Bound => character != null;
    public bool Playing { get; private set; }
    public Vector2 CharacterAnchoredPosition => character != null ? character.anchoredPosition : Vector2.zero;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (stepRoutine != null) StopCoroutine(stepRoutine);
    }

    public void Bind(RectTransform characterTransform)
    {
        character = characterTransform;
    }

    public void Snap(Vector2 position)
    {
        if (stepRoutine != null) StopCoroutine(stepRoutine);
        stepRoutine = null;
        Playing = false;
        if (character != null) character.anchoredPosition = position;
    }

    public void NudgeToward(Vector2 stop, float fraction, float duration)
    {
        if (character == null) return;
        Vector2 target = Vector2.Lerp(character.anchoredPosition, stop, Mathf.Clamp01(fraction));
        PlayTo(target, duration, null);
    }

    public void PlayTo(Vector2 target, float duration, Action onArrive)
    {
        if (character == null)
        {
            if (onArrive != null) onArrive();
            return;
        }

        if (stepRoutine != null) StopCoroutine(stepRoutine);
        stepRoutine = StartCoroutine(StepRoutine(target, duration, onArrive));
    }

    IEnumerator StepRoutine(Vector2 targetPosition, float duration, Action onArrive)
    {
        Playing = true;
        float elapsed = 0f;
        Vector2 startPos = character.anchoredPosition;
        float span = Mathf.Max(0.05f, duration);

        while (elapsed < span)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / span);
            const float elasticity = 5f;
            float easeOutBack = Mathf.Pow(t - 1f, 3f) * ((elasticity + 1f) * (t - 1f) + elasticity) + 1f;
            character.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPosition, easeOutBack);
            yield return null;
        }

        character.anchoredPosition = targetPosition;
        Playing = false;
        stepRoutine = null;
        if (onArrive != null) onArrive();
    }
}
