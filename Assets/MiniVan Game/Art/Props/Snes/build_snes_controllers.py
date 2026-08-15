"""
Two SNES controllers with cables — local-space hierarchy, top-down preview.
Blender 3.4+
"""
import math
import os
import bpy
from mathutils import Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(OUT_DIR, "SnesControllers.blend")
FBX_PATH = os.path.join(OUT_DIR, "SnesControllers.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "SnesControllers_preview.png")

COL_BODY = (0.72, 0.72, 0.74, 1.0)
COL_DARK = (0.32, 0.32, 0.34, 1.0)
COL_BLACK = (0.06, 0.06, 0.07, 1.0)
COL_P_LIGHT = (0.70, 0.55, 0.84, 1.0)
COL_P_DARK = (0.40, 0.18, 0.58, 1.0)
COL_CABLE = (0.07, 0.07, 0.08, 1.0)


def clear_scene():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.materials, bpy.data.curves):
        for block in list(data):
            data.remove(block)


def get_mat(name, color, rough=0.55):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name=name)
        m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = rough
    return m


def set_mat(obj, material):
    obj.data.materials.clear()
    obj.data.materials.append(material)


def smooth(obj):
    mesh = obj.data
    for p in mesh.polygons:
        p.use_smooth = True
    if hasattr(mesh, "use_auto_smooth"):
        mesh.use_auto_smooth = True
        mesh.auto_smooth_angle = math.radians(40)


def link_child(obj, parent, local_loc=(0, 0, 0), local_rot=(0, 0, 0), local_scale=None):
    obj.parent = parent
    obj.location = local_loc
    obj.rotation_euler = local_rot
    if local_scale is not None:
        obj.scale = local_scale


def new_empty(name, parent=None, loc=(0, 0, 0)):
    e = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(e)
    e.empty_display_size = 0.04
    e.empty_display_type = "PLAIN_AXES"
    if parent:
        link_child(e, parent, loc)
    else:
        e.location = loc
    return e


