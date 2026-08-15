"""
MiniVan defibrillator: suitcase + tube (paddle).
Run in Blender 3.x/4.x:
  blender --background --python build_defibrillator_blender.py

Produces two objects (each a single joined mesh) + materials, saved as .blend and .fbx.
"""
from __future__ import annotations

import math
import os
from typing import Iterable, List, Sequence, Tuple

import bpy
from mathutils import Euler, Matrix, Vector

# ---------------------------------------------------------------------------
OUT_DIR = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(OUT_DIR, "MiniVan_Defibrillator.blend")
FBX_SUITCASE = os.path.join(OUT_DIR, "MiniVan_Defib_Suitcase.fbx")
FBX_TUBE = os.path.join(OUT_DIR, "MiniVan_Defib_Tube.fbx")

Vec3 = Tuple[float, float, float]


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.collections):
        for item in list(block):
            block.remove(item)


def ensure_mat(name: str, color: Sequence[float], roughness: float = 0.45,
               metallic: float = 0.0, emission: Sequence[float] | None = None,
               emission_strength: float = 0.0) -> bpy.types.Material:
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    if bsdf is None:
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = metallic
    if emission is not None:
        key = "Emission Color" if "Emission Color" in bsdf.inputs else "Emission"
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = (emission[0], emission[1], emission[2], 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def link_active(obj: bpy.types.Object) -> None:
    if obj.name not in bpy.context.scene.collection.objects:
        bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def deselect_all() -> None:
    for o in bpy.context.selected_objects:
        o.select_set(False)


def add_cube(size: Vec3, loc: Vec3, name: str, mat: bpy.types.Material,
             rot: Vec3 = (0.0, 0.0, 0.0), bevel: float = 0.0) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    obj.rotation_euler = Euler(rot)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0.0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)
    return obj


def add_cylinder(radius: float, depth: float, loc: Vec3, name: str,
                 mat: bpy.types.Material, rot: Vec3 = (0.0, 0.0, 0.0),
                 vertices: int = 16) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        radius=radius, depth=depth, location=loc, vertices=vertices)
    obj = bpy.context.active_object
    obj.name = name
    obj.rotation_euler = Euler(rot)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)
    return obj


def add_uv_sphere(radius: float, loc: Vec3, name: str, mat: bpy.types.Material,
                  segments: int = 12, rings: int = 8) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=radius, location=loc, segments=segments, ring_count=rings)
    obj = bpy.context.active_object
    obj.name = name
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)
    return obj


def join_into(name: str, objects: Iterable[bpy.types.Object]) -> bpy.types.Object:
    objs = [o for o in objects if o is not None]
    if not objs:
        raise RuntimeError("nothing to join")
    deselect_all()
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = name
    # Origin to geometry center for easier placement in Unity
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    return joined


