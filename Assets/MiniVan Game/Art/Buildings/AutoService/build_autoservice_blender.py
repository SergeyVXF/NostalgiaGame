"""
Low-poly abandoned auto service - exterior + walkable interior.

  "C:\\Program Files\\Blender Foundation\\Blender 3.4\\blender.exe" --background --python build_autoservice_blender.py

Blender space: Z up, facade faces +Y. Exported so the facade faces +Z in Unity.
Every visual object is a single mesh; only the parts meant to be opened later
(car doors/hood/trunk, office door, two roll-up shutters) stay separate and
keep their origin on the hinge.
"""
from __future__ import annotations

import math
import os
import sys
from typing import Iterable, List, Optional, Sequence, Tuple

import bmesh
import bpy
from mathutils import Euler, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
if OUT_DIR not in sys.path:
    sys.path.insert(0, OUT_DIR)
import as_atlas  # noqa: E402
import as_common  # noqa: E402

# Keep .blend outside Assets: Unity 6 imports .blend via Blender and can hang Bee.
BLEND_DIR = os.path.normpath(os.path.join(OUT_DIR, "..", "..", "..", "..", "..", "..", "tools", "autoservice"))
os.makedirs(BLEND_DIR, exist_ok=True)
BLEND_PATH = os.path.join(BLEND_DIR, "AutoService.blend")
FBX_PATH = os.path.join(OUT_DIR, "AutoService.fbx")
ATLAS_PATH = os.path.join(OUT_DIR, "AutoService_Atlas.png")

# ---------------------------------------------------------------------------
# dimensions (metres)
# ---------------------------------------------------------------------------
W, D, H = 16.0, 12.0, 4.5
HX, HY = W * 0.5, D * 0.5
WALL_T = 0.25
FLOOR_T = 0.18
ROOF_T = 0.22
PARAPET_H = 0.34
TRIM_H = 1.25                      # blue plinth height
GAR_W, GAR_H = 3.6, 3.0            # roll-up openings
OFF_W, OFF_H = 1.02, 2.16          # office door
SIDE_DOOR = (-4.75, -3.65)         # yard exit in the +X wall, back of the bay

# facade layout, x from -8 .. +8
WIN_A = (-7.30, -6.10)
DOOR_OFF = (-5.45, -4.43)
WIN_B = (-3.85, -2.65)
GAR_L = (-1.85, 1.75)
GAR_R = (2.85, 6.45)
WIN_Z = (1.40, 2.35)
SIGN_X, SIGN_W, SIGN_H = 2.30, 4.80, 1.15
SIGN_Z = 3.82

# interior
OFFICE_X = -2.75                   # partition wall x
OFFICE_Y = 1.55                    # partition wall y
PIT = (3.95, 5.45, -1.30, 2.90)    # x0, x1, y0, y1
PIT_D = 1.25

MAT: Optional[bpy.types.Material] = None
MAT_GLASS: Optional[bpy.types.Material] = None
_SEQ = 0


# ---------------------------------------------------------------------------
# small helpers
# ---------------------------------------------------------------------------
def uid(prefix: str) -> str:
    global _SEQ
    _SEQ += 1
    return f"{prefix}_{_SEQ:04d}"


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images,
                 bpy.data.cameras, bpy.data.lights, bpy.data.worlds):
        for item in list(coll):
            try:
                coll.remove(item)
            except Exception:
                pass


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_trs(obj: bpy.types.Object, loc: bool = False, rot: bool = True, scale: bool = True) -> None:
    set_active(obj)
    bpy.ops.object.transform_apply(location=loc, rotation=rot, scale=scale)


def origin_to(obj: bpy.types.Object, world: Sequence[float]) -> None:
    bpy.context.scene.cursor.location = Vector(world)
    set_active(obj)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    bpy.context.scene.cursor.location = Vector((0.0, 0.0, 0.0))


def new_empty(name: str, loc: Sequence[float] = (0.0, 0.0, 0.0)) -> bpy.types.Object:
    e = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(e)
    e.location = loc
    e.empty_display_size = 0.5
    e.empty_display_type = "PLAIN_AXES"
    return e


