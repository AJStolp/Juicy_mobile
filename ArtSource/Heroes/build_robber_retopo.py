# Rebuild SM_Robber as a quad A-pose with the sack detached.
# The intact sculpt is SM_Robber_Fal.blend. SM_Robber_Fal_Humanoid.blend is an older torn attempt.
# This does not write Robber_Rig.fbx. Authoring space is Blender Z up, face -Y.
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = "/Users/astolp/Desktop/Dev/JuicySortGame"
PREVIEW = "/tmp/fal-preview"
SOURCE_BLEND = ROOT + "/ArtSource/Heroes/FalClean/SM_Robber_Fal.blend"
OUT_BLEND = ROOT + "/ArtSource/Heroes/FalClean/SM_Robber_Fal_Humanoid.blend"
OUT_FBX = ROOT + "/Assets/Art/FalClean/SM_Robber_Fal.fbx"
TARGET_HEIGHT = 1.90
SIDES = 10
FORWARD = Vector((0.0, -1.0, 0.0))
UP = Vector((0.0, 0.0, 1.0))
OWNERS = []

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


def mesh_adjacency(mesh):
    adj = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adj[a].append(b)
        adj[b].append(a)
    return adj


def load_source():
    bpy.ops.wm.open_mainfile(filepath=SOURCE_BLEND)
    body = bpy.data.objects["SM_Robber"]
    for obj in list(bpy.data.objects):
        if obj != body:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    body.name = "SM_Robber_Source"
    return body


def landmarks(mesh):
    verts = [vertex.co.copy() for vertex in mesh.vertices]

    def pick(pred):
        return [point for point in verts if pred(point)]

    def mid(points, fallback):
        return centroid(points) if points else fallback

    hips = mid(pick(lambda p: 0.78 < p.z < 1.05 and abs(p.x) < 0.18 and abs(p.y) < 0.25), Vector((0.0, 0.0, 0.9)))
    chest = mid(pick(lambda p: 1.22 < p.z < 1.48 and abs(p.x) < 0.2 and p.y < 0.15), hips + Vector((0.0, -0.2, 0.4)))
    neck = mid(pick(lambda p: 1.48 < p.z < 1.66 and abs(p.x) < 0.16 and p.y < 0.1), chest + Vector((0.0, -0.02, 0.16)))
    head = mid(pick(lambda p: p.z > 1.7 and abs(p.x) < 0.3 and p.y < 0.15), neck + Vector((0.0, -0.04, 0.22)))
    grip = pick(lambda p: 0.2 < p.x < 0.5 and 0.95 < p.z < 1.38 and p.y < 0.16)
    left_hand = centroid(grip) if grip else Vector((0.34, -0.12, 1.18))
    fist = pick(lambda p: p.x < -0.35 and 0.55 < p.z < 1.15 and p.y < 0.05)
    right_hand = min(fist, key=lambda p: p.x) if fist else Vector((-0.6, -0.4, 0.9))
    left_shoulder = Vector((chest.x + 0.16, chest.y, chest.z))
    right_shoulder = Vector((chest.x - 0.16, chest.y, chest.z))

    def ankle(pred):
        points = pick(pred)
        ordered = sorted(points, key=lambda p: p.z)
        return centroid(ordered[int(len(ordered) * 0.72):])

    def toe(pred):
        return min(pick(pred), key=lambda p: p.y + p.z * 0.2)

    back = lambda p: p.z < 0.28 and p.y > 0.2
    front = lambda p: p.z < 0.28 and p.y < -0.2
    # +X is character left, and that leg trails. -X leads.
    left_ankle = ankle(back)
    right_ankle = ankle(front)
    left_toe = toe(back)
    right_toe = toe(front)

    def bulge(start, end, pred):
        span = end - start
        length_sq = max(1e-6, span.length_squared)
        best = None
        best_d = -1.0
        for point in verts:
            if not pred(point):
                continue
            mix = (point - start).dot(span) / length_sq
            if mix < 0.25 or mix > 0.75:
                continue
            dist = segment_distance(point, start, end)
            if dist > best_d:
                best_d = dist
                best = point
        if best is None or best_d > 0.22:
            return start.lerp(end, 0.48)
        return start.lerp(end, 0.48).lerp(best, 0.65)

    left_hip = Vector((hips.x + 0.08, hips.y, hips.z))
    right_hip = Vector((hips.x - 0.08, hips.y, hips.z))
    marks = {
        "hips": hips,
        "chest": chest,
        "neck": neck,
        "head": head + Vector((0.0, -0.02, 0.05)),
        "left_shoulder": left_shoulder,
        "right_shoulder": right_shoulder,
        "left_hand": left_hand,
        "right_hand": right_hand,
        "left_elbow": bulge(left_shoulder, left_hand, lambda p: p.x > 0.02),
        "right_elbow": bulge(right_shoulder, right_hand, lambda p: p.x < -0.02),
        "left_hip": left_hip,
        "right_hip": right_hip,
        "left_knee": bulge(left_hip, left_ankle, lambda p: p.x > -0.05 and p.z < hips.z),
        "right_knee": bulge(right_hip, right_ankle, lambda p: p.x < 0.05 and p.z < hips.z),
        "left_ankle": left_ankle,
        "right_ankle": right_ankle,
        "left_toe": left_toe,
        "right_toe": right_toe,
    }
    for name in (
        "hips", "chest", "neck", "head", "left_hand", "right_hand",
        "left_elbow", "right_elbow", "left_knee", "right_knee", "left_ankle", "right_ankle",
    ):
        point = marks[name]
        print(f"  {name:12} ({point.x:+.3f},{point.y:+.3f},{point.z:+.3f})")
    return marks, verts


