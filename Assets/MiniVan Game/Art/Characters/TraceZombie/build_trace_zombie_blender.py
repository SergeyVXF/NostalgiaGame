"""
TraceZombie: Minecraft voxel crawler on all fours, matching the 4-view concept.

Body is a horizontal slab, cube head at the FRONT, four splayed legs in an X
with elbows ABOVE the torso. All object rotations are applied so FBX import
stays identity (same pipeline as AcidZombie).

Run:
  "C:\\Program Files\\Blender Foundation\\Blender 3.4\\blender.exe" --background --python build_trace_zombie_blender.py
"""
from __future__ import annotations

import os
from typing import Dict, List, Sequence, Tuple

import bmesh
import bpy
from mathutils import Vector

OUT_DIR = r"d:\UnityProjects\Zelda\NostalgiaGame\Assets\MiniVan Game\Art\Characters\TraceZombie"
PREVIEW_DIR = r"d:\UnityProjects\Zelda\tools\trace_zombie"
BLEND_PATH = os.path.join(OUT_DIR, "TraceZombie.blend")
FBX_PATH = os.path.join(OUT_DIR, "TraceZombie.fbx")
ALBEDO_PATH = os.path.join(OUT_DIR, "TraceZombie.png")

# Minecraft pixel = 1/16 m. Blender: Z up, +Y forward (AcidZombie / vampire FBX).
PX = 1.0 / 16.0
HEAD = 8 * PX
BODY_W, BODY_L, BODY_H = 8 * PX, 12 * PX, 6 * PX
LEG_T = 4 * PX
FOOT_W, FOOT_L, FOOT_H = 6 * PX, 5 * PX, 3 * PX
CLAW = 1.5 * PX
BODY_Z = 7 * PX
SCALE = 4
MC = 64
SIZE = MC * SCALE

SKIN_A = (0.80, 0.70, 0.50)
SKIN_B = (0.86, 0.76, 0.56)
SKIN_C = (0.72, 0.62, 0.44)
SKIN_D = (0.58, 0.49, 0.34)
CLOTH_A = (0.12, 0.13, 0.15)
CLOTH_B = (0.18, 0.18, 0.20)
CLOTH_C = (0.07, 0.07, 0.08)
VOID = (0.04, 0.04, 0.045)
PATCH = (0.76, 0.66, 0.47)
TOOTH = (0.82, 0.72, 0.52)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for item in list(coll):
            coll.remove(item)


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def hash01(x: int, y: int, salt: int = 0) -> float:
    n = (x * 374761393 + y * 668265263 + salt * 1274126177) & 0xFFFFFFFF
    n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
    return (n & 0xFFFFFF) / 16777215.0


def mix(a: Sequence[float], b: Sequence[float], t: float) -> Tuple[float, float, float]:
    t = max(0.0, min(1.0, t))
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def set_px(albedo: List[float], x_tl: int, y_tl: int, rgb: Sequence[float]) -> None:
    if x_tl < 0 or y_tl < 0 or x_tl >= SIZE or y_tl >= SIZE:
        return
    y = SIZE - 1 - y_tl
    i = (y * SIZE + x_tl) * 4
    albedo[i] = rgb[0]
    albedo[i + 1] = rgb[1]
    albedo[i + 2] = rgb[2]
    albedo[i + 3] = 1.0


def skin_at(x: int, y: int) -> Tuple[float, float, float]:
    t = hash01(x, y, 11)
    if t < 0.18:
        c = SKIN_D
    elif t < 0.5:
        c = SKIN_A
    elif t < 0.82:
        c = SKIN_B
    else:
        c = SKIN_C
    jitter = (hash01(x, y, 3) - 0.5) * 0.05
    return (
        max(0.0, min(1.0, c[0] + jitter)),
        max(0.0, min(1.0, c[1] + jitter * 0.8)),
        max(0.0, min(1.0, c[2] + jitter * 0.4)),
    )


def cloth_at(x: int, y: int) -> Tuple[float, float, float]:
    t = hash01(x, y, 29)
    if t < 0.22:
        c = CLOTH_C
    elif t < 0.7:
        c = CLOTH_A
    else:
        c = CLOTH_B
    jitter = (hash01(x, y, 9) - 0.5) * 0.03
    return (
        max(0.0, min(1.0, c[0] + jitter)),
        max(0.0, min(1.0, c[1] + jitter)),
        max(0.0, min(1.0, c[2] + jitter)),
    )


def fill_rect(albedo, mx, my, mw, mh, painter) -> None:
    x0, y0 = mx * SCALE, my * SCALE
    w, h = mw * SCALE, mh * SCALE
    for j in range(h):
        for i in range(w):
            painter(albedo, x0 + i, y0 + j, i, j, w, h)


