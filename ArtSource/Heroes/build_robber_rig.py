# Stylized heist robber. Blender Z up, facing -Y. Exports a Unity generic rig.
import math
import os

import bpy
import bmesh
from mathutils import Euler, Vector

OUT_FBX = "/Users/astolp/Desktop/Dev/JuicySortGame/Assets/Resources/Heroes/Robber_Rig.fbx"
OUT_BLEND = "/Users/astolp/Desktop/Dev/JuicySortGame/ArtSource/Heroes/SM_RobberRig.blend"
PREVIEW = "/tmp/robber-preview.png"

BONES = (
    ("Hips", (0.0, 0.0, 0.86), (0.0, 0.0, 1.02), None),
    ("Spine", (0.0, 0.0, 1.02), (0.0, 0.0, 1.22), "Hips"),
    ("Chest", (0.0, 0.0, 1.22), (0.0, -0.02, 1.40), "Spine"),
    ("Head", (0.0, -0.02, 1.48), (0.0, -0.04, 1.84), "Chest"),
    ("UpperArm.L", (0.18, -0.02, 1.30), (0.36, -0.08, 1.12), "Chest"),
    ("LowerArm.L", (0.36, -0.08, 1.12), (0.46, -0.16, 0.96), "UpperArm.L"),
    ("Hand.L", (0.46, -0.16, 0.96), (0.52, -0.22, 0.90), "LowerArm.L"),
    ("UpperArm.R", (-0.18, -0.02, 1.30), (-0.32, -0.20, 1.16), "Chest"),
    ("LowerArm.R", (-0.32, -0.20, 1.16), (-0.40, -0.34, 1.02), "UpperArm.R"),
    ("Hand.R", (-0.40, -0.34, 1.02), (-0.44, -0.42, 0.96), "LowerArm.R"),
    ("UpperLeg.L", (0.10, 0.0, 0.84), (0.12, -0.08, 0.48), "Hips"),
    ("LowerLeg.L", (0.12, -0.08, 0.48), (0.12, -0.02, 0.16), "UpperLeg.L"),
    ("Foot.L", (0.12, -0.02, 0.16), (0.12, -0.18, 0.08), "LowerLeg.L"),
    ("UpperLeg.R", (-0.10, 0.02, 0.84), (-0.10, 0.12, 0.50), "Hips"),
    ("LowerLeg.R", (-0.10, 0.12, 0.50), (-0.08, 0.04, 0.16), "UpperLeg.R"),
    ("Foot.R", (-0.08, 0.04, 0.16), (-0.08, -0.12, 0.08), "LowerLeg.R"),
    ("Bag", (0.34, 0.16, 1.16), (0.42, 0.28, 0.78), "UpperArm.L"),
)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 24
    scene.frame_start = 1


def material(name, color, roughness, metallic=0.0, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission > 0.0:
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = emission
    tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def new_mesh(name, bm):
    me = bpy.data.meshes.new(name + "Mesh")
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def primitive(kind):
    bm = bmesh.new()
    if kind == "sphere":
        bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=12, radius=0.5)
    elif kind == "cube":
        bmesh.ops.create_cube(bm, size=1.0)
    else:
        bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=0.5, radius2=0.5, depth=1.0)
    return bm


def limb(name, start, end, radius, mat, bone):
    start_v = Vector(start)
    end_v = Vector(end)
    mid = (start_v + end_v) * 0.5
    direction = end_v - start_v
    length = max(0.05, direction.length)
    rotation = direction.to_track_quat("Z", "Y").to_euler()
    return part(name, "cylinder", mid, (radius * 2.0, radius * 2.0, length), rotation, mat, bone)


def part(name, kind, location, scale, rotation, mat, bone):
    obj = new_mesh(name, primitive(kind))
    obj.location = location
    obj.scale = scale
    obj.rotation_euler = rotation
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.select_set(False)
    group = obj.vertex_groups.new(name=bone)
    group.add([vertex.index for vertex in obj.data.vertices], 1.0, "REPLACE")
    return obj


