# Biped on the fal robber. Rest pose is the baked run. Blender Z up, face -Y.
# Unity stays on the generic clips and the chute path. No root motion.
import heapq
import math
import os
import sys

import bpy
from mathutils import Euler, Vector

BLEND = "/Users/astolp/Desktop/Dev/JuicySortGame/ArtSource/Heroes/SM_RobberProvided.blend"
OUT_FBX = "/Users/astolp/Desktop/Dev/JuicySortGame/Assets/Resources/Heroes/Robber_Rig.fbx"
ALBEDO = "/Users/astolp/Desktop/Dev/JuicySortGame/Assets/Resources/Heroes/T_Robber_Albedo.jpg"
# Collapse decimation tore a fin out of the sack, so the fal mesh stays as cleaned.

# The sculpt is already one stride. Large swings fold the torso and spike the sack.
# These are the holds that still read as a step after looking at the four frames.


def reset_scene_objects(body):
    for obj in list(bpy.data.objects):
        if obj == body:
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
    body.modifiers.clear()
    body.parent = None
    body.vertex_groups.clear()
    body.animation_data_clear()


def report_tris(body):
    body.data.calc_loop_triangles()
    print("TRIS", len(body.data.loop_triangles))


def mesh_graph(body):
    verts = [v.co.copy() for v in body.data.vertices]
    adj = [[] for _ in verts]
    for edge in body.data.edges:
        a, b = edge.vertices
        weight = (verts[a] - verts[b]).length
        adj[a].append((b, weight))
        adj[b].append((a, weight))
    return verts, adj


def dijkstra(adj, source):
    dist = [1e9] * len(adj)
    parent = [-1] * len(adj)
    dist[source] = 0.0
    heap = [(0.0, source)]
    while heap:
        current, node = heapq.heappop(heap)
        if current != dist[node]:
            continue
        for other, weight in adj[node]:
            nxt = current + weight
            if nxt < dist[other]:
                dist[other] = nxt
                parent[other] = node
                heapq.heappush(heap, (nxt, other))
    return parent


def path_points(verts, parent, tip, core):
    points = []
    node = tip
    guard = 0
    while node != -1 and guard < 6000:
        points.append(verts[node].copy())
        if node == core:
            break
        node = parent[node]
        guard += 1
    points.reverse()
    return points


def resample(path, count):
    if len(path) < 2:
        return path[:]
    lengths = [0.0]
    for a, b in zip(path, path[1:]):
        lengths.append(lengths[-1] + (a - b).length)
    total = max(lengths[-1], 1e-6)
    out = []
    for step in range(count):
        target = total * step / (count - 1)
        for index in range(1, len(lengths)):
            if lengths[index] < target and index != len(lengths) - 1:
                continue
            span = max(1e-6, lengths[index] - lengths[index - 1])
            mix = (target - lengths[index - 1]) / span
            out.append(path[index - 1].lerp(path[index], mix))
            break
    return out


def extreme(verts, score, pred):
    best = None
    best_score = None
    for index, point in enumerate(verts):
        if not pred(point):
            continue
        value = score(point)
        if best is None or value > best_score:
            best = index
            best_score = value
    return best