def assign_mat(obj: bpy.types.Object, mat: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def recalc_normals(obj: bpy.types.Object) -> None:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def project_uv(obj: bpy.types.Object) -> None:
    """Per-face planar projection normalised to the face bounds (0..1)."""
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uvl = mesh.uv_layers.active.data
    for poly in mesh.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        iu, iv = (1, 2) if ax == 0 else ((0, 2) if ax == 1 else (0, 1))
        coords = []
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            coords.append((co[iu], co[iv]))
        us = [c[0] for c in coords]
        vs = [c[1] for c in coords]
        u0, u1, v0, v1 = min(us), max(us), min(vs), max(vs)
        du = max(u1 - u0, 1e-5)
        dv = max(v1 - v0, 1e-5)
        for li, (u, v) in zip(poly.loop_indices, coords):
            uvl[li].uv = ((u - u0) / du, (v - v0) / dv)


def uv_tile(obj: bpy.types.Object, key: str) -> None:
    """Squeeze the object's 0..1 UVs into one atlas tile."""
    u0, v0, du, dv = as_atlas.uv_rect(key)
    mesh = obj.data
    if not mesh.uv_layers:
        project_uv(obj)
    for loop in mesh.uv_layers.active.data:
        loop.uv.x = u0 + min(max(loop.uv.x, 0.0), 1.0) * du
        loop.uv.y = v0 + min(max(loop.uv.y, 0.0), 1.0) * dv


def override_face_uv(obj: bpy.types.Object, key: str, axis: int, positive: bool,
                     flip_u: bool = False, flip_v: bool = False) -> None:
    """Re-map only the faces pointing along ±axis onto another atlas rect."""
    u0, v0, du, dv = as_atlas.uv_rect(key)
    mesh = obj.data
    uvl = mesh.uv_layers.active.data
    iu, iv = (1, 2) if axis == 0 else ((0, 2) if axis == 1 else (0, 1))
    for poly in mesh.polygons:
        n = poly.normal
        if abs(n[axis]) < 0.7:
            continue
        if (n[axis] > 0.0) != positive:
            continue
        coords = []
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            coords.append((co[iu], co[iv]))
        us = [c[0] for c in coords]
        vs = [c[1] for c in coords]
        a0, a1, b0, b1 = min(us), max(us), min(vs), max(vs)
        da = max(a1 - a0, 1e-5)
        db = max(b1 - b0, 1e-5)
        for li, (a, b) in zip(poly.loop_indices, coords):
            u = (a - a0) / da
            v = (b - b0) / db
            if flip_u:
                u = 1.0 - u
            if flip_v:
                v = 1.0 - v
            uvl[li].uv = (u0 + u * du, v0 + v * dv)


def shade_flat(obj: bpy.types.Object) -> None:
    set_active(obj)
    bpy.ops.object.shade_flat()


# ---------------------------------------------------------------------------
# primitives
# ---------------------------------------------------------------------------
def make_box(loc, size, tile, rot=(0.0, 0.0, 0.0), mat=None) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = uid("box")
    obj.scale = size
    apply_trs(obj)
    project_uv(obj)
    uv_tile(obj, tile)
    assign_mat(obj, mat or MAT)
    return obj


def make_cyl(loc, radius, depth, tile, verts=10, rot=(0.0, 0.0, 0.0), mat=None) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth,
                                        location=loc, rotation=rot, end_fill_type="NGON")
    obj = bpy.context.active_object
    obj.name = uid("cyl")
    apply_trs(obj)
    project_uv(obj)
    uv_tile(obj, tile)
    assign_mat(obj, mat or MAT)
    return obj


