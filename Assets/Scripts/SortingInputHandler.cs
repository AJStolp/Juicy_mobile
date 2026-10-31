using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class SortingInputHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SortingGridManager gridManager;
    [SerializeField] private Camera mainCamera;

    [Header("Juice & Selection Settings")]
    [SerializeField] private float gemLiftHeight = 0.34f;
    [SerializeField] private float liftDuration = 0.15f;

    // State lock to prevent input spam during animations (Zero-GC guard)
    private bool isAnimating = false;
    private Transform selectedGemTransform = null;
    private Vector3 gemOriginalPosition;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (gridManager == null) gridManager = GetComponent<SortingGridManager>();
    }

    private void Update()
    {
        // Block player touch/click input if an animation is currently playing
        if (isAnimating) return;

        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        ProcessInput(pointer.position.ReadValue());
    }

    private void ProcessInput(Vector3 screenPosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Expecting tube GameObjects to have a TubeComponent attached with their ID index
            TubeView tubeView = hit.collider.GetComponentInParent<TubeView>();
            if (tubeView != null)
            {
                OnTubeTapped(tubeView);
            }
        }
    }

    private void OnTubeTapped(TubeView tubeView)
    {
        int clickedIndex = tubeView.TubeIndex;

        if (gridManager == null) return;

        TubeClickOutcome outcome = gridManager.HandleTubeClick(clickedIndex);

        if (outcome.Kind == TubeClickKind.Selected)
        {
            Transform topGem = tubeView.GetTopGemTransform();
            if (topGem == null) return;
            selectedGemTransform = topGem;
            gemOriginalPosition = topGem.position;
            StartCoroutine(AnimateGemLift(topGem, gemOriginalPosition + (Vector3.up * gemLiftHeight)));
            return;
        }

        if (selectedGemTransform == null) return;

        if (outcome.Kind == TubeClickKind.Moved)
        {
            Vector3 dropTarget = tubeView.GetNextAvailableSlotPosition();
            GemView gemView = selectedGemTransform.GetComponent<GemView>();
            if (gemView != null) gemView.AttachTo(tubeView.transform);
            else selectedGemTransform.SetParent(tubeView.transform, true);
            StartCoroutine(AnimateGemMoveAndDrop(selectedGemTransform, dropTarget));
            selectedGemTransform = null;
            return;
        }

        if (outcome.Kind == TubeClickKind.Deselected || outcome.Kind == TubeClickKind.Rejected)
        {
            TubeView home = selectedGemTransform.GetComponentInParent<TubeView>();
            int slot = selectedGemTransform.GetSiblingIndex();
            Vector3 homePosition = home != null ? home.GetSlotPosition(slot) : gemOriginalPosition;
            StartCoroutine(AnimateGemMoveAndDrop(selectedGemTransform, homePosition));
            selectedGemTransform = null;
        }
    }

    private IEnumerator AnimateGemLift(Transform gem, Vector3 targetPos)
    {
        isAnimating = true;
        float elapsed = 0f;
        Vector3 startPos = gem.position;

        while (elapsed < liftDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / liftDuration);
            
            // Smooth step ease for snappy picking motion
            gem.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        gem.position = targetPos;
        isAnimating = false;
    }

    private IEnumerator AnimateGemMoveAndDrop(Transform gem, Vector3 targetPos)
    {
        isAnimating = true;
        float elapsed = 0f;
        float moveDuration = 0.25f;
        Vector3 startPos = gem.position;

        // Phase 1: Arc travel over to the top of the destination tube
        Vector3 apexPoint = new Vector3(targetPos.x, Mathf.Max(startPos.y, targetPos.y + 1f), startPos.z);

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);

            Vector3 m1 = Vector3.Lerp(startPos, apexPoint, t);
            Vector3 m2 = Vector3.Lerp(apexPoint, targetPos, t);
            gem.position = Vector3.Lerp(m1, m2, Mathf.SmoothStep(0f, 1f, t));
            
            yield return null;
        }

        // Phase 2: Elastic bounce landing at bottom of tube using our Back-Ease formula
        float bounceElapsed = 0f;
        float bounceDuration = 0.15f;
        Vector3 bounceStart = gem.position;

        while (bounceElapsed < bounceDuration)
        {
            bounceElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(bounceElapsed / bounceDuration);
            float elasticity = 4.0f;
            float easeOutBack = Mathf.Pow(t - 1f, 3f) * ((elasticity + 1f) * (t - 1f) + elasticity) + 1f;

            gem.position = Vector3.LerpUnclamped(bounceStart, targetPos, easeOutBack);
            yield return null;
        }

        gem.position = targetPos;
        isAnimating = false;
    }
}