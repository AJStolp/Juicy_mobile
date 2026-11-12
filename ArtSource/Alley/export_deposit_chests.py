# Exports the crimson and indigo deposit chests from ArtSource/Heroes/SM_DepositChests.blend
# as Assets/Art/Alley/SM_DepositChest_Crimson.fbx and SM_DepositChest_Indigo.fbx.
# Each chest is joined into one mesh, its origin at the bottom center, front
# facing Blender -Y like the other alley models. Materials keep their names
# (M_BrushedSteel, M_ChestGold, M_Keyhole, M_CrimsonGlow, M_IndigoGlow); the
# alley builder remaps them to URP materials.
#
#   blender -b ArtSource/Heroes/SM_DepositChests.blend -P ArtSource/Alley/export_deposit_chests.py
import bpy, os
from mathutils import Vector

root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
art = os.path.join(root, "Assets", "Art", "Alley")

for m in bpy.data.materials:
    if not m.node_tree: continue
    bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None: continue
    base = tuple(round(v, 3) for v in bsdf.inputs['Base Color'].default_value[:3])
    emit = bsdf.inputs.get('Emission Color')
    emit = tuple(round(v, 3) for v in emit.default_value[:3]) if emit else None
    strength = round(bsdf.inputs['Emission Strength'].default_value, 2) if 'Emission Strength' in bsdf.inputs else 0
    print("MATERIAL", m.name, "base", base, "metal", round(bsdf.inputs['Metallic'].default_value, 2),
          "rough", round(bsdf.inputs['Roughness'].default_value, 2), "emit", emit, strength)

for color in ("Crimson", "Indigo"):
    bpy.ops.object.select_all(action='DESELECT')
    parts = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("SM_Chest_" + color + "_")]
    copies = []
    for o in parts:
        c = o.copy(); c.data = o.data.copy()
        bpy.context.scene.collection.objects.link(c)
        copies.append(c)
    for c in copies: c.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    chest = bpy.context.view_layer.objects.active
    chest.name = "SM_DepositChest_" + color
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    pts = [chest.matrix_world @ v.co for v in chest.data.vertices]
    bottom = Vector(((min(p.x for p in pts) + max(p.x for p in pts)) / 2,
                     (min(p.y for p in pts) + max(p.y for p in pts)) / 2,
                     min(p.z for p in pts)))
    for v in chest.data.vertices:
        v.co = chest.matrix_world.inverted() @ ((chest.matrix_world @ v.co) - bottom)
    chest.location = (0, 0, 0)
    bpy.ops.object.select_all(action='DESELECT')
    chest.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(art, chest.name + ".fbx"), use_selection=True,
                             path_mode='STRIP', embed_textures=False, bake_anim=False,
                             bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL')
    print("EXPORTED", chest.name)
