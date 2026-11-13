# Builds the vault door from scratch, with every moving part separate, and
# exports it as Assets/Art/Alley/SM_VaultDoor.fbx. Styled after
# VaultDoorPreview.png: grey steel body, gold frame and rings, silver wheel,
# green keyhole.
#
#   blender -b -P ArtSource/Alley/build_vault_door.py [-- --preview <dir>]
#
# Every part is a direct child of the frame (front faces Blender -Y like the
# other alley models). The FBX export mangles a second level of nesting, so
# AlleySceneBuilder moves the door parts under Door when it builds the scene.
#   SM_VaultDoor            the wall frame and hinges, static
#     Door                  origin on the left hinge line, swings open
#     Bolt_0..Bolt_3        door parts: corner bolts, origin at the bolt
#     Ring_Outer            door part, origin at the door center
#     Ring_Inner            same
#     Wheel                 same
#     Keyhole               same; emissive, the game drives its glow
# Material names are fixed; AlleySceneBuilder remaps them to URP:
#   M_VaultSteel, M_VaultGold, M_VaultSilver, M_VaultDark, M_VaultKeyGlow
import bpy, math, os, sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
art = os.path.join(root, "Assets", "Art", "Alley")

W, H, T = 1.4, 1.7, 0.18   # door width (x), height (z), thickness (y)
SILL = 0.12                # door bottom above the ground
CZ = SILL + H / 2          # door center height
FRONT = -T / 2             # door front face y
BORDER = 0.2               # wall frame width around the door
TAU = math.tau


def material(name, color, metal, rough, emit=None, strength=4.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1)
        bsdf.inputs["Emission Strength"].default_value = strength
    return m


def obj(mesh_fn, name, mat, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), bevel=0.0, segments=3):
    mesh_fn()
    o = bpy.context.active_object
    o.name = name
    o.location, o.rotation_euler, o.scale = loc, rot, scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.data.materials.clear()
    o.data.materials.append(mat)
    if bevel > 0:
        mod = o.modifiers.new("bevel", 'BEVEL')
        mod.width, mod.segments, mod.limit_method = bevel, segments, 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.shade_auto_smooth(angle=math.radians(35))
    return o


def box(name, mat, size, loc, bevel=0.0, segments=3, rot=(0, 0, 0)):
    return obj(lambda: bpy.ops.mesh.primitive_cube_add(size=1), name, mat, loc, rot, scale=size, bevel=bevel, segments=segments)


def cyl(name, mat, r, depth, loc, rot=(math.radians(90), 0, 0), verts=32, bevel=0.0):
    return obj(lambda: bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth), name, mat, loc, rot, bevel=bevel, segments=2)


def sphere(name, mat, r, loc, scale=(1, 1, 1)):
    return obj(lambda: bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=r), name, mat, loc, scale=scale)


def torus(name, mat, major, minor, loc):
    return obj(lambda: bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=64, minor_segments=14),
               name, mat, loc, (math.radians(90), 0, 0))


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    return o


def pivot(o, point, parent):
    # Moves the origin to point (world) and parents without moving the mesh.
    # The parent inverse keeps the child's world placement, which is what
    # survives the FBX export cleanly for nested parts.
    p = Vector(point)
    o.data.transform(Matrix.Translation(-p))
    o.location = p
    bpy.context.view_layer.update()
    o.parent = parent
    o.matrix_parent_inverse = parent.matrix_world.inverted()
    return o


