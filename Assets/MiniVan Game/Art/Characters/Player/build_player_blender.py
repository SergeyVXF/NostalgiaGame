"""
MiniVan player: Minecraft-style low-poly body + half-capsule head.

Run:
  "C:\\Program Files\\Blender Foundation\\Blender 3.4\\blender.exe" --background --python build_player_blender.py
"""
from __future__ import annotations

import math
import os
from typing import Dict, List, Sequence, Tuple

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(OUT_DIR, "MiniVanPlayer.blend")
FBX_PATH = os.path.join(OUT_DIR, "MiniVanPlayer.fbx")
BODY_TEX_PATH = os.path.join(OUT_DIR, "MiniVanPlayer_Body.png")
HEAD_TEX_PATH = os.path.join(OUT_DIR, "MiniVanPlayer_Head.png")

TEX_SIZE = 256

# Proportions in meters. Origin at feet. Character faces -Y (Unity +Z after FBX).
FOOT_H = 0.08
LEG_SHORTS_H = 0.28
LEG_SHIN_H = 0.28
TORSO_W = 0.44
TORSO_H = 0.50
TORSO_D = 0.26
ARM_W = 0.16
SLEEVE_H = 0.22
FOREARM_H = 0.32
HEAD_R = 0.20
HEAD_CYL = 0.28
HEAD_SEGS = 16
HEAD_RINGS = 7

LEG_H = LEG_SHORTS_H + LEG_SHIN_H
HIP_Z = FOOT_H + LEG_H
SHOULDER_Z = HIP_Z + TORSO_H
HEAD_TOP_Z = SHOULDER_Z + HEAD_CYL + HEAD_R
ARM_LEN = SLEEVE_H + FOREARM_H

SKIN = (0.93, 0.88, 0.80, 1.0)
ORANGE = (0.82, 0.42, 0.18, 1.0)
TEAL = (0.12, 0.55, 0.52, 1.0)
YELLOW = (0.95, 0.62, 0.22, 1.0)
WHITE = (0.94, 0.94, 0.92, 1.0)
KHAKI = (0.72, 0.62, 0.42, 1.0)
SANDAL = (0.16, 0.11, 0.08, 1.0)
STRAP = (0.10, 0.08, 0.07, 1.0)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.actions, bpy.data.armatures):
        for item in list(block):
            block.remove(item)


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def ensure_mat(name: str, color: Sequence[float], roughness: float = 0.55) -> bpy.types.Material:
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    if bsdf is None:
        for node in list(nt.nodes):
            nt.nodes.remove(node)
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        out.location = (300, 0)
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.location = (0, 0)
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.0
    if "Specular" in bsdf.inputs:
        bsdf.inputs["Specular"].default_value = 0.2
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.2
    return mat


def assign_image(mat: bpy.types.Material, image: bpy.types.Image) -> None:
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.get("Image Texture")
    if tex is None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.location = (-280, 0)
        tex.interpolation = "Closest"
    tex.image = image
    if bsdf is not None:
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])


def add_cube(size: Tuple[float, float, float], loc: Tuple[float, float, float], name: str) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def shade_flat(obj: bpy.types.Object) -> None:
    set_active(obj)
    bpy.ops.object.shade_flat()


def map_box_uvs(obj: bpy.types.Object, regions: Dict[str, Tuple[float, float, float, float]]) -> None:
    """Map cube faces to atlas rects. Keys: front, back, left, right, top, bottom."""
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    uv_layer = bm.loops.layers.uv.verify()
    for face in bm.faces:
        n = face.normal
        if n.y < -0.5:
            key = "front"
        elif n.y > 0.5:
            key = "back"
        elif n.x < -0.5:
            key = "left"
        elif n.x > 0.5:
            key = "right"
        elif n.z > 0.5:
            key = "top"
        else:
            key = "bottom"
        rect = regions.get(key) or regions.get("side") or regions.get("front")
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


