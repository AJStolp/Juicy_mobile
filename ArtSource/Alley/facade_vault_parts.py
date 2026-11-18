# Splits the graffiti facade's own vault door into moving parts, for
# import_glb.py --vault-parts. The parts keep their faces, UVs and the facade
# material, so at rest the facade looks exactly as imported.
#
#   Facade_Door        the door inside the frame, origin on its hinge (left edge)
#     Facade_Wheel     the spoked wheel, origin on the hub
#     Facade_Gear      the gear lock right of the wheel, origin on its center
#     Facade_Bar_0..2  the locking bars, left, upper right, lower right; they
#                      slide in toward the hub, out of their end keepers
#     Facade_Handle    the pull handle
#     Facade_Door_Back a thin plate behind the door, so the door has a back
#                      when it swings open and a bar or the handle moving shows
#                      dark steel where it was
#   Facade_Recess      a dark box behind the doorway, seen when the door opens
#
# Coordinates are glTF meters in Blender axes: X across, -Y out of the front,
# Z up. They come from a ray cast depth map of the front: wall and door plate
# at y -0.084, frame -0.110, bars -0.105, bar keepers -0.111, wheel -0.116 to
# -0.125.
import bmesh
from mathutils import Matrix, Vector

DOOR = (-0.178, 0.178, 0.036, 0.398)   # x0 x1 z0 z1 inside the frame
DOOR_FRONT = -0.095                    # the door's hinge line depth
PLATE = -0.084                         # door plate and wall depth
WHEEL_GUESS = Vector((0.0, 0.212))     # (x, z)
WHEEL_R = 0.060
GEAR_GUESS = Vector((0.095, 0.205))
GEAR_R = 0.036
HANDLE = (0.103, 0.165, 0.135, 0.29)   # x0 x1 z0 z1, faces in front of y -0.115
BAR_ENDS = [Vector((-0.165, 0.212)), Vector((0.085, 0.36)), Vector((0.085, 0.064))]
BAR_HALF_WIDTH = 0.0085
BAR_DEPTH = (-0.109, -0.095)           # keepers at -0.111 stay on the door
RECESS_BACK = PLATE + 0.09


def _xz(v):
    return Vector((v.x, v.z))


def _center(obj, guess, radius, front):
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    near = [p for p in pts if (_xz(p) - guess).length < radius and p.y < front]
    return sum((_xz(p) for p in near), Vector((0.0, 0.0))) / max(len(near), 1)


def _texel(obj, image, darkest):
    """UV of a texel on the facade: the darkest one, or the door plate's."""
    if darkest:
        px = list(image.pixels[:])
        w, h = image.size
        best, uv = 9.0, (0.5, 0.5)
        for j in range(4, h - 4, 16):
            for i in range(4, w - 4, 16):
                k = (j * w + i) * 4
                lum = px[k] + px[k + 1] + px[k + 2]
                if lum < best:
                    best, uv = lum, ((i + 0.5) / w, (j + 0.5) / h)
        return Vector(uv)
    # A face on bare door plate between the wheel and the lower bar.
    uvs = obj.data.uv_layers.active.data
    target = Vector((0.05, 0.13))
    best, uv = 9.0, Vector((0.5, 0.5))
    for poly in obj.data.polygons:
        c = obj.matrix_world @ poly.center
        d = (_xz(c) - target).length
        if d < best and c.y > PLATE - 0.004:
            best = d
            uv = sum((uvs[l].uv for l in poly.loop_indices), Vector((0.0, 0.0))) / poly.loop_total
    return uv


SIDES = 32


def _in_disc(p, center, radius):
    """Inside the SIDES sided polygon whose apothem is radius; the mesh is
    bisected on that polygon, so a whole face is on one side of it."""
    import math
    d = p - center
    return all(d.x * math.cos(a) + d.y * math.sin(a) < radius
               for a in (2 * math.pi * k / SIDES for k in range(SIDES)))


