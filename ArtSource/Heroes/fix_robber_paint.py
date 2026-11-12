# Paints the robber's missing glove and black eyes on the locked drawing.
# The paper-doll slices share those pixels, so the same paint lands on each file.
# Run: blender --background --python ArtSource/Heroes/fix_robber_paint.py
import array
import os

ROOT = "/Users/astolp/Desktop/Dev/JuicySortGame"
HEROES = os.path.join(ROOT, "Assets/Resources/UIHeroes")
SOURCE = "/Users/astolp/Downloads/individual-theme-assets/characters/robber.jpg"
REFERENCE = os.path.join(ROOT, "ArtSource/Themes/References/robber.jpg")

# hand_box limits the glove to the sack hand. None skips the glove.
JOBS = (
    (os.path.join(HEROES, "Robber/head.png"), None, True),
    (os.path.join(HEROES, "heist_robber.png"), (325, 140, 430, 430), True),
    (os.path.join(HEROES, "Robber/armFront.png"), (0, 0, 107, 190), False),
    (os.path.join(HEROES, "Robber/bag.png"), (0, 140, 120, 300), False),
    (SOURCE, (330, 250, 530, 580), True),
)


def luma(r, g, b):
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def glove_color(t):
    # Shadow, mid, and highlight taken from the fist that already has a glove.
    shadow = (34, 22, 16)
    mid = (62, 42, 30)
    hi = (96, 70, 48)
    if t < 0.55:
        u = t / 0.55
        a, b = shadow, mid
    else:
        u = (t - 0.55) / 0.45
        a, b = mid, hi
    return tuple(int(a[i] + (b[i] - a[i]) * u) for i in range(3))


def is_skin(r, g, b, a):
    if a < 30 or r < 145 or b > 165:
        return False
    if r - g < 36 or g - b < 16 or g < 70:
        return False
    return True


def is_eye_white(r, g, b, a):
    if a < 40:
        return False
    return r > 200 and g > 185 and b > 165 and abs(r - g) < 40 and r - b < 55 and b > r - 40


def components(points):
    pending = set(points)
    found = []
    while pending:
        start = pending.pop()
        stack = [start]
        comp = []
        while stack:
            x, y = stack.pop()
            comp.append((x, y))
            for nxt in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if nxt in pending:
                    pending.remove(nxt)
                    stack.append(nxt)
        found.append(comp)
    return found


def bounds(comp):
    xs = [p[0] for p in comp]
    ys = [p[1] for p in comp]
    return min(xs), min(ys), max(xs), max(ys)


def near(a, b, pad):
    ax0, ay0, ax1, ay1 = bounds(a)
    bx0, by0, bx1, by1 = bounds(b)
    return not (ax1 + pad < bx0 or bx1 + pad < ax0 or ay1 + pad < by0 or by1 + pad < ay0)


def eye_bodies(points):
    # Bright specks on the ear and the sack are not eyes. Keep the two eyes
    # and the white scraps the pupil splits off from them.
    comps = components(points)
    seeds = [comp for comp in comps if len(comp) > 200]
    if not seeds:
        return []
    kept = []
    for comp in comps:
        if any(near(comp, seed, 8) for seed in seeds):
            kept.extend(comp)
    return kept


def split_eyes(points):
    if not points:
        return []
    xs = [p[0] for p in points]
    gap_x, gap = min(xs), -1
    occupied = set(xs)
    for x in range(min(xs), max(xs)):
        if x not in occupied and (x - 1) in occupied:
            run = 0
            while x + run not in occupied and x + run <= max(xs):
                run += 1
            if run > gap:
                gap, gap_x = run, x
    sides = [[], []]
    for x, y in points:
        sides[0 if x < gap_x else 1].append((x, y))
    return [side for side in sides if len(side) > 40]


def in_box(x, y, box, w, h):
    if box is None:
        return False
    x0, y0, x1, y1 = box
    return x0 <= x <= min(x1, w - 1) and y0 <= y <= min(y1, h - 1)


