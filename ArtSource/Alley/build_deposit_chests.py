# Builds the crimson and indigo deposit chests from scratch and exports them as
# Assets/Art/Alley/SM_DepositChest_Crimson.fbx and SM_DepositChest_Indigo.fbx.
# Replaces the older boxy chests from ArtSource/Heroes/SM_DepositChests.blend.
#
#   blender -b -P ArtSource/Alley/build_deposit_chests.py [-- --preview <dir>]
#
# Each chest is a body mesh with a child "Lid" whose origin sits on the back
# hinge, so the game can swing it open. Front faces Blender -Y like the other
# alley models. Material names are fixed; AlleySceneBuilder remaps them to URP:
#   M_Gunmetal, M_BrushedSteel, M_ChestGold, M_Chrome, M_Keyhole, M_<Color>Glow
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
art = os.path.join(root, "Assets", "Art", "Alley")

W, D = 1.2, 0.9            # body width (x) and depth (y)
FOOT = 0.06                # feet lift the body
BODY_H = 0.58
LID_H = 0.2
FRONT = -D / 2             # front face y
COLORS = {"Crimson": (1.0, 0.08, 0.12), "Indigo": (0.2, 0.32, 1.0)}


def material(name, color, metal, rough, emit=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1)
        bsdf.inputs["Emission Strength"].default_value = 6.0
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


def box(name, mat, size, loc, bevel=0.0, segments=3):
    return obj(lambda: bpy.ops.mesh.primitive_cube_add(size=1), name, mat, loc, scale=size, bevel=bevel, segments=segments)


def cyl(name, mat, r, depth, loc, rot=(0, 0, 0), verts=32, bevel=0.0):
    return obj(lambda: bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth), name, mat, loc, rot, bevel=bevel, segments=2)


def sphere(name, mat, r, loc):
    return obj(lambda: bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=r), name, mat, loc)


def rivet(mat, x, z, y=FRONT):
    return sphere("rivet", mat, 0.014, (x, y - 0.004, z))


def gem(name, mat, loc, size):
    # Brilliant cut facing the front (-Y): a flat table, an 8 sided girdle and
    # a pointed pavilion behind it.
    bm = bmesh.new()
    girdle = [bm.verts.new((math.cos(i * math.tau / 8), 0, math.sin(i * math.tau / 8))) for i in range(8)]
    table = [bm.verts.new((0.55 * math.cos(i * math.tau / 8), -0.32, 0.55 * math.sin(i * math.tau / 8))) for i in range(8)]
    culet = bm.verts.new((0, 0.75, 0))
    bm.faces.new(list(reversed(table)))
    for i in range(8):
        j = (i + 1) % 8
        bm.faces.new((girdle[i], girdle[j], table[j], table[i]))
        bm.faces.new((girdle[j], girdle[i], culet))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh); bm.free()
    top = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(top)
    top.location = loc
    top.scale = (size, size, size * 1.2)
    bpy.context.view_layer.objects.active = top
    bpy.ops.object.select_all(action='DESELECT')
    top.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    top.data.materials.append(mat)
    return top


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    return o


