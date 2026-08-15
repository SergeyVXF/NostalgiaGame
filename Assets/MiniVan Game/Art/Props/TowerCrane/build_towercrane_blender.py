"""
Low-poly tower crane with a lifting magnet.

  "C:\\Program Files\\Blender Foundation\\Blender 3.4\\blender.exe" --background --python build_towercrane_blender.py

Compact size: 12 m mast, 14 m jib. Authored Z-up with the jib along +Y, which
the shared exporter turns into Unity's +Z.

Everything that has to move later is its own mesh with the origin on the axis
it moves about:
  TC_Slew      - whole top assembly, spins about the mast centre
  TC_Trolley   - rides along the jib
  TC_Rope      - origin at the top, scale it in Y to pay out cable
  TC_Magnet    - origin at the rope attachment, drops straight down
  TC_Lever_1/2/3, TC_Button - cab controls, origin on their pivot
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
BLEND_PATH = os.path.join(BLEND_DIR, "TowerCrane.blend")
FBX_PATH = os.path.join(OUT_DIR, "TowerCrane.fbx")
ATLAS_PATH = os.path.join(SHARED, "AutoService_Atlas.png")

# ---------------------------------------------------------------------------
MAST_W = 1.10           # square lattice section
MAST_Z0 = 0.50          # top of the ballast pad
MAST_TOP = 12.00
BAY = 1.15              # lattice bay height
LEG = 0.12              # corner leg thickness
BRACE = 0.07

SLEW_Z = MAST_TOP       # turntable height
CAB_Z = SLEW_Z + 0.30
CAB_W, CAB_D, CAB_H = 1.35, 1.55, 1.85
LADDER_Z0, LADDER_Z1 = 0.55, CAB_Z + 0.25   # runs past the cab floor to step off
# Sits behind the cab (cab occupies y -0.28..1.28), so the rails never pierce it
# and the climber arrives straight in front of the doorway.
LADDER_X, LADDER_Y = -0.71, -0.85
DOOR_X, DOOR_W, DOOR_H = -0.86, 0.62, 1.80  # cab doorway, lines up with the ladder

JIB_Y0, JIB_Y1 = 0.85, 14.85     # 14 m of jib
JIB_TOP_Z = SLEW_Z + 1.55
JIB_BOT_Z = SLEW_Z + 0.95
JIB_HALF = 0.36
CJIB_Y = -4.60                   # counter-jib tail
APEX_Z = SLEW_Z + 3.30

TROLLEY_Y = 9.20
ROPE_LEN = 6.20
MAGNET_R = 0.85


def bar(p0, p1, thick, tile):
    """Box running between two points, whatever the direction."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    length = d.length
    mid = (p0 + p1) * 0.5
    obj = C.make_box(mid, (thick, thick, length), tile)
    obj.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    C.apply_trs(obj)
    return obj


# ---------------------------------------------------------------------------
def build_base():
    parts = [
        C.make_box((0, 0, 0.25), (4.20, 4.20, 0.50), "concrete"),
        C.make_box((0, 0, 0.54), (2.60, 2.60, 0.12), "steel"),
    ]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(C.make_box((sx * 1.55, sy * 1.55, 0.72), (0.90, 0.90, 0.46), "hazard"))
            parts.append(C.make_box((sx * (MAST_W * 0.5 - 0.06), sy * (MAST_W * 0.5 - 0.06),
                                     MAST_Z0 * 0.5 + 0.25), (0.22, 0.22, 0.55), "crane_worn"))
    return C.join_as("TC_Base", parts, origin=(0, 0, 0))


