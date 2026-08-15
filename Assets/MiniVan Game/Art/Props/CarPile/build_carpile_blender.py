"""
Scrap car pile - one decorative mesh.

  "C:\\Program Files\\Blender Foundation\\Blender 3.4\\blender.exe" --background --python build_carpile_blender.py

~14 wrecks on a 12x10 m footprint in three tiers, all from the same sedan
generator the auto service uses, varied by colour, damage, open doors, tilt
and scale. Every window is smashed, so no glass is emitted and the whole pile
collapses into a single mesh with a single material.
"""
from __future__ import annotations

import math
import os
import sys

import bpy
from mathutils import Euler, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
SHARED = os.path.abspath(os.path.join(OUT_DIR, "..", "..", "Buildings", "AutoService"))
for p in (OUT_DIR, SHARED):
    if p not in sys.path:
        sys.path.insert(0, p)

import as_atlas   # noqa: E402
import as_common as C  # noqa: E402

# .blend stays outside Assets: Unity 6 imports .blend through Blender and can hang Bee
BLEND_DIR = os.path.normpath(os.path.join(OUT_DIR, "..", "..", "..", "..", "..", "..", "tools", "props"))
os.makedirs(BLEND_DIR, exist_ok=True)
BLEND_PATH = os.path.join(BLEND_DIR, "CarPile.blend")
FBX_PATH = os.path.join(OUT_DIR, "CarPile.fbx")
ATLAS_PATH = os.path.join(SHARED, "AutoService_Atlas.png")

CAR_H = 1.45          # used to lift the flipped wreck back above ground
SPREAD = 0.86         # horizontal squeeze applied to the layout below


def wreck(idx: int, pos, yaw: float, color: str, roll: float = 0.0, pitch: float = 0.0,
          scale: float = 1.0, doors=(), **flags):
    """One car, welded into a single object and dropped into the pile."""
    name = f"CP_{idx:02d}"
    meshes = C.build_sedan(name, (0.0, 0.0, 0.0), 0.0, color, **flags)
    keep = []
    for m in meshes:
        if m.name.endswith("_Glass"):
            bpy.data.objects.remove(m, do_unlink=True)   # every window is gone
            continue
        keep.append(m)
    for m in keep:
        for tag, ang in doors:
            if m.name.endswith(tag):
                m.rotation_euler[2] = math.radians(ang)   # origin already on the hinge
                C.apply_trs(m)
    obj = C.join_as(name, keep, origin=None)
    obj.rotation_euler = Euler((math.radians(pitch), math.radians(roll), math.radians(yaw)))
    obj.scale = (scale, scale, scale)
    # squeeze the footprint horizontally: wrecks in a heap overlap, and it keeps
    # the pile inside the agreed 12x10 m
    obj.location = (pos[0] * SPREAD, pos[1] * SPREAD, pos[2])
    C.apply_trs(obj, loc=False, rot=True, scale=True)
    return obj


# x, y, z, yaw, colour, extras -------------------------------------------------
LAYOUT = [
    # ground tier
    dict(pos=(-3.8, -3.2, 0.00), yaw=8, color="car_blue", doors=(("_Door_FL", 58),)),
    dict(pos=(0.5, -3.6, 0.00), yaw=-16, color="car_beige", no_bumper=True),
    dict(pos=(4.3, -2.4, 0.00), yaw=24, color="car_green"),
    dict(pos=(-4.6, 0.9, 0.00), yaw=96, color="car_orange", no_hood=True),
    dict(pos=(0.1, 0.5, 0.00), yaw=-6, color="car_blue", no_wheel_fl=True),
    dict(pos=(4.8, 1.8, 0.00), yaw=82, color="car_beige", doors=(("_Door_RR", -52),)),
    dict(pos=(-1.2, 3.8, 0.00), yaw=168, color="car_green", no_trunk_lid=True),
    # second tier
    dict(pos=(-2.6, -1.5, 1.30), yaw=28, roll=7, color="car_orange"),
    dict(pos=(2.2, -1.0, 1.38), yaw=-34, pitch=-6, color="car_beige", no_hood=True),
    dict(pos=(1.4, 2.6, 1.32), yaw=128, roll=-8, color="car_blue", doors=(("_Door_FR", 62),)),
    dict(pos=(-3.2, 2.4, 1.28), yaw=62, roll=5, color="car_green", scale=1.16),   # the big one
    # top tier
    dict(pos=(-0.8, 0.1, 2.58), yaw=42, roll=12, color="car_orange", no_bumper=True),
    dict(pos=(2.6, 1.2, 2.52), yaw=-68, pitch=9, color="car_green"),
    dict(pos=(0.3, -1.6, 2.60 + CAR_H), yaw=200, roll=180, color="car_beige",
         no_hood=True, no_trunk_lid=True),                                        # upside down
]


def build_debris():
    parts = []
    for (x, y, rz) in ((-5.6, -1.4, 0), (5.4, -0.4, 12), (-2.0, 4.9, -20), (3.4, 4.4, 8)):
        parts.append(C.make_cyl((x, y, 0.11), 0.33, 0.20, "rubber", verts=12))
        parts.append(C.make_cyl((x + 0.42, y + 0.18, 0.11), 0.33, 0.20, "rubber", verts=12,
                                rot=(math.radians(84), 0, math.radians(rz))))
    parts.append(C.make_box((-5.0, 3.2, 0.28), (1.20, 0.60, 0.06), "car_blue",
                            rot=(0, math.radians(6), math.radians(30))))
    parts.append(C.make_box((5.2, 3.0, 0.30), (1.05, 0.66, 0.06), "car_orange",
                            rot=(0, math.radians(-9), math.radians(-24))))
    parts.append(C.make_box((-0.4, -4.7, 0.22), (0.90, 0.70, 0.40), "rust_dark",
                            rot=(0, 0, math.radians(18))))
    return parts


def render_previews():
    scene, cam, cam_data = C.setup_render()
    cam_data.lens = 40
    cam.location = Vector((-13.0, -14.0, 9.0))
    C.look_at(cam, Vector((0.0, 0.0, 1.4)))
    C.render_to(scene, os.path.join(OUT_DIR, "CarPile_preview.png"))

    cam_data.lens = 34
    cam.location = Vector((9.0, -9.5, 4.2))
    C.look_at(cam, Vector((-0.5, 0.5, 1.6)))
    C.render_to(scene, os.path.join(OUT_DIR, "CarPile_preview_close.png"))


def main():
    C.clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    img = as_atlas.save_atlas(ATLAS_PATH)
    lit, glass = C.make_materials(img)
    C.set_materials(lit, glass)

    root = C.new_empty("CarPile", (0, 0, 0))
    cars = [wreck(i + 1, **spec) for i, spec in enumerate(LAYOUT)]
    pile = C.join_as("CP_Pile", cars + build_debris(), origin=(0, 0, 0))
    pile.parent = root

    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    try:
        render_previews()
    except Exception as exc:
        print("[CarPile] preview failed:", exc)
    C.export_fbx(root, FBX_PATH)

    tris = sum(len(p.vertices) - 2 for p in pile.data.polygons)
    bb = pile.bound_box
    size = [max(v[i] for v in bb) - min(v[i] for v in bb) for i in range(3)]
    print("[CarPile] cars:", len(cars), " tris:", tris,
          " size: %.1f x %.1f x %.1f" % tuple(size))


if __name__ == "__main__":
    main()
