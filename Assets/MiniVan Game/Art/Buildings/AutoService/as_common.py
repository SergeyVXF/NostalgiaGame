"""
Shared low-poly building blocks for the MiniVan props.

Geometry helpers, the atlas material setup and the sedan generator all live
here so the auto service, the tower crane and the scrap pile stay one visual
family and the car only has to be fixed in one place.

Blender space: Z up. Call set_materials() before building anything.
"""
from __future__ import annotations

import math
import os
from typing import Iterable, List, Optional, Sequence, Tuple

import bmesh
import bpy
from mathutils import Euler, Vector

import as_atlas

MAT: Optional[bpy.types.Material] = None
MAT_GLASS: Optional[bpy.types.Material] = None
_SEQ = 0


def set_materials(lit, glass) -> None:
    """Point the helpers at the materials every builder should use."""
    global MAT, MAT_GLASS
    MAT, MAT_GLASS = lit, glass


def glass_mat():
    return MAT_GLASS


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

# ---------------------------------------------------------------------------
# cars
# ---------------------------------------------------------------------------
CAR_HALF_W = 0.86       # outer body skin
CABIN_HALF_W = 0.80     # greenhouse frame
DOOR_X = 0.83           # doors sit flush in the 0.80..0.86 gap
WHEEL_R = 0.33
WHEEL_W = 0.22
AXLE_Y = 1.28           # wheel centres at ±AXLE_Y

# The body is three separate solids so the car is genuinely hollow:
# front clip with an open engine bay, rear clip with an open boot, and a
# floor pan in between that the cabin sits on.
FRONT_PROFILE = [
    (0.92, 0.26), (2.12, 0.26), (2.12, 0.74), (2.00, 0.86),
    (1.92, 0.62), (0.98, 0.62),                  # engine bay well
    (0.92, 0.96),
]
REAR_PROFILE = [
    (-2.10, 0.26), (-1.30, 0.26), (-1.30, 0.96),
    (-1.38, 0.56), (-1.96, 0.56),                # boot well
    (-2.04, 0.90), (-2.10, 0.84),
]

CABIN_Y0, CABIN_Y1 = -1.30, 0.92                 # passenger compartment
FLOOR_Z = 0.44                                   # cabin floor height
SILL_Z = 0.50                                    # bottom of the door aperture
BELT_Z = 0.96                                    # top of the door aperture
ROOF_Z = 1.42
B_PILLAR_Y = -0.42


def build_wheel(loc, tile="rubber", flat: bool = False):
    r = WHEEL_R * (0.94 if flat else 1.0)
    parts = [make_cyl(loc, r, WHEEL_W, tile, verts=12, rot=(0, math.radians(90), 0))]
    hub_x = loc[0] + (WHEEL_W * 0.5 + 0.005) * (1 if loc[0] > 0 else -1)
    parts.append(make_cyl((hub_x, loc[1], loc[2]), r * 0.44, 0.03, "hubcap", verts=10,
                          rot=(0, math.radians(90), 0)))
    return parts