def build_armature():
    arm_data = bpy.data.armatures.new("RobberRig")
    arm = bpy.data.objects.new("RobberRig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    edit = {}
    for name, head, tail, parent in BONES:
        bone = arm_data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.roll = 0.0
        if parent:
            bone.parent = edit[parent]
        edit[name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def build_body(mats):
    parts = []
    suit = mats["RobberSuit"]
    skin = mats["RobberSkin"]
    mask = mats["RobberMask"]
    beanie = mats["RobberBeanie"]
    hair = mats["RobberHair"]
    leather = mats["RobberLeather"]
    gold = mats["RobberGold"]
    bag = mats["RobberBag"]
    eye = mats["RobberEye"]
    pupil = mats["RobberPupil"]

    parts.append(part("Pelvis", "sphere", (0.0, 0.0, 0.90), (0.30, 0.20, 0.18), (0, 0, 0), suit, "Hips"))
    parts.append(part("Torso", "sphere", (0.0, -0.02, 1.20), (0.36, 0.24, 0.40), (0, 0, 0), suit, "Chest"))
    parts.append(part("Head", "sphere", (0.0, -0.04, 1.60), (0.40, 0.38, 0.40), (0, 0, 0), skin, "Head"))
    parts.append(part("Beanie", "sphere", (0.0, -0.02, 1.74), (0.42, 0.40, 0.28), (0, 0, 0), beanie, "Head"))
    parts.append(part("Cuff", "cylinder", (0.0, -0.03, 1.58), (0.44, 0.42, 0.10), (0, 0, 0), beanie, "Head"))
    parts.append(part("Hair", "sphere", (0.0, -0.20, 1.70), (0.22, 0.12, 0.12), (0, 0, 0), hair, "Head"))
    parts.append(part("HairSide", "sphere", (0.16, -0.16, 1.66), (0.10, 0.08, 0.10), (0, 0, 0), hair, "Head"))
    parts.append(part("Mask", "cube", (0.0, -0.22, 1.60), (0.38, 0.08, 0.12), (0, 0, 0), mask, "Head"))
    parts.append(part("Eye.L", "sphere", (0.09, -0.26, 1.62), (0.10, 0.06, 0.10), (0, 0, 0), eye, "Head"))
    parts.append(part("Eye.R", "sphere", (-0.09, -0.26, 1.62), (0.10, 0.06, 0.10), (0, 0, 0), eye, "Head"))
    parts.append(part("Pupil.L", "sphere", (0.09, -0.30, 1.62), (0.045, 0.03, 0.05), (0, 0, 0), pupil, "Head"))
    parts.append(part("Pupil.R", "sphere", (-0.09, -0.30, 1.62), (0.045, 0.03, 0.05), (0, 0, 0), pupil, "Head"))

    parts.append(limb("Arm.L", (0.18, -0.02, 1.30), (0.36, -0.08, 1.12), 0.075, suit, "UpperArm.L"))
    parts.append(limb("Forearm.L", (0.36, -0.08, 1.12), (0.48, -0.18, 0.94), 0.062, suit, "LowerArm.L"))
    parts.append(part("Hand.L", "sphere", (0.50, -0.20, 0.90), (0.11, 0.10, 0.11), (0, 0, 0), leather, "Hand.L"))
    parts.append(limb("Arm.R", (-0.18, -0.02, 1.30), (-0.32, -0.20, 1.16), 0.075, suit, "UpperArm.R"))
    parts.append(limb("Forearm.R", (-0.32, -0.20, 1.16), (-0.42, -0.36, 1.00), 0.062, suit, "LowerArm.R"))
    parts.append(part("Hand.R", "sphere", (-0.44, -0.40, 0.96), (0.11, 0.10, 0.11), (0, 0, 0), leather, "Hand.R"))

    parts.append(limb("Thigh.L", (0.10, 0.0, 0.84), (0.12, -0.08, 0.46), 0.085, suit, "UpperLeg.L"))
    parts.append(limb("Shin.L", (0.12, -0.08, 0.46), (0.12, -0.04, 0.14), 0.068, suit, "LowerLeg.L"))
    parts.append(part("Boot.L", "cube", (0.12, -0.12, 0.09), (0.16, 0.30, 0.12), (0, 0, 0), leather, "Foot.L"))
    parts.append(limb("Thigh.R", (-0.10, 0.02, 0.84), (-0.10, 0.12, 0.48), 0.085, suit, "UpperLeg.R"))
    parts.append(limb("Shin.R", (-0.10, 0.12, 0.48), (-0.08, 0.04, 0.14), 0.068, suit, "LowerLeg.R"))
    parts.append(part("Boot.R", "cube", (-0.08, -0.04, 0.09), (0.16, 0.30, 0.12), (0, 0, 0), leather, "Foot.R"))

    parts.append(part("Belt", "cylinder", (0.0, 0.0, 0.98), (0.34, 0.24, 0.06), (0, 0, 0), leather, "Hips"))
    parts.append(part("Buckle", "cube", (0.0, -0.16, 0.98), (0.10, 0.04, 0.08), (0, 0, 0), gold, "Hips"))
    parts.append(part("Sack", "sphere", (0.36, 0.18, 1.08), (0.36, 0.30, 0.46), (0, 0, 0), bag, "Bag"))
    parts.append(part("Knot", "sphere", (0.26, 0.02, 1.30), (0.10, 0.08, 0.10), (0, 0, 0), bag, "Bag"))
    return parts


def join_parts(parts, arm):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = parts[0]
    body.name = "RobberBody"
    modifier = body.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm
    body.parent = arm
    return body


def center(arm, body):
    bpy.context.view_layer.update()
    corners = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
    center_v = sum(corners, Vector()) / 8.0
    delta = -center_v
    for vertex in body.data.vertices:
        vertex.co += delta
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for bone in arm.data.edit_bones:
        bone.head += delta
        bone.tail += delta
    bpy.ops.object.mode_set(mode="OBJECT")
    print("CENTERED", tuple(round(v, 3) for v in center_v))


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


def key_pose(arm, frame, rotations):
    bpy.context.view_layer.objects.active = arm
    if bpy.context.object.mode != "POSE":
        bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    for bone in arm.pose.bones:
        bone.rotation_mode = "XYZ"
        if bone.name in rotations:
            bone.rotation_euler = rotations[bone.name]
        bone.keyframe_insert("rotation_euler", frame=frame)
        bone.keyframe_insert("location", frame=frame)


def animate(arm):
    action_for(arm, "Idle_Ledge", 1, 48, True)
    key_pose(arm, 1, {})
    key_pose(arm, 16, {
        "Hips": Euler((0.04, 0.0, 0.0)),
        "Chest": Euler((0.05, 0.0, 0.04)),
        "Head": Euler((0.0, 0.0, 0.18)),
        "Bag": Euler((0.08, 0.0, 0.0)),
    })
    key_pose(arm, 32, {
        "Hips": Euler((-0.03, 0.0, 0.0)),
        "Chest": Euler((0.02, 0.0, -0.05)),
        "Head": Euler((0.08, 0.0, -0.16)),
        "Bag": Euler((-0.06, 0.0, 0.0)),
    })
    key_pose(arm, 48, {})

    action_for(arm, "Scurry_Run", 1, 16, True)
    for frame, swing in ((1, 1.0), (5, 0.0), (9, -1.0), (13, 0.0), (16, 1.0)):
        key_pose(arm, frame, {
            "UpperLeg.L": Euler((0.65 * swing, 0.0, 0.0)),
            "UpperLeg.R": Euler((-0.65 * swing, 0.0, 0.0)),
            "LowerLeg.L": Euler((0.55 * max(0.0, -swing), 0.0, 0.0)),
            "LowerLeg.R": Euler((0.55 * max(0.0, swing), 0.0, 0.0)),
            "UpperArm.R": Euler((0.5 * swing, 0.0, 0.0)),
            "UpperArm.L": Euler((-0.35 * swing, 0.0, 0.0)),
            "Chest": Euler((0.12, 0.0, 0.05 * swing)),
            "Head": Euler((-0.08, 0.0, 0.0)),
            "Hips": Euler((0.06, 0.0, 0.0)),
        })

    action_for(arm, "Panic_Look", 1, 12, True)
    for frame, swing, look in ((1, 1.0, 0.45), (4, 0.0, -0.15), (7, -1.0, -0.5), (10, 0.0, 0.1), (12, 1.0, 0.45)):
        key_pose(arm, frame, {
            "UpperLeg.L": Euler((0.8 * swing, 0.0, 0.0)),
            "UpperLeg.R": Euler((-0.8 * swing, 0.0, 0.0)),
            "LowerLeg.L": Euler((0.7 * max(0.0, -swing), 0.0, 0.0)),
            "LowerLeg.R": Euler((0.7 * max(0.0, swing), 0.0, 0.0)),
            "UpperArm.R": Euler((0.7 * swing, 0.0, -0.3)),
            "UpperArm.L": Euler((-0.4 * swing, 0.0, 0.4)),
            "Head": Euler((0.0, 0.0, look)),
            "Chest": Euler((0.18, 0.0, look * 0.3)),
        })

    action_for(arm, "Leap_To_Car", 1, 20, False)
    key_pose(arm, 1, {
        "UpperLeg.L": Euler((0.4, 0.0, 0.0)),
        "UpperLeg.R": Euler((0.4, 0.0, 0.0)),
        "Chest": Euler((0.25, 0.0, 0.0)),
        "Head": Euler((0.15, 0.0, 0.0)),
    })
    key_pose(arm, 8, {
        "UpperLeg.L": Euler((-0.5, 0.0, 0.0)),
        "UpperLeg.R": Euler((-0.2, 0.0, 0.0)),
        "LowerLeg.L": Euler((0.4, 0.0, 0.0)),
        "UpperArm.R": Euler((-1.1, 0.0, 0.2)),
        "UpperArm.L": Euler((-0.8, 0.0, -0.3)),
        "Chest": Euler((-0.35, 0.0, 0.0)),
        "Head": Euler((-0.2, 0.0, 0.0)),
    })
    key_pose(arm, 20, {
        "UpperLeg.L": Euler((-0.9, 0.0, 0.0)),
        "UpperLeg.R": Euler((-0.7, 0.0, 0.0)),
        "LowerLeg.L": Euler((0.8, 0.0, 0.0)),
        "LowerLeg.R": Euler((0.6, 0.0, 0.0)),
        "UpperArm.R": Euler((-1.4, 0.0, 0.1)),
        "UpperArm.L": Euler((-1.1, 0.0, -0.2)),
        "Chest": Euler((-0.7, 0.0, 0.0)),
        "Head": Euler((-0.25, 0.0, 0.0)),
        "Bag": Euler((0.4, 0.0, 0.0)),
    })


def preview(arm, body):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1024
    scene.render.film_transparent = True
    cam_data = bpy.data.cameras.new("Preview")
    cam_data.lens = 50
    cam = bpy.data.objects.new("Preview", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0.15, -3.1, 0.15)
    cam.rotation_euler = (math.radians(90), 0.0, 0.0)
    scene.camera = cam
    arm.animation_data.action = bpy.data.actions["Idle_Ledge"]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    scene.render.filepath = PREVIEW
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    bpy.ops.render.render(write_still=True)
    print("PREVIEW", PREVIEW)


def export(arm, body):
    bpy.ops.object.mode_set(mode="OBJECT")
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
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="FACE",
        path_mode="AUTO",
    )
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    print("EXPORTED", OUT_FBX)
    for act in bpy.data.actions:
        print("ACTION", act.name, "frames", act.frame_start, act.frame_end, "slots", len(act.slots))


def main():
    reset()
    mats = {
        "RobberSuit": material("RobberSuit", (0.07, 0.09, 0.14, 1.0), 0.35),
        "RobberSkin": material("RobberSkin", (0.93, 0.66, 0.46, 1.0), 0.45),
        "RobberMask": material("RobberMask", (0.03, 0.03, 0.04, 1.0), 0.3),
        "RobberBeanie": material("RobberBeanie", (0.06, 0.07, 0.1, 1.0), 0.4),
        "RobberHair": material("RobberHair", (0.32, 0.16, 0.08, 1.0), 0.4),
        "RobberLeather": material("RobberLeather", (0.33, 0.17, 0.09, 1.0), 0.32),
        "RobberGold": material("RobberGold", (1.0, 0.72, 0.12, 1.0), 0.2, 1.0, 0.6),
        "RobberBag": material("RobberBag", (0.74, 0.55, 0.32, 1.0), 0.55),
        "RobberEye": material("RobberEye", (0.96, 0.97, 0.98, 1.0), 0.2),
        "RobberPupil": material("RobberPupil", (0.04, 0.04, 0.05, 1.0), 0.3),
    }
    arm = build_armature()
    parts = build_body(mats)
    body = join_parts(parts, arm)
    center(arm, body)
    animate(arm)
    preview(arm, body)
    export(arm, body)


if __name__ == "__main__":
    main()
