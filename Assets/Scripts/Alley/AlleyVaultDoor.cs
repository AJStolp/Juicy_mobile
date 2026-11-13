using System.Collections;
using UnityEngine;

// The vault door is the progress meter. Stages 0 to 3 are the corner bolts,
// which back out as their stage fills and pop off when it locks. Stage 4 turns
// the outer ring, 5 the inner ring, 6 spins the wheel. Progress creeps forward
// quickly and grinds back slowly on a bust. Cracking the vault spins the
// wheel, flashes the keyhole green and swings the door open on a flood of
// light, then the door closes on a fresh vault.
public class AlleyVaultDoor : MonoBehaviour
{
    const int Bolts = 4;

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Transform door;
    [SerializeField] Transform[] bolts = new Transform[Bolts];
    [SerializeField] Transform ringOuter;
    [SerializeField] Transform ringInner;
    [SerializeField] Transform wheel;
    [SerializeField] Renderer keyhole;
    [SerializeField] Light flood;
    [SerializeField] AlleyCameraRig cameraRig;
    [Tooltip("World direction out of the door face, toward the street.")]
    [SerializeField] Vector3 outWorld = Vector3.back;
    [SerializeField] float boltTravel = 0.1f;
    [SerializeField] float creepSpeed = 1.5f;
    [SerializeField] float grindSpeed = 0.35f;
    [SerializeField] float floodIntensity = 6f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color KeyGreen = new Color(0.35f, 1f, 0.45f);

    VaultProgress vault;
    readonly float[] shown = new float[VaultProgress.Stages];
    readonly bool[] popped = new bool[Bolts];
    readonly float[] popTime = new float[Bolts];
    Vector3[] boltRest;
    Quaternion[] boltRestRotation;
    Quaternion outerRest, innerRest, wheelRest, doorRest;
    Vector3 doorRestPosition;
    float doorSwingSign = 1f;
    float wheelExtra;
    float keyFlash;
    bool cracking;
    MaterialPropertyBlock block;

    public bool Cracking => cracking;
    public int Pops { get; private set; }
    public int Rollbacks { get; private set; }
    public int Cracks { get; private set; }
    public Vector3 Focus => keyhole != null ? keyhole.transform.position : transform.position;

    public void Configure(AlleyChuteDirector chute, Transform swingDoor, Transform[] cornerBolts, Transform outer, Transform inner, Transform spokeWheel,
        Renderer keyLens, Light floodLight, AlleyCameraRig rig, Vector3 outward)
    {
        director = chute;
        door = swingDoor;
        bolts = cornerBolts;
        ringOuter = outer;
        ringInner = inner;
        wheel = spokeWheel;
        keyhole = keyLens;
        flood = floodLight;
        cameraRig = rig;
        outWorld = outward;
    }

    public void Bind(VaultProgress progress)
    {
        Unbind();
        vault = progress;
        vault.StageLocked += OnStageLocked;
        vault.RolledBack += OnRolledBack;
        vault.Cracked += OnCracked;
        Snap();
    }

    void Awake()
    {
        block = new MaterialPropertyBlock();
        boltRest = new Vector3[bolts.Length];
        boltRestRotation = new Quaternion[bolts.Length];
        for (int i = 0; i < bolts.Length; i++)
        {
            boltRest[i] = bolts[i].localPosition;
            boltRestRotation[i] = bolts[i].localRotation;
        }
        outerRest = ringOuter.localRotation;
        innerRest = ringInner.localRotation;
        wheelRest = wheel.localRotation;
        doorRest = door.localRotation;
        doorRestPosition = door.localPosition;
        doorSwingSign = OutwardSwingSign();
        if (flood != null) flood.intensity = 0f;
    }

    void Start()
    {
        if (director != null) Bind(director.Vault);
    }

    void OnDestroy() => Unbind();

    void Unbind()
    {
        if (vault == null) return;
        vault.StageLocked -= OnStageLocked;
        vault.RolledBack -= OnRolledBack;
        vault.Cracked -= OnCracked;
        vault = null;
    }

    // Jumps straight to the vault's state, as after loading a save.
    public void Snap()
    {
        for (int s = 0; s < shown.Length; s++) shown[s] = Target(s);
        for (int i = 0; i < Bolts; i++)
        {
            popped[i] = vault != null && vault.Stage > i;
            popTime[i] = popped[i] ? 99f : 0f;
        }
        Pose();
    }

    float Target(int stage)
    {
        if (vault == null || stage > vault.Stage) return 0f;
        return stage < vault.Stage ? 1f : vault.StageFraction;
    }

    void OnStageLocked(int stage)
    {
        if (cracking) return;
        shown[stage] = 1f;
        JuicyAudioHooks.Vault();
        if (stage < Bolts)
        {
            popped[stage] = true;
            popTime[stage] = 0f;
            Pops++;
        }
        keyFlash = 1f;
        if (cameraRig != null)
        {
            cameraRig.Push(Focus, 0.18f, 1.1f);
            cameraRig.Shake(0.25f);
        }
    }

