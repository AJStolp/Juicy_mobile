using System.Collections.Generic;
using UnityEngine;

// Keeps a humanoid's feet centred and planted on a spot. The humanoid idle
// places the feet differently from the editor pose, and pushes shift them, so
// each frame the character eases back until its stance sits on the spot.
//
// On the first animated frame it measures, once, how far the foot bones sit
// above the real soles (the lowest vertices skinned to the feet; the tail
// hangs lower, so the whole-mesh minimum would be wrong). After that it plants
// by the foot bones alone, which costs nothing per frame.
// Runs before TorchGrip so the torch follows the corrected hand.
[DefaultExecutionOrder(-10)]
public class StandOnSpot : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] SkinnedMeshRenderer skin;
    [SerializeField] Vector3 spot;
    [Tooltip("Metres per second the stance eases back toward the spot.")]
    [SerializeField] float settleSpeed = 0.8f;

    Transform leftFoot;
    Transform rightFoot;
    readonly List<int> footVertices = new List<int>();
    float ankleHeight = float.NaN;

    public float AnkleHeight => ankleHeight;

    public void Configure(Animator body, SkinnedMeshRenderer mesh, Vector3 target)
    {
        animator = body;
        skin = mesh;
        spot = target;
    }

    void Start()
    {
        if (animator == null || !animator.isHuman || skin == null) return;
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        var feet = new HashSet<Transform> { leftFoot, rightFoot,
            animator.GetBoneTransform(HumanBodyBones.LeftToes), animator.GetBoneTransform(HumanBodyBones.RightToes) };
        Transform[] bones = skin.bones;
        BoneWeight[] weights = skin.sharedMesh.boneWeights;
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight w = weights[i];
            float onFeet = (feet.Contains(bones[w.boneIndex0]) ? w.weight0 : 0f) + (feet.Contains(bones[w.boneIndex1]) ? w.weight1 : 0f)
                         + (feet.Contains(bones[w.boneIndex2]) ? w.weight2 : 0f) + (feet.Contains(bones[w.boneIndex3]) ? w.weight3 : 0f);
            if (onFeet >= 0.5f) footVertices.Add(i);
        }
    }

    void LateUpdate()
    {
        if (leftFoot == null || rightFoot == null) return;
        float planted = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
        bool first = float.IsNaN(ankleHeight);
        if (first) ankleHeight = planted - MeasureSole(planted);

        Vector3 stance = (leftFoot.position + rightFoot.position) * 0.5f;
        Vector3 offset = new Vector3(spot.x - stance.x, spot.y + ankleHeight - planted, spot.z - stance.z);
        // Snap on the first frame so he never starts off the spot.
        Vector3 step = first ? offset : Vector3.MoveTowards(Vector3.zero, offset, settleSpeed * Time.deltaTime);
        transform.position += step;
    }

    float MeasureSole(float fallback)
    {
        if (footVertices.Count == 0) return fallback - 0.08f;
        var baked = new Mesh();
        skin.BakeMesh(baked, true);
        Vector3[] vertices = baked.vertices;
        float lowest = float.MaxValue;
        for (int i = 0; i < footVertices.Count; i++)
            lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertices[footVertices[i]]).y);
        Destroy(baked);
        return lowest;
    }
}