def cylindrical_head_uvs(obj: bpy.types.Object) -> None:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    uv_layer = bm.loops.layers.uv.verify()
    zs = [v.co.z for v in bm.verts]
    z0, z1 = min(zs), max(zs)
    dz = max(z1 - z0, 1e-6)
    for face in bm.faces:
        for loop in face.loops:
            p = loop.vert.co
            # Front (-Y) sits at U=0.5 so the face is in the middle of the atlas.
            u = (math.atan2(p.x, -p.y) / (2.0 * math.pi)) + 0.5
            if u < 0.0:
                u += 1.0
            if u >= 1.0:
                u -= 1.0
            v = (p.z - z0) / dz
            loop[uv_layer].uv = Vector((u, v))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def create_head() -> bpy.types.Object:
    """Half-capsule in local space: origin at the neck, +Z up, front = -Y."""
    bm = bmesh.new()
    segs = HEAD_SEGS
    cyl_divs = 4
    hemi_divs = HEAD_RINGS

    def ring(z: float, radius: float):
        vs = []
        for i in range(segs):
            ang = (i / segs) * math.pi * 2.0
            x = math.sin(ang) * radius
            y = -math.cos(ang) * radius
            vs.append(bm.verts.new((x, y, z)))
        return vs

    bottom = bm.verts.new((0.0, 0.0, 0.0))
    rings = [ring(0.0, HEAD_R)]
    for i in range(1, cyl_divs + 1):
        rings.append(ring(HEAD_CYL * (i / cyl_divs), HEAD_R))
    for i in range(1, hemi_divs):
        phi = (i / hemi_divs) * (math.pi * 0.5)
        z = HEAD_CYL + math.sin(phi) * HEAD_R
        radius = math.cos(phi) * HEAD_R
        rings.append(ring(z, max(radius, 0.0008)))
    pole = bm.verts.new((0.0, 0.0, HEAD_CYL + HEAD_R))

    for i in range(segs):
        bm.faces.new((bottom, rings[0][i], rings[0][(i + 1) % segs]))
    for r in range(len(rings) - 1):
        for i in range(segs):
            v0 = rings[r][i]
            v1 = rings[r][(i + 1) % segs]
            v2 = rings[r + 1][(i + 1) % segs]
            v3 = rings[r + 1][i]
            bm.faces.new((v0, v1, v2, v3))
    last = rings[-1]
    for i in range(segs):
        bm.faces.new((last[i], last[(i + 1) % segs], pole))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("Head")
    bm.to_mesh(mesh)
    bm.free()
    head = bpy.data.objects.new("Head", mesh)
    bpy.context.scene.collection.objects.link(head)
    shade_flat(head)
    cylindrical_head_uvs(head)
    return head


def paint_flower(px: List[float], size: int, cx: float, cy: float, scale: float) -> None:
    petal_r = 7.0 * scale
    center_r = 3.2 * scale
    for i in range(8):
        ang = i * (math.pi / 4.0)
        for t in range(6):
            rr = (t / 5.0) * petal_r
            x = cx + math.cos(ang) * rr
            y = cy + math.sin(ang) * rr
            rad = 2.4 * scale * (1.0 - t / 8.0)
            stamp_circle(px, size, x, y, rad, TEAL)
    stamp_circle(px, size, cx, cy, center_r, YELLOW)


def stamp_circle(px: List[float], size: int, cx: float, cy: float, radius: float, color: Sequence[float]) -> None:
    r = int(math.ceil(radius))
    x0 = max(0, int(cx) - r - 1)
    x1 = min(size - 1, int(cx) + r + 1)
    y0 = max(0, int(cy) - r - 1)
    y1 = min(size - 1, int(cy) + r + 1)
    r2 = radius * radius
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            dx = x + 0.5 - cx
            dy = y + 0.5 - cy
            if dx * dx + dy * dy <= r2:
                i = (y * size + x) * 4
                px[i] = color[0]
                px[i + 1] = color[1]
                px[i + 2] = color[2]
                px[i + 3] = 1.0


