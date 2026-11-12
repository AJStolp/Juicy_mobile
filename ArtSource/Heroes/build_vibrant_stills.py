# Retints the hero gem and coin blend and renders neon stills.
# Leaves SM_GemsCoins.blend untouched. Writes a copy and the PNGs.
import os
import sys

import bpy
from mathutils import Vector

SRC = "/Users/astolp/Desktop/Dev/JuicySortGame/ArtSource/Heroes/SM_GemsCoins.blend"
OUT_BLEND = "/Users/astolp/Desktop/Dev/JuicySortGame/ArtSource/Heroes/SM_VibrantGems.blend"
OUT_DIR = "/Users/astolp/Desktop/Dev/JuicySortGame/ArtSource/Heroes/Export"
UI_DIR = "/Users/astolp/Desktop/Dev/JuicySortGame/Assets/Resources/UIHeroes"
THEME_DIR = "/Users/astolp/Desktop/Dev/JuicySortGame/Assets/Art/Themes/Exported"
THEME_NAMES = {
    "gem_ruby": "icon_gem_ruby_albedo.png",
    "gem_sapphire": "icon_gem_sapphire_albedo.png",
    "gem_emerald": "icon_gem_emerald_albedo.png",
    "gem_topaz": "icon_gem_topaz_albedo.png",
    "coins_hero": "icon_coins_albedo.png",
}

QUICK = "--quick" in sys.argv

GEMS = (
    ("SM_Gem_Ruby", "gem_ruby", (0.92, 0.02, 0.08, 1.0), (1.0, 0.05, 0.14, 1.0), 0.22, 3.2),
    ("SM_Gem_Sapphire", "gem_sapphire", (0.02, 0.14, 0.85, 1.0), (0.1, 0.35, 1.0, 1.0), 0.2, 3.0),
    ("SM_Gem_Emerald", "gem_emerald", (0.02, 0.72, 0.18, 1.0), (0.15, 1.0, 0.35, 1.0), 0.18, 2.8),
    ("SM_Gem_Topaz", "gem_topaz", (0.95, 0.48, 0.02, 1.0), (1.0, 0.62, 0.08, 1.0), 0.16, 2.4),
)


def principled(mat):
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return bsdf


def paint_gem(shell_name, core_name, base, emission, shell_glow, core_glow):
    shell = principled(bpy.data.materials[shell_name])
    shell.inputs["Base Color"].default_value = base
    shell.inputs["Metallic"].default_value = 0.0
    shell.inputs["Roughness"].default_value = 0.035
    shell.inputs["IOR"].default_value = 1.85
    shell.inputs["Transmission Weight"].default_value = 0.82
    shell.inputs["Specular IOR Level"].default_value = 1.0
    shell.inputs["Coat Weight"].default_value = 0.25
    shell.inputs["Coat Roughness"].default_value = 0.04
    shell.inputs["Emission Color"].default_value = emission
    shell.inputs["Emission Strength"].default_value = shell_glow

    core = principled(bpy.data.materials[core_name])
    core.inputs["Base Color"].default_value = emission
    core.inputs["Roughness"].default_value = 0.08
    core.inputs["Emission Color"].default_value = emission
    core.inputs["Emission Strength"].default_value = core_glow
    core.inputs["Transmission Weight"].default_value = 0.15


def paint_coins():
    coin = principled(bpy.data.materials["M_CoinGold"])
    coin.inputs["Base Color"].default_value = (1.0, 0.78, 0.08, 1.0)
    coin.inputs["Metallic"].default_value = 1.0
    coin.inputs["Roughness"].default_value = 0.14
    coin.inputs["Specular IOR Level"].default_value = 1.0
    coin.inputs["Coat Weight"].default_value = 0.35
    coin.inputs["Emission Color"].default_value = (1.0, 0.68, 0.08, 1.0)
    coin.inputs["Emission Strength"].default_value = 0.85


def hide_all():
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj.hide_render = True


def show(name):
    obj = bpy.data.objects[name]
    obj.hide_render = False
    return obj


def aim(cam, focus):
    bpy.context.view_layer.update()
    objects = [focus]
    core_name = focus.name + "_Core"
    if core_name in bpy.data.objects and not bpy.data.objects[core_name].hide_render:
        objects.append(bpy.data.objects[core_name])
    corners = []
    for obj in objects:
        corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    center = sum(corners, Vector()) / len(corners)
    radius = max((corner - center).length for corner in corners)
    distance = max(0.35, radius * 4.4)
    cam.location = center + Vector((distance * 0.42, -distance, distance * 0.32))
    direction = center - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 70


def prepare_scene():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "GPU"
    scene.cycles.samples = 8 if QUICK else 48
    scene.cycles.use_denoising = True
    scene.render.film_transparent = True
    scene.render.resolution_x = 512 if QUICK else 2048
    scene.render.resolution_y = 512 if QUICK else 2048
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "16"
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    scene.view_settings.look = "None"
    scene.view_settings.exposure = -0.15
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "METAL"
    prefs.get_devices()
    for device in prefs.devices:
        device.use = True
    for obj in bpy.data.objects:
        if obj.type == "LIGHT" and obj.data is not None:
            obj.data.energy *= 0.48

    cam_data = bpy.data.cameras.new("VibrantCam")
    cam_data.lens = 70
    cam = bpy.data.objects.new("VibrantCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def render_one(cam, filename, focus):
    aim(cam, focus)
    path = os.path.join(OUT_DIR, filename + ".png")
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    data = open(path, "rb").read()
    with open(os.path.join(UI_DIR, filename + ".png"), "wb") as dst:
        dst.write(data)
    theme_name = THEME_NAMES.get(filename)
    if theme_name:
        with open(os.path.join(THEME_DIR, theme_name), "wb") as dst:
            dst.write(data)
    print("WROTE", path)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    paint_coins()
    for mesh_name, _file, base, emission, shell_glow, core_glow in GEMS:
        paint_gem(mesh_name.replace("SM_Gem_", "M_Gem_"), mesh_name.replace("SM_Gem_", "M_Gem_") + "_Core",
                  base, emission, shell_glow, core_glow)

    cam = prepare_scene()
    jobs = [(name, filename, name) for name, filename, *_rest in GEMS]
    jobs.append(("SM_CoinStack", "coins_hero", "SM_CoinStack"))
    if QUICK:
        jobs = [jobs[0]]
    elif "--gems" in sys.argv:
        jobs = [job for job in jobs if job[0] != "SM_CoinStack"]

    for mesh_name, filename, focus_name in jobs:
        hide_all()
        focus = show(mesh_name)
        core_name = mesh_name + "_Core"
        if core_name in bpy.data.objects:
            show(core_name)
        print("BOUNDS", focus.name)
        render_one(cam, filename, focus)

    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND, copy=True)
    print("SAVED", OUT_BLEND)


if __name__ == "__main__":
    main()
