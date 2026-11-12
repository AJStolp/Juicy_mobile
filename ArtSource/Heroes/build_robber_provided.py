# The fal robber. One running pose, no skeleton. Cleans the mesh, adds a
# light rig for idle, run, peek, and leap, and exports the Unity FBX.
import os

import bpy
import bmesh
from mathutils import Euler, Vector

SRC = "/Users/astolp/Downloads/29H0777OYVQHTxMIEPSxs_model.glb"
OUT_FBX = "/Users/astolp/Desktop/Dev/JuicySortGame/Assets/Resources/Heroes/Robber_Rig.fbx"
OUT_BLEND = "/Users/astolp/Desktop/Dev/JuicySortGame/ArtSource/Heroes/SM_RobberProvided.blend"
ALBEDO = "/Users/astolp/Desktop/Dev/JuicySortGame/Assets/Resources/Heroes/T_Robber_Albedo.jpg"


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 24
    bpy.context.scene.frame_start = 1


def cleanup(obj):
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.001)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.calc_loop_triangles()
    print("CLEAN", len(me.loop_triangles))

    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = 0.42
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier="Decimate")
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.calc_loop_triangles()
    print("DECI", len(obj.data.loop_triangles))


def pack_albedo():
    image = None
    for candidate in bpy.data.images:
        if candidate.size[0] > 0:
            image = candidate
            break
    if image is None:
        print("NO IMAGE")
        return
    image.scale(1024, 1024)
    image.filepath_raw = ALBEDO
    image.file_format = "JPEG"
    image.save()
    print("ALBEDO", ALBEDO, image.size[0], image.size[1])


def bounds(obj):
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)


def add_bone(arm, name, head, tail, parent):
    bone = arm.data.edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    bone.roll = 0.0
    if parent:
        bone.parent = arm.data.edit_bones[parent]
    return bone


def weight(obj, limits):
    minx, maxx, miny, maxy, minz, maxz = limits
    dx = max(0.001, maxx - minx)
    dy = max(0.001, maxy - miny)
    dz = max(0.001, maxz - minz)
    root = obj.vertex_groups.new(name="Root")
    head = obj.vertex_groups.new(name="Head")
    sack = obj.vertex_groups.new(name="Sack")
    root_ids = []
    head_ids = []
    sack_ids = []
    head_w = []
    sack_w = []
    for vert in obj.data.vertices:
        co = vert.co
        nx = (co.x - minx) / dx
        ny = (co.y - miny) / dy
        nz = (co.z - minz) / dz
        hw = 0.0
        sw = 0.0
        if nz > 0.74 and ny < 0.62:
            hw = min(1.0, (nz - 0.74) / 0.12)
        elif ny > 0.58 and 0.28 < nz < 0.78:
            sw = min(1.0, (ny - 0.58) / 0.18)
        if hw > 0.05:
            head_ids.append(vert.index)
            head_w.append(hw)
        elif sw > 0.05:
            sack_ids.append(vert.index)
            sack_w.append(sw)
        root_ids.append(vert.index)
    root.add(root_ids, 1.0, "REPLACE")
    if head_ids:
        head.add(head_ids, 1.0, "REPLACE")
        for index, weight_value in zip(head_ids, head_w):
            head.add([index], weight_value, "REPLACE")
            root.add([index], 1.0 - weight_value, "REPLACE")
    if sack_ids:
        for index, weight_value in zip(sack_ids, sack_w):
            sack.add([index], weight_value, "REPLACE")
            root.add([index], 1.0 - weight_value, "REPLACE")
    print("WEIGHT head", len(head_ids), "sack", len(sack_ids))


def build_armature(limits):
    minx, maxx, miny, maxy, minz, maxz = limits
    cx = (minx + maxx) * 0.5
    cz = (minz + maxz) * 0.5
    arm_data = bpy.data.armatures.new("RobberRig")
    arm = bpy.data.objects.new("RobberRig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    add_bone(arm, "Root", (cx, (miny + maxy) * 0.45, minz + (maxz - minz) * 0.42),
             (cx, (miny + maxy) * 0.45, maxz * 0.72), None)
    add_bone(arm, "Head", (cx, miny + (maxy - miny) * 0.35, minz + (maxz - minz) * 0.78),
             (cx, miny + (maxy - miny) * 0.32, maxz), "Root")
    add_bone(arm, "Sack", (cx + (maxx - minx) * 0.05, maxy - (maxy - miny) * 0.18, cz),
             (cx, maxy, cz + (maxz - minz) * 0.12), "Root")
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


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


def key_pose(arm, frame, rotations, locations):
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