def sack_indices(mesh, verts, marks):
    shoulder = marks["left_shoulder"]
    hand = marks["left_hand"]
    hips = marks["hips"]
    chest = marks["chest"]
    adj = mesh_adjacency(mesh)
    seeds = [
        index for index, point in enumerate(verts)
        if point.x > 0.28 and point.y > 0.12 and 0.6 < point.z < 1.45
    ]
    if not seeds:
        print("  sack seed missing")
        return []
    seed = max(seeds, key=lambda index: verts[index].y)
    seen = set()
    stack = [seed]
    chosen = []
    while stack:
        index = stack.pop()
        if index in seen:
            continue
        seen.add(index)
        point = verts[index]
        if point.z < 0.4 or point.z > 1.62 or point.x < 0.02:
            continue
        arm = min(
            segment_distance(point, shoulder, hand),
            segment_distance(point, marks["left_elbow"], hand),
        )
        spine = min(
            segment_distance(point, hips, chest),
            segment_distance(point, chest, marks["neck"]),
        )
        # Keep the gripping hand on the body. The cloth outside that tube is the sack.
        # The gripping fist stays on the body. The cloth outside that ball is the sack.
        if (point - hand).length < 0.15:
            continue
        if (point - marks["right_hand"]).length < 0.12:
            continue
        if arm < 0.09 and point.y < 0.12:
            continue
        if spine < 0.14 and point.x < 0.22 and point.y < 0.08:
            continue
        chosen.append(index)
        for other in adj[index]:
            if other not in seen:
                stack.append(other)
    print("  sack verts", len(chosen))
    return chosen


def face_sets(mesh, sack):
    sack_set = set(sack)
    sack_faces = []
    body_faces = []
    for poly in mesh.polygons:
        inside = sum(1 for index in poly.vertices if index in sack_set)
        if inside == len(poly.vertices):
            sack_faces.append(poly.index)
        else:
            body_faces.append(poly.index)
    print("  sack faces", len(sack_faces), "body faces", len(body_faces))
    return sack_faces, body_faces


def build_bvh(mesh, verts, face_ids):
    # BVH face indexes are into this triangle list, not the source polygon list.
    coords = verts
    tris = []
    sources = []
    for index in face_ids:
        poly = mesh.polygons[index]
        verts_i = list(poly.vertices)
        loops = list(poly.loop_indices)
        for offset in range(1, len(verts_i) - 1):
            tris.append((verts_i[0], verts_i[offset], verts_i[offset + 1]))
            sources.append((loops[0], loops[offset], loops[offset + 1]))
    return BVHTree.FromPolygons(coords, tris), sources


def ring_axes(direction, up_hint):
    y_axis = direction.normalized()
    x_axis = y_axis.cross(up_hint)
    if x_axis.length < 1e-4:
        x_axis = y_axis.cross(FORWARD)
    x_axis.normalize()
    z_axis = x_axis.cross(y_axis).normalized()
    x_axis = y_axis.cross(z_axis).normalized()
    return x_axis, z_axis


def add_ring(bm, center, direction, up_hint, rx, ry, sides, bone):
    x_axis, z_axis = ring_axes(direction, up_hint)
    axis = direction.normalized() if direction.length > 1e-8 else UP.copy()
    verts = []
    for index in range(sides):
        angle = math.tau * index / sides
        point = center + x_axis * (math.cos(angle) * rx) + z_axis * (math.sin(angle) * ry)
        verts.append(bm.verts.new(point))
        OWNERS.append((bone, center.copy(), axis.copy()))
    return verts


def bridge(bm, ring_a, ring_b):
    count = len(ring_a)
    for index in range(count):
        nxt = (index + 1) % count
        bm.faces.new((ring_a[index], ring_a[nxt], ring_b[nxt], ring_b[index]))


def cap(bm, ring, center, flip, bone, outward):
    pole = bm.verts.new(center)
    OWNERS.append((bone, center.copy(), outward.normalized()))
    count = len(ring)
    for index in range(count):
        nxt = (index + 1) % count
        if flip:
            bm.faces.new((pole, ring[nxt], ring[index]))
        else:
            bm.faces.new((pole, ring[index], ring[nxt]))


def loft(bm, points, radii, up_hint, sides, bones):
    rings = []
    for index, center in enumerate(points):
        if index < len(points) - 1:
            direction = points[index + 1] - center
        else:
            direction = center - points[index - 1]
        rx, ry = radii[index]
        rings.append(add_ring(bm, center, direction, up_hint, rx, ry, sides, bones[index]))
    for earlier, later in zip(rings, rings[1:]):
        bridge(bm, earlier, later)
    return rings


def limb_points(start, end, radii_ends, cuts):
    points = []
    radii = []
    for cut in cuts:
        points.append(start.lerp(end, cut))
        radii.append((
            radii_ends[0][0] * (1.0 - cut) + radii_ends[1][0] * cut,
            radii_ends[0][1] * (1.0 - cut) + radii_ends[1][1] * cut,
        ))
    return points, radii


def chain_lengths(marks):
    def dist(a, b):
        return (marks[a] - marks[b]).length

    upper = max(0.22, dist("right_shoulder", "right_elbow"))
    lower = max(0.16, dist("right_elbow", "right_hand") * 0.72)
    hand = 0.09
    thigh = max(0.3, dist("left_hip", "left_knee"), dist("right_hip", "right_knee"))
    shin = max(0.3, dist("left_knee", "left_ankle"), dist("right_knee", "right_ankle"))
    torso = max(0.42, dist("hips", "neck"))
    head = max(0.26, dist("neck", "head") + 0.14)
    shoulder = max(
        0.12,
        (marks["left_shoulder"] - marks["chest"]).length,
        (marks["right_shoulder"] - marks["chest"]).length,
    )
    print(
        f"  lengths upper {upper:.3f} lower {lower:.3f} thigh {thigh:.3f}"
        f" shin {shin:.3f} torso {torso:.3f} head {head:.3f} shoulder {shoulder:.3f}"
    )
    return {
        "upper": upper,
        "lower": lower,
        "hand": hand,
        "thigh": thigh,
        "shin": shin,
        "torso": torso,
        "head": head,
        "shoulder": shoulder,
    }


