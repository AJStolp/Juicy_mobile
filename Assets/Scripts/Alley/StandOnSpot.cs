using System.Collections.Generic;
using UnityEngine;

// Keeps a humanoid's feet planted on a spot and its tail out of the spot.
//
// Feet: each frame the character moves up or down so its lowest sole vertex
// (the vertices skinned to the feet and toes) sits exactly on the spot, and
// eases sideways until its stance is centred on it. The idle places the feet
// differently from the editor pose, and pushes and climbs shift them.
//
// Tail: the rig has no tail bones; the tail is skinned to the thighs, so the
// crouch and the reactions swing it down into the spot. On start the tail is
// rebound to a tail bone added under the hips. Each frame that bone is reset,
// and if the tail's lowest vertex would sit below the spot it swings up about
// its root until it clears.
// Runs before TorchGrip so the torch follows the corrected hand.
[DefaultExecutionOrder(-10)]
public class StandOnSpot : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] SkinnedMeshRenderer skin;
    [SerializeField] Vector3 spot;
    [Tooltip("Metres per second the stance eases sideways toward the spot.")]
    [SerializeField] float settleSpeed = 0.8f;

    // The tail is found in the bind pose as the vertices below the hips and
    // further behind them than this share of the hip height. The body has a
    // clear gap there; the thighs end before it and the tail starts after it.
    const float TailDepth = 0.22f;
    // Within this share of the hip height of the tail root, the tail's
    // skinning blends from the thighs to the tail bone, so its root does not
    // tear; further out it follows the tail bone alone.
    const float TailBlend = 0.3f;
    // How far above the spot the lifted tail's lowest point is put.
    const float TailClearance = 0.002f;
    const int LiftPasses = 6;

    Transform leftFoot;
    Transform rightFoot;
    Transform tail;
    Transform hipsBone;
    Vector3 hipsBack;
    Transform[] bones;
    Matrix4x4[] bindposes;
    Matrix4x4[] skinning;
    BoneWeight[] weights;
    Vector3[] bindVertices;
    readonly List<int> footVertices = new List<int>();
    readonly List<int> tailVertices = new List<int>();
    bool prepared;
    bool placed;

    public Vector3 Spot => spot;
    public SkinnedMeshRenderer Skin => skin;
    public IReadOnlyList<int> FootVertices => footVertices;
    public IReadOnlyList<int> TailVertices => tailVertices;

    public void Configure(Animator body, SkinnedMeshRenderer mesh, Vector3 target)
    {
        animator = body;
        skin = mesh;
        spot = target;
    }

    void Start() => Prepare();

    void LateUpdate() => Settle(Time.deltaTime);

    // Finds the foot and tail vertices and adds the tail bone. Safe to call
    // more than once.
    public void Prepare()
    {
        if (prepared || animator == null || !animator.isHuman || skin == null) return;
        prepared = true;
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        var feet = new HashSet<Transform> { leftFoot, rightFoot,
            animator.GetBoneTransform(HumanBodyBones.LeftToes), animator.GetBoneTransform(HumanBodyBones.RightToes) };
        Mesh mesh = skin.sharedMesh;
        bones = skin.bones;
        bindposes = mesh.bindposes;
        weights = mesh.boneWeights;
        bindVertices = mesh.vertices;
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight w = weights[i];
            float onFeet = (feet.Contains(bones[w.boneIndex0]) ? w.weight0 : 0f) + (feet.Contains(bones[w.boneIndex1]) ? w.weight1 : 0f)
                         + (feet.Contains(bones[w.boneIndex2]) ? w.weight2 : 0f) + (feet.Contains(bones[w.boneIndex3]) ? w.weight3 : 0f);
            if (onFeet >= 0.5f) footVertices.Add(i);
        }
        AddTailBone(mesh);
        skinning = new Matrix4x4[bones.Length];
    }

    void AddTailBone(Mesh mesh)
    {
        hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
        int hips = System.Array.IndexOf(bones, hipsBone);
        int foot = System.Array.IndexOf(bones, leftFoot);
        int toes = System.Array.IndexOf(bones, animator.GetBoneTransform(HumanBodyBones.LeftToes));
        if (hips < 0 || foot < 0 || toes < 0) return;
        Vector3 hipsBind = bindposes[hips].inverse.GetColumn(3);
        Vector3 footBind = bindposes[foot].inverse.GetColumn(3);
        Vector3 toesBind = bindposes[toes].inverse.GetColumn(3);
        Vector3 back = footBind - toesBind;
        back.y = 0f;
        back.Normalize();
        float hipHeight = hipsBind.y - toesBind.y;
        float depth = TailDepth * hipHeight;
        float blend = TailBlend * hipHeight;

        // The tail bone hangs off the hips where the tail starts.
        Vector3 root = hipsBind + back * depth;
        hipsBack = bindposes[hips].MultiplyVector(back);
        tail = new GameObject("Tail").transform;
        tail.SetParent(hipsBone, false);
        tail.localPosition = bindposes[hips].MultiplyPoint3x4(root);
        int tailIndex = bones.Length;

        var reweighted = (BoneWeight[])weights.Clone();
        for (int i = 0; i < bindVertices.Length; i++)
        {
            Vector3 v = bindVertices[i] - hipsBind;
            float behind = Vector3.Dot(v, back) - depth;
            if (v.y >= 0f || behind <= 0f) continue;
            tailVertices.Add(i);
            reweighted[i] = TowardBone(weights[i], tailIndex, Mathf.Clamp01((bindVertices[i] - root).magnitude / blend));
        }

        System.Array.Resize(ref bones, tailIndex + 1);
        bones[tailIndex] = tail;
        System.Array.Resize(ref bindposes, tailIndex + 1);
        bindposes[tailIndex] = Matrix4x4.Translate(-tail.localPosition) * bindposes[hips];
        weights = reweighted;

        Mesh rebound = Instantiate(mesh);
        rebound.name = mesh.name + " (tail bone)";
        rebound.bindposes = bindposes;
        rebound.boneWeights = weights;
        skin.sharedMesh = rebound;
        skin.bones = bones;
    }

    // Moves share of a vertex's skinning onto the bone, keeping the four
    // largest influences, heaviest first.
    static BoneWeight TowardBone(BoneWeight w, int bone, float share)
    {
        var influences = new List<KeyValuePair<int, float>>
        {
            new KeyValuePair<int, float>(bone, share),
            new KeyValuePair<int, float>(w.boneIndex0, w.weight0 * (1f - share)),
            new KeyValuePair<int, float>(w.boneIndex1, w.weight1 * (1f - share)),
            new KeyValuePair<int, float>(w.boneIndex2, w.weight2 * (1f - share)),
            new KeyValuePair<int, float>(w.boneIndex3, w.weight3 * (1f - share)),
        };
        influences.Sort((a, b) => b.Value.CompareTo(a.Value));
        float total = influences[0].Value + influences[1].Value + influences[2].Value + influences[3].Value;
        return new BoneWeight
        {
            boneIndex0 = influences[0].Key, weight0 = influences[0].Value / total,
            boneIndex1 = influences[1].Key, weight1 = influences[1].Value / total,
            boneIndex2 = influences[2].Key, weight2 = influences[2].Value / total,
            boneIndex3 = influences[3].Key, weight3 = influences[3].Value / total,
        };
    }

    public void Settle(float deltaTime)
    {
        if (leftFoot == null || rightFoot == null || footVertices.Count == 0) return;
        if (tail != null) tail.localRotation = Quaternion.identity;
        UpdateSkinning();
        float sole = Lowest(footVertices).y;

        Vector3 stance = (leftFoot.position + rightFoot.position) * 0.5f;
        Vector3 across = new Vector3(spot.x - stance.x, 0f, spot.z - stance.z);
        // Snap on the first frame so he never starts off the spot.
        if (placed) across = Vector3.MoveTowards(Vector3.zero, across, settleSpeed * deltaTime);
        placed = true;
        transform.position += across + Vector3.up * (spot.y - sole);

        if (tail != null) LiftTail();
    }

    // Swings the tail back and up about the hips' left and right axis until
    // its lowest point clears the spot.
    void LiftTail()
    {
        Vector3 back = hipsBone.TransformDirection(hipsBack);
        back.y = 0f;
        if (back.sqrMagnitude < 1e-8f) return;
        back.Normalize();
        float height = spot.y + TailClearance - tail.position.y;
        for (int pass = 0; pass < LiftPasses; pass++)
        {
            UpdateSkinning();
            Vector3 reach = Lowest(tailVertices) - tail.position;
            if (reach.y + tail.position.y >= spot.y) return;
            // The lowest point's reach in the vertical plane along the back,
            // turned up until it is level with the clearance height.
            Vector3 swing = back * Vector3.Dot(reach, back) + Vector3.up * reach.y;
            float length = swing.magnitude;
            float rise = Mathf.Min(height, length);
            Vector3 target = back * Mathf.Sqrt(length * length - rise * rise) + Vector3.up * rise;
            tail.rotation = Quaternion.FromToRotation(swing, target) * tail.rotation;
        }
    }

    void UpdateSkinning()
    {
        for (int b = 0; b < bones.Length; b++) skinning[b] = bones[b].localToWorldMatrix * bindposes[b];
    }

    // The lowest of these vertices in world space, skinned on the CPU.
    Vector3 Lowest(List<int> vertices)
    {
        Vector3 lowest = new Vector3(0f, float.MaxValue, 0f);
        for (int k = 0; k < vertices.Count; k++)
        {
            int i = vertices[k];
            BoneWeight w = weights[i];
            Vector3 v = bindVertices[i];
            Vector3 p = skinning[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
            if (w.weight1 > 0f) p += skinning[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
            if (w.weight2 > 0f) p += skinning[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
            if (w.weight3 > 0f) p += skinning[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
            if (p.y < lowest.y) lowest = p;
        }
        return lowest;
    }
}