def make_prism(profile: Sequence[Tuple[float, float]], x_half: float, tile: str,
               loc=(0.0, 0.0, 0.0), mat=None) -> bpy.types.Object:
    """Extrude a (y, z) silhouette along X. This is what gives the cars a
    real profile - sloped hood, raked screens - instead of stacked boxes."""
    n = len(profile)
    verts = [(x_half, y, z) for (y, z) in profile] + [(-x_half, y, z) for (y, z) in profile]
    faces = [list(range(n)), list(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        j = (i + 1) % n
        faces.append([i, j, n + j, n + i])
    mesh = bpy.data.meshes.new(uid("prism"))
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    recalc_normals(obj)
    project_uv(obj)
    uv_tile(obj, tile)
    assign_mat(obj, mat or MAT)
    return obj


def bar_profile(p0: Sequence[float], p1: Sequence[float], thick: float) -> List[Tuple[float, float]]:
    """Rectangle of the given thickness running from p0 to p1 in the YZ plane.
    Used for slanted pillars - building them from rotated boxes put the panels
    at the wrong angle, this puts the geometry exactly on the edge."""
    (y0, z0), (y1, z1) = p0, p1
    dy, dz = y1 - y0, z1 - z0
    ln = math.hypot(dy, dz) or 1e-6
    ny, nz = -dz / ln * thick * 0.5, dy / ln * thick * 0.5
    return [(y0 + ny, z0 + nz), (y1 + ny, z1 + nz), (y1 - ny, z1 - nz), (y0 - ny, z0 - nz)]


def make_torus(loc, major: float, minor: float, tile: str, rot=(0.0, 0.0, 0.0),
               mseg: int = 10, nseg: int = 4) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(location=loc, rotation=rot, major_radius=major,
                                     minor_radius=minor, major_segments=mseg, minor_segments=nseg)
    obj = bpy.context.active_object
    obj.name = uid("torus")
    apply_trs(obj)
    project_uv(obj)
    uv_tile(obj, tile)
    assign_mat(obj, MAT)
    return obj


def join_as(name: str, objs: Iterable[bpy.types.Object],
            origin: Optional[Sequence[float]] = (0.0, 0.0, 0.0)) -> bpy.types.Object:
    items = [o for o in objs if o is not None]
    if not items:
        raise ValueError("join_as with no parts: " + name)
    if len(items) > 1:
        bpy.ops.object.select_all(action="DESELECT")
        for o in items:
            o.select_set(True)
        bpy.context.view_layer.objects.active = items[0]
        bpy.ops.object.join()
    obj = items[0]
    obj.name = name
    obj.data.name = name
    shade_flat(obj)
    if origin is not None:
        origin_to(obj, origin)
    return obj


def spans_minus(z0: float, z1: float, holes: Sequence[Tuple[float, float]]) -> List[Tuple[float, float]]:
    out = [(z0, z1)]
    for h0, h1 in sorted(holes):
        nxt = []
        for a, b in out:
            if h1 <= a or h0 >= b:
                nxt.append((a, b))
                continue
            if h0 > a:
                nxt.append((a, h0))
            if h1 < b:
                nxt.append((h1, b))
        out = nxt
    return [(a, b) for a, b in out if b - a > 0.03]


def wall_piece(parts: List, x0: float, x1: float, y: float, z0: float, z1: float,
               thick: float = WALL_T, axis: str = "x", inner_tile: str = "plaster") -> None:
    """A wall chunk, split at the plinth line so the blue band is real geometry
    rather than a decal box fighting with the wall."""
    if x1 - x0 < 0.03 or z1 - z0 < 0.03:
        return
    for a, b, tile in ((z0, min(z1, TRIM_H), "blue_plinth"), (max(z0, TRIM_H), z1, inner_tile)):
        if b - a < 0.03:
            continue
        cx = (x0 + x1) * 0.5
        cz = (a + b) * 0.5
        if axis == "x":
            parts.append(make_box((cx, y, cz), (x1 - x0, thick, b - a), tile))
        else:
            parts.append(make_box((y, cx, cz), (thick, x1 - x0, b - a), tile))


# ---------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------
def make_materials(img: bpy.types.Image):
    def principled(name: str, use_tex: bool, alpha: float) -> bpy.types.Material:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        nt = mat.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        out.location = (400, 0)
        if use_tex:
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            tex.interpolation = "Linear"
            tex.location = (-400, 0)
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        else:
            bsdf.inputs["Base Color"].default_value = (0.34, 0.42, 0.44, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.82
        bsdf.inputs["Metallic"].default_value = 0.0
        if "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = alpha
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        if alpha < 1.0:
            mat.blend_method = "BLEND"
            if hasattr(mat, "shadow_method"):
                mat.shadow_method = "NONE"
        return mat

    return principled("AS_Lit", True, 1.0), principled("AS_Glass", False, 0.22)


# ---------------------------------------------------------------------------
# building shell
# ---------------------------------------------------------------------------
def build_shell():
    parts: List[bpy.types.Object] = []
    glass: List[bpy.types.Object] = []
    yf = HY - WALL_T * 0.5

    # --- front wall, opening by opening ---
    segs = [
        (-HX, WIN_A[0], []),
        (WIN_A[0], WIN_A[1], [WIN_Z]),
        (WIN_A[1], DOOR_OFF[0], []),
        (DOOR_OFF[0], DOOR_OFF[1], [(0.0, OFF_H)]),
        (DOOR_OFF[1], WIN_B[0], []),
        (WIN_B[0], WIN_B[1], [WIN_Z]),
        (WIN_B[1], GAR_L[0], []),
        (GAR_L[0], GAR_L[1], [(0.0, GAR_H)]),
        (GAR_L[1], GAR_R[0], []),
        (GAR_R[0], GAR_R[1], [(0.0, GAR_H)]),
        (GAR_R[1], HX, []),
    ]
    for x0, x1, holes in segs:
        for z0, z1 in spans_minus(0.0, H, holes):
            wall_piece(parts, x0, x1, yf, z0, z1)

    # window frames + glass
    for xa, xb in (WIN_A, WIN_B):
        cx, cz = (xa + xb) * 0.5, (WIN_Z[0] + WIN_Z[1]) * 0.5
        fw, fh, t = xb - xa, WIN_Z[1] - WIN_Z[0], 0.07
        parts.append(make_box((cx, yf, WIN_Z[1] - t * 0.5), (fw, WALL_T + 0.02, t), "frame"))
        parts.append(make_box((cx, yf, WIN_Z[0] + t * 0.5), (fw, WALL_T + 0.02, t), "frame"))
        parts.append(make_box((xa + t * 0.5, yf, cz), (t, WALL_T + 0.02, fh), "frame"))
        parts.append(make_box((xb - t * 0.5, yf, cz), (t, WALL_T + 0.02, fh), "frame"))
        parts.append(make_box((cx, yf, cz), (0.05, 0.05, fh), "frame"))  # mullion
        glass.append(make_box((cx, yf, cz), (fw - 0.12, 0.04, fh - 0.12), "glass", mat=MAT_GLASS))

    # --- back wall ---
    yb = -HY + WALL_T * 0.5
    rear_win = (-2.4, -1.2)
    for x0, x1, holes in ((-HX, rear_win[0], []), (rear_win[0], rear_win[1], [(2.05, 3.05)]), (rear_win[1], HX, [])):
        for z0, z1 in spans_minus(0.0, H, holes):
            wall_piece(parts, x0, x1, yb, z0, z1)
    glass.append(make_box(((rear_win[0] + rear_win[1]) * 0.5, yb, 2.55), (1.1, 0.04, 0.9), "glass", mat=MAT_GLASS))

    # --- side walls (the +X one carries the yard exit) ---
    wall_piece(parts, -HY, HY, -HX + WALL_T * 0.5, 0.0, H, axis="y")
    for a, b, holes in ((-HY, SIDE_DOOR[0], []),
                        (SIDE_DOOR[0], SIDE_DOOR[1], [(0.0, OFF_H)]),
                        (SIDE_DOOR[1], HY, [])):
        for z0, z1 in spans_minus(0.0, H, holes):
            wall_piece(parts, a, b, HX - WALL_T * 0.5, z0, z1, axis="y")
    # stoop outside the yard exit
    sdy = (SIDE_DOOR[0] + SIDE_DOOR[1]) * 0.5
    parts.append(make_box((HX + 0.55, sdy, 0.05), (1.20, 1.60, 0.10), "concrete"))
    parts.append(make_box((HX + 0.42, sdy, OFF_H + 0.26), (0.95, 1.45, 0.07), "corr_rust",
                          rot=(0, math.radians(8), 0)))
    # side window (office end)
    parts.append(make_box((-HX + WALL_T * 0.5, 3.6, 2.05), (WALL_T + 0.02, 1.15, 0.85), "frame"))
    glass.append(make_box((-HX + WALL_T * 0.5, 3.6, 2.05), (0.04, 1.0, 0.7), "glass", mat=MAT_GLASS))

    # --- office partitions ---
    for a, b in spans_minus(OFFICE_Y, HY - WALL_T, [(3.30, 4.32)]):
        parts.append(make_box((OFFICE_X, (a + b) * 0.5, H * 0.5), (WALL_T, b - a, H), "interior_wall"))
    parts.append(make_box((OFFICE_X, 3.81, (2.20 + H) * 0.5), (WALL_T, 1.02, H - 2.20), "interior_wall"))
    parts.append(make_box(((-HX + OFFICE_X) * 0.5, OFFICE_Y, H * 0.5),
                          (OFFICE_X + HX, WALL_T, H), "interior_wall"))

    # --- floor with the inspection pit cut out ---
    xi0, xi1 = -HX + WALL_T, HX - WALL_T
    yi0, yi1 = -HY + WALL_T, HY - WALL_T
    zt = -FLOOR_T * 0.5
    px0, px1, py0, py1 = PIT
    parts.append(make_box(((xi0 + xi1) * 0.5, (yi0 + py0) * 0.5, zt), (xi1 - xi0, py0 - yi0, FLOOR_T), "floor"))
    parts.append(make_box(((xi0 + xi1) * 0.5, (py1 + yi1) * 0.5, zt), (xi1 - xi0, yi1 - py1, FLOOR_T), "floor"))
    parts.append(make_box(((xi0 + px0) * 0.5, (py0 + py1) * 0.5, zt), (px0 - xi0, py1 - py0, FLOOR_T), "floor"))
    parts.append(make_box(((px1 + xi1) * 0.5, (py0 + py1) * 0.5, zt), (xi1 - px1, py1 - py0, FLOOR_T), "floor"))

    # pit walls only - the bottom slab is deliberately left out, so the pit
    # opens straight through instead of showing a box hanging under the floor
    pcx, pcy = (px0 + px1) * 0.5, (py0 + py1) * 0.5
    pw, pd = px1 - px0, py1 - py0
    parts.append(make_box((px0 - 0.06, pcy, -PIT_D * 0.5), (0.12, pd, PIT_D), "concrete"))
    parts.append(make_box((px1 + 0.06, pcy, -PIT_D * 0.5), (0.12, pd, PIT_D), "concrete"))
    parts.append(make_box((pcx, py0 - 0.06, -PIT_D * 0.5), (pw + 0.24, 0.12, PIT_D), "concrete"))
    parts.append(make_box((pcx, py1 + 0.06, -PIT_D * 0.5), (pw + 0.24, 0.12, PIT_D), "concrete"))
    # yellow lip markings
    for yy in (py0 - 0.09, py1 + 0.09):
        parts.append(make_box((pcx, yy, 0.012), (pw + 0.3, 0.10, 0.03), "yellow"))
    for xx in (px0 - 0.09, px1 + 0.09):
        parts.append(make_box((xx, pcy, 0.012), (0.10, pd, 0.03), "yellow"))

    return parts, glass


def build_roof() -> List[bpy.types.Object]:
    parts = []
    parts.append(make_box((0.0, 0.0, H + ROOF_T * 0.5), (W + 0.30, D + 0.30, ROOF_T), "roof"))
    # parapet
    top = H + ROOF_T
    for (cx, cy, sx, sy) in ((0.0, HY + 0.15, W + 0.30, 0.16),
                             (0.0, -HY - 0.15, W + 0.30, 0.16),
                             (-HX - 0.15, 0.0, 0.16, D + 0.30),
                             (HX + 0.15, 0.0, 0.16, D + 0.30)):
        parts.append(make_box((cx, cy, top + PARAPET_H * 0.5), (sx, sy, PARAPET_H), "plaster"))
    # roof clutter from the concept: patched panels, a hatch, a vent, a pipe
    # patches bite into the deck instead of straddling its top plane, which was
    # making the whole roof flicker
    parts.append(make_box((-3.2, -1.6, top - 0.02), (3.0, 2.4, 0.06), "corr_rust"))
    parts.append(make_box((3.6, 2.0, top - 0.02), (2.6, 2.2, 0.06), "corr_rust"))
    parts.append(make_box((1.0, -0.4, top + 0.09), (1.20, 1.20, 0.18), "frame"))
    parts.append(make_box((-1.4, 2.6, top + 0.14), (0.9, 0.9, 0.28), "blue_metal"))
    parts.append(make_cyl((5.6, -3.4, top + 0.62), 0.13, 1.25, "rust", verts=8))
    parts.append(make_cyl((5.6, -3.4, top + 1.28), 0.19, 0.14, "rust_dark", verts=8))
    return parts


def build_sign_board() -> bpy.types.Object:
    """Painted board over the bays - the lettering comes from the atlas."""
    board = make_box((SIGN_X, HY + 0.09, SIGN_Z), (SIGN_W, 0.16, SIGN_H), "wood")
    # facing +Y: the outside viewer's right is -X, so flip u to read correctly
    override_face_uv(board, "sign", axis=1, positive=True, flip_u=True)
    brackets = []
    for dx in (-SIGN_W * 0.42, SIGN_W * 0.42):
        brackets.append(make_box((SIGN_X + dx, HY + 0.02, SIGN_Z - SIGN_H * 0.5 - 0.10),
                                 (0.10, 0.12, 0.22), "rust"))
    return join_as("AS_Sign", [board] + brackets, origin=None)


def build_doors():
    """Office door + two shutters. Origins sit on the hinge / on the header."""
    doors = []

    # office door, hinge on the -X jamb, inside the reveal
    ocx = (DOOR_OFF[0] + DOOR_OFF[1]) * 0.5
    y_in = HY - WALL_T - 0.04
    leaf = make_box((ocx, y_in, OFF_H * 0.5), (OFF_W - 0.03, 0.06, OFF_H - 0.03), "blue_metal")
    pane = make_box((ocx, y_in + 0.035, 1.62), (0.44, 0.02, 0.50), "glass_dark")
    handle = make_box((DOOR_OFF[1] - 0.16, y_in + 0.06, 1.05), (0.05, 0.09, 0.14), "chrome")
    door = join_as("AS_Door_Office", [leaf, pane, handle], origin=None)
    origin_to(door, (DOOR_OFF[0] + 0.02, y_in, OFF_H * 0.5))
    doors.append(door)

    # yard exit in the +X wall, hinge on the -Y jamb
    sdy = (SIDE_DOOR[0] + SIDE_DOOR[1]) * 0.5
    x_in = HX - WALL_T - 0.04
    leaf = make_box((x_in, sdy, OFF_H * 0.5), (0.06, OFF_W - 0.03, OFF_H - 0.03), "blue_metal")
    pane = make_box((x_in - 0.035, sdy, 1.62), (0.02, 0.44, 0.50), "glass_dark")
    grip = make_box((x_in - 0.06, SIDE_DOOR[1] - 0.16, 1.05), (0.05, 0.09, 0.14), "chrome")
    side = join_as("AS_Door_Side", [leaf, pane, grip], origin=None)
    origin_to(side, (x_in, SIDE_DOOR[0] + 0.02, OFF_H * 0.5))
    doors.append(side)

    # roll-up shutters: origin on the header centre, they slide up behind the wall
    def shutter(name: str, x0: float, x1: float, open_frac: float) -> bpy.types.Object:
        cx = (x0 + x1) * 0.5
        y_sh = HY - WALL_T - 0.06
        panel = make_box((cx, y_sh, GAR_H * 0.5), (x1 - x0 - 0.05, 0.07, GAR_H - 0.02), "corrugated")
        ribs = [panel]
        n = 9
        for i in range(1, n):
            z = i * GAR_H / n
            ribs.append(make_box((cx, y_sh + 0.05, z), (x1 - x0 - 0.09, 0.05, 0.05), "corr_rust"))
        obj = join_as(name, ribs, origin=None)
        origin_to(obj, (cx, y_sh, GAR_H))
        if open_frac > 0.0:
            obj.location.z += GAR_H * open_frac
        return obj

    doors.append(shutter("AS_Door_Garage_L", GAR_L[0], GAR_L[1], 0.0))
    doors.append(shutter("AS_Door_Garage_R", GAR_R[0], GAR_R[1], 0.40))
    return doors


def build_awning() -> List[bpy.types.Object]:
    ocx = (DOOR_OFF[0] + DOOR_OFF[1]) * 0.5
    parts = [make_box((ocx, HY + 0.42, OFF_H + 0.30), (1.85, 0.95, 0.07), "corr_rust", rot=(math.radians(-9), 0, 0))]
    for dx in (-0.80, 0.80):
        parts.append(make_box((ocx + dx, HY + 0.12, OFF_H + 0.10), (0.06, 0.06, 0.55), "rust"))
        parts.append(make_box((ocx + dx, HY + 0.42, OFF_H + 0.02), (0.05, 0.60, 0.05), "rust",
                              rot=(math.radians(30), 0, 0)))
    # step slab, lifted clear of the apron so their bottom faces do not coincide
    parts.append(make_box((ocx, HY + 0.55, 0.12), (1.70, 1.10, 0.12), "concrete"))
    return parts


def build_apron() -> bpy.types.Object:
    """Overlapping slabs get their own height band. They used to sit within a
    centimetre of each other and flickered against the ground and each other."""
    pad = make_box((2.3, HY + 3.0, 0.035), (11.0, 6.0, 0.11), "concrete")     # -0.020 .. 0.090
    strip = make_box((2.3, HY + 6.9, 0.030), (12.6, 3.0, 0.04), "asphalt")    #  0.010 .. 0.050
    side = make_box((-5.9, HY + 1.3, 0.048), (4.6, 2.6, 0.045), "dirt")       #  0.025 .. 0.070
    return join_as("AS_Apron", [pad, strip, side])


# ---------------------------------------------------------------------------
# props
# ---------------------------------------------------------------------------
def build_parts_rack(name: str, loc, yaw: float) -> bpy.types.Object:
    parts = []
    for x in (-0.62, 0.62):
        parts.append(make_box((x, -0.19, 1.15), (0.07, 0.07, 2.30), "rust"))
        parts.append(make_box((x, 0.19, 1.15), (0.07, 0.07, 2.30), "rust"))
    for z in (0.18, 0.86, 1.54, 2.18):
        parts.append(make_box((0.0, 0.0, z), (1.34, 0.46, 0.05), "rust"))
    stock = [
        (-0.36, 0.0, 0.42, 0.42, 0.32, 0.44, "crate"),
        (0.30, 0.02, 0.40, 0.36, 0.34, 0.40, "red"),
        (-0.30, -0.02, 1.12, 0.46, 0.30, 0.46, "yellow"),
        (0.34, 0.0, 1.08, 0.30, 0.30, 0.38, "engine"),
        (-0.32, 0.0, 1.80, 0.38, 0.26, 0.46, "crate"),
        (0.30, 0.0, 1.76, 0.26, 0.26, 0.38, "dark"),
        (0.02, 0.0, 2.36, 0.50, 0.32, 0.30, "canvas"),
    ]
    for x, y, z, sx, sy, sz, tile in stock:
        parts.append(make_box((x, y, z), (sx, sy, sz), tile))
    for i, (x, y, z) in enumerate(((-0.18, 0.06, 0.60), (0.16, -0.06, 1.30))):
        parts.append(make_cyl((x, y, z), 0.10, 0.30, "blue_metal" if i else "red",
                              verts=8, rot=(math.radians(90), 0, 0)))
    obj = join_as(name, parts)
    obj.location = loc
    obj.rotation_euler[2] = yaw
    return obj


def build_workbench() -> bpy.types.Object:
    parts = []
    L = 7.6
    parts.append(make_box((0.0, 0.0, 0.86), (L, 0.70, 0.07), "wood"))
    for x in [v * 0.5 for v in range(-int(L), int(L) + 1, 3)]:
        parts.append(make_box((x, -0.26, 0.42), (0.08, 0.08, 0.84), "wood"))
        parts.append(make_box((x, 0.26, 0.42), (0.08, 0.08, 0.84), "wood"))
    # pegboard with tools
    parts.append(make_box((0.0, -0.36, 1.62), (L, 0.05, 1.25), "wood"))
    for i, x in enumerate((-3.1, -2.4, -1.6, -0.8, 0.1, 0.9, 1.7, 2.5, 3.2)):
        parts.append(make_box((x, -0.31, 1.78), (0.07, 0.16, 0.42), "tool" if i % 2 else "chrome"))
        parts.append(make_box((x + 0.16, -0.31, 1.35), (0.20, 0.14, 0.07), "tool"))
    # tool chests, vice, compressor
    parts.append(make_box((-2.6, 0.06, 0.42), (0.90, 0.52, 0.84), "red"))
    parts.append(make_box((2.2, 0.06, 0.34), (0.74, 0.50, 0.68), "red"))
    parts.append(make_box((-0.9, 0.0, 0.97), (0.22, 0.18, 0.16), "chrome"))
    parts.append(make_cyl((3.4, 0.04, 0.44), 0.26, 0.62, "blue_metal", verts=10, rot=(0, math.radians(90), 0)))
    parts.append(make_box((3.4, 0.04, 0.10), (0.62, 0.42, 0.14), "rust"))
    parts.append(make_cyl((3.55, 0.04, 0.78), 0.07, 0.24, "red", verts=8))
    # clutter
    parts.append(make_box((-1.7, 0.05, 1.00), (0.42, 0.28, 0.20), "engine"))
    parts.append(make_box((0.5, 0.02, 1.00), (0.30, 0.22, 0.20), "yellow"))
    parts.append(make_cyl((1.4, 0.08, 1.00), 0.09, 0.20, "barrel", verts=8))
    obj = join_as("AS_Workbench", parts)
    obj.location = (0.6, -HY + 0.75, 0.0)
    return obj


def build_lift(name: str, loc) -> bpy.types.Object:
    parts = []
    for x in (-1.12, 1.12):
        parts.append(make_box((x, 0.0, 1.30), (0.20, 0.26, 2.60), "blue_metal"))
        parts.append(make_box((x, 0.0, 0.05), (0.52, 0.62, 0.10), "rust"))
        parts.append(make_box((x, 0.0, 2.62), (0.24, 0.30, 0.10), "blue_metal"))
        inner = -0.55 if x > 0 else 0.55
        for dy in (-0.34, 0.34):
            parts.append(make_box((x + inner * 0.55, dy, 0.62), (1.05, 0.13, 0.09), "blue_metal"))
            parts.append(make_box((x + inner * 1.02, dy, 0.62), (0.26, 0.20, 0.05), "rust"))
    parts.append(make_box((-1.12, 0.0, 2.72), (2.44, 0.16, 0.10), "blue_metal"))
    parts.append(make_box((-1.30, -0.40, 1.10), (0.16, 0.14, 0.34), "red"))
    obj = join_as(name, parts)
    obj.location = loc
    return obj


def build_office_desk() -> bpy.types.Object:
    parts = []
    parts.append(make_box((0.0, 0.0, 0.75), (1.45, 0.72, 0.06), "wood"))
    parts.append(make_box((-0.52, 0.0, 0.38), (0.36, 0.66, 0.70), "wood"))
    for x, y in ((0.66, -0.30), (0.66, 0.30)):
        parts.append(make_box((x, y, 0.37), (0.07, 0.07, 0.74), "wood"))
    parts.append(make_box((0.20, -0.06, 0.80), (0.38, 0.30, 0.03), "paper"))
    parts.append(make_box((-0.30, 0.14, 0.83), (0.24, 0.20, 0.08), "paper"))
    parts.append(make_box((0.52, 0.16, 0.86), (0.26, 0.22, 0.18), "dark"))
    # chair
    parts.append(make_box((0.05, 0.88, 0.46), (0.44, 0.44, 0.08), "seat"))
    parts.append(make_box((0.05, 1.06, 0.86), (0.44, 0.08, 0.58), "seat"))
    for x, y in ((-0.13, 0.74), (0.23, 0.74), (-0.13, 1.02), (0.23, 1.02)):
        parts.append(make_box((x, y, 0.23), (0.05, 0.05, 0.46), "dark"))
    # filing cabinet + wall notices
    parts.append(make_box((1.35, 0.30, 0.62), (0.52, 0.60, 1.24), "blue_metal"))
    parts.append(make_box((-0.60, -0.62, 1.62), (0.52, 0.03, 0.62), "paper"))
    parts.append(make_box((0.16, -0.62, 1.72), (0.34, 0.03, 0.44), "paper"))
    obj = join_as("AS_OfficeDesk", parts)
    obj.location = (-5.70, 3.90, 0.0)
    obj.rotation_euler[2] = math.radians(-8)
    return obj


def tire_stack(x: float, y: float, n: int, z0: float = 0.11, r: float = 0.33) -> List[bpy.types.Object]:
    return [make_cyl((x, y, z0 + i * 0.21), r, 0.20, "rubber", verts=12) for i in range(n)]


def build_tire_shed() -> bpy.types.Object:
    parts = []
    for x, y in ((-1.75, -1.25), (1.75, -1.25), (-1.75, 1.25), (1.75, 1.25)):
        parts.append(make_box((x, y, 1.20), (0.13, 0.13, 2.40), "wood"))
    parts.append(make_box((0.0, 0.0, 2.52), (3.90, 2.90, 0.09), "corr_rust", rot=(math.radians(-9), 0, 0)))
    for x, y in ((-1.75, 0.0), (1.75, 0.0)):
        parts.append(make_box((x, y, 2.36), (0.10, 2.70, 0.16), "wood"))
    parts += tire_stack(-1.05, 0.35, 6)
    parts += tire_stack(-0.15, 0.55, 5)
    parts += tire_stack(0.95, 0.15, 7)
    parts += tire_stack(-0.35, -0.75, 4)
    parts += tire_stack(1.15, -0.85, 3)
    parts.append(make_cyl((1.30, 0.95, 0.44), 0.29, 0.88, "barrel", verts=12))
    parts.append(make_cyl((-1.35, 0.90, 0.44), 0.29, 0.88, "blue_metal", verts=12))
    obj = join_as("AS_TireShed", parts)
    obj.location = (-11.60, 2.10, 0.0)
    obj.rotation_euler[2] = math.radians(6)
    return obj


def build_storage_shed() -> bpy.types.Object:
    parts = []
    parts.append(make_box((0.0, 0.0, 1.15), (3.30, 2.70, 2.30), "corr_rust"))
    parts.append(make_box((0.0, -0.10, 2.44), (3.60, 3.10, 0.10), "corrugated", rot=(math.radians(-7), 0, 0)))
    parts.append(make_box((0.0, 1.37, 1.02), (1.10, 0.07, 1.90), "rust"))
    parts.append(make_box((-0.34, 1.42, 1.02), (0.05, 0.05, 1.86), "rust_dark"))
    parts.append(make_box((0.90, 1.40, 1.70), (0.40, 0.04, 0.34), "dark"))
    for x, y in ((-1.72, 0.0), (1.72, 0.0)):
        parts.append(make_box((x, y, 1.15), (0.10, 2.74, 2.34), "wood"))
    obj = join_as("AS_StorageShed", parts)
    obj.location = (11.70, 1.60, 0.0)
    obj.rotation_euler[2] = math.radians(-5)
    return obj


def build_crates() -> bpy.types.Object:
    parts = [
        make_box((0.0, 0.0, 0.30), (0.60, 0.50, 0.60), "crate"),
        make_box((0.56, 0.10, 0.24), (0.48, 0.44, 0.48), "wood"),
        make_box((0.20, -0.06, 0.76), (0.46, 0.40, 0.32), "crate"),
        make_box((-0.52, 0.14, 0.22), (0.42, 0.38, 0.44), "crate"),
    ]
    parts.append(make_cyl((1.20, 0.05, 0.44), 0.29, 0.88, "barrel", verts=12))
    obj = join_as("AS_Crates", parts)
    obj.location = (-7.10, HY + 1.30, 0.0)
    obj.rotation_euler[2] = math.radians(14)
    return obj


def build_pallets() -> bpy.types.Object:
    parts = []
    for (x, y, z, rz) in ((0.0, 0.0, 0.07, 0.0), (0.12, 0.18, 0.21, 0.12), (1.25, -0.15, 0.07, -0.2)):
        parts.append(make_box((x, y, z), (1.20, 0.90, 0.10), "wood", rot=(0, 0, rz)))
        for dy in (-0.38, 0.0, 0.38):
            parts.append(make_box((x, y + dy, z - 0.06), (1.16, 0.10, 0.08), "wood", rot=(0, 0, rz)))
    parts.append(make_cyl((1.95, 0.30, 0.44), 0.29, 0.88, "barrel", verts=12))
    parts.append(make_cyl((2.35, -0.20, 0.44), 0.29, 0.88, "rust", verts=12))
    parts += tire_stack(2.30, 0.75, 3)
    obj = join_as("AS_Pallets", parts)
    obj.location = (9.60, 4.90, 0.0)
    obj.rotation_euler[2] = math.radians(-12)
    return obj


# ---------------------------------------------------------------------------
# cars
# ---------------------------------------------------------------------------
# cars come from as_common so the scrap pile and the yard wrecks stay
# the exact same model
build_car = as_common.build_sedan


# ---------------------------------------------------------------------------
# preview + export
# ---------------------------------------------------------------------------
def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 800
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Filmic" if "Filmic" in [
        v.name for v in scene.view_settings.bl_rna.properties["view_transform"].enum_items] else "Standard"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("ASWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.58, 0.66, 0.60, 1.0)
        bg.inputs[1].default_value = 1.15
    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 3.4
    sun_ob = bpy.data.objects.new("Sun", sun)
    bpy.context.collection.objects.link(sun_ob)
    sun_ob.rotation_euler = Euler((math.radians(52), math.radians(8), math.radians(-35)))
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    return scene, cam, cam_data


def render_to(scene, path: str) -> None:
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("[AutoService] render:", path)


def render_previews(roof: bpy.types.Object) -> None:
    scene, cam, cam_data = setup_render()

    cam_data.lens = 38
    cam.location = Vector((-13.0, 24.0, 12.5))
    look_at(cam, Vector((0.0, 2.0, 2.0)))
    render_to(scene, os.path.join(OUT_DIR, "AutoService_preview.png"))

    cam_data.lens = 50
    cam.location = Vector((1.0, 21.0, 3.4))
    look_at(cam, Vector((1.6, HY, 2.6)))
    render_to(scene, os.path.join(OUT_DIR, "AutoService_preview_front.png"))

    cam_data.lens = 55
    cam.location = Vector((16.0, 17.5, 3.0))
    look_at(cam, Vector((10.6, 9.6, 0.75)))
    render_to(scene, os.path.join(OUT_DIR, "AutoService_preview_cars.png"))

    # down into a cabin with the glass hidden, to check seats / wheel / footwell
    glass = [o for o in bpy.data.objects if o.type == "MESH" and o.name.endswith("_Glass")]
    for g in glass:
        g.hide_render = True
    cam_data.lens = 42
    cam.location = Vector((9.9, 3.9, 3.3))
    look_at(cam, Vector((13.1, 7.5, 0.85)))
    render_to(scene, os.path.join(OUT_DIR, "AutoService_preview_cabin.png"))
    for g in glass:
        g.hide_render = False

    # interior: drop the roof for the shot only
    roof.hide_render = True
    cam_data.lens = 24
    cam.location = Vector((-1.4, 10.0, 8.6))
    look_at(cam, Vector((1.5, -1.0, 0.8)))
    render_to(scene, os.path.join(OUT_DIR, "AutoService_preview_interior.png"))

    # eye level, standing just inside the open bay
    cam_data.lens = 28
    cam.location = Vector((4.7, 4.6, 1.65))
    look_at(cam, Vector((1.0, -4.0, 1.2)))
    render_to(scene, os.path.join(OUT_DIR, "AutoService_preview_eye.png"))
    roof.hide_render = False


def export_fbx(root: bpy.types.Object) -> None:
    """Export the meshes only - no parent empty.

    With a parent empty in the file, Blender parks the Z-up -> Y-up correction
    on that empty's rotation and leaves the geometry Z-up; anything that later
    normalises the root transform in Unity silently lays the building on its
    back. Dropping the empty lets bake_space_transform write the conversion
    into the vertices, so every object arrives upright at identity.
    """
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [o for o in root.children_recursive if o.type == "MESH"]
    # The model is authored facade-on-+Y, which the -Z/Y preset lands on Unity's
    # -Z. Spinning it half a turn here (and baking that into the vertices) puts
    # the bays on +Z with every transform left at identity. Runs after the .blend
    # is saved, so the authoring orientation is untouched.
    half_turn = Euler((0.0, 0.0, math.pi)).to_matrix().to_4x4()
    for obj in meshes:
        obj.parent = None
        obj.matrix_world = half_turn @ obj.matrix_world
        obj.select_set(True)
    bpy.context.view_layer.update()
    for obj in meshes:
        apply_trs(obj, loc=False, rot=True, scale=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,
        object_types={"MESH"},
        mesh_smooth_type="OFF",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=False,
    )
    print("[AutoService] fbx:", FBX_PATH)


# ---------------------------------------------------------------------------
def main() -> None:
    global MAT, MAT_GLASS
    clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    img = as_atlas.save_atlas(ATLAS_PATH)
    MAT, MAT_GLASS = make_materials(img)
    as_common.set_materials(MAT, MAT_GLASS)   # the shared sedan builder uses these

    root = new_empty("AutoService_Building", (0.0, 0.0, 0.0))
    flat: List[bpy.types.Object] = []

    shell_parts, glass_parts = build_shell()
    shell_parts += build_awning()
    flat.append(join_as("AS_Building", shell_parts, origin=None))
    flat.append(join_as("AS_Glass", glass_parts, origin=None))
    roof = join_as("AS_Roof", build_roof(), origin=None)
    flat.append(roof)
    flat.append(build_sign_board())
    flat += build_doors()
    flat.append(build_apron())

    flat.append(build_parts_rack("AS_PartsRack_A", (-7.30, -3.60, 0.0), math.radians(90)))
    flat.append(build_parts_rack("AS_PartsRack_B", (-7.30, -1.20, 0.0), math.radians(90)))
    flat.append(build_workbench())
    flat.append(build_lift("AS_Lift_A", (-0.05, 2.10, 0.0)))
    flat.append(build_lift("AS_Lift_B", (-0.05, -1.90, 0.0)))
    flat.append(build_office_desk())
    flat.append(build_tire_shed())
    flat.append(build_storage_shed())
    flat.append(build_crates())
    flat.append(build_pallets())

    # green car under repair, over the pit, hood removed
    flat += build_car("AS_Car_Repair", (4.70, 0.80, 0.0), math.radians(0), "car_green", no_hood=True)
    # yard wrecks
    flat += build_car("AS_Car_Blue", (-11.40, 8.60, 0.0), math.radians(-118), "car_blue", no_bumper=True)
    flat += build_car("AS_Car_Orange", (9.40, 9.10, 0.0), math.radians(28), "car_orange", no_hood=True)
    flat += build_car("AS_Car_Beige", (13.10, 7.40, 0.0), math.radians(-16), "car_beige")
    flat += build_car("AS_Car_Green", (8.30, 13.20, 0.0), math.radians(8), "car_green", no_wheel_fl=True)

    for obj in flat:
        obj.parent = root

    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    try:
        render_previews(roof)
    except Exception as exc:  # previews must never block the export
        print("[AutoService] preview failed:", exc)
    export_fbx(root)

    tris = 0
    for o in bpy.data.objects:
        if o.type == "MESH":
            tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
    print("[AutoService] objects:", len(flat), " tris:", tris)
    print("[AutoService] blend:", BLEND_PATH)


if __name__ == "__main__":
    main()