def pick_joints(body):
    verts, adj = mesh_graph(body)
    core_pts = [p for p in verts if abs(p.x) < 0.2 and -0.05 < p.z < 0.45 and -0.45 < p.y < 0.15]
    core_at = sum(core_pts, Vector()) / max(1, len(core_pts))
    core = min(range(len(verts)), key=lambda i: (verts[i] - core_at).length)
    parent = dijkstra(adj, core)

    seeds = {
        "head": extreme(verts, lambda p: p.z - 0.4 * max(0.0, p.y), lambda p: p.y < 0.2 and p.z > 0.55),
        "front_foot": extreme(verts, lambda p: -p.z + -0.2 * p.y, lambda p: p.y < -0.2 and p.z < -0.45),
        "back_foot": extreme(verts, lambda p: p.y - 0.4 * p.z, lambda p: p.y > 0.25 and p.z < -0.15 and abs(p.x) < 0.45),
        "fist": extreme(verts, lambda p: -p.y + 0.15 * abs(p.x), lambda p: -0.05 < p.z < 0.45 and p.y < -0.35 and p.x < -0.05),
        "sack_hand": extreme(
            verts,
            lambda p: p.x - 0.2 * abs(p.z - 0.58),
            lambda p: 0.18 < p.x < 0.48 and 0.42 < p.z < 0.72 and 0.0 < p.y < 0.42,
        ),
        "sack": extreme(verts, lambda p: p.y + 0.05 * p.z, lambda p: p.y > 0.45 and p.z > -0.05),
    }
    joints = {}
    for name, tip in seeds.items():
        if tip is None:
            print("MISS", name)
            continue
        samples = resample(path_points(verts, parent, tip, core), 6)
        joints[name] = samples
        print(name, "tip", tuple(round(c, 3) for c in verts[tip]))
        for point in samples:
            print("   ", tuple(round(c, 3) for c in point))
    joints["core"] = verts[core].copy()
    return joints


def add_bone(arm, name, head, tail, parent):
    bone = arm.data.edit_bones.new(name)
    bone.head = Vector(head)
    bone.tail = Vector(tail)
    if (bone.tail - bone.head).length < 0.05:
        bone.tail = bone.head + Vector((0.0, -0.02, 0.08))
    if parent:
        bone.parent = arm.data.edit_bones[parent]
    # Local X stays lateral so an X rotation swings the limb in the side view.
    best_roll = 0.0
    best_dot = -2.0
    for step in range(72):
        bone.roll = step * math.pi / 36.0
        dot = bone.x_axis.dot(Vector((1.0, 0.0, 0.0)))
        if dot > best_dot:
            best_dot = dot
            best_roll = bone.roll
    bone.roll = best_roll
    return bone


def build_armature(joints):
    arm_data = bpy.data.armatures.new("RobberRig")
    arm = bpy.data.objects.new("RobberRig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")

    core = joints["core"]
    head = joints["head"]
    front = joints["front_foot"]
    back = joints["back_foot"]
    fist = joints["fist"]
    sack_hand = joints.get("sack_hand") or joints["head"]
    sack = joints["sack"]

    hips = (core.x, core.y, core.z - 0.16)
    waist = (core.x, core.y + 0.01, core.z)
    chest = head[2]
    neck = head[3]
    skull = head[4]

    add_bone(arm, "Hips", hips, waist, None)
    add_bone(arm, "Spine", waist, ((waist[0] + chest.x) * 0.5, (waist[1] + chest.y) * 0.5, (waist[2] + chest.z) * 0.5), "Hips")
    add_bone(arm, "Chest", ((waist[0] + chest.x) * 0.5, (waist[1] + chest.y) * 0.5, (waist[2] + chest.z) * 0.5), chest, "Spine")
    add_bone(arm, "Head", neck, skull, "Chest")

    # Character faces -Y. -X is his right (the punching arm). +X holds the sack.
    add_bone(arm, "UpperArm.R", fist[1], fist[3], "Chest")
    add_bone(arm, "LowerArm.R", fist[3], fist[4], "UpperArm.R")
    add_bone(arm, "Hand.R", fist[4], fist[5], "LowerArm.R")

    add_bone(arm, "UpperArm.L", sack_hand[1], sack_hand[3], "Chest")
    add_bone(arm, "LowerArm.L", sack_hand[3], sack_hand[4], "UpperArm.L")
    add_bone(arm, "Hand.L", sack_hand[4], sack_hand[5], "LowerArm.L")

    # Start the thigh below the belt. The first sample sits in the pelvis,
    # and a pelvis vertex on the shin bone gets thrown when the knee bends.
    add_bone(arm, "UpperLeg.R", front[2], front[3], "Hips")
    add_bone(arm, "LowerLeg.R", front[3], front[4], "UpperLeg.R")
    add_bone(arm, "Foot.R", front[4], front[5], "LowerLeg.R")

    add_bone(arm, "UpperLeg.L", back[2], back[3], "Hips")
    add_bone(arm, "LowerLeg.L", back[3], back[4], "UpperLeg.L")
    add_bone(arm, "Foot.L", back[4], back[5], "LowerLeg.L")

    sack_from = Vector((0.12, 0.22, 0.48))
    sack_to = sack[5]
    add_bone(arm, "Sack", sack_from, sack_to, "Chest")

    bpy.ops.object.mode_set(mode="OBJECT")
    print("BONES", len(arm.data.bones))
    return arm


