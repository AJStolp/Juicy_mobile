# Reshape the fal stride shells into a T pose, then bind a Unity humanoid skeleton.
# The source blends stay untouched. This does not write Robber_Rig.fbx.
# Authoring space is Blender Z up, face -Y. The FBX recipe turns that into Unity +Z.
import heapq
import math
import os

import bpy
from mathutils import Euler, Vector

ROOT = "/Users/astolp/Desktop/Dev/JuicySortGame"
PREVIEW = "/tmp/fal-preview"
TARGET_HEIGHT = 1.90
FORWARD = Vector((0.0, -1.0, 0.0))
UP = Vector((0.0, 0.0, 1.0))

JOBS = (
    {
        "key": "robber",
        "blend": ROOT + "/ArtSource/Heroes/FalClean/SM_Robber_Fal.blend",
        "mesh": "SM_Robber",
        "out_fbx": ROOT + "/Assets/Art/FalClean/SM_Robber_Fal.fbx",
        "out_blend": ROOT + "/ArtSource/Heroes/FalClean/SM_Robber_Fal_Humanoid.blend",
        "arm_name": "RobberArmature",
    },
    {
        "key": "hiker",
        "blend": ROOT + "/ArtSource/Heroes/FalClean/SM_Hiker_Fal.blend",
        "mesh": "SM_Hiker",
        "out_fbx": ROOT + "/Assets/Art/FalClean/SM_Hiker_Fal.fbx",
        "out_blend": ROOT + "/ArtSource/Heroes/FalClean/SM_Hiker_Fal_Humanoid.blend",
        "arm_name": "HikerArmature",
    },
)

PARENT = {
    "Spine": "Hips",
    "Chest": "Spine",
    "Neck": "Chest",
    "Head": "Neck",
    "LeftShoulder": "Chest",
    "LeftUpperArm": "LeftShoulder",
    "LeftLowerArm": "LeftUpperArm",
    "LeftHand": "LeftLowerArm",
    "RightShoulder": "Chest",
    "RightUpperArm": "RightShoulder",
    "RightLowerArm": "RightUpperArm",
    "RightHand": "RightLowerArm",
    "LeftUpperLeg": "Hips",
    "LeftLowerLeg": "LeftUpperLeg",
    "LeftFoot": "LeftLowerLeg",
    "LeftToes": "LeftFoot",
    "RightUpperLeg": "Hips",
    "RightLowerLeg": "RightUpperLeg",
    "RightFoot": "RightLowerLeg",
    "RightToes": "RightFoot",
}


def centroid(points):
    if not points:
        return None
    total = Vector()
    for point in points:
        total += point
    return total / len(points)


def segment_distance(point, start, end):
    span = end - start
    length_sq = span.length_squared
    if length_sq < 1e-8:
        return (point - start).length
    mix = max(0.0, min(1.0, (point - start).dot(span) / length_sq))
    return (point - (start + span * mix)).length


def select_points(verts, pred):
    return [point for point in verts if pred(point)]


def frame_of(head, tail, z_hint):
    direction = tail - head
    if direction.length < 1e-6:
        direction = Vector((0.0, 0.0, 0.08))
    y_axis = direction.normalized()
    z_axis = z_hint - y_axis * z_hint.dot(y_axis)
    if z_axis.length < 1e-5:
        fallback = UP if abs(y_axis.z) < 0.9 else FORWARD
        z_axis = fallback - y_axis * fallback.dot(y_axis)
    z_axis.normalize()
    x_axis = y_axis.cross(z_axis).normalized()
    z_axis = x_axis.cross(y_axis).normalized()
    return (Vector(head), x_axis, y_axis, z_axis)


def apply_frame(point, src, dst):
    offset = point - src[0]
    local = Vector((offset.dot(src[1]), offset.dot(src[2]), offset.dot(src[3])))
    return dst[0] + dst[1] * local.x + dst[2] * local.y + dst[3] * local.z


def neighbor_counts(verts, radius):
    counts = [0] * len(verts)
    radius_sq = radius * radius
    cell = radius
    buckets = {}
    for index, point in enumerate(verts):
        key = (int(math.floor(point.x / cell)), int(math.floor(point.y / cell)), int(math.floor(point.z / cell)))
        buckets.setdefault(key, []).append(index)
    for index, point in enumerate(verts):
        ix = int(math.floor(point.x / cell))
        iy = int(math.floor(point.y / cell))
        iz = int(math.floor(point.z / cell))
        count = 0
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for other in buckets.get((ix + dx, iy + dy, iz + dz), ()):
                        if other == index:
                            continue
                        delta = verts[other] - point
                        if delta.length_squared <= radius_sq:
                            count += 1
        counts[index] = count
    return counts


def elbow_point(shoulder, hand, bulge):
    # A bulge past the fist is the other limb or the sack, not this elbow.
    base = shoulder.lerp(hand, 0.46)
    if bulge is None:
        return base
    span = hand - shoulder
    length_sq = max(1e-8, span.length_squared)
    mix = (bulge - shoulder).dot(span) / length_sq
    if mix < 0.2 or mix > 0.75:
        return base
    if segment_distance(bulge, shoulder, hand) > 0.16:
        return base
    return base.lerp(bulge, 0.7)


def knee_bulge(verts, hip, ankle, side, x_limit=0.42):
    # The knee is the point that leaves the hip-ankle line, not the midpoint of a bent stride.
    best = None
    best_score = -1.0
    span = ankle - hip
    length_sq = max(1e-8, span.length_squared)
    z0 = min(hip.z, ankle.z) + 0.03
    z1 = max(hip.z, ankle.z) - 0.02
    for point in verts:
        if point.z < z0 or point.z > z1:
            continue
        if (point.x - hip.x) * side < -0.03:
            continue
        if abs(point.x) > x_limit:
            continue
        mix = (point - hip).dot(span) / length_sq
        if mix < 0.3 or mix > 0.72:
            continue
        dist = segment_distance(point, hip, ankle)
        if dist < 0.02 or dist > 0.32:
            continue
        score = dist * (1.0 - abs(mix - 0.5))
        if score > best_score:
            best_score = score
            best = point
    if best is None:
        return hip.lerp(ankle, 0.5)
    return best


def bend_center(verts, start, end, pred, radius):
    span = end - start
    length_sq = max(1e-8, span.length_squared)
    picked = []
    for point in verts:
        if not pred(point):
            continue
        mix = (point - start).dot(span) / length_sq
        if mix < 0.28 or mix > 0.72:
            continue
        if segment_distance(point, start, end) > radius:
            continue
        picked.append(point)
    center = centroid(picked)
    if center is None:
        return start.lerp(end, 0.5)
    return center


def chain_distance(point, joints):
    best = 1e9
    for start, end in zip(joints, joints[1:]):
        best = min(best, segment_distance(point, start, end))
    return best