def paint(pixels, w, h, hand_box, do_eyes):
    if hand_box is not None:
        skin = []
        for y in range(h):
            for x in range(w):
                if in_box(x, y, hand_box, w, h) and is_skin(*pixels[y][x]):
                    skin.append((x, y))
        # The sack neck is the same peach family as skin. The hand is the one
        # big connected region; the cloth specks stay cloth.
        comps = components(skin)
        hand = max(comps, key=len) if comps else []
        if hand:
            lumas = [luma(*pixels[y][x][:3]) for x, y in hand]
            lo, hi = min(lumas), max(lumas)
            span = max(1.0, hi - lo)
            painted = set(hand)
            for (x, y), tone in zip(hand, lumas):
                r, g, b = glove_color((tone - lo) / span)
                pixels[y][x] = (r, g, b, pixels[y][x][3])
        else:
            painted = set()
    else:
        painted = set()

    eye_count = 0
    if do_eyes:
        eyes = []
        for y in range(h):
            for x in range(w):
                if is_eye_white(*pixels[y][x]):
                    eyes.append((x, y))
        eye_set = set(eyes)
        extra = []
        for x, y in eyes:
            for xx, yy in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if (xx, yy) in eye_set or not (0 <= xx < w and 0 <= yy < h):
                    continue
                r, g, b, a = pixels[yy][xx]
                if a > 40 and r > 150 and g > 140 and b > 120 and abs(r - g) < 50:
                    extra.append((xx, yy))
                    eye_set.add((xx, yy))
        eyes.extend(extra)
        kept = eye_bodies(eyes)
        for x, y in kept:
            pixels[y][x] = (12, 12, 14, pixels[y][x][3])
        eye_count = len(kept)
        sides = split_eyes(kept)
        for side in sides:
            side_set = set(side)
            sx = [p[0] for p in side]
            sy = [p[1] for p in side]
            tx = min(sx) + (max(sx) - min(sx)) * 0.34
            ty = min(sy) + (max(sy) - min(sy)) * 0.32
            anchor = min(side, key=lambda p: (p[0] - tx) ** 2 + (p[1] - ty) ** 2)
            for oy in (0, 1):
                for ox in (0, 1):
                    xx, yy = anchor[0] + ox, anchor[1] + oy
                    if (xx, yy) in side_set:
                        pixels[yy][xx] = (248, 248, 248, pixels[yy][xx][3])
    return len(painted), eye_count


def is_glove_brown(r, g, b, a):
    if a < 140:
        return False
    return 18 <= r <= 120 and 10 <= g <= 95 and b <= 75 and r + 8 >= g and g + 12 >= b and r - b >= 6


def is_skin_edge(r, g, b, a):
    if a < 30 or r < 10:
        return False
    if r < g - 2 or g + 10 < b or r - b < 8:
        return False
    dark_red = r < 70 and g < r * 0.55 and b < 40
    peach = r > 90 and r - g >= 18 and g - b >= 8 and not (g > 150 and r - g < 40 and b > 90)
    return dark_red or peach


def is_orange_speck(r, g, b, a):
    if a < 80:
        return False
    return 40 < r < 170 and r - g >= 26 and g - b >= 10 and b < 100 and g < 140


def is_fill_white(red, green, blue, alpha):
    # The white of the eye, including the soft edge of that white.
    return alpha > 150 and red > 220 and green > 210 and blue > 190 and abs(red - green) < 30


def clear_eye_specks(pixels, w, h):
    # Gray crumbs left in the white. A crumb has white on most sides.
    # The pupil, the catchlight, and the mask edge stay.
    sclera = (250, 246, 238)
    pupil = (12, 12, 14)
    catch = (255, 255, 255)
    painted = 0
    for _ in range(3):
        hits = []
        for y in range(h):
            for x in range(w):
                red, green, blue, alpha = pixels[y][x]
                if alpha <= 80 or is_fill_white(red, green, blue, alpha):
                    continue
                if (red, green, blue) in (pupil, catch):
                    continue
                if max(red, green, blue) - min(red, green, blue) > 40:
                    continue
                tone = luma(red, green, blue)
                if tone < 70 or tone > 190:
                    continue
                white_n = 0
                for oy in (-1, 0, 1):
                    for ox in (-1, 0, 1):
                        if ox == 0 and oy == 0:
                            continue
                        xx, yy = x + ox, y + oy
                        if 0 <= xx < w and 0 <= yy < h and is_fill_white(*pixels[yy][xx]):
                            white_n += 1
                if white_n >= 4:
                    hits.append((x, y))
        if not hits:
            break
        for x, y in hits:
            pixels[y][x] = (*sclera, pixels[y][x][3])
            painted += 1
    return painted


