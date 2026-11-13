using UnityEngine;

// Owns the alley camera's pose. Gameplay asks for moments instead of moving
// the camera itself: a shake, a short push toward something worth seeing
// (the vault turning), or a hold on a pose until released (Plinko).
public class AlleyCameraRig : MonoBehaviour
{
    [SerializeField] float holdBlendSpeed = 1.6f;

    Camera view;
    Vector3 homePosition;
    Quaternion homeRotation;
    float homeFov;
    float holdFov;
    bool homed;

    float shake;
    Vector3 pushTarget;
    float pushAmount;
    float pushTime;
    float pushDuration;

    bool holding;
    Vector3 holdPosition;
    Quaternion holdRotation;
    float holdBlend;

    public bool Holding => holding;
    public bool Settled => holding ? holdBlend >= 0.999f : holdBlend <= 0.001f;

    void Start()
    {
        view = GetComponent<Camera>();
        homePosition = transform.position;
        homeRotation = transform.rotation;
        homeFov = view != null ? view.fieldOfView : 45f;
        holdFov = homeFov;
        homed = true;
    }

    public void Shake(float amount) => shake = Mathf.Max(shake, amount);

    // Moves part of the way toward target and back over the duration.
    public void Push(Vector3 target, float amount, float duration)
    {
        pushTarget = target;
        pushAmount = amount;
        pushDuration = Mathf.Max(0.1f, duration);
        pushTime = 0f;
    }

    // Fov widens the view while holding, so a wide subject fits a tall screen.
    public void Hold(Vector3 position, Vector3 lookAt, float fov)
    {
        holding = true;
        holdPosition = position;
        holdRotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
        holdFov = fov;
    }

    public void Release() => holding = false;

    void LateUpdate()
    {
        if (!homed) return;
        float dt = Time.deltaTime;
        holdBlend = Mathf.MoveTowards(holdBlend, holding ? 1f : 0f, dt * holdBlendSpeed);
        float eased = Mathf.SmoothStep(0f, 1f, holdBlend);
        Vector3 position = Vector3.Lerp(homePosition, holdPosition, eased);
        Quaternion rotation = Quaternion.Slerp(homeRotation, holdRotation, eased);
        if (view != null) view.fieldOfView = Mathf.Lerp(homeFov, holdFov, eased);

        if (pushTime < pushDuration)
        {
            pushTime += dt;
            float wave = Mathf.Sin(Mathf.Clamp01(pushTime / pushDuration) * Mathf.PI);
            Vector3 toward = pushTarget - position;
            position += toward * (pushAmount * wave);
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(toward, Vector3.up), wave * 0.35f);
        }

        shake = Mathf.MoveTowards(shake, 0f, dt * 1.4f);
        if (shake > 0f) position += Random.insideUnitSphere * (shake * shake * 0.12f);
        transform.SetPositionAndRotation(position, rotation);
    }
}