def fill_rect(px: List[float], size: int, x0: int, y0: int, x1: int, y1: int, color: Sequence[float]) -> None:
    x0 = max(0, x0)
    y0 = max(0, y0)
    x1 = min(size, x1)
    y1 = min(size, y1)
    for y in range(y0, y1):
        for x in range(x0, x1):
            i = (y * size + x) * 4
            px[i] = color[0]
            px[i + 1] = color[1]
            px[i + 2] = color[2]
            px[i + 3] = 1.0


def fill_hawaiian(px: List[float], size: int, x0: int, y0: int, x1: int, y1: int) -> None:
    fill_rect(px, size, x0, y0, x1, y1, ORANGE)
    step = 28
    row = 0
    y = y0 + 14
    while y < y1:
        x = x0 + 14 + (10 if row % 2 else 0)
        while x < x1:
            if x0 + 4 < x < x1 - 4 and y0 + 4 < y < y1 - 4:
                paint_flower(px, size, x, y, 0.85)
            x += step
        y += 24
        row += 1


def make_body_texture() -> bpy.types.Image:
    size = TEX_SIZE
    px = [0.0] * (size * size * 4)
    # Atlas (pixels, origin bottom-left):
    # torso front 0-128, 128-256
    fill_hawaiian(px, size, 0, 128, 128, 256)
    # white undershirt down the open front
    fill_rect(px, size, 48, 128, 80, 248, WHITE)
    fill_rect(px, size, 44, 236, 84, 256, WHITE)
    # torso back
    fill_hawaiian(px, size, 128, 128, 256, 256)
    # sides / top / bottom
    fill_hawaiian(px, size, 0, 64, 64, 128)
    fill_hawaiian(px, size, 64, 64, 128, 128)
    fill_hawaiian(px, size, 128, 64, 192, 128)
    fill_hawaiian(px, size, 192, 64, 256, 128)
    # sleeves
    fill_hawaiian(px, size, 0, 0, 64, 64)
    fill_hawaiian(px, size, 64, 0, 128, 64)
    # shorts
    fill_rect(px, size, 128, 0, 192, 64, KHAKI)
    # sandals: sole + two straps
    fill_rect(px, size, 192, 0, 256, 64, SANDAL)
    fill_rect(px, size, 198, 28, 250, 36, STRAP)
    fill_rect(px, size, 198, 44, 250, 52, STRAP)

    img = bpy.data.images.new("MiniVanPlayer_Body", width=size, height=size, alpha=True)
    img.pixels = px
    img.filepath_raw = BODY_TEX_PATH
    img.file_format = "PNG"
    img.save()
    img.pack()
    return img


def make_head_texture() -> bpy.types.Image:
    size = TEX_SIZE
    px = [0.0] * (size * size * 4)
    fill_rect(px, size, 0, 0, size, size, SKIN)
    img = bpy.data.images.new("MiniVanPlayer_Head", width=size, height=size, alpha=True)
    img.pixels = px
    img.filepath_raw = HEAD_TEX_PATH
    img.file_format = "PNG"
    img.save()
    img.pack()
    return img


def parent_to_bone(obj: bpy.types.Object, armature: bpy.types.Object, bone_name: str) -> None:
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world


