using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.Editor
{
    /// <summary>
    /// Inspector for the meat maze zone with a top-down preview of the noise that drives the walls.
    /// The reachability mode flood fills from the four entrances so it is obvious which pockets of
    /// the maze can only be opened with the flamethrower.
    /// </summary>
    [CustomEditor(typeof(MiniVanMeatMazeZone))]
    public sealed class MiniVanMeatMazeZoneEditor : UnityEditor.Editor
    {
        private enum PreviewMode
        {
            Walls,
            RawNoise,
            Reachability
        }

        private const float PassableDensity = 0.2f;

        private static readonly Color OpenColor = new Color(0.07f, 0.06f, 0.07f);
        private static readonly Color WallDeepColor = new Color(0.30f, 0.05f, 0.05f);
        private static readonly Color WallCoreColor = new Color(0.86f, 0.20f, 0.16f);
        private static readonly Color ReachableColor = new Color(0.10f, 0.26f, 0.24f);
        private static readonly Color SealedColor = new Color(0.05f, 0.35f, 0.62f);
        private static readonly Color EntranceColor = new Color(0.25f, 0.95f, 0.35f);
        private static readonly Color ChunkLineColor = new Color(1f, 1f, 1f, 0.16f);

        private Texture2D preview;
        private Color32[] pixels;
        private const float PreviewDisplaySize = 148f;
        private PreviewMode mode = PreviewMode.Walls;
        private int resolution = 128;
        private int previewPulse;
        private bool autoRefresh = true;
        private bool showChunkGrid;
        private string signature;
        private string stats = string.Empty;

        private void OnDisable()
        {
            if (preview != null)
            {
                DestroyImmediate(preview);
                preview = null;
            }
        }

        public override void OnInspectorGUI()
        {
            MiniVanMeatMazeZone zone = (MiniVanMeatMazeZone)target;

            DrawTopPreview(zone);
            EditorGUILayout.Space(4f);

            DrawDefaultInspector();

            EditorGUILayout.Space(6f);
            DrawBudget(zone);
            if (!string.IsNullOrEmpty(stats))
            {
                EditorGUILayout.LabelField(stats, EditorStyles.wordWrappedMiniLabel);
            }

            DrawLegend();
            DrawActions(zone);
        }

        private void DrawTopPreview(MiniVanMeatMazeZone zone)
        {
            string current = BuildSignature(zone);
            if (preview == null || (autoRefresh && current != signature))
            {
                Regenerate(zone, current);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Noise Preview", EditorStyles.boldLabel);
                    mode = (PreviewMode)EditorGUILayout.EnumPopup(mode);
                    previewPulse = EditorGUILayout.IntSlider(previewPulse, 0, 12);
                    showChunkGrid = EditorGUILayout.ToggleLeft("Chunk Grid", showChunkGrid);
                    autoRefresh = EditorGUILayout.ToggleLeft("Auto Refresh", autoRefresh);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Refresh", GUILayout.Height(22f)))
                        {
                            Regenerate(zone, BuildSignature(zone));
                        }

                        if (GUILayout.Button("Seed", GUILayout.Width(52f), GUILayout.Height(22f)))
                        {
                            Undo.RecordObject(zone, "Randomize Meat Maze Seed");
                            zone.EditorSeed = Random.Range(1, int.MaxValue);
                            EditorUtility.SetDirty(zone);
                        }
                    }
                }

                GUILayout.FlexibleSpace();

                Rect rect = GUILayoutUtility.GetRect(
                    PreviewDisplaySize,
                    PreviewDisplaySize,
                    GUILayout.Width(PreviewDisplaySize),
                    GUILayout.Height(PreviewDisplaySize));
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
                if (preview != null)
                {
                    GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                }
            }
        }

        private void DrawBudget(MiniVanMeatMazeZone zone)
        {
            EditorGUILayout.LabelField(
                "Voxels",
                $"{zone.GridCellsX}x{zone.GridCellsVertical}x{zone.GridCellsZ}, " +
                $"chunks {zone.ChunkGridSizeX}x{zone.ChunkGridSizeZ} (loaded {zone.LoadedChunkCount}), " +
                $"thickness@{previewPulse}: {zone.ThicknessAtPulse(previewPulse):F2}m",
                EditorStyles.miniLabel);
        }

        private void DrawLegend()
        {
            string legend = mode switch
            {
                PreviewMode.Walls => "Red = wall, black = open, green = entrance.",
                PreviewMode.RawNoise => "Grey = noise, orange/cyan = Contour A/B.",
                _ => "Teal = reachable, blue = sealed open floor, red = wall."
            };
            EditorGUILayout.LabelField(legend, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawActions(MiniVanMeatMazeZone zone)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Mesh In Scene"))
                {
                    zone.EditorRebuildPreview();
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Clear Mesh"))
                {
                    zone.EditorClearPreview();
                    SceneView.RepaintAll();
                }
            }
        }

        private string BuildSignature(MiniVanMeatMazeZone zone)
        {
            return string.Join("|",
                zone.ZoneSizeX, zone.ZoneSizeY, zone.CellSize, zone.WallHeight, zone.CurrentSeed,
                zone.NoiseScaleX, zone.NoiseScaleY, zone.NoiseAngular, zone.ContourA, zone.ContourB,
                zone.WallThickness, zone.ThicknessPerPulse, zone.MaxWallThickness,
                zone.EntranceDepth, zone.EntranceWidth, zone.ChunkCells,
                zone.StreamRadius, zone.MaxLoadedChunks,
                mode, resolution, previewPulse, showChunkGrid);
        }

        private void Regenerate(MiniVanMeatMazeZone zone, string newSignature)
        {
            signature = newSignature;
            int size = Mathf.Max(32, resolution);

            if (preview == null || preview.width != size)
            {
                if (preview != null)
                {
                    DestroyImmediate(preview);
                }

                preview = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "MeatMazeNoisePreview",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                pixels = new Color32[size * size];
            }

            float[] density = SampleDensity(zone, size);
            switch (mode)
            {
                case PreviewMode.RawNoise:
                    PaintRawNoise(zone, size);
                    break;
                case PreviewMode.Reachability:
                    PaintReachability(zone, density, size);
                    break;
                default:
                    PaintWalls(zone, density, size);
                    break;
            }

            if (showChunkGrid)
            {
                PaintChunkGrid(zone, size);
            }

            PaintEntranceMarkers(size);
            preview.SetPixels32(pixels);
            preview.Apply(false, false);
        }

        private float[] SampleDensity(MiniVanMeatMazeZone zone, int size)
        {
            float[] density = new float[size * size];
            int seed = zone.CurrentSeed;
            float halfX = zone.HalfX;
            float halfZ = zone.HalfZ;
            float stepX = zone.ZoneSizeX / (size - 1);
            float stepZ = zone.ZoneSizeY / (size - 1);

            for (int y = 0; y < size; y++)
            {
                float pz = -halfZ + y * stepZ;
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    density[row + x] = zone.EvaluateWallDensity(-halfX + x * stepX, pz, seed, previewPulse);
                }
            }

            return density;
        }

        private void PaintWalls(MiniVanMeatMazeZone zone, float[] density, int size)
        {
            float wallTop = zone.WallHeight;
            float coverage = 0f;
            float blocked = 0f;

            for (int i = 0; i < density.Length; i++)
            {
                float d = density[i];
                Color color = d <= 0.001f
                    ? OpenColor
                    : Color.Lerp(WallDeepColor, WallCoreColor, Mathf.Clamp01((d - 0.05f) / 0.95f));
                pixels[i] = color;

                coverage += d;
                if (d > PassableDensity)
                {
                    blocked += 1f;
                }
            }

            stats = string.Format(
                "Wall coverage {0:P0} of the zone, {1:P0} of the floor is blocked.\nTallest walls reach {2:F1} m. Zone {3:F0}x{4:F0} m.",
                coverage / density.Length, blocked / density.Length, wallTop, zone.ZoneSizeX, zone.ZoneSizeY);
        }

        private void PaintRawNoise(MiniVanMeatMazeZone zone, int size)
        {
            int seed = zone.CurrentSeed;
            float halfX = zone.HalfX;
            float halfZ = zone.HalfZ;
            float stepX = zone.ZoneSizeX / (size - 1);
            float stepZ = zone.ZoneSizeY / (size - 1);
            float bandWidth = 0.012f;

            for (int y = 0; y < size; y++)
            {
                float pz = -halfZ + y * stepZ;
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float n = zone.EvaluateRawNoise(-halfX + x * stepX, pz, seed, previewPulse);
                    Color color = new Color(n * 0.55f, n * 0.55f, n * 0.6f);

                    if (Mathf.Abs(n - zone.ContourA) < bandWidth)
                    {
                        color = new Color(1f, 0.55f, 0.1f);
                    }
                    else if (Mathf.Abs(n - zone.ContourB) < bandWidth)
                    {
                        color = new Color(0.25f, 0.85f, 0.95f);
                    }

                    pixels[row + x] = color;
                }
            }

            stats = $"Noise scale X {zone.NoiseScaleX:F3} / Y {zone.NoiseScaleY:F3}, " +
                    $"period ~{1f / Mathf.Max(0.001f, zone.NoiseScaleX):F1} x {1f / Mathf.Max(0.001f, zone.NoiseScaleY):F1} m, " +
                    $"angular {zone.NoiseAngular:P0}.";
        }

        private void PaintReachability(MiniVanMeatMazeZone zone, float[] density, int size)
        {
            bool[] passable = new bool[density.Length];
            int openCount = 0;
            for (int i = 0; i < density.Length; i++)
            {
                passable[i] = density[i] <= PassableDensity;
                if (passable[i])
                {
                    openCount++;
                }
            }

            bool[] reached = new bool[density.Length];
            Queue<int> frontier = new Queue<int>();
            int mid = size / 2;
            TrySeed(passable, reached, frontier, size, mid, 0);
            TrySeed(passable, reached, frontier, size, mid, size - 1);
            TrySeed(passable, reached, frontier, size, 0, mid);
            TrySeed(passable, reached, frontier, size, size - 1, mid);

            int reachedCount = frontier.Count;
            while (frontier.Count > 0)
            {
                int index = frontier.Dequeue();
                int x = index % size;
                int y = index / size;

                if (x > 0) reachedCount += TryVisit(passable, reached, frontier, index - 1);
                if (x < size - 1) reachedCount += TryVisit(passable, reached, frontier, index + 1);
                if (y > 0) reachedCount += TryVisit(passable, reached, frontier, index - size);
                if (y < size - 1) reachedCount += TryVisit(passable, reached, frontier, index + size);
            }

            for (int i = 0; i < density.Length; i++)
            {
                if (!passable[i])
                {
                    pixels[i] = Color.Lerp(WallDeepColor, WallCoreColor, Mathf.Clamp01(density[i]));
                }
                else
                {
                    pixels[i] = reached[i] ? ReachableColor : SealedColor;
                }
            }

            bool centerReached = reached[mid * size + mid];
            float openFraction = openCount / (float)density.Length;
            float reachedFraction = openCount > 0 ? reachedCount / (float)openCount : 0f;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Open floor {0:P0} of the zone.\n", openFraction);
            sb.AppendFormat("Reachable on foot from the entrances: {0:P0} of that floor.\n", reachedFraction);
            sb.Append(centerReached
                ? "The zone center can be reached without burning."
                : "The zone center is sealed: players must burn a path.");
            stats = sb.ToString();
        }

        private static void TrySeed(bool[] passable, bool[] reached, Queue<int> frontier, int size, int x, int y)
        {
            for (int offset = -3; offset <= 3; offset++)
            {
                int px = Mathf.Clamp(x == 0 || x == size - 1 ? x : x + offset, 0, size - 1);
                int py = Mathf.Clamp(y == 0 || y == size - 1 ? y : y + offset, 0, size - 1);
                int index = py * size + px;
                if (passable[index] && !reached[index])
                {
                    reached[index] = true;
                    frontier.Enqueue(index);
                }
            }
        }

        private static int TryVisit(bool[] passable, bool[] reached, Queue<int> frontier, int index)
        {
            if (reached[index] || !passable[index])
            {
                return 0;
            }

            reached[index] = true;
            frontier.Enqueue(index);
            return 1;
        }

        private void PaintChunkGrid(MiniVanMeatMazeZone zone, int size)
        {
            int chunksX = Mathf.Max(1, zone.ChunkGridSizeX);
            int chunksZ = Mathf.Max(1, zone.ChunkGridSizeZ);
            for (int c = 1; c < chunksX; c++)
            {
                int line = Mathf.Clamp(Mathf.RoundToInt(c / (float)chunksX * size), 0, size - 1);
                for (int i = 0; i < size; i++)
                {
                    pixels[i * size + line] = Blend(pixels[i * size + line], ChunkLineColor);
                }
            }

            for (int c = 1; c < chunksZ; c++)
            {
                int line = Mathf.Clamp(Mathf.RoundToInt(c / (float)chunksZ * size), 0, size - 1);
                for (int i = 0; i < size; i++)
                {
                    pixels[line * size + i] = Blend(pixels[line * size + i], ChunkLineColor);
                }
            }
        }

        private void PaintEntranceMarkers(int size)
        {
            int mid = size / 2;
            MarkCross(size, mid, 2);
            MarkCross(size, mid, size - 3);
            MarkCross(size, 2, mid);
            MarkCross(size, size - 3, mid);
        }

        private void MarkCross(int size, int x, int y)
        {
            for (int d = -2; d <= 2; d++)
            {
                int px = Mathf.Clamp(x + d, 0, size - 1);
                int py = Mathf.Clamp(y + d, 0, size - 1);
                pixels[y * size + px] = EntranceColor;
                pixels[py * size + x] = EntranceColor;
            }
        }

        private static Color32 Blend(Color32 baseColor, Color overlay)
        {
            Color b = baseColor;
            return Color.Lerp(b, overlay, overlay.a);
        }
    }
}
