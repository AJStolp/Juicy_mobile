using UnityEngine;

public class TorchFlicker : MonoBehaviour
{
    Light torchLight;
    [SerializeField] float minIntensity = 1.2f;
    [SerializeField] float maxIntensity = 2.0f;
    [SerializeField] float flickerSpeed = 0.08f;

    void Start()
    {
        torchLight = GetComponent<Light>();
        if (torchLight == null) return;
        InvokeRepeating(nameof(ApplyFlicker), 0f, flickerSpeed);
    }

    void ApplyFlicker()
    {
        if (torchLight == null) return;
        torchLight.intensity = Random.Range(minIntensity, maxIntensity);
    }
}