def create_armature() -> bpy.types.Object:
    bpy.ops.object.armature_add(enter_editmode=True, location=(0.0, 0.0, 0.0))
    arm_obj = bpy.context.active_object
    arm_obj.name = "PlayerVisual"
    arm = arm_obj.data
    arm.name = "MiniVanPlayer"
    bpy.ops.armature.select_all(action="SELECT")
    bpy.ops.armature.delete()

    def add_bone(name: str, head: Tuple[float, float, float], tail: Tuple[float, float, float], parent: str | None = None):
        bone = arm.edit_bones.new(name)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        bone.use_deform = True
        bone.use_connect = False
        if parent:
            bone.parent = arm.edit_bones[parent]
        return bone

    hip_z = HIP_Z
    neck_z = SHOULDER_Z
    add_bone("Body", (0.0, 0.0, hip_z), (0.0, 0.0, neck_z))
    add_bone("Head", (0.0, 0.0, neck_z), (0.0, 0.0, HEAD_TOP_Z), "Body")
    shoulder_x = TORSO_W * 0.5 + ARM_W * 0.5
    add_bone("Arm_L", (-shoulder_x, 0.0, neck_z - 0.02), (-shoulder_x, 0.0, neck_z - 0.02 - ARM_LEN), "Body")
    add_bone("Arm_R", (shoulder_x, 0.0, neck_z - 0.02), (shoulder_x, 0.0, neck_z - 0.02 - ARM_LEN), "Body")
    hip_x = TORSO_W * 0.25
    add_bone("Leg_L", (-hip_x, 0.0, hip_z), (-hip_x, 0.0, FOOT_H), "Body")
    add_bone("Leg_R", (hip_x, 0.0, hip_z), (hip_x, 0.0, FOOT_H), "Body")

    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def build_meshes(body_img: bpy.types.Image, head_img: bpy.types.Image) -> Dict[str, bpy.types.Object]:
    mat_clothes = ensure_mat("M_PlayerClothes", ORANGE)
    assign_image(mat_clothes, body_img)
    mat_head = ensure_mat("M_PlayerHead", SKIN, roughness=0.62)
    assign_image(mat_head, head_img)
    mat_skin = ensure_mat("M_PlayerSkin", SKIN, roughness=0.62)

    hawaiian = {
        "front": (0.00, 0.50, 0.50, 1.00),
        "back": (0.50, 0.50, 1.00, 1.00),
        "left": (0.00, 0.25, 0.25, 0.50),
        "right": (0.25, 0.25, 0.50, 0.50),
        "top": (0.50, 0.25, 0.75, 0.50),
        "bottom": (0.75, 0.25, 1.00, 0.50),
    }
    sleeve = {
        "front": (0.00, 0.00, 0.25, 0.25),
        "back": (0.00, 0.00, 0.25, 0.25),
        "left": (0.25, 0.00, 0.50, 0.25),
        "right": (0.25, 0.00, 0.50, 0.25),
        "top": (0.00, 0.00, 0.25, 0.25),
        "bottom": (0.25, 0.00, 0.50, 0.25),
    }
    shorts = {
        "front": (0.50, 0.00, 0.75, 0.25),
        "back": (0.50, 0.00, 0.75, 0.25),
        "left": (0.50, 0.00, 0.75, 0.25),
        "right": (0.50, 0.00, 0.75, 0.25),
        "top": (0.50, 0.00, 0.75, 0.25),
        "bottom": (0.50, 0.00, 0.75, 0.25),
    }
    sandal = {
        "front": (0.75, 0.00, 1.00, 0.25),
        "back": (0.75, 0.00, 1.00, 0.25),
        "left": (0.75, 0.00, 1.00, 0.25),
        "right": (0.75, 0.00, 1.00, 0.25),
        "top": (0.75, 0.00, 1.00, 0.25),
        "bottom": (0.75, 0.00, 1.00, 0.25),
    }

    parts: Dict[str, bpy.types.Object] = {}

    torso_z = HIP_Z + TORSO_H * 0.5
    torso = add_cube((TORSO_W, TORSO_D, TORSO_H), (0.0, 0.0, torso_z), "Torso")
    torso.data.materials.append(mat_clothes)
    map_box_uvs(torso, hawaiian)
    shade_flat(torso)
    parts["Torso"] = torso

    head = create_head()
    head.location = (0.0, 0.0, SHOULDER_Z)
    head.data.materials.clear()
    head.data.materials.append(mat_head)
    shade_flat(head)
    parts["Head"] = head

    shoulder_x = TORSO_W * 0.5 + ARM_W * 0.5
    sleeve_z = SHOULDER_Z - 0.02 - SLEEVE_H * 0.5
    forearm_z = SHOULDER_Z - 0.02 - SLEEVE_H - FOREARM_H * 0.5
    for side, sx in (("L", -1.0), ("R", 1.0)):
        sleeve_obj = add_cube((ARM_W, ARM_W, SLEEVE_H), (sx * shoulder_x, 0.0, sleeve_z), f"Arm{side}_Sleeve")
        sleeve_obj.data.materials.append(mat_clothes)
        map_box_uvs(sleeve_obj, sleeve)
        shade_flat(sleeve_obj)
        parts[f"Arm{side}_Sleeve"] = sleeve_obj

        forearm = add_cube((ARM_W * 0.92, ARM_W * 0.92, FOREARM_H), (sx * shoulder_x, 0.0, forearm_z), f"Arm{side}_Forearm")
        forearm.data.materials.append(mat_skin)
        shade_flat(forearm)
        parts[f"Arm{side}_Forearm"] = forearm

    hip_x = TORSO_W * 0.25
    shorts_z = FOOT_H + LEG_SHIN_H + LEG_SHORTS_H * 0.5
    shin_z = FOOT_H + LEG_SHIN_H * 0.5
    foot_z = FOOT_H * 0.5
    for side, sx in (("L", -1.0), ("R", 1.0)):
        shorts_obj = add_cube((ARM_W + 0.04, ARM_W + 0.02, LEG_SHORTS_H), (sx * hip_x, 0.0, shorts_z), f"Leg{side}_Shorts")
        shorts_obj.data.materials.append(mat_clothes)
        map_box_uvs(shorts_obj, shorts)
        shade_flat(shorts_obj)
        parts[f"Leg{side}_Shorts"] = shorts_obj

        shin = add_cube((ARM_W, ARM_W, LEG_SHIN_H), (sx * hip_x, 0.0, shin_z), f"Leg{side}_Shin")
        shin.data.materials.append(mat_skin)
        shade_flat(shin)
        parts[f"Leg{side}_Shin"] = shin

        foot = add_cube((ARM_W + 0.02, 0.26, FOOT_H), (sx * hip_x, 0.04, foot_z), f"Foot{side}")
        foot.data.materials.append(mat_clothes)
        map_box_uvs(foot, sandal)
        shade_flat(foot)
        parts[f"Foot{side}"] = foot

    return parts


