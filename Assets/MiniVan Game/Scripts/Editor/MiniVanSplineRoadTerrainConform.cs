using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Raises/flattens terrain heightmap under a SplineExtrude road so gaps disappear.
    /// Menu: MiniVan/Terrain/Conform Terrain Under Spline Road
    /// </summary>
    public sealed class MiniVanSplineRoadTerrainConform : EditorWindow
    {
        const string PrefPrefix = "MiniVan.SplineRoadTerrainConform.";

        SplineExtrude extrude;
        float halfWidth = 15f;
        float sampleStep = 1.25f;
        float sinkBelowRoad = 0.08f;
        float shoulderWidth = 4f;
        bool affectAllSplines = true;
        int onlySplineIndex;
        bool raiseOnly = false;

        [MenuItem("MiniVan/Terrain/Conform Terrain Under Spline Road")]
        public static void Open()
        {
            var window = GetWindow<MiniVanSplineRoadTerrainConform>("Road → Terrain");
            window.minSize = new Vector2(360f, 280f);
            window.TryAutoAssign();
            window.Show();
        }

        [MenuItem("MiniVan/Terrain/Conform Terrain Under Selected/Focused Road")]
        public static void ConformQuick()
        {
            ConformActiveOrFirstRoad();
        }

        /// <summary>Editor/automation entry: conform selected or first SplineExtrude road.</summary>
        public static void ConformActiveOrFirstRoad()
        {
            var window = CreateInstance<MiniVanSplineRoadTerrainConform>();
            try
            {
                window.TryAutoAssign();
                if (window.extrude == null)
                {
                    Debug.LogError("[Road→Terrain] Select a SplineExtrude / SplineContainer road object first.");
                    return;
                }

                window.LoadPrefs();
                window.RunConform();
            }
            finally
            {
                DestroyImmediate(window);
            }
        }

        void OnEnable()
        {
            LoadPrefs();
            TryAutoAssign();
        }

        void OnDisable()
        {
            SavePrefs();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Conform terrain under spline road", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Поднимает/выравнивает heightmap под полотном дороги (SplineExtrude), чтобы не было щели. " +
                "Сначала выдели объект дороги или оставь авто-поиск.",
                MessageType.Info);

            extrude = (SplineExtrude)EditorGUILayout.ObjectField("Road (SplineExtrude)", extrude, typeof(SplineExtrude), true);
            if (GUILayout.Button("Find in scene / selection"))
            {
                TryAutoAssign();
            }

            EditorGUILayout.Space(6f);
            halfWidth = EditorGUILayout.Slider("Half width (m)", halfWidth, 1f, 40f);
            sampleStep = EditorGUILayout.Slider("Sample step (m)", sampleStep, 0.5f, 4f);
            sinkBelowRoad = EditorGUILayout.Slider("Sink below road (m)", sinkBelowRoad, 0f, 0.5f);
            shoulderWidth = EditorGUILayout.Slider("Shoulder blend (m)", shoulderWidth, 0f, 20f);
            raiseOnly = EditorGUILayout.Toggle(
                new GUIContent("Raise only", "Только поднимать terrain до дороги (не срезать холмы выше полотна)."),
                raiseOnly);

            affectAllSplines = EditorGUILayout.Toggle("All splines in container", affectAllSplines);
            using (new EditorGUI.DisabledScope(affectAllSplines))
            {
                onlySplineIndex = EditorGUILayout.IntField("Spline index", onlySplineIndex);
            }

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(extrude == null))
            {
                if (GUILayout.Button("Conform Terrain Now", GUILayout.Height(34f)))
                {
                    RunConform();
                }
            }
        }

        void TryAutoAssign()
        {
            if (Selection.activeGameObject != null)
            {
                extrude = Selection.activeGameObject.GetComponent<SplineExtrude>()
                    ?? Selection.activeGameObject.GetComponentInParent<SplineExtrude>()
                    ?? Selection.activeGameObject.GetComponentInChildren<SplineExtrude>();
            }

            if (extrude == null)
            {
                extrude = FindFirstObjectByType<SplineExtrude>();
            }

            if (extrude != null)
            {
                var so = new SerializedObject(extrude);
                var radiusProp = so.FindProperty("m_Radius");
                if (radiusProp != null && radiusProp.floatValue > 0.1f)
                {
                    halfWidth = radiusProp.floatValue;
                }
            }
        }

        void LoadPrefs()
        {
            halfWidth = EditorPrefs.GetFloat(PrefPrefix + "halfWidth", halfWidth);
            sampleStep = EditorPrefs.GetFloat(PrefPrefix + "sampleStep", sampleStep);
            sinkBelowRoad = EditorPrefs.GetFloat(PrefPrefix + "sink", sinkBelowRoad);
            shoulderWidth = EditorPrefs.GetFloat(PrefPrefix + "shoulder", shoulderWidth);
            raiseOnly = EditorPrefs.GetBool(PrefPrefix + "raiseOnly", raiseOnly);
            affectAllSplines = EditorPrefs.GetBool(PrefPrefix + "allSplines", true);
            onlySplineIndex = EditorPrefs.GetInt(PrefPrefix + "splineIndex", 0);
        }

        void SavePrefs()
        {
            EditorPrefs.SetFloat(PrefPrefix + "halfWidth", halfWidth);
            EditorPrefs.SetFloat(PrefPrefix + "sampleStep", sampleStep);
            EditorPrefs.SetFloat(PrefPrefix + "sink", sinkBelowRoad);
            EditorPrefs.SetFloat(PrefPrefix + "shoulder", shoulderWidth);
            EditorPrefs.SetBool(PrefPrefix + "raiseOnly", raiseOnly);
            EditorPrefs.SetBool(PrefPrefix + "allSplines", affectAllSplines);
            EditorPrefs.SetInt(PrefPrefix + "splineIndex", onlySplineIndex);
        }

        void RunConform()
        {
            if (extrude == null)
            {
                Debug.LogError("[Road→Terrain] No SplineExtrude.");
                return;
            }

            SplineContainer container = extrude.Container != null
                ? extrude.Container
                : extrude.GetComponent<SplineContainer>();
            if (container == null || container.Splines == null || container.Splines.Count == 0)
            {
                Debug.LogError("[Road→Terrain] SplineContainer missing or empty.");
                return;
            }

            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains == null || terrains.Length == 0)
            {
                Debug.LogError("[Road→Terrain] No terrains in scene.");
                return;
            }

            SavePrefs();

            float hardHalf = Mathf.Max(0.5f, halfWidth);
            float totalHalf = hardHalf + Mathf.Max(0f, shoulderWidth);
            float step = Mathf.Max(0.5f, sampleStep);

            // Lazy per-terrain height cache (only tiles the road actually touches).
            var cache = new Dictionary<Terrain, TerrainHeightCache>(32);
            var terrainList = new List<Terrain>(terrains.Length);
            foreach (Terrain terrain in terrains)
            {
                if (terrain != null && terrain.terrainData != null)
                {
                    terrainList.Add(terrain);
                }
            }

            int samples = 0;
            int writes = 0;
            int splineCount = container.Splines.Count;
            bool cancelled = false;

            try
            {
                for (int si = 0; si < splineCount; si++)
                {
                    if (!affectAllSplines && si != onlySplineIndex)
                    {
                        continue;
                    }

                    float length = math.max(0.01f, container.CalculateLength(si));
                    int segments = Mathf.Max(2, Mathf.CeilToInt(length / step));

                    for (int s = 0; s <= segments; s++)
                    {
                        float t = s / (float)segments;
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "Conform terrain under road",
                                $"Spline {si + 1}/{splineCount}  t={t:0.00}",
                                (si + t) / Mathf.Max(1, affectAllSplines ? splineCount : 1)))
                        {
                            cancelled = true;
                            break;
                        }

                        float3 pos3 = container.EvaluatePosition(si, t);
                        float3 tan3 = container.EvaluateTangent(si, t);
                        Vector3 pos = pos3;
                        Vector3 tangent = tan3;
                        if (tangent.sqrMagnitude < 1e-6f)
                        {
                            continue;
                        }

                        tangent.Normalize();
                        Vector3 right = Vector3.Cross(Vector3.up, tangent);
                        if (right.sqrMagnitude < 1e-6f)
                        {
                            float3 up3 = container.EvaluateUpVector(si, t);
                            right = Vector3.Cross((Vector3)up3, tangent);
                        }

                        if (right.sqrMagnitude < 1e-6f)
                        {
                            continue;
                        }

                        right.Normalize();
                        samples++;

                        float targetY = pos.y - sinkBelowRoad;
                        float lateralStep = Mathf.Clamp(step * 0.85f, 0.75f, 1.5f);
                        for (float lat = -totalHalf; lat <= totalHalf + 0.001f; lat += lateralStep)
                        {
                            Vector3 world = pos + right * lat;
                            Terrain terrain = FindTerrainAt(terrainList, world);
                            if (terrain == null)
                            {
                                continue;
                            }

                            if (!cache.TryGetValue(terrain, out TerrainHeightCache entry))
                            {
                                entry = new TerrainHeightCache(terrain);
                                cache.Add(terrain, entry);
                            }

                            if (!entry.WorldToHeightmap(world, out int hx, out int hz, out float currentNorm))
                            {
                                continue;
                            }

                            float targetNorm = entry.WorldYToNormalized(targetY);
                            float absLat = Mathf.Abs(lat);
                            float newNorm;
                            if (absLat <= hardHalf)
                            {
                                newNorm = targetNorm;
                            }
                            else if (shoulderWidth <= 0.001f)
                            {
                                continue;
                            }
                            else
                            {
                                float u = Mathf.InverseLerp(hardHalf, totalHalf, absLat);
                                float w = 1f - (u * u * (3f - 2f * u));
                                newNorm = Mathf.Lerp(currentNorm, targetNorm, w);
                            }

                            if (raiseOnly && newNorm < currentNorm)
                            {
                                newNorm = currentNorm;
                            }

                            if (entry.Set(hx, hz, newNorm))
                            {
                                writes++;
                            }
                        }
                    }

                    if (cancelled)
                    {
                        break;
                    }
                }

                if (cancelled)
                {
                    Debug.LogWarning("[Road→Terrain] Cancelled — no heights applied.");
                    return;
                }

                int terrainsTouched = 0;
                foreach (var kv in cache)
                {
                    if (!kv.Value.Dirty)
                    {
                        continue;
                    }

                    kv.Value.Apply();
                    terrainsTouched++;
                }

                Debug.Log(
                    $"[Road→Terrain] Done. samples={samples}, cellWrites={writes}, terrains={terrainsTouched}, " +
                    $"halfWidth={hardHalf:0.##}, shoulder={shoulderWidth:0.##}, sink={sinkBelowRoad:0.##}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static Terrain FindTerrainAt(List<Terrain> terrains, Vector3 world)
        {
            for (int i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                Vector3 p = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (world.x >= p.x && world.x <= p.x + size.x
                    && world.z >= p.z && world.z <= p.z + size.z)
                {
                    return terrain;
                }
            }

            return null;
        }

        sealed class TerrainHeightCache
        {
            readonly Terrain terrain;
            readonly TerrainData data;
            readonly Vector3 pos;
            readonly Vector3 size;
            readonly int res;
            readonly float[,] heights;
            public bool Dirty { get; private set; }

            public TerrainHeightCache(Terrain terrain)
            {
                this.terrain = terrain;
                data = terrain.terrainData;
                pos = terrain.transform.position;
                size = data.size;
                res = data.heightmapResolution;
                heights = data.GetHeights(0, 0, res, res);
            }

            public float WorldYToNormalized(float worldY)
            {
                return Mathf.Clamp01((worldY - pos.y) / Mathf.Max(0.001f, size.y));
            }

            public bool WorldToHeightmap(Vector3 world, out int hx, out int hz, out float currentNorm)
            {
                float u = (world.x - pos.x) / size.x;
                float v = (world.z - pos.z) / size.z;
                hx = Mathf.RoundToInt(u * (res - 1));
                hz = Mathf.RoundToInt(v * (res - 1));
                if (hx < 0 || hz < 0 || hx >= res || hz >= res)
                {
                    currentNorm = 0f;
                    return false;
                }

                currentNorm = heights[hz, hx];
                return true;
            }

            public bool Set(int hx, int hz, float norm)
            {
                norm = Mathf.Clamp01(norm);
                float prev = heights[hz, hx];
                if (Mathf.Abs(prev - norm) < 1e-5f)
                {
                    return false;
                }

                heights[hz, hx] = norm;
                Dirty = true;
                return true;
            }

            public void Apply()
            {
                Undo.RegisterCompleteObjectUndo(data, "Conform Terrain Under Road");
                data.SetHeights(0, 0, heights);
                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(terrain);
                terrain.Flush();
            }
        }
    }
}