def _bisect(obj, planes, region):
    """Cuts every face whose outline overlaps region (x0 x1 z0 z1) on each
    plane, so no face straddles a part's outline and a part that turns or
    swings never drags a big wall triangle along. The facade's wall is one
    big flat plane, so overlap is tested on the face's bounds, not its
    vertices."""
    x0, x1, z0, z1 = region
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for co, no in planes:
        faces = []
        for f in bm.faces:
            xs = [v.co.x for v in f.verts]
            zs = [v.co.z for v in f.verts]
            if max(xs) > x0 and min(xs) < x1 and max(zs) > z0 and min(zs) < z1:
                faces.append(f)
        geom = list({e for f in faces for e in f.edges}) + faces + list({v for f in faces for v in f.verts})
        bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=co, plane_no=no)
    bm.to_mesh(obj.data)
    bm.free()


def _cut_outlines(obj, wheel, gear):
    import math
    for center, radius in ((wheel, WHEEL_R), (gear, GEAR_R)):
        planes = []
        for k in range(SIDES):
            a = 2 * math.pi * k / SIDES
            n = Vector((math.cos(a), 0.0, math.sin(a)))
            planes.append((Vector((center.x, 0.0, center.y)) + n * radius, n))
        r = radius * 1.1
        _bisect(obj, planes, (center.x - r, center.x + r, center.y - r, center.y + r))
    x0, x1, z0, z1 = DOOR
    _bisect(obj, [(Vector((x0, 0, 0)), Vector((1, 0, 0))), (Vector((x1, 0, 0)), Vector((1, 0, 0))),
                  (Vector((0, 0, z0)), Vector((0, 0, 1))), (Vector((0, 0, z1)), Vector((0, 0, 1)))],
            (x0 - 0.01, x1 + 0.01, z0 - 0.01, z1 + 0.01))


def _classify(obj, wheel, gear):
    out = {}
    hub = Vector((wheel.x, wheel.y))
    for poly in obj.data.polygons:
        c = obj.matrix_world @ poly.center
        p = _xz(c)
        if not (DOOR[0] < c.x < DOOR[1] and DOOR[2] < c.z < DOOR[3]):
            continue
        # Relief backs reach to y -0.01; they swing with the door as its
        # thickness. Anything behind the recess's back wall stays put.
        if any((obj.matrix_world @ obj.data.vertices[v].co).y > RECESS_BACK - 0.002 for v in poly.vertices):
            continue
        part = "Facade_Door"
        if _in_disc(p, wheel, WHEEL_R):
            part = "Facade_Wheel"
        elif _in_disc(p, gear, GEAR_R):
            part = "Facade_Gear"
        elif HANDLE[0] < c.x < HANDLE[1] and HANDLE[2] < c.z < HANDLE[3] and c.y < -0.115:
            part = "Facade_Handle"
        else:
            for i, end in enumerate(BAR_ENDS):
                axis = end - hub
                t = (p - hub).dot(axis) / axis.length_squared
                off = (p - hub - axis * t).length
                if 0.0 < t < 1.0 and (p - hub).length > WHEEL_R and off < BAR_HALF_WIDTH and BAR_DEPTH[0] < c.y < BAR_DEPTH[1]:
                    part = "Facade_Bar_%d" % i
                    break
        out[poly.index] = part
    return out


