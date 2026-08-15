"""
MiniVan amber button (low-poly, single manifold mesh) from collectible concept.

Critical for Unity inverted-hull outlines:
  - vertices must stay welded (no FACE-split export)
  - normals recalculated outward
  - no transparent surface that reveals hole interiors

Run:
  "C:\\Program Files\\Blender Foundation\\Blender 3.4\\blender.exe" --background --python build_amber_button_blender.py
"""
from __future__ import annotations

import math
import os

import bmesh
import bpy
from mathutils import Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(OUT_DIR, "MiniVan_AmberButton.blend")
FBX_PATH = os.path.join(OUT_DIR, "MiniVan_AmberButton.fbx")
OBJ_PATH = os.path.join(OUT_DIR, "MiniVan_AmberButton.obj")

RADIUS = 0.085
THICKNESS = 0.028
SIDES = 18
RECESS_INSET = 0.018
RECESS_DEPTH = 0.010
HOLE_RADIUS = 0.0075
HOLE_SPACING = 0.018
HOLE_SEGMENTS = 8
BEVEL_WIDTH = 0.0030
BEVEL_SEGMENTS = 1
MERGE_DISTANCE = 0.00008


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.collections):
        for item in list(block):
            block.remove(item)


def ensure_amber_material() -> bpy.types.Material:
    name = "MVG_AmberButton"
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

    # Opaque honey amber — transmission made hole interiors read as "see-through UV bugs".
    amber = (0.92, 0.52, 0.12, 1.0)
    bsdf.inputs["Base Color"].default_value = amber
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.28
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.0
    if "Specular" in bsdf.inputs:
        bsdf.inputs["Specular"].default_value = 0.45
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.45
    if "Transmission" in bsdf.inputs:
        bsdf.inputs["Transmission"].default_value = 0.0
    if "Transmission Weight" in bsdf.inputs:
        bsdf.inputs["Transmission Weight"].default_value = 0.0
    if "Alpha" in bsdf.inputs:
        bsdf.inputs["Alpha"].default_value = 1.0
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (1.0, 0.55, 0.12, 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = 0.12
    elif "Emission" in bsdf.inputs:
        bsdf.inputs["Emission"].default_value = (0.35, 0.18, 0.04, 1.0)

    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if hasattr(mat, "blend_method"):
        mat.blend_method = "OPAQUE"
    if hasattr(mat, "shadow_method"):
        mat.shadow_method = "OPAQUE"
    if hasattr(mat, "use_backface_culling"):
        mat.use_backface_culling = True
    return mat


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def cleanup_manifold(obj: bpy.types.Object) -> None:
    """Weld verts, drop loose geo, force consistent outward normals (outline-safe)."""
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

    # Drop zero-area / non-manifold junk faces if any remain after boolean.
    bad = [f for f in bm.faces if f.calc_area() < 1e-10]
    if bad:
        bmesh.ops.delete(bm, geom=bad, context="FACES")

    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    # Keep ONE vertex per corner. Flat look comes from sharp edges + auto-smooth,
    # not from FACE-split export (that breaks inverted-hull outlines).
    # Fully smooth + no sharp splits. Faceting comes from low poly counts;
    # hard edges would break Unity inverted-hull outlines into flying panels.
    set_active(obj)
    bpy.ops.object.shade_smooth()
    if hasattr(mesh, "use_auto_smooth"):
        mesh.use_auto_smooth = False

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.mark_sharp(clear=True)
    bpy.ops.mesh.quads_convert_to_tris(quad_method="BEAUTY", ngon_method="BEAUTY")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    # Drop UVs — Unity weld rebuilds a simple set; seams were splitting verts.
    try:
        bpy.ops.mesh.uv_texture_remove()
    except Exception:
        pass
    bpy.ops.object.mode_set(mode="OBJECT")

    if hasattr(mesh, "has_custom_normals") and mesh.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()


def build_button_with_ops() -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=SIDES,
        radius=RADIUS,
        depth=THICKNESS,
        end_fill_type="NGON",
        location=(0.0, 0.0, 0.0),
    )
    obj = bpy.context.active_object
    obj.name = "MiniVan_AmberButton"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    top = max(bm.faces, key=lambda f: f.calc_center_median().z)
    for f in bm.faces:
        f.select = f == top
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_mode(type="FACE")
    bpy.ops.mesh.inset(thickness=RECESS_INSET, depth=0.0)
    bpy.ops.mesh.extrude_region_move(
        TRANSFORM_OT_translate={"value": (0.0, 0.0, -RECESS_DEPTH)}
    )
    bpy.ops.object.mode_set(mode="OBJECT")

    cutters = []
    # Cutters must fully pierce the body so hole walls are solid tunnels.
    z = 0.0
    for ox, oy in (
        (-HOLE_SPACING * 0.5, -HOLE_SPACING * 0.5),
        (HOLE_SPACING * 0.5, -HOLE_SPACING * 0.5),
        (-HOLE_SPACING * 0.5, HOLE_SPACING * 0.5),
        (HOLE_SPACING * 0.5, HOLE_SPACING * 0.5),
    ):
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=HOLE_SEGMENTS,
            radius=HOLE_RADIUS,
            depth=THICKNESS * 3.0,
            location=(ox, oy, z),
        )
        cutter = bpy.context.active_object
        cutter.name = "HoleCutter"
        cutters.append(cutter)

    set_active(obj)
    for cutter in cutters:
        mod = obj.modifiers.new(name="Bool_" + cutter.name, type="BOOLEAN")
        mod.operation = "DIFFERENCE"
        try:
            mod.solver = "EXACT"
        except Exception:
            pass
        mod.object = cutter
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cutter, do_unlink=True)

    set_active(obj)
    bev = obj.modifiers.new(name="Bevel", type="BEVEL")
    bev.width = BEVEL_WIDTH
    bev.segments = BEVEL_SEGMENTS
    bev.limit_method = "ANGLE"
    bev.angle_limit = math.radians(30.0)
    try:
        bev.harden_normals = False
    except Exception:
        pass
    bpy.ops.object.modifier_apply(modifier=bev.name)

    cleanup_manifold(obj)

    mat = ensure_amber_material()
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)

    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    obj.location = (0.0, 0.0, 0.0)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    obj.name = "MiniVan_AmberButton"
    obj.data.name = "MiniVan_AmberButton"
    return obj


def export_assets(obj: bpy.types.Object) -> None:
    set_active(obj)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

    # OFF = keep welded verts. FACE would explode inverted-hull outlines into panels.
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


def main() -> None:
    clear_scene()
    obj = build_button_with_ops()
    export_assets(obj)

    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    non_manifold = sum(1 for e in bm.edges if not e.is_manifold)
    bm.free()

    print("[AmberButton] Done:", obj.name)
    print("[AmberButton] verts=", len(mesh.vertices), "faces=", len(mesh.polygons))
    print("[AmberButton] non_manifold_edges=", non_manifold)
    print("[AmberButton] blend=", BLEND_PATH)
    print("[AmberButton] fbx=", FBX_PATH)
    print("[AmberButton] obj=", OBJ_PATH)


if __name__ == "__main__":
    main()