def build_body(lengths):
    # A-pose tubes. Segment lengths match the extended limbs so a two-bone
    # solve can plant the wrists and ankles on the stride sculpt.
    OWNERS.clear()
    hip_z = lengths["thigh"] + lengths["shin"] + 0.05
    hip = Vector((0.0, 0.0, hip_z))
    neck = hip + Vector((0.0, -0.02, lengths["torso"]))
    spine = hip.lerp(neck, 0.4)
    chest = hip.lerp(neck, 0.72)
    crown = neck + Vector((0.0, -0.03, lengths["head"]))
    drop = 0.55
    arm_dir = Vector((math.cos(drop), 0.0, -math.sin(drop))).normalized()

    def arm(side):
        root = Vector((lengths["shoulder"] * side, -0.02, chest.z))
        direction = Vector((arm_dir.x * side, 0.0, arm_dir.z))
        elbow = root + direction * lengths["upper"]
        wrist = elbow + direction * lengths["lower"]
        tip = wrist + direction * lengths["hand"]
        return root, elbow, wrist, tip

    def leg(side):
        root = Vector((0.1 * side, 0.0, hip.z))
        knee = root + Vector((0.01 * side, 0.02, -lengths["thigh"]))
        ankle = knee + Vector((0.0, 0.0, -lengths["shin"]))
        ball = ankle + Vector((0.0, -0.11, 0.0))
        toe = ball + Vector((0.0, -0.08, 0.0))
        return root, knee, ankle, ball, toe

    joints = {
        "left": arm(1.0),
        "right": arm(-1.0),
        "lleg": leg(1.0),
        "rleg": leg(-1.0),
        "torso": (hip, spine, chest, neck, crown),
    }
    bm = bmesh.new()
    torso_pts = [
        hip,
        hip.lerp(spine, 0.5),
        spine,
        spine.lerp(chest, 0.5),
        chest,
        chest.lerp(neck, 0.5),
        neck,
    ]
    torso_bones = ["Hips", "Hips", "Spine", "Spine", "Chest", "Chest", "Chest"]
    torso_rad = [
        (0.16, 0.12), (0.17, 0.13), (0.18, 0.14), (0.19, 0.15),
        (0.20, 0.16), (0.15, 0.12), (0.09, 0.08),
    ]
    torso_rings = loft(bm, torso_pts, torso_rad, FORWARD, 12, torso_bones)
    cap(bm, torso_rings[0], hip + Vector((0.0, 0.0, -0.06)), True, "Hips", Vector((0.0, 0.0, -1.0)))

    head_pts = [neck.lerp(crown, cut) for cut in (0.08, 0.3, 0.52, 0.75, 0.95)]
    head_rad = [(0.09, 0.08), (0.15, 0.14), (0.17, 0.16), (0.15, 0.14), (0.08, 0.07)]
    head_rings = loft(bm, head_pts, head_rad, FORWARD, 12, ["Head"] * len(head_pts))
    bridge(bm, torso_rings[-1], head_rings[0])
    cap(bm, head_rings[-1], crown, False, "Head", (crown - neck).normalized())

    for side, key, prefix in ((1.0, "left", "Left"), (-1.0, "right", "Right")):
        root, elbow, wrist, tip = joints[key]
        chest_side = Vector((0.05 * side, -0.02, chest.z))
        shoulder_pts = [chest_side, chest_side.lerp(root, 0.55), root]
        shoulder_rad = [(0.07, 0.07), (0.065, 0.065), (0.06, 0.06)]
        loft(bm, shoulder_pts, shoulder_rad, UP, SIDES, [prefix + "Shoulder"] * 3)
        upper_pts, upper_rad = limb_points(root, elbow, ((0.06, 0.06), (0.05, 0.05)), (0.0, 0.4, 0.75, 1.0))
        lower_pts, lower_rad = limb_points(elbow, wrist, ((0.05, 0.05), (0.04, 0.035)), (0.0, 0.4, 0.75, 1.0))
        hand_pts, hand_rad = limb_points(wrist, tip, ((0.04, 0.035), (0.035, 0.03)), (0.0, 0.45, 0.8, 1.0))
        direction = (tip - wrist).normalized()
        loft(bm, upper_pts, upper_rad, UP, SIDES, [prefix + "UpperArm"] * len(upper_pts))
        loft(bm, lower_pts, lower_rad, UP, SIDES, [prefix + "LowerArm"] * len(lower_pts))
        hand_rings = loft(bm, hand_pts, hand_rad, UP, SIDES, [prefix + "Hand"] * len(hand_pts))
        cap(bm, hand_rings[-1], tip + direction * 0.012, False, prefix + "Hand", direction)
        leg_root, knee, ankle, ball, toe = joints["lleg" if side > 0 else "rleg"]
        thigh_pts, thigh_rad = limb_points(leg_root, knee, ((0.09, 0.09), (0.07, 0.07)), (0.0, 0.4, 0.75, 1.0))
        shin_pts, shin_rad = limb_points(knee, ankle, ((0.07, 0.07), (0.05, 0.045)), (0.0, 0.4, 0.75, 1.0))
        foot_pts = [ankle, ankle.lerp(ball, 0.5), ball, toe]
        foot_rad = [(0.05, 0.045), (0.045, 0.05), (0.04, 0.04), (0.03, 0.025)]
        loft(bm, thigh_pts, thigh_rad, FORWARD, SIDES, [prefix + "UpperLeg"] * len(thigh_pts))
        loft(bm, shin_pts, shin_rad, FORWARD, SIDES, [prefix + "LowerLeg"] * len(shin_pts))
        foot_rings = loft(bm, foot_pts, foot_rad, UP, SIDES, [prefix + "Foot"] * len(foot_pts))
        cap(bm, foot_rings[-1], toe + Vector((0.0, -0.012, 0.0)), False, prefix + "Toes", Vector((0.0, -1.0, 0.0)))

    bm.verts.ensure_lookup_table()
    if len(OWNERS) != len(bm.verts):
        raise RuntimeError(f"owner count {len(OWNERS)} != verts {len(bm.verts)}")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mesh = bpy.data.meshes.new("SM_Robber_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    owners = list(OWNERS)
    obj = bpy.data.objects.new("SM_Robber", mesh)
    bpy.context.scene.collection.objects.link(obj)
    print("  mannequin verts", len(mesh.vertices), "faces", len(mesh.polygons))
    return obj, joints, owners


def bone_specs(joints):
    hip, spine, chest, neck, crown = joints["torso"]
    specs = {
        "Hips": (hip + Vector((0.0, 0.0, -0.04)), spine),
        "Spine": (spine, chest),
        "Chest": (chest, neck),
        "Neck": (neck, neck.lerp(crown, 0.28)),
        "Head": (neck.lerp(crown, 0.22), crown),
    }
    for side, key, prefix in ((1.0, "left", "Left"), (-1.0, "right", "Right")):
        root, elbow, wrist, tip = joints[key]
        chest_pt = chest + Vector((0.04 * side, -0.02, 0.0))
        specs[prefix + "Shoulder"] = (chest_pt, root)
        specs[prefix + "UpperArm"] = (root, elbow)
        specs[prefix + "LowerArm"] = (elbow, wrist)
        specs[prefix + "Hand"] = (wrist, tip)
        leg_root, knee, ankle, ball, toe = joints["lleg" if side > 0 else "rleg"]
        specs[prefix + "UpperLeg"] = (leg_root, knee)
        specs[prefix + "LowerLeg"] = (knee, ankle)
        specs[prefix + "Foot"] = (ankle, ball)
        specs[prefix + "Toes"] = (ball, toe)
    return specs


def add_bone(arm, name, head, tail, parent, up_hint):
    bone = arm.data.edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    if (bone.tail - bone.head).length < 0.04:
        bone.tail = bone.head + (bone.tail - bone.head).normalized() * 0.05
    if parent:
        bone.parent = arm.data.edit_bones[parent]
        bone.use_connect = False
    best_roll = 0.0
    best_score = -2.0
    for step in range(72):
        bone.roll = step * math.pi / 36.0
        if bone.z_axis.dot(up_hint) > best_score:
            best_score = bone.z_axis.dot(up_hint)
            best_roll = bone.roll
    bone.roll = best_roll
    return bone


def build_armature(specs):
    data = bpy.data.armatures.new("RobberArmature")
    arm = bpy.data.objects.new("RobberArmature", data)
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
    for name in order:
        head, tail = specs[name]
        hint = UP if "Foot" in name or "Toes" in name else FORWARD
        add_bone(arm, name, head, tail, PARENT.get(name), hint)
    bpy.ops.object.mode_set(mode="OBJECT")
    hip = arm.data.bones["Hips"]
    print(
        f"  hips axis y ({hip.y_axis.x:+.2f},{hip.y_axis.y:+.2f},{hip.y_axis.z:+.2f})"
        f" z ({hip.z_axis.x:+.2f},{hip.z_axis.y:+.2f},{hip.z_axis.z:+.2f})"
    )
    print("  bones", len(arm.data.bones))
    return arm


def bind_auto(body, arm):
    body.parent = None
    body.vertex_groups.clear()
    body.modifiers.clear()
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="OBJECT")
    body.select_set(True)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL")