    void OnRolledBack(int stage, float fraction)
    {
        Rollbacks++;
        if (cameraRig != null) cameraRig.Push(Focus, 0.12f, 1.4f);
    }

    void OnCracked(int heist)
    {
        Cracks++;
        StartCoroutine(Crack());
    }

    IEnumerator Crack()
    {
        cracking = true;
        for (int s = 0; s < shown.Length; s++) shown[s] = 1f;
        if (cameraRig != null) cameraRig.Push(Focus, 0.22f, 5f);
        JuicyAudioHooks.Vault();

        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            wheelExtra = 720f * Mathf.SmoothStep(0f, 1f, t);
            keyFlash = 1f + t * 2f;
            Pose();
            yield return null;
        }
        JuicyAudioHooks.Chime();
        if (cameraRig != null) cameraRig.Shake(0.4f);
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            SwingDoor(105f * EaseOutBack(t / 1.2f));
            if (flood != null) flood.intensity = floodIntensity * Mathf.Clamp01(t / 0.6f);
            yield return null;
        }
        yield return new WaitForSeconds(2.5f);
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            SwingDoor(105f * (1f - Mathf.SmoothStep(0f, 1f, t)));
            if (flood != null) flood.intensity = floodIntensity * (1f - t);
            yield return null;
        }
        SwingDoor(0f);
        if (flood != null) flood.intensity = 0f;
        wheelExtra = 0f;
        cracking = false;
        Snap();
    }

    void Update()
    {
        if (vault == null || cracking) return;
        float dt = Time.deltaTime;
        bool grinding = false;
        for (int s = 0; s < shown.Length; s++)
        {
            float target = Target(s);
            if (target < shown[s]) grinding = true;
            shown[s] = Mathf.MoveTowards(shown[s], target, dt * (target < shown[s] ? grindSpeed : creepSpeed));
        }
        for (int i = 0; i < Bolts; i++) popTime[i] += dt;
        keyFlash = Mathf.MoveTowards(keyFlash, 0f, dt * 1.5f);
        door.localPosition = doorRestPosition + (grinding ? door.parent.InverseTransformVector(Random.insideUnitSphere * 0.004f) : Vector3.zero);
        Pose();
    }

    void Pose()
    {
        for (int i = 0; i < bolts.Length; i++) PoseBolt(i);
        Turn(ringOuter, outerRest, 90f * shown[4]);
        Turn(ringInner, innerRest, -90f * shown[5]);
        Turn(wheel, wheelRest, 360f * shown[6] + wheelExtra);

        if (keyhole == null) return;
        float overall = vault != null ? (vault.Stage + vault.StageFraction) / VaultProgress.Stages : 0f;
        keyhole.GetPropertyBlock(block);
        block.SetColor(EmissionId, KeyGreen * (0.4f + overall * 2f + keyFlash * 4f));
        keyhole.SetPropertyBlock(block);
    }

    void PoseBolt(int i)
    {
        Transform bolt = bolts[i];
        Vector3 outLocal = bolt.parent.InverseTransformVector(outWorld.normalized);
        if (!popped[i])
        {
            bolt.gameObject.SetActive(true);
            bolt.localPosition = boltRest[i] + outLocal * (boltTravel * shown[i]);
            bolt.localRotation = boltRestRotation[i] * Quaternion.AngleAxis(540f * shown[i], bolt.InverseTransformDirection(outWorld));
            return;
        }
        // Popped: flies off toward the street and drops, then is gone.
        float t = popTime[i];
        bool flying = t < 1.1f;
        bolt.gameObject.SetActive(flying);
        if (!flying) return;
        Vector3 worldOffset = outWorld.normalized * (boltTravel + 1.6f * t) + Vector3.up * (1.4f * t - 4.9f * t * t);
        bolt.localPosition = boltRest[i] + bolt.parent.InverseTransformVector(worldOffset);
        bolt.localRotation = boltRestRotation[i] * Quaternion.AngleAxis(540f + 900f * t, Vector3.right);
    }

    void Turn(Transform part, Quaternion rest, float degrees)
    {
        Vector3 axis = Quaternion.Inverse(part.parent.rotation * rest) * outWorld.normalized;
        part.localRotation = rest * Quaternion.AngleAxis(degrees, axis);
    }

    void SwingDoor(float degrees)
    {
        Vector3 axis = Quaternion.Inverse(door.parent.rotation * doorRest) * Vector3.up;
        door.localRotation = doorRest * Quaternion.AngleAxis(degrees * doorSwingSign, axis);
    }

    // The door must swing out toward the street, whichever way the model faces.
    float OutwardSwingSign()
    {
        Vector3 center = keyhole != null ? keyhole.transform.position : door.position;
        Vector3 hingeToCenter = center - door.position;
        Vector3 moved = Quaternion.AngleAxis(10f, Vector3.up) * hingeToCenter - hingeToCenter;
        return Vector3.Dot(moved, outWorld) >= 0f ? 1f : -1f;
    }

    static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c = 1.4f;
        float u = t - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }
}