def build_sedan(name: str, loc, yaw: float, color: str,
              no_hood: bool = False, no_wheel_fl: bool = False,
              no_bumper: bool = False, no_trunk_lid: bool = False) -> List[bpy.types.Object]:
    """Returns a flat list of meshes:
    <name>_Body, _Glass, _Door_FL/FR/RL/RR, _Hood, _Trunk."""
    body_parts: List[bpy.types.Object] = []
    glass_parts: List[bpy.types.Object] = []

    # --- structure: front clip, rear clip, floor pan ---
    body_parts.append(make_prism(FRONT_PROFILE, CAR_HALF_W, color))
    body_parts.append(make_prism(REAR_PROFILE, CAR_HALF_W, color))
    cab_len = CABIN_Y1 - CABIN_Y0
    cab_mid = (CABIN_Y0 + CABIN_Y1) * 0.5
    # stops where the sills start: sharing the x=CAR_HALF_W plane with the sill
    # and the door made those faces flicker against each other
    body_parts.append(make_box((0.0, cab_mid, 0.35), ((DOOR_X - 0.03) * 2, cab_len, 0.18), color))

    # sills, beltline rail and B-pillar frame the door aperture
    for sx in (-1, 1):
        x = sx * DOOR_X
        body_parts.append(make_box((x, cab_mid, 0.37), (0.06, cab_len, 0.26), color))
        body_parts.append(make_box((x, cab_mid, 1.00), (0.06, cab_len, 0.08), color))
        body_parts.append(make_box((x, B_PILLAR_Y, (SILL_Z + ROOF_Z) * 0.5),
                                   (0.07, 0.08, ROOF_Z - SILL_Z), color))

    # --- greenhouse frame (pillars follow the real edges, so no loose panels) ---
    ws0, ws1 = (0.92, BELT_Z + 0.02), (0.26, ROOF_Z)
    bl0, bl1 = (CABIN_Y0, BELT_Z + 0.02), (-1.02, ROOF_Z)
    for sx in (-1, 1):
        x = sx * CABIN_HALF_W
        body_parts.append(make_prism(bar_profile(ws0, ws1, 0.09), 0.04, color, loc=(x, 0, 0)))
        body_parts.append(make_prism(bar_profile(bl0, bl1, 0.09), 0.04, color, loc=(x, 0, 0)))
        body_parts.append(make_prism(bar_profile(ws1, bl1, 0.09), 0.04, color, loc=(x, 0, 0)))
    body_parts.append(make_box((0.0, -0.38, ROOF_Z), (CABIN_HALF_W * 2, 1.28, 0.08), color))
    body_parts.append(make_box((0.0, 0.92, 0.99), (CABIN_HALF_W * 2, 0.08, 0.10), color))
    body_parts.append(make_box((0.0, CABIN_Y0, 0.99), (CABIN_HALF_W * 2, 0.08, 0.10), color))

    # --- glass, kept as its own object so it can simply be deleted ---
    glass_parts.append(make_prism(bar_profile(ws0, ws1, 0.03), 0.72, "glass", mat=MAT_GLASS))
    glass_parts.append(make_prism(bar_profile(bl0, bl1, 0.03), 0.72, "glass", mat=MAT_GLASS))
    side_f = [(0.86, 1.00), (0.30, ROOF_Z - 0.05), (-0.38, ROOF_Z - 0.05), (-0.38, 1.00)]
    side_r = [(-0.46, 1.00), (-0.46, ROOF_Z - 0.05), (-1.06, ROOF_Z - 0.05), (-1.22, 1.00)]
    for sx in (-1, 1):
        for prof in (side_f, side_r):
            glass_parts.append(make_prism(prof, 0.015, "glass",
                                          loc=(sx * (CABIN_HALF_W - 0.01), 0, 0), mat=MAT_GLASS))

    # --- interior: floor, seats, wheel, dash ---
    # sits proud of the floor pan: sharing the pan's top plane made the whole
    # cabin floor flicker
    body_parts.append(make_box((0.0, cab_mid, FLOOR_Z - 0.005), (CAR_HALF_W * 2 - 0.14, cab_len - 0.06, 0.05), "interior_dark"))
    body_parts.append(make_box((0.0, 0.70, 0.84), (1.52, 0.32, 0.26), "interior_dark"))
    for sx in (-1, 1):
        body_parts.append(make_box((sx * 0.40, 0.16, FLOOR_Z + 0.07), (0.46, 0.48, 0.14), "seat"))
        body_parts.append(make_box((sx * 0.40, -0.10, FLOOR_Z + 0.34), (0.46, 0.14, 0.54), "seat"))
    body_parts.append(make_box((0.0, -0.80, FLOOR_Z + 0.07), (1.44, 0.44, 0.14), "seat"))
    body_parts.append(make_box((0.0, -1.04, FLOOR_Z + 0.32), (1.44, 0.14, 0.50), "seat"))
    body_parts.append(make_torus((-0.40, 0.50, 0.90), 0.16, 0.022, "dark",
                                 rot=(math.radians(72), 0, 0)))
    body_parts.append(make_box((-0.40, 0.60, 0.84), (0.05, 0.24, 0.05), "dark"))
    body_parts.append(make_box((0.0, 0.10, FLOOR_Z + 0.14), (0.05, 0.05, 0.22), "chrome"))

    # --- wheel arches, sills, bumpers, lights ---
    for sy in (AXLE_Y, -AXLE_Y):
        for sx in (-1, 1):
            # recess kept strictly inside the skin, otherwise its outer face is
            # coplanar with the sill and the door and the pair z-fights
            body_parts.append(make_box((sx * (CAR_HALF_W - 0.09), sy, 0.42), (0.10, 0.92, 0.56), "interior_dark"))
            body_parts.append(make_box((sx * (CAR_HALF_W + 0.03), sy, 0.74), (0.09, 1.00, 0.16), color))
    if not no_bumper:
        body_parts.append(make_box((0.0, 2.16, 0.44), (CAR_HALF_W * 2 - 0.06, 0.14, 0.22), "bumper"))
    body_parts.append(make_box((0.0, -2.14, 0.44), (CAR_HALF_W * 2 - 0.06, 0.14, 0.22), "bumper"))
    for sx in (-1, 1):
        # sunk into the panel rather than skimming it, so no near-coplanar pair
        body_parts.append(make_box((sx * 0.56, 2.09, 0.68), (0.34, 0.10, 0.16), "headlight"))
        body_parts.append(make_box((sx * 0.56, -2.07, 0.68), (0.34, 0.10, 0.16), "taillight"))
    body_parts.append(make_box((0.0, 2.09, 0.66), (0.70, 0.10, 0.18), "interior_dark"))

    # engine sits inside the open bay
    if no_hood:
        body_parts.append(make_box((0.0, 1.44, 0.80), (1.02, 0.86, 0.34), "engine"))
        body_parts.append(make_box((0.24, 1.14, 0.86), (0.30, 0.26, 0.20), "rust"))
        body_parts.append(make_cyl((-0.28, 1.60, 0.94), 0.09, 0.20, "dark", verts=8))

    # wheels
    for sy in (AXLE_Y, -AXLE_Y):
        for sx in (-1, 1):
            if no_wheel_fl and sx < 0 and sy > 0:
                body_parts.append(make_box((-CAR_HALF_W + 0.16, sy, 0.10), (0.34, 0.30, 0.20), "crate"))
                body_parts.append(make_cyl((-CAR_HALF_W + 0.16, sy, 0.26), 0.09, 0.14, "rust_dark",
                                           verts=8, rot=(0, math.radians(90), 0)))
                continue
            body_parts += build_wheel((sx * (CAR_HALF_W - 0.04), sy, WHEEL_R))

    meshes = [join_as(f"{name}_Body", body_parts, origin=None)]
    meshes.append(join_as(f"{name}_Glass", glass_parts, origin=None))

    # --- doors shaped to the aperture they close ---
    def door(dn: str, sx: int, y0: float, y1: float, hinge_y: float) -> bpy.types.Object:
        x = sx * DOOR_X
        cy = (y0 + y1) * 0.5
        leaf = make_box((x, cy, (SILL_Z + BELT_Z) * 0.5), (0.06, y1 - y0, BELT_Z - SILL_Z), color)
        # shoulder strip along the top edge + handle, so it is not a bare slab
        trim = make_box((x + sx * 0.035, cy, BELT_Z - 0.05), (0.03, y1 - y0 - 0.06, 0.05), "dark")
        hx = cy - (0.36 if hinge_y > cy else -0.36)
        handle = make_box((x + sx * 0.03, hx, BELT_Z - 0.14), (0.03, 0.16, 0.05), "chrome")
        obj = join_as(dn, [leaf, trim, handle], origin=None)
        origin_to(obj, (x, hinge_y, (SILL_Z + BELT_Z) * 0.5))
        return obj

    meshes.append(door(f"{name}_Door_FL", -1, B_PILLAR_Y + 0.04, 0.90, 0.90))
    meshes.append(door(f"{name}_Door_FR", 1, B_PILLAR_Y + 0.04, 0.90, 0.90))
    meshes.append(door(f"{name}_Door_RL", -1, -1.28, B_PILLAR_Y - 0.04, B_PILLAR_Y - 0.04))
    meshes.append(door(f"{name}_Door_RR", 1, -1.28, B_PILLAR_Y - 0.04, B_PILLAR_Y - 0.04))

    if not no_hood:
        lid = make_box((0.0, 1.46, 0.90), (CAR_HALF_W * 2 - 0.04, 1.10, 0.07), color)
        hood = join_as(f"{name}_Hood", [lid], origin=None)
        origin_to(hood, (0.0, 0.94, 0.92))       # hinge at the windscreen end
        meshes.append(hood)

    if not no_trunk_lid:
        lid = make_box((0.0, -1.66, 0.94), (CAR_HALF_W * 2 - 0.04, 0.64, 0.06), color)
        trunk = join_as(f"{name}_Trunk", [lid], origin=None)
        origin_to(trunk, (0.0, -1.32, 0.94))
        meshes.append(trunk)

    # place the whole car, then bake the rotation into every mesh so the FBX
    # exporter cannot re-interpret a parent's axes
    pivot = Vector((loc[0], loc[1], loc[2]))
    tilt = math.radians(-2.0) if no_wheel_fl else 0.0
    for m in meshes:
        set_active(m)
        m.rotation_euler = Euler((0.0, tilt, yaw))
        m.location = pivot + Euler((0.0, tilt, yaw)).to_matrix() @ m.location
        apply_trs(m, loc=False, rot=True, scale=True)
    if no_wheel_fl:
        for m in meshes:
            m.location.z -= 0.10
    return meshes


# ---------------------------------------------------------------------------

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




def export_fbx(root: bpy.types.Object, path: str) -> None:
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
        filepath=path,
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
    print("[common] fbx:", path)