def add_hand_socket(armature: bpy.types.Object) -> bpy.types.Object:
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0.0, 0.0, 0.0))
    empty = bpy.context.active_object
    empty.name = "RightHand"
    empty.empty_display_size = 0.06
    bone = armature.pose.bones["Arm_R"]
    wrist = armature.matrix_world @ bone.tail
    empty.location = wrist + Vector((0.0, -0.02, 0.0))
    empty.rotation_euler = Euler((math.radians(90.0), 0.0, 0.0))
    parent_to_bone(empty, armature, "Arm_R")
    return empty


def key_pose(armature: bpy.types.Object, frame: int, pose: Dict[str, Tuple[float, float, float]]) -> None:
    bpy.context.scene.frame_set(frame)
    for name, euler_deg in pose.items():
        pb = armature.pose.bones[name]
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = Euler((math.radians(euler_deg[0]), math.radians(euler_deg[1]), math.radians(euler_deg[2])))
        pb.keyframe_insert(data_path="rotation_euler", frame=frame)
        pb.keyframe_insert(data_path="location", frame=frame)


def rest() -> Dict[str, Tuple[float, float, float]]:
    return {
        "Body": (0.0, 0.0, 0.0),
        "Head": (0.0, 0.0, 0.0),
        "Arm_L": (0.0, 0.0, 0.0),
        "Arm_R": (0.0, 0.0, 0.0),
        "Leg_L": (0.0, 0.0, 0.0),
        "Leg_R": (0.0, 0.0, 0.0),
    }


def make_action(armature: bpy.types.Object, name: str, frames: List[Tuple[int, Dict[str, Tuple[float, float, float]]]], loop: bool) -> bpy.types.Action:
    set_active(armature)
    bpy.ops.object.mode_set(mode="POSE")
    for pb in armature.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = Euler((0.0, 0.0, 0.0))
        pb.location = Vector((0.0, 0.0, 0.0))

    action = bpy.data.actions.new(name)
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame, pose in frames:
        merged = rest()
        merged.update(pose)
        key_pose(armature, frame, merged)

    for fcurve in action.fcurves:
        for kp in fcurve.keyframe_points:
            kp.interpolation = "LINEAR" if name in ("Walk", "Idle") else "BEZIER"
            if loop:
                kp.easing = "EASE_IN_OUT"

    action.use_fake_user = True
    bpy.ops.object.mode_set(mode="OBJECT")
    return action


