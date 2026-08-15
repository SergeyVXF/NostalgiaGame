"""
Coplanar-face audit: finds the surface pairs that make textures flicker.

  blender --background --python as_zcheck.py -- <file.blend> [more.blend ...]

Two faces z-fight when they are parallel, sit on (almost) the same plane and
overlap when projected onto it. Faces are bucketed by quantised plane so the
comparison stays local instead of O(faces^2), then only same-bucket pairs are
tested for overlap.
"""
from __future__ import annotations

import sys
from collections import defaultdict

import bpy

PLANE_TOL = 0.012      # metres: planes closer than this read as the same surface
NORMAL_TOL = 0.02      # normal quantisation
MIN_AREA = 0.004       # ignore slivers


def audit(obj) -> list:
    mesh = obj.data
    mw = obj.matrix_world
    buckets = defaultdict(list)

    for poly in mesh.polygons:
        if poly.area < MIN_AREA:
            continue
        n = (mw.to_3x3() @ poly.normal).normalized()
        c = mw @ poly.center
        # canonical sign so a face and its back-to-back twin land together
        sign = 1.0
        for comp in (n.x, n.y, n.z):
            if abs(comp) > 1e-4:
                sign = 1.0 if comp > 0 else -1.0
                break
        nc = n * sign
        d = nc.dot(c)
        key = (round(nc.x / NORMAL_TOL), round(nc.y / NORMAL_TOL),
               round(nc.z / NORMAL_TOL), round(d / PLANE_TOL))

        # project onto the two axes least aligned with the normal
        ax = max(range(3), key=lambda i: abs(nc[i]))
        iu, iv = [i for i in range(3) if i != ax]
        us, vs = [], []
        for vi in poly.vertices:
            p = mw @ mesh.vertices[vi].co
            us.append(p[iu])
            vs.append(p[iv])
        buckets[key].append((min(us), max(us), min(vs), max(vs), poly.area, c))

    hits = []
    for key, faces in buckets.items():
        if len(faces) < 2:
            continue
        for i in range(len(faces)):
            for j in range(i + 1, len(faces)):
                a, b = faces[i], faces[j]
                ou = min(a[1], b[1]) - max(a[0], b[0])
                ov = min(a[3], b[3]) - max(a[2], b[2])
                if ou > 0.02 and ov > 0.02:
                    hits.append((ou * ov, a[5]))
    hits.sort(key=lambda h: -h[0])
    return hits


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for path in args:
        bpy.ops.wm.open_mainfile(filepath=path)
        print("\n=== %s ===" % path)
        total = 0
        for obj in sorted(bpy.data.objects, key=lambda o: o.name):
            if obj.type != "MESH":
                continue
            hits = audit(obj)
            if not hits:
                continue
            total += len(hits)
            area = sum(h[0] for h in hits)
            worst = hits[0]
            print("  %-26s pairs=%-4d overlap=%6.2f m2  worst at (%.2f, %.2f, %.2f) %.2f m2"
                  % (obj.name, len(hits), area, worst[1].x, worst[1].y, worst[1].z, worst[0]))
        print("  TOTAL overlapping pairs:", total)


main()