def weight_rows(body):
    groups = {group.index: group.name for group in body.vertex_groups}
    rows = []
    for vertex in body.data.vertices:
        row = {}
        for item in vertex.groups:
            if item.weight > 0.001 and item.group in groups:
                row[groups[item.group]] = item.weight
        rows.append(row)
    return rows


def bone_delta(pose_bone):
    return pose_bone.matrix @ pose_bone.bone.matrix_local.inverted()


def aim_bone(pose_bone, direction):
    direction = direction.normalized()
    pose_bone.rotation_mode = "QUATERNION"
    parent = pose_bone.parent.matrix.to_3x3() if pose_bone.parent else Matrix.Identity(3)
    if pose_bone.parent:
        rest_rel = pose_bone.parent.bone.matrix_local.to_3x3().inverted() @ pose_bone.bone.matrix_local.to_3x3()
    else:
        rest_rel = pose_bone.bone.matrix_local.to_3x3()
    local_target = rest_rel.inverted() @ (parent.inverted() @ direction)
    if local_target.length < 1e-6:
        return
    pose_bone.rotation_quaternion = Vector((0.0, 1.0, 0.0)).rotation_difference(local_target.normalized())


def solve_two_bone(root, target, hint, len_a, len_b):
    span = target - root
    reach = span.length
    if reach < 1e-5:
        side = hint.normalized() if hint.length > 1e-5 else FORWARD.copy()
        return root + side * len_a
    axis = span / reach
    max_reach = len_a + len_b - 1e-4
    min_reach = abs(len_a - len_b) + 1e-4
    if reach > max_reach:
        return root + axis * len_a
    used = max(reach, min_reach)
    cos_ang = (len_a * len_a + used * used - len_b * len_b) / (2.0 * len_a * used)
    cos_ang = max(-1.0, min(1.0, cos_ang))
    sin_ang = math.sqrt(max(0.0, 1.0 - cos_ang * cos_ang))
    side = hint - axis * hint.dot(axis)
    if side.length < 1e-5:
        side = axis.cross(UP)
        if side.length < 1e-5:
            side = axis.cross(FORWARD)
    side.normalize()
    return root + axis * (len_a * cos_ang) + side * (len_a * sin_ang)