def segment_distance(point, start, end):
    span = end - start
    length_sq = span.length_squared
    if length_sq < 1e-8:
        return (point - start).length
    mix = max(0.0, min(1.0, (point - start).dot(span) / length_sq))
    return (point - (start + span * mix)).length


def radii_for(name):
    if name in ("Hips", "Spine", "Chest"):
        return 0.24
    if name == "Head":
        return 0.2
    if name == "Sack":
        return 0.38
    if name.startswith("Hand") or name.startswith("Foot"):
        return 0.22
    return 0.2


# A limb owns a vertex when that vertex sits on the limb, closer to the
# chain than to the torso. Axis boxes kept the thigh on the hips, so the
# boot swung and the knee tore open.
LIMB_CHAINS = {
    "Arm.R": ("UpperArm.R", "LowerArm.R", "Hand.R"),
    "Arm.L": ("UpperArm.L", "LowerArm.L", "Hand.L"),
    "Leg.R": ("UpperLeg.R", "LowerLeg.R", "Foot.R"),
    "Leg.L": ("UpperLeg.L", "LowerLeg.L", "Foot.L"),
}
TORSO_BONES = ("Hips", "Spine", "Chest", "Head")


def chain_distance(point, names, by_name):
    return min(segment_distance(point, by_name[name][1], by_name[name][2]) for name in names)


def weight_pool(point, by_name):
    # The face has to stay on the head, or a look-back only turns the hair.
    if point.z > 0.55 and point.y < 0.15 and abs(point.x) < 0.42:
        return ("Head", "Chest")
    # Boots and the fist are one piece each. Splitting them across a bend
    # is what left the toe behind.
    if point.z < -0.62 and point.y < -0.28:
        return ("Foot.R", "LowerLeg.R")
    if point.z < -0.62 and point.y > 0.35:
        return ("Foot.L", "LowerLeg.L")
    if point.y < -0.48 and point.x < -0.02 and point.z < 0.28:
        return ("Hand.R", "LowerArm.R")
    # Everything else below the belt is a leg. The gap between the legs
    # stays on the hips so the two shins cannot pull it apart.
    if point.z < -0.05:
        right = chain_distance(point, LIMB_CHAINS["Leg.R"], by_name)
        left = chain_distance(point, LIMB_CHAINS["Leg.L"], by_name)
        if abs(right - left) < 0.06:
            return TORSO_BONES
        names = LIMB_CHAINS["Leg.R"] if right < left else LIMB_CHAINS["Leg.L"]
        return names + ("Hips",)
    sack_dist = chain_distance(point, ("Sack",), by_name)
    torso_dist = chain_distance(point, TORSO_BONES, by_name)
    if point.y > 0.32 and sack_dist + 0.02 < torso_dist:
        return ("Sack", "Chest")
    if point.x < -0.15 and point.y < -0.12 and -0.25 < point.z < 0.5:
        return LIMB_CHAINS["Arm.R"] + ("Chest",)
    if point.x > 0.2 and 0.15 < point.z < 0.75 and -0.05 < point.y < 0.45:
        return LIMB_CHAINS["Arm.L"] + ("Chest",)
    return TORSO_BONES


