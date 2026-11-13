using UnityEngine;

// Tells the Plinko round when its coin strikes a pin, so the round can add
// the small, even scatter that real worn pins give a coin.
public class AlleyPlinkoCoin : MonoBehaviour
{
    AlleyPlinko plinko;
    Rigidbody body;

    void Awake()
    {
        plinko = GetComponentInParent<AlleyPlinko>();
        body = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision hit)
    {
        if (plinko != null && hit.collider.name.StartsWith("Pin_")) plinko.OnPinHit(body, hit.collider.transform);
    }
}