def pose_to_stride(arm, marks, joints):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    hip_bone = arm.pose.bones["Hips"]
    hip_bone.location = hip_bone.bone.matrix_local.to_3x3().inverted() @ (marks["hips"] - hip_bone.bone.head_local)
    bpy.context.view_layer.update()

    def aim_at(name, target):
        bone = arm.pose.bones[name]
        bpy.context.view_layer.update()
        aim_bone(bone, target - bone.head)
        bpy.context.view_layer.update()
        wanted = target - bone.head
        dot = 0.0 if wanted.length < 1e-6 else (bone.tail - bone.head).normalized().dot(wanted.normalized())
        print(f"  aim {name:16} {dot:.3f} miss {(bone.tail - target).length:.3f}")

    aim_at("Spine", marks["chest"])
    aim_at("Chest", marks["neck"])
    aim_at("Neck", marks["head"])
    aim_at("Head", marks["head"] + Vector((0.0, -0.05, 0.12)))

    for key, prefix, joint_key in (("left", "Left", "left"), ("right", "Right", "right")):
        root_r, elbow_r, wrist_r, tip_r = joints[joint_key]
        upper_len = (elbow_r - root_r).length
        lower_len = (wrist_r - elbow_r).length
        hand_len = (tip_r - wrist_r).length
        aim_at(prefix + "Shoulder", marks[key + "_shoulder"])
        upper = arm.pose.bones[prefix + "UpperArm"]
        bpy.context.view_layer.update()
        root = upper.head.copy()
        target = marks[key + "_hand"]
        elbow = solve_two_bone(root, target, marks[key + "_elbow"] - root, upper_len, lower_len)
        aim_at(prefix + "UpperArm", elbow)
        aim_at(prefix + "LowerArm", target)
        hand_dir = target - elbow
        if hand_dir.length < 1e-5:
            hand_dir = FORWARD.copy()
        aim_at(prefix + "Hand", target + hand_dir.normalized() * hand_len)

    for key, prefix, joint_key in (("left", "Left", "lleg"), ("right", "Right", "rleg")):
        root_r, knee_r, ankle_r, _ball, _toe = joints[joint_key]
        thigh_len = (knee_r - root_r).length
        shin_len = (ankle_r - knee_r).length
        upper = arm.pose.bones[prefix + "UpperLeg"]
        bpy.context.view_layer.update()
        root = upper.head.copy()
        target = marks[key + "_ankle"]
        knee = solve_two_bone(root, target, marks[key + "_knee"] - root, thigh_len, shin_len)
        aim_at(prefix + "UpperLeg", knee)
        aim_at(prefix + "LowerLeg", target)
        aim_at(prefix + "Foot", marks[key + "_toe"])
        aim_at(prefix + "Toes", marks[key + "_toe"] + Vector((0.0, -0.06, 0.0)))


def posed_coordinates(body):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = body.evaluated_get(depsgraph)
    return [vertex.co.copy() for vertex in evaluated.data.vertices]


def barycentric(point, a, b, c):
    v0 = b - a
    v1 = c - a
    v2 = point - a
    d00 = v0.dot(v0)
    d01 = v0.dot(v1)
    d11 = v1.dot(v1)
    d20 = v2.dot(v0)
    d21 = v2.dot(v1)
    denom = d00 * d11 - d01 * d01
    if abs(denom) < 1e-10:
        return 1.0, 0.0, 0.0
    v = (d11 * d20 - d01 * d21) / denom
    w = (d00 * d21 - d01 * d20) / denom
    return 1.0 - v - w, v, w


def assign_owner_weights(body, arm, owners):
    body.parent = arm
    body.matrix_parent_inverse = arm.matrix_world.inverted()
    body.vertex_groups.clear()
    body.modifiers.clear()
    groups = {bone.name: body.vertex_groups.new(name=bone.name) for bone in arm.data.bones}
    for index, (bone, _center, _axis) in enumerate(owners):
        groups[bone].add([index], 1.0, "REPLACE")
    modifier = body.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm


def radius_limit(bone):
    if bone == "Head":
        return 0.34
    if bone == "Neck":
        return 0.16
    if bone in ("Hips", "Spine", "Chest"):
        return 0.38
    if "Hand" in bone or "Foot" in bone or "Toes" in bone or "Arm" in bone or "Shoulder" in bone:
        return 0.16
    return 0.22


def radial_unwrap(body, owners, source, bvh, tri_sources, arm):
    # Cast from each posed bone axis out to the sculpt, then store that hit
    # in the bone's rest frame. One bone per vertex, so the ring stays a ring.
    bpy.context.view_layer.update()
    matrices = {bone.name: bone_delta(arm.pose.bones[bone.name]) for bone in arm.data.bones}
    uv_src = source.data.uv_layers.active.data
    mesh = body.data
    uvs = [Vector((0.5, 0.5)) for _ in mesh.vertices]
    missed = 0
    for index, vertex in enumerate(mesh.vertices):
        bone, center, axis = owners[index]
        delta = matrices[bone]
        posed_center = delta @ center
        direction = (delta @ vertex.co) - posed_center
        if direction.length < 1e-5:
            direction = delta.to_3x3() @ axis
        if direction.length < 1e-5:
            direction = UP.copy()
        direction.normalize()
        limit = radius_limit(bone)
        hit, _normal, face_index, _distance = bvh.ray_cast(posed_center, direction, limit)
        if hit is None:
            origin = posed_center + direction * limit
            hit, _normal, face_index, _distance = bvh.ray_cast(origin, -direction, limit)
        if hit is None:
            snapped = posed_center + direction * min(0.05, limit)
            missed += 1
        else:
            radius = max(0.02, min((hit - posed_center).length, limit))
            snapped = posed_center + direction * radius
            if face_index is not None and 0 <= face_index < len(tri_sources):
                loop_a, loop_b, loop_c = tri_sources[face_index]
                corners = [
                    source.data.vertices[source.data.loops[loop].vertex_index].co
                    for loop in (loop_a, loop_b, loop_c)
                ]
                bu, bv, bw = barycentric(hit, corners[0], corners[1], corners[2])
                uvs[index] = uv_src[loop_a].uv * bu + uv_src[loop_b].uv * bv + uv_src[loop_c].uv * bw
        vertex.co = delta.inverted() @ snapped
    if mesh.uv_layers.active is None:
        mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        uv_data = mesh.uv_layers.active.data
        uv_data[loop.index].uv = uvs[loop.vertex_index]
    mesh.update()
    print("  radial missed", missed, "of", len(mesh.vertices))
    return missed


def snap_and_invert(body, source, bvh, tri_sources, arm, weights):
    posed = posed_coordinates(body)
    uv_src = source.data.uv_layers.active.data
    snapped = []
    uvs = []
    far = 0
    for point in posed:
        location, _normal, face_index, distance = bvh.find_nearest(point)
        if face_index is None:
            snapped.append(point)
            uvs.append(Vector((0.0, 0.0)))
            far += 1
            continue
        if distance > 0.2:
            far += 1
        loop_a, loop_b, loop_c = tri_sources[face_index]
        tri = [
            source.data.vertices[source.data.loops[loop_a].vertex_index].co,
            source.data.vertices[source.data.loops[loop_b].vertex_index].co,
            source.data.vertices[source.data.loops[loop_c].vertex_index].co,
        ]
        bu, bv, bw = barycentric(location, tri[0], tri[1], tri[2])
        uv = (
            uv_src[loop_a].uv * bu
            + uv_src[loop_b].uv * bv
            + uv_src[loop_c].uv * bw
        )
        snapped.append(location)
        uvs.append(Vector(uv))
    names = [bone.name for bone in arm.data.bones]
    matrices = {bone.name: bone_delta(arm.pose.bones[bone.name]) for bone in arm.data.bones}
    rest = []
    for index, target in enumerate(snapped):
        row = weights[index]
        total = sum(row.values()) or 1.0
        mix_r = Matrix.Identity(3)
        mix_r.zero()
        mix_t = Vector()
        for name, weight in row.items():
            if name not in matrices:
                continue
            amount = weight / total
            delta = matrices[name]
            mix_r += delta.to_3x3() * amount
            mix_t += delta.to_translation() * amount
        if abs(mix_r.determinant()) < 1e-6:
            rest.append(target)
            continue
        rest.append(mix_r.inverted() @ (target - mix_t))
    mesh = body.data
    for vertex, point in zip(mesh.vertices, rest):
        vertex.co = point
    if mesh.uv_layers.active is None:
        mesh.uv_layers.new(name="UVMap")
    uv_data = mesh.uv_layers.active.data
    for loop in mesh.loops:
        uv_data[loop.index].uv = uvs[loop.vertex_index]
    mesh.update()
    print("  snap far", far, "of", len(posed))
    return far


def smooth_seams(body, weights):
    mesh = body.data
    neighbors = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].append(b)
        neighbors[b].append(a)
    coords = [vertex.co.copy() for vertex in mesh.vertices]
    for _ in range(5):
        nxt = [point.copy() for point in coords]
        for index, row in enumerate(weights):
            total = sum(row.values()) or 1.0
            dominant = max(row.values()) / total if row else 1.0
            if dominant > 0.72 or not neighbors[index]:
                continue
            acc = Vector()
            for other in neighbors[index]:
                acc += coords[other]
            blended = acc / len(neighbors[index])
            nxt[index] = coords[index].lerp(blended, 0.45)
        coords = nxt
    for vertex, point in zip(mesh.vertices, coords):
        vertex.co = point
    mesh.update()


def park_sack(sack, arm):
    # Stride pose is still on. Store the sack where the A-pose hand holds it,
    # so identity skin keeps it there and a later hand pose carries it along.
    bpy.context.view_layer.update()
    delta = bone_delta(arm.pose.bones["LeftHand"])
    if abs(delta.to_3x3().determinant()) < 1e-8:
        print("  sack park skipped")
        return
    inverse = delta.inverted()
    for vertex in sack.data.vertices:
        vertex.co = inverse @ vertex.co
    sack.data.update()
    center = centroid([vertex.co.copy() for vertex in sack.data.vertices])
    hand = arm.data.bones["LeftHand"].head_local
    print(
        f"  sack parked ({center.x:+.3f},{center.y:+.3f},{center.z:+.3f})"
        f" rest hand ({hand.x:+.3f},{hand.y:+.3f},{hand.z:+.3f})"
    )


