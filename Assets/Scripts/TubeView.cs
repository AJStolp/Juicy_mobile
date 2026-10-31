using UnityEngine;

public class TubeView : MonoBehaviour
{
    [SerializeField] private int tubeIndex;
    [SerializeField] private float slotSpacing = 0.28f;
    [SerializeField] private float baseHeight = 0.22f;
    [SerializeField] private float cameraOffset = 0.12f;

    public int TubeIndex => tubeIndex;

    public void SetTubeIndex(int index)
    {
        tubeIndex = index;
    }

    public Transform GetTopGemTransform()
    {
        if (transform.childCount == 0) return null;
        return transform.GetChild(transform.childCount - 1);
    }

    public Vector3 GetSlotPosition(int stackIndex)
    {
        float halfHeight = transform.lossyScale.y;
        Vector3 bottom = transform.position - transform.up * halfHeight;
        Vector3 towardCamera = -transform.forward * cameraOffset;
        return bottom + transform.up * (baseHeight + stackIndex * slotSpacing) + towardCamera;
    }

    public Vector3 GetNextAvailableSlotPosition()
    {
        return GetSlotPosition(transform.childCount);
    }
}
