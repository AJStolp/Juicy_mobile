# Builds the Plinko board and its coin, exported as
# Assets/Art/Alley/SM_PlinkoBoard.fbx and SM_PlinkoCoin.fbx. A worn pachinko
# style board: dark steel back, brass frame, rivet pins, nine slots.
#
#   blender -b -P ArtSource/Alley/build_plinko_board.py [-- --preview <dir>]
#
# Front faces Blender -Y like the other alley models. The board origin is the
# bottom center of its back. Children the game builds physics from, by name:
#   Pin_<row>_<n>    a pin; gets a capsule collider
#   Divider_<n>      a slot wall; gets a box collider
#   Rail_L, Rail_R   the side walls; get box colliders
#   Floor            the slot floor; gets a box collider
#   VaultSlot        the glowing floor of the middle slot
# The layout numbers below are mirrored in AlleyPlinko (SlotWidth, DropHeight,
# SlotTop, AimHalfWidth); change both together.
# Material names are fixed; AlleySceneBuilder remaps them to URP:
#   M_PlinkoBack, M_PlinkoBrass, M_PlinkoPin, M_PlinkoCoin, M_PlinkoVaultGlow
import bpy, math, os, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
art = os.path.join(root, "Assets", "Art", "Alley")

SLOTS = 9
SLOT_W = 0.12              # slot and pin spacing
INNER_W = SLOTS * SLOT_W   # 1.08
FLOOR_Z = 0.1
DIVIDER_H = 0.24
PIN_TOP = 1.32
PIN_ROWS = 8
ROW_GAP = 0.11
DROP_Z = 1.48              # the aim rail, where coins are released
AIM_HALF = 0.3             # coins drop only from the middle of the rail
H = 1.62
DEPTH = 0.08               # how far pins and walls stand off the back
PIN_R = 0.013
COIN_R = 0.032             # coin radius plus pin radius (0.045) must stay clear of half the pin spacing (0.06), or a coin lands dead on the next pin and walks to a rail
                           # the Unity colliders use these same radii (CoinRadius, PinRadius in AlleySceneBuilder.Heist.cs)


def material(name, color, metal, rough, emit=None, strength=3.0):
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


def box(name, mat, size, loc, bevel=0.0, segments=2):
    return obj(lambda: bpy.ops.mesh.primitive_cube_add(size=1), name, mat, loc, scale=size, bevel=bevel, segments=segments)


def cyl(name, mat, r, depth, loc, verts=20, bevel=0.0):
    return obj(lambda: bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth), name, mat, loc,
               (math.radians(90), 0, 0), bevel=bevel)


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    return o


def parent(child, board):
    child.parent = board
    child.matrix_parent_inverse = board.matrix_world.inverted()