def build_suitcase() -> bpy.types.Object:
    red = ensure_mat("Defib_Red", (0.85, 0.12, 0.08), roughness=0.4)
    beige = ensure_mat("Defib_Beige", (0.86, 0.78, 0.62), roughness=0.5)
    black = ensure_mat("Defib_Black", (0.05, 0.05, 0.055), roughness=0.55)
    dark = ensure_mat("Defib_Dark", (0.12, 0.12, 0.13), roughness=0.5)
    cream = ensure_mat("Defib_Cream", (0.92, 0.88, 0.78), roughness=0.48)
    screen = ensure_mat(
        "Defib_Screen", (0.12, 0.55, 0.22), roughness=0.25,
        emission=(0.15, 0.9, 0.35), emission_strength=1.4)
    green = ensure_mat(
        "Defib_LED_G", (0.1, 0.9, 0.2), roughness=0.3,
        emission=(0.2, 1.0, 0.3), emission_strength=2.0)
    yellow = ensure_mat(
        "Defib_LED_Y", (0.95, 0.8, 0.1), roughness=0.3,
        emission=(1.0, 0.85, 0.2), emission_strength=1.5)
    led_r = ensure_mat(
        "Defib_LED_R", (0.95, 0.1, 0.08), roughness=0.3,
        emission=(1.0, 0.15, 0.1), emission_strength=1.5)
    blue = ensure_mat("Defib_Blue", (0.15, 0.35, 0.85), roughness=0.4)
    yel = ensure_mat("Defib_Yellow", (0.95, 0.82, 0.12), roughness=0.4)
    white = ensure_mat("Defib_White", (0.95, 0.95, 0.95), roughness=0.4)

    parts: List[bpy.types.Object] = []

    # Base shell (open case bottom) — units in meters, ~0.48 wide
    base = add_cube((0.48, 0.34, 0.14), (0.0, 0.0, 0.07), "base", red, bevel=0.012)
    parts.append(base)
    # Single interior bed (no second coplanar floor — that caused z-fighting).
    parts.append(add_cube((0.44, 0.30, 0.02), (0.0, 0.0, 0.12), "tray", beige, bevel=0.006))

    # Corner bumpers (8)
    bumper_positions = [
        (-0.22, -0.15, 0.03), (0.22, -0.15, 0.03),
        (-0.22, 0.15, 0.03), (0.22, 0.15, 0.03),
        (-0.22, -0.15, 0.12), (0.22, -0.15, 0.12),
        (-0.22, 0.15, 0.12), (0.22, 0.15, 0.12),
    ]
    for i, p in enumerate(bumper_positions):
        parts.append(add_cube((0.06, 0.05, 0.045), p, f"bumper_{i}", dark, bevel=0.008))

    # Front handle + latches
    parts.append(add_cube((0.16, 0.035, 0.04), (0.0, -0.19, 0.06), "handle", black, bevel=0.01))
    parts.append(add_cube((0.04, 0.03, 0.035), (-0.12, -0.185, 0.06), "latch_l", black, bevel=0.004))
    parts.append(add_cube((0.04, 0.03, 0.035), (0.12, -0.185, 0.06), "latch_r", black, bevel=0.004))

    # Medical cross badge
    parts.append(add_cube((0.07, 0.01, 0.07), (0.0, -0.175, 0.145), "badge_plate", beige))
    parts.append(add_cube((0.055, 0.012, 0.018), (0.0, -0.178, 0.145), "cross_h", red))
    parts.append(add_cube((0.018, 0.012, 0.055), (0.0, -0.178, 0.145), "cross_v", red))

    # Console panel sits clearly ABOVE the tray (gap avoids z-fight).
    parts.append(add_cube((0.40, 0.22, 0.03), (0.0, 0.02, 0.155), "panel", cream, bevel=0.004))
    # Screen
    parts.append(add_cube((0.14, 0.10, 0.012), (-0.11, 0.04, 0.176), "screen", screen, bevel=0.002))
    # Battery bars under screen
    for i in range(5):
        parts.append(add_cube(
            (0.018, 0.012, 0.008),
            (-0.15 + i * 0.022, -0.02, 0.174),
            f"bat_{i}", green))

    # Big shock button + octagon guard (approx with cube ring)
    parts.append(add_cube((0.09, 0.09, 0.02), (0.08, 0.0, 0.172), "btn_guard", black, bevel=0.01))
    parts.append(add_cylinder(0.032, 0.022, (0.08, 0.0, 0.186), "btn", red, vertices=12))
    # Lightning bolt on button (simple bars)
    parts.append(add_cube((0.018, 0.006, 0.01), (0.08, 0.0, 0.199), "bolt_h", white))
    parts.append(add_cube((0.006, 0.006, 0.028), (0.075, 0.0, 0.199), "bolt_v", white))

    # Knobs
    parts.append(add_cylinder(0.018, 0.02, (0.02, 0.08, 0.179), "knob_a", black, vertices=12))
    parts.append(add_cylinder(0.018, 0.02, (0.08, 0.08, 0.179), "knob_b", black, vertices=12))

    # Status LEDs
    parts.append(add_uv_sphere(0.008, (0.15, 0.07, 0.179), "led_g", green, 8, 6))
    parts.append(add_uv_sphere(0.008, (0.15, 0.04, 0.179), "led_y", yellow, 8, 6))
    parts.append(add_uv_sphere(0.008, (0.15, 0.01, 0.179), "led_r", led_r, 8, 6))

    # Speaker grille slits
    for i in range(5):
        parts.append(add_cube(
            (0.035, 0.004, 0.006),
            (0.16, -0.04 + i * 0.012, 0.174),
            f"grille_{i}", dark))

    # Hinge strip on the back top edge of the base.
    parts.append(add_cube((0.46, 0.03, 0.03), (0.0, 0.16, 0.145), "hinge", dark, bevel=0.004))

    # Lid built closed, then swung open BACKWARD around the hinge so the bottom
    # edge stays touching the case (same 62° lean, opposite side from before).
    lid_parts: List[bpy.types.Object] = []
    lid_parts.append(add_cube((0.48, 0.34, 0.06), (0.0, 0.0, 0.17), "lid", red, bevel=0.012))
    lid_parts.append(add_cube((0.44, 0.30, 0.02), (0.0, 0.0, 0.145), "lid_inner", cream, bevel=0.004))

    def make_docked_paddle(x: float, color_mat: bpy.types.Material, tag: str) -> None:
        lid_parts.append(add_cube((0.12, 0.09, 0.025), (x, 0.02, 0.155), f"{tag}_plate", cream, bevel=0.006))
        lid_parts.append(add_cube((0.11, 0.08, 0.01), (x, 0.02, 0.165), f"{tag}_face", beige))
        lid_parts.append(add_cube((0.125, 0.095, 0.012), (x, 0.02, 0.148), f"{tag}_rim", color_mat, bevel=0.004))
        lid_parts.append(add_cube((0.10, 0.04, 0.035), (x, -0.08, 0.155), f"{tag}_grip", color_mat, bevel=0.006))

    make_docked_paddle(-0.12, blue, "pad_b")
    make_docked_paddle(0.12, yel, "pad_y")

    hinge_loc = Vector((0.0, 0.17, 0.14))
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=hinge_loc)
    hinge = bpy.context.active_object
    hinge.name = "LidHingeTemp"
    for obj in lid_parts:
        obj.parent = hinge
        obj.matrix_parent_inverse = hinge.matrix_world.inverted()
    # Closed = 0°. Open over the console was -62°. Other way, same lean: -(180-62).
    hinge.rotation_euler = Euler((-math.radians(118.0), 0.0, 0.0))
    bpy.context.view_layer.update()
    for obj in lid_parts:
        world = obj.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = world
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        obj.select_set(False)
    bpy.data.objects.remove(hinge, do_unlink=True)
    parts.extend(lid_parts)

    return join_into("Defib_Suitcase", parts)


