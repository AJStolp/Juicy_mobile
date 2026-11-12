# Builds the alley road pieces:
#   Assets/Art/Alley/Textures/T_AlleyAsphalt_Albedo.png  tileable wet asphalt, sRGB
#   Assets/Art/Alley/Textures/T_AlleyAsphalt_Normal.png  tangent space normal map
#   Assets/Art/Alley/Textures/T_AlleyAsphalt_Mask.png    URP Lit metallic/smoothness (A = smoothness)
#   Assets/Art/Alley/Textures/T_AlleyPaintLine.png       worn yellow line with alpha
#   Assets/Art/Alley/SM_Manhole.fbx                       cast iron cover in a rusted frame
#
#   blender -b -P ArtSource/Alley/build_alley_road.py [-- --preview <dir>]
#
# Textures tile because every pattern is generated on a periodic grid (FFT
# filtered noise, wrap around cracks). The manhole's front faces Blender -Y
# like the other alley models and its top faces +Z; materials are M_CastIron
# and M_Rust, remapped to URP by AlleySceneBuilder.
import bpy, bmesh, math, os, sys
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
art = os.path.join(root, "Assets", "Art", "Alley")
tex = os.path.join(art, "Textures")
rng = np.random.default_rng(11)
N = 1024


def band_noise(size, low, high):
    """Periodic noise keeping spatial frequencies between low and high cycles per tile."""
    white = rng.standard_normal((size, size))
    f = np.fft.fftfreq(size) * size
    r = np.sqrt(f[:, None] ** 2 + f[None, :] ** 2)
    mask = np.exp(-((np.log2(np.maximum(r, 1e-6)) - np.log2((low + high) / 2)) ** 2) / 0.9)
    mask[(r < low * 0.5) | (r > high * 2)] = 0
    out = np.real(np.fft.ifft2(np.fft.fft2(white) * mask))
    return (out - out.mean()) / (out.std() + 1e-9)


def blur(img, sigma):
    size = img.shape[0]
    f = np.fft.fftfreq(size)
    g = np.exp(-2 * (math.pi * sigma) ** 2 * (f[:, None] ** 2 + f[None, :] ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(img) * g))


def cracks(size, count):
    canvas = np.zeros((size, size))
    for _ in range(count):
        x, y = rng.uniform(0, size, 2)
        angle = rng.uniform(0, math.tau)
        for _ in range(int(rng.uniform(120, 420))):
            angle += rng.normal(0, 0.35)
            x, y = (x + math.cos(angle) * 1.5) % size, (y + math.sin(angle) * 1.5) % size
            canvas[int(y), int(x)] = 1
            if rng.random() < 0.01:
                angle += rng.choice((-1, 1)) * 1.1
    return np.clip(blur(canvas, 0.9) * 3.5, 0, 1)


def save(name, rgba, colorspace):
    img = bpy.data.images.new(name, N if rgba.shape[0] == N else rgba.shape[1], rgba.shape[0], alpha=True, float_buffer=False)
    img.colorspace_settings.name = colorspace
    img.pixels.foreach_set(np.ascontiguousarray(rgba[::-1]).astype(np.float32).ravel())
    img.filepath_raw = os.path.join(tex, name + ".png")
    img.file_format = 'PNG'
    img.save()
    print("SAVED", name)


# ---- Asphalt -------------------------------------------------------------
broad = band_noise(N, 1, 4)          # big tonal variation and puddle placement
medium = band_noise(N, 8, 24)
grain = band_noise(N, 90, 260)       # aggregate texture
pebbles = np.clip(band_noise(N, 120, 300) - 1.6, 0, None)   # sparse light stones
crack = cracks(N, 9)
stains = np.clip(band_noise(N, 3, 8) - 0.9, 0, None)        # oil

height = 0.22 * grain + 0.45 * pebbles + 0.15 * medium - 1.6 * crack
puddle = np.clip((broad * 0.8 + medium * 0.2 - 0.8) * 1.6, 0, 1)
puddle = blur(puddle, 6.0)
height = height * (1 - 0.7 * puddle)          # puddles fill the low spots flat

tone = 0.115 + 0.018 * broad + 0.012 * medium + 0.03 * grain
tone = tone + 0.11 * np.clip(pebbles, 0, 1)
tone = tone * (1 - 0.55 * crack) * (1 - 0.35 * np.clip(stains, 0, 1)) * (1 - 0.12 * puddle)
tone = np.clip(tone, 0.02, 0.6)
albedo = np.stack([tone * 0.98, tone, tone * 1.04 + 0.006 * np.clip(stains, 0, 1), np.ones_like(tone)], -1)
save("T_AlleyAsphalt_Albedo", albedo, 'sRGB')

dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5
dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * 0.5
strength = 1.1
n = np.stack([-dx * strength, dy * strength, np.ones_like(height)], -1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
normal = np.concatenate([n * 0.5 + 0.5, np.ones_like(height)[..., None]], -1)
save("T_AlleyAsphalt_Normal", normal, 'Non-Color')

smooth = np.clip(0.28 + 0.6 * puddle + 0.12 * np.clip(stains, 0, 1) - 0.2 * crack, 0.05, 0.95)
zero = np.zeros_like(smooth)
save("T_AlleyAsphalt_Mask", np.stack([zero, zero, zero, smooth], -1), 'Non-Color')

# ---- Worn paint line (128 wide, 1024 long, tiles along its length) ----------
W = 128
wear = band_noise(N, 6, 40)[:, :W] + 0.6 * band_noise(N, 60, 200)[:, :W]
u = np.abs(np.linspace(-1, 1, W))[None, :]
edge = np.clip((0.82 - u) * 12, 0, 1)
alpha = np.clip(edge * np.clip(wear + 1.6, 0, 1), 0, 1)
line = np.stack([np.full_like(alpha, 0.85), np.full_like(alpha, 0.66), np.full_like(alpha, 0.12), alpha], -1)
img = bpy.data.images.new("T_AlleyPaintLine", W, N, alpha=True)
img.pixels.foreach_set(np.ascontiguousarray(line[::-1]).astype(np.float32).ravel())
img.filepath_raw = os.path.join(tex, "T_AlleyPaintLine.png")
img.file_format = 'PNG'
img.save()
print("SAVED T_AlleyPaintLine")


# ---- Manhole --------------------------------------------------------------
def material(name, color, metal, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    return m


def ring(bm, r_in, r_out, z0, z1, segments=64):
    def loop(r, z):
        return [bm.verts.new((r * math.cos(i * math.tau / segments), r * math.sin(i * math.tau / segments), z)) for i in range(segments)]
    io, oo, it, ot = loop(r_in, z0), loop(r_out, z0), loop(r_in, z1), loop(r_out, z1)
    for i in range(segments):
        j = (i + 1) % segments
        bm.faces.new((ot[i], ot[j], it[j], it[i]))        # top
        bm.faces.new((oo[i], oo[j], ot[j], ot[i]))        # outer wall
        bm.faces.new((it[i], it[j], io[j], io[i]))        # inner wall


def mesh_object(name, bm, mat):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = False
    return o


bpy.ops.wm.read_factory_settings(use_empty=True)
iron = material("M_CastIron", (0.1, 0.1, 0.11), 0.75, 0.42)
rust = material("M_Rust", (0.3, 0.15, 0.07), 0.2, 0.8)

bm = bmesh.new()
bmesh.ops.create_circle(bm, cap_ends=True, segments=64, radius=0.36)
disc = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
bmesh.ops.translate(bm, vec=(0, 0, 0.03), verts=[v for v in disc["geom"] if isinstance(v, bmesh.types.BMVert)])
ring(bm, 0.335, 0.355, 0.03, 0.042)       # raised outer rim
ring(bm, 0.11, 0.135, 0.03, 0.042)        # inner ring
ring(bm, 0.0, 0.05, 0.03, 0.038, 32)      # centre boss
# Diamond grip studs between the rings.
step = 0.044
for gx in np.arange(-0.33, 0.34, step):
    for gy in np.arange(-0.33, 0.34, step):
        r = math.hypot(gx, gy)
        if r < 0.16 or r > 0.31: continue
        s = 0.013
        base = [bm.verts.new((gx + dx, gy + dy, 0.03)) for dx, dy in ((s, 0), (0, s), (-s, 0), (0, -s))]
        top = [bm.verts.new((gx + dx * 0.6, gy + dy * 0.6, 0.041)) for dx, dy in ((s, 0), (0, s), (-s, 0), (0, -s))]
        bm.faces.new(top)
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((base[i], base[j], top[j], top[i]))
# Radial ribs across the inner field.
for k in range(8):
    a = k * math.tau / 8
    c, sn = math.cos(a), math.sin(a)
    pts = []
    for rr, z in ((0.05, 0.03), (0.11, 0.03), (0.11, 0.04), (0.05, 0.04)):
        for w in (-0.008, 0.008):
            pts.append((rr * c - w * sn, rr * sn + w * c, z))
    vs = [bm.verts.new(p) for p in pts]
    bmesh.ops.convex_hull(bm, input=vs)
cover = mesh_object("SM_Manhole", bm, iron)

bm = bmesh.new()
ring(bm, 0.362, 0.45, -0.01, 0.022)       # frame the cover sits in
frame = mesh_object("Frame", bm, rust)
frame.parent = cover

if preview:
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 700, 600
    for name, loc, e in (("key", (-1.5, -1.5, 2), 300), ("rim", (1.5, 1.2, 1), 150)):
        l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'AREA'))
        l.data.energy, l.data.size = e, 1.5
        l.location = loc
        l.rotation_euler = (-__import__("mathutils").Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        scene.collection.objects.link(l)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam); scene.camera = cam
    cam.location = (0.0, -0.9, 0.85)
    cam.rotation_euler = (-cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(preview, "SM_Manhole.png")
    bpy.ops.render.render(write_still=True)

bpy.ops.object.select_all(action='DESELECT')
cover.select_set(True); frame.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(art, "SM_Manhole.fbx"), use_selection=True,
                         path_mode='STRIP', embed_textures=False, bake_anim=False,
                         bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL')
print("EXPORTED SM_Manhole", len(cover.data.polygons) + len(frame.data.polygons), "faces")
