"""
Generate defibrillator suitcase + tube as two single-mesh OBJ files (no Blender required).
Also writes MTL materials. Run: python generate_defib_obj.py
"""
from __future__ import annotations

import math
import os
from dataclasses import dataclass, field
from typing import Dict, List, Sequence, Tuple

OUT_DIR = os.path.dirname(os.path.abspath(__file__))

Vec3 = Tuple[float, float, float]


@dataclass
class MeshBuilder:
    name: str
    vertices: List[Vec3] = field(default_factory=list)
    # (mat_name, list of face vertex index quads/tris, 1-based for OBJ)
    faces: List[Tuple[str, List[Tuple[int, ...]]]] = field(default_factory=list)
    current_mat: str = "Default"

    def set_mat(self, name: str) -> None:
        self.current_mat = name
        if not self.faces or self.faces[-1][0] != name:
            self.faces.append((name, []))

    def add_vertex(self, v: Vec3) -> int:
        self.vertices.append(v)
        return len(self.vertices)  # 1-based

    def add_face(self, indices: Sequence[int]) -> None:
        if not self.faces or self.faces[-1][0] != self.current_mat:
            self.faces.append((self.current_mat, []))
        self.faces[-1][1].append(tuple(indices))

    def add_box(self, center: Vec3, size: Vec3, rot_xyz_deg: Vec3 = (0.0, 0.0, 0.0)) -> None:
        hx, hy, hz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
        local = [
            (-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
            (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz),
        ]
        rx, ry, rz = [math.radians(a) for a in rot_xyz_deg]
        cx, cy, cz = math.cos(rx), math.cos(ry), math.cos(rz)
        sx, sy, sz = math.sin(rx), math.sin(ry), math.sin(rz)

        def rotate(p: Vec3) -> Vec3:
            x, y, z = p
            # ZYX
            x, y = x * cz - y * sz, x * sz + y * cz
            x, z = x * cy + z * sy, -x * sy + z * cy
            y, z = y * cx - z * sx, y * sx + z * cx
            return (x + center[0], y + center[1], z + center[2])

        ids = [self.add_vertex(rotate(p)) for p in local]
        quads = [
            (0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1),
            (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0),
        ]
        for q in quads:
            self.add_face([ids[i] for i in q])

    def add_cylinder(self, center: Vec3, radius: float, height: float,
                     segments: int = 12, rot_xyz_deg: Vec3 = (0.0, 0.0, 0.0)) -> None:
        # Default cylinder along Z; then rotate
        ring_bot: List[int] = []
        ring_top: List[int] = []
        rx, ry, rz = [math.radians(a) for a in rot_xyz_deg]
        cx, cy, cz = math.cos(rx), math.cos(ry), math.cos(rz)
        sx, sy, sz = math.sin(rx), math.sin(ry), math.sin(rz)

        def rotate(p: Vec3) -> Vec3:
            x, y, z = p
            x, y = x * cz - y * sz, x * sz + y * cz
            x, z = x * cy + z * sy, -x * sy + z * cy
            y, z = y * cx - z * sx, y * sx + z * cx
            return (x + center[0], y + center[1], z + center[2])

        hh = height * 0.5
        for i in range(segments):
            a = (i / segments) * math.tau
            x, y = math.cos(a) * radius, math.sin(a) * radius
            ring_bot.append(self.add_vertex(rotate((x, y, -hh))))
            ring_top.append(self.add_vertex(rotate((x, y, hh))))
        c_bot = self.add_vertex(rotate((0.0, 0.0, -hh)))
        c_top = self.add_vertex(rotate((0.0, 0.0, hh)))
        for i in range(segments):
            j = (i + 1) % segments
            self.add_face([ring_bot[i], ring_bot[j], ring_top[j], ring_top[i]])
            self.add_face([c_bot, ring_bot[j], ring_bot[i]])
            self.add_face([c_top, ring_top[i], ring_top[j]])

    def add_sphere(self, center: Vec3, radius: float, segments: int = 8, rings: int = 6) -> None:
        grid: List[List[int]] = []
        for r in range(rings + 1):
            row: List[int] = []
            v = r / rings
            phi = v * math.pi
            for s in range(segments):
                u = s / segments
                theta = u * math.tau
                x = center[0] + radius * math.sin(phi) * math.cos(theta)
                y = center[1] + radius * math.sin(phi) * math.sin(theta)
                z = center[2] + radius * math.cos(phi)
                row.append(self.add_vertex((x, y, z)))
            grid.append(row)
        for r in range(rings):
            for s in range(segments):
                s2 = (s + 1) % segments
                self.add_face([grid[r][s], grid[r][s2], grid[r + 1][s2], grid[r + 1][s]])

    def write_obj(self, obj_path: str, mtl_name: str) -> None:
        with open(obj_path, "w", encoding="utf-8") as f:
            f.write(f"# {self.name}\n")
            f.write(f"mtllib {mtl_name}\n")
            f.write(f"o {self.name}\n")
            for v in self.vertices:
                f.write(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}\n")
            for mat, faces in self.faces:
                if not faces:
                    continue
                f.write(f"usemtl {mat}\n")
                for face in faces:
                    f.write("f " + " ".join(str(i) for i in face) + "\n")


MATERIALS: Dict[str, Tuple[float, float, float]] = {
    "Defib_Red": (0.85, 0.12, 0.08),
    "Defib_Beige": (0.86, 0.78, 0.62),
    "Defib_Black": (0.05, 0.05, 0.055),
    "Defib_Dark": (0.12, 0.12, 0.13),
    "Defib_Cream": (0.92, 0.88, 0.78),
    "Defib_Screen": (0.12, 0.55, 0.22),
    "Defib_LED_G": (0.1, 0.9, 0.2),
    "Defib_LED_Y": (0.95, 0.8, 0.1),
    "Defib_LED_R": (0.95, 0.1, 0.08),
    "Defib_Blue": (0.15, 0.35, 0.85),
    "Defib_Yellow": (0.95, 0.82, 0.12),
    "Defib_White": (0.95, 0.95, 0.95),
    "Tube_Cream": (0.90, 0.86, 0.76),
    "Tube_Red": (0.82, 0.08, 0.06),
    "Tube_Dark": (0.10, 0.10, 0.11),
    "Tube_Silver": (0.72, 0.74, 0.76),
    "Tube_LED_G": (0.15, 0.95, 0.25),
    "Tube_LED_R": (0.95, 0.12, 0.08),
    "Tube_Gold": (0.85, 0.65, 0.2),
}


def write_mtl(path: str, names: Sequence[str]) -> None:
    with open(path, "w", encoding="utf-8") as f:
        f.write("# MiniVan defibrillator materials\n")
        for name in names:
            rgb = MATERIALS[name]
            f.write(f"newmtl {name}\n")
            f.write(f"Kd {rgb[0]:.4f} {rgb[1]:.4f} {rgb[2]:.4f}\n")
            f.write("Ka 0.05 0.05 0.05\n")
            f.write("Ks 0.15 0.15 0.15\n")
            f.write("Ns 40\n")
            f.write("d 1.0\n")
            f.write("illum 2\n\n")


def build_suitcase() -> MeshBuilder:
    m = MeshBuilder("Defib_Suitcase")

    m.set_mat("Defib_Red")
    m.add_box((0.0, 0.0, 0.07), (0.48, 0.34, 0.14))

    # Single interior bed — no second coplanar cream floor (z-fighting).
    m.set_mat("Defib_Beige")
    m.add_box((0.0, 0.0, 0.12), (0.44, 0.30, 0.02))

    m.set_mat("Defib_Dark")
    for x, y, z in [
        (-0.22, -0.15, 0.03), (0.22, -0.15, 0.03),
        (-0.22, 0.15, 0.03), (0.22, 0.15, 0.03),
        (-0.22, -0.15, 0.12), (0.22, -0.15, 0.12),
        (-0.22, 0.15, 0.12), (0.22, 0.15, 0.12),
    ]:
        m.add_box((x, y, z), (0.06, 0.05, 0.045))

    m.set_mat("Defib_Black")
    m.add_box((0.0, -0.19, 0.06), (0.16, 0.035, 0.04))
    m.add_box((-0.12, -0.185, 0.06), (0.04, 0.03, 0.035))
    m.add_box((0.12, -0.185, 0.06), (0.04, 0.03, 0.035))

    m.set_mat("Defib_Beige")
    m.add_box((0.0, -0.175, 0.145), (0.07, 0.01, 0.07))
    m.set_mat("Defib_Red")
    m.add_box((0.0, -0.178, 0.145), (0.055, 0.012, 0.018))
    m.add_box((0.0, -0.178, 0.145), (0.018, 0.012, 0.055))

    # Console clearly above tray.
    m.set_mat("Defib_Cream")
    m.add_box((0.0, 0.02, 0.155), (0.40, 0.22, 0.03))
    m.set_mat("Defib_Screen")
    m.add_box((-0.11, 0.04, 0.176), (0.14, 0.10, 0.012))
    m.set_mat("Defib_LED_G")
    for i in range(5):
        m.add_box((-0.15 + i * 0.022, -0.02, 0.174), (0.018, 0.012, 0.008))

    m.set_mat("Defib_Black")
    m.add_box((0.08, 0.0, 0.172), (0.09, 0.09, 0.02))
    m.set_mat("Defib_Red")
    m.add_cylinder((0.08, 0.0, 0.186), 0.032, 0.022, 12)
    m.set_mat("Defib_White")
    m.add_box((0.08, 0.0, 0.199), (0.018, 0.006, 0.01))
    m.add_box((0.075, 0.0, 0.199), (0.006, 0.006, 0.028))

    m.set_mat("Defib_Black")
    m.add_cylinder((0.02, 0.08, 0.179), 0.018, 0.02, 12)
    m.add_cylinder((0.08, 0.08, 0.179), 0.018, 0.02, 12)

    m.set_mat("Defib_LED_G")
    m.add_sphere((0.15, 0.07, 0.179), 0.008)
    m.set_mat("Defib_LED_Y")
    m.add_sphere((0.15, 0.04, 0.179), 0.008)
    m.set_mat("Defib_LED_R")
    m.add_sphere((0.15, 0.01, 0.179), 0.008)

    m.set_mat("Defib_Dark")
    for i in range(5):
        m.add_box((0.16, -0.04 + i * 0.012, 0.174), (0.035, 0.004, 0.006))

    m.set_mat("Defib_Dark")
    m.add_box((0.0, 0.16, 0.145), (0.46, 0.03, 0.03))

    # Lid hinged at back top edge, swung OPEN BACKWARD at the same 62° lean
    # (closed→open the other way = -118° around X about the hinge).
    hinge = (0.0, 0.17, 0.14)
    lid_angle = -118.0

    def add_lid_box(local_center: Vec3, size: Vec3, mat: str) -> None:
        # local_center is in closed-case space; rotate around hinge then place.
        rx = math.radians(lid_angle)
        c, s = math.cos(rx), math.sin(rx)
        rel = (
            local_center[0] - hinge[0],
            local_center[1] - hinge[1],
            local_center[2] - hinge[2],
        )
        y2 = rel[1] * c - rel[2] * s
        z2 = rel[1] * s + rel[2] * c
        world = (hinge[0] + rel[0], hinge[1] + y2, hinge[2] + z2)
        m.set_mat(mat)
        m.add_box(world, size, (lid_angle, 0.0, 0.0))

    add_lid_box((0.0, 0.0, 0.17), (0.48, 0.34, 0.06), "Defib_Red")
    add_lid_box((0.0, 0.0, 0.145), (0.44, 0.30, 0.02), "Defib_Cream")

    def docked_paddle(x: float, rim_mat: str) -> None:
        add_lid_box((x, 0.02, 0.155), (0.12, 0.09, 0.025), "Defib_Cream")
        add_lid_box((x, 0.02, 0.165), (0.11, 0.08, 0.01), "Defib_Beige")
        add_lid_box((x, 0.02, 0.148), (0.125, 0.095, 0.012), rim_mat)
        add_lid_box((x, -0.08, 0.155), (0.10, 0.04, 0.035), rim_mat)

    docked_paddle(-0.12, "Defib_Blue")
    docked_paddle(0.12, "Defib_Yellow")
    return m


def build_tube() -> MeshBuilder:
    m = MeshBuilder("Defib_Tube")

    m.set_mat("Tube_Cream")
    m.add_box((0.0, 0.0, 0.22), (0.14, 0.05, 0.11))
    m.set_mat("Tube_Red")
    m.add_box((0.0, -0.028, 0.22), (0.145, 0.02, 0.115))
    m.set_mat("Tube_Silver")
    m.add_box((0.0, -0.038, 0.22), (0.11, 0.012, 0.085))

    m.set_mat("Tube_Cream")
    m.add_box((0.0, 0.04, 0.08), (0.055, 0.055, 0.16), (18.0, 0.0, 0.0))
    m.set_mat("Tube_Dark")
    m.add_box((0.0, 0.055, 0.05), (0.058, 0.058, 0.09), (18.0, 0.0, 0.0))
    for i in range(6):
        m.add_box((0.0, 0.055 + i * 0.002, 0.02 + i * 0.012), (0.06, 0.006, 0.01), (18.0, 0.0, 0.0))

    m.set_mat("Tube_Red")
    m.add_box((0.0, 0.09, 0.07), (0.035, 0.02, 0.05), (18.0, 0.0, 0.0))
    m.add_box((0.0, 0.065, 0.155), (0.012, 0.004, 0.02))
    m.add_box((-0.004, 0.065, 0.155), (0.004, 0.004, 0.028))

    m.set_mat("Tube_LED_G")
    m.add_sphere((0.02, 0.07, 0.14), 0.006)
    m.set_mat("Tube_LED_R")
    m.add_sphere((-0.02, 0.07, 0.14), 0.006)

    # No cable / plug.
    return m


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    suitcase = build_suitcase()
    tube = build_tube()

    suitcase_mats = sorted({mat for mat, faces in suitcase.faces if faces})
    tube_mats = sorted({mat for mat, faces in tube.faces if faces})

    write_mtl(os.path.join(OUT_DIR, "MiniVan_Defib_Suitcase.mtl"), suitcase_mats)
    write_mtl(os.path.join(OUT_DIR, "MiniVan_Defib_Tube.mtl"), tube_mats)
    suitcase.write_obj(os.path.join(OUT_DIR, "MiniVan_Defib_Suitcase.obj"), "MiniVan_Defib_Suitcase.mtl")
    tube.write_obj(os.path.join(OUT_DIR, "MiniVan_Defib_Tube.obj"), "MiniVan_Defib_Tube.mtl")

    print(f"Wrote suitcase: {len(suitcase.vertices)} verts, 1 object")
    print(f"Wrote tube:     {len(tube.vertices)} verts, 1 object")
    print(f"Out dir: {OUT_DIR}")


if __name__ == "__main__":
    main()