def fit_landmarks(kind, verts, counts):
    front_foot = select_points(verts, lambda p: p.z < 0.22 and p.y < -0.18)
    back_foot = select_points(verts, lambda p: p.z < 0.22 and p.y > 0.18)
    if kind == "hiker":
        # +X is the character's left. The forward foot is the left foot.
        left_foot = front_foot
        right_foot = back_foot
    else:
        # The punching arm is -X, and that side leads.
        right_foot = front_foot
        left_foot = back_foot

    def ankle_of(points):
        ordered = sorted(points, key=lambda p: p.z)
        top = ordered[int(len(ordered) * 0.72):]
        return centroid(top)

    def toe_of(points):
        return min(points, key=lambda p: p.y + p.z * 0.15)

    hip_pts = select_points(verts, lambda p: 0.78 < p.z < 1.05 and abs(p.x) < 0.16 and abs(p.y) < 0.22)
    chest_pts = select_points(verts, lambda p: 1.22 < p.z < 1.48 and abs(p.x) < 0.18 and p.y < 0.18)
    neck_pts = select_points(verts, lambda p: 1.48 < p.z < 1.66 and abs(p.x) < 0.14 and p.y < 0.12)
    head_pts = select_points(verts, lambda p: p.z > 1.7 and abs(p.x) < 0.28 and p.y < 0.2)
    hips = centroid(hip_pts)
    chest = centroid(chest_pts)
    neck = centroid(neck_pts) or (chest + Vector((0.0, -0.02, 0.16)))
    head = centroid(head_pts) or (neck + Vector((0.0, -0.04, 0.2)))

    def side_arm(side):
        # side +1 is character left (+X). Keep the search off the sack and the staff tip.
        if side > 0:
            pred = lambda p: p.x > 0.1 and 0.75 < p.z < 1.5 and p.y < 0.32
        else:
            pred = lambda p: p.x < -0.1 and 0.6 < p.z < 1.45 and p.y < 0.15
        pool = select_points(verts, pred)
        if not pool:
            return chest + Vector((side * 0.35, -0.1, -0.1))
        return max(pool, key=lambda p: abs(p.x) + max(0.0, -p.y) * 0.35)

    left_hand = side_arm(1.0)
    right_hand = side_arm(-1.0)
    if kind == "hiker":
        # The free hand is the outer +X skin mass. The staff hand stays on -X.
        left_pool = select_points(verts, lambda p: p.x > 0.18 and 0.7 < p.z < 1.2)
        right_pool = select_points(verts, lambda p: p.x < -0.12 and 0.85 < p.z < 1.35 and p.y < 0.2)
        if left_pool:
            left_hand = centroid(left_pool)
        if right_pool:
            right_hand = centroid(right_pool)
    elif kind == "robber":
        # The outermost +X point is the sack, not the fist. The grip sits on the front of the sack.
        grip = select_points(verts, lambda p: 0.22 < p.x < 0.52 and 0.95 < p.z < 1.35 and p.y < 0.18)
        if grip:
            left_hand = centroid(grip)

    left_shoulder = Vector((chest.x + 0.16, chest.y - 0.02, chest.z + 0.02))
    right_shoulder = Vector((chest.x - 0.16, chest.y - 0.02, chest.z + 0.02))
    left_elbow = elbow_point(left_shoulder, left_hand, knee_bulge(verts, left_shoulder, left_hand, 1.0, 0.8))
    right_elbow = elbow_point(right_shoulder, right_hand, knee_bulge(verts, right_shoulder, right_hand, -1.0, 0.8))

    def leg_joints(foot_points, ankle, toe, side):
        hip = Vector((hips.x + side * 0.08, hips.y, hips.z - 0.02))
        knee = knee_bulge(verts, hip, ankle, side)
        return hip, knee, ankle, toe

    left_ankle = ankle_of(left_foot)
    right_ankle = ankle_of(right_foot)
    left_leg = leg_joints(left_foot, left_ankle, toe_of(left_foot), 1.0)
    right_leg = leg_joints(right_foot, right_ankle, toe_of(right_foot), -1.0)

    marks = {
        "hips": hips,
        "chest": chest,
        "neck": neck,
        "head": head + Vector((0.0, -0.02, 0.06)),
        "left_leg": left_leg,
        "right_leg": right_leg,
        "left_arm": (left_shoulder, left_elbow, left_hand),
        "right_arm": (right_shoulder, right_elbow, right_hand),
    }
    for name, point in (
        ("hips", hips),
        ("chest", chest),
        ("neck", neck),
        ("head", marks["head"]),
        ("L_hand", left_hand),
        ("R_hand", right_hand),
        ("L_elbow", left_elbow),
        ("R_elbow", right_elbow),
        ("L_ankle", left_ankle),
        ("R_ankle", right_ankle),
    ):
        print(f"  {name:8} ({point.x:+.3f},{point.y:+.3f},{point.z:+.3f})")
    return marks


def bone_length(start, end, minimum):
    return max(minimum, (end - start).length)


def build_targets(marks):
    hips = marks["hips"]
    chest = marks["chest"]
    neck = marks["neck"]
    head = marks["head"]
    left_hip, left_knee, left_ankle, left_toe = marks["left_leg"]
    right_hip, right_knee, right_ankle, right_toe = marks["right_leg"]
    left_shoulder, left_elbow, left_hand = marks["left_arm"]
    right_shoulder, right_elbow, right_hand = marks["right_arm"]

    thigh_l = bone_length(left_hip, left_knee, 0.28)
    shin_l = bone_length(left_knee, left_ankle, 0.26)
    thigh_r = bone_length(right_hip, right_knee, 0.28)
    shin_r = bone_length(right_knee, right_ankle, 0.26)
    foot_l = max(0.1, (left_toe - left_ankle).length)
    foot_r = max(0.1, (right_toe - right_ankle).length)
    upper_l = bone_length(left_shoulder, left_elbow, 0.18)
    lower_l = bone_length(left_elbow, left_hand, 0.16)
    upper_r = bone_length(right_shoulder, right_elbow, 0.18)
    lower_r = bone_length(right_shoulder, right_elbow, 0.18)
    lower_r = bone_length(right_elbow, right_hand, 0.16)
    spine_l = bone_length(hips, chest, 0.16) * 0.55
    chest_l = max(0.14, (neck - chest).length)
    neck_l = max(0.06, (head - neck).length * 0.45)
    head_l = max(0.16, (head - neck).length * 0.7)

    ankle_z = 0.1
    hip_z = ankle_z + max(thigh_l + shin_l, thigh_r + shin_r)
    # Keep the two legs the same length so the hips stay level.
    leg = hip_z - ankle_z
    thigh = leg * 0.54
    shin = leg - thigh
    stance = 0.11
    chest_z = hip_z + spine_l + chest_l
    shoulder_z = chest_z + 0.02

    def leg(side, foot_len):
        x = stance * side
        hip = Vector((x, 0.0, hip_z))
        knee = Vector((x, 0.0, hip_z - thigh))
        ankle = Vector((x, 0.0, ankle_z))
        ball = Vector((x, -foot_len * 0.72, ankle_z - 0.015))
        toe = Vector((x, -foot_len, ankle_z - 0.03))
        return hip, knee, ankle, ball, toe

    left = leg(1.0, min(foot_l, 0.22))
    right = leg(-1.0, min(foot_r, 0.22))
    hip_head = Vector((0.0, 0.0, hip_z))
    spine_head = Vector((0.0, -0.01, hip_z + 0.02))
    spine_tail = Vector((0.0, -0.02, hip_z + spine_l))
    chest_tail = Vector((0.0, -0.03, chest_z))
    neck_tail = Vector((0.0, -0.035, chest_z + neck_l))
    head_tail = Vector((0.0, -0.05, neck_tail.z + head_l))

    def arm(side, upper, lower):
        root = Vector((0.05 * side, -0.03, shoulder_z))
        shoulder = Vector((0.16 * side, -0.03, shoulder_z))
        elbow = shoulder + Vector((upper * side, 0.0, 0.0))
        wrist = elbow + Vector((lower * 0.82 * side, 0.0, 0.0))
        hand = elbow + Vector((lower * side, 0.0, 0.0))
        return root, shoulder, elbow, wrist, hand

    left_arm = arm(1.0, min(upper_l, 0.32), min(lower_l, 0.3))
    right_arm = arm(-1.0, min(upper_r, 0.32), min(lower_r, 0.3))
    print(
        "  lengths thigh {:.3f} shin {:.3f} upperL {:.3f} lowerL {:.3f} hip_z {:.3f}".format(
            thigh, shin, upper_l, lower_l, hip_z
        )
    )
    return {
        "current": {
            "Hips": (Vector((hips.x, hips.y, hips.z - 0.04)), Vector((hips.x, hips.y, hips.z + spine_l * 0.35))),
            "Spine": (Vector((hips.x, hips.y, hips.z)), chest),
            "Chest": (chest, neck),
            "Neck": (neck, neck.lerp(head, 0.45)),
            "Head": (neck.lerp(head, 0.35), head),
            "LeftUpperArm": (left_shoulder, left_elbow),
            "LeftLowerArm": (left_elbow, left_elbow.lerp(left_hand, 0.78)),
            "LeftHand": (left_elbow.lerp(left_hand, 0.72), left_hand),
            "RightUpperArm": (right_shoulder, right_elbow),
            "RightLowerArm": (right_elbow, right_elbow.lerp(right_hand, 0.78)),
            "RightHand": (right_elbow.lerp(right_hand, 0.72), right_hand),
            "LeftUpperLeg": (left_hip, left_knee),
            "LeftLowerLeg": (left_knee, left_ankle),
            "LeftFoot": (left_ankle, left_ankle.lerp(left_toe, 0.7)),
            "LeftToes": (left_ankle.lerp(left_toe, 0.62), left_toe),
            "RightUpperLeg": (right_hip, right_knee),
            "RightLowerLeg": (right_knee, right_ankle),
            "RightFoot": (right_ankle, right_ankle.lerp(right_toe, 0.7)),
            "RightToes": (right_ankle.lerp(right_toe, 0.62), right_toe),
        },
        "target": {
            "Hips": (hip_head, spine_head),
            "Spine": (spine_head, spine_tail),
            "Chest": (spine_tail, chest_tail),
            "Neck": (chest_tail, neck_tail),
            "Head": (neck_tail, head_tail),
            "LeftShoulder": (left_arm[0], left_arm[1]),
            "LeftUpperArm": (left_arm[1], left_arm[2]),
            "LeftLowerArm": (left_arm[2], left_arm[3]),
            "LeftHand": (left_arm[3], left_arm[4]),
            "RightShoulder": (right_arm[0], right_arm[1]),
            "RightUpperArm": (right_arm[1], right_arm[2]),
            "RightLowerArm": (right_arm[2], right_arm[3]),
            "RightHand": (right_arm[3], right_arm[4]),
            "LeftUpperLeg": (left[0], left[1]),
            "LeftLowerLeg": (left[1], left[2]),
            "LeftFoot": (left[2], left[3]),
            "LeftToes": (left[3], left[4]),
            "RightUpperLeg": (right[0], right[1]),
            "RightLowerLeg": (right[1], right[2]),
            "RightFoot": (right[2], right[3]),
            "RightToes": (right[3], right[4]),
        },
    }