def build_mast():
    parts = []
    h = MAST_W * 0.5 - LEG * 0.5
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(C.make_box((sx * h, sy * h, (MAST_Z0 + MAST_TOP) * 0.5),
                                    (LEG, LEG, MAST_TOP - MAST_Z0), "crane"))
    bays = int((MAST_TOP - MAST_Z0) / BAY)
    for i in range(bays + 1):
        z = MAST_Z0 + i * BAY
        if z > MAST_TOP:
            break
        for sy in (-1, 1):
            parts.append(C.make_box((0, sy * h, z), (MAST_W - LEG, BRACE, BRACE), "crane"))
        for sx in (-1, 1):
            parts.append(C.make_box((sx * h, 0, z), (BRACE, MAST_W - LEG, BRACE), "crane"))
    # single diagonal per face per bay, alternating so it reads as a zigzag
    for i in range(bays):
        z0 = MAST_Z0 + i * BAY
        z1 = z0 + BAY
        flip = i % 2 == 0
        for sy in (-1, 1):
            a = (-h if flip else h, sy * h, z0)
            b = (h if flip else -h, sy * h, z1)
            parts.append(bar(a, b, BRACE, "crane"))
        for sx in (-1, 1):
            a = (sx * h, -h if flip else h, z0)
            b = (sx * h, h if flip else -h, z1)
            parts.append(bar(a, b, BRACE, "crane"))
    return C.join_as("TC_Mast", parts, origin=(0, 0, 0))


def build_ladder():
    """One uninterrupted run from the ground to the cab doorway.

    No rest platforms: they broke the climb volume into pieces and the player
    could not get past them. The climb itself is driven by MiniVanLadder in
    Unity, which needs a single clean box to work with.
    """
    parts = []
    x, y = LADDER_X, LADDER_Y
    z0, z1 = LADDER_Z0, LADDER_Z1
    for sx in (-1, 1):
        parts.append(C.make_box((x + sx * 0.24, y, (z0 + z1) * 0.5), (0.05, 0.05, z1 - z0), "steel"))
    z = z0 + 0.28
    while z < z1 - 0.10:
        parts.append(C.make_box((x, y, z), (0.52, 0.05, 0.04), "steel"))
        z += 0.32
    # hoops above head height, open toward the mast so they never block the climb
    z = z0 + 2.60
    while z < z1 - 0.40:
        parts.append(C.make_box((x, y - 0.34, z), (0.56, 0.05, 0.04), "steel"))
        for sx in (-1, 1):
            parts.append(C.make_box((x + sx * 0.28, y - 0.17, z), (0.05, 0.36, 0.04), "steel"))
        z += 1.40
    # stand-off brackets tying the rails back to the nearest mast leg
    leg = (-(MAST_W * 0.5 - LEG * 0.5), -(MAST_W * 0.5 - LEG * 0.5))
    z = z0 + 1.10
    while z < z1 - 0.30:
        parts.append(bar((x, y, z), (leg[0], leg[1], z), 0.05, "steel"))
        z += 2.30
    return C.join_as("TC_Ladder", parts, origin=(0, 0, 0))


def build_slew():
    """Turntable, A-frame and the tie bars - the body everything rotates with."""
    parts = [
        C.make_cyl((0, 0, SLEW_Z - 0.10), 0.80, 0.30, "steel", verts=12),
        C.make_box((0, 0, SLEW_Z + 0.12), (2.10, 2.30, 0.24), "crane"),
    ]
    # A-frame legs to the apex
    for sy in (-1, 1):
        parts.append(bar((0.0, sy * 0.70, SLEW_Z + 0.24), (0.0, 0.0, APEX_Z), 0.13, "crane"))
    parts.append(C.make_box((0, 0, APEX_Z + 0.06), (0.34, 0.34, 0.16), "crane"))
    # tie bars out to the jib and back to the counter-jib
    for y in (6.40, 13.60):
        parts.append(bar((0.0, 0.0, APEX_Z), (0.0, y, JIB_TOP_Z + 0.10), 0.07, "steel"))
    parts.append(bar((0.0, 0.0, APEX_Z), (0.0, CJIB_Y + 0.30, SLEW_Z + 0.90), 0.07, "steel"))
    # railing round the machine deck
    for sy in (-1, 1):
        parts.append(C.make_box((0, sy * 1.10, SLEW_Z + 0.60), (2.10, 0.05, 0.72), "steel"))
    return C.join_as("TC_Slew", parts, origin=(0, 0, SLEW_Z))