def clear_glove_spots(pixels, w, h, hand_box):
    # Pale crumbs on the leather. A crumb is tiny, and it sits in the glove.
    # The sack is pale too, so a crumb that mostly touches cloth is left alone.
    if hand_box is None:
        return 0
    seeds = [(x, y) for y in range(h) for x in range(w) if in_box(x, y, hand_box, w, h) and is_glove_brown(*pixels[y][x])]
    comps = components(seeds)
    hand = set(max(comps, key=len)) if comps else set()
    if not hand:
        return 0
    bright = []
    for y in range(h):
        for x in range(w):
            if not in_box(x, y, hand_box, w, h):
                continue
            red, green, blue, alpha = pixels[y][x]
            if alpha < 80 or red <= 175 or green <= 130 or blue <= 90:
                continue
            if red < green - 5 or green < blue - 10 or red - blue <= 20:
                continue
            bright.append((x, y))
    # Diagonal crumbs are one spot. The glove body stays on the 4-neighbor test.
    pending = set(bright)
    specks = []
    while pending:
        start = pending.pop()
        stack = [start]
        speck = []
        while stack:
            x, y = stack.pop()
            speck.append((x, y))
            for oy in (-1, 0, 1):
                for ox in (-1, 0, 1):
                    if ox == 0 and oy == 0:
                        continue
                    nxt = (x + ox, y + oy)
                    if nxt in pending:
                        pending.remove(nxt)
                        stack.append(nxt)
        specks.append(speck)
    painted = 0
    for speck in specks:
        if len(speck) > 25:
            continue
        near_glove = 0
        seen = set(speck)
        for x, y in speck:
            for oy in range(-2, 3):
                for ox in range(-2, 3):
                    spot = (x + ox, y + oy)
                    if spot in seen:
                        continue
                    seen.add(spot)
                    if spot in hand:
                        near_glove += 1
        if near_glove < 14:
            continue
        cols = []
        for x, y in speck:
            for oy in range(-3, 4):
                for ox in range(-3, 4):
                    spot = (x + ox, y + oy)
                    if spot in hand:
                        cols.append(pixels[spot[1]][spot[0]][:3])
        if not cols:
            continue
        color = tuple(sum(col[i] for col in cols) // len(cols) for i in range(3))
        for x, y in speck:
            pixels[y][x] = (*color, pixels[y][x][3])
            painted += 1
    return painted


def finish(pixels, w, h, hand_box, do_eyes):
    # Second pass. The glove rim is the skin still showing around the sack hand.
    # Each eye keeps one pupil. single_pupils clears the extra black mark.
    rim_n = 0
    if hand_box is not None:
        seeds = []
        for y in range(h):
            for x in range(w):
                if in_box(x, y, hand_box, w, h) and is_glove_brown(*pixels[y][x]):
                    seeds.append((x, y))
        comps = components(seeds)
        hand = max(comps, key=len) if comps else []
        hand_set = set(hand)
        rim = []
        for y in range(h):
            for x in range(w):
                if (x, y) in hand_set or not in_box(x, y, hand_box, w, h):
                    continue
                if not is_skin_edge(*pixels[y][x]):
                    continue
                if not any((xx, yy) in hand_set for yy in range(max(0, y - 2), min(h, y + 3)) for xx in range(max(0, x - 2), min(w, x + 3))):
                    continue
                glove_n = clear_n = sack_n = 0
                for yy in (y - 1, y, y + 1):
                    for xx in (x - 1, x, x + 1):
                        if (xx, yy) == (x, y) or not (0 <= xx < w and 0 <= yy < h):
                            continue
                        rr, gg, bb, aa = pixels[yy][xx]
                        if (xx, yy) in hand_set or is_glove_brown(rr, gg, bb, aa):
                            glove_n += 1
                        elif aa < 30:
                            clear_n += 1
                        elif gg > 110 and rr - gg < 42 and bb > 70:
                            sack_n += 1
                if sack_n == 0 and (clear_n >= 1 or glove_n >= 4):
                    rim.append((x, y))
        for x, y in rim:
            red, green, blue, alpha = pixels[y][x]
            tone = (luma(red, green, blue) - 20.0) / 150.0
            pixels[y][x] = (*glove_color(max(0.0, min(1.0, tone)) * 0.4), alpha)
        orange = [(x, y) for y in range(h) for x in range(w) if in_box(x, y, hand_box, w, h) and is_orange_speck(*pixels[y][x])]
        for speck in components(orange):
            if len(speck) > 60:
                continue
            if not any(any((sx + dx, sy + dy) in hand_set for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2)) for sx, sy in speck):
                continue
            for x, y in speck:
                pixels[y][x] = (42, 28, 20, pixels[y][x][3])
            rim.extend(speck)
        rim_n = len(rim)

    eye_n = single_pupils(pixels, w, h) if do_eyes else 0
    # single_pupils draws one pupil. These two calls only lift the crumbs
    # that pass is still leaving in the white and on the leather.
    if do_eyes:
        eye_n += clear_eye_specks(pixels, w, h)
    rim_n += clear_glove_spots(pixels, w, h, hand_box)
    return rim_n, eye_n