def build():
    steel = material("M_VaultSteel", (0.26, 0.29, 0.34), 0.75, 0.4)
    gold = material("M_VaultGold", (1.0, 0.68, 0.18), 1.0, 0.24)
    silver = material("M_VaultSilver", (0.8, 0.82, 0.86), 0.95, 0.2)
    dark = material("M_VaultDark", (0.03, 0.035, 0.045), 0.4, 0.6)
    glow = material("M_VaultKeyGlow", (0.55, 1.0, 0.62), 0.0, 0.3, (0.35, 1.0, 0.45))

    # Wall frame: a steel surround with a dark recess, and three hinges.
    ow, oh = W + 2 * BORDER, H + BORDER + SILL
    frame = [
        box("frame_top", steel, (ow, T + 0.08, BORDER), (0, 0.02, SILL + H + BORDER / 2), bevel=0.03),
        box("frame_l", steel, (BORDER, T + 0.08, oh), (-W / 2 - BORDER / 2, 0.02, oh / 2), bevel=0.03),
        box("frame_r", steel, (BORDER, T + 0.08, oh), (W / 2 + BORDER / 2, 0.02, oh / 2), bevel=0.03),
        box("sill", steel, (ow, T + 0.08, SILL), (0, 0.02, SILL / 2), bevel=0.02),
        box("recess", dark, (W, 0.02, H), (0, T / 2 + 0.03, CZ)),
    ]
    for z in (SILL + 0.25, CZ, SILL + H - 0.25):
        frame.append(cyl("hinge", silver, 0.06, 0.26, (-W / 2 - 0.03, FRONT - 0.04, z), (0, 0, 0), 24, bevel=0.01))
    vault = join(frame, "SM_VaultDoor")

    # Door slab with the gold frame bars, corner caps, inner knobs and the
    # silver cog teeth behind the rings. All of this swings with the door.
    door = [box("slab", steel, (W, T, H), (0, 0, CZ), bevel=0.06, segments=4)]
    fy = FRONT - 0.04
    inset = 0.17
    bar = 0.12
    for sx, sz, x, z in ((W - 2 * inset, bar, 0, CZ + H / 2 - inset), (W - 2 * inset, bar, 0, CZ - H / 2 + inset),
                         (bar, H - 2 * inset, -W / 2 + inset, CZ), (bar, H - 2 * inset, W / 2 - inset, CZ)):
        door.append(box("bar", gold, (sx, 0.08, sz), (x, fy, z), bevel=0.05, segments=4))
    door.append(box("face", dark, (W - 2 * inset - 0.1, 0.02, H - 2 * inset - 0.1), (0, FRONT - 0.005, CZ)))
    for x in (-1, 1):
        for z in (-1, 1):
            door.append(sphere("inner_knob", gold, 0.07, (x * (W / 2 - inset - 0.13), fy - 0.02, CZ + z * (H / 2 - inset - 0.13))))
    for i in range(12):
        a = i * TAU / 12
        door.append(box("tooth", silver, (0.09, 0.05, 0.07), (math.sin(a) * 0.56, FRONT - 0.03, CZ + math.cos(a) * 0.56),
                        bevel=0.015, rot=(0, a, 0)))
    door_obj = join(door, "Door")
    pivot(door_obj, (-W / 2, 0, CZ), vault)

    center = Vector((0, 0, CZ))

    # Corner bolts: a big gold cap on a short shaft, sitting in the frame
    # corners. They back out toward the viewer and pop off.
    for i, (x, z) in enumerate(((-1, 1), (1, 1), (1, -1), (-1, -1))):
        at = (x * (W / 2 - inset), fy - 0.06, CZ + z * (H / 2 - inset))
        shaft = cyl("shaft", silver, 0.05, 0.14, (at[0], at[1] + 0.07, at[2]), verts=24)
        cap = sphere("cap", gold, 0.1, at, (1, 0.8, 1))
        bolt = join([cap, shaft], "Bolt_%d" % i)
        pivot(bolt, at, vault)

    ring_y = FRONT - 0.07
    outer = torus("Ring_Outer", gold, 0.48, 0.06, (0, ring_y, CZ))
    for i in range(4):
        a = i * TAU / 4 + TAU / 8
        outer = join([outer, box("notch", dark, (0.05, 0.03, 0.05), (math.sin(a) * 0.48, ring_y - 0.06, CZ + math.cos(a) * 0.48))], "Ring_Outer")
    pivot(outer, center, vault)

    inner = torus("Ring_Inner", gold, 0.33, 0.045, (0, ring_y - 0.02, CZ))
    for i in range(4):
        a = i * TAU / 4
        inner = join([inner, box("notch", dark, (0.04, 0.03, 0.04), (math.sin(a) * 0.33, ring_y - 0.065, CZ + math.cos(a) * 0.33))], "Ring_Inner")
    pivot(inner, center, vault)

    wy = ring_y - 0.1
    wheel = [cyl("hub", silver, 0.13, 0.08, (0, wy, CZ), verts=48, bevel=0.015)]
    for i in range(8):
        a = i * TAU / 8
        wheel.append(cyl("spoke", silver, 0.028, 0.3, (math.sin(a) * 0.25, wy - 0.01, CZ + math.cos(a) * 0.25), (0, a, 0), 16))
        wheel.append(sphere("grip", silver, 0.04, (math.sin(a) * 0.4, wy - 0.01, CZ + math.cos(a) * 0.4)))
    wheel_obj = join(wheel, "Wheel")
    pivot(wheel_obj, center, vault)

    key = [cyl("lens", glow, 0.085, 0.03, (0, wy - 0.045, CZ), verts=48),
           cyl("key_round", dark, 0.016, 0.034, (0, wy - 0.05, CZ + 0.015), verts=16),
           box("key_slot", dark, (0.012, 0.034, 0.04), (0, wy - 0.05, CZ - 0.012))]
    key_obj = join(key, "Keyhole")
    pivot(key_obj, center, vault)
    return vault