def add_cyl(name, parent, radius, depth, loc, rot=(0, 0, 0), scale=(1, 1, 1), verts=32, material=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    link_child(obj, parent, loc, rot, scale)
    if material:
        set_mat(obj, material)
    smooth(obj)
    return obj


def add_cube(name, parent, size, loc, rot=(0, 0, 0), scale=(1, 1, 1), material=None):
    bpy.ops.mesh.primitive_cube_add(size=size, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    link_child(obj, parent, loc, rot, scale)
    if material:
        set_mat(obj, material)
    smooth(obj)
    return obj


def add_sphere(name, parent, radius, loc, scale=(1, 1, 1), material=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=radius, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    link_child(obj, parent, loc, (0, 0, 0), scale)
    if material:
        set_mat(obj, material)
    smooth(obj)
    return obj


def add_tube_seg(name, parent, p0, p1, radius, material):
    d = Vector(p1) - Vector(p0)
    length = d.length
    if length < 1e-5:
        return None
    mid = (Vector(p0) + Vector(p1)) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=length, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    obj.parent = parent
    obj.location = mid
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = d.to_track_quat("Z", "Y")
    set_mat(obj, material)
    smooth(obj)
    return obj


def build_pad(name, world_loc, yaw_deg, cable_side):
    m_body = get_mat("SnesBody", COL_BODY, 0.6)
    m_dark = get_mat("SnesDark", COL_DARK, 0.5)
    m_black = get_mat("SnesBlack", COL_BLACK, 0.45)
    m_pl = get_mat("SnesPurpleL", COL_P_LIGHT, 0.35)
    m_pd = get_mat("SnesPurpleD", COL_P_DARK, 0.35)
    m_cable = get_mat("SnesCable", COL_CABLE, 0.7)

    root = new_empty(name, None, world_loc)
    root.rotation_euler = (0, 0, math.radians(yaw_deg))

    # Body parts as separate meshes (no boolean — more reliable)
    body = new_empty(name + "_Body", root, (0, 0, 0))

    # Center bridge
    add_cube(name + "_Bridge", body, 1.0, (0, 0, 0), scale=(0.10, 0.040, 0.016), material=m_body)
    # Lobes
    add_cyl(name + "_LobeL", body, 0.050, 0.016, (-0.065, 0, 0), scale=(1.25, 1.0, 1.0), verts=48, material=m_body)
    add_cyl(name + "_LobeR", body, 0.050, 0.016, (0.065, 0, 0), scale=(1.25, 1.0, 1.0), verts=48, material=m_body)
    # Top face slight lip
    add_cube(name + "_TopFace", body, 1.0, (0, 0, 0.0085), scale=(0.09, 0.034, 0.002), material=m_body)

    # Dark face plate
    add_cyl(name + "_FacePlate", body, 0.031, 0.0025, (0.065, 0, 0.0095), scale=(1.1, 0.95, 1), verts=40, material=m_dark)
    # Dpad recess
    add_cyl(name + "_DpadRecess", body, 0.022, 0.0018, (-0.065, 0, 0.0092), verts=32, material=get_mat("SnesRecess", (0.60, 0.60, 0.62, 1), 0.7))

    # D-pad
    add_cube(name + "_DpadH", body, 1.0, (-0.065, 0, 0.0115), scale=(0.028, 0.010, 0.0045), material=m_black)
    add_cube(name + "_DpadV", body, 1.0, (-0.065, 0, 0.0115), scale=(0.010, 0.028, 0.0045), material=m_black)

    # Action buttons diamond
    r = 0.014
    cx = 0.065
    add_sphere(name + "_BtnX", body, 0.0072, (cx, r, 0.012), scale=(1, 1, 0.55), material=m_pl)
    add_sphere(name + "_BtnY", body, 0.0072, (cx - r, 0, 0.012), scale=(1, 1, 0.55), material=m_pl)
    add_sphere(name + "_BtnA", body, 0.0072, (cx + r, 0, 0.012), scale=(1, 1, 0.55), material=m_pd)
    add_sphere(name + "_BtnB", body, 0.0072, (cx, -r, 0.012), scale=(1, 1, 0.55), material=m_pd)

    # Start / Select
    add_cyl(name + "_Select", body, 0.004, 0.014, (-0.014, -0.007, 0.0105),
            rot=(math.radians(90), 0, math.radians(40)), scale=(0.45, 0.45, 1), verts=16, material=m_dark)
    add_cyl(name + "_Start", body, 0.004, 0.014, (0.014, -0.007, 0.0105),
            rot=(math.radians(90), 0, math.radians(40)), scale=(0.45, 0.45, 1), verts=16, material=m_dark)

    # Shoulders
    add_cube(name + "_BtnL", body, 1.0, (-0.082, 0.038, 0.001), scale=(0.028, 0.010, 0.006), material=m_body)
    add_cube(name + "_BtnR", body, 1.0, (0.082, 0.038, 0.001), scale=(0.028, 0.010, 0.006), material=m_body)

    # Strain relief
    add_cyl(name + "_Strain", body, 0.006, 0.018, (0, 0.046, 0),
            rot=(math.radians(90), 0, 0), verts=16, material=m_cable)
    for i, y in enumerate((0.040, 0.046, 0.052)):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.0062, minor_radius=0.00105,
                                         major_segments=16, minor_segments=8, location=(0, 0, 0))
        rib = bpy.context.active_object
        rib.name = name + "_Rib%d" % i
        link_child(rib, body, (0, y, 0), (math.radians(90), 0, 0))
        set_mat(rib, m_cable)

    # Cable in local space of body (+Y out of top)
    s = cable_side
    pts = [
        (0.0, 0.055, 0.0),
        (0.02 * s, 0.14, -0.01),
        (0.05 * s, 0.26, -0.04),
        (0.08 * s, 0.40, -0.05),
        (0.10 * s, 0.55, -0.02),
    ]
    cable = new_empty(name + "_Cable", body, (0, 0, 0))
    for i in range(len(pts) - 1):
        add_tube_seg("%s_Seg%d" % (name, i), cable, pts[i], pts[i + 1], 0.0032, m_cable)
        add_sphere("%s_Joint%d" % (name, i), cable, 0.0032, pts[i + 1], material=m_cable)

    return root


def setup_camera_lights():
    # Top-down camera (Blender camera looks along local -Z)
    bpy.ops.object.camera_add(location=(0.0, 0.12, 1.35), rotation=(0.0, 0.0, 0.0))
    cam = bpy.context.active_object
    cam.name = "_Camera"
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 1.15
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(0.5, -0.5, 2.0))
    sun = bpy.context.active_object
    sun.name = "_Sun"
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(25), math.radians(20), 0)

    bpy.ops.object.light_add(type="AREA", location=(0.0, 0.0, 1.0))
    area = bpy.context.active_object
    area.name = "_Fill"
    area.data.energy = 60
    area.data.size = 2.0

    bpy.ops.mesh.primitive_plane_add(size=4.0, location=(0, 0.15, -0.03))
    ground = bpy.context.active_object
    ground.name = "_Ground"
    set_mat(ground, get_mat("Ground", (0.55, 0.55, 0.57, 1), 0.95))

    world = bpy.context.scene.world or bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.45, 0.45, 0.47, 1)
        bg.inputs[1].default_value = 0.35


def export_fbx():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.data.objects:
        if obj.name.startswith("_"):
            continue
        if obj.type in {"EMPTY", "MESH"}:
            obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        object_types={"EMPTY", "MESH"},
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        path_mode="AUTO",
    )


def render_preview():
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1400
    sc.render.resolution_y = 800
    sc.render.filepath = PREVIEW_PATH
    sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)


def main():
    clear_scene()
    build_pad("SnesPad_L", (-0.22, 0.0, 0.0), 15.0, -1)
    build_pad("SnesPad_R", (0.22, 0.0, 0.0), -15.0, 1)
    setup_camera_lights()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export_fbx()
    render_preview()
    # Count meshes
    pads = [o for o in bpy.data.objects if o.name.startswith("SnesPad_") and o.parent is None]
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and not o.name.startswith("_")]
    print("Pads:", len(pads), "Meshes:", len(meshes))
    print("Saved:", BLEND_PATH)
    print("Exported:", FBX_PATH)
    print("Preview:", PREVIEW_PATH)


if __name__ == "__main__":
    main()
