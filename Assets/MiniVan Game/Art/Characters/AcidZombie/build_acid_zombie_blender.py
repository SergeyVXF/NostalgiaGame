"""
Acid-spitting zombie: Minecraft 6-box body + pixel atlas (not colored geometry).

Run:
  "C:\\Program Files\\Blender Foundation\\Blender 3.4\\blender.exe" --background --python build_acid_zombie_blender.py
"""
from __future__ import annotations

import math
import os
from typing import Dict, List, Sequence, Tuple

import bmesh
import bpy
from mathutils import Euler, Vector

OUT_DIR = r"d:\UnityProjects\Zelda\NostalgiaGame\Assets\MiniVan Game\Art\Characters\AcidZombie"
PREVIEW_DIR = r"d:\UnityProjects\Zelda\tools\acid_zombie"
BLEND_PATH = os.path.join(OUT_DIR, "AcidZombie.blend")
FBX_PATH = os.path.join(OUT_DIR, "AcidZombie.fbx")
ALBEDO_PATH = os.path.join(OUT_DIR, "AcidZombie.png")
EMIT_PATH = os.path.join(OUT_DIR, "AcidZombie_Emit.png")

# 4x Minecraft skin (64x64 layout). Closest filtering keeps pixels sharp.
SCALE = 4
MC = 64
SIZE = MC * SCALE  # 256

# 1 Minecraft pixel = 1/16 m. Origin at feet. Faces +Y (vampire FBX convention).
PX = 1.0 / 16.0
HEAD = 8 * PX
BODY_W, BODY_H, BODY_D = 8 * PX, 12 * PX, 4 * PX
LIMB_W, LIMB_H = 4 * PX, 12 * PX

# Concept-sampled sRGB (olive skin, chartreuse acid — not white-hot).
SKIN_A = (0.392, 0.400, 0.235)
SKIN_B = (0.431, 0.435, 0.255)
SKIN_C = (0.478, 0.470, 0.290)
SKIN_D = (0.310, 0.318, 0.180)
SKIN_GRAY = (0.455, 0.450, 0.360)
ACID = (0.55, 0.82, 0.08)
ACID_HOT = (0.78, 0.95, 0.22)
ACID_DEEP = (0.40, 0.62, 0.05)
MOUTH = (0.06, 0.07, 0.04)
VOID = (0.04, 0.04, 0.04)


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


def skin_at(x: int, y: int) -> Tuple[float, float, float]:
    t = hash01(x, y, 11)
    if t < 0.22:
        c = SKIN_D
    elif t < 0.55:
        c = SKIN_A
    elif t < 0.82:
        c = SKIN_B
    else:
        c = SKIN_C
    if hash01(x, y, 77) > 0.82:
        c = mix(c, SKIN_GRAY, 0.45)
    # Fine grain so faces don't look like 4 flat colors.
    jitter = (hash01(x, y, 3) - 0.5) * 0.06
    return (
        max(0.0, min(1.0, c[0] + jitter)),
        max(0.0, min(1.0, c[1] + jitter * 0.8)),
        max(0.0, min(1.0, c[2] + jitter * 0.4)),
    )


def acid_at(x: int, y: int, hot: bool = False) -> Tuple[float, float, float]:
    t = hash01(x, y, 91)
    if hot or t > 0.78:
        return mix(ACID, ACID_HOT, 0.55 + 0.45 * hash01(x, y, 5))
    if t < 0.25:
        return ACID_DEEP
    return mix(ACID, ACID_HOT, t * 0.35)


def set_px(
    albedo: List[float],
    emit: List[float],
    x_tl: int,
    y_tl: int,
    rgb: Sequence[float],
    glowing: bool = False,
) -> None:
    if x_tl < 0 or y_tl < 0 or x_tl >= SIZE or y_tl >= SIZE:
        return
    y = SIZE - 1 - y_tl
    i = (y * SIZE + x_tl) * 4
    albedo[i] = rgb[0]
    albedo[i + 1] = rgb[1]
    albedo[i + 2] = rgb[2]
    albedo[i + 3] = 1.0
    if glowing:
        emit[i] = rgb[0]
        emit[i + 1] = rgb[1]
        emit[i + 2] = rgb[2]
        emit[i + 3] = 1.0
    else:
        emit[i] = 0.0
        emit[i + 1] = 0.0
        emit[i + 2] = 0.0
        emit[i + 3] = 1.0


