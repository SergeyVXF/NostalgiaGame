using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Procedural low-poly Zoro bandana: a closed head wrap, a fabric funnel that cinches at the
    /// back of the skull, a lumpy knot and two tapered ribbons draping behind.
    /// Built around the player capsule's head sphere so it hugs the skull.
    /// </summary>
    public static class MiniVanZoroBandanaMesh
    {
        /// <summary>Head sphere in bandana-local space (root sits at the head attach point).</summary>
        private static readonly Vector3 HeadCenter = new Vector3(0f, -0.38f, 0f);
        private const float HeadRadius = 0.53f;

        /// <summary>Direction the fabric is pulled to before it is tied off.</summary>
        private static readonly Vector3 CinchDirection = new Vector3(0.06f, 0.20f, -1f).normalized;

        private const float CinchLength = 0.23f;
        private const float KnotRadius = 0.105f;

        private const int CapRadialSegments = 18;
        private const int CapRings = 7;

        private static Vector3 CinchBase => HeadCenter + CinchDirection * (HeadRadius * 0.97f);
        private static Vector3 KnotCenter => CinchBase + CinchDirection * CinchLength;

        private static Mesh cached;

        public static Mesh Get()
        {
            if (cached == null)
            {
                cached = Build();
            }

            return cached;
        }

        private static Mesh Build()
        {
            List<Vector3> vertices = new List<Vector3>(768);
            List<int> triangles = new List<int>(2048);

            BuildCap(vertices, triangles);
            BuildCinch(vertices, triangles);
            BuildKnot(vertices, triangles);
            BuildTail(vertices, triangles, yawDegrees: -17f, length: 0.54f, sag: 0.46f, halfWidth: 0.105f, halfThickness: 0.025f, seed: 3);
            BuildTail(vertices, triangles, yawDegrees: 11f, length: 0.66f, sag: 0.58f, halfWidth: 0.09f, halfThickness: 0.022f, seed: 11);

            return CreateFlatShadedMesh(vertices, triangles, "MiniVan_ZoroBandana");
        }

        /// <summary>Closed cloth dome over the skull, bunched a little towards the knot side.</summary>
        private static void BuildCap(List<Vector3> vertices, List<int> triangles)
        {
            int baseIndex = vertices.Count;

            for (int ring = 0; ring <= CapRings; ring++)
            {
                float t = ring / (float)CapRings;
                float latitude = Mathf.Lerp(-10f, 78f, t) * Mathf.Deg2Rad;

                for (int segment = 0; segment < CapRadialSegments; segment++)
                {
                    float longitude = segment / (float)CapRadialSegments * Mathf.PI * 2f;
                    vertices.Add(CapPoint(longitude, latitude, t, ring, segment));
                }
            }

            int poleIndex = vertices.Count;
            vertices.Add(HeadCenter + new Vector3(0f, HeadRadius * 1.02f, -0.02f));

            for (int ring = 0; ring < CapRings; ring++)
            {
                for (int segment = 0; segment < CapRadialSegments; segment++)
                {
                    int next = (segment + 1) % CapRadialSegments;
                    int a = baseIndex + ring * CapRadialSegments + segment;
                    int b = baseIndex + ring * CapRadialSegments + next;
                    int c = baseIndex + (ring + 1) * CapRadialSegments + next;
                    int d = baseIndex + (ring + 1) * CapRadialSegments + segment;
                    AddQuad(vertices, triangles, a, b, c, d, HeadCenter);
                }
            }

            for (int segment = 0; segment < CapRadialSegments; segment++)
            {
                int next = (segment + 1) % CapRadialSegments;
                int a = baseIndex + CapRings * CapRadialSegments + segment;
                int b = baseIndex + CapRings * CapRadialSegments + next;
                AddTriangle(vertices, triangles, a, b, poleIndex, HeadCenter);
            }

            BuildCapRim(vertices, triangles, baseIndex);
        }

        /// <summary>Thin band folded under the bottom edge so the cloth is not paper thin.</summary>
        private static void BuildCapRim(List<Vector3> vertices, List<int> triangles, int outerRingStart)
        {
            int innerStart = vertices.Count;
            for (int segment = 0; segment < CapRadialSegments; segment++)
            {
                Vector3 outer = vertices[outerRingStart + segment];
                Vector3 toCenter = HeadCenter - outer;
                toCenter.y = 0f;
                Vector3 inward = toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized : Vector3.zero;
                vertices.Add(outer + inward * 0.05f + Vector3.up * 0.05f);
            }

            Vector3 reference = HeadCenter + Vector3.up * 0.25f;
            for (int segment = 0; segment < CapRadialSegments; segment++)
            {
                int next = (segment + 1) % CapRadialSegments;
                AddQuad(vertices, triangles,
                    innerStart + segment,
                    innerStart + next,
                    outerRingStart + next,
                    outerRingStart + segment,
                    reference);
            }
        }

        private static Vector3 CapPoint(float longitude, float latitude, float ringT, int ring, int segment)
        {
            // longitude 0 faces +Z (forehead), PI faces -Z (knot side).
            Vector3 unit = new Vector3(
                Mathf.Cos(latitude) * Mathf.Sin(longitude),
                Mathf.Sin(latitude),
                Mathf.Cos(latitude) * Mathf.Cos(longitude));

            Vector3 point = HeadCenter + new Vector3(
                unit.x * HeadRadius * 1.04f,
                unit.y * HeadRadius * 0.99f,
                unit.z * HeadRadius);

            // Cloth bunches up where it is pulled towards the knot.
            float angleFromBack = Mathf.Abs(Mathf.DeltaAngle(longitude * Mathf.Rad2Deg, 180f));
            float towardsKnot = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(angleFromBack / 95f));
            float lowRings = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((ringT - 0.15f) / 0.85f));
            point += CinchDirection * (towardsKnot * lowRings * 0.06f);

            float jitter = Mathf.Lerp(0.016f, 0.008f, ringT);
            point += new Vector3(
                Hash(ring * 131 + segment * 17 + 1) - 0.5f,
                Hash(ring * 71 + segment * 37 + 2) - 0.5f,
                Hash(ring * 197 + segment * 53 + 3) - 0.5f) * jitter * 2f;

            return point;
        }

        /// <summary>Funnel of gathered cloth from the back of the dome into the knot.</summary>
        private static void BuildCinch(List<Vector3> vertices, List<int> triangles)
        {
            const int radial = 10;
            const int rings = 4;
            float[] radii = { 0.20f, 0.16f, 0.105f, 0.058f };
            float[] distances = { -0.03f, 0.07f, 0.15f, CinchLength - 0.01f };

            Vector3 axis = CinchDirection;
            Vector3 right = Vector3.Cross(Vector3.up, axis).normalized;
            Vector3 up = Vector3.Cross(axis, right).normalized;

            int baseIndex = vertices.Count;
            for (int ring = 0; ring < rings; ring++)
            {
                Vector3 center = CinchBase + axis * distances[ring];
                for (int segment = 0; segment < radial; segment++)
                {
                    float angle = segment / (float)radial * Mathf.PI * 2f;
                    // Folds: alternating segments pinch inwards like pleated cloth.
                    float pleat = 1f - (segment % 2 == 0 ? 0.14f : 0f) * Mathf.Clamp01(ring / 1.5f);
                    float radius = radii[ring] * pleat * (0.94f + Hash(ring * 41 + segment * 13) * 0.12f);
                    vertices.Add(center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius);
                }
            }

            for (int ring = 0; ring < rings - 1; ring++)
            {
                Vector3 reference = CinchBase + axis * distances[ring];
                for (int segment = 0; segment < radial; segment++)
                {
                    int next = (segment + 1) % radial;
                    AddQuad(vertices, triangles,
                        baseIndex + ring * radial + segment,
                        baseIndex + ring * radial + next,
                        baseIndex + (ring + 1) * radial + next,
                        baseIndex + (ring + 1) * radial + segment,
                        reference);
                }
            }
        }

        private static void BuildKnot(List<Vector3> vertices, List<int> triangles)
        {
            const int radial = 9;
            const int rings = 4;
            int baseIndex = vertices.Count;
            Vector3 knot = KnotCenter;
            Vector3 scale = new Vector3(KnotRadius * 1.15f, KnotRadius, KnotRadius * 1.25f);

            for (int ring = 0; ring < rings; ring++)
            {
                float latitude = Mathf.Lerp(-62f, 62f, ring / (float)(rings - 1)) * Mathf.Deg2Rad;
                for (int segment = 0; segment < radial; segment++)
                {
                    float longitude = segment / (float)radial * Mathf.PI * 2f;
                    Vector3 unit = new Vector3(
                        Mathf.Cos(latitude) * Mathf.Sin(longitude),
                        Mathf.Sin(latitude),
                        Mathf.Cos(latitude) * Mathf.Cos(longitude));

                    float lumpy = 0.88f + Hash(ring * 29 + segment * 7) * 0.3f;
                    vertices.Add(knot + Vector3.Scale(unit, scale) * lumpy);
                }
            }

            int bottomPole = vertices.Count;
            vertices.Add(knot + new Vector3(0f, -scale.y * 1.05f, 0f));
            int topPole = vertices.Count;
            vertices.Add(knot + new Vector3(0f, scale.y * 1.05f, 0f));

            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int segment = 0; segment < radial; segment++)
                {
                    int next = (segment + 1) % radial;
                    AddQuad(vertices, triangles,
                        baseIndex + ring * radial + segment,
                        baseIndex + ring * radial + next,
                        baseIndex + (ring + 1) * radial + next,
                        baseIndex + (ring + 1) * radial + segment,
                        knot);
                }
            }

            int lastRing = baseIndex + (rings - 1) * radial;
            for (int segment = 0; segment < radial; segment++)
            {
                int next = (segment + 1) % radial;
                AddTriangle(vertices, triangles, baseIndex + segment, baseIndex + next, bottomPole, knot);
                AddTriangle(vertices, triangles, lastRing + segment, lastRing + next, topPole, knot);
            }
        }

        /// <summary>Tapered ribbon with a folded (diamond) cross section draping from the knot.</summary>
        private static void BuildTail(List<Vector3> vertices, List<int> triangles,
            float yawDegrees, float length, float sag, float halfWidth, float halfThickness, int seed)
        {
            const int segments = 8;
            Quaternion yaw = Quaternion.Euler(0f, yawDegrees, 0f);
            Vector3 back = yaw * Vector3.back;
            Vector3 side = yaw * Vector3.right;
            Vector3 start = KnotCenter + back * (KnotRadius * 0.6f);

            Vector3[] spine = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                spine[i] = start
                           + back * (length * Mathf.Sqrt(t))
                           + Vector3.down * (sag * t * t)
                           + side * (Mathf.Sin(t * Mathf.PI * 0.8f) * 0.05f);
            }

            int ringStart = vertices.Count;
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;
                Vector3 tangent = (spine[i + 1] - spine[i]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                if (right.sqrMagnitude < 0.001f)
                {
                    right = side;
                }

                Vector3 up = Vector3.Cross(tangent, right).normalized;

                float widthProfile = Mathf.Lerp(0.82f, 1.05f, Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI * 0.5f));
                widthProfile *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.5f) / 0.5f)) * 0.6f;
                float w = halfWidth * widthProfile * (0.94f + Hash(seed * 13 + i) * 0.12f);
                float th = halfThickness * widthProfile;

                Vector3 center = spine[i];
                vertices.Add(center + right * w);
                vertices.Add(center + up * th);
                vertices.Add(center - right * w);
                vertices.Add(center - up * th);
            }

            int tipIndex = vertices.Count;
            vertices.Add(spine[segments]);

            for (int i = 0; i < segments - 1; i++)
            {
                int a = ringStart + i * 4;
                int b = ringStart + (i + 1) * 4;
                Vector3 reference = spine[i];
                for (int k = 0; k < 4; k++)
                {
                    int k2 = (k + 1) % 4;
                    AddQuad(vertices, triangles, a + k, a + k2, b + k2, b + k, reference);
                }
            }

            int last = ringStart + (segments - 1) * 4;
            for (int k = 0; k < 4; k++)
            {
                int k2 = (k + 1) % 4;
                AddTriangle(vertices, triangles, last + k, last + k2, tipIndex, spine[segments - 1]);
            }
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, int a, int b, int c, int d, Vector3 reference)
        {
            AddTriangle(vertices, triangles, a, b, c, reference);
            AddTriangle(vertices, triangles, a, c, d, reference);
        }

        /// <summary>Adds a triangle, flipping it when its face normal points back at the reference point.</summary>
        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c, Vector3 reference)
        {
            if (a == b || b == c || a == c)
            {
                return;
            }

            Vector3 va = vertices[a];
            Vector3 vb = vertices[b];
            Vector3 vc = vertices[c];
            Vector3 normal = Vector3.Cross(vb - va, vc - va);
            if (normal.sqrMagnitude < 1e-10f)
            {
                return;
            }

            Vector3 outward = (va + vb + vc) / 3f - reference;
            if (Vector3.Dot(normal, outward) < 0f)
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                return;
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static Mesh CreateFlatShadedMesh(List<Vector3> vertices, List<int> triangles, string name)
        {
            Vector3[] flatVertices = new Vector3[triangles.Count];
            int[] flatTriangles = new int[triangles.Count];
            for (int i = 0; i < triangles.Count; i++)
            {
                flatVertices[i] = vertices[triangles[i]];
                flatTriangles[i] = i;
            }

            Mesh mesh = new Mesh { name = name };
            mesh.indexFormat = flatVertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = flatVertices;
            mesh.triangles = flatTriangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Hash(int value)
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