def paint_skin(albedo, x, y, i, j, w, h) -> None:
    set_px(albedo, x, y, skin_at(x, y))


def paint_cloth(albedo, x, y, i, j, w, h) -> None:
    set_px(albedo, x, y, cloth_at(x, y))


def paint_head_front(albedo, x, y, i, j, w, h) -> None:
    ni = i / max(w - 1, 1)
    nj = j / max(h - 1, 1)
    in_eye_l = 0.16 <= ni <= 0.40 and 0.28 <= nj <= 0.52
    in_eye_r = 0.60 <= ni <= 0.84 and 0.28 <= nj <= 0.52
    in_mouth = 0.28 <= ni <= 0.72 and 0.62 <= nj <= 0.78
    in_tooth_l = 0.34 <= ni <= 0.44 and 0.70 <= nj <= 0.78
    in_tooth_r = 0.56 <= ni <= 0.66 and 0.70 <= nj <= 0.78
    if in_tooth_l or in_tooth_r:
        set_px(albedo, x, y, TOOTH)
        return
    if in_eye_l or in_eye_r or in_mouth:
        set_px(albedo, x, y, VOID)
        return
    set_px(albedo, x, y, skin_at(x, y))


def paint_body_top(albedo, x, y, i, j, w, h) -> None:
    ni = i / max(w - 1, 1)
    nj = j / max(h - 1, 1)
    jagged = 0.22 + 0.06 * hash01(int(ni * 12), int(nj * 12), 7)
    if jagged <= ni <= 1.0 - jagged and 0.24 <= nj <= 0.78:
        set_px(albedo, x, y, mix(PATCH, SKIN_A, 0.2 + 0.2 * hash01(x, y, 4)))
        return
    paint_cloth(albedo, x, y, i, j, w, h)


def paint_foot_front(albedo, x, y, i, j, w, h) -> None:
    ni = i / max(w - 1, 1)
    if 0.18 <= ni <= 0.32 or 0.43 <= ni <= 0.57 or 0.68 <= ni <= 0.82:
        set_px(albedo, x, y, mix(SKIN_D, VOID, 0.35))
        return
    paint_skin(albedo, x, y, i, j, w, h)


def make_textures() -> bpy.types.Image:
    n = SIZE * SIZE * 4
    albedo = [0.0] * n
    for i in range(0, n, 4):
        albedo[i:i + 3] = list(VOID)
        albedo[i + 3] = 1.0

    fill_rect(albedo, 8, 0, 8, 8, paint_skin)
    fill_rect(albedo, 16, 0, 8, 8, paint_skin)
    fill_rect(albedo, 0, 8, 8, 8, paint_skin)
    fill_rect(albedo, 8, 8, 8, 8, paint_head_front)
    fill_rect(albedo, 16, 8, 8, 8, paint_skin)
    fill_rect(albedo, 24, 8, 8, 8, paint_skin)

    fill_rect(albedo, 20, 16, 12, 6, paint_body_top)
    fill_rect(albedo, 32, 16, 12, 6, paint_cloth)
    fill_rect(albedo, 16, 22, 4, 8, paint_cloth)
    fill_rect(albedo, 20, 22, 12, 8, paint_cloth)
    fill_rect(albedo, 32, 22, 4, 8, paint_cloth)
    fill_rect(albedo, 36, 22, 12, 8, paint_body_top)

    fill_rect(albedo, 0, 40, 8, 16, paint_skin)
    fill_rect(albedo, 8, 40, 8, 16, paint_cloth)
    fill_rect(albedo, 16, 40, 8, 8, paint_skin)
    fill_rect(albedo, 24, 40, 8, 8, paint_foot_front)

    img = bpy.data.images.new("TraceZombie", width=SIZE, height=SIZE, alpha=True)
    img.pixels = albedo
    img.filepath_raw = ALBEDO_PATH
    img.file_format = "PNG"
    img.save()
    img.pack()
    return img


def mc_uv(mx: float, my: float, mw: float, mh: float) -> Tuple[float, float, float, float]:
    x0 = mx * SCALE
    y0_tl = my * SCALE
    w = mw * SCALE
    h = mh * SCALE
    u0 = x0 / SIZE
    u1 = (x0 + w) / SIZE
    v1 = (SIZE - y0_tl) / SIZE
    v0 = (SIZE - y0_tl - h) / SIZE
    return (u0, v0, u1, v1)