def build_jib():
    """Inverted triangle truss: two top chords carry the trolley."""
    parts = []
    for sx in (-1, 1):
        parts.append(C.make_box((sx * JIB_HALF, (JIB_Y0 + JIB_Y1) * 0.5, JIB_TOP_Z),
                                (0.11, JIB_Y1 - JIB_Y0, 0.11), "crane"))
    parts.append(C.make_box((0, (JIB_Y0 + JIB_Y1) * 0.5, JIB_BOT_Z),
                            (0.11, JIB_Y1 - JIB_Y0, 0.11), "crane"))
    bays = 10
    step = (JIB_Y1 - JIB_Y0) / bays
    for i in range(bays + 1):
        y = JIB_Y0 + i * step
        parts.append(C.make_box((0, y, JIB_TOP_Z), (JIB_HALF * 2, BRACE, BRACE), "crane"))
        if i < bays:
            y1 = y + step
            for sx in (-1, 1):
                parts.append(bar((sx * JIB_HALF, y, JIB_TOP_Z), (0.0, y1, JIB_BOT_Z), BRACE, "crane"))
    # jib head with the rope sheave
    parts.append(C.make_box((0, JIB_Y1 + 0.18, JIB_TOP_Z - 0.30), (0.50, 0.36, 0.70), "crane"))
    parts.append(C.make_cyl((0, JIB_Y1 + 0.18, JIB_TOP_Z - 0.55), 0.16, 0.10, "steel",
                            verts=10, rot=(0, math.radians(90), 0)))
    return C.join_as("TC_Jib", parts, origin=(0, 0, SLEW_Z))


def build_counter_jib():
    parts = []
    for sx in (-1, 1):
        parts.append(C.make_box((sx * 0.34, (CJIB_Y - 0.85) * 0.5, JIB_TOP_Z - 0.25),
                                (0.11, abs(CJIB_Y) - 0.85, 0.11), "crane"))
        parts.append(C.make_box((sx * 0.34, (CJIB_Y - 0.85) * 0.5, SLEW_Z + 0.30),
                                (0.11, abs(CJIB_Y) - 0.85, 0.11), "crane"))
    for i in range(4):
        y = -1.10 - i * 1.05
        parts.append(bar((-0.34, y, SLEW_Z + 0.30), (0.34, y - 0.60, JIB_TOP_Z - 0.25), BRACE, "crane"))
    # counterweight slabs + winch drum
    parts.append(C.make_box((0, CJIB_Y + 0.35, SLEW_Z + 0.72), (1.70, 1.05, 1.05), "concrete"))
    parts.append(C.make_box((0, CJIB_Y + 0.35, SLEW_Z + 1.28), (1.76, 1.10, 0.10), "steel"))
    parts.append(C.make_cyl((0, -2.10, SLEW_Z + 0.75), 0.34, 0.90, "steel",
                            verts=12, rot=(0, math.radians(90), 0)))
    parts.append(C.make_box((0, -2.10, SLEW_Z + 0.30), (1.10, 0.90, 0.20), "crane_worn"))
    return C.join_as("TC_CounterJib", parts, origin=(0, 0, SLEW_Z))


