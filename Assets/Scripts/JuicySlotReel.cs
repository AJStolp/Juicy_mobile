using UnityEngine;
using System.Collections;

public class JuicySlotReel : MonoBehaviour
{
    [Header("Reel Configuration")]
    [Tooltip("Total distance each symbol/item occupies vertically.")]
    [SerializeField] private float symbolHeight = 2.0f;
    [Tooltip("Total number of unique items on the loop.")]
    [SerializeField] private int totalSymbols = 6;
    
    [Header("Juice & Dynamics")]
    [SerializeField] private float maxSpeed = 45f;
    [SerializeField] private float accelerationTime = 0.35f;
    [SerializeField] private float bounceOvershoot = 0.6f;
    [SerializeField] private float settleDuration = 0.25f;

    [Header("Shader Properties (GPU Optimization)")]
    [SerializeField] private Renderer reelRenderer;
    [SerializeField] private string blurPropertyName = "_MotionBlurIntensity";

    private int blurPropertyID;
    private float currentPosition;
    private bool isSpinning;
    private Coroutine activeSpinCoroutine;

    private void Awake()
    {
        blurPropertyID = Shader.PropertyToID(blurPropertyName);
        if (reelRenderer == null) reelRenderer = GetComponent<Renderer>();
    }

    public void StartSpin()
    {
        if (isSpinning && activeSpinCoroutine != null) StopCoroutine(activeSpinCoroutine);
        activeSpinCoroutine = StartCoroutine(SpinRoutine());
    }

    public void StopSpin(int targetIndex)
    {
        if (!isSpinning) return;
        StopCoroutine(activeSpinCoroutine);
        activeSpinCoroutine = StartCoroutine(StopRoutine(targetIndex));
    }

    private IEnumerator SpinRoutine()
    {
        isSpinning = true;
        float elapsedTime = 0f;
        float currentSpeed = 0f;

        while (elapsedTime < accelerationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / accelerationTime);
            currentSpeed = Mathf.Lerp(0f, maxSpeed, Mathf.SmoothStep(0f, 1f, t));
            currentPosition += currentSpeed * Time.deltaTime;
            ApplyMovementAndShader(currentSpeed);
            yield return null;
        }

        while (isSpinning)
        {
            currentPosition += maxSpeed * Time.deltaTime;
            ApplyMovementAndShader(maxSpeed);
            yield return null;
        }
    }

    private IEnumerator StopRoutine(int targetIndex)
    {
        float startPos = currentPosition;
        float targetBasePos = targetIndex * symbolHeight;
        float normalizedCurrent = currentPosition % (totalSymbols * symbolHeight);
        float distanceToTarget = targetBasePos - normalizedCurrent;
        if (distanceToTarget <= 0) distanceToTarget += (totalSymbols * symbolHeight);
        
        float finalTargetPos = currentPosition + distanceToTarget + (totalSymbols * symbolHeight * 2f);
        float elapsed = 0f;
        float decelerationTime = 0.6f;

        while (elapsed < decelerationTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / decelerationTime);
            float easeVal = 1f - Mathf.Pow(1f - t, 4f);
            currentPosition = Mathf.Lerp(startPos, finalTargetPos - bounceOvershoot, easeVal);
            
            float instantaneousSpeed = (finalTargetPos - bounceOvershoot - currentPosition) / (decelerationTime - elapsed + 0.0001f);
            ApplyMovementAndShader(instantaneousSpeed);
            yield return null;
        }

        float bounceElapsed = 0f;
        float overshootStartPos = currentPosition;
        float restingTargetPos = finalTargetPos - bounceOvershoot;

        while (bounceElapsed < settleDuration)
        {
            bounceElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(bounceElapsed / settleDuration);
            float elasticity = 7.0158f;
            float easeOutBack = (Mathf.Pow(t - 1, 3) * ((elasticity + 1) * (t - 1) + elasticity) + 1);

            currentPosition = Mathf.LerpUnclamped(overshootStartPos, restingTargetPos + bounceOvershoot, easeOutBack);
            ApplyMovementAndShader(maxSpeed * (1f - t));
            yield return null;
        }

        currentPosition = restingTargetPos;
        ApplyMovementAndShader(0f);
        isSpinning = false;
    }

    private void ApplyMovementAndShader(float speed)
    {
        float wrappedY = -(currentPosition % (totalSymbols * symbolHeight));
        transform.localPosition = new Vector3(transform.localPosition.x, wrappedY, transform.localPosition.z);

        if (reelRenderer != null && reelRenderer.sharedMaterial != null)
        {
            float blurFactor = Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed);
            reelRenderer.sharedMaterial.SetFloat(blurPropertyID, blurFactor);
        }
    }
}