def build(color_name, rgb):
    gunmetal = material("M_Gunmetal", (0.16, 0.17, 0.19), 0.85, 0.32)
    steel = material("M_BrushedSteel", (0.55, 0.57, 0.6), 0.9, 0.38)
    gold = material("M_ChestGold", (1.0, 0.72, 0.22), 1.0, 0.18)
    chrome = material("M_Chrome", (0.92, 0.93, 0.95), 1.0, 0.06)
    dark = material("M_Keyhole", (0.012, 0.013, 0.016), 0.5, 0.5)
    glow = material("M_" + color_name + "Glow", rgb, 0.0, 0.25, rgb)

    base_z = FOOT
    top_z = base_z + BODY_H
    body = [box("body", gunmetal, (W, D, BODY_H), (0, 0, base_z + BODY_H / 2), bevel=0.035)]

    # Steel straps wrapping front, bottom and back, with rivet rows.
    for x in (-0.36, 0.36):
        body.append(box("strap", steel, (0.08, D + 0.024, BODY_H + 0.02), (x, 0, base_z + BODY_H / 2), bevel=0.008, segments=2))
        for z in (base_z + 0.09, base_z + 0.29, base_z + 0.49):
            body.append(rivet(gold, x, z, FRONT - 0.012))
            body.append(rivet(gold, x, z, -FRONT + 0.028))

    # Recessed front panel with a gold frame.
    panel_w, panel_h, panel_z = 0.5, 0.42, base_z + BODY_H / 2
    body.append(box("panel", dark, (panel_w, 0.01, panel_h), (0, FRONT - 0.002, panel_z)))
    for dx, dz, sx, sz in ((0, panel_h / 2, panel_w + 0.05, 0.025), (0, -panel_h / 2, panel_w + 0.05, 0.025),
                           (panel_w / 2, 0, 0.025, panel_h + 0.05), (-panel_w / 2, 0, 0.025, panel_h + 0.05)):
        body.append(box("frame", gold, (sx, 0.022, sz), (dx, FRONT - 0.008, panel_z + dz), bevel=0.006, segments=2))

    # Vault dial: gold ring, dark face, tick marks, three spoke spinner.
    dial_y = FRONT - 0.02
    body.append(cyl("dial_ring", gold, 0.13, 0.03, (0, dial_y, panel_z), (math.radians(90), 0, 0), 48, bevel=0.006))
    body.append(cyl("dial_face", dark, 0.105, 0.034, (0, dial_y - 0.002, panel_z), (math.radians(90), 0, 0), 48))
    for i in range(24):
        a = i * math.tau / 24
        long = i % 6 == 0
        r = 0.088
        body.append(box("tick", chrome, (0.006, 0.006, 0.02 if long else 0.011),
                        (math.sin(a) * r, dial_y - 0.02, panel_z + math.cos(a) * r)))
        body[-1].rotation_euler = (0, a, 0)
    # Vault wheel: chrome rim and hub, four spokes with gold knobs.
    wheel_y = dial_y - 0.045
    body.append(obj(lambda: bpy.ops.mesh.primitive_torus_add(major_radius=0.125, minor_radius=0.011, major_segments=48, minor_segments=10),
                    "wheel_rim", chrome, (0, wheel_y, panel_z), (math.radians(90), 0, 0)))
    body.append(cyl("hub", chrome, 0.034, 0.05, (0, dial_y - 0.03, panel_z), (math.radians(90), 0, 0), 32, bevel=0.005))
    for i in range(4):
        a = i * math.tau / 4 + math.radians(45)
        spoke = cyl("spoke", chrome, 0.01, 0.3, (0, wheel_y, panel_z), (0, a, 0), 12)
        body.append(spoke)
        body.append(sphere("knob", gold, 0.02, (math.sin(a) * 0.15, wheel_y, panel_z + math.cos(a) * 0.15)))
    body.append(gem("emblem", glow, (0, dial_y - 0.004, panel_z + 0.165), 0.03))

    # Gold corner guards, feet and side handles.
    for x in (-W / 2, W / 2):
        for y in (-D / 2, D / 2):
            body.append(box("corner", gold, (0.1, 0.1, BODY_H + 0.01), (x, y, base_z + BODY_H / 2), bevel=0.02))
            body.append(cyl("foot", gold, 0.05, FOOT, (x * 0.88, y * 0.85, FOOT / 2), verts=24, bevel=0.01))
        handle = obj(lambda: bpy.ops.mesh.primitive_torus_add(major_radius=0.1, minor_radius=0.016, major_segments=32, minor_segments=10),
                     "handle", chrome, (x + math.copysign(0.03, x), 0, base_z + BODY_H * 0.62), (0, math.radians(90), 0))
        body.append(handle)
        body.append(box("handle_mount", gold, (0.03, 0.26, 0.06), (x + math.copysign(0.012, x), 0, base_z + BODY_H * 0.62), bevel=0.008))

    chest = join(body, "SM_DepositChest_" + color_name)
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    # Lid: beveled slab, glowing seam, chrome framed deposit slot, emblem.
    lid_z = top_z + LID_H / 2
    lid = [box("lid", gunmetal, (W + 0.02, D + 0.02, LID_H), (0, 0, lid_z), bevel=0.05, segments=4)]
    lid.append(box("seam", glow, (W + 0.012, D + 0.012, 0.016), (0, 0, top_z + 0.006)))
    lid.append(box("slot_bezel", chrome, (0.62, 0.13, 0.03), (0, 0, top_z + LID_H + 0.005), bevel=0.012))
    lid.append(box("slot", glow, (0.54, 0.05, 0.034), (0, 0, top_z + LID_H + 0.006)))
    for x in (-W / 2, W / 2):
        for y in (-D / 2, D / 2):
            lid.append(box("lid_corner", gold, (0.12, 0.12, LID_H + 0.02), (x, y, lid_z), bevel=0.025))
    for x in (-0.36, 0.36):
        lid.append(box("lid_strap", steel, (0.08, D + 0.04, 0.02), (x, 0, top_z + LID_H + 0.002), bevel=0.006, segments=2))
    lid.append(box("latch", gold, (0.14, 0.03, 0.09), (0, FRONT - 0.02, top_z + 0.02), bevel=0.01))
    lid.append(gem("lid_emblem", glow, (0, FRONT - 0.03, lid_z + 0.03), 0.045))
    lid_obj = join(lid, "Lid")
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    # Hinge at the back top edge so the lid swings open.
    hinge = Vector((0, D / 2 + 0.01, top_z))
    lid_obj.data.transform(Matrix.Translation(-hinge))
    lid_obj.location = hinge
    lid_obj.parent = chest
    return chest, lid_obj


def render(chest, path):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 900, 760
    world = scene.world or bpy.data.worlds.new("w"); scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.025, 0.05, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    for name, loc, energy, col in (("key", (-2.5, -3, 3.5), 900, (1, 0.85, 0.7)), ("rim", (2.5, 2.5, 2.5), 700, (0.6, 0.7, 1)), ("fill", (2.5, -2.5, 1), 250, (1, 1, 1))):
        l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'AREA'))
        l.data.energy, l.data.size, l.data.color = energy, 2.0, col
        l.location = loc
        l.rotation_euler = (Vector((0, 0, 0.4)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        scene.collection.objects.link(l)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam); scene.camera = cam
    cam.location = (chest.location.x + 1.35, -2.4, 1.55)
    cam.rotation_euler = (Vector((chest.location.x, 0, 0.42)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
for color_name, rgb in COLORS.items():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    chest, lid = build(color_name, rgb)
    bpy.ops.object.select_all(action='DESELECT')
    chest.select_set(True); lid.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(art, chest.name + ".fbx"), use_selection=True,
                             path_mode='STRIP', embed_textures=False, bake_anim=False,
                             bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL')
    print("EXPORTED", chest.name, len(chest.data.polygons) + len(lid.data.polygons), "faces")
    if preview:
        render(chest, os.path.join(preview, chest.name + ".png"))