def bind_weights(body, arm):
    bones = []
    for bone in arm.data.bones:
        bones.append((bone.name, bone.head_local.copy(), bone.tail_local.copy(), radii_for(bone.name)))
    by_name = {item[0]: item for item in bones}
    verts = body.data.vertices
    owned = {name: 0 for name, _, _, _ in bones}
    max_influences = 0
    groups = {name: body.vertex_groups.new(name=name) for name, _, _, _ in bones}
    for vert in verts:
        pool = [by_name[name] for name in weight_pool(vert.co, by_name)]
        scored = []
        for name, head, tail, radius in pool:
            dist = segment_distance(vert.co, head, tail)
            influence = max(0.0, 1.0 - dist / radius)
            influence = influence * influence
            if influence > 0.0:
                scored.append((influence, name))
        if not scored:
            nearest = min(pool, key=lambda item: segment_distance(vert.co, item[1], item[2]))
            scored = [(1.0, nearest[0])]
        scored.sort(reverse=True)
        scored = scored[:4]
        total = sum(weight for weight, _ in scored)
        max_influences = max(max_influences, len(scored))
        best_name = scored[0][1]
        owned[best_name] += 1
        for weight, name in scored:
            groups[name].add([vert.index], weight / total, "REPLACE")
    print("OWNED", {name: count for name, count in owned.items() if count})
    print("MAX_INFLUENCES", max_influences)
    modifier = body.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm
    modifier.use_vertex_groups = True
    modifier.use_bone_envelopes = False
    body.parent = arm
    body.matrix_parent_inverse = arm.matrix_world.inverted()


def action_for(arm, name, start, end, cyclic):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    act.use_frame_range = True
    act.frame_start = start
    act.frame_end = end
    act.use_cyclic = cyclic
    arm.animation_data_create()
    arm.animation_data.action = act
    return act


def key_pose(arm, frame, rotations, locations=None):
    locations = locations or {}
    bpy.context.view_layer.objects.active = arm
    if bpy.context.object.mode != "POSE":
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


def pose_of(rotations, bob=0.0):
    posed = {name: Euler(angles) for name, angles in rotations.items()}
    locations = {}
    if abs(bob) > 0.0:
        locations["Hips"] = Vector((0.0, 0.0, bob))
    return posed, locations