def build_board():
    back = material("M_PlinkoBack", (0.06, 0.07, 0.09), 0.6, 0.55)
    brass = material("M_PlinkoBrass", (0.95, 0.66, 0.25), 1.0, 0.3)
    pin = material("M_PlinkoPin", (0.82, 0.84, 0.88), 1.0, 0.22)
    glow = material("M_PlinkoVaultGlow", (0.35, 1.0, 0.45), 0.0, 0.3, (0.35, 1.0, 0.45))

    W = INNER_W + 0.2
    shell = [box("back", back, (W, 0.04, H), (0, 0.02, H / 2), bevel=0.02)]
    # Brass trim: top arch bar, rivets along the frame.
    shell.append(box("top", brass, (W, DEPTH + 0.02, 0.08), (0, -DEPTH / 2, H - 0.04), bevel=0.02))
    # The aim rail spans only the drop zone, with stops at each end.
    shell.append(box("aim_rail", pin, (2 * AIM_HALF + 2 * COIN_R, 0.02, 0.018), (0, -DEPTH - 0.01, DROP_Z + 0.06), bevel=0.006))
    for x in (-AIM_HALF - COIN_R, AIM_HALF + COIN_R):
        shell.append(box("aim_stop", brass, (0.02, 0.04, 0.09), (x, -DEPTH - 0.01, DROP_Z + 0.04), bevel=0.006))
    for i in range(12):
        z = 0.15 + i * (H - 0.3) / 11
        for x in (-W / 2 + 0.04, W / 2 - 0.04):
            shell.append(cyl("rivet", brass, 0.012, 0.01, (x, -DEPTH - 0.012, z), 12))
    board = join(shell, "SM_PlinkoBoard")

    def child(o):
        parent(o, board)
        return o

    child(box("Rail_L", brass, (0.06, DEPTH, H - 0.08), (-INNER_W / 2 - 0.03, -DEPTH / 2, (H - 0.08) / 2), bevel=0.012))
    child(box("Rail_R", brass, (0.06, DEPTH, H - 0.08), (INNER_W / 2 + 0.03, -DEPTH / 2, (H - 0.08) / 2), bevel=0.012))
    child(box("Floor", brass, (INNER_W, DEPTH, FLOOR_Z), (0, -DEPTH / 2, FLOOR_Z / 2), bevel=0.01))
    child(box("VaultSlot", glow, (SLOT_W - 0.016, 0.03, 0.012), (0, -DEPTH / 2, FLOOR_Z + 0.006)))
    for k in range(SLOTS + 1):
        x = (k - SLOTS / 2) * SLOT_W
        if k in (0, SLOTS):
            continue
        child(box("Divider_%d" % k, brass, (0.012, DEPTH, DIVIDER_H), (x, -DEPTH / 2, FLOOR_Z + DIVIDER_H / 2), bevel=0.004))
    for r in range(PIN_ROWS):
        z = PIN_TOP - r * ROW_GAP
        count = SLOTS if r % 2 == 0 else SLOTS - 1
        offset = (count - 1) / 2
        for n in range(count):
            x = (n - offset) * SLOT_W
            # Edge pins on even rows would leave a gap to the rail narrower
            # than a coin, where coins wedge. Push them against the rail so
            # they deflect inward instead.
            if abs(x) > INNER_W / 2 - 0.1:
                x = math.copysign(INNER_W / 2 - PIN_R, x)
            p = cyl("Pin_%d_%d" % (r, n), pin, PIN_R, DEPTH, (x, -DEPTH / 2, z), 16)
            head = cyl("head", brass, PIN_R * 1.8, 0.01, (x, -DEPTH - 0.004, z), 16)
            p = join([p, head], "Pin_%d_%d" % (r, n))
            child(p)
    return board


def build_coin():
    gold = material("M_PlinkoCoin", (1.0, 0.75, 0.25), 1.0, 0.22)
    face = cyl("face", gold, COIN_R, 0.012, (0, 0, 0), 40, bevel=0.003)
    rim = obj(lambda: bpy.ops.mesh.primitive_torus_add(major_radius=COIN_R * 0.85, minor_radius=0.0035, major_segments=40, minor_segments=8),
              "rim", gold, (0, -0.006, 0), (math.radians(90), 0, 0))
    star = box("mark", gold, (0.022, 0.004, 0.022), (0, -0.007, 0))
    star.rotation_euler = (0, math.radians(45), 0)
    return join([face, rim, star], "SM_PlinkoCoin")


def export(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(art, name + ".fbx"), use_selection=True,
                             path_mode='STRIP', embed_textures=False, bake_anim=False,
                             bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL')


def render(path):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 760, 1000
    world = scene.world or bpy.data.worlds.new("w"); scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.025, 0.05, 1)
    for name, loc, energy in (("key", (-2, -3, 2.5), 900), ("fill", (2, -3, 1), 400)):
        l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'AREA'))
        l.data.energy, l.data.size = energy, 2.5
        l.location = loc
        l.rotation_euler = (Vector((0, 0, 0.8)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        scene.collection.objects.link(l)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam); scene.camera = cam
    cam.location = (0.5, -3.2, 1.1)
    cam.rotation_euler = (Vector((0, 0, 0.8)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
board = build_board()
parts = [board] + list(board.children)
export(parts, "SM_PlinkoBoard")
print("EXPORTED SM_PlinkoBoard", len(parts), "objects,", sum(1 for o in parts if o.name.startswith("Pin_")), "pins")
coin = build_coin()
coin.location = (0.2, -0.12, DROP_Z - 0.3)
if preview:
    render(os.path.join(preview, "plinko.png"))
coin.location = (0, 0, 0)
export([coin], "SM_PlinkoCoin")
print("EXPORTED SM_PlinkoCoin")
