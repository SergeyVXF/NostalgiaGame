using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Small procedural mesh kit for flat shaded low-poly props.
    /// Triangles are collected per material group and expanded into submeshes on Build().
    /// </summary>
    public sealed class MiniVanMeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<List<int>> groups = new List<List<int>>();

        public int GroupCount => groups.Count;

        public int NewGroup()
        {
            groups.Add(new List<int>(512));
            return groups.Count - 1;
        }

        public int AddVertex(Vector3 point)
        {
            vertices.Add(point);
            return vertices.Count - 1;
        }

        /// <summary>Triangle with explicit winding.</summary>
        public void Tri(int group, int a, int b, int c)
        {
            if (a == b || b == c || a == c)
            {
                return;
            }

            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.sqrMagnitude < 1e-12f)
            {
                return;
            }

            List<int> list = groups[group];
            list.Add(a);
            list.Add(b);
            list.Add(c);
        }

        public void Quad(int group, int a, int b, int c, int d)
        {
            Tri(group, a, b, c);
            Tri(group, a, c, d);
        }

        /// <summary>Triangle that is flipped when it would face the reference point.</summary>
        public void TriOut(int group, int a, int b, int c, Vector3 reference)
        {
            if (a == b || b == c || a == c)
            {
                return;
            }

            Vector3 va = vertices[a];
            Vector3 vb = vertices[b];
            Vector3 vc = vertices[c];
            Vector3 normal = Vector3.Cross(vb - va, vc - va);
            if (normal.sqrMagnitude < 1e-12f)
            {
                return;
            }

            if (Vector3.Dot(normal, (va + vb + vc) / 3f - reference) < 0f)
            {
                Tri(group, a, c, b);
                return;
            }

            Tri(group, a, b, c);
        }

        public void QuadOut(int group, int a, int b, int c, int d, Vector3 reference)
        {
            TriOut(group, a, b, c, reference);
            TriOut(group, a, c, d, reference);
        }

        /// <summary>Appends an existing mesh (all of it into one group).</summary>
        public void AddMesh(int group, Mesh mesh, Matrix4x4 transform)
        {
            if (mesh == null)
            {
                return;
            }

            Vector3[] sourceVertices = mesh.vertices;
            int[] sourceTriangles = mesh.triangles;
            int offset = vertices.Count;
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                vertices.Add(transform.MultiplyPoint3x4(sourceVertices[i]));
            }

            for (int i = 0; i < sourceTriangles.Length; i += 3)
            {
                Tri(group, offset + sourceTriangles[i], offset + sourceTriangles[i + 1], offset + sourceTriangles[i + 2]);
            }
        }

        /// <summary>Spherical cap. Latitudes are degrees, -90 is the bottom pole.</summary>
        public int[] Dome(int group, Vector3 center, Vector3 radii, int segments, int rings,
            float startLatitude, float endLatitude, float jitter = 0f, int seed = 0,
            System.Func<float, int> groupPicker = null)
        {
            int[] bottomRing = new int[segments];
            int[] previous = null;
            bool closesAtPole = endLatitude > 88f;

            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float latitude = Mathf.Lerp(startLatitude, closesAtPole ? 80f : endLatitude, t) * Mathf.Deg2Rad;
                int[] current = new int[segments];

                for (int segment = 0; segment < segments; segment++)
                {
                    float longitude = segment / (float)segments * Mathf.PI * 2f;
                    Vector3 unit = new Vector3(
                        Mathf.Cos(latitude) * Mathf.Sin(longitude),
                        Mathf.Sin(latitude),
                        Mathf.Cos(latitude) * Mathf.Cos(longitude));

                    Vector3 point = center + Vector3.Scale(unit, radii);
                    if (jitter > 0f)
                    {
                        point += Jitter(seed + ring * 131 + segment * 17) * jitter;
                    }

                    current[segment] = AddVertex(point);
                }

                if (ring == 0)
                {
                    bottomRing = current;
                }
                else
                {
                    for (int segment = 0; segment < segments; segment++)
                    {
                        int next = (segment + 1) % segments;
                        float longitude = (segment + 0.5f) / segments * 360f;
                        int target = groupPicker != null ? groupPicker(longitude) : group;
                        QuadOut(target, previous[segment], previous[next], current[next], current[segment], center);
                    }
                }

                previous = current;
            }

            if (closesAtPole)
            {
                int pole = AddVertex(center + new Vector3(0f, radii.y, 0f));
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    float longitude = (segment + 0.5f) / segments * 360f;
                    int target = groupPicker != null ? groupPicker(longitude) : group;
                    TriOut(target, previous[segment], previous[next], pole, center);
                }
            }

            return bottomRing;
        }

        /// <summary>Flat disc, used to close open hat bottoms.</summary>
        public void Disc(int group, Vector3 center, float radius, int segments, bool faceUp)
        {
            int middle = AddVertex(center);
            int[] rim = new int[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                rim[i] = AddVertex(center + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                if (faceUp)
                {
                    Tri(group, middle, rim[i], rim[next]);
                }
                else
                {
                    Tri(group, middle, rim[next], rim[i]);
                }
            }
        }

        /// <summary>
        /// Hat brim: a ring with thickness whose outer edge height can vary per angle
        /// (that is what turns a plain brim into a tricorn).
        /// </summary>
        public void Brim(int group, Vector3 center, float innerRadius, float outerRadius, float thickness,
            int segments, System.Func<float, float> outerHeight, System.Func<float, float> innerHeight = null,
            int bottomGroup = -1)
        {
            if (bottomGroup < 0)
            {
                bottomGroup = group;
            }

            int[] innerTop = new int[segments];
            int[] outerTop = new int[segments];
            int[] innerBottom = new int[segments];
            int[] outerBottom = new int[segments];

            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float degrees = t * 360f;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                float inner = innerHeight != null ? innerHeight(degrees) : 0f;
                float outer = outerHeight != null ? outerHeight(degrees) : 0f;

                Vector3 innerPoint = center + direction * innerRadius + Vector3.up * inner;
                Vector3 outerPoint = center + direction * outerRadius + Vector3.up * outer;

                innerTop[i] = AddVertex(innerPoint);
                outerTop[i] = AddVertex(outerPoint);
                innerBottom[i] = AddVertex(innerPoint - Vector3.up * thickness);
                outerBottom[i] = AddVertex(outerPoint - Vector3.up * thickness);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                Quad(group, innerTop[i], outerTop[i], outerTop[next], innerTop[next]);
                Quad(bottomGroup, innerBottom[i], innerBottom[next], outerBottom[next], outerBottom[i]);
                Quad(group, outerTop[i], outerBottom[i], outerBottom[next], outerTop[next]);
                Quad(bottomGroup, innerTop[i], innerTop[next], innerBottom[next], innerBottom[i]);
            }
        }

        /// <summary>Open arc version of <see cref="Brim"/>, used for caps with a front visor only.</summary>
        public void BrimArc(int group, Vector3 center, float innerRadius, float outerRadius, float thickness,
            int segments, float startDegrees, float endDegrees,
            System.Func<float, float> outerHeight, System.Func<float, float> innerHeight = null, int bottomGroup = -1,
            System.Func<float, float> outerRadiusScale = null)
        {
            if (bottomGroup < 0)
            {
                bottomGroup = group;
            }

            int count = segments + 1;
            int[] innerTop = new int[count];
            int[] outerTop = new int[count];
            int[] innerBottom = new int[count];
            int[] outerBottom = new int[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)segments;
                float degrees = Mathf.Lerp(startDegrees, endDegrees, t);
                float angle = degrees * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                float inner = innerHeight != null ? innerHeight(degrees) : 0f;
                float outer = outerHeight != null ? outerHeight(degrees) : 0f;

                float scale = outerRadiusScale != null ? outerRadiusScale(degrees) : 1f;
                Vector3 innerPoint = center + direction * innerRadius + Vector3.up * inner;
                Vector3 outerPoint = center + direction * Mathf.Max(innerRadius + 0.01f, outerRadius * scale) + Vector3.up * outer;

                innerTop[i] = AddVertex(innerPoint);
                outerTop[i] = AddVertex(outerPoint);
                innerBottom[i] = AddVertex(innerPoint - Vector3.up * thickness);
                outerBottom[i] = AddVertex(outerPoint - Vector3.up * thickness);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = i + 1;
                Quad(group, innerTop[i], outerTop[i], outerTop[next], innerTop[next]);
                Quad(bottomGroup, innerBottom[i], innerBottom[next], outerBottom[next], outerBottom[i]);
                Quad(group, outerTop[i], outerBottom[i], outerBottom[next], outerTop[next]);
                Quad(bottomGroup, innerTop[i], innerTop[next], innerBottom[next], innerBottom[i]);
            }

            int last = count - 1;
            Vector3 startReference = (vertices[innerTop[1]] + vertices[outerTop[1]]) * 0.5f;
            Vector3 endReference = (vertices[innerTop[last - 1]] + vertices[outerTop[last - 1]]) * 0.5f;
            QuadOut(group, innerTop[0], outerTop[0], outerBottom[0], innerBottom[0], startReference);
            QuadOut(group, innerTop[last], outerTop[last], outerBottom[last], innerBottom[last], endReference);
        }

        /// <summary>Round tube along a spine. A zero final radius produces a spike.</summary>
        public void Tube(int group, IList<Vector3> spine, IList<float> radii, int sides,
            bool capStart = true, bool capEnd = true)
        {
            if (spine.Count < 2)
            {
                return;
            }

            Vector3 tangent = (spine[1] - spine[0]).normalized;
            Vector3 normal = Vector3.Cross(tangent, Vector3.up);
            if (normal.sqrMagnitude < 0.001f)
            {
                normal = Vector3.Cross(tangent, Vector3.forward);
            }

            normal.Normalize();

            int[] previous = null;
            Vector3 previousCenter = spine[0];
            bool tip = radii[radii.Count - 1] <= 0.0005f;
            int rings = tip ? spine.Count - 1 : spine.Count;

            for (int i = 0; i < rings; i++)
            {
                Vector3 forward = i < spine.Count - 1 ? spine[i + 1] - spine[i] : spine[i] - spine[i - 1];
                forward.Normalize();
                normal = (normal - forward * Vector3.Dot(normal, forward)).normalized;
                Vector3 binormal = Vector3.Cross(forward, normal);

                int[] current = new int[sides];
                for (int s = 0; s < sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2f;
                    Vector3 offset = (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radii[i];
                    current[s] = AddVertex(spine[i] + offset);
                }

                if (previous != null)
                {
                    for (int s = 0; s < sides; s++)
                    {
                        int next = (s + 1) % sides;
                        QuadOut(group, previous[s], previous[next], current[next], current[s], (previousCenter + spine[i]) * 0.5f);
                    }
                }
                else if (capStart)
                {
                    int center = AddVertex(spine[i]);
                    for (int s = 0; s < sides; s++)
                    {
                        int next = (s + 1) % sides;
                        TriOut(group, current[s], current[next], center, spine[i] + forward * 0.05f);
                    }
                }

                previous = current;
                previousCenter = spine[i];
            }

            if (tip)
            {
                int tipIndex = AddVertex(spine[spine.Count - 1]);
                for (int s = 0; s < sides; s++)
                {
                    int next = (s + 1) % sides;
                    TriOut(group, previous[s], previous[next], tipIndex, previousCenter);
                }
            }
            else if (capEnd)
            {
                Vector3 last = spine[spine.Count - 1];
                int center = AddVertex(last);
                for (int s = 0; s < sides; s++)
                {
                    int next = (s + 1) % sides;
                    TriOut(group, previous[s], previous[next], center, previousCenter);
                }
            }
        }

        /// <summary>Flat ribbon along a spine (helmet crest, cloth straps).</summary>
        public void Strip(int group, IList<Vector3> spine, IList<float> halfWidths, float halfThickness, Vector3 widthAxis)
        {
            int count = spine.Count;
            int[][] rings = new int[count][];

            for (int i = 0; i < count; i++)
            {
                Vector3 forward = i < count - 1 ? spine[i + 1] - spine[i] : spine[i] - spine[i - 1];
                forward.Normalize();
                Vector3 side = Vector3.Cross(forward, widthAxis).sqrMagnitude > 0.001f
                    ? Vector3.Cross(forward, widthAxis).normalized
                    : widthAxis;
                Vector3 up = Vector3.Cross(side, forward).normalized;

                float w = halfWidths[i];
                rings[i] = new[]
                {
                    AddVertex(spine[i] + side * w + up * halfThickness),
                    AddVertex(spine[i] - side * w + up * halfThickness),
                    AddVertex(spine[i] - side * w - up * halfThickness),
                    AddVertex(spine[i] + side * w - up * halfThickness)
                };
            }

            for (int i = 0; i < count - 1; i++)
            {
                for (int k = 0; k < 4; k++)
                {
                    int k2 = (k + 1) % 4;
                    QuadOut(group, rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k], (spine[i] + spine[i + 1]) * 0.5f);
                }
            }

            QuadOut(group, rings[0][0], rings[0][1], rings[0][2], rings[0][3], spine[1]);
            int lastRing = count - 1;
            QuadOut(group, rings[lastRing][0], rings[lastRing][1], rings[lastRing][2], rings[lastRing][3], spine[lastRing - 1]);
        }

        public void Box(int group, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            int[] v = new int[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? -h.x : h.x,
                    (i & 2) == 0 ? -h.y : h.y,
                    (i & 4) == 0 ? -h.z : h.z);
                v[i] = AddVertex(center + rotation * corner);
            }

            QuadOut(group, v[0], v[1], v[3], v[2], center);
            QuadOut(group, v[4], v[5], v[7], v[6], center);
            QuadOut(group, v[0], v[1], v[5], v[4], center);
            QuadOut(group, v[2], v[3], v[7], v[6], center);
            QuadOut(group, v[0], v[2], v[6], v[4], center);
            QuadOut(group, v[1], v[3], v[7], v[5], center);
        }

        public void Torus(int group, Vector3 center, float majorRadius, float minorRadius,
            int majorSegments, int minorSegments, float verticalSquash = 1f)
        {
            int[][] rings = new int[majorSegments][];
            Vector3[] ringCenters = new Vector3[majorSegments];

            for (int i = 0; i < majorSegments; i++)
            {
                float angle = i / (float)majorSegments * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 ringCenter = center + direction * majorRadius;
                ringCenters[i] = ringCenter;
                rings[i] = new int[minorSegments];

                for (int j = 0; j < minorSegments; j++)
                {
                    float minorAngle = j / (float)minorSegments * Mathf.PI * 2f;
                    Vector3 offset = direction * (Mathf.Cos(minorAngle) * minorRadius)
                                     + Vector3.up * (Mathf.Sin(minorAngle) * minorRadius * verticalSquash);
                    rings[i][j] = AddVertex(ringCenter + offset);
                }
            }

            for (int i = 0; i < majorSegments; i++)
            {
                int nextRing = (i + 1) % majorSegments;
                for (int j = 0; j < minorSegments; j++)
                {
                    int nextMinor = (j + 1) % minorSegments;
                    QuadOut(group, rings[i][j], rings[i][nextMinor], rings[nextRing][nextMinor], rings[nextRing][j], ringCenters[i]);
                }
            }
        }

        /// <summary>Irregular patch lying on an ellipsoid surface, used for fur spots.</summary>
        public void SpherePatch(int group, Vector3 center, Vector3 radii, Vector3 direction,
            float angularRadiusDegrees, int segments, float lift, int seed)
        {
            direction = direction.normalized;
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.Cross(direction, Vector3.forward);
            }

            side.Normalize();
            Vector3 other = Vector3.Cross(direction, side);

            int middle = AddVertex(center + Vector3.Scale(direction, radii) * (1f + lift));
            int[] rim = new int[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float spread = angularRadiusDegrees * (0.65f + Hash(seed * 31 + i) * 0.7f) * Mathf.Deg2Rad;
                Vector3 unit = (direction * Mathf.Cos(spread)
                                + (side * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * Mathf.Sin(spread)).normalized;
                rim[i] = AddVertex(center + Vector3.Scale(unit, radii) * (1f + lift * 0.85f));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                TriOut(group, middle, rim[i], rim[next], center);
            }
        }

        /// <summary>Irregular patch laid on any curved surface (spots on a rolled brim).</summary>
        public void CurvedPatch(int group, Vector3 position, Vector3 normal, float radius,
            float curvatureRadius, int segments, int seed)
        {
            normal = normal.normalized;
            Vector3 side = Vector3.Cross(normal, Vector3.up);
            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.Cross(normal, Vector3.forward);
            }

            side.Normalize();
            Vector3 other = Vector3.Cross(normal, side);
            Vector3 inside = position - normal * curvatureRadius;

            int middle = AddVertex(position + normal * 0.004f);
            int[] rim = new int[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float r = radius * (0.65f + Hash(seed * 37 + i) * 0.7f);
                Vector3 flat = position + (side * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * r;
                float drop = curvatureRadius > 0.001f ? r * r / (2f * curvatureRadius) : 0f;
                rim[i] = AddVertex(flat + normal * (0.004f - drop));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                TriOut(group, middle, rim[i], rim[next], inside);
            }
        }

        public Mesh Build(string name)
        {
            int total = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                total += groups[i].Count;
            }

            Vector3[] flatVertices = new Vector3[total];
            int[][] submeshes = new int[groups.Count][];
            int cursor = 0;

            for (int g = 0; g < groups.Count; g++)
            {
                List<int> list = groups[g];
                int[] indices = new int[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    flatVertices[cursor] = vertices[list[i]];
                    indices[i] = cursor;
                    cursor++;
                }

                submeshes[g] = indices;
            }

            Mesh mesh = new Mesh { name = name };
            mesh.indexFormat = total > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = flatVertices;
            mesh.subMeshCount = groups.Count;
            for (int g = 0; g < groups.Count; g++)
            {
                mesh.SetTriangles(submeshes[g], g, false);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector3 Jitter(int seed)
        {
            return new Vector3(Hash(seed * 3 + 1) - 0.5f, Hash(seed * 3 + 2) - 0.5f, Hash(seed * 3 + 3) - 0.5f) * 2f;
        }

        public static float Hash(int value)
        {
            unchecked
            {
                int x = value * 374761393 + 668265263;
                x = (x ^ (x >> 13)) * 1274126177;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
