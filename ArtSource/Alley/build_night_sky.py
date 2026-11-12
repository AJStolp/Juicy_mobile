# Builds the night sky layers for the alley:
#   Assets/Art/Alley/Textures/T_AlleyClouds_Far.png   dense moonlit cloud bank, behind the moon
#   Assets/Art/Alley/Textures/T_AlleyClouds_Near.png  thin wisps that drift across the moon
#   Assets/Art/Alley/Textures/T_AlleyMoonHalo.png      soft radial glow around the moon
#
#   blender -b -P ArtSource/Alley/build_night_sky.py
#
# Cloud textures tile horizontally (periodic noise) so the game can scroll them.
# RGB is the lit cloud colour, A is coverage. Tops catch cool moonlight and
# undersides fall off to deep navy.
import bpy, math, os, sys
import numpy as np

root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
tex = os.path.join(root, "Assets", "Art", "Alley", "Textures")
rng = np.random.default_rng(23)
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None


def periodic_noise(h, w, low, high):
    white = rng.standard_normal((h, w))
    fy = np.fft.fftfreq(h)[:, None] * h
    fx = np.fft.fftfreq(w)[None, :] * w
    r = np.sqrt(fx ** 2 + (fy * w / h) ** 2)
    band = np.exp(-((np.log2(np.maximum(r, 1e-6)) - np.log2((low + high) / 2)) ** 2) / 1.2)
    band[r < 0.5] = 0
    out = np.real(np.fft.ifft2(np.fft.fft2(white) * band))
    return (out - out.mean()) / (out.std() + 1e-9)


def fbm(h, w, base, octaves):
    total = np.zeros((h, w))
    amp = 1.0
    for i in range(octaves):
        f = base * 2 ** i
        total += amp * periodic_noise(h, w, f * 0.7, f * 1.4)
        amp *= 0.45
    return total / 1.6


def blur(img, sigma):
    h, w = img.shape
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    g = np.exp(-2 * (math.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(img) * g))


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def save(name, rgba):
    h, w = rgba.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(np.ascontiguousarray(rgba[::-1]).astype(np.float32).ravel())
    img.filepath_raw = os.path.join(tex, name + ".png")
    img.file_format = 'PNG'
    img.save()
    print("SAVED", name)


def clouds(name, h, w, base, cover, softness, vertical_bias, top_light, under):
    density = fbm(h, w, base, 4)
    v = np.linspace(1, 0, h)[:, None]                 # 1 at the top of the texture
    density = density + vertical_bias * (v - 0.5)      # thicker toward the top
    alpha = smoothstep(cover, cover + softness, density)
    # Light comes from above: compare with the density a few rows up.
    soft = blur(density, 5)
    above = np.roll(soft, 22, axis=0)
    lit = np.clip(0.5 + (soft - above) * 2.4, 0, 1)
    lit = lit * (0.7 + 0.3 * v)
    rgb = under[None, None, :] * (1 - lit[..., None]) + top_light[None, None, :] * lit[..., None]
    alpha = alpha * smoothstep(0.0, 0.18, v) * smoothstep(1.0, 0.85, v)   # fade out at both edges
    save(name, np.concatenate([rgb, alpha[..., None]], -1))
    if preview:
        sky = np.array([0.03, 0.04, 0.08])
        comp = sky * (1 - alpha[..., None]) + rgb * alpha[..., None]
        img = bpy.data.images.new(name + "_preview", w, h)
        img.pixels.foreach_set(np.ascontiguousarray(np.concatenate([comp, np.ones((h, w, 1))], -1)[::-1]).astype(np.float32).ravel())
        img.filepath_raw = os.path.join(preview, name + "_preview.png")
        img.file_format = 'PNG'
        img.save()


clouds("T_AlleyClouds_Far", 512, 2048, 2, cover=-0.35, softness=0.8, vertical_bias=0.5,
       top_light=np.array([0.42, 0.46, 0.62]), under=np.array([0.05, 0.06, 0.11]))
clouds("T_AlleyClouds_Near", 512, 2048, 3, cover=0.15, softness=0.75, vertical_bias=0.0,
       top_light=np.array([0.62, 0.67, 0.82]), under=np.array([0.12, 0.13, 0.2]))

# Moon halo: soft falloff with a faint ring, white so the material tints it.
n = 512
y, x = np.mgrid[-1:1:n * 1j, -1:1:n * 1j]
r = np.sqrt(x ** 2 + y ** 2)
glow = np.exp(-(r / 0.32) ** 2) * 0.85 + np.exp(-((r - 0.62) / 0.05) ** 2) * 0.12
glow *= smoothstep(1.0, 0.85, r)
save("T_AlleyMoonHalo", np.stack([np.ones_like(r), np.ones_like(r), np.ones_like(r), np.clip(glow, 0, 1)], -1))