def mesh_adj(mesh):
    adj = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        weight = (mesh.vertices[a].co - mesh.vertices[b].co).length
        adj[a].append((b, weight))
        adj[b].append((a, weight))
    return adj


def nearest_vertex(verts, point):
    best = 0
    best_d = 1e9
    for index, co in enumerate(verts):
        dist = (co - point).length_squared
        if dist < best_d:
            best_d = dist
            best = index
    return best


def multi_source(adj, seeds):
    dist = [1e9] * len(adj)
    label = [None] * len(adj)
    heap = []
    for name, index in seeds.items():
        dist[index] = 0.0
        label[index] = name
        heapq.heappush(heap, (0.0, index))
    while heap:
        current, index = heapq.heappop(heap)
        if current != dist[index]:
            continue
        name = label[index]
        for other, weight in adj[index]:
            nxt = current + weight
            if nxt < dist[other]:
                dist[other] = nxt
                label[other] = name
                heapq.heappush(heap, (nxt, other))
    return label


def dijkstra_subset(adj, sources, allowed):
    dist = {index: 1e9 for index in allowed}
    heap = []
    for source in sources:
        if source not in dist:
            continue
        dist[source] = 0.0
        heapq.heappush(heap, (0.0, source))
    while heap:
        current, index = heapq.heappop(heap)
        if current != dist[index]:
            continue
        for other, weight in adj[index]:
            if other not in dist:
                continue
            nxt = current + weight
            if nxt < dist[other]:
                dist[other] = nxt
                heapq.heappush(heap, (nxt, other))
    return dist


def split_chain(adj, labels, region, tip, root_names, bone_names, cuts):
    members = [index for index, name in enumerate(labels) if name == region]
    member_set = set(members)
    if not members:
        return {name: [] for name in bone_names}
    boundary = []
    for index in members:
        for other, _ in adj[index]:
            if labels[other] in root_names:
                boundary.append(index)
                break
    if not boundary:
        boundary = [min(members, key=lambda index: sum((1 for _ in adj[index])))]
    from_root = dijkstra_subset(adj, boundary, member_set)
    from_tip = dijkstra_subset(adj, [tip], member_set)
    buckets = {name: [] for name in bone_names}
    for index in members:
        root_d = from_root.get(index, 0.0)
        tip_d = from_tip.get(index, 0.0)
        frac = root_d / max(1e-6, root_d + tip_d)
        if frac < cuts[0]:
            buckets[bone_names[0]].append(index)
        elif frac < cuts[1]:
            buckets[bone_names[1]].append(index)
        else:
            buckets[bone_names[2]].append(index)
    return buckets


def principal_axis(points, center):
    xx = xy = xz = yy = yz = zz = 0.0
    for point in points:
        delta = point - center
        xx += delta.x * delta.x
        xy += delta.x * delta.y
        xz += delta.x * delta.z
        yy += delta.y * delta.y
        yz += delta.y * delta.z
        zz += delta.z * delta.z
    axis = Vector((0.0, 0.0, 1.0))
    for _ in range(14):
        nxt = Vector((
            xx * axis.x + xy * axis.y + xz * axis.z,
            xy * axis.x + yy * axis.y + yz * axis.z,
            xz * axis.x + yz * axis.y + zz * axis.z,
        ))
        if nxt.length < 1e-8:
            return UP.copy()
        axis = nxt.normalized()
    return axis