def map_box_uvs(obj: bpy.types.Object, regions: Dict[str, Tuple[float, float, float, float]]) -> None:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    uv_layer = bm.loops.layers.uv.verify()
    for face in bm.faces:
        n = face.normal
        if n.y > 0.5:
            key = "front"
        elif n.y < -0.5:
            key = "back"
        elif n.x < -0.5:
            key = "left"
        elif n.x > 0.5:
            key = "right"
        elif n.z > 0.5:
            key = "top"
        else:
            key = "bottom"
        rect = regions.get(key) or regions["front"]
        u0, v0, u1, v1 = rect
        if key in ("front", "back"):
            a = [loop.vert.co.x for loop in face.loops]
            b = [loop.vert.co.z for loop in face.loops]
        elif key in ("left", "right"):
            a = [loop.vert.co.y for loop in face.loops]
            b = [loop.vert.co.z for loop in face.loops]
        else:
            a = [loop.vert.co.x for loop in face.loops]
            b = [loop.vert.co.y for loop in face.loops]
        min_a, max_a = min(a), max(a)
        min_b, max_b = min(b), max(b)
        da = max(max_a - min_a, 1e-6)
        db = max(max_b - min_b, 1e-6)
        for loop in face.loops:
            if key in ("front", "back"):
                sa, sb = loop.vert.co.x, loop.vert.co.z
            elif key in ("left", "right"):
                sa, sb = loop.vert.co.y, loop.vert.co.z
            else:
                sa, sb = loop.vert.co.x, loop.vert.co.y
            nu = (sa - min_a) / da
            nv = (sb - min_b) / db
            if key in ("back", "right"):
                nu = 1.0 - nu
            loop[uv_layer].uv = Vector((u0 + nu * (u1 - u0), v0 + nv * (v1 - v0)))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def make_material(albedo: bpy.types.Image) -> bpy.types.Material:
    mat = bpy.data.materials.new("M_TraceZombie")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (420, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (140, 0)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.location = (-280, 40)
    tex.image = albedo
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.94
    if "Specular" in bsdf.inputs:
        bsdf.inputs["Specular"].default_value = 0.03
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


HEAD_UV = {
    "top": mc_uv(8, 0, 8, 8),
    "bottom": mc_uv(16, 0, 8, 8),
    "right": mc_uv(0, 8, 8, 8),
    "front": mc_uv(8, 8, 8, 8),
    "left": mc_uv(16, 8, 8, 8),
    "back": mc_uv(24, 8, 8, 8),
}
BODY_UV = {
    "top": mc_uv(20, 16, 12, 6),
    "bottom": mc_uv(32, 16, 12, 6),
    "right": mc_uv(16, 22, 4, 8),
    "front": mc_uv(20, 22, 12, 8),
    "left": mc_uv(32, 22, 4, 8),
    "back": mc_uv(36, 22, 12, 8),
}
SKIN_UV = {
    "top": mc_uv(0, 40, 8, 4),
    "bottom": mc_uv(0, 44, 8, 4),
    "right": mc_uv(0, 48, 4, 8),
    "front": mc_uv(4, 48, 4, 8),
    "left": mc_uv(0, 40, 4, 8),
    "back": mc_uv(4, 40, 4, 8),
}
FOOT_UV = {
    "top": mc_uv(16, 40, 8, 4),
    "bottom": mc_uv(16, 44, 8, 4),
    "right": mc_uv(16, 40, 4, 8),
    "front": mc_uv(24, 40, 8, 8),
    "left": mc_uv(20, 40, 4, 8),
    "back": mc_uv(28, 40, 4, 8),
}


def add_cube(name: str, size: Tuple[float, float, float]) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for poly in obj.data.polygons:
        poly.use_smooth = False
    set_active(obj)
    bpy.ops.object.shade_flat()
    return obj


def finish_mesh(obj: bpy.types.Object, parent: bpy.types.Object, uvs, mat) -> bpy.types.Object:
    map_box_uvs(obj, uvs)
    obj.data.materials.append(mat)
    obj.parent = parent
    obj.rotation_euler = (0.0, 0.0, 0.0)
    return obj


def add_axis_box(
    name: str,
    center: Vector,
    size: Tuple[float, float, float],
    parent: bpy.types.Object,
    uvs,
    mat,
) -> bpy.types.Object:
    obj = add_cube(name, size)
    finish_mesh(obj, parent, uvs, mat)
    obj.location = center
    return obj


def add_segment(
    name: str,
    start: Vector,
    end: Vector,
    thickness: float,
    parent: bpy.types.Object,
    uvs,
    mat,
) -> bpy.types.Object:
    delta = end - start
    length = max(delta.length, 0.04)
    mid = (start + end) * 0.5
    obj = add_cube(name, (thickness, thickness, length))
    map_box_uvs(obj, uvs)
    obj.location = mid
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    set_active(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    obj.data.materials.append(mat)
    obj.parent = parent
    obj.location = mid
    obj.rotation_euler = (0.0, 0.0, 0.0)
    return obj


def build_meshes(mat: bpy.types.Material) -> bpy.types.Object:
    root = bpy.data.objects.new("TraceZombie", None)
    root.empty_display_size = 0.1
    bpy.context.collection.objects.link(root)

    body_center = Vector((0.0, 0.0, BODY_Z))
    head_center = Vector((0.0, BODY_L * 0.5 + HEAD * 0.42, BODY_Z + 0.04))
    add_axis_box("Body_Mesh", body_center, (BODY_W, BODY_L, BODY_H), root, BODY_UV, mat)
    add_axis_box("Head_Mesh", head_center, (HEAD, HEAD, HEAD), root, HEAD_UV, mat)

    # Spider-crawler X stance: hips at torso corners, elbows UP, feet on the ground.
    legs = {
        "LegFL": (-1.0, 1.0),
        "LegFR": (1.0, 1.0),
        "LegBL": (-1.0, -1.0),
        "LegBR": (1.0, -1.0),
    }
    for name, (sx, sy) in legs.items():
        hip = Vector((sx * BODY_W * 0.52, sy * BODY_L * 0.38, BODY_Z))
        elbow = Vector((sx * 0.54, sy * 0.50, BODY_Z + 0.30))
        ankle = Vector((sx * 0.60, sy * 0.58, FOOT_H + 0.02))
        foot = Vector((sx * 0.62, sy * 0.64, FOOT_H * 0.5))
        add_segment(name + "_Upper", hip, elbow, LEG_T, root, SKIN_UV, mat)
        add_segment(name + "_Mid", elbow, ankle, LEG_T * 0.92, root, SKIN_UV, mat)
        add_axis_box(name + "_Foot", foot, (FOOT_W, FOOT_L, FOOT_H), root, FOOT_UV, mat)
        claw_forward = Vector((0.0, sy * (FOOT_L * 0.42), 0.0))
        for k, ox in enumerate((-FOOT_W * 0.28, 0.0, FOOT_W * 0.28)):
            claw_c = foot + claw_forward + Vector((ox, 0.0, -FOOT_H * 0.15))
            add_axis_box(name + "_Claw" + str(k), claw_c, (CLAW, CLAW * 1.4, CLAW * 1.6), root, FOOT_UV, mat)

    return root


def setup_world() -> None:
    scene = bpy.context.scene
    world = bpy.data.worlds.new("TraceZombieWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.16, 0.16, 0.17, 1.0)
        bg.inputs[1].default_value = 1.0
    scene.view_settings.view_transform = "Standard"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.film_transparent = False


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_views() -> None:
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    target = Vector((0.0, 0.08, 0.36))

    views = {
        "front": Vector((0.0, 3.8, 0.42)),
        "back": Vector((0.0, -3.8, 0.42)),
        "side": Vector((3.8, 0.0, 0.42)),
        "three_quarter": Vector((2.5, 3.0, 1.45)),
        "top": Vector((0.0, 0.01, 4.6)),
    }

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    cam_data.lens = 50
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.6

    light_data = bpy.data.lights.new(name="Key", type="AREA")
    light_data.energy = 240
    light_data.size = 3.0
    light = bpy.data.objects.new(name="Key", object_data=light_data)
    bpy.context.collection.objects.link(light)
    light.location = (1.8, 2.4, 3.4)

    fill_data = bpy.data.lights.new(name="Fill", type="AREA")
    fill_data.energy = 55
    fill_data.size = 4.0
    fill = bpy.data.objects.new(name="Fill", object_data=fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (-2.2, -1.2, 2.5)

    for name, loc in views.items():
        cam.location = loc
        look_at(cam, target)
        if name == "top":
            cam_data.type = "ORTHO"
            cam_data.ortho_scale = 2.4
            cam.location = Vector((0.0, 0.0, 4.8))
            look_at(cam, target)
        elif name == "three_quarter":
            cam_data.type = "PERSP"
            cam_data.lens = 50
        else:
            cam_data.type = "ORTHO"
            cam_data.ortho_scale = 2.8
        scene.render.filepath = os.path.join(PREVIEW_DIR, f"preview_{name}.png")
        bpy.ops.render.render(write_still=True)

    cam.location = views["three_quarter"]
    look_at(cam, target)
    cam_data.type = "PERSP"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.filepath = os.path.join(OUT_DIR, "TraceZombie_preview.png")
    bpy.ops.render.render(write_still=True)


def export_fbx() -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "EMPTY"}:
            obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,
        object_types={"MESH", "EMPTY"},
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=True,
    )


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    albedo = make_textures()
    mat = make_material(albedo)
    build_meshes(mat)
    setup_world()
    render_views()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export_fbx()
    print("[TraceZombie] blend=", BLEND_PATH)
    print("[TraceZombie] fbx=", FBX_PATH)
    print("[TraceZombie] albedo=", ALBEDO_PATH)


if __name__ == "__main__":
    main()