def create_animations(armature: bpy.types.Object) -> None:
    scene = bpy.context.scene
    scene.render.fps = 30

    # Idle: light breathing + arm sway.
    idle_frames = []
    for i, t in enumerate((1, 16, 31, 46, 61)):
        phase = i / 4.0 * math.pi * 2.0
        bob = math.sin(phase) * 2.0
        arm = math.sin(phase) * 4.0
        idle_frames.append((t, {
            "Body": (bob, 0.0, 0.0),
            "Head": (-bob * 0.4, 0.0, 0.0),
            "Arm_L": (8.0 + arm, 0.0, 6.0),
            "Arm_R": (8.0 - arm, 0.0, -6.0),
        }))
    make_action(armature, "Idle", idle_frames, True)

    # Walk: Minecraft opposite swing.
    walk = []
    keys = [
        (1, 40.0, -40.0),
        (8, 0.0, 0.0),
        (15, -40.0, 40.0),
        (22, 0.0, 0.0),
        (29, 40.0, -40.0),
    ]
    for frame, a, b in keys:
        walk.append((frame, {
            "Arm_L": (a, 0.0, 8.0),
            "Arm_R": (b, 0.0, -8.0),
            "Leg_L": (b * 0.9, 0.0, 0.0),
            "Leg_R": (a * 0.9, 0.0, 0.0),
            "Body": (0.0, 0.0, math.sin(frame) * 1.5),
            "Head": (0.0, 0.0, 0.0),
        }))
    make_action(armature, "Walk", walk, True)

    # Sit: legs forward, slight recline, arms on lap.
    sit_pose = {
        "Body": (12.0, 0.0, 0.0),
        "Head": (-6.0, 0.0, 0.0),
        "Arm_L": (55.0, 0.0, 12.0),
        "Arm_R": (55.0, 0.0, -12.0),
        "Leg_L": (-82.0, 0.0, 6.0),
        "Leg_R": (-82.0, 0.0, -6.0),
    }
    make_action(armature, "Sit", [(1, sit_pose), (10, sit_pose)], True)

    # Bat swing: windup back, sweep across, recover.
    bat = [
        (1, {"Arm_R": (15.0, 0.0, -20.0), "Arm_L": (10.0, 0.0, 8.0), "Body": (0.0, 0.0, 8.0)}),
        (5, {"Arm_R": (-70.0, 20.0, -35.0), "Arm_L": (20.0, 0.0, 12.0), "Body": (0.0, 0.0, 18.0), "Head": (0.0, 0.0, 8.0)}),
        (10, {"Arm_R": (55.0, -10.0, 50.0), "Arm_L": (25.0, 0.0, 15.0), "Body": (8.0, 0.0, -16.0), "Head": (4.0, 0.0, -6.0)}),
        (16, {"Arm_R": (15.0, 0.0, -20.0), "Arm_L": (10.0, 0.0, 8.0), "Body": (0.0, 0.0, 0.0)}),
    ]
    make_action(armature, "BatSwing", bat, False)

    # Stake stab: pull back, thrust, recover.
    stake = [
        (1, {"Arm_R": (20.0, 0.0, -10.0), "Body": (0.0, 0.0, 0.0)}),
        (4, {"Arm_R": (-25.0, 0.0, -18.0), "Body": (6.0, 0.0, 8.0), "Head": (4.0, 0.0, 0.0)}),
        (8, {"Arm_R": (75.0, 0.0, -8.0), "Body": (-4.0, 0.0, -6.0), "Head": (-2.0, 0.0, 0.0)}),
        (13, {"Arm_R": (20.0, 0.0, -10.0), "Body": (0.0, 0.0, 0.0)}),
    ]
    make_action(armature, "StakeStab", stake, False)

    # Cross hold: right arm raised in front of chest.
    cross = {
        "Arm_R": (70.0, -8.0, 18.0),
        "Arm_L": (12.0, 0.0, 8.0),
        "Head": (4.0, 0.0, 0.0),
        "Body": (2.0, 0.0, 0.0),
    }
    make_action(armature, "CrossHold", [(1, cross), (20, cross)], True)

    hold = {
        "Arm_R": (48.0, -6.0, -16.0),
        "Arm_L": (10.0, 0.0, 8.0),
        "Body": (2.0, 0.0, 4.0),
        "Head": (0.0, 0.0, 0.0),
    }
    make_action(armature, "HoldItem", [(1, hold), (16, hold)], True)

    armature.animation_data.action = bpy.data.actions.get("Idle")