def region_ends(coords, indices, root_point):
    points = [coords[index] for index in indices]
    center = centroid(points)
    axis = principal_axis(points, center)
    projections = [((coords[index] - center).dot(axis), index) for index in indices]
    projections.sort()
    count = max(1, len(projections) // 7)
    low = centroid([coords[index] for _, index in projections[:count]])
    high = centroid([coords[index] for _, index in projections[-count:]])
    if (high - root_point).length < (low - root_point).length:
        head, tail = high, low
    else:
        head, tail = low, high
    if (tail - head).length < 0.05:
        tail = head + axis * 0.08
    return head, tail


BLEND_PAIRS = {
    frozenset(("Hips", "Spine")),
    frozenset(("Spine", "Chest")),
    frozenset(("Chest", "Head")),
    frozenset(("Chest", "LeftUpperArm")),
    frozenset(("Chest", "RightUpperArm")),
    frozenset(("Hips", "LeftUpperLeg")),
    frozenset(("Hips", "RightUpperLeg")),
    frozenset(("LeftUpperLeg", "LeftLowerLeg")),
    frozenset(("LeftLowerLeg", "LeftFoot")),
    frozenset(("RightUpperLeg", "RightLowerLeg")),
    frozenset(("RightLowerLeg", "RightFoot")),
    frozenset(("LeftUpperArm", "LeftLowerArm")),
    frozenset(("LeftLowerArm", "LeftHand")),
    frozenset(("RightUpperArm", "RightLowerArm")),
    frozenset(("RightLowerArm", "RightHand")),
    frozenset(("LeftHand", "Sack")),
    frozenset(("RightHand", "Staff")),
}


def segment_surface(kind, mesh, verts, counts, marks):
    adj = mesh_adj(mesh)
    seeds = {
        "Head": nearest_vertex(verts, marks["head"]),
        "Chest": nearest_vertex(verts, marks["chest"]),
        "Hips": nearest_vertex(verts, marks["hips"]),
        "LeftHand": nearest_vertex(verts, marks["left_arm"][2]),
        "RightHand": nearest_vertex(verts, marks["right_arm"][2]),
        "LeftFoot": nearest_vertex(verts, marks["left_leg"][2]),
        "RightFoot": nearest_vertex(verts, marks["right_leg"][2]),
    }
    if kind == "robber":
        sack_pool = [index for index, point in enumerate(verts) if point.z > 0.6 and point.y > 0.15 and point.x > 0.0]
        if sack_pool:
            seeds["Sack"] = max(sack_pool, key=lambda index: verts[index].y)
    else:
        staff_pool = [
            index for index, point in enumerate(verts)
            if counts[index] <= 7 and 0.28 < point.z < 0.95 and abs(point.x) < 0.22 and point.y < 0.1
        ]
        if staff_pool:
            seeds["Staff"] = min(staff_pool, key=lambda index: abs(verts[index].y + 0.2) + abs(verts[index].z - 0.55))
    labels = multi_source(adj, seeds)
    # A prop flood that reaches the torso gets handed back to the nearest body region.
    # The stick itself stays, including a tip that touches the ground. Fat boot verts do not.
    if kind == "hiker":
        staff_ids = [index for index, name in enumerate(labels) if name == "Staff"]
        thin = [index for index in staff_ids if counts[index] <= 8]
        if len(thin) >= 6:
            center = centroid([verts[index] for index in thin])
            axis = principal_axis([verts[index] for index in thin], center)
            for index in staff_ids:
                rel = verts[index] - center
                along = rel.dot(axis)
                radial = (rel - axis * along).length
                if radial > 0.075 or (verts[index].z < 0.24 and counts[index] > 8):
                    labels[index] = None
    if kind == "robber":
        for index, name in enumerate(labels):
            if name != "Sack":
                continue
            point = verts[index]
            if abs(point.x) < 0.09 and point.y < 0.02 and 0.75 < point.z < 1.62:
                labels[index] = None
    pending = [index for index, name in enumerate(labels) if name is None]
    for _ in range(40):
        if not pending:
            break
        nxt = []
        for index in pending:
            picked = None
            for other, _ in adj[index]:
                other_name = labels[other]
                if other_name not in (None, "Sack", "Staff"):
                    picked = other_name
                    break
            if picked is None:
                nxt.append(index)
            else:
                labels[index] = picked
        if len(nxt) == len(pending):
            for index in pending:
                for other, _ in adj[index]:
                    if labels[other] is not None:
                        labels[index] = labels[other]
                        break
                if labels[index] is None:
                    labels[index] = "Chest"
            break
        pending = nxt
    buckets = {
        "Head": [index for index, name in enumerate(labels) if name == "Head"],
        "Chest": [],
        "Spine": [],
        "Hips": [index for index, name in enumerate(labels) if name == "Hips"],
        "Sack": [index for index, name in enumerate(labels) if name == "Sack"],
        "Staff": [index for index, name in enumerate(labels) if name == "Staff"],
    }
    chest_members = [index for index, name in enumerate(labels) if name == "Chest"]
    if chest_members:
        mid = sorted(verts[index].z for index in chest_members)[len(chest_members) // 2]
        for index in chest_members:
            buckets["Spine" if verts[index].z < mid else "Chest"].append(index)
    leg_cuts = (0.40, 0.74)
    arm_cuts = (0.46, 0.74)
    body_names = {"Hips", "Chest", "Spine"}
    buckets.update(split_chain(adj, labels, "LeftFoot", seeds["LeftFoot"], body_names, ["LeftUpperLeg", "LeftLowerLeg", "LeftFoot"], leg_cuts))
    buckets.update(split_chain(adj, labels, "RightFoot", seeds["RightFoot"], body_names, ["RightUpperLeg", "RightLowerLeg", "RightFoot"], leg_cuts))
    buckets.update(split_chain(adj, labels, "LeftHand", seeds["LeftHand"], body_names, ["LeftUpperArm", "LeftLowerArm", "LeftHand"], arm_cuts))
    buckets.update(split_chain(adj, labels, "RightHand", seeds["RightHand"], body_names, ["RightUpperArm", "RightLowerArm", "RightHand"], arm_cuts))
    counts_out = {name: len(indices) for name, indices in buckets.items()}
    print("  regions", counts_out)
    return adj, buckets


def frames_from_regions(coords, buckets, marks):
    roots = {
        "Hips": marks["hips"],
        "Spine": marks["hips"],
        "Chest": marks["hips"],
        "Head": marks["chest"],
        "LeftUpperArm": marks["chest"],
        "LeftLowerArm": marks["left_arm"][0],
        "LeftHand": marks["left_arm"][1],
        "RightUpperArm": marks["chest"],
        "RightLowerArm": marks["right_arm"][0],
        "RightHand": marks["right_arm"][1],
        "LeftUpperLeg": marks["hips"],
        "LeftLowerLeg": marks["left_leg"][0],
        "LeftFoot": marks["left_leg"][1],
        "RightUpperLeg": marks["hips"],
        "RightLowerLeg": marks["right_leg"][0],
        "RightFoot": marks["right_leg"][1],
    }
    frames = {}
    lengths = {}
    for name, root in roots.items():
        indices = buckets.get(name, [])
        if len(indices) < 8:
            print("  thin region", name, len(indices))
            continue
        head, tail = region_ends(coords, indices, root)
        hint = UP if "Foot" in name else FORWARD
        frames[name] = frame_of(head, tail, hint)
        lengths[name] = max(0.05, (tail - head).length)
    # Torso blobs are wider than they are tall, so a long-axis fit turns the chest sideways.
    # Landmark lines keep the belly in front while the destination pose stands the spine up.
    frames["Hips"] = frame_of(marks["hips"], marks["hips"] + UP * 0.16, FORWARD)
    lengths["Hips"] = 0.16
    frames["Spine"] = frame_of(marks["hips"], marks["chest"], FORWARD)
    lengths["Spine"] = max(0.12, (marks["chest"] - marks["hips"]).length)
    frames["Chest"] = frame_of(marks["chest"], marks["neck"], FORWARD)
    lengths["Chest"] = max(0.1, (marks["neck"] - marks["chest"]).length)
    frames["Head"] = frame_of(marks["neck"], marks["head"], FORWARD)
    lengths["Head"] = max(0.14, (marks["head"] - marks["neck"]).length)
    return frames, lengths


def targets_from_lengths(lengths):
    def length(name, default):
        return lengths.get(name, default)

    thigh_l = length("LeftUpperLeg", 0.42)
    thigh_r = length("RightUpperLeg", 0.42)
    shin_l = length("LeftLowerLeg", 0.38)
    shin_r = length("RightLowerLeg", 0.38)
    foot_l = max(length("LeftFoot", 0.14), 0.1)
    foot_r = max(length("RightFoot", 0.14), 0.1)
    upper_l = length("LeftUpperArm", 0.26)
    upper_r = length("RightUpperArm", 0.26)
    lower_l = length("LeftLowerArm", 0.22)
    lower_r = length("RightLowerArm", 0.22)
    hand_l = max(length("LeftHand", 0.08), 0.06)
    hand_r = max(length("RightHand", 0.08), 0.06)
    spine = length("Spine", 0.18)
    chest = length("Chest", 0.2)
    head = length("Head", 0.22)
    # Hips stay level. Each leg keeps its own length, so the shorter foot may sit slightly high.
    hip_z = 0.06 + max(thigh_l + shin_l, thigh_r + shin_r)
    stance = 0.1
    # Spine, chest, and head lengths are the landmark distances the warp uses.
    # The bones meet at those landmarks so the torso does not pull apart.
    hip_head = Vector((0.0, 0.0, hip_z))
    hip_tail = Vector((0.0, 0.0, hip_z + 0.1))
    spine_tail = Vector((0.0, 0.0, hip_z + spine))
    chest_tail = Vector((0.0, 0.0, spine_tail.z + chest))
    neck_tail = Vector((0.0, 0.0, chest_tail.z + 0.06))
    head_tail = Vector((0.0, 0.0, chest_tail.z + head))
    shoulder_z = chest_tail.z - 0.04

    def leg(side, thigh, shin, foot):
        x = stance * side
        hip = Vector((x, 0.0, hip_z))
        knee = Vector((x, 0.0, hip_z - thigh))
        ankle = Vector((x, 0.0, hip_z - thigh - shin))
        ball = Vector((x, -foot * 0.65, 0.035))
        toe = Vector((x, -foot, 0.02))
        return hip, knee, ankle, ball, toe

    def arm(side, upper, lower, hand):
        # Mild A pose. Fully horizontal arms tear the coat and the sack shoulder.
        drop = 0.62
        direction = Vector((math.cos(drop) * side, 0.0, -math.sin(drop)))
        root = Vector((0.04 * side, 0.0, shoulder_z))
        shoulder = Vector((0.14 * side, 0.0, shoulder_z))
        elbow = shoulder + direction * upper
        wrist = elbow + direction * lower
        tip = wrist + direction * hand
        return root, shoulder, elbow, wrist, tip

    left = leg(1.0, thigh_l, shin_l, foot_l)
    right = leg(-1.0, thigh_r, shin_r, foot_r)
    left_arm = arm(1.0, upper_l, lower_l, hand_l)
    right_arm = arm(-1.0, upper_r, lower_r, hand_r)
    joints = {
        "Hips": (hip_head, hip_tail),
        "Spine": (hip_head, spine_tail),
        "Chest": (spine_tail, chest_tail),
        "Neck": (chest_tail, neck_tail),
        "Head": (chest_tail, head_tail),
        "LeftShoulder": (left_arm[0], left_arm[1]),
        "LeftUpperArm": (left_arm[1], left_arm[2]),
        "LeftLowerArm": (left_arm[2], left_arm[3]),
        "LeftHand": (left_arm[3], left_arm[4]),
        "RightShoulder": (right_arm[0], right_arm[1]),
        "RightUpperArm": (right_arm[1], right_arm[2]),
        "RightLowerArm": (right_arm[2], right_arm[3]),
        "RightHand": (right_arm[3], right_arm[4]),
        "LeftUpperLeg": (left[0], left[1]),
        "LeftLowerLeg": (left[1], left[2]),
        "LeftFoot": (left[2], left[3]),
        "LeftToes": (left[3], left[4]),
        "RightUpperLeg": (right[0], right[1]),
        "RightLowerLeg": (right[1], right[2]),
        "RightFoot": (right[2], right[3]),
        "RightToes": (right[3], right[4]),
    }
    print(
        "  tpose thigh {:.3f}/{:.3f} shin {:.3f}/{:.3f} upper {:.3f}/{:.3f} hip_z {:.3f}".format(
            thigh_l, thigh_r, shin_l, shin_r, upper_l, upper_r, hip_z
        )
    )
    return joints


def target_frames(joints):
    frames = {}
    for name in (
        "Hips", "Spine", "Chest", "Head",
        "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightUpperArm", "RightLowerArm", "RightHand",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
        "RightUpperLeg", "RightLowerLeg", "RightFoot",
    ):
        hint = UP if "Foot" in name else FORWARD
        frames[name] = frame_of(joints[name][0], joints[name][1], hint)
    frames["Sack"] = frames["LeftHand"]
    frames["Staff"] = frames["RightHand"]
    return frames


def warp_regions(coords, adj, buckets, sources, targets):
    owner = [None] * len(coords)
    for name, indices in buckets.items():
        if name not in sources and name not in ("Sack", "Staff"):
            continue
        for index in indices:
            owner[index] = name
    for index, name in enumerate(owner):
        if name is not None:
            continue
        for other, _ in adj[index]:
            if owner[other] is not None:
                owner[index] = owner[other]
                break
        if owner[index] is None:
            owner[index] = "Chest"

    def moved_by(index, name):
        src_name = name if name in sources else "LeftHand" if name == "Sack" else "RightHand" if name == "Staff" else "Chest"
        if src_name not in sources:
            src_name = "Chest"
        return apply_frame(coords[index], sources[src_name], targets[src_name if src_name in targets else "Chest"])

    moved = []
    for index, point in enumerate(coords):
        name = owner[index]
        result = moved_by(index, name)
        extra = []
        for other, _ in adj[index]:
            other_name = owner[other]
            if other_name == name:
                continue
            if frozenset((name, other_name)) not in BLEND_PAIRS:
                continue
            extra.append(moved_by(index, other_name))
        if extra:
            total = result * 2.0
            for item in extra:
                total += item
            result = total / (2.0 + len(extra))
        moved.append(result)
    return moved, buckets.get("Sack", []), buckets.get("Staff", [])


def hang_prop(coords, indices, pivot, aim=None):
    if len(indices) < 12:
        return
    center = centroid([coords[index] for index in indices])
    offset = center - pivot
    if offset.length < 1e-4:
        return
    if aim is None:
        outward = offset.x
        aim = Vector((math.copysign(max(0.05, abs(outward) * 0.35), outward), -0.04, -offset.length))
    if aim.length < 1e-4:
        return
    rotation = offset.normalized().rotation_difference(aim.normalized())
    for index in indices:
        coords[index] = pivot + rotation @ (coords[index] - pivot)


def plant_and_scale(coords, joints):
    min_z = min(point.z for point in coords)
    core = [point for point in coords if 0.35 < point.z - min_z < 1.35 and abs(point.x) < 0.28]
    core_point = centroid(core) or centroid(coords)
    shift = Vector((-core_point.x, -core_point.y, -min_z))
    shifted = [point + shift for point in coords]
    height = max(point.z for point in shifted)
    scale = TARGET_HEIGHT / max(height, 1e-4)
    scaled = [point * scale for point in shifted]
    scaled_joints = {}
    for name, pair in joints.items():
        scaled_joints[name] = ((pair[0] + shift) * scale, (pair[1] + shift) * scale)
    size = Vector((
        max(p.x for p in scaled) - min(p.x for p in scaled),
        max(p.y for p in scaled) - min(p.y for p in scaled),
        max(p.z for p in scaled) - min(p.z for p in scaled),
    ))
    print(f"  planted height {height:.3f} scale {scale:.3f} size ({size.x:.3f},{size.y:.3f},{size.z:.3f})")
    return scaled, scaled_joints


def stretch_report(mesh, before):
    ratios = []
    worst = 0.0
    for edge in mesh.edges:
        a, b = edge.vertices
        old = (before[a] - before[b]).length
        new = (mesh.vertices[a].co - mesh.vertices[b].co).length
        if old < 1e-6:
            continue
        ratio = new / old
        ratios.append(ratio)
        worst = max(worst, ratio)
    ratios.sort()
    mid = ratios[len(ratios) // 2] if ratios else 0.0
    over = sum(1 for ratio in ratios if ratio > 1.75)
    print(f"  stretch median {mid:.3f} worst {worst:.3f} edges_over_1.75 {over}")
    return over


def add_bone(arm, name, head, tail, parent, z_hint):
    bone = arm.data.edit_bones.new(name)
    bone.head = Vector(head)
    bone.tail = Vector(tail)
    if (bone.tail - bone.head).length < 0.04:
        bone.tail = bone.head + (bone.tail - bone.head).normalized() * 0.05
    if parent:
        bone.parent = arm.data.edit_bones[parent]
        bone.use_connect = False
    best_roll = 0.0
    best_score = -2.0
    for step in range(72):
        bone.roll = step * math.pi / 36.0
        score = bone.z_axis.dot(z_hint)
        if score > best_score:
            best_score = score
            best_roll = bone.roll
    bone.roll = best_roll
    return bone


def build_armature(name, joints):
    arm_data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    order = [
        "Hips", "Spine", "Chest", "Neck", "Head",
        "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
        "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
    ]
    for bone_name in order:
        head, tail = joints[bone_name]
        hint = UP if "Foot" in bone_name or "Toes" in bone_name else FORWARD
        add_bone(arm, bone_name, head, tail, PARENT.get(bone_name), hint)
    bpy.ops.object.mode_set(mode="OBJECT")
    print("  bones", len(arm.data.bones))
    return arm


def bind_mesh(body, arm, sack, staff, pack):
    body.parent = None
    body.vertex_groups.clear()
    for modifier in list(body.modifiers):
        body.modifiers.remove(modifier)
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    try:
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
        print("  weights auto")
    except RuntimeError as exc:
        print("  weights auto failed", exc)
        distance_bind(body, arm)
    force_group(body, sack, "LeftHand")
    force_group(body, staff, "RightHand")
    force_group(body, pack, "Chest")
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="OBJECT")
    body.select_set(True)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL")
    max_influences = 0
    mesh = body.data
    for vertex in mesh.vertices:
        used = sum(1 for group in vertex.groups if group.weight > 0.001)
        max_influences = max(max_influences, used)
    print("  max influences", max_influences)


def distance_bind(body, arm):
    groups = {}
    for bone in arm.data.bones:
        groups[bone.name] = body.vertex_groups.new(name=bone.name)
    bones = [(bone.name, bone.head_local.copy(), bone.tail_local.copy()) for bone in arm.data.bones]
    for vertex in body.data.vertices:
        scored = []
        for name, head, tail in bones:
            dist = segment_distance(vertex.co, head, tail)
            influence = max(0.0, 1.0 - dist / 0.28)
            if influence > 0.0:
                scored.append((influence * influence, name))
        if not scored:
            nearest = min(bones, key=lambda item: segment_distance(vertex.co, item[1], item[2]))
            scored = [(1.0, nearest[0])]
        scored.sort(reverse=True)
        scored = scored[:4]
        total = sum(weight for weight, _ in scored)
        for weight, name in scored:
            groups[name].add([vertex.index], weight / total, "REPLACE")
    modifier = body.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm
    body.parent = arm


def force_group(body, indices, name):
    if not indices:
        return
    group = body.vertex_groups.get(name)
    if group is None:
        group = body.vertex_groups.new(name=name)
    others = [item for item in body.vertex_groups if item.name != name]
    for index in indices:
        for other in others:
            other.remove([index])
        group.add([index], 1.0, "REPLACE")


def assign_action(arm, name, start, end):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = action
    if hasattr(action, "slots") and len(action.slots) == 0:
        slot = action.slots.new(arm.id_type, name)
        if hasattr(arm.animation_data, "action_slot"):
            arm.animation_data.action_slot = slot
    elif hasattr(arm.animation_data, "action_slot") and len(action.slots) > 0:
        arm.animation_data.action_slot = action.slots[0]
    try:
        action.use_frame_range = True
        action.frame_start = start
        action.frame_end = end
    except AttributeError:
        pass
    return action


def key_pose(arm, frame, rotations, locations):
    bpy.context.view_layer.objects.active = arm
    if arm.mode != "POSE":
        bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    for bone in arm.pose.bones:
        bone.rotation_mode = "XYZ"
        if bone.name in rotations:
            bone.rotation_euler = rotations[bone.name]
        if bone.name in locations:
            bone.location = locations[bone.name]
        bone.keyframe_insert("rotation_euler", frame=frame)
        bone.keyframe_insert("location", frame=frame)


def animate(arm):
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    scene = bpy.context.scene
    scene.render.fps = 30

    def level(thigh, shin, pitch):
        return -(thigh + shin) * 0.85 + pitch

    # +X swings a limb toward -Y (forward). Knee flexion is -X.
    # frame, root distance, L thigh, L knee, R thigh, R knee, L arm, R arm, bob
    assign_action(arm, "Walk", 1, 32)
    for frame, dist, lt, lk, rt, rk, la, ra, bob in (
        (1, 0.0, 0.48, -0.2, -0.42, -0.55, -0.42, 0.42, 0.0),
        (8, 0.22, 0.05, -0.95, -0.08, -0.25, 0.08, -0.08, 0.025),
        (16, 0.45, -0.48, -0.2, 0.42, -0.55, 0.42, -0.42, 0.0),
        (24, 0.68, -0.08, -0.25, 0.05, -0.95, -0.08, 0.08, 0.025),
        (32, 0.90, 0.48, -0.2, -0.42, -0.55, -0.42, 0.42, 0.0),
    ):
        key_pose(
            arm,
            frame,
            {
                "LeftUpperLeg": Euler((lt, 0.0, 0.0)),
                "LeftLowerLeg": Euler((lk, 0.0, 0.0)),
                "LeftFoot": Euler((level(lt, lk, 0.08 if lt > 0.2 else 0.0), 0.0, 0.0)),
                "RightUpperLeg": Euler((rt, 0.0, 0.0)),
                "RightLowerLeg": Euler((rk, 0.0, 0.0)),
                "RightFoot": Euler((level(rt, rk, 0.08 if rt > 0.2 else 0.0), 0.0, 0.0)),
                "LeftUpperArm": Euler((la, 0.0, 0.0)),
                "RightUpperArm": Euler((ra, 0.0, 0.0)),
                "LeftLowerArm": Euler((-0.45, 0.0, 0.0)),
                "RightLowerArm": Euler((-0.45, 0.0, 0.0)),
                "Spine": Euler((0.04, 0.0, lt * 0.08)),
                "Chest": Euler((0.03, 0.0, lt * 0.05)),
                "Head": Euler((-0.04, 0.0, -lt * 0.04)),
            },
            {"Hips": Vector((0.0, -dist, bob))},
        )

    assign_action(arm, "Run", 1, 18)
    for frame, dist, lt, lk, rt, rk, la, ra, bob in (
        (1, 0.0, 0.7, -0.35, -0.55, -0.9, -0.7, 0.7, 0.02),
        (5, 0.4, 0.1, -1.25, -0.1, -0.35, 0.15, -0.15, 0.06),
        (9, 0.8, -0.7, -0.35, 0.55, -0.9, 0.7, -0.7, 0.02),
        (14, 1.2, -0.1, -0.4, 0.1, -1.25, -0.15, 0.15, 0.06),
        (18, 1.6, 0.7, -0.35, -0.55, -0.9, -0.7, 0.7, 0.02),
    ):
        key_pose(
            arm,
            frame,
            {
                "LeftUpperLeg": Euler((lt, 0.0, 0.0)),
                "LeftLowerLeg": Euler((lk, 0.0, 0.0)),
                "LeftFoot": Euler((level(lt, lk, 0.15 if lt > 0.3 else -0.05), 0.0, 0.0)),
                "RightUpperLeg": Euler((rt, 0.0, 0.0)),
                "RightLowerLeg": Euler((rk, 0.0, 0.0)),
                "RightFoot": Euler((level(rt, rk, 0.15 if rt > 0.3 else -0.05), 0.0, 0.0)),
                "LeftUpperArm": Euler((la, 0.0, 0.0)),
                "RightUpperArm": Euler((ra, 0.0, 0.0)),
                "LeftLowerArm": Euler((-0.85, 0.0, 0.0)),
                "RightLowerArm": Euler((-0.85, 0.0, 0.0)),
                "Spine": Euler((0.18, 0.0, lt * 0.06)),
                "Chest": Euler((0.22, 0.0, lt * 0.04)),
                "Head": Euler((-0.1, 0.0, 0.0)),
            },
            {"Hips": Vector((0.0, -dist, bob))},
        )

    assign_action(arm, "Idle", 1, 60)
    for frame, breath, sway in ((1, 0.0, 0.0), (30, 0.035, 0.04), (60, 0.0, 0.0)):
        key_pose(
            arm,
            frame,
            {
                "Spine": Euler((breath, 0.0, sway)),
                "Chest": Euler((breath * 0.8, 0.0, sway * 0.5)),
                "Head": Euler((-breath, 0.0, -sway * 0.4)),
                "LeftUpperArm": Euler((0.04 + sway, 0.0, 0.0)),
                "RightUpperArm": Euler((0.04 - sway, 0.0, 0.0)),
                "LeftLowerArm": Euler((-0.18, 0.0, 0.0)),
                "RightLowerArm": Euler((-0.18, 0.0, 0.0)),
            },
            {"Hips": Vector((0.0, 0.0, breath * 0.25))},
        )
    bpy.ops.object.mode_set(mode="OBJECT")


def render_views(body, tag):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.film_transparent = False
    world = scene.world or bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background:
        background.inputs[0].default_value = (0.16, 0.17, 0.19, 1.0)
        background.inputs[1].default_value = 1.0
    corners = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
    center = centroid(corners)
    span = max(
        max(corner.x for corner in corners) - min(corner.x for corner in corners),
        max(corner.y for corner in corners) - min(corner.y for corner in corners),
        max(corner.z for corner in corners) - min(corner.z for corner in corners),
    )
    views = {
        "front": (center + Vector((0.0, -span * 2.2, 0.0)), (math.pi / 2.0, 0.0, 0.0)),
        "side": (center + Vector((span * 2.2, 0.0, 0.0)), (math.pi / 2.0, 0.0, math.pi / 2.0)),
    }
    os.makedirs(PREVIEW, exist_ok=True)
    for label, (location, rotation) in views.items():
        cam_data = bpy.data.cameras.new(label)
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = span * 1.35
        cam = bpy.data.objects.new(label, cam_data)
        scene.collection.objects.link(cam)
        cam.location = location
        cam.rotation_euler = rotation
        scene.camera = cam
        sun_data = bpy.data.lights.new(label + "Sun", "SUN")
        sun_data.energy = 3.2
        sun = bpy.data.objects.new(label + "Sun", sun_data)
        scene.collection.objects.link(sun)
        sun.rotation_euler = (0.9, 0.2, 0.5)
        scene.render.filepath = os.path.join(PREVIEW, f"{tag}_{label}.png")
        bpy.ops.render.render(write_still=True)
        print("  shot", scene.render.filepath)
        bpy.data.objects.remove(cam, do_unlink=True)
        bpy.data.objects.remove(sun, do_unlink=True)


def export_fbx(arm, body, path):
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="SMOOTH_GROUP",
        path_mode="COPY",
        embed_textures=True,
        use_tspace=True,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
    )
    print("  exported", path)


DEFORM = (
    "Hips", "Spine", "Chest", "Head",
    "LeftUpperArm", "LeftLowerArm", "LeftHand",
    "RightUpperArm", "RightLowerArm", "RightHand",
    "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
    "RightUpperLeg", "RightLowerLeg", "RightFoot",
)
TORSO_NAMES = ("Hips", "Spine", "Chest", "Head")
LIMB_NAMES = DEFORM[4:]
TORSO_RADIUS = {"Hips": 0.17, "Spine": 0.16, "Chest": 0.17, "Head": 0.3}
LIMB_RADIUS = {
    "LeftUpperArm": 0.12, "LeftLowerArm": 0.11, "LeftHand": 0.1,
    "RightUpperArm": 0.12, "RightLowerArm": 0.11, "RightHand": 0.1,
    "LeftUpperLeg": 0.14, "LeftLowerLeg": 0.12, "LeftFoot": 0.08,
    "RightUpperLeg": 0.14, "RightLowerLeg": 0.12, "RightFoot": 0.08,
}


def trace_chain(verts, start, goal, hops=14):
    # Walk the surface from the fist or the ankle back to the socket so the bone sits in the limb.
    point = Vector(start)
    path = [Vector(start)]
    for _ in range(hops):
        if (goal - point).length < 0.09:
            break
        best = None
        best_score = 1e9
        for vertex in verts:
            step = (vertex - point).length
            if step < 0.04 or step > 0.18:
                continue
            score = (vertex - goal).length
            if score < best_score:
                best_score = score
                best = vertex
        if best is None or best_score > (goal - point).length - 0.01:
            point = point.lerp(goal, 0.3)
        else:
            bunch = [vertex for vertex in verts if (vertex - best).length < 0.07]
            point = centroid(bunch) if len(bunch) >= 6 else Vector(best)
        path.append(Vector(point))
    path.append(Vector(goal))
    return path


def point_at(path, frac):
    lengths = [0.0]
    for earlier, later in zip(path, path[1:]):
        lengths.append(lengths[-1] + (later - earlier).length)
    total = max(lengths[-1], 1e-4)
    target = total * frac
    for index in range(1, len(lengths)):
        if lengths[index] < target:
            continue
        span = lengths[index] - lengths[index - 1]
        mix = 0.0 if span < 1e-6 else (target - lengths[index - 1]) / span
        return path[index - 1].lerp(path[index], mix)
    return path[-1]


def source_segments(marks, verts):
    hips = marks["hips"]
    chest = marks["chest"]
    neck = marks["neck"]
    head = marks["head"]
    spine_dir = chest - hips
    if spine_dir.length < 1e-4:
        spine_dir = UP.copy()
    spine_dir = spine_dir.normalized()
    head_dir = head - neck
    if head_dir.length < 1e-4:
        head_dir = UP.copy()
    segs = {
        "Hips": (hips, hips + spine_dir * 0.1),
        "Spine": (hips, chest),
        "Chest": (chest, neck),
        "Head": (neck, head + head_dir.normalized() * 0.14),
    }
    for side, key in ((1.0, "left"), (-1.0, "right")):
        shoulder, _elbow, hand = marks[key + "_arm"]
        _hip, _knee, ankle, toe = marks[key + "_leg"]
        prefix = "Left" if side > 0.0 else "Right"
        hip_goal = Vector((hips.x + side * 0.08, hips.y, hips.z))
        arm_path = list(reversed(trace_chain(verts, hand, shoulder)))
        leg_path = list(reversed(trace_chain(verts, ankle, hip_goal)))
        arm = [point_at(arm_path, cut) for cut in (0.0, 0.42, 0.78, 1.0)]
        leg = [point_at(leg_path, cut) for cut in (0.0, 0.48, 1.0)]
        segs[prefix + "UpperArm"] = (arm[0], arm[1])
        segs[prefix + "LowerArm"] = (arm[1], arm[2])
        segs[prefix + "Hand"] = (arm[2], arm[3])
        segs[prefix + "UpperLeg"] = (leg[0], leg[1])
        segs[prefix + "LowerLeg"] = (leg[1], leg[2])
        segs[prefix + "Foot"] = (ankle, toe)
        print(
            "  {} arm {:.2f} leg {:.2f}".format(
                prefix,
                (arm[3] - arm[0]).length,
                (leg[2] - leg[0]).length,
            )
        )
    return segs


def lengths_from_segments(segs):
    lengths = {}
    for name, (head, tail) in segs.items():
        lengths[name] = max(0.05, (tail - head).length)
    return lengths


def closest_named(point, segs, names):
    best_name = names[0]
    best_dist = 1e9
    for name in names:
        head, tail = segs[name]
        dist = segment_distance(point, head, tail)
        if dist < best_dist:
            best_dist = dist
            best_name = name
    return best_name, best_dist


def staff_segment(marks, verts, counts):
    hand = marks["right_arm"][2]
    ankle = marks["left_leg"][2]
    best = None
    best_dist = 1e9
    for index, point in enumerate(verts):
        if counts[index] > 8 or point.z > 0.22:
            continue
        dist = (point.x - ankle.x) ** 2 + (point.y - ankle.y) ** 2 + (point.z * 0.2) ** 2
        if dist < best_dist:
            best_dist = dist
            best = point
    if best is None:
        best = Vector((hand.x * 0.3, hand.y - 0.25, 0.02))
    return hand, best


def assign_deform(kind, point, counts, segs, staff_line):
    torso_name, torso_dist = closest_named(point, segs, TORSO_NAMES)
    limb_name, limb_dist = closest_named(point, segs, LIMB_NAMES)
    if "Foot" in limb_name and point.z > segs[limb_name][0].z + 0.07:
        leg_name = limb_name.replace("Foot", "LowerLeg")
        leg_dist = segment_distance(point, *segs[leg_name])
        if leg_dist < limb_dist + 0.04:
            limb_name, limb_dist = leg_name, leg_dist
    if kind == "hiker" and staff_line is not None:
        staff_dist = segment_distance(point, staff_line[0], staff_line[1])
        if staff_dist < 0.055 and counts <= 11 and point.z >= 0.16 and torso_dist > 0.1:
            return "Staff", True
    neck_z = segs["Chest"][1].z
    if torso_name == "Head" and torso_dist <= limb_dist + 0.02 and torso_dist < TORSO_RADIUS["Head"] and point.z > neck_z - 0.06:
        return "Head", torso_dist < 0.18
    # A limb wins when it is closer than the spine. The old core test kept the stride legs on the hips.
    if limb_dist < LIMB_RADIUS[limb_name] and limb_dist + 0.01 < torso_dist:
        return limb_name, True
    if torso_dist < TORSO_RADIUS[torso_name] and torso_dist <= limb_dist:
        return torso_name, torso_dist < TORSO_RADIUS[torso_name] * 0.7
    if torso_dist < 0.4:
        return torso_name, False
    if limb_dist < LIMB_RADIUS[limb_name] + 0.04:
        return limb_name, False
    return torso_name, False


def diffuse_weights(adj, weights, hard, iterations):
    bone_count = len(weights[0])
    current = weights
    for _ in range(iterations):
        nxt = [row[:] for row in current]
        for index, row in enumerate(current):
            if hard[index]:
                continue
            acc = [0.0] * bone_count
            total = 0
            for other, _ in adj[index]:
                other_row = current[other]
                amount = 0.0
                for value in other_row:
                    amount += value
                if amount <= 1e-8:
                    continue
                for bone in range(bone_count):
                    acc[bone] += other_row[bone]
                total += 1
            if total:
                inv = 1.0 / total
                nxt[index] = [value * inv for value in acc]
        current = nxt
    return current


def sack_flood(adj, coords, segs):
    seeds = [
        index for index, point in enumerate(coords)
        if point.x > 0.28 and point.y > 0.12 and point.z > 0.6
    ]
    if not seeds:
        return []
    seed = max(seeds, key=lambda index: coords[index].y)
    seen = set()
    stack = [seed]
    sack = []
    while stack:
        index = stack.pop()
        if index in seen:
            continue
        seen.add(index)
        point = coords[index]
        if point.x < 0.1 or point.z < 0.45 or point.z > 1.62:
            continue
        arm_dist = min(
            segment_distance(point, *segs["LeftUpperArm"]),
            segment_distance(point, *segs["LeftLowerArm"]),
            segment_distance(point, *segs["LeftHand"]),
        )
        spine_dist = min(
            segment_distance(point, *segs["Spine"]),
            segment_distance(point, *segs["Chest"]),
            segment_distance(point, *segs["Hips"]),
        )
        # The grip stays on the arm. The rest of the cloth is one rigid sack.
        if arm_dist < 0.075 and point.y < 0.1:
            continue
        if spine_dist < 0.11 and point.x < 0.22 and point.y < 0.08:
            continue
        sack.append(index)
        for other, _ in adj[index]:
            if other not in seen:
                stack.append(other)
    return sack


def repose_lbs(kind, mesh, coords, counts, marks, targets):
    segs = source_segments(marks, coords)
    sources = {}
    for name, (head, tail) in segs.items():
        hint = UP if "Foot" in name else FORWARD
        sources[name] = frame_of(head, tail, hint)
    staff_line = staff_segment(marks, coords, counts) if kind == "hiker" else None
    adj = mesh_adj(mesh)
    sack = []
    if kind == "robber":
        for index, point in enumerate(coords):
            if point.x < 0.18 or point.y < 0.0 or point.z < 0.5 or point.z > 1.65:
                continue
            arm_dist = min(
                segment_distance(point, *segs["LeftUpperArm"]),
                segment_distance(point, *segs["LeftLowerArm"]),
                segment_distance(point, *segs["LeftHand"]),
            )
            if arm_dist > 0.11:
                sack.append(index)
    sack_set = set(sack)
    bone_index = {name: index for index, name in enumerate(DEFORM)}
    weights = [[0.0] * len(DEFORM) for _ in coords]
    hard = [False] * len(coords)
    staff = []
    counts_out = {}
    for index, point in enumerate(coords):
        if index in sack_set:
            name, pinned = "Sack", True
        else:
            name, pinned = assign_deform(kind, point, counts[index], segs, staff_line)
        counts_out[name] = counts_out.get(name, 0) + 1
        if name == "Sack":
            name = "LeftHand"
            pinned = True
        elif name == "Staff":
            staff.append(index)
            name = "RightHand"
            pinned = True
        weights[index][bone_index[name]] = 1.0
        hard[index] = pinned
    # Only the unpinned coat shell blends. Limb pins stay on their own bone.
    weights = diffuse_weights(adj, weights, hard, 8)
    moved = []
    for index, point in enumerate(coords):
        row = weights[index]
        total = 0.0
        for value in row:
            total += value
        if total < 1e-6:
            moved.append(apply_frame(point, sources["Chest"], targets["Chest"]))
            continue
        acc = Vector()
        inv = 1.0 / total
        for name, bone in bone_index.items():
            weight = row[bone]
            if weight < 0.001:
                continue
            acc += apply_frame(point, sources[name], targets[name]) * (weight * inv)
        moved.append(acc)
    print("  bind counts", counts_out)
    print("  sack", len(sack), "staff", len(staff), "hard", sum(hard))
    pack = []
    if kind == "hiker":
        for index, point in enumerate(coords):
            if point.y > 0.22 and point.z > 1.0 and abs(point.x) < 0.28:
                pack.append(index)
        print("  backpack", len(pack))
    return moved, sack, staff, pack


def take_pack(kind, verts, marks, buckets):
    if kind != "hiker":
        return []
    chest = marks["chest"]
    pack = []
    for name in ("Chest", "Spine", "Hips"):
        kept = []
        for index in buckets.get(name, []):
            point = verts[index]
            if point.y > chest.y + 0.12 and point.z > 0.95:
                pack.append(index)
            else:
                kept.append(index)
        buckets[name] = kept
    buckets.setdefault("Chest", [])
    buckets["Chest"].extend(pack)
    print("  backpack", len(pack))
    return pack


def use_action(arm, name):
    action = bpy.data.actions[name]
    arm.animation_data.action = action
    if hasattr(arm.animation_data, "action_slot") and len(getattr(action, "slots", [])) > 0:
        arm.animation_data.action_slot = action.slots[0]


def prepare_body(mesh_name):
    body = bpy.data.objects[mesh_name]
    for obj in list(bpy.data.objects):
        if obj != body:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    body.vertex_groups.clear()
    body.modifiers.clear()
    if body.animation_data:
        body.animation_data_clear()
    return body


def process(job):
    print("JOB", job["key"])
    bpy.ops.wm.open_mainfile(filepath=job["blend"])
    body = prepare_body(job["mesh"])
    mesh = body.data
    before = [vertex.co.copy() for vertex in mesh.vertices]
    verts = [vertex.co.copy() for vertex in mesh.vertices]
    counts = neighbor_counts(verts, 0.055)
    marks = fit_landmarks(job["key"], verts, counts)
    lengths = lengths_from_segments(source_segments(marks, verts))
    joints = targets_from_lengths(lengths)
    targets = target_frames(joints)
    moved, sack, staff, pack = repose_lbs(job["key"], mesh, verts, counts, marks, targets)
    planted, joints = plant_and_scale(moved, joints)
    for vertex, point in zip(mesh.vertices, planted):
        vertex.co = point
    mesh.update()
    torn = stretch_report(mesh, before)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    arm = build_armature(job["arm_name"], joints)
    bind_mesh(body, arm, sack, staff, pack)
    animate(arm)
    scene = bpy.context.scene
    use_action(arm, "Idle")
    scene.frame_set(1)
    bpy.context.view_layer.update()
    render_views(body, job["key"] + "_tpose")
    use_action(arm, "Walk")
    scene.frame_set(8)
    bpy.context.view_layer.update()
    render_views(body, job["key"] + "_walk")
    use_action(arm, "Run")
    scene.frame_set(5)
    bpy.context.view_layer.update()
    render_views(body, job["key"] + "_run")
    use_action(arm, "Idle")
    scene.frame_set(1)
    # A fused stride shell tears when limbs are swung into an A pose. Do not replace the live FBX with that.
    if torn > 200:
        print("  skipped live FBX, torn edges", torn)
    else:
        export_fbx(arm, body, job["out_fbx"])
    bpy.ops.wm.save_as_mainfile(filepath=job["out_blend"])
    print("  saved", job["out_blend"])


def main():
    for job in JOBS:
        process(job)


if __name__ == "__main__":
    main()
