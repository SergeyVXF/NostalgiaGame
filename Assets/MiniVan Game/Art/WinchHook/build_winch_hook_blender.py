"""
MiniVan winch screw-eye hook (low-poly, single manifold mesh) from concept.

Critical for Unity inverted-hull outlines:
  - one joined solid (boolean UNION, not overlapping loose parts)
  - vertices welded (no FACE-split export)
  - normals recalculated outward
  - shade smooth, no sharp splits

Run:
  "C:\\Program Files\\Blender Foundation\\Blender 3.4\\blender.exe" --background --python build_winch_hook_blender.py
"""
from __future__ import annotations

import math
import os

import bmesh
import bpy
from mathutils import Euler, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(OUT_DIR, "MiniVan_WinchHook.blend")
FBX_PATH = os.path.join(OUT_DIR, "MiniVan_WinchHook.fbx")
OBJ_PATH = os.path.join(OUT_DIR, "MiniVan_WinchHook.obj")
PREVIEW_PATH = os.path.join(OUT_DIR, "MiniVan_WinchHook_preview.png")

MERGE_DISTANCE = 0.00012

# Proportions in meters. Eye stands vertical (hole faces +Y). Tip at -Z / z≈0.
EYE_MAJOR = 0.062
EYE_MINOR = 0.017
EYE_MAJOR_SEGS = 14
EYE_MINOR_SEGS = 8
EYE_CENTER_Z = 0.228

YOKE_ARM_T = 0.015
YOKE_ARM_W = 0.030
YOKE_ARM_H = 0.052
YOKE_GAP = 0.034          # clear space between arms (along Y)
YOKE_BASE_H = 0.016
YOKE_BASE_Z = 0.152

YELLOW_R = 0.038
YELLOW_H = 0.011
YELLOW_Z = 0.134

NUT_R = 0.048
NUT_H = 0.032
NUT_Z = 0.110
NUT_SIDES = 6

SHAFT_TOP_R = 0.030
SHAFT_BOT_R = 0.010
SHAFT_LEN = 0.140
SHAFT_TOP_Z = 0.088

THREAD_COUNT = 7
THREAD_DEPTH = 0.012
THREAD_HEIGHT = 0.012
THREAD_SEGS = 12