def rest_edge_report(body):
    lengths = []
    for edge in body.data.edges:
        a, b = edge.vertices
        lengths.append((body.data.vertices[a].co - body.data.vertices[b].co).length)
    lengths.sort()
    median = lengths[len(lengths) // 2] if lengths else 0.0
    worst = lengths[-1] if lengths else 0.0
    over = sum(1 for length in lengths if median > 1e-6 and length > median * 4.0)
    print(f"  rest edges median {median:.4f} worst {worst:.4f} over_4x_median {over} of {len(lengths)}")
    return over


def clear_pose(arm):
    bpy.context.view_layer.objects.active = arm
    if arm.mode != "POSE":
        bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()


def make_sack(source, sack_faces, arm):
    src = source.data
    bm = bmesh.new()
    bm.from_mesh(src)
    bm.faces.ensure_lookup_table()
    keep = set(sack_faces)
    drop = [face for face in bm.faces if face.index not in keep]
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    loose = [vert for vert in bm.verts if not vert.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    mesh = bpy.data.meshes.new("SM_Robber_Sack_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("SM_Robber_Sack", mesh)
    bpy.context.scene.collection.objects.link(obj)
    # Rest coordinates stay in armature space. Full LeftHand weight moves the sack with the hand.
    group = obj.vertex_groups.new(name="LeftHand")
    group.add(list(range(len(mesh.vertices))), 1.0, "REPLACE")
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm
    print("  sack mesh verts", len(mesh.vertices), "faces", len(mesh.polygons))
    return obj


def plant(body, sack, arm):
    coords = [vertex.co.copy() for vertex in body.data.vertices]
    coords.extend(vertex.co.copy() for vertex in sack.data.vertices)
    min_z = min(point.z for point in coords)
    height = max(point.z for point in coords) - min_z
    scale = TARGET_HEIGHT / max(height, 1e-4)
    shift = Vector((0.0, 0.0, -min_z))

    def apply_mesh(obj):
        for vertex in obj.data.vertices:
            vertex.co = (vertex.co + shift) * scale
        obj.data.update()

    apply_mesh(body)
    apply_mesh(sack)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for bone in arm.data.edit_bones:
        bone.head = (bone.head + shift) * scale
        bone.tail = (bone.tail + shift) * scale
    bpy.ops.object.mode_set(mode="OBJECT")
    print(f"  planted height {height:.3f} scale {scale:.3f}")


def recalc(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def weight_report(body):
    mesh = body.data
    max_influences = 0
    empty = 0
    for vertex in mesh.vertices:
        used = [item for item in vertex.groups if item.weight > 0.001]
        max_influences = max(max_influences, len(used))
        if not used:
            empty += 1
    tris = sum(len(poly.vertices) - 2 for poly in mesh.polygons)
    print("  influences", max_influences, "unweighted", empty, "tris", tris, "verts", len(mesh.vertices))
    return max_influences, empty, tris


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
            loc = locations[bone.name]
            if bone.name == "Hips":
                loc = bone.bone.matrix_local.to_3x3().inverted() @ loc
            bone.location = loc
        bone.keyframe_insert("rotation_euler", frame=frame)
        bone.keyframe_insert("location", frame=frame)


def animate(arm):
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    bpy.context.scene.render.fps = 30

    def level(thigh, shin, pitch):
        return -(thigh + shin) * 0.85 + pitch

    assign_action(arm, "Walk", 1, 32)
    for frame, dist, lt, lk, rt, rk, la, ra, bob in (
        (1, 0.0, 0.45, -0.25, -0.4, -0.55, -0.4, 0.4, 0.0),
        (8, 0.22, 0.05, -0.9, -0.08, -0.25, 0.08, -0.08, 0.02),
        (16, 0.45, -0.45, -0.25, 0.4, -0.55, 0.4, -0.4, 0.0),
        (24, 0.68, -0.08, -0.25, 0.05, -0.9, -0.08, 0.08, 0.02),
        (32, 0.9, 0.45, -0.25, -0.4, -0.55, -0.4, 0.4, 0.0),
    ):
        key_pose(
            arm, frame,
            {
                "LeftUpperLeg": Euler((lt, 0.0, 0.0)),
                "LeftLowerLeg": Euler((lk, 0.0, 0.0)),
                "LeftFoot": Euler((level(lt, lk, 0.05), 0.0, 0.0)),
                "RightUpperLeg": Euler((rt, 0.0, 0.0)),
                "RightLowerLeg": Euler((rk, 0.0, 0.0)),
                "RightFoot": Euler((level(rt, rk, 0.05), 0.0, 0.0)),
                "LeftUpperArm": Euler((la, 0.0, 0.0)),
                "RightUpperArm": Euler((ra, 0.0, 0.0)),
                "LeftLowerArm": Euler((-0.35, 0.0, 0.0)),
                "RightLowerArm": Euler((-0.35, 0.0, 0.0)),
                "Spine": Euler((0.04, 0.0, lt * 0.08)),
                "Chest": Euler((0.03, 0.0, lt * 0.05)),
            },
            {"Hips": Vector((0.0, -dist, bob))},
        )
    assign_action(arm, "Run", 1, 18)
    for frame, dist, lt, lk, rt, rk, la, ra, bob in (
        (1, 0.0, 0.65, -0.4, -0.5, -0.9, -0.65, 0.65, 0.02),
        (5, 0.4, 0.1, -1.2, -0.1, -0.35, 0.15, -0.15, 0.05),
        (9, 0.8, -0.65, -0.4, 0.5, -0.9, 0.65, -0.65, 0.02),
        (14, 1.2, -0.1, -0.4, 0.1, -1.2, -0.15, 0.15, 0.05),
        (18, 1.6, 0.65, -0.4, -0.5, -0.9, -0.65, 0.65, 0.02),
    ):
        key_pose(
            arm, frame,
            {
                "LeftUpperLeg": Euler((lt, 0.0, 0.0)),
                "LeftLowerLeg": Euler((lk, 0.0, 0.0)),
                "LeftFoot": Euler((level(lt, lk, 0.1), 0.0, 0.0)),
                "RightUpperLeg": Euler((rt, 0.0, 0.0)),
                "RightLowerLeg": Euler((rk, 0.0, 0.0)),
                "RightFoot": Euler((level(rt, rk, 0.1), 0.0, 0.0)),
                "LeftUpperArm": Euler((la, 0.0, 0.0)),
                "RightUpperArm": Euler((ra, 0.0, 0.0)),
                "LeftLowerArm": Euler((-0.7, 0.0, 0.0)),
                "RightLowerArm": Euler((-0.7, 0.0, 0.0)),
                "Spine": Euler((0.16, 0.0, lt * 0.05)),
                "Chest": Euler((0.18, 0.0, 0.0)),
            },
            {"Hips": Vector((0.0, -dist, bob))},
        )
    assign_action(arm, "Idle", 1, 60)
    for frame, breath in ((1, 0.0), (30, 0.03), (60, 0.0)):
        key_pose(
            arm, frame,
            {
                "Spine": Euler((breath, 0.0, 0.0)),
                "Chest": Euler((breath * 0.6, 0.0, 0.0)),
                "LeftUpperArm": Euler((0.04, 0.0, 0.0)),
                "RightUpperArm": Euler((0.04, 0.0, 0.0)),
            },
            {"Hips": Vector((0.0, 0.0, breath * 0.2))},
        )
    bpy.ops.object.mode_set(mode="OBJECT")


def bend_stretch(body, arm):
    before = [vertex.co.copy() for vertex in body.data.vertices]
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for bone in arm.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = Euler((0.0, 0.0, 0.0))
        bone.location = Vector()
    arm.pose.bones["LeftLowerArm"].rotation_euler = Euler((-1.2, 0.0, 0.0))
    arm.pose.bones["RightLowerLeg"].rotation_euler = Euler((-1.1, 0.0, 0.0))
    bpy.context.view_layer.update()
    posed = posed_coordinates(body)
    ratios = []
    for edge in body.data.edges:
        a, b = edge.vertices
        old = (before[a] - before[b]).length
        new = (posed[a] - posed[b]).length
        if old > 1e-6:
            ratios.append(new / old)
    ratios.sort()
    worst = ratios[-1] if ratios else 0.0
    over = sum(1 for ratio in ratios if ratio > 2.0)
    mid = ratios[len(ratios) // 2] if ratios else 0.0
    print(f"  bend stretch median {mid:.3f} worst {worst:.3f} edges_over_2 {over} of {len(ratios)}")
    bpy.ops.pose.transforms_clear()
    bpy.ops.object.mode_set(mode="OBJECT")
    return over, len(ratios)


def render_views(bodies, tag):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    world = scene.world or bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background:
        background.inputs[0].default_value = (0.16, 0.17, 0.19, 1.0)
        background.inputs[1].default_value = 1.0
    corners = []
    for body in bodies:
        corners.extend(body.matrix_world @ Vector(corner) for corner in body.bound_box)
    center = centroid(corners)
    span = max(
        max(corner.x for corner in corners) - min(corner.x for corner in corners),
        max(corner.y for corner in corners) - min(corner.y for corner in corners),
        max(corner.z for corner in corners) - min(corner.z for corner in corners),
        0.5,
    )
    views = {
        "front": (center + Vector((0.0, -span * 2.3, 0.05)), (math.pi / 2.0, 0.0, 0.0)),
        "side": (center + Vector((span * 2.3, 0.0, 0.05)), (math.pi / 2.0, 0.0, math.pi / 2.0)),
    }
    os.makedirs(PREVIEW, exist_ok=True)
    for label, (location, rotation) in views.items():
        cam_data = bpy.data.cameras.new(label)
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = span * 1.25
        cam = bpy.data.objects.new(label, cam_data)
        scene.collection.objects.link(cam)
        cam.location = location
        cam.rotation_euler = rotation
        scene.camera = cam
        sun = bpy.data.lights.new(label + "Sun", "SUN")
        sun.energy = 3.5
        sun_obj = bpy.data.objects.new(label + "Sun", sun)
        scene.collection.objects.link(sun_obj)
        sun_obj.rotation_euler = (0.9, 0.2, 0.4)
        scene.render.filepath = os.path.join(PREVIEW, f"{tag}_{label}.png")
        bpy.ops.render.render(write_still=True)
        print("  shot", scene.render.filepath)
        bpy.data.objects.remove(cam, do_unlink=True)
        bpy.data.objects.remove(sun_obj, do_unlink=True)


def use_action(arm, name):
    action = bpy.data.actions[name]
    arm.animation_data.action = action
    if hasattr(arm.animation_data, "action_slot") and len(getattr(action, "slots", [])) > 0:
        arm.animation_data.action_slot = action.slots[0]


def export_fbx(arm, meshes):
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    for mesh in meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX,
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
    print("  exported", OUT_FBX, os.path.getsize(OUT_FBX))


def feet_aligned(body):
    left = [vertex.co.copy() for vertex in body.data.vertices if vertex.co.z < 0.12 and vertex.co.x > 0.02]
    right = [vertex.co.copy() for vertex in body.data.vertices if vertex.co.z < 0.12 and vertex.co.x < -0.02]
    if not left or not right:
        print("  feet missing", len(left), len(right))
        return False
    left_y = centroid(left).y
    right_y = centroid(right).y
    print(f"  foot y left {left_y:+.3f} right {right_y:+.3f}")
    return abs(left_y - right_y) < 0.18


def main():
    source = load_source()
    marks, verts = landmarks(source.data)
    sack = sack_indices(source.data, verts, marks)
    sack_faces, body_faces = face_sets(source.data, sack)
    lengths = chain_lengths(marks)
    body, joints, owners = build_body(lengths)
    source.data.materials[0].name = "M_Robber_Fal"
    body.data.materials.append(source.data.materials[0])
    arm = build_armature(bone_specs(joints))
    assign_owner_weights(body, arm, owners)
    pose_to_stride(arm, marks, joints)
    bvh, tri_sources = build_bvh(source.data, verts, body_faces)
    sack_obj = make_sack(source, sack_faces, arm)
    if source.data.materials:
        sack_obj.data.materials.append(source.data.materials[0])
    park_sack(sack_obj, arm)
    missed = radial_unwrap(body, owners, source, bvh, tri_sources, arm)
    source.hide_render = True
    render_views((body, sack_obj), "robber_retopo_stride")
    clear_pose(arm)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    # Heat weights on the unwrapped A-pose. The owner weights were only for the unwrap.
    bind_auto(body, arm)
    plant(body, sack_obj, arm)
    recalc(body)
    recalc(sack_obj)
    spiked = rest_edge_report(body)
    influences, empty, tris = weight_report(body)
    over, edge_count = bend_stretch(body, arm)
    aligned = feet_aligned(body)
    animate(arm)
    use_action(arm, "Idle")
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    render_views((body, sack_obj), "robber_retopo")
    use_action(arm, "Walk")
    bpy.context.scene.frame_set(8)
    bpy.context.view_layer.update()
    render_views((body, sack_obj), "robber_retopo_walk")
    use_action(arm, "Idle")
    bpy.context.scene.frame_set(1)
    # Pictures decide the export. RETOPO_EXPORT=1 is required so a bad run cannot replace the live FBX.
    ready = (
        os.environ.get("RETOPO_EXPORT") == "1"
        and missed < len(owners) * 0.45
        and empty == 0
        and influences <= 4
        and tris < 8000
        and over < edge_count * 0.05
        and spiked < 24
        and aligned
    )
    if ready:
        export_fbx(arm, (body, sack_obj))
    else:
        print(
            "  skipped FBX",
            "missed", missed,
            "empty", empty,
            "influences", influences,
            "tris", tris,
            "bend_over", over,
            "spiked", spiked,
            "aligned", aligned,
        )
    # Keep the source sculpt in the working blend, hidden. The original blend file is untouched.
    source.hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    print("  saved", OUT_BLEND)


if __name__ == "__main__":
    main()