def animate(arm):
    # The source blend already has the old three bone actions. A second
    # Idle_Ledge would export as Idle_Ledge.001 and miss the Unity clips.
    for act in list(bpy.data.actions):
        bpy.data.actions.remove(act)

    # Thighs stay put. The sculpt is already one stride, and swinging both
    # thighs pulls the crotch apart. The step is a knee bend and an arm pump.
    # The front boot is already planted in the sculpt. An extra ankle
    # bend flips that sole toward the camera. The foot bones stay put
    # and the shin bend stays small.
    stride_a = {
        "LowerLeg.L": (-0.1, 0.0, 0.0),
        "UpperArm.R": (0.16, 0.0, 0.0),
        "LowerArm.R": (-0.12, 0.0, 0.0),
        "Sack": (0.08, 0.0, 0.0),
        "Chest": (0.03, 0.0, 0.0),
        "Head": (-0.03, 0.0, 0.0),
    }
    stride_b = {
        "LowerLeg.R": (-0.08, 0.0, 0.0),
        "LowerLeg.L": (0.1, 0.0, 0.0),
        "UpperArm.R": (-0.16, 0.0, 0.0),
        "LowerArm.R": (0.1, 0.0, 0.0),
        "Sack": (-0.08, 0.0, 0.0),
        "Chest": (-0.03, 0.0, 0.0),
        "Head": (0.04, 0.0, 0.0),
    }

    action_for(arm, "Idle_Ledge", 1, 48, True)
    key_pose(arm, 1, *pose_of({"Sack": (0.04, 0.0, 0.0), "Chest": (0.02, 0.0, 0.0)}, 0.0))
    key_pose(arm, 16, *pose_of({"Sack": (0.1, 0.0, 0.0), "Head": (0.0, 0.0, 0.08), "Chest": (0.03, 0.0, 0.0)}, 0.015))
    key_pose(arm, 32, *pose_of({"Sack": (-0.06, 0.0, 0.0), "Head": (0.0, 0.0, -0.1), "Chest": (0.0, 0.0, -0.04)}, 0.0))
    key_pose(arm, 48, *pose_of({"Sack": (0.04, 0.0, 0.0), "Chest": (0.02, 0.0, 0.0)}, 0.0))

    action_for(arm, "Scurry_Run", 1, 16, True)
    key_pose(arm, 1, *pose_of(stride_a, 0.02))
    key_pose(arm, 5, *pose_of({"Chest": (0.02, 0.0, 0.0)}, 0.0))
    key_pose(arm, 9, *pose_of(stride_b, 0.02))
    key_pose(arm, 13, *pose_of({"Chest": (0.02, 0.0, 0.0)}, 0.008))
    key_pose(arm, 16, *pose_of(stride_a, 0.02))

    action_for(arm, "Panic_Look", 1, 12, True)
    key_pose(arm, 1, *pose_of({"Head": (0.0, 0.0, 0.28), "Chest": (0.04, 0.0, 0.06), "Sack": (0.08, 0.0, 0.0)}, 0.01))
    key_pose(arm, 4, *pose_of({"Head": (0.0, 0.0, -0.05), "UpperArm.R": (0.12, 0.0, 0.0)}, 0.0))
    key_pose(arm, 7, *pose_of({"Head": (0.0, 0.0, -0.32), "Chest": (0.02, 0.0, -0.08), "Sack": (-0.06, 0.0, 0.0)}, 0.012))
    key_pose(arm, 10, *pose_of({"Head": (0.0, 0.0, 0.08)}, 0.0))
    key_pose(arm, 12, *pose_of({"Head": (0.0, 0.0, 0.28), "Chest": (0.04, 0.0, 0.06), "Sack": (0.08, 0.0, 0.0)}, 0.01))

    # Same poses as the run. A bigger arm bend on top of that sent the
    # fist through the front shoe.
    action_for(arm, "Leap_To_Car", 1, 20, False)
    key_pose(arm, 1, *pose_of(stride_a, 0.01))
    leaned = dict(stride_b)
    leaned["Chest"] = (-0.06, 0.0, 0.0)
    key_pose(arm, 8, *pose_of(leaned, 0.02))
    key_pose(arm, 20, *pose_of({"Chest": (-0.03, 0.0, 0.0), "Sack": (0.04, 0.0, 0.0)}, 0.0))