def world_of(o):
    return o.matrix_world.translation


def render(vault, path, pose):
    scene = bpy.context.scene
    door = bpy.data.objects["Door"]
    for o in bpy.data.objects:
        if o.name.startswith("Bolt_"):
            o.location.y = o.get("home_y", o.location.y)
    if pose in ("progress", "open"):
        for name in ("Bolt_0", "Bolt_1"):
            b = bpy.data.objects[name]
            b.location.y -= 0.18
            b.rotation_euler.y = math.radians(200)
        bpy.data.objects["Ring_Outer"].rotation_euler.y = math.radians(60)
        bpy.data.objects["Ring_Inner"].rotation_euler.y = math.radians(-45)
        bpy.data.objects["Wheel"].rotation_euler.y = math.radians(30)
    if pose == "open":
        for o in bpy.data.objects:
            if o.name.startswith(("Bolt_", "Ring_", "Wheel", "Keyhole")):
                world = o.matrix_world.copy()
                o.parent = door
                o.matrix_world = world
    door.rotation_euler.z = math.radians(-70) if pose == "open" else 0
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    door.rotation_euler.z = 0
    for o in bpy.data.objects:
        if o.name.startswith(("Bolt_", "Ring_", "Wheel")):
            o.rotation_euler = (0, 0, 0)
    for name in ("Bolt_0", "Bolt_1"):
        bpy.data.objects[name].location.y += 0.18 if pose in ("progress", "open") else 0


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 760, 900
    world = scene.world or bpy.data.worlds.new("w"); scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.025, 0.05, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    for name, loc, energy, col in (("key", (-2.5, -3.5, 3.5), 1100, (1, 0.85, 0.7)), ("rim", (2.5, 2.5, 3), 700, (0.6, 0.7, 1)), ("fill", (2.5, -3, 1), 350, (1, 1, 1))):
        l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'AREA'))
        l.data.energy, l.data.size, l.data.color = energy, 2.5, col
        l.location = loc
        l.rotation_euler = (Vector((0, 0, CZ)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        scene.collection.objects.link(l)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam); scene.camera = cam
    cam.location = (1.1, -3.6, CZ + 0.5)
    cam.rotation_euler = (Vector((0, 0, CZ)) - cam.location).to_track_quat('-Z', 'Y').to_euler()


bpy.ops.wm.read_factory_settings(use_empty=True)
vault = build()
parts = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(art, "SM_VaultDoor.fbx"), use_selection=True,
                         path_mode='STRIP', embed_textures=False, bake_anim=False,
                         bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL')
print("EXPORTED SM_VaultDoor", sum(len(o.data.polygons) for o in parts), "faces,", ", ".join(sorted(o.name for o in parts)))
if preview:
    setup_render()
    for pose in ("closed", "progress", "open"):
        render(vault, os.path.join(preview, "vault_" + pose + ".png"), pose)