def _separate(bpy, obj, indices, name):
    for poly in obj.data.polygons:
        poly.select = poly.index in indices
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    before = set(bpy.context.scene.objects)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.object.mode_set(mode='OBJECT')
    for poly in obj.data.polygons:
        poly.select = poly.index in indices
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')
    part = [o for o in bpy.context.scene.objects if o not in before][0]
    # Bisecting leaves stray vertices on the cut lines; they would only widen
    # the part's bounds.
    bm = bmesh.new()
    bm.from_mesh(part.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(part.data)
    bm.free()
    part.name = name
    part.data.name = name
    return part


def _set_origin(part, world):
    part.data.transform(Matrix.Translation(-(world - part.location)))
    part.location = world


def _box(bpy, obj, name, lo, hi, uv, inward):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    v = [bm.verts.new((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
    # Index = 4x + 2y + z over (lo, hi).
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    if inward:
        quads = quads[:2] + quads[3:]   # open on the -Y front
    for q in quads:
        f = bm.faces.new([v[i] for i in q])
        if inward:
            f.normal_flip()
    bm.normal_update()
    layer = bm.loops.layers.uv.new(obj.data.uv_layers.active.name)
    for f in bm.faces:
        for loop in f.loops:
            loop[layer].uv = uv
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(obj.data.materials[0])
    part = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(part)
    return part


def _parent(bpy, child, parent):
    bpy.context.view_layer.update()
    world = child.matrix_world.copy()
    child.parent = parent
    child.matrix_parent_inverse.identity()
    child.matrix_world = world


def split(bpy, obj, image):
    wheel = _center(obj, WHEEL_GUESS, 0.02, -0.123)
    gear = _center(obj, GEAR_GUESS, 0.03, -0.113)
    print("VAULT wheel", tuple(wheel), "gear", tuple(gear))
    _cut_outlines(obj, wheel, gear)
    groups = {}
    for index, part in _classify(obj, wheel, gear).items():
        groups.setdefault(part, set()).add(index)
    plate_uv = _texel(obj, image, False)
    dark_uv = _texel(obj, image, True)

    # Separate the children first, then what is left of the door.
    parts = {}
    for name in ["Facade_Wheel", "Facade_Gear", "Facade_Handle", "Facade_Bar_0", "Facade_Bar_1", "Facade_Bar_2", "Facade_Door"]:
        if name not in groups:
            raise SystemExit("no faces for " + name)
        # Indices shift after each separate, so classify again by name.
        current = {i for i, p in _classify(obj, wheel, gear).items() if p == name}
        parts[name] = _separate(bpy, obj, current, name)
        print("VAULT", name, "faces", len(parts[name].data.polygons))

    door = parts["Facade_Door"]
    _set_origin(door, Vector((DOOR[0], DOOR_FRONT, DOOR[2])))
    _set_origin(parts["Facade_Wheel"], Vector((wheel.x, -0.12, wheel.y)))
    _set_origin(parts["Facade_Gear"], Vector((gear.x, -0.115, gear.y)))
    _set_origin(parts["Facade_Handle"], Vector(((HANDLE[0] + HANDLE[1]) / 2, -0.12, (HANDLE[2] + HANDLE[3]) / 2)))
    for i, end in enumerate(BAR_ENDS):
        mid = (wheel + end) / 2
        _set_origin(parts["Facade_Bar_%d" % i], Vector((mid.x, -0.105, mid.y)))

    back = _box(bpy, obj, "Facade_Door_Back", Vector((DOOR[0], PLATE + 0.001, DOOR[2])),
                Vector((DOOR[1], PLATE + 0.012, DOOR[3])), plate_uv, False)
    recess = _box(bpy, obj, "Facade_Recess", Vector((DOOR[0] - 0.002, PLATE + 0.012, DOOR[2] - 0.002)),
                  Vector((DOOR[1] + 0.002, RECESS_BACK, DOOR[3] + 0.002)), dark_uv, True)
    for name in ["Facade_Wheel", "Facade_Gear", "Facade_Handle", "Facade_Bar_0", "Facade_Bar_1", "Facade_Bar_2"]:
        _parent(bpy, parts[name], door)
    _parent(bpy, back, door)
    _parent(bpy, door, obj)
    _parent(bpy, recess, obj)
    bpy.context.view_layer.update()
    for o in [obj] + list(obj.children_recursive):
        print("VAULT pivot", o.name, tuple(round(v, 3) for v in o.matrix_world.translation))


def bake_axes(bpy, obj):
    """Puts every object in Unity's axes with no rotation of its own.

    The exporter's bake_space_transform only bakes the root correctly and
    leaves children rotated, so for a hierarchy the conversion is applied
    here and the export runs without it. Each object's mesh turns by the
    exporter's axis conversion C and its world matrix becomes C^-1 T(C t), so
    the export writes plain translations and Unity gets upright parts."""
    from bpy_extras.io_utils import axis_conversion
    convert = axis_conversion(to_forward='-Z', to_up='Y').to_4x4()
    chain = [obj] + list(obj.children_recursive)
    bpy.context.view_layer.update()
    pivots = {o.name: o.matrix_world.translation.copy() for o in chain}
    for o in chain:
        o.data.transform(convert)
    for o in chain:   # parents come before their children
        o.matrix_world = convert.inverted() @ Matrix.Translation(convert @ pivots[o.name])
        bpy.context.view_layer.update()