def build_tube() -> bpy.types.Object:
    """Handheld defibrillator paddle / tube from second reference."""
    cream = ensure_mat("Tube_Cream", (0.90, 0.86, 0.76), roughness=0.48)
    red = ensure_mat("Tube_Red", (0.82, 0.08, 0.06), roughness=0.38)
    dark = ensure_mat("Tube_Dark", (0.10, 0.10, 0.11), roughness=0.55)
    silver = ensure_mat("Tube_Silver", (0.72, 0.74, 0.76), roughness=0.28, metallic=0.75)
    led_g = ensure_mat(
        "Tube_LED_G", (0.15, 0.95, 0.25), roughness=0.3,
        emission=(0.2, 1.0, 0.3), emission_strength=2.0)
    led_r = ensure_mat(
        "Tube_LED_R", (0.95, 0.12, 0.08), roughness=0.3,
        emission=(1.0, 0.15, 0.1), emission_strength=1.8)
    parts: List[bpy.types.Object] = []

    # Head housing
    parts.append(add_cube((0.14, 0.05, 0.11), (0.0, 0.0, 0.22), "head", cream, bevel=0.01))
    # Red trim around face
    parts.append(add_cube((0.145, 0.02, 0.115), (0.0, -0.028, 0.22), "trim", red, bevel=0.006))
    # Silver electrode plate
    parts.append(add_cube((0.11, 0.012, 0.085), (0.0, -0.038, 0.22), "electrode", silver, bevel=0.004))

    # Handle body (angled)
    handle = add_cube((0.055, 0.055, 0.16), (0.0, 0.04, 0.08), "handle_body", cream, bevel=0.008)
    handle.rotation_euler = Euler((math.radians(18), 0.0, 0.0))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    parts.append(handle)

    # Dark grip with ridges
    grip = add_cube((0.058, 0.058, 0.09), (0.0, 0.055, 0.05), "grip", dark, bevel=0.006)
    grip.rotation_euler = Euler((math.radians(18), 0.0, 0.0))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    parts.append(grip)
    for i in range(6):
        ridge = add_cube(
            (0.06, 0.006, 0.01),
            (0.0, 0.055 + i * 0.002, 0.02 + i * 0.012),
            f"ridge_{i}", dark)
        ridge.rotation_euler = Euler((math.radians(18), 0.0, 0.0))
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        parts.append(ridge)

    # Trigger button
    trig = add_cube((0.035, 0.02, 0.05), (0.0, 0.09, 0.07), "trigger", red, bevel=0.004)
    trig.rotation_euler = Euler((math.radians(18), 0.0, 0.0))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    parts.append(trig)

    # LEDs + lightning decal (simple bars)
    parts.append(add_uv_sphere(0.006, (0.02, 0.07, 0.14), "led_g", led_g, 8, 6))
    parts.append(add_uv_sphere(0.006, (-0.02, 0.07, 0.14), "led_r", led_r, 8, 6))
    parts.append(add_cube((0.012, 0.004, 0.02), (0.0, 0.065, 0.155), "bolt_a", red))
    parts.append(add_cube((0.004, 0.004, 0.028), (-0.004, 0.065, 0.155), "bolt_b", red))

    # No cable / plug — handle ends cleanly at the grip.

    return join_into("Defib_Tube", parts)


def export_fbx(obj: bpy.types.Object, path: str) -> None:
    deselect_all()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"MESH"},
        mesh_smooth_type="FACE",
        embed_textures=False,
        axis_forward="-Z",
        axis_up="Y",
    )


def main() -> None:
    clear_scene()
    suitcase = build_suitcase()
    # Move suitcase aside so both fit in scene
    suitcase.location = (-0.6, 0.0, 0.0)

    tube = build_tube()
    tube.location = (0.6, 0.0, 0.0)

    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export_fbx(suitcase, FBX_SUITCASE)
    export_fbx(tube, FBX_TUBE)
    print(f"Saved blend: {BLEND_PATH}")
    print(f"Saved FBX:   {FBX_SUITCASE}")
    print(f"Saved FBX:   {FBX_TUBE}")
    print(f"Suitcase objects after join: 1 ({suitcase.name})")
    print(f"Tube objects after join: 1 ({tube.name})")


if __name__ == "__main__":
    main()