def fill_rect(
    albedo: List[float],
    emit: List[float],
    mx: int,
    my: int,
    mw: int,
    mh: int,
    painter,
) -> None:
    """mx,my,mw,mh in Minecraft 64 top-left space."""
    for j in range(mh * SCALE):
        for i in range(mw * SCALE):
            x = mx * SCALE + i
            y = my * SCALE + j
            painter(albedo, emit, x, y, i, j, mw * SCALE, mh * SCALE)


def paint_skin(albedo, emit, x, y, *_rest) -> None:
    set_px(albedo, emit, x, y, skin_at(x, y), False)


def paint_acid_pixel(albedo, emit, x, y, hot: bool = False) -> None:
    set_px(albedo, emit, x, y, acid_at(x, y, hot), True)


def jagged_climb(i: int, j: int, w: int, h: int, base: float, salt: int) -> bool:
    """True if this pixel (i right, j down from top) is acid climbing from the bottom."""
    t = 1.0 - (j + 0.5) / max(h, 1)  # 0 at top, 1 at bottom
    n = hash01(i, j // 2, salt)
    wave = math.sin((i + 0.3) * 2.3 + salt) * 0.08
    edge = base + wave + (n - 0.5) * 0.18
    if hash01(i, 0, salt + 9) > 0.62:
        edge += 0.18 * hash01(i, j, salt + 2)
    return t <= edge or (t < edge + 0.12 and n > 0.84)


def paint_limb_face(albedo, emit, x, y, i, j, w, h, salt: int) -> None:
    if jagged_climb(i, j, w, h, 0.40, salt):
        paint_acid_pixel(albedo, emit, x, y, hot=(j > h * 0.72))
    else:
        paint_skin(albedo, emit, x, y)


def paint_chest_front(albedo, emit, x, y, i, j, w, h) -> None:
    # Leave a skin collar so the mouth glob does not fuse with the chest river.
    if j < int(h * 0.16):
        paint_skin(albedo, emit, x, y)
        return
    cx = (w - 1) * 0.5
    wobble = math.sin(j * 0.38) * 2.2 + (hash01(i, j, 21) - 0.5) * 1.6
    dist = abs(i - (cx + wobble))
    width = 2.1 + 1.4 * hash01(0, j, 8) + (1.1 if j > h * 0.55 else 0.0)
    if dist < width:
        paint_acid_pixel(albedo, emit, x, y, hot=dist < 1.1)
    else:
        paint_skin(albedo, emit, x, y)


def paint_spine_back(albedo, emit, x, y, i, j, w, h) -> None:
    cx = (w - 1) * 0.5
    wobble = math.sin(j * 0.55 + 0.4) * 1.6 + (hash01(i, j, 33) - 0.5) * 1.2
    dist = abs(i - (cx + wobble))
    if dist < 1.35 + (0.8 if hash01(0, j, 14) > 0.55 else 0.0):
        paint_acid_pixel(albedo, emit, x, y, hot=True)
    else:
        paint_skin(albedo, emit, x, y)


def paint_head_front(albedo, emit, x, y, i, j, w, h) -> None:
    # 32x32 face at SCALE=4. Eyes are two lime squares; mouth is a dark slit
    # with a short glob that STOPS at the chin (chest river is separate).
    in_eye_l = 6 <= i <= 12 and 9 <= j <= 14
    in_eye_r = 19 <= i <= 25 and 9 <= j <= 14
    if in_eye_l or in_eye_r:
        paint_acid_pixel(albedo, emit, x, y, hot=True)
        return
    in_mouth = 8 <= i <= 23 and 20 <= j <= 23
    cx = (w - 1) * 0.5
    drip = False
    if 21 <= j <= 29:
        drip_w = 3.2 + (j - 21) * 0.35
        if j >= 26:
            drip_w = 5.0 - (j - 26) * 0.9
        drip = abs(i - cx) < drip_w + hash01(i, j, 41) * 1.1
    if in_mouth and not drip:
        set_px(albedo, emit, x, y, MOUTH, False)
        return
    if drip:
        paint_acid_pixel(albedo, emit, x, y, hot=j >= 24)
        return
    paint_skin(albedo, emit, x, y)


def paint_drip_tile(albedo, emit, x, y, i, j, w, h) -> None:
    # Small hanging glob used by the mouth-drip plane.
    cx, cy = (w - 1) * 0.5, (h - 1) * 0.35
    dx = (i - cx) / max(w * 0.38, 0.01)
    dy = (j - cy) / max(h * 0.48, 0.01)
    blob = dx * dx + dy * dy * 0.75
    if blob < 1.0 and j < h * 0.85:
        paint_acid_pixel(albedo, emit, x, y, hot=blob < 0.35)
    elif 0.35 * w < i < 0.65 * w and j > h * 0.55:
        paint_acid_pixel(albedo, emit, x, y, hot=False)
    else:
        set_px(albedo, emit, x, y, (0.0, 0.0, 0.0), False)
        # Transparent-ish: keep black, no emit. Plane uses clip via alpha later if needed.
        yb = SIZE - 1 - y
        idx = (yb * SIZE + x) * 4
        albedo[idx + 3] = 0.0


def make_textures() -> Tuple[bpy.types.Image, bpy.types.Image]:
    n = SIZE * SIZE * 4
    albedo = [0.0] * n
    emit = [0.0] * n
    for i in range(0, n, 4):
        albedo[i:i + 3] = list(VOID)
        albedo[i + 3] = 1.0
        emit[i + 3] = 1.0

    def skin_face(mx, my, mw, mh):
        fill_rect(albedo, emit, mx, my, mw, mh, paint_skin)

    def limb(mx, my, mw, mh, salt):
        fill_rect(
            albedo,
            emit,
            mx,
            my,
            mw,
            mh,
            lambda a, e, x, y, i, j, w, h: paint_limb_face(a, e, x, y, i, j, w, h, salt),
        )

    # Head (Minecraft layout).
    skin_face(8, 0, 8, 8)  # top
    skin_face(16, 0, 8, 8)  # bottom
    skin_face(0, 8, 8, 8)  # right
    fill_rect(albedo, emit, 8, 8, 8, 8, paint_head_front)
    skin_face(16, 8, 8, 8)  # left
    skin_face(24, 8, 8, 8)  # back

    # Body.
    skin_face(20, 16, 8, 4)  # top
    skin_face(28, 16, 8, 4)  # bottom
    skin_face(16, 20, 4, 12)  # right
    fill_rect(albedo, emit, 20, 20, 8, 12, paint_chest_front)
    skin_face(28, 20, 4, 12)  # left
    fill_rect(albedo, emit, 32, 20, 8, 12, paint_spine_back)

    # Right arm.
    limb(44, 16, 4, 4, 101)  # top — no climb, still mottled; override:
    skin_face(44, 16, 4, 4)
    limb(48, 16, 4, 4, 102)
    limb(40, 20, 4, 12, 110)
    limb(44, 20, 4, 12, 111)
    limb(48, 20, 4, 12, 112)
    limb(52, 20, 4, 12, 113)

    # Right leg.
    skin_face(4, 16, 4, 4)
    limb(8, 16, 4, 4, 202)
    limb(0, 20, 4, 12, 210)
    limb(4, 20, 4, 12, 211)
    limb(8, 20, 4, 12, 212)
    limb(12, 20, 4, 12, 213)

    # Left leg (1.8 layout).
    skin_face(20, 48, 4, 4)
    limb(24, 48, 4, 4, 302)
    limb(16, 52, 4, 12, 310)
    limb(20, 52, 4, 12, 311)
    limb(24, 52, 4, 12, 312)
    limb(28, 52, 4, 12, 313)

    # Left arm.
    skin_face(36, 48, 4, 4)
    limb(40, 48, 4, 4, 402)
    limb(32, 52, 4, 12, 410)
    limb(36, 52, 4, 12, 411)
    limb(40, 52, 4, 12, 412)
    limb(44, 52, 4, 12, 413)

    # Mouth-drip sprite in unused corner (56,0) 8x16 in 64-space.
    fill_rect(albedo, emit, 56, 0, 8, 16, paint_drip_tile)

    img_a = bpy.data.images.new("AcidZombie", width=SIZE, height=SIZE, alpha=True)
    img_a.pixels = albedo
    img_a.filepath_raw = ALBEDO_PATH
    img_a.file_format = "PNG"
    img_a.save()
    img_a.pack()

    img_e = bpy.data.images.new("AcidZombie_Emit", width=SIZE, height=SIZE, alpha=True)
    img_e.colorspace_settings.name = "Non-Color"
    img_e.pixels = emit
    img_e.filepath_raw = EMIT_PATH
    img_e.file_format = "PNG"
    img_e.save()
    img_e.pack()
    return img_a, img_e


def mc_uv(mx: float, my: float, mw: float, mh: float) -> Tuple[float, float, float, float]:
    """Minecraft 64 TL rect -> Blender 0-1 UV (bottom-left)."""
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
        rect = regions[key]
        u0, v0, u1, v1 = rect
        xs = [loop.vert.co.x for loop in face.loops]
        ys = [loop.vert.co.y for loop in face.loops]
        zs = [loop.vert.co.z for loop in face.loops]
        if key in ("front", "back"):
            a, b = xs, zs
        elif key in ("left", "right"):
            a, b = ys, zs
        else:
            a, b = xs, ys
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
            if key == "back":
                nu = 1.0 - nu
            if key == "right":
                nu = 1.0 - nu
            loop[uv_layer].uv = Vector((u0 + nu * (u1 - u0), v0 + nv * (v1 - v0)))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def make_material(albedo: bpy.types.Image, emit_img: bpy.types.Image) -> bpy.types.Material:
    mat = bpy.data.materials.new("M_AcidZombie")
    mat.use_nodes = True
    mat.blend_method = "CLIP"
    mat.shadow_method = "CLIP"
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (520, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (180, 0)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.location = (-320, 80)
    tex.image = albedo
    tex.interpolation = "Closest"
    em = nt.nodes.new("ShaderNodeTexImage")
    em.location = (-320, -220)
    em.image = emit_img
    em.interpolation = "Closest"
    em.image.colorspace_settings.name = "Non-Color"
    sep = nt.nodes.new("ShaderNodeSeparateRGB")
    sep.location = (-40, -220)
    add = nt.nodes.new("ShaderNodeMath")
    add.location = (140, -180)
    add.operation = "ADD"
    add2 = nt.nodes.new("ShaderNodeMath")
    add2.location = (140, -320)
    add2.operation = "ADD"
    mul = nt.nodes.new("ShaderNodeMath")
    mul.location = (320, -220)
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = 3.6
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.92
    if "Specular" in bsdf.inputs:
        bsdf.inputs["Specular"].default_value = 0.04
    if "Emission" in bsdf.inputs:
        nt.links.new(em.outputs["Color"], bsdf.inputs["Emission"])
    if "Emission Strength" in bsdf.inputs:
        nt.links.new(em.outputs["Color"], sep.inputs["Image"])
        nt.links.new(sep.outputs["R"], add.inputs[0])
        nt.links.new(sep.outputs["G"], add.inputs[1])
        nt.links.new(add.outputs["Value"], add2.inputs[0])
        nt.links.new(sep.outputs["B"], add2.inputs[1])
        nt.links.new(add2.outputs["Value"], mul.inputs[0])
        nt.links.new(mul.outputs["Value"], bsdf.inputs["Emission Strength"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def add_cube(name: str, loc: Tuple[float, float, float], size: Tuple[float, float, float]) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.location = loc
    for poly in obj.data.polygons:
        poly.use_smooth = False
    set_active(obj)
    bpy.ops.object.shade_flat()
    return obj


def add_drip_plane(name: str, loc: Tuple[float, float, float], size: Tuple[float, float, float]) -> bpy.types.Object:
    """Thin box so the mouth glob reads from the side, like the concept."""
    return add_cube(name, loc, size)


HEAD_UV = {
    "top": mc_uv(8, 0, 8, 8),
    "bottom": mc_uv(16, 0, 8, 8),
    "right": mc_uv(0, 8, 8, 8),
    "front": mc_uv(8, 8, 8, 8),
    "left": mc_uv(16, 8, 8, 8),
    "back": mc_uv(24, 8, 8, 8),
}
BODY_UV = {
    "top": mc_uv(20, 16, 8, 4),
    "bottom": mc_uv(28, 16, 8, 4),
    "right": mc_uv(16, 20, 4, 12),
    "front": mc_uv(20, 20, 8, 12),
    "left": mc_uv(28, 20, 4, 12),
    "back": mc_uv(32, 20, 8, 12),
}
ARM_R_UV = {
    "top": mc_uv(44, 16, 4, 4),
    "bottom": mc_uv(48, 16, 4, 4),
    "right": mc_uv(40, 20, 4, 12),
    "front": mc_uv(44, 20, 4, 12),
    "left": mc_uv(48, 20, 4, 12),
    "back": mc_uv(52, 20, 4, 12),
}
ARM_L_UV = {
    "top": mc_uv(36, 48, 4, 4),
    "bottom": mc_uv(40, 48, 4, 4),
    "right": mc_uv(32, 52, 4, 12),
    "front": mc_uv(36, 52, 4, 12),
    "left": mc_uv(40, 52, 4, 12),
    "back": mc_uv(44, 52, 4, 12),
}
LEG_R_UV = {
    "top": mc_uv(4, 16, 4, 4),
    "bottom": mc_uv(8, 16, 4, 4),
    "right": mc_uv(0, 20, 4, 12),
    "front": mc_uv(4, 20, 4, 12),
    "left": mc_uv(8, 20, 4, 12),
    "back": mc_uv(12, 20, 4, 12),
}
LEG_L_UV = {
    "top": mc_uv(20, 48, 4, 4),
    "bottom": mc_uv(24, 48, 4, 4),
    "right": mc_uv(16, 52, 4, 12),
    "front": mc_uv(20, 52, 4, 12),
    "left": mc_uv(24, 52, 4, 12),
    "back": mc_uv(28, 52, 4, 12),
}
DRIP_UV = {
    "front": mc_uv(56, 0, 8, 16),
    "back": mc_uv(56, 0, 8, 16),
    "left": mc_uv(56, 0, 8, 16),
    "right": mc_uv(56, 0, 8, 16),
    "top": mc_uv(56, 0, 8, 8),
    "bottom": mc_uv(56, 8, 8, 8),
}


def build_meshes(mat: bpy.types.Material) -> bpy.types.Object:
    root = bpy.data.objects.new("AcidZombie", None)
    bpy.context.collection.objects.link(root)

    def attach(obj, uvs):
        obj.data.materials.append(mat)
        map_box_uvs(obj, uvs)
        obj.parent = root
        return obj

    head_z = LIMB_H + BODY_H + HEAD * 0.5
    body_z = LIMB_H + BODY_H * 0.5
    limb_z = LIMB_H * 0.5
    arm_x = BODY_W * 0.5 + LIMB_W * 0.5
    leg_x = BODY_W * 0.25

    attach(add_cube("Head", (0.0, 0.0, head_z), (HEAD, HEAD, HEAD)), HEAD_UV)
    attach(add_cube("Body", (0.0, 0.0, body_z), (BODY_W, BODY_D, BODY_H)), BODY_UV)
    attach(add_cube("Left Arm", (-arm_x, 0.0, body_z), (LIMB_W, LIMB_W, LIMB_H)), ARM_L_UV)
    attach(add_cube("Right Arm", (arm_x, 0.0, body_z), (LIMB_W, LIMB_W, LIMB_H)), ARM_R_UV)
    attach(add_cube("Left Leg", (-leg_x, 0.0, limb_z), (LIMB_W, LIMB_W, LIMB_H)), LEG_L_UV)
    attach(add_cube("Right Leg", (leg_x, 0.0, limb_z), (LIMB_W, LIMB_W, LIMB_H)), LEG_R_UV)

    # Short textured glob on the chin — must not reach the torso.
    drip = add_drip_plane("MouthDrip", (0.0, HEAD * 0.54, head_z - 0.10), (0.08, 0.03, 0.09))
    attach(drip, DRIP_UV)
    return root


def setup_world() -> None:
    scene = bpy.context.scene
    world = bpy.data.worlds.new("AcidZombieWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.11, 0.11, 0.11, 1.0)
        bg.inputs[1].default_value = 1.0
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    if hasattr(scene.eevee, "use_bloom"):
        scene.eevee.use_bloom = True
        scene.eevee.bloom_intensity = 0.10
        scene.eevee.bloom_threshold = 0.85
        scene.eevee.bloom_radius = 2.2


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_views() -> None:
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 640
    scene.render.resolution_y = 800
    target = Vector((0.0, 0.0, 1.05))
    dist = 3.6

    views = {
        "front": Vector((0.0, dist, 1.05)),
        "back": Vector((0.0, -dist, 1.05)),
        "side": Vector((dist, 0.0, 1.05)),
        "three_quarter": Vector((2.4, 3.2, 1.55)),
        "top": Vector((0.0, 0.01, 4.2)),
    }

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    cam_data.lens = 50
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.35

    light_data = bpy.data.lights.new(name="Key", type="AREA")
    light_data.energy = 220
    light_data.size = 3.0
    light = bpy.data.objects.new(name="Key", object_data=light_data)
    bpy.context.collection.objects.link(light)
    light.location = (1.6, 2.2, 3.2)

    fill_data = bpy.data.lights.new(name="Fill", type="AREA")
    fill_data.energy = 50
    fill_data.size = 4.0
    fill = bpy.data.objects.new(name="Fill", object_data=fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (-2.0, -1.0, 2.4)

    for name, loc in views.items():
        cam.location = loc
        look_at(cam, target)
        if name == "top":
            cam_data.ortho_scale = 1.7
            cam.location = Vector((0.0, 0.0, 4.4))
            look_at(cam, target)
        elif name == "three_quarter":
            cam_data.type = "PERSP"
            cam_data.lens = 50
            cam_data.ortho_scale = 2.35
        else:
            cam_data.type = "ORTHO"
            cam_data.ortho_scale = 2.35
        scene.render.filepath = os.path.join(PREVIEW_DIR, f"preview_{name}.png")
        bpy.ops.render.render(write_still=True)
        print("[AcidZombie] preview", scene.render.filepath)

    # Restore persp three-quarter as the main preview in the art folder.
    cam.location = views["three_quarter"]
    look_at(cam, target)
    cam_data.type = "PERSP"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.filepath = os.path.join(OUT_DIR, "AcidZombie_preview.png")
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
    os.makedirs(os.path.join(OUT_DIR, "Concepts"), exist_ok=True)
    clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    albedo, emit_img = make_textures()
    mat = make_material(albedo, emit_img)
    build_meshes(mat)
    setup_world()
    render_views()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export_fbx()
    print("[AcidZombie] blend=", BLEND_PATH)
    print("[AcidZombie] fbx=", FBX_PATH)
    print("[AcidZombie] albedo=", ALBEDO_PATH)
    print("[AcidZombie] emit=", EMIT_PATH)


if __name__ == "__main__":
    main()
