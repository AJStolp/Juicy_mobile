using UnityEngine;

public class PlinkoSlot : MonoBehaviour
{
    [SerializeField] private double payout = 1000d;

    public double Payout => payout;
}
