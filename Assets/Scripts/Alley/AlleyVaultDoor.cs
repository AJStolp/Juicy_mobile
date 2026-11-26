using System.Collections;
using UnityEngine;

// The vault door is the progress meter, on the graffiti facade's own door.
// Stages 0 to 3 slide its three locking bars and then its handle, stage 4
// turns the gear lock, and the wheel turns a little with every stage.
// It turns during the vault boss (VaultProgress): progress creeps forward
// quickly, and grinds back slowly to a fresh door when a new attempt starts.
// Cracking the vault spins the wheel and swings the door open on a flood of
// light, then the door closes again.
public class AlleyVaultDoor : MonoBehaviour
{
    const int Bolts = 4;

    [SerializeField] AlleyChuteDirector director;
    [SerializeField] Transform door;
    [SerializeField] Transform[] bolts = new Transform[Bolts];
    [Tooltip("World offset each bolt slides to as its stage fills.")]
    [SerializeField] Vector3[] boltSlide = new Vector3[Bolts];
    [SerializeField] Transform gear;
    [SerializeField] Transform wheel;
    [Tooltip("Degrees the wheel turns per stage.")]
    [SerializeField] float wheelPerStage = 90f;
    [SerializeField] Light flood;
    [SerializeField] AlleyCameraRig cameraRig;
    [Tooltip("World direction out of the door face, toward the street.")]
    [SerializeField] Vector3 outWorld = Vector3.back;
    [SerializeField] float creepSpeed = 1.5f;
    [SerializeField] float grindSpeed = 0.35f;
    [SerializeField] float floodIntensity = 6f;

    VaultProgress vault;
    readonly float[] shown = new float[VaultProgress.Stages];
    readonly bool[] popped = new bool[Bolts];
    Vector3[] boltRest;
    Vector3[] boltSlideLocal;
    Quaternion gearRest, wheelRest, doorRest;
    Vector3 doorRestPosition;
    float doorSwingSign = 1f;
    float wheelExtra;
    bool cracking;
    bool rested;

    public int Pops { get; private set; }
    public int Cracks { get; private set; }
    public Vector3 Focus => wheel != null ? wheel.position : transform.position;

    public void Configure(AlleyChuteDirector chute, Transform swingDoor, Transform[] slidingBolts, Vector3[] slide, Transform gearLock,
        Transform spokeWheel, float degreesPerStage, Light floodLight, AlleyCameraRig rig, Vector3 outward)
    {
        director = chute;
        door = swingDoor;
        bolts = slidingBolts;
        boltSlide = slide;
        gear = gearLock;
        wheel = spokeWheel;
        wheelPerStage = degreesPerStage;
        flood = floodLight;
        cameraRig = rig;
        outWorld = outward;
    }

    public void Bind(VaultProgress progress)
    {
        Unbind();
        vault = progress;
        vault.StageLocked += OnStageLocked;
        vault.Cracked += OnCracked;
        vault.Restarted += OnRestarted;
        Snap();
    }

    // A fresh door: the bolts slide home and the rest grinds back in Update.
    void OnRestarted()
    {
        for (int i = 0; i < Bolts; i++) popped[i] = false;
    }

    void Awake()
    {
        Rest();
        if (flood != null) flood.intensity = 0f;
    }

    // Reads every part's rest pose once.
    void Rest()
    {
        if (rested) return;
        rested = true;
        boltRest = new Vector3[bolts.Length];
        boltSlideLocal = new Vector3[bolts.Length];
        for (int i = 0; i < bolts.Length; i++)
        {
            boltRest[i] = bolts[i].localPosition;
            boltSlideLocal[i] = bolts[i].parent.InverseTransformVector(boltSlide[i]);
        }
        if (gear != null) gearRest = gear.localRotation;
        if (wheel != null) wheelRest = wheel.localRotation;
        if (door != null)
        {
            doorRest = door.localRotation;
            doorRestPosition = door.localPosition;
        }
        doorSwingSign = OutwardSwingSign();
    }

    // Poses the door for a still in the editor: the given progress and the
    // door swung open by doorDegrees. EndPreview puts every part back.
    public void Preview(VaultProgress progress, float doorDegrees)
    {
        Rest();
        vault = progress;
        Snap();
        SwingDoor(doorDegrees);
        if (flood != null) flood.intensity = doorDegrees > 0f ? floodIntensity : 0f;
    }

    public void EndPreview()
    {
        vault = null;
        Snap();
        SwingDoor(0f);
        if (flood != null) flood.intensity = 0f;
        rested = false;
    }

    // How many stages the door shows as locked, read from the parts
    // themselves: fully slid bolts and the gear a quarter turned.
    public int LockedShown()
    {
        int count = 0;
        for (int i = 0; i < bolts.Length; i++)
            if ((bolts[i].localPosition - boltRest[i]).magnitude >= boltSlideLocal[i].magnitude * 0.99f) count++;
        if (gear != null && Quaternion.Angle(gear.localRotation, gearRest) >= 89f) count++;
        return count;
    }

    void Start()
    {
        if (director != null) Bind(director.Vault);
    }

    void OnDestroy()
    {
        Unbind();
    }

    void Unbind()
    {
        if (vault == null) return;
        vault.StageLocked -= OnStageLocked;
        vault.Cracked -= OnCracked;
        vault.Restarted -= OnRestarted;
        vault = null;
    }

    // Jumps straight to the vault's state, as after loading a save.
    public void Snap()
    {
        for (int s = 0; s < shown.Length; s++) shown[s] = Target(s);
        for (int i = 0; i < Bolts; i++) popped[i] = vault != null && vault.Stage > i;
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
            Pops++;
        }
        if (cameraRig != null)
        {
            cameraRig.Push(Focus, 0.18f, 1.1f);
            cameraRig.Shake(0.25f);
        }
    }

    void OnCracked()
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
        if (door != null)
            door.localPosition = doorRestPosition + (grinding ? door.parent.InverseTransformVector(Random.insideUnitSphere * 0.004f) : Vector3.zero);
        Pose();
    }

    void Pose()
    {
        for (int i = 0; i < bolts.Length; i++)
            bolts[i].localPosition = boltRest[i] + boltSlideLocal[i] * (popped[i] ? 1f : shown[i]);
        Turn(gear, gearRest, 90f * shown[4]);
        float spin = 0f;
        for (int s = 0; s < shown.Length; s++) spin += wheelPerStage * shown[s];
        Turn(wheel, wheelRest, spin + wheelExtra);
    }

    void Turn(Transform part, Quaternion rest, float degrees)
    {
        if (part == null) return;
        Vector3 axis = Quaternion.Inverse(part.parent.rotation * rest) * outWorld.normalized;
        part.localRotation = rest * Quaternion.AngleAxis(degrees, axis);
    }

    void SwingDoor(float degrees)
    {
        if (door == null) return;
        Vector3 axis = Quaternion.Inverse(door.parent.rotation * doorRest) * Vector3.up;
        door.localRotation = doorRest * Quaternion.AngleAxis(degrees * doorSwingSign, axis);
    }

    // The door must swing out toward the street, whichever way the model faces.
    float OutwardSwingSign()
    {
        if (door == null) return 1f;
        Vector3 center = Focus;
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
