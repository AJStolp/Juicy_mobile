# Converts a Tripo or fal.ai GLB into the Alley import layout:
#   Assets/Art/Alley/<Name>.fbx and Assets/Art/Alley/Textures/<Name>_basecolor.jpg
# AlleySceneBuilder.AssignEnvironmentLit then builds the URP material.
#
#   blender -b -P ArtSource/Alley/import_glb.py -- <file.glb> <Name> [--cut x0 x1 y0 y1 z0 z1] [--untextured] [--preview dir]
#
# --untextured is for models colored at runtime, like the gem diamond; no
# texture is written and the scene code supplies the material.
#
# --cut deletes faces whose vertices all sit inside the box (glTF meters,
# Blender axes). Use it to strip generator artifacts such as stray fins.
import bpy, bmesh, sys, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, name = argv[0], argv[1]
cuts, preview, untextured = [], None, False
i = 2
while i < len(argv):
    if argv[i] == "--cut":
        cuts.append([float(v) for v in argv[i + 1:i + 7]]); i += 7
    elif argv[i] == "--untextured":
        untextured = True; i += 1
    elif argv[i] == "--preview":
        preview = argv[i + 1]; i += 2
    else:
        raise SystemExit("unknown arg " + argv[i])

root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
art = os.path.join(root, "Assets", "Art", "Alley")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if len(meshes) != 1:
    raise SystemExit("expected one mesh, found %d" % len(meshes))
obj = meshes[0]
obj.name = name

for box in cuts:
    lo, hi = Vector((box[0], box[2], box[4])), Vector((box[1], box[3], box[5]))
    bm = bmesh.new(); bm.from_mesh(obj.data)
    inside = lambda v: all(lo[k] <= (obj.matrix_world @ v.co)[k] <= hi[k] for k in range(3))
    doomed = [f for f in bm.faces if all(inside(v) for v in f.verts)]
    bmesh.ops.delete(bm, geom=doomed, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data); bm.free()
    print("CUT", box, "faces", len(doomed))

image = None
for mat in obj.data.materials:
    if mat and mat.use_nodes:
        for node in mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image:
                image = node.image
tex_path = None
if not untextured:
    if image is None:
        raise SystemExit("no base color image in " + src + "; pass --untextured if that is intended")
    tex_path = os.path.join(art, "Textures", name + "_basecolor.jpg")
    image.filepath_raw = tex_path
    image.file_format = 'JPEG'
    image.save()

# Bake the axis conversion into the mesh so the Unity root has no -90 X
# rotation; scene tools set root rotation directly and would tip it over.
bpy.ops.export_scene.fbx(filepath=os.path.join(art, name + ".fbx"), use_selection=False,
                         path_mode='STRIP', embed_textures=False, bake_anim=False,
                         bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL')
print("EXPORTED", name, tex_path)

if preview:
    os.makedirs(preview, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'TEXTURE'
    scene.render.resolution_x, scene.render.resolution_y = 900, 600
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    center = sum(pts, Vector()) / 8
    size = max((p - center).length for p in pts)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam); scene.camera = cam
    for view, d in {"side": (0, -1, 0.15), "quarter": (0.8, -0.8, 0.5)}.items():
        cam.location = center + Vector(d).normalized() * size * 2.6
        cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(preview, name + "_" + view + ".png")
        bpy.ops.render.render(write_still=True)