def export_fbx(armature: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "EMPTY", "ARMATURE"}:
            obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"ARMATURE", "MESH", "EMPTY"},
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        armature_nodetype="NULL",
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=True,
    )


def render_preview(armature: bpy.types.Object) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 768
    scene.render.filepath = os.path.join(OUT_DIR, "MiniVanPlayer_preview.png")
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("PreviewWorld") if bpy.data.worlds.get("PreviewWorld") is None else bpy.data.worlds["PreviewWorld"]
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg is not None:
        bg.inputs[0].default_value = (0.45, 0.45, 0.48, 1.0)
        bg.inputs[1].default_value = 1.0

    target = Vector((0.0, 0.0, 0.82))
    loc = Vector((2.6, -3.4, 1.35))
    bpy.ops.object.camera_add(location=loc)
    cam = bpy.context.active_object
    cam.name = "PreviewCam"
    direction = target - loc
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    bpy.ops.object.light_add(type="SUN", location=(2.0, -1.5, 4.0))
    sun = bpy.context.active_object
    sun.data.energy = 3.0
    sun.rotation_euler = Euler((math.radians(40), math.radians(15), math.radians(30)))

    armature.animation_data.action = bpy.data.actions.get("Idle")
    scene.frame_set(1)
    bpy.ops.render.render(write_still=True)
    print("[Player] preview=", scene.render.filepath)


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    body_img = make_body_texture()
    head_img = make_head_texture()
    parts = build_meshes(body_img, head_img)
    armature = create_armature()

    parent_to_bone(parts["Torso"], armature, "Body")
    parent_to_bone(parts["Head"], armature, "Head")
    parent_to_bone(parts["ArmL_Sleeve"], armature, "Arm_L")
    parent_to_bone(parts["ArmL_Forearm"], armature, "Arm_L")
    parent_to_bone(parts["ArmR_Sleeve"], armature, "Arm_R")
    parent_to_bone(parts["ArmR_Forearm"], armature, "Arm_R")
    parent_to_bone(parts["LegL_Shorts"], armature, "Leg_L")
    parent_to_bone(parts["LegL_Shin"], armature, "Leg_L")
    parent_to_bone(parts["FootL"], armature, "Leg_L")
    parent_to_bone(parts["LegR_Shorts"], armature, "Leg_R")
    parent_to_bone(parts["LegR_Shin"], armature, "Leg_R")
    parent_to_bone(parts["FootR"], armature, "Leg_R")
    add_hand_socket(armature)

    bpy.context.view_layer.update()
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        zs = [c.z for c in corners]
        print(f"  bounds {obj.name}: z={min(zs):.3f}..{max(zs):.3f} verts={len(obj.data.vertices)}")

    create_animations(armature)
    render_preview(armature)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export_fbx(armature)

    print("[Player] height=", round(HEAD_TOP_Z, 3))
    print("[Player] blend=", BLEND_PATH)
    print("[Player] fbx=", FBX_PATH)
    print("[Player] body_tex=", BODY_TEX_PATH)
    print("[Player] head_tex=", HEAD_TEX_PATH)
    print("[Player] actions=", [a.name for a in bpy.data.actions])


if __name__ == "__main__":
    main()
