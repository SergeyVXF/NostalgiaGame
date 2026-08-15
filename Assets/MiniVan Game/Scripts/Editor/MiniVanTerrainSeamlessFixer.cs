using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Stepped terrain fixes to avoid OOM. Run menu items one by one and wait between them.
    /// </summary>
    public static class MiniVanTerrainSeamlessFixer
    {
        const string Folder = "Assets/MiniVan Game/Settings/World/GameMode/Terrain";
        const float TileSize = 512f;
        const float CommonHeight = 256f;

        [MenuItem("MiniVan/Terrain/Step 1 - Unique TerrainData (batch)")]
        public static void Step1_UniqueData()
        {
            var terrains = FindTerrains();
            int cloned = 0;
            var sharedGroups = terrains.Where(t => t.terrainData != null)
                .GroupBy(t => t.terrainData)
                .Where(g => g.Count() > 1)
                .ToList();

            if (sharedGroups.Count == 0)
            {
                Debug.Log("[TerrainFix] Step1: no shared TerrainData.");
                return;
            }

            // Only clone first shared group this click to keep memory low.
            var g = sharedGroups[0];
            bool first = true;
            int batchLimit = 4;
            int done = 0;
            foreach (var t in g)
            {
                if (first)
                {
                    first = false;
                    continue;
                }

                if (done >= batchLimit) break;

                string srcPath = AssetDatabase.GetAssetPath(g.Key);
                string safe = MakeSafe(t.name) + "_x" + Mathf.RoundToInt(t.transform.position.x)
                    + "_z" + Mathf.RoundToInt(t.transform.position.z);
                string dst = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + safe + "_TerrainData.asset");

                if (string.IsNullOrEmpty(srcPath) || !AssetDatabase.CopyAsset(srcPath, dst))
                {
                    var copy = UnityEngine.Object.Instantiate(g.Key);
                    AssetDatabase.CreateAsset(copy, dst);
                }

                var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dst);
                Undo.RecordObject(t, "Unique TerrainData");
                t.terrainData = td;
                var col = t.GetComponent<TerrainCollider>();
                if (col != null)
                {
                    Undo.RecordObject(col, "Unique TerrainData");
                    col.terrainData = td;
                    EditorUtility.SetDirty(col);
                }

                EditorUtility.SetDirty(t);
                cloned++;
                done++;
            }

            AssetDatabase.SaveAssets();
            Resources.UnloadUnusedAssets();
            GC.Collect();

            int remaining = terrains.Where(t => t.terrainData != null)
                .GroupBy(t => t.terrainData.GetInstanceID())
                .Count(x => x.Count() > 1);

            Debug.Log("[TerrainFix] Step1: cloned=" + cloned
                + " sharedGroupsLeft=" + remaining
                + " (run Step1 again until sharedGroupsLeft=0)");
            MarkDirty();
        }

        [MenuItem("MiniVan/Terrain/Step 2 - Snap to 512 grid (positions only)")]
        public static void Step2_SnapGrid()
        {
            var terrains = FindTerrains();
            var xs = Cluster(terrains.Select(t => t.transform.position.x).ToList(), 120f);
            var zs = Cluster(terrains.Select(t => t.transform.position.z).ToList(), 120f);
            float originX = xs[0];
            float originZ = zs[0];

            var cellOcc = new Dictionary<(int, int), Terrain>();
            Material mat = terrains.Select(t => t.materialTemplate).FirstOrDefault(m => m != null);

            foreach (var t in terrains.OrderBy(t => t.name))
            {
                int ix = NearestIndex(xs, t.transform.position.x);
                int iz = NearestIndex(zs, t.transform.position.z);
                var key = (ix, iz);
                int guard = 0;
                while (cellOcc.ContainsKey(key) && guard++ < 64)
                {
                    if (!cellOcc.ContainsKey((ix, iz + 1))) iz++;
                    else ix++;
                    key = (ix, iz);
                }

                cellOcc[key] = t;
                Undo.RecordObject(t.transform, "Snap terrain grid");
                t.transform.position = new Vector3(originX + ix * TileSize, 0f, originZ + iz * TileSize);

                if (mat != null && t.materialTemplate == null)
                {
                    Undo.RecordObject(t, "Terrain mat");
                    t.materialTemplate = mat;
                }

                t.drawHeightmap = true;
                t.drawInstanced = true;
                EditorUtility.SetDirty(t);
            }

            Debug.Log("[TerrainFix] Step2: snapped " + terrains.Count
                + " tiles to grid cols=" + xs.Count + " rows=" + zs.Count
                + " origin=(" + originX + "," + originZ + ")");
            MarkDirty();
        }

        [MenuItem("MiniVan/Terrain/Step 3 - Unify height scale (one tile)")]
        public static void Step3_UnifyHeightOne()
        {
            var terrains = FindTerrains();
            Terrain target = null;
            foreach (var t in terrains)
            {
                if (t.terrainData == null) continue;
                bool needs = Mathf.Abs(t.terrainData.size.y - CommonHeight) > 0.01f
                    || Mathf.Abs(t.transform.position.y) > 0.01f
                    || Mathf.Abs(t.terrainData.size.x - TileSize) > 0.01f
                    || Mathf.Abs(t.terrainData.size.z - TileSize) > 0.01f;
                if (needs)
                {
                    target = t;
                    break;
                }
            }

            if (target == null)
            {
                Debug.Log("[TerrainFix] Step3: all tiles already unified.");
                return;
            }

            UnifyHeightScale(target, CommonHeight);
            AssetDatabase.SaveAssets();
            Resources.UnloadUnusedAssets();
            GC.Collect();

            int left = terrains.Count(t =>
                t.terrainData != null && (
                    Mathf.Abs(t.terrainData.size.y - CommonHeight) > 0.01f
                    || Mathf.Abs(t.transform.position.y) > 0.01f
                    || Mathf.Abs(t.terrainData.size.x - TileSize) > 0.01f
                    || Mathf.Abs(t.terrainData.size.z - TileSize) > 0.01f));

            Debug.Log("[TerrainFix] Step3: unified " + target.name
                + " remaining=" + left + " (run again until remaining=0)");
            MarkDirty();
        }

        [MenuItem("MiniVan/Terrain/Step 4 - Stitch one neighbor pair")]
        public static void Step4_StitchOnePair()
        {
            var cellOcc = BuildCellMap(FindTerrains());
            if (cellOcc.Count == 0)
            {
                Debug.LogError("[TerrainFix] Step4: no terrains.");
                return;
            }

            // Prefer pairs with largest seam error.
            Terrain bestA = null, bestB = null;
            bool vertical = true;
            float bestErr = 0.05f; // ignore already good seams

            foreach (var kv in cellOcc)
            {
                int ix = kv.Key.Item1;
                int iz = kv.Key.Item2;
                var t = kv.Value;
                if (t.terrainData == null) continue;

                if (cellOcc.TryGetValue((ix + 1, iz), out var right) && right.terrainData != null)
                {
                    float err = MeasureVerticalSeam(t, right);
                    if (err > bestErr)
                    {
                        bestErr = err;
                        bestA = t;
                        bestB = right;
                        vertical = true;
                    }
                }

                if (cellOcc.TryGetValue((ix, iz + 1), out var north) && north.terrainData != null)
                {
                    float err = MeasureHorizontalSeam(t, north);
                    if (err > bestErr)
                    {
                        bestErr = err;
                        bestA = t;
                        bestB = north;
                        vertical = false;
                    }
                }
            }

            if (bestA == null)
            {
                Debug.Log("[TerrainFix] Step4: all neighbor seams <= 0.05m.");
                return;
            }

            if (vertical) StitchVerticalEdge(bestA, bestB);
            else StitchHorizontalEdge(bestA, bestB);

            if (bestA.terrainData != null) bestA.terrainData.SyncHeightmap();
            if (bestB.terrainData != null) bestB.terrainData.SyncHeightmap();
            AssetDatabase.SaveAssets();
            Resources.UnloadUnusedAssets();
            GC.Collect();

            float after = vertical ? MeasureVerticalSeam(bestA, bestB) : MeasureHorizontalSeam(bestA, bestB);
            Debug.Log("[TerrainFix] Step4: stitched "
                + bestA.name + (vertical ? " EAST-WEST " : " NORTH-SOUTH ")
                + bestB.name + " err " + bestErr.ToString("F3") + " -> " + after.ToString("F3")
                + "m (run again for next pair)");
            MarkDirty();
        }

        [MenuItem("MiniVan/Terrain/Step 5 - SetNeighbors + save")]
        public static void Step5_Neighbors()
        {
            var cellOcc = BuildCellMap(FindTerrains());
            foreach (var kv in cellOcc)
            {
                int ix = kv.Key.Item1;
                int iz = kv.Key.Item2;
                var t = kv.Value;
                cellOcc.TryGetValue((ix - 1, iz), out var left);
                cellOcc.TryGetValue((ix + 1, iz), out var right);
                cellOcc.TryGetValue((ix, iz + 1), out var top);
                cellOcc.TryGetValue((ix, iz - 1), out var bottom);
                Undo.RecordObject(t, "Neighbors");
                t.SetNeighbors(left, top, right, bottom);
                EditorUtility.SetDirty(t);
            }

            AssetDatabase.SaveAssets();
            MarkDirty();
            Debug.Log("[TerrainFix] Step5: neighbors set for " + cellOcc.Count + " tiles. Save scene (Ctrl+S).");
        }

        static List<Terrain> FindTerrains()
        {
            return UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
        }

        static Dictionary<(int, int), Terrain> BuildCellMap(List<Terrain> terrains)
        {
            var map = new Dictionary<(int, int), Terrain>();
            if (terrains.Count == 0) return map;
            float minX = terrains.Min(t => t.transform.position.x);
            float minZ = terrains.Min(t => t.transform.position.z);
            foreach (var t in terrains)
            {
                int ix = Mathf.RoundToInt((t.transform.position.x - minX) / TileSize);
                int iz = Mathf.RoundToInt((t.transform.position.z - minZ) / TileSize);
                var key = (ix, iz);
                int guard = 0;
                while (map.ContainsKey(key) && guard++ < 64)
                {
                    if (!map.ContainsKey((ix, iz + 1))) iz++;
                    else ix++;
                    key = (ix, iz);
                }

                map[key] = t;
            }

            return map;
        }

        static void UnifyHeightScale(Terrain t, float newY)
        {
            var d = t.terrainData;
            if (d == null) return;
            float oldY = d.size.y;
            float oldBase = t.transform.position.y;
            int res = d.heightmapResolution;

            Undo.RecordObject(t.transform, "Terrain y=0");
            // Keep world heights: convert then move base to 0.
            float[,] h = d.GetHeights(0, 0, res, res);
            if (Mathf.Abs(oldY - newY) > 0.01f || Mathf.Abs(oldBase) > 0.01f)
            {
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float world = oldBase + h[y, x] * oldY;
                    h[y, x] = Mathf.Clamp01(world / newY);
                }

                Undo.RecordObject(d, "Unify height");
                d.size = new Vector3(TileSize, newY, TileSize);
                d.SetHeightsDelayLOD(0, 0, h);
                d.SyncHeightmap();
                EditorUtility.SetDirty(d);
            }
            else if (Mathf.Abs(d.size.x - TileSize) > 0.01f || Mathf.Abs(d.size.z - TileSize) > 0.01f)
            {
                Undo.RecordObject(d, "Unify xz size");
                d.size = new Vector3(TileSize, newY, TileSize);
                EditorUtility.SetDirty(d);
            }

            t.transform.position = new Vector3(t.transform.position.x, 0f, t.transform.position.z);
            var col = t.GetComponent<TerrainCollider>();
            if (col != null)
            {
                Undo.RecordObject(col, "Collider data");
                col.terrainData = d;
                EditorUtility.SetDirty(col);
            }

            EditorUtility.SetDirty(t);
            h = null;
        }

        static void StitchVerticalEdge(Terrain left, Terrain right)
        {
            var ld = left.terrainData;
            var rd = right.terrainData;
            if (ld == null || rd == null) return;
            int samples = Mathf.Max(ld.heightmapResolution, rd.heightmapResolution);
            float[] world = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float u = i / (float)(samples - 1);
                float lz = left.transform.position.z + u * ld.size.z;
                float rz = right.transform.position.z + u * rd.size.z;
                world[i] = (SampleEdgeWorldY(left, true, lz) + SampleEdgeWorldY(right, false, rz)) * 0.5f;
            }

            Undo.RegisterCompleteObjectUndo(new[] { (UnityEngine.Object)ld, (UnityEngine.Object)rd }, "Stitch vertical");
            WriteEdgeWorldY(left, true, world);
            WriteEdgeWorldY(right, false, world);
            world = null;
        }

        static void StitchHorizontalEdge(Terrain south, Terrain north)
        {
            var sd = south.terrainData;
            var nd = north.terrainData;
            if (sd == null || nd == null) return;
            int samples = Mathf.Max(sd.heightmapResolution, nd.heightmapResolution);
            float[] world = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float u = i / (float)(samples - 1);
                float sx = south.transform.position.x + u * sd.size.x;
                float nx = north.transform.position.x + u * nd.size.x;
                world[i] = (SampleEdgeWorldX(south, true, sx) + SampleEdgeWorldX(north, false, nx)) * 0.5f;
            }

            Undo.RegisterCompleteObjectUndo(new[] { (UnityEngine.Object)sd, (UnityEngine.Object)nd }, "Stitch horizontal");
            WriteEdgeWorldX(south, true, world);
            WriteEdgeWorldX(north, false, world);
            world = null;
        }

        static float SampleEdgeWorldY(Terrain t, bool east, float worldZ)
        {
            var d = t.terrainData;
            float v = Mathf.Clamp01((worldZ - t.transform.position.z) / d.size.z);
            float fy = v * (d.heightmapResolution - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, d.heightmapResolution - 1);
            int y1 = Mathf.Min(y0 + 1, d.heightmapResolution - 1);
            int x = east ? d.heightmapResolution - 1 : 0;
            float a = t.transform.position.y + d.GetHeight(x, y0);
            float b = t.transform.position.y + d.GetHeight(x, y1);
            return Mathf.Lerp(a, b, fy - y0);
        }

        static float SampleEdgeWorldX(Terrain t, bool north, float worldX)
        {
            var d = t.terrainData;
            float u = Mathf.Clamp01((worldX - t.transform.position.x) / d.size.x);
            float fx = u * (d.heightmapResolution - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, d.heightmapResolution - 1);
            int x1 = Mathf.Min(x0 + 1, d.heightmapResolution - 1);
            int y = north ? d.heightmapResolution - 1 : 0;
            float a = t.transform.position.y + d.GetHeight(x0, y);
            float b = t.transform.position.y + d.GetHeight(x1, y);
            return Mathf.Lerp(a, b, fx - x0);
        }

        static void WriteEdgeWorldY(Terrain t, bool east, float[] worldEdge)
        {
            var d = t.terrainData;
            int res = d.heightmapResolution;
            float[,] col = new float[res, 1];
            for (int y = 0; y < res; y++)
            {
                float u = y / (float)(res - 1);
                col[y, 0] = Mathf.Clamp01((SampleArr(worldEdge, u) - t.transform.position.y) / d.size.y);
            }

            d.SetHeightsDelayLOD(east ? res - 1 : 0, 0, col);
            d.SyncHeightmap();
            EditorUtility.SetDirty(d);
        }

        static void WriteEdgeWorldX(Terrain t, bool north, float[] worldEdge)
        {
            var d = t.terrainData;
            int res = d.heightmapResolution;
            float[,] row = new float[1, res];
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1);
                row[0, x] = Mathf.Clamp01((SampleArr(worldEdge, u) - t.transform.position.y) / d.size.y);
            }

            d.SetHeightsDelayLOD(0, north ? res - 1 : 0, row);
            d.SyncHeightmap();
            EditorUtility.SetDirty(d);
        }

        static float SampleArr(float[] arr, float u)
        {
            float x = u * (arr.Length - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, arr.Length - 1);
            int x1 = Mathf.Min(x0 + 1, arr.Length - 1);
            return Mathf.Lerp(arr[x0], arr[x1], x - x0);
        }

        static float MeasureVerticalSeam(Terrain left, Terrain right)
        {
            float maxErr = 0f;
            for (int i = 0; i < 32; i++)
            {
                float u = i / 31f;
                float z = left.transform.position.z + u * left.terrainData.size.z;
                maxErr = Mathf.Max(maxErr,
                    Mathf.Abs(SampleEdgeWorldY(left, true, z) - SampleEdgeWorldY(right, false, z)));
            }

            return maxErr;
        }

        static float MeasureHorizontalSeam(Terrain south, Terrain north)
        {
            float maxErr = 0f;
            for (int i = 0; i < 32; i++)
            {
                float u = i / 31f;
                float x = south.transform.position.x + u * south.terrainData.size.x;
                maxErr = Mathf.Max(maxErr,
                    Mathf.Abs(SampleEdgeWorldX(south, true, x) - SampleEdgeWorldX(north, false, x)));
            }

            return maxErr;
        }

        static List<float> Cluster(List<float> values, float tol)
        {
            var sorted = values.OrderBy(v => v).ToList();
            var centers = new List<float>();
            foreach (var v in sorted)
            {
                if (centers.Count == 0 || Mathf.Abs(v - centers[centers.Count - 1]) > tol)
                    centers.Add(v);
                else
                    centers[centers.Count - 1] = (centers[centers.Count - 1] + v) * 0.5f;
            }

            return centers;
        }

        static int NearestIndex(List<float> centers, float v)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < centers.Count; i++)
            {
                float d = Mathf.Abs(centers[i] - v);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return best;
        }

        static string MakeSafe(string name)
        {
            var chars = name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
            string s = new string(chars).Trim('_');
            return string.IsNullOrEmpty(s) ? "Terrain" : s;
        }

        static void MarkDirty()
        {
            if (!EditorApplication.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}