def animate(arm):
    action_for(arm, "Idle_Ledge", 1, 48, True)
    key_pose(arm, 1, {}, {})
    key_pose(arm, 16, {"Root": Euler((0.04, 0.0, 0.05)), "Sack": Euler((0.12, 0.0, 0.0))},
             {"Root": Vector((0.0, 0.0, 0.02))})
    key_pose(arm, 32, {"Root": Euler((0.02, 0.0, -0.05)), "Head": Euler((0.0, 0.0, 0.12)),
                       "Sack": Euler((-0.08, 0.0, 0.0))},
             {"Root": Vector((0.0, 0.0, 0.0))})
    key_pose(arm, 48, {}, {})

    action_for(arm, "Scurry_Run", 1, 16, True)
    for frame, swing in ((1, 1.0), (5, 0.0), (9, -1.0), (13, 0.0), (16, 1.0)):
        key_pose(
            arm, frame,
            {
                "Root": Euler((0.12, 0.0, 0.06 * swing)),
                "Head": Euler((-0.06, 0.0, 0.0)),
                "Sack": Euler((0.28 * swing, 0.0, 0.0)),
            },
            {"Root": Vector((0.0, 0.0, 0.045 * abs(swing)))},
        )

    action_for(arm, "Panic_Look", 1, 12, True)
    for frame, look in ((1, 0.45), (4, -0.2), (7, -0.5), (10, 0.15), (12, 0.45)):
        key_pose(
            arm, frame,
            {
                "Head": Euler((0.0, 0.0, look)),
                "Root": Euler((0.08, 0.0, look * 0.25)),
                "Sack": Euler((0.15, 0.0, 0.0)),
            },
            {"Root": Vector((0.0, 0.0, 0.03))},
        )

    action_for(arm, "Leap_To_Car", 1, 20, False)
    key_pose(arm, 1, {"Root": Euler((0.2, 0.0, 0.0))}, {})
    key_pose(arm, 8, {"Root": Euler((-0.45, 0.0, 0.0)), "Sack": Euler((0.35, 0.0, 0.0)),
                      "Head": Euler((-0.15, 0.0, 0.0))},
             {"Root": Vector((0.0, 0.0, 0.08))})
    key_pose(arm, 20, {"Root": Euler((-0.7, 0.0, 0.0)), "Sack": Euler((0.5, 0.0, 0.0)),
                       "Head": Euler((-0.2, 0.0, 0.0))},
             {"Root": Vector((0.0, 0.0, 0.02))})


def shoot(arm, body, action_name, frame, path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 480
    scene.render.resolution_y = 640
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.16, 0.17, 0.19, 1)
        bg.inputs[1].default_value = 1.0
    arm.animation_data.action = bpy.data.actions[action_name]
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    corners = [body.matrix_world @ Vector(c) for c in body.bound_box]
    center = sum(corners, Vector()) / 8.0
    span = max((max(c.y for c in corners) - min(c.y for c in corners)),
               (max(c.z for c in corners) - min(c.z for c in corners)))
    cam_data = bpy.data.cameras.new("Shot")
    cam_data.lens = 55
    cam = bpy.data.objects.new("Shot", cam_data)
    scene.collection.objects.link(cam)
    cam.location = center + Vector((0.0, -span * 1.35, span * 0.05))
    direction = (center - cam.location).normalized()
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    light_data = bpy.data.lights.new("ShotL", "AREA")
    light_data.energy = 300
    light_data.size = span
    light = bpy.data.objects.new("ShotL", light_data)
    scene.collection.objects.link(light)
    light.location = cam.location + Vector((0.3, 0.2, span * 0.4))
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("SHOT", path)


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
        axis_forward="-Y",
        axis_up="Z",
        bake_space_transform=True,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=True,
    )
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    print("EXPORTED", OUT_FBX)
    for act in bpy.data.actions:
        print("ACTION", act.name, act.frame_start, act.frame_end)


def main():
    reset()
    bpy.ops.import_scene.gltf(filepath=SRC)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = "RobberBody"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    cleanup(body)
    pack_albedo()
    limits = bounds(body)
    print("BOUNDS", tuple(round(v, 3) for v in limits))
    arm = build_armature(limits)
    weight(body, limits)
    modifier = body.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm
    body.parent = arm
    animate(arm)
    shoot(arm, body, "Idle_Ledge", 1, "/tmp/robber-hero-idle.png")
    shoot(arm, body, "Scurry_Run", 5, "/tmp/robber-hero-run.png")
    shoot(arm, body, "Leap_To_Car", 12, "/tmp/robber-hero-leap.png")
    export(arm, body)


if __name__ == "__main__":
    main()