TIP_LEN = 0.034


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.collections):
        for item in list(block):
            block.remove(item)


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def ensure_mat(
    name: str,
    color: tuple[float, float, float],
    roughness: float,
    metallic: float,
) -> bpy.types.Material:
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    for node in list(nt.nodes):
        nt.nodes.remove(node)

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (400, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (0, 0)
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = metallic
    if "Specular" in bsdf.inputs:
        bsdf.inputs["Specular"].default_value = 0.4
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.4
    for key in ("Transmission", "Transmission Weight"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.0
    if "Alpha" in bsdf.inputs:
        bsdf.inputs["Alpha"].default_value = 1.0

    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.diffuse_color = (color[0], color[1], color[2], 1.0)
    if hasattr(mat, "metallic"):
        mat.metallic = metallic
    if hasattr(mat, "roughness"):
        mat.roughness = roughness
    if hasattr(mat, "blend_method"):
        mat.blend_method = "OPAQUE"
    if hasattr(mat, "shadow_method"):
        mat.shadow_method = "OPAQUE"
    if hasattr(mat, "use_backface_culling"):
        mat.use_backface_culling = True
    return mat


def assign_mat(obj: bpy.types.Object, mat: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def apply_trs(obj: bpy.types.Object) -> None:
    set_active(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def boolean_union(target: bpy.types.Object, donor: bpy.types.Object) -> None:
    set_active(target)
    mod = target.modifiers.new(name="BoolUnion_" + donor.name, type="BOOLEAN")
    mod.operation = "UNION"
    try:
        mod.solver = "EXACT"
    except Exception:
        pass
    mod.object = donor
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(donor, do_unlink=True)


def make_torus(
    name: str,
    major: float,
    minor: float,
    major_segs: int,
    minor_segs: int,
    location: tuple[float, float, float],
    mat: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    scale: tuple[float, float, float] = (1.0, 1.0, 1.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major,
        minor_radius=minor,
        major_segments=major_segs,
        minor_segments=minor_segs,
        location=location,
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.rotation_euler = Euler(rotation)
    obj.scale = scale
    assign_mat(obj, mat)
    apply_trs(obj)
    return obj


def make_cylinder(
    name: str,
    radius: float,
    depth: float,
    vertices: int,
    location: tuple[float, float, float],
    mat: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        end_fill_type="NGON",
        location=location,
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.rotation_euler = Euler(rotation)
    assign_mat(obj, mat)
    apply_trs(obj)
    return obj


def make_cone(
    name: str,
    radius1: float,
    radius2: float,
    depth: float,
    vertices: int,
    location: tuple[float, float, float],
    mat: bpy.types.Material,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius1,
        radius2=radius2,
        depth=depth,
        end_fill_type="NGON",
        location=location,
    )
    obj = bpy.context.active_object
    obj.name = name
    assign_mat(obj, mat)
    apply_trs(obj)
    return obj


def make_cube(
    name: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    mat: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    obj.rotation_euler = Euler(rotation)
    assign_mat(obj, mat)
    apply_trs(obj)
    return obj


def cleanup_manifold(obj: bpy.types.Object) -> None:
    set_active(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=MERGE_DISTANCE)
    bpy.ops.mesh.delete_loose()
    bpy.ops.mesh.dissolve_degenerate(threshold=MERGE_DISTANCE)
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=MERGE_DISTANCE)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bad = [f for f in bm.faces if f.calc_area() < 1e-10]
    if bad:
        bmesh.ops.delete(bm, geom=bad, context="FACES")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    set_active(obj)
    bpy.ops.object.shade_smooth()
    if hasattr(mesh, "use_auto_smooth"):
        mesh.use_auto_smooth = False

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.mark_sharp(clear=True)
    bpy.ops.mesh.quads_convert_to_tris(quad_method="BEAUTY", ngon_method="BEAUTY")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    try:
        bpy.ops.mesh.uv_texture_remove()
    except Exception:
        pass
    bpy.ops.object.mode_set(mode="OBJECT")

    if hasattr(mesh, "has_custom_normals") and mesh.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()


def reassign_materials(
    obj: bpy.types.Object,
    mat_ring: bpy.types.Material,
    mat_yellow: bpy.types.Material,
    mat_metal: bpy.types.Material,
    yellow_center_z: float,
) -> None:
    mesh = obj.data
    mesh.materials.clear()
    mesh.materials.append(mat_ring)
    mesh.materials.append(mat_yellow)
    mesh.materials.append(mat_metal)

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()

    yellow_half = YELLOW_H * 0.70
    eye_cz = EYE_CENTER_Z

    for f in bm.faces:
        c = f.calc_center_median()
        d_ring = abs(math.hypot(c.x, c.z - eye_cz) - EYE_MAJOR)
        radial_xy = math.hypot(c.x, c.y)

        # Eye torus volume (hole along Y)
        if d_ring < EYE_MINOR * 1.45 and abs(c.y) < EYE_MINOR * 1.9 and c.z > eye_cz - EYE_MAJOR - EYE_MINOR:
            f.material_index = 0
            continue

        # Thin yellow collar at the exact construction Z
        if (
            abs(c.z - yellow_center_z) <= yellow_half
            and YELLOW_R * 0.45 <= radial_xy <= YELLOW_R * 1.2
        ):
            f.material_index = 1
            continue

        f.material_index = 2

    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def build_yoke(mat_metal: bpy.types.Material) -> bpy.types.Object:
    """U-bracket that holds the vertical eye (concept swivel look)."""
    base = make_cube(
        "YokeBase",
        size=(YOKE_ARM_W * 1.35, YOKE_GAP + YOKE_ARM_T * 2.2, YOKE_BASE_H),
        location=(0.0, 0.0, YOKE_BASE_Z),
        mat=mat_metal,
    )

    arm_y = (YOKE_GAP + YOKE_ARM_T) * 0.5
    arm_z = YOKE_BASE_Z + YOKE_BASE_H * 0.35 + YOKE_ARM_H * 0.35
    for sign, name in ((-1.0, "YokeArmL"), (1.0, "YokeArmR")):
        arm = make_cube(
            name,
            size=(YOKE_ARM_W, YOKE_ARM_T, YOKE_ARM_H),
            location=(0.0, sign * arm_y, arm_z),
            mat=mat_metal,
        )
        boolean_union(base, arm)

    # Pivot pin through eye (along Y)
    pin = make_cylinder(
        "YokePin",
        radius=0.0075,
        depth=YOKE_GAP + YOKE_ARM_T * 2.4,
        vertices=8,
        location=(0.0, 0.0, EYE_CENTER_Z - EYE_MAJOR * 0.95),
        mat=mat_metal,
        rotation=(math.radians(90.0), 0.0, 0.0),
    )
    boolean_union(base, pin)
    base.name = "Yoke"
    return base


def build_nut(mat_metal: bpy.types.Material) -> bpy.types.Object:
    nut = make_cylinder(
        "Nut",
        radius=NUT_R,
        depth=NUT_H,
        vertices=NUT_SIDES,
        location=(0.0, 0.0, NUT_Z),
        mat=mat_metal,
    )
    for i in range(NUT_SIDES):
        ang = (math.pi * 2.0 * i) / NUT_SIDES + math.pi / NUT_SIDES
        r = NUT_R * math.cos(math.pi / NUT_SIDES) + 0.0025
        ridge = make_cube(
            f"NutRidge_{i}",
            size=(0.007, 0.007, NUT_H * 0.72),
            location=(math.cos(ang) * r, math.sin(ang) * r, NUT_Z),
            mat=mat_metal,
            rotation=(0.0, 0.0, ang),
        )
        boolean_union(nut, ridge)
    return nut


def build_threads(mat_metal: bpy.types.Material) -> list[bpy.types.Object]:
    parts: list[bpy.types.Object] = []
    usable = SHAFT_LEN - TIP_LEN * 0.4
    for i in range(THREAD_COUNT):
        t = (i + 0.55) / (THREAD_COUNT + 0.15)
        z = SHAFT_TOP_Z - usable * t
        u = (SHAFT_TOP_Z - z) / max(SHAFT_LEN, 1e-6)
        shaft_r = SHAFT_TOP_R * (1.0 - u) + SHAFT_BOT_R * u
        major = shaft_r + THREAD_DEPTH * 0.45
        bead = make_torus(
            f"Thread_{i}",
            major=major,
            minor=THREAD_HEIGHT * 0.40,
            major_segs=THREAD_SEGS,
            minor_segs=6,
            location=(0.0, 0.0, z),
            mat=mat_metal,
            # Keep threads horizontal (around shaft axis Z)
            scale=(1.0, 1.0, 0.78),
        )
        parts.append(bead)
    return parts


def build_hook() -> bpy.types.Object:
    mat_ring = ensure_mat("MVG_WinchHook_Ring", (0.72, 0.10, 0.05), roughness=0.58, metallic=0.04)
    mat_yellow = ensure_mat("MVG_WinchHook_Yellow", (0.92, 0.78, 0.12), roughness=0.58, metallic=0.0)
    mat_metal = ensure_mat("MVG_WinchHook_Metal", (0.24, 0.25, 0.27), roughness=0.58, metallic=0.62)

    # 1) Vertical eye — rotate 90° around X so hole faces +Y (concept view)
    eye = make_torus(
        "Eye",
        major=EYE_MAJOR,
        minor=EYE_MINOR,
        major_segs=EYE_MAJOR_SEGS,
        minor_segs=EYE_MINOR_SEGS,
        location=(0.0, 0.0, EYE_CENTER_Z),
        mat=mat_ring,
        rotation=(math.radians(90.0), 0.0, 0.0),
        # Slightly flatten tube for chunky industrial look
        scale=(1.0, 1.08, 0.92),
    )

    # 2) U-yoke under / through bottom of eye
    boolean_union(eye, build_yoke(mat_metal))

    # 3) Yellow accent collar
    yellow = make_cylinder(
        "YellowBand",
        radius=YELLOW_R,
        depth=YELLOW_H,
        vertices=12,
        location=(0.0, 0.0, YELLOW_Z),
        mat=mat_yellow,
    )
    boolean_union(eye, yellow)

    # 4) Hex nut
    boolean_union(eye, build_nut(mat_metal))

    # 5) Tapered shaft
    shaft = make_cone(
        "Shaft",
        radius1=SHAFT_TOP_R,
        radius2=SHAFT_BOT_R,
        depth=SHAFT_LEN,
        vertices=12,
        location=(0.0, 0.0, SHAFT_TOP_Z - SHAFT_LEN * 0.5),
        mat=mat_metal,
    )
    boolean_union(eye, shaft)

    # 6) Chunky threads
    for bead in build_threads(mat_metal):
        boolean_union(eye, bead)

    # 7) Tip
    tip = make_cone(
        "Tip",
        radius1=SHAFT_BOT_R * 1.08,
        radius2=0.0018,
        depth=TIP_LEN,
        vertices=10,
        location=(0.0, 0.0, (SHAFT_TOP_Z - SHAFT_LEN) - TIP_LEN * 0.32),
        mat=mat_metal,
    )
    boolean_union(eye, tip)

    set_active(eye)
    bev = eye.modifiers.new(name="Bevel", type="BEVEL")
    bev.width = 0.0014
    bev.segments = 1
    bev.limit_method = "ANGLE"
    bev.angle_limit = math.radians(35.0)
    try:
        bev.harden_normals = False
    except Exception:
        pass
    bpy.ops.object.modifier_apply(modifier=bev.name)

    cleanup_manifold(eye)

    # Paint while construction Z is still valid (before origin/tip shift).
    reassign_materials(eye, mat_ring, mat_yellow, mat_metal, YELLOW_Z)

    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    zs = [v.co.z for v in eye.data.vertices]
    eye.location = (0.0, 0.0, -min(zs))
    apply_trs(eye)

    eye.name = "MiniVan_WinchHook"
    eye.data.name = "MiniVan_WinchHook"
    return eye


def render_preview(obj: bpy.types.Object) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1000
    scene.render.film_transparent = False
    if hasattr(scene.eevee, "use_bloom"):
        scene.eevee.use_bloom = False
    scene.world = bpy.data.worlds.new("PreviewWorld") if scene.world is None else scene.world
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.45, 0.47, 0.50, 1.0)
        bg.inputs[1].default_value = 0.85

    bpy.ops.object.light_add(type="SUN", location=(0.5, -0.6, 1.0))
    sun = bpy.context.active_object
    sun.data.energy = 2.2
    sun.rotation_euler = Euler((math.radians(45.0), math.radians(15.0), math.radians(-30.0)))

    bpy.ops.object.light_add(type="AREA", location=(-0.5, 0.4, 0.5))
    fill = bpy.context.active_object
    fill.data.energy = 40.0
    fill.data.size = 1.5

    target = Vector((0.0, 0.0, 0.19))

    def aim_camera(loc: tuple[float, float, float], ortho: bool = False) -> bpy.types.Object:
        bpy.ops.object.camera_add(location=loc)
        cam = bpy.context.active_object
        direction = target - cam.location
        cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = 55
        cam.data.type = "ORTHO" if ortho else "PERSP"
        if ortho:
            cam.data.ortho_scale = 0.55
        scene.camera = cam
        return cam

    # Main three-quarter (concept left view)
    aim_camera((0.36, -0.40, 0.24), ortho=False)
    scene.render.filepath = PREVIEW_PATH
    set_active(obj)
    bpy.ops.render.render(write_still=True)
    print("[WinchHook] preview=", PREVIEW_PATH)

    # Side profile
    side_path = os.path.join(OUT_DIR, "MiniVan_WinchHook_preview_side.png")
    aim_camera((0.55, 0.0, 0.19), ortho=True)
    scene.render.filepath = side_path
    bpy.ops.render.render(write_still=True)
    print("[WinchHook] preview_side=", side_path)


def export_assets(obj: bpy.types.Object) -> None:
    set_active(obj)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        apply_scale_options="FBX_SCALE_UNITS",
        object_types={"MESH"},
        mesh_smooth_type="OFF",
        use_tspace=True,
        add_leaf_bones=False,
    )

    try:
        bpy.ops.wm.obj_export(filepath=OBJ_PATH, export_selected_objects=True)
    except Exception:
        bpy.ops.export_scene.obj(filepath=OBJ_PATH, use_selection=True)

    try:
        render_preview(obj)
    except Exception as exc:
        print("[WinchHook] preview failed:", exc)


def report_topology(obj: bpy.types.Object) -> None:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    non_manifold = sum(1 for e in bm.edges if not e.is_manifold)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    wire = sum(1 for e in bm.edges if e.is_wire)
    mats = [0, 0, 0]
    for f in bm.faces:
        if f.material_index < 3:
            mats[f.material_index] += 1
    bm.free()
    print("[WinchHook] Done:", obj.name)
    print("[WinchHook] verts=", len(mesh.vertices), "faces=", len(mesh.polygons))
    print("[WinchHook] non_manifold_edges=", non_manifold)
    print("[WinchHook] boundary_edges=", boundary)
    print("[WinchHook] wire_edges=", wire)
    print("[WinchHook] mat_faces ring/yellow/metal=", mats)
    print("[WinchHook] dims=", tuple(round(x, 4) for x in obj.dimensions))
    print("[WinchHook] blend=", BLEND_PATH)
    print("[WinchHook] fbx=", FBX_PATH)


def main() -> None:
    clear_scene()
    obj = build_hook()
    export_assets(obj)
    report_topology(obj)


if __name__ == "__main__":
    main()