def build_cab():
    """Operator cab hung off the -X side of the deck: glazed on three sides so
    the controls inside are actually visible, solid only at the back."""
    cx = -(MAST_W * 0.5 + CAB_W * 0.5 - 0.02)
    cy = 0.50
    t = 0.07
    sill = CAB_Z + 0.52
    head = CAB_Z + CAB_H - 0.16
    parts = [
        C.make_box((cx, cy, CAB_Z - 0.05), (CAB_W + 0.14, CAB_D + 0.14, 0.12), "steel"),
        C.make_box((cx, cy, CAB_Z + CAB_H), (CAB_W + 0.12, CAB_D + 0.12, 0.09), "cab"),
    ]
    # back wall with a walk-through doorway instead of a door panel
    by = cy - CAB_D * 0.5
    d0, d1 = DOOR_X - DOOR_W * 0.5, DOOR_X + DOOR_W * 0.5
    wl, wr = cx - CAB_W * 0.5, cx + CAB_W * 0.5
    for a, b in ((wl, d0), (d1, wr)):
        if b - a > 0.02:
            parts.append(C.make_box(((a + b) * 0.5, by, CAB_Z + CAB_H * 0.5),
                                    (b - a, t, CAB_H), "cab"))
    parts.append(C.make_box((DOOR_X, by, CAB_Z + (DOOR_H + CAB_H) * 0.5),
                            (DOOR_W, t, CAB_H - DOOR_H), "cab"))
    for xj in (d0, d1):
        parts.append(C.make_box((xj, by, CAB_Z + DOOR_H * 0.5), (0.05, t + 0.02, DOOR_H), "steel"))
    # corner posts + waist and header rails around the glazed sides
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(C.make_box((cx + sx * (CAB_W * 0.5 - 0.045), cy + sy * (CAB_D * 0.5 - 0.045),
                                     CAB_Z + CAB_H * 0.5), (0.09, 0.09, CAB_H), "cab"))
    for (px, py, sx_, sy_) in ((cx, cy + CAB_D * 0.5, CAB_W, t),
                               (cx + CAB_W * 0.5, cy, t, CAB_D),
                               (cx - CAB_W * 0.5, cy, t, CAB_D)):
        parts.append(C.make_box((px, py, CAB_Z + 0.26), (sx_, sy_, 0.52), "cab"))
        parts.append(C.make_box((px, py, head + 0.08), (sx_, sy_, 0.16), "cab"))
    # bracket carrying the cab off the slewing deck
    for sy in (-1, 1):
        parts.append(bar((-MAST_W * 0.5 - 0.05, cy + sy * 0.45, SLEW_Z + 0.26),
                         (cx - 0.35, cy + sy * 0.45, CAB_Z - 0.08), 0.10, "crane"))
    # seat
    parts.append(C.make_box((cx - 0.16, cy - 0.30, CAB_Z + 0.34), (0.50, 0.48, 0.14), "seat"))
    parts.append(C.make_box((cx - 0.16, cy - 0.52, CAB_Z + 0.72), (0.50, 0.14, 0.60), "seat"))
    cab = C.join_as("TC_Cab", parts, origin=(0, 0, SLEW_Z))

    gz = (sill + head) * 0.5
    gh = head - sill
    glass = [
        C.make_box((cx, cy + CAB_D * 0.5, gz), (CAB_W - 0.14, 0.03, gh), "glass", mat=C.glass_mat()),
        C.make_box((cx + CAB_W * 0.5, cy, gz), (0.03, CAB_D - 0.14, gh), "glass", mat=C.glass_mat()),
        C.make_box((cx - CAB_W * 0.5, cy, gz), (0.03, CAB_D - 0.14, gh), "glass", mat=C.glass_mat()),
    ]
    cab_glass = C.join_as("TC_Cab_Glass", glass, origin=(0, 0, SLEW_Z))
    return cab, cab_glass, (cx, cy)


def build_controls(cx: float, cy: float):
    """Console with three levers and the magnet on/off button."""
    px, py, pz = cx + 0.30, cy + 0.30, CAB_Z + 0.07
    parts = [
        C.make_box((px, py, pz + 0.30), (0.86, 0.46, 0.60), "steel"),
        C.make_box((px, py, pz + 0.62), (0.90, 0.50, 0.06), "dark"),
    ]
    console = C.join_as("TC_Console", parts, origin=(0, 0, SLEW_Z))

    made = []
    for i in range(3):
        lx = px - 0.26 + i * 0.24
        base = C.make_box((lx, py - 0.06, pz + 0.66), (0.10, 0.10, 0.06), "dark")
        stick = C.make_box((lx, py - 0.06, pz + 0.86), (0.045, 0.045, 0.36), "steel")
        knob = C.make_cyl((lx, py - 0.06, pz + 1.05), 0.055, 0.08, "red", verts=8)
        lever = C.join_as(f"TC_Lever_{i + 1}", [base, stick, knob], origin=None)
        C.origin_to(lever, (lx, py - 0.06, pz + 0.68))     # pivot at the console
        made.append(lever)

    btn_x, btn_y = px + 0.30, py - 0.06
    cap = C.make_cyl((btn_x, btn_y, pz + 0.70), 0.075, 0.07, "red", verts=10)
    ring = C.make_cyl((btn_x, btn_y, pz + 0.65), 0.095, 0.04, "steel", verts=10)
    button = C.join_as("TC_Button", [cap, ring], origin=None)
    C.origin_to(button, (btn_x, btn_y, pz + 0.65))
    return [console] + made + [button]