def shoot(arm, body, path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 800
    scene.render.film_transparent = True
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "VERTEX" if body.data.color_attributes else "MATERIAL"
    bpy.context.view_layer.update()
    corners = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
    center = sum(corners, Vector()) / 8.0
    span = max(
        max(c.x for c in corners) - min(c.x for c in corners),
        max(c.y for c in corners) - min(c.y for c in corners),
        max(c.z for c in corners) - min(c.z for c in corners),
    )
    cam_data = bpy.data.cameras.new("Shot")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = max(2.4, span * 1.7)
    cam = bpy.data.objects.new("Shot", cam_data)
    scene.collection.objects.link(cam)
    cam.location = center + Vector((span * 1.7, 0.0, 0.05))
    cam.rotation_euler = (1.5708, 0.0, 1.5708)
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    print("SHOT", path)


def paint_ownership(body):
    colors = {
        "Hips": (0.4, 0.25, 0.15, 1),
        "Spine": (0.55, 0.35, 0.2, 1),
        "Chest": (0.75, 0.55, 0.3, 1),
        "Head": (0.95, 0.8, 0.55, 1),
        "UpperArm.R": (0.9, 0.2, 0.2, 1),
        "LowerArm.R": (0.7, 0.05, 0.05, 1),
        "Hand.R": (1.0, 0.45, 0.45, 1),
        "UpperArm.L": (0.2, 0.35, 0.95, 1),
        "LowerArm.L": (0.05, 0.15, 0.7, 1),
        "Hand.L": (0.45, 0.7, 1.0, 1),
        "UpperLeg.R": (0.15, 0.75, 0.25, 1),
        "LowerLeg.R": (0.05, 0.45, 0.12, 1),
        "Foot.R": (0.6, 1.0, 0.4, 1),
        "UpperLeg.L": (0.85, 0.7, 0.1, 1),
        "LowerLeg.L": (0.55, 0.4, 0.05, 1),
        "Foot.L": (1.0, 0.9, 0.4, 1),
        "Sack": (0.75, 0.45, 0.85, 1),
    }
    attribute = body.data.color_attributes.new(name="Owner", type="BYTE_COLOR", domain="POINT")
    body.data.color_attributes.active_color = attribute
    best = {}
    for group in body.vertex_groups:
        for vert in body.data.vertices:
            try:
                weight = group.weight(vert.index)
            except RuntimeError:
                continue
            if weight > best.get(vert.index, (0.0, ""))[0]:
                best[vert.index] = (weight, group.name)
    for vert in body.data.vertices:
        name = best.get(vert.index, (1.0, "Hips"))[1]
        attribute.data[vert.index].color = colors.get(name, (1, 1, 1, 1))


def bend_check(arm, body):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    shoot(arm, body, "/tmp/robber-bend-rest.png")
    for bone_name in ("UpperLeg.R", "UpperLeg.L", "UpperArm.R", "UpperArm.L", "Chest"):
        bpy.ops.pose.transforms_clear()
        bone = arm.pose.bones[bone_name]
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = Euler((0.85, 0.0, 0.0))
        bpy.context.view_layer.update()
        shoot(arm, body, "/tmp/robber-bend-%s.png" % bone_name.replace(".", ""))
    bpy.ops.pose.transforms_clear()
    bpy.ops.object.mode_set(mode="OBJECT")


def displacement_check(arm, body):
    def capture():
        bpy.context.view_layer.update()
        deps = bpy.context.evaluated_depsgraph_get()
        evaluated = body.evaluated_get(deps)
        return [Vector(v.co) for v in evaluated.data.vertices]

    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.transforms_clear()
    for bone in arm.pose.bones:
        print("AXIS", bone.name, tuple(round(v, 2) for v in bone.x_axis))
    rest = capture()
    raw = [v.co.copy() for v in body.data.vertices]
    rest_delta = max((a - b).length for a, b in zip(raw, rest))
    print("REST_DELTA", round(rest_delta, 4))
    for bone_name in ("UpperLeg.R", "UpperLeg.L", "UpperArm.R", "UpperArm.L", "Head", "Sack"):
        bpy.ops.pose.transforms_clear()
        bone = arm.pose.bones[bone_name]
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = Euler((0.7, 0.0, 0.0))
        moved = capture()
        delta = sum((a - b).length for a, b in zip(rest, moved)) / len(rest)
        print("MOVE", bone_name, round(delta, 4))
    bpy.ops.pose.transforms_clear()
    bpy.ops.object.mode_set(mode="OBJECT")


def pack_albedo(body):
    image = None
    for material in body.data.materials:
        if material is None or not material.node_tree:
            continue
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                image = node.image
    if image is None:
        print("NO IMAGE")
        return
    if max(image.size[0], image.size[1]) > 1024:
        image.scale(1024, 1024)
    image.filepath_raw = ALBEDO
    image.file_format = "JPEG"
    image.save()
    print("ALBEDO", image.size[0], image.size[1])


def deformed_span(body):
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    evaluated = body.evaluated_get(deps)
    zs = [vertex.co.z for vertex in evaluated.data.vertices]
    ys = [vertex.co.y for vertex in evaluated.data.vertices]
    return max(zs) - min(zs), max(ys) - min(ys)


def rest_edge_lengths(body):
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    evaluated = body.evaluated_get(deps)
    coords = [vertex.co.copy() for vertex in evaluated.data.vertices]
    lengths = []
    for edge in body.data.edges:
        a, b = edge.vertices
        lengths.append((coords[a] - coords[b]).length)
    return lengths


def paint_motion(body, rest_coords):
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    evaluated = body.evaluated_get(deps)
    coords = [vertex.co.copy() for vertex in evaluated.data.vertices]
    existing = body.data.color_attributes.get("Motion")
    if existing is not None:
        body.data.color_attributes.remove(existing)
    attribute = body.data.color_attributes.new(name="Motion", type="BYTE_COLOR", domain="POINT")
    body.data.color_attributes.active_color = attribute
    hot = 0
    peak = 0.0
    peak_at = Vector()
    for index, coord in enumerate(coords):
        delta = (coord - rest_coords[index]).length
        if delta > peak:
            peak = delta
            peak_at = coord
        if delta > 0.08:
            hot += 1
            color = (1.0, 0.05, 0.05, 1.0)
        elif delta > 0.035:
            color = (1.0, 0.85, 0.1, 1.0)
        else:
            color = (0.55, 0.55, 0.55, 1.0)
        attribute.data[index].color = color
    print("HOT", hot, "PEAK", round(peak, 3), "at", tuple(round(c, 3) for c in peak_at))


def worst_stretch(body, rest_lengths):
    # Ignore hairline edges. A 0.1 mm edge that moves 1 mm reports a
    # huge ratio and is not a tear you can see.
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    evaluated = body.evaluated_get(deps)
    coords = [vertex.co.copy() for vertex in evaluated.data.vertices]
    worst_ratio = 1.0
    worst_growth = 0.0
    samples = []
    for edge in body.data.edges:
        base = rest_lengths[edge.index]
        if base < 0.012:
            continue
        a, b = edge.vertices
        length = (coords[a] - coords[b]).length
        ratio = length / base
        growth = length - base
        if ratio > worst_ratio:
            worst_ratio = ratio
        if growth > worst_growth:
            worst_growth = growth
        if growth > 0.025:
            mid = (coords[a] + coords[b]) * 0.5
            samples.append((growth, ratio, mid))
    samples.sort(reverse=True)
    for growth, ratio, mid in samples[:6]:
        print(
            "  STRETCH",
            round(growth, 3),
            "ratio",
            round(ratio, 2),
            "at",
            tuple(round(c, 3) for c in mid),
        )
    print("STRETCH_MAX", round(worst_growth, 3), "ratio", round(worst_ratio, 3))
    return worst_growth


def watch_cycle(arm, body):
    paint_ownership(body)
    shoot(arm, body, "/tmp/robber-watch-weights.png")
    owner = body.data.color_attributes.get("Owner")
    if owner is not None:
        body.data.color_attributes.remove(owner)
    scene = bpy.context.scene
    action = bpy.data.actions["Scurry_Run"]
    arm.animation_data.action = action
    if hasattr(arm.animation_data, "action_slot") and action.slots:
        arm.animation_data.action_slot = action.slots[0]
    scene.frame_set(5)
    bpy.context.view_layer.update()
    rest_h, rest_d = deformed_span(body)
    rest_lengths = rest_edge_lengths(body)
    deps = bpy.context.evaluated_depsgraph_get()
    rest_coords = [vertex.co.copy() for vertex in body.evaluated_get(deps).data.vertices]
    print("BIND", round(rest_h, 3), round(rest_d, 3))
    held = True
    for frame in (1, 5, 9, 13):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        height, depth = deformed_span(body)
        stretch = worst_stretch(body, rest_lengths)
        print("FRAME", frame, "h", round(height, 3), "d", round(depth, 3), "growth", round(stretch, 3))
        # 0.14 covers the sack sway and the fist pump. A real tear, the
        # folded body from the old cycle, drops the height or blows the depth.
        if height < rest_h * 0.82 or depth > rest_d * 1.35 or stretch > 0.16:
            held = False
            print("COLLAPSE", frame)
        if frame in (1, 9):
            paint_motion(body, rest_coords)
            shoot(arm, body, "/tmp/robber-motion-%02d.png" % frame)
            motion = body.data.color_attributes.get("Motion")
            if motion is not None:
                body.data.color_attributes.remove(motion)
        shoot(arm, body, "/tmp/robber-watch-side-%02d.png" % frame)
    bpy.context.view_layer.update()
    corners = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
    center = sum(corners, Vector()) / 8.0
    span = max(
        max(c.x for c in corners) - min(c.x for c in corners),
        max(c.y for c in corners) - min(c.y for c in corners),
        max(c.z for c in corners) - min(c.z for c in corners),
    )
    data = bpy.data.cameras.new("Face")
    data.type = "ORTHO"
    data.ortho_scale = span * 1.35
    face = bpy.data.objects.new("Face", data)
    scene.collection.objects.link(face)
    face.location = center + Vector((0.0, -span * 2.2, span * 0.02))
    face.rotation_euler = (1.5708, 0.0, 0.0)
    scene.camera = face
    for frame in (1, 5, 9, 13):
        scene.frame_set(frame)
        scene.render.filepath = "/tmp/robber-watch-front-%02d.png" % frame
        bpy.ops.render.render(write_still=True)
        print("WROTE", scene.render.filepath)
    leap = bpy.data.actions["Leap_To_Car"]
    arm.animation_data.action = leap
    if hasattr(arm.animation_data, "action_slot") and leap.slots:
        arm.animation_data.action_slot = leap.slots[0]
    scene.frame_set(8)
    bpy.context.view_layer.update()
    height, depth = deformed_span(body)
    stretch = worst_stretch(body, rest_lengths)
    print("LEAP", round(height, 3), round(depth, 3), "growth", round(stretch, 3))
    try:
        for layer in leap.layers:
            for strip in layer.strips:
                for slot in leap.slots:
                    bag = strip.channelbag(slot)
                    if bag is None:
                        continue
                    for fcurve in bag.fcurves:
                        keys = [(round(k.co[0]), round(k.co[1], 3)) for k in fcurve.keyframe_points]
                        if any(abs(value) > 0.2 for _, value in keys):
                            print("CURVE", fcurve.data_path, keys)
    except Exception as exc:
        print("CURVE_FAIL", exc)
    deps = bpy.context.evaluated_depsgraph_get()
    posed = [vertex.co.copy() for vertex in body.evaluated_get(deps).data.vertices]
    moved = []
    for index, coord in enumerate(posed):
        delta = (coord - rest_coords[index]).length
        if delta > 0.08:
            moved.append((delta, index))
    moved.sort(reverse=True)
    print("LEAP_MOVED", len(moved))
    for delta, index in moved[:5]:
        weights = []
        for group in body.vertex_groups:
            try:
                weight = group.weight(index)
            except RuntimeError:
                continue
            if weight > 0.15:
                weights.append((round(weight, 2), group.name))
        print("MOVED", round(delta, 3), weights)
    if height < rest_h * 0.82 or depth > rest_d * 1.35 or stretch > 0.16:
        held = False
        print("COLLAPSE", "leap")
    shoot(arm, body, "/tmp/robber-watch-leap.png")
    return held


def export(arm, body):
    bpy.ops.object.mode_set(mode="OBJECT")
    owner = body.data.color_attributes.get("Owner")
    if owner is not None:
        body.data.color_attributes.remove(owner)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        axis_forward="-Y",
        axis_up="Z",
        bake_space_transform=True,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=True,
    )
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("EXPORTED", OUT_FBX)


def main():
    sys.argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    do_export = "--export" in sys.argv
    do_watch = "--watch" in sys.argv or do_export
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    body = bpy.data.objects["RobberBody"]
    reset_scene_objects(body)
    report_tris(body)
    joints = pick_joints(body)
    arm = build_armature(joints)
    bind_weights(body, arm)
    if not do_watch:
        displacement_check(arm, body)
        print("SKIP_EXPORT")
        return
    animate(arm)
    held = watch_cycle(arm, body)
    if not held:
        print("HOLD_FAILED")
        return
    if not do_export:
        print("SKIP_EXPORT")
        return
    pack_albedo(body)
    export(arm, body)


if __name__ == "__main__":
    main()
