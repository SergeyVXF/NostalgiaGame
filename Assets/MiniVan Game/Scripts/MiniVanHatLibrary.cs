using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    public struct MiniVanHatMaterial
    {
        public string Name;
        public Color Color;
        public float Smoothness;
        public float Metallic;
    }

    public struct MiniVanHatModel
    {
        public Mesh Mesh;
        public MiniVanHatMaterial[] Materials;

        public bool IsValid => Mesh != null && Materials != null && Materials.Length > 0;
    }

    /// <summary>
    /// Procedural low-poly headwear. Every model is built in head space: the player's head
    /// sphere sits at <see cref="HeadCenter"/> with <see cref="HeadRadius"/>, and the mesh
    /// origin is the head attach point, so a hat drops straight onto the character.
    /// </summary>
    public static class MiniVanHatLibrary
    {
        public static readonly Vector3 HeadCenter = new Vector3(0f, -0.38f, 0f);
        public const float HeadRadius = 0.52f;

        private static readonly Dictionary<MiniVanInventoryItem, MiniVanHatModel> cache =
            new Dictionary<MiniVanInventoryItem, MiniVanHatModel>();

        public static bool HasModel(MiniVanInventoryItem item)
        {
            switch (item)
            {
                case MiniVanInventoryItem.StrawHat:
                case MiniVanInventoryItem.ChopperHat:
                case MiniVanInventoryItem.AshCap:
                case MiniVanInventoryItem.NarutoHeadband:
                case MiniVanInventoryItem.LawHat:
                case MiniVanInventoryItem.GokuHair:
                case MiniVanInventoryItem.SuperSaiyanHair:
                case MiniVanInventoryItem.MarioCap:
                case MiniVanInventoryItem.VikingHelmet:
                case MiniVanInventoryItem.PirateTricorn:
                    return true;
                default:
                    return false;
            }
        }

        public static void ClearCache()
        {
            cache.Clear();
        }

        public static MiniVanHatModel Get(MiniVanInventoryItem item)
        {
            if (cache.TryGetValue(item, out MiniVanHatModel cached) && cached.Mesh != null)
            {
                return cached;
            }

            MiniVanHatModel model = Create(item);
            cache[item] = model;
            return model;
        }

        private static MiniVanHatModel Create(MiniVanInventoryItem item)
        {
            MiniVanMeshBuilder builder = new MiniVanMeshBuilder();
            List<MiniVanHatMaterial> palette = new List<MiniVanHatMaterial>();

            switch (item)
            {
                case MiniVanInventoryItem.StrawHat: BuildStrawHat(builder, palette); break;
                case MiniVanInventoryItem.ChopperHat: BuildChopperHat(builder, palette); break;
                case MiniVanInventoryItem.AshCap: BuildAshCap(builder, palette); break;
                case MiniVanInventoryItem.NarutoHeadband: BuildNarutoHeadband(builder, palette); break;
                case MiniVanInventoryItem.LawHat: BuildLawHat(builder, palette); break;
                case MiniVanInventoryItem.GokuHair: BuildSpikyHair(builder, palette, false); break;
                case MiniVanInventoryItem.SuperSaiyanHair: BuildSpikyHair(builder, palette, true); break;
                case MiniVanInventoryItem.MarioCap: BuildMarioCap(builder, palette); break;
                case MiniVanInventoryItem.VikingHelmet: BuildVikingHelmet(builder, palette); break;
                case MiniVanInventoryItem.PirateTricorn: BuildPirateTricorn(builder, palette); break;
                default: return default;
            }

            return new MiniVanHatModel
            {
                Mesh = builder.Build("MiniVan_" + item),
                Materials = palette.ToArray()
            };
        }

        private static int Group(MiniVanMeshBuilder builder, List<MiniVanHatMaterial> palette,
            string name, Color color, float smoothness = 0.14f, float metallic = 0f)
        {
            palette.Add(new MiniVanHatMaterial
            {
                Name = name,
                Color = color,
                Smoothness = smoothness,
                Metallic = metallic
            });

            return builder.NewGroup();
        }

        /// <summary>
        /// Solid baseball-cap visor that butts against the dome instead of floating as a ring arc.
        /// </summary>
        private static void BuildCapVisor(MiniVanMeshBuilder b, int topGroup, int bottomGroup,
            Vector3 hinge, float halfWidth, float depth, float thickness, int segments)
        {
            int count = segments + 1;
            int[] outerTop = new int[count];
            int[] outerBottom = new int[count];
            int[] innerTop = new int[count];
            int[] innerBottom = new int[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)segments;
                float x = Mathf.Lerp(-halfWidth, halfWidth, t);
                float normalized = x / Mathf.Max(0.001f, halfWidth);
                float z = depth * Mathf.Sqrt(Mathf.Max(0f, 1f - normalized * normalized));
                float yDrop = 0.028f * normalized * normalized;

                Vector3 outer = hinge + new Vector3(x, -yDrop, z);
                // Curve the hinge edge along the dome so the sides don't leave a gap.
                float innerZ = -0.04f - 0.10f * (1f - normalized * normalized);
                Vector3 inner = hinge + new Vector3(x * 0.88f, 0.025f - yDrop * 0.15f, innerZ);

                outerTop[i] = b.AddVertex(outer);
                outerBottom[i] = b.AddVertex(outer + Vector3.down * thickness);
                innerTop[i] = b.AddVertex(inner);
                innerBottom[i] = b.AddVertex(inner + Vector3.down * thickness);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = i + 1;
                // Top (+Y), bottom (-Y), front rim (outward).
                b.Quad(topGroup, innerTop[i], outerTop[i], outerTop[next], innerTop[next]);
                b.Quad(bottomGroup, innerBottom[i], innerBottom[next], outerBottom[next], outerBottom[i]);
                b.Quad(topGroup, outerTop[i], outerBottom[i], outerBottom[next], outerTop[next]);
            }

            int last = count - 1;
            b.Quad(topGroup, innerTop[0], outerTop[0], outerBottom[0], innerBottom[0]);
            b.Quad(topGroup, outerTop[last], innerTop[last], innerBottom[last], outerBottom[last]);
            b.Quad(bottomGroup, innerTop[0], innerBottom[0], innerBottom[last], innerTop[last]);
        }

        // ---------------------------------------------------------------- straw hat

        private static void BuildStrawHat(MiniVanMeshBuilder b, List<MiniVanHatMaterial> p)
        {
            int straw = Group(b, p, "Straw", new Color(0.91f, 0.74f, 0.36f));
            int strawShade = Group(b, p, "StrawShade", new Color(0.64f, 0.48f, 0.21f));
            int band = Group(b, p, "Band", new Color(0.78f, 0.13f, 0.15f));

            Vector3 brimCenter = new Vector3(0f, -0.30f, 0f);
            b.Dome(straw, brimCenter, new Vector3(0.56f, 0.48f, 0.56f), 16, 4, 0f, 90f, 0.01f, 7);
            b.Brim(straw, brimCenter, 0.56f, 1.06f, 0.03f, 22,
                angle => -0.14f - 0.015f * Mathf.Cos(angle * Mathf.Deg2Rad * 2f), null, strawShade);
            b.Disc(strawShade, brimCenter + Vector3.up * 0.005f, 0.55f, 16, false);
            b.Torus(band, new Vector3(0f, -0.22f, 0f), 0.555f, 0.05f, 18, 6, 1.25f);
        }

        // ---------------------------------------------------------------- chopper hat

        private static void BuildChopperHat(MiniVanMeshBuilder b, List<MiniVanHatMaterial> p)
        {
            int pink = Group(b, p, "Pink", new Color(0.94f, 0.34f, 0.48f));
            int pinkDark = Group(b, p, "PinkDark", new Color(0.78f, 0.22f, 0.36f));
            int white = Group(b, p, "White", new Color(0.95f, 0.95f, 0.94f));
            int antler = Group(b, p, "Antler", new Color(0.52f, 0.31f, 0.15f));

            Vector3 brimCenter = new Vector3(0f, -0.34f, 0f);
            b.Brim(pinkDark, brimCenter, 0.57f, 0.96f, 0.07f, 20, angle => 0.03f);
            b.Disc(pinkDark, brimCenter + Vector3.up * 0.075f, 0.57f, 16, false);

            List<Vector3> crown = new List<Vector3>
            {
                new Vector3(0f, -0.32f, 0f),
                new Vector3(0f, -0.05f, 0f),
                new Vector3(0f, 0.20f, 0f),
                new Vector3(0f, 0.36f, 0f)
            };

            b.Tube(pink, crown, new[] { 0.575f, 0.565f, 0.555f, 0.545f }, 16, false, false);
            b.Dome(pink, new Vector3(0f, 0.36f, 0f), new Vector3(0.545f, 0.10f, 0.545f), 16, 2, 0f, 90f);

            b.Box(white, new Vector3(0f, 0.02f, 0.565f), new Vector3(0.30f, 0.075f, 0.05f), Quaternion.Euler(0f, 0f, 42f));
            b.Box(white, new Vector3(0f, 0.02f, 0.565f), new Vector3(0.30f, 0.075f, 0.05f), Quaternion.Euler(0f, 0f, -42f));

            BuildAntler(b, antler, 1f);
            BuildAntler(b, antler, -1f);
        }

        private static void BuildAntler(MiniVanMeshBuilder b, int group, float side)
        {
            List<Vector3> main = new List<Vector3>
            {
                new Vector3(0.48f * side, -0.10f, 0.04f),
                new Vector3(0.70f * side, 0.00f, 0.02f),
                new Vector3(0.86f * side, 0.18f, 0.00f),
                new Vector3(0.94f * side, 0.40f, -0.02f)
            };

            b.Tube(group, main, new[] { 0.06f, 0.045f, 0.032f, 0f }, 6);

            List<Vector3> prongA = new List<Vector3>
            {
                new Vector3(0.70f * side, 0.00f, 0.02f),
                new Vector3(0.74f * side, 0.16f, 0.12f),
                new Vector3(0.72f * side, 0.30f, 0.20f)
            };

            b.Tube(group, prongA, new[] { 0.038f, 0.026f, 0f }, 6);

            List<Vector3> prongB = new List<Vector3>
            {
                new Vector3(0.86f * side, 0.18f, 0.00f),
                new Vector3(0.98f * side, 0.26f, -0.14f),
                new Vector3(1.02f * side, 0.36f, -0.24f)
            };

            b.Tube(group, prongB, new[] { 0.034f, 0.024f, 0f }, 6);
        }

        // ---------------------------------------------------------------- ash cap

        private static void BuildAshCap(MiniVanMeshBuilder b, List<MiniVanHatMaterial> p)
        {
            int red = Group(b, p, "Red", new Color(0.84f, 0.16f, 0.16f));
            int white = Group(b, p, "White", new Color(0.94f, 0.94f, 0.93f));
            int green = Group(b, p, "Green", new Color(0.13f, 0.52f, 0.24f));

            Vector3 center = new Vector3(0f, -0.32f, 0f);
            b.Dome(red, center, new Vector3(0.56f, 0.46f, 0.56f), 18, 4, -8f, 90f, 0.006f, 3,
                longitude => Mathf.Abs(Mathf.DeltaAngle(longitude, 0f)) < 84f ? white : red);

            BuildCapVisor(b, green, green, center + new Vector3(0f, -0.06f, 0.46f), 0.50f, 0.36f, 0.04f, 12);
            b.Disc(green, center + Vector3.up * 0.01f, 0.55f, 16, false);
            b.Tube(red, new List<Vector3> { new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.17f, 0f) },
                new[] { 0.055f, 0.05f }, 8);

            // League chevron on the white front panel.
            b.Box(green, new Vector3(-0.085f, -0.075f, 0.545f), new Vector3(0.06f, 0.26f, 0.03f), Quaternion.Euler(0f, 0f, -36f));
            b.Box(green, new Vector3(0.085f, -0.075f, 0.545f), new Vector3(0.06f, 0.26f, 0.03f), Quaternion.Euler(0f, 0f, 36f));
            b.Box(green, new Vector3(0f, 0.025f, 0.55f), new Vector3(0.075f, 0.075f, 0.03f), Quaternion.Euler(0f, 0f, 45f));
        }

        // ---------------------------------------------------------------- naruto headband

        private static void BuildNarutoHeadband(MiniVanMeshBuilder b, List<MiniVanHatMaterial> p)
        {
            int cloth = Group(b, p, "Cloth", new Color(0.11f, 0.15f, 0.27f));
            int metal = Group(b, p, "Metal", new Color(0.70f, 0.73f, 0.76f), 0.45f, 0.8f);
            int engrave = Group(b, p, "Engrave", new Color(0.08f, 0.09f, 0.11f), 0.15f, 0.2f);

            b.AddMesh(cloth, MiniVanZoroBandanaMesh.Get(), Matrix4x4.identity);

            // Flat forehead plate (one solid panel, not faceted strips that twist the symbol).
            const float plateRadius = 0.545f;
            const float plateY = -0.19f;
            b.Box(metal, new Vector3(0f, plateY, plateRadius), new Vector3(0.52f, 0.26f, 0.045f), Quaternion.identity);

            // Corner rivets.
            float[] rivetX = { -0.20f, -0.20f, 0.20f, 0.20f };
            float[] rivetY = { 0.08f, -0.08f, 0.08f, -0.08f };
            for (int i = 0; i < 4; i++)
            {
                Vector3 root = new Vector3(rivetX[i], plateY + rivetY[i], plateRadius + 0.01f);
                b.Tube(engrave, new List<Vector3> { root, root + Vector3.forward * 0.03f }, new[] { 0.024f, 0.02f }, 6);
            }

            BuildLeafSymbol(b, engrave, new Vector3(0f, plateY + 0.01f, plateRadius + 0.028f));
        }

        /// <summary>
        /// Konoha symbol: a filled spiral comma matching the leaf village forehead protector.
        /// </summary>
        private static void BuildLeafSymbol(MiniVanMeshBuilder b, int group, Vector3 origin)
        {
            const int steps = 28;
            Vector3[] centerLine = new Vector3[steps + 1];
            float[] halfWidths = new float[steps + 1];

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                // Tip at the bottom; stroke runs counter-clockwise and coils into the centre.
                // That silhouette is what reads as the Konoha leaf swirl.
                float angle = Mathf.Lerp(255f, 255f + 400f, t) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(0.108f, 0.006f, Mathf.Pow(t, 0.72f));
                centerLine[i] = origin + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius + 0.02f, 0f);

                if (t < 0.08f)
                {
                    halfWidths[i] = Mathf.Lerp(0.0025f, 0.015f, t / 0.08f);
                }
                else if (t > 0.9f)
                {
                    halfWidths[i] = Mathf.Lerp(0.014f, 0.0035f, (t - 0.9f) / 0.1f);
                }
                else
                {
                    halfWidths[i] = 0.015f;
                }
            }

            const float halfDepth = 0.012f;
            int[] leftFront = new int[steps + 1];
            int[] rightFront = new int[steps + 1];
            int[] leftBack = new int[steps + 1];
            int[] rightBack = new int[steps + 1];

            for (int i = 0; i <= steps; i++)
            {
                Vector3 tangent = i < steps
                    ? (centerLine[i + 1] - centerLine[i]).normalized
                    : (centerLine[i] - centerLine[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.forward, tangent).normalized;
                Vector3 left = centerLine[i] + side * halfWidths[i];
                Vector3 right = centerLine[i] - side * halfWidths[i];

                leftFront[i] = b.AddVertex(left + Vector3.forward * halfDepth);
                rightFront[i] = b.AddVertex(right + Vector3.forward * halfDepth);
                leftBack[i] = b.AddVertex(left - Vector3.forward * halfDepth * 0.2f);
                rightBack[i] = b.AddVertex(right - Vector3.forward * halfDepth * 0.2f);
            }

            for (int i = 0; i < steps; i++)
            {
                int next = i + 1;
                b.Quad(group, leftFront[i], rightFront[i], rightFront[next], leftFront[next]);
                b.Quad(group, leftBack[i], leftBack[next], rightBack[next], rightBack[i]);
                b.Quad(group, leftFront[i], leftFront[next], leftBack[next], leftBack[i]);
                b.Quad(group, rightFront[i], rightBack[i], rightBack[next], rightFront[next]);
            }

            b.Quad(group, leftFront[0], leftBack[0], rightBack[0], rightFront[0]);
            int last = steps;
            b.Quad(group, leftFront[last], rightFront[last], rightBack[last], leftBack[last]);
        }

        // ---------------------------------------------------------------- law hat

        private static void BuildLawHat(MiniVanMeshBuilder b, List<MiniVanHatMaterial> p)
        {
            int cream = Group(b, p, "Cream", new Color(0.91f, 0.89f, 0.85f));
            int spot = Group(b, p, "Spot", new Color(0.13f, 0.14f, 0.16f));
            int lining = Group(b, p, "Lining", new Color(0.70f, 0.66f, 0.58f));

            Vector3 center = new Vector3(0f, -0.24f, 0f);
            Vector3 radii = new Vector3(0.54f, 0.66f, 0.54f);
            b.Dome(cream, center, radii, 16, 5, -12f, 90f, 0.008f, 21);
            b.Torus(cream, new Vector3(0f, -0.34f, 0f), 0.55f, 0.185f, 18, 8);
            b.Disc(lining, new Vector3(0f, -0.40f, 0f), 0.38f, 16, false);

            Vector3[] spotDirections =
            {
                new Vector3(0.35f, 0.55f, 0.75f), new Vector3(-0.55f, 0.35f, 0.6f),
                new Vector3(0.85f, 0.3f, 0.1f), new Vector3(-0.8f, 0.45f, -0.2f),
                new Vector3(0.15f, 0.9f, -0.1f), new Vector3(-0.2f, 0.7f, -0.6f),
                new Vector3(0.5f, 0.45f, -0.7f), new Vector3(-0.45f, 0.25f, -0.85f),
                new Vector3(0.05f, 0.35f, 0.95f), new Vector3(0.7f, 0.7f, 0.25f),
                new Vector3(-0.65f, 0.75f, 0.15f), new Vector3(0.25f, 0.2f, -0.95f)
            };

            for (int i = 0; i < spotDirections.Length; i++)
            {
                b.SpherePatch(spot, center, radii, spotDirections[i], 15f, 7, 0.012f, 100 + i * 7);
            }

            for (int i = 0; i < 8; i++)
            {
                float degrees = i / 8f * 360f + 12f;
                float angle = degrees * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                float minorAngle = (MiniVanMeshBuilder.Hash(i * 13) - 0.35f) * 1.1f;
                Vector3 normal = (direction * Mathf.Cos(minorAngle) + Vector3.up * Mathf.Sin(minorAngle)).normalized;
                Vector3 position = new Vector3(0f, -0.34f, 0f) + direction * 0.55f + normal * 0.185f;
                b.CurvedPatch(spot, position, normal, 0.095f, 0.185f, 7, 300 + i * 5);
            }
        }

        // ---------------------------------------------------------------- goku / super saiyan hair

        private static void BuildSpikyHair(MiniVanMeshBuilder b, List<MiniVanHatMaterial> p, bool superSaiyan)
        {
            int hair = superSaiyan
                ? Group(b, p, "GoldHair", new Color(0.98f, 0.76f, 0.09f), 0.3f)
                : Group(b, p, "BlackHair", new Color(0.07f, 0.08f, 0.11f), 0.22f);

            Vector3 center = new Vector3(0f, -0.34f, 0f);
            Vector3 skull = new Vector3(0.56f, 0.46f, 0.56f);
            b.Dome(hair, center, skull, 14, 4, -8f, 90f, 0.014f, superSaiyan ? 41 : 17);
            b.Disc(hair, new Vector3(0f, -0.38f, 0f), 0.55f, 14, false);

            int count = superSaiyan ? 15 : 13;
            for (int i = 0; i < count; i++)
            {
                float longitude = i * 137.5f * Mathf.Deg2Rad;
                float latitude = Mathf.Lerp(6f, 68f, MiniVanMeshBuilder.Hash(i * 29 + (superSaiyan ? 5 : 0))) * Mathf.Deg2Rad;

                Vector3 rootDirection = new Vector3(
                    Mathf.Cos(latitude) * Mathf.Sin(longitude),
                    Mathf.Sin(latitude),
                    Mathf.Cos(latitude) * Mathf.Cos(longitude)).normalized;

                Vector3 root = center + Vector3.Scale(rootDirection, skull * 0.86f);
                float length = superSaiyan
                    ? Mathf.Lerp(0.60f, 1.05f, MiniVanMeshBuilder.Hash(i * 71 + 3))
                    : Mathf.Lerp(0.38f, 0.68f, MiniVanMeshBuilder.Hash(i * 71 + 9));

                Vector3 tipDirection = superSaiyan
                    ? (rootDirection * 0.45f + Vector3.up * 1.4f).normalized
                    : (rootDirection * 1.0f + Vector3.up * 0.55f + Vector3.back * 0.2f).normalized;

                Vector3 bend = superSaiyan ? Vector3.up * 0.06f : rootDirection * 0.05f;
                Vector3 mid = root + tipDirection * (length * 0.5f) + bend;
                Vector3 tip = root + tipDirection * length + bend * 2f;

                float baseRadius = superSaiyan ? 0.185f : 0.165f;
                b.Tube(hair, new List<Vector3> { root, mid, tip },
                    new[] { baseRadius, baseRadius * 0.62f, 0f }, 6, false);
            }

            // Bangs hanging over the forehead.
            int bangs = superSaiyan ? 4 : 5;
            for (int i = 0; i < bangs; i++)
            {
                float x = Mathf.Lerp(-0.36f, 0.36f, i / (float)(bangs - 1));
                Vector3 root = new Vector3(x, -0.18f, 0.44f);
                float length = superSaiyan ? 0.40f : 0.46f;
                Vector3 direction = new Vector3(x * 0.7f, superSaiyan ? 0.35f : -0.5f, 1f).normalized;
                Vector3 mid = root + direction * (length * 0.5f);
                Vector3 tip = root + direction * length + Vector3.down * (superSaiyan ? 0f : 0.1f);
                b.Tube(hair, new List<Vector3> { root, mid, tip }, new[] { 0.135f, 0.075f, 0f }, 6, false);
            }
        }

        // ---------------------------------------------------------------- mario cap

        private static void BuildMarioCap(MiniVanMeshBuilder b, List<MiniVanHatMaterial> p)
        {
            int red = Group(b, p, "Red", new Color(0.86f, 0.13f, 0.13f));
            int redDark = Group(b, p, "RedDark", new Color(0.62f, 0.08f, 0.08f));
            int white = Group(b, p, "White", new Color(0.96f, 0.96f, 0.95f));

            Vector3 center = new Vector3(0f, -0.30f, 0f);
            b.Dome(red, center + new Vector3(0f, 0f, 0.02f), new Vector3(0.58f, 0.48f, 0.60f), 18, 5, -8f, 90f, 0.005f, 11);
            // Hinge sits on the front of the dome; depth pushes the bill clear of the crown.
            BuildCapVisor(b, red, redDark, center + new Vector3(0f, -0.06f, 0.48f), 0.52f, 0.38f, 0.05f, 12);
            b.Disc(redDark, center + Vector3.up * 0.01f, 0.56f, 16, false);

            // White badge with the M.
            b.Tube(white, new List<Vector3> { new Vector3(0f, -0.02f, 0.53f), new Vector3(0f, -0.02f, 0.585f) },
                new[] { 0.20f, 0.20f }, 12);

            const float z = 0.60f;
            b.Box(red, new Vector3(-0.105f, -0.02f, z), new Vector3(0.05f, 0.20f, 0.03f), Quaternion.Euler(0f, 0f, 7f));
            b.Box(red, new Vector3(0.105f, -0.02f, z), new Vector3(0.05f, 0.20f, 0.03f), Quaternion.Euler(0f, 0f, -7f));
            b.Box(red, new Vector3(-0.048f, 0.005f, z), new Vector3(0.045f, 0.155f, 0.03f), Quaternion.Euler(0f, 0f, 33f));
            b.Box(red, new Vector3(0.048f, 0.005f, z), new Vector3(0.045f, 0.155f, 0.03f), Quaternion.Euler(0f, 0f, -33f));
        }

        // ---------------------------------------------------------------- viking helmet

        private static void BuildVikingHelmet(MiniVanMeshBuilder b, List<MiniVanHatMaterial> p)
        {
            int steel = Group(b, p, "Steel", new Color(0.34f, 0.35f, 0.37f), 0.38f, 0.75f);
            int steelDark = Group(b, p, "SteelDark", new Color(0.20f, 0.21f, 0.23f), 0.32f, 0.7f);
            int shadow = Group(b, p, "Inner", new Color(0.04f, 0.04f, 0.05f), 0.05f);
            int bone = Group(b, p, "Horn", new Color(0.79f, 0.75f, 0.60f), 0.18f);

            Vector3 center = new Vector3(0f, -0.34f, 0f);
            b.Dome(steel, center, new Vector3(0.58f, 0.54f, 0.58f), 14, 4, -18f, 90f, 0.004f, 5);
            b.Dome(shadow, new Vector3(0f, -0.42f, 0.05f), new Vector3(0.52f, 0.48f, 0.50f), 12, 3, -70f, 20f);

            b.Torus(steelDark, new Vector3(0f, -0.28f, 0f), 0.585f, 0.048f, 16, 6);
            for (int i = 0; i <= 11; i++)
            {
                float t = i / 11f;
                float degrees = Mathf.Lerp(-90f, 90f, t);
                float angle = degrees * Mathf.Deg2Rad;
                Vector3 position = center + new Vector3(0f, Mathf.Cos(angle) * 0.55f, Mathf.Sin(angle) * 0.585f);
                b.Box(steelDark, position, new Vector3(0.13f, 0.065f, 0.19f), Quaternion.Euler(degrees, 0f, 0f));
            }

            // Face mask arranged so brow + nose + cheeks leave two eye openings.
            b.Box(steelDark, new Vector3(0f, -0.34f, 0.545f), new Vector3(0.70f, 0.12f, 0.10f), Quaternion.Euler(8f, 0f, 0f));
            b.Box(steelDark, new Vector3(0f, -0.52f, 0.55f), new Vector3(0.13f, 0.36f, 0.09f), Quaternion.Euler(4f, 0f, 0f));
            b.Box(steelDark, new Vector3(0f, -0.72f, 0.52f), new Vector3(0.22f, 0.10f, 0.08f), Quaternion.Euler(12f, 0f, 0f));

            b.Box(steel, new Vector3(0.28f, -0.58f, 0.46f), new Vector3(0.28f, 0.28f, 0.12f), Quaternion.Euler(0f, 18f, -8f));
            b.Box(steel, new Vector3(-0.28f, -0.58f, 0.46f), new Vector3(0.28f, 0.28f, 0.12f), Quaternion.Euler(0f, -18f, 8f));
            b.Box(steel, new Vector3(0.34f, -0.78f, 0.38f), new Vector3(0.22f, 0.22f, 0.12f), Quaternion.Euler(8f, 24f, -14f));
            b.Box(steel, new Vector3(-0.34f, -0.78f, 0.38f), new Vector3(0.22f, 0.22f, 0.12f), Quaternion.Euler(8f, -24f, 14f));
            b.Box(steelDark, new Vector3(0.40f, -0.92f, 0.34f), new Vector3(0.14f, 0.16f, 0.10f), Quaternion.Euler(10f, 28f, -20f));
            b.Box(steelDark, new Vector3(-0.40f, -0.92f, 0.34f), new Vector3(0.14f, 0.16f, 0.10f), Quaternion.Euler(10f, -28f, 20f));

            for (int i = -2; i <= 2; i++)
            {
                float degrees = 180f + i * 24f;
                float angle = degrees * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                b.Box(steel, new Vector3(0f, -0.60f, 0f) + direction * 0.55f,
                    new Vector3(0.26f, 0.38f, 0.07f), Quaternion.Euler(-10f, degrees, 0f));
            }

            b.Box(steelDark, new Vector3(0.50f, -0.30f, 0.05f), new Vector3(0.16f, 0.16f, 0.16f), Quaternion.Euler(0f, 0f, -12f));
            b.Box(steelDark, new Vector3(-0.50f, -0.30f, 0.05f), new Vector3(0.16f, 0.16f, 0.16f), Quaternion.Euler(0f, 0f, 12f));
            for (int i = 0; i < 10; i++)
            {
                float degrees = i / 10f * 360f + 18f;
                float angle = degrees * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 root = new Vector3(0f, -0.28f, 0f) + direction * 0.61f;
                b.Tube(steel, new List<Vector3> { root, root + direction * 0.028f }, new[] { 0.028f, 0.024f }, 6);
            }

            BuildHorn(b, bone, 1f);
            BuildHorn(b, bone, -1f);
        }

        private static void BuildHorn(MiniVanMeshBuilder b, int group, float side)
        {
            List<Vector3> spine = new List<Vector3>
            {
                new Vector3(0.52f * side, -0.30f, 0.05f),
                new Vector3(0.78f * side, -0.20f, 0.00f),
                new Vector3(0.98f * side, 0.04f, -0.03f),
                new Vector3(1.06f * side, 0.30f, -0.04f),
                new Vector3(1.02f * side, 0.54f, -0.01f),
                new Vector3(0.92f * side, 0.70f, 0.03f)
            };

            b.Tube(group, spine, new[] { 0.145f, 0.115f, 0.082f, 0.052f, 0.028f, 0f }, 8);
        }

        // ---------------------------------------------------------------- pirate tricorn

        private static void BuildPirateTricorn(MiniVanMeshBuilder b, List<MiniVanHatMaterial> p)
        {
            int leather = Group(b, p, "Leather", new Color(0.25f, 0.19f, 0.15f), 0.1f);
            int leatherDark = Group(b, p, "LeatherDark", new Color(0.16f, 0.12f, 0.10f), 0.1f);
            int trim = Group(b, p, "Trim", new Color(0.72f, 0.62f, 0.43f));
            int sash = Group(b, p, "Sash", new Color(0.45f, 0.14f, 0.15f));

            Vector3 center = new Vector3(0f, -0.30f, 0f);
            b.Dome(leather, center, new Vector3(0.53f, 0.45f, 0.53f), 14, 4, 0f, 90f, 0.014f, 33);
            b.Disc(leatherDark, center + Vector3.up * 0.005f, 0.52f, 16, false);

            float[] lobes = { 0f, 128f, 232f };
            System.Func<float, float> fold = angle =>
            {
                float lift = 0f;
                for (int i = 0; i < lobes.Length; i++)
                {
                    float distance = Mathf.Abs(Mathf.DeltaAngle(angle, lobes[i]));
                    float local = Mathf.Cos(Mathf.Clamp01(distance / 72f) * Mathf.PI * 0.5f);
                    lift = Mathf.Max(lift, local);
                }

                return -0.17f + lift * 0.44f;
            };

            const int brimSegments = 26;
            b.Brim(leather, center, 0.53f, 1.0f, 0.035f, brimSegments, fold, null, leatherDark);

            List<Vector3> edge = new List<Vector3>();
            List<float> edgeRadii = new List<float>();
            for (int i = 0; i <= brimSegments; i++)
            {
                float degrees = i / (float)brimSegments * 360f;
                float angle = degrees * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                edge.Add(center + direction * 1.0f + Vector3.up * (fold(degrees) - 0.017f));
                edgeRadii.Add(0.026f);
            }

            b.Tube(trim, edge, edgeRadii, 5);
            b.Torus(sash, new Vector3(0f, -0.265f, 0f), 0.545f, 0.075f, 16, 6, 1.15f);

            // Sash knot with two tails dropping past the brim on the left.
            Vector3 knot = new Vector3(-0.52f, -0.28f, 0.16f);
            b.Dome(sash, knot, new Vector3(0.11f, 0.10f, 0.11f), 8, 3, -80f, 90f);
            b.Tube(sash, new List<Vector3> { knot, knot + new Vector3(-0.08f, -0.22f, 0.06f), knot + new Vector3(-0.12f, -0.44f, 0.11f) },
                new[] { 0.06f, 0.045f, 0f }, 5, false);
            b.Tube(sash, new List<Vector3> { knot, knot + new Vector3(-0.14f, -0.18f, -0.04f), knot + new Vector3(-0.22f, -0.36f, -0.08f) },
                new[] { 0.055f, 0.04f, 0f }, 5, false);

            // Stitch marks on the crown.
            for (int i = 0; i < 2; i++)
            {
                Vector3 stitch = new Vector3(0.10f + i * 0.16f, -0.10f - i * 0.10f, 0.50f);
                b.Box(trim, stitch, new Vector3(0.10f, 0.022f, 0.02f), Quaternion.Euler(0f, 0f, 45f));
                b.Box(trim, stitch, new Vector3(0.10f, 0.022f, 0.02f), Quaternion.Euler(0f, 0f, -45f));
            }
        }
    }
}