def build_trolley():
    parts = [
        C.make_box((0, TROLLEY_Y, JIB_TOP_Z - 0.22), (0.86, 0.74, 0.28), "crane_worn"),
        C.make_box((0, TROLLEY_Y, JIB_TOP_Z - 0.40), (0.40, 0.40, 0.14), "steel"),
    ]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(C.make_cyl((sx * JIB_HALF, TROLLEY_Y + sy * 0.26, JIB_TOP_Z - 0.06),
                                    0.10, 0.07, "steel", verts=8, rot=(0, math.radians(90), 0)))
    trolley = C.join_as("TC_Trolley", parts, origin=None)
    C.origin_to(trolley, (0, TROLLEY_Y, JIB_TOP_Z))
    return trolley


def build_rope():
    top = JIB_TOP_Z - 0.44
    rope = C.make_cyl((0, TROLLEY_Y, top - ROPE_LEN * 0.5), 0.035, ROPE_LEN, "dark", verts=6)
    rope = C.join_as("TC_Rope", [rope], origin=None)
    C.origin_to(rope, (0, TROLLEY_Y, top))     # scale in Y (Unity) to pay out
    return rope


def build_magnet():
    z = JIB_TOP_Z - 0.44 - ROPE_LEN
    parts = [
        C.make_box((0, TROLLEY_Y, z - 0.12), (0.34, 0.34, 0.26), "steel"),
        C.make_cyl((0, TROLLEY_Y, z - 0.42), MAGNET_R * 0.55, 0.36, "steel", verts=12),
        C.make_cyl((0, TROLLEY_Y, z - 0.72), MAGNET_R, 0.30, "magnet", verts=16),
        C.make_cyl((0, TROLLEY_Y, z - 0.90), MAGNET_R * 0.94, 0.10, "hazard", verts=16),
    ]
    for sx in (-1, 1):
        parts.append(bar((sx * 0.30, TROLLEY_Y, z - 0.02),
                         (sx * MAGNET_R * 0.5, TROLLEY_Y, z - 0.40), 0.07, "steel"))
    magnet = C.join_as("TC_Magnet", parts, origin=None)
    C.origin_to(magnet, (0, TROLLEY_Y, z))
    return magnet


# ---------------------------------------------------------------------------
def render_previews():
    scene, cam, cam_data = C.setup_render()
    cam_data.lens = 32
    cam.location = Vector((-16.0, -18.0, 13.0))
    C.look_at(cam, Vector((0.0, 3.0, 7.0)))
    C.render_to(scene, os.path.join(OUT_DIR, "TowerCrane_preview.png"))

    cam_data.lens = 45
    cam.location = Vector((-7.5, -3.2, 14.6))
    C.look_at(cam, Vector((-1.4, 1.2, 12.9)))
    C.render_to(scene, os.path.join(OUT_DIR, "TowerCrane_preview_cab.png"))

    cam_data.lens = 40
    cam.location = Vector((-6.0, 9.0, 8.5))
    C.look_at(cam, Vector((0.0, 9.2, 6.5)))
    C.render_to(scene, os.path.join(OUT_DIR, "TowerCrane_preview_magnet.png"))


def main():
    C.clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    img = as_atlas.save_atlas(ATLAS_PATH)
    lit, glass = C.make_materials(img)
    C.set_materials(lit, glass)

    root = C.new_empty("TowerCrane", (0, 0, 0))
    flat = [build_base(), build_mast(), build_ladder(), build_slew(),
            build_jib(), build_counter_jib()]
    cab, cab_glass, (cx, cy) = build_cab()
    flat += [cab, cab_glass]
    flat += build_controls(cx, cy)
    flat += [build_trolley(), build_rope(), build_magnet()]

    for o in flat:
        o.parent = root
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    try:
        render_previews()
    except Exception as exc:
        print("[TowerCrane] preview failed:", exc)
    C.export_fbx(root, FBX_PATH)

    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons)
               for o in bpy.data.objects if o.type == "MESH")
    print("[TowerCrane] objects:", len(flat), " tris:", tris)


if __name__ == "__main__":
    main()