def convex_hull(points):
    pts = sorted(set(points))

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    lower = []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    upper = []
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def point_inside(x, y, poly):
    # Ray cast. x and y are a pixel center, so the test does not ride the edge.
    hit = False
    j = len(poly) - 1
    for i, (xi, yi) in enumerate(poly):
        xj, yj = poly[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            hit = not hit
        j = i
    return hit


def is_sclera_px(red, green, blue, alpha):
    return alpha > 150 and red > 230 and green > 220 and blue > 200 and abs(red - green) < 25


def single_pupils(pixels, w, h):
    # The white of each eye is one ring. The old pupil was left in that ring
    # when a second pupil was drawn, and the two marks share the mask edge.
    # Fill the dark bite inside the ring, then draw one pupil.
    import math

    sclera = (250, 246, 238)
    pupil = (12, 12, 14)
    pts = [(x, y) for y in range(h) for x in range(w) if is_sclera_px(*pixels[y][x])]
    comps = [comp for comp in components(pts) if len(comp) > 20]
    merged = []
    used = [False] * len(comps)
    for i, comp in enumerate(comps):
        if used[i]:
            continue
        group = list(comp)
        used[i] = True
        grew = True
        while grew:
            grew = False
            for j, other in enumerate(comps):
                if used[j]:
                    continue
                if near(group, other, 18):
                    group.extend(other)
                    used[j] = True
                    grew = True
        merged.append(group)
    merged.sort(key=len, reverse=True)
    painted = 0
    for eye in merged[:2]:
        if len(eye) < 200:
            continue
        poly = convex_hull(eye)
        if len(poly) < 3:
            continue
        cx = sum(p[0] + 0.5 for p in eye) / len(eye)
        cy = sum(p[1] + 0.5 for p in eye) / len(eye)
        radii = sorted(math.hypot(p[0] + 0.5 - cx, p[1] + 0.5 - cy) for p in eye)
        hole = radii[max(0, int(len(radii) * 0.08))]
        pr = hole * 0.92
        xs = [p[0] for p in poly]
        ys = [p[1] for p in poly]
        x0, x1 = max(0, min(xs)), min(w - 1, max(xs))
        y0, y1 = max(0, min(ys)), min(h - 1, max(ys))
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if not point_inside(x + 0.5, y + 0.5, poly):
                    continue
                red, green, blue, alpha = pixels[y][x]
                if alpha < 80:
                    continue
                inside_pupil = math.hypot(x + 0.5 - cx, y + 0.5 - cy) <= pr
                dark_bite = red < 100 and green < 100 and blue < 120
                if inside_pupil or dark_bite:
                    pixels[y][x] = (*(pupil if inside_pupil else sclera), alpha)
                    painted += 1
        hx = int(round(cx - pr * 0.42))
        hy = int(round(cy - pr * 0.46))
        for oy in range(3):
            for ox in range(3):
                xx, yy = hx + ox, hy + oy
                if 0 <= xx < w and 0 <= yy < h and pixels[yy][xx][:3] == pupil:
                    pixels[yy][xx] = (255, 255, 255, pixels[yy][xx][3])
    return painted


def restore_jpg(painted, original, w, h, hand_box):
    # The jpeg moved the flat black, so the eye whites come back from the
    # untouched original. Pixels that were the white of the eye are white again.
    # The original pupil, the smaller black shape, stays put.
    eyes = []
    for y in range(h):
        for x in range(w):
            if is_eye_white(*original[y][x]):
                eyes.append((x, y))
                painted[y][x] = (original[y][x][0], original[y][x][1], original[y][x][2], painted[y][x][3])
    skin = [(x, y) for y in range(h) for x in range(w) if in_box(x, y, hand_box, w, h) and is_skin(*original[y][x])]
    comps = components(skin)
    hand = max(comps, key=len) if comps else []
    floor = min((luma(*painted[y][x][:3]) for x, y in hand), default=40.0)
    rim = 0
    for x, y in hand:
        if is_skin(*painted[y][x]) or is_skin_edge(*painted[y][x]) or is_orange_speck(*painted[y][x]):
            red, green, blue, alpha = painted[y][x]
            tone = (luma(red, green, blue) - floor) / 160.0
            painted[y][x] = (*glove_color(max(0.0, min(1.0, tone))), alpha)
            rim += 1
    return rim, len(eyes)


def paint_file_pil(path, dest, hand_box, do_eyes):
    from PIL import Image

    image = Image.open(path).convert("RGBA")
    w, h = image.size
    raw = list(image.getdata())
    rows = [list(raw[y * w:(y + 1) * w]) for y in range(h)]
    glove_n, eye_n = finish(rows, w, h, hand_box, do_eyes)
    image.putdata([px for row in rows for px in row])
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    if dest.lower().endswith(".jpg") or dest.lower().endswith(".jpeg"):
        image.convert("RGB").save(dest, quality=98)
    else:
        image.save(dest)
    print("PAINTED", os.path.basename(path), "glove", glove_n, "eyes", eye_n, "->", dest)


def paint_file_blender(path, dest, hand_box, do_eyes):
    import bpy

    image = bpy.data.images.load(path)
    w, h = image.size
    channels = image.channels
    count = w * h * channels
    buf = array.array("f", [0.0]) * count
    image.pixels.foreach_get(buf)
    # Blender stores the bottom row first.
    # In this Blender, image.pixels is the file color as 0 to 1, not scene linear.
    rows = [[(0, 0, 0, 255) for _ in range(w)] for _ in range(h)]
    for y in range(h):
        src_y = h - 1 - y
        for x in range(w):
            i = (src_y * w + x) * channels
            if channels == 4:
                r, g, b, a = buf[i], buf[i + 1], buf[i + 2], buf[i + 3]
            else:
                r, g, b, a = buf[i], buf[i + 1], buf[i + 2], 1.0
            rows[y][x] = (
                int(round(r * 255)),
                int(round(g * 255)),
                int(round(b * 255)),
                int(round(a * 255)),
            )
    glove_n, eye_n = finish(rows, w, h, hand_box, do_eyes)
    for y in range(h):
        src_y = h - 1 - y
        for x in range(w):
            i = (src_y * w + x) * channels
            px = rows[y][x]
            buf[i] = px[0] / 255.0
            buf[i + 1] = px[1] / 255.0
            buf[i + 2] = px[2] / 255.0
            if channels == 4:
                buf[i + 3] = px[3] / 255.0
    image.pixels.foreach_set(buf)
    image.update()
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    image.filepath_raw = dest
    image.file_format = "JPEG" if dest.lower().endswith((".jpg", ".jpeg")) else "PNG"
    image.save()
    print("PAINTED", os.path.basename(path), "glove", glove_n, "eyes", eye_n, "->", dest)


def load_rows_pil(path):
    from PIL import Image

    image = Image.open(path).convert("RGBA")
    w, h = image.size
    raw = list(image.getdata())
    rows = [list(raw[y * w:(y + 1) * w]) for y in range(h)]
    return image, rows, w, h


def save_rows_pil(image, rows, dest):
    image.putdata([px for row in rows for px in row])
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    if dest.lower().endswith((".jpg", ".jpeg")):
        image.convert("RGB").save(dest, quality=98)
    else:
        image.save(dest)


def retouch_jpg_pil(painted_path, dest, hand_box):
    painted_image, painted, w, h = load_rows_pil(painted_path)
    _, original, ow, oh = load_rows_pil(SOURCE)
    if (ow, oh) != (w, h):
        raise RuntimeError("original robber size does not match the painted file")
    rim, eyes = restore_jpg(painted, original, w, h, hand_box)
    save_rows_pil(painted_image, painted, dest)
    print("RETOUCHED", os.path.basename(painted_path), "rim", rim, "eyes", eyes, "->", dest)


def load_rows_blender(path):
    import bpy

    image = bpy.data.images.load(path)
    w, h = image.size
    channels = image.channels
    buf = array.array("f", [0.0]) * (w * h * channels)
    image.pixels.foreach_get(buf)
    rows = [[(0, 0, 0, 255) for _ in range(w)] for _ in range(h)]
    for y in range(h):
        src_y = h - 1 - y
        for x in range(w):
            i = (src_y * w + x) * channels
            if channels == 4:
                red, green, blue, alpha = buf[i], buf[i + 1], buf[i + 2], buf[i + 3]
            else:
                red, green, blue, alpha = buf[i], buf[i + 1], buf[i + 2], 1.0
            rows[y][x] = (
                int(round(red * 255)),
                int(round(green * 255)),
                int(round(blue * 255)),
                int(round(alpha * 255)),
            )
    return image, rows, w, h, channels, buf


def store_rows_blender(image, rows, buf, channels, dest):
    w, h = image.size
    for y in range(h):
        src_y = h - 1 - y
        for x in range(w):
            i = (src_y * w + x) * channels
            px = rows[y][x]
            buf[i] = px[0] / 255.0
            buf[i + 1] = px[1] / 255.0
            buf[i + 2] = px[2] / 255.0
            if channels == 4:
                buf[i + 3] = px[3] / 255.0
    image.pixels.foreach_set(buf)
    image.update()
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    image.filepath_raw = dest
    image.file_format = "JPEG" if dest.lower().endswith((".jpg", ".jpeg")) else "PNG"
    image.save()


def retouch_jpg_blender(painted_path, dest, hand_box):
    painted_image, painted, w, h, channels, buf = load_rows_blender(painted_path)
    _, original, ow, oh, _, _ = load_rows_blender(SOURCE)
    if (ow, oh) != (w, h):
        raise RuntimeError("original robber size does not match the painted file")
    rim, eyes = restore_jpg(painted, original, w, h, hand_box)
    store_rows_blender(painted_image, painted, buf, channels, dest)
    print("RETOUCHED", os.path.basename(painted_path), "rim", rim, "eyes", eyes, "->", dest)


def destinations(preview):
    fixed_download = SOURCE.replace("robber.jpg", "robber-fixed.jpg")
    if preview:
        pngs = (
            (JOBS[0][0], "/tmp/robber-fix/head.png", JOBS[0][1], JOBS[0][2]),
            (JOBS[1][0], "/tmp/robber-fix/heist_robber.png", JOBS[1][1], JOBS[1][2]),
            (JOBS[2][0], "/tmp/robber-fix/armFront.png", JOBS[2][1], JOBS[2][2]),
            (JOBS[3][0], "/tmp/robber-fix/bag.png", JOBS[3][1], JOBS[3][2]),
        )
        jpgs = ((fixed_download, "/tmp/robber-fix/robber.jpg", JOBS[4][1]),)
        return pngs, jpgs
    pngs = (
        (JOBS[0][0], JOBS[0][0], JOBS[0][1], JOBS[0][2]),
        (JOBS[1][0], JOBS[1][0], JOBS[1][1], JOBS[1][2]),
        (JOBS[2][0], JOBS[2][0], JOBS[2][1], JOBS[2][2]),
        (JOBS[3][0], JOBS[3][0], JOBS[3][1], JOBS[3][2]),
    )
    jpgs = (
        (fixed_download, fixed_download, JOBS[4][1]),
        (REFERENCE, REFERENCE, JOBS[4][1]),
    )
    return pngs, jpgs


def main():
    import sys

    preview = "--preview" in sys.argv
    # Blender puts its own args before --, and our flag after the script name
    # only when python runs this file. Detect Blender by bpy.
    try:
        import bpy  # noqa: F401
        in_blender = True
    except ImportError:
        in_blender = False
    writer = paint_file_blender if in_blender else paint_file_pil
    jpg_writer = retouch_jpg_blender if in_blender else retouch_jpg_pil
    pngs, jpgs = destinations(preview)
    for src, dest, box, eyes in pngs:
        writer(src, dest, box, eyes)
    for src, dest, box in jpgs:
        jpg_writer(src, dest, box)


if __name__ == "__main__":
    main()
