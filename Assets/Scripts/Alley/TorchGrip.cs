using UnityEngine;

// Keeps a hand-held torch upright with its flame on top, whatever the hand's
// animation is doing. The grip point stays locked to the hand; only the
// torch's angle is overridden, with a little of the hand's swing blended in so
// it still reads as held.
public class TorchGrip : MonoBehaviour
{
    [SerializeField] Transform hand;
    [SerializeField] Transform body;
    [Tooltip("The grip point in the torch's local space.")]
    [SerializeField] Vector3 gripLocal;
    [SerializeField] float lean = -8f;
    [Range(0f, 1f)] [SerializeField] float handSway = 0.15f;

    Quaternion handRest;

    public void Configure(Transform holdingHand, Transform character, Vector3 grip, float forwardLean)
    {
        hand = holdingHand;
        body = character;
        gripLocal = grip;
        lean = forwardLean;
    }

    void Start()
    {
        if (hand != null) handRest = hand.rotation;
    }

    void LateUpdate()
    {
        if (hand == null || body == null) return;
        Quaternion upright = Quaternion.Euler(lean, body.eulerAngles.y, 0f);
        Quaternion sway = Quaternion.Slerp(Quaternion.identity, hand.rotation * Quaternion.Inverse(handRest), handSway);
        transform.rotation = sway * upright;
        transform.position += hand.position - transform.TransformPoint(gripLocal);
    }
}
