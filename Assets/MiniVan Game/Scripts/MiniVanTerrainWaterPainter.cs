using UnityEngine;

namespace MiniVanGame
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MiniVanTerrainWaterPainter : MonoBehaviour
    {
        public Transform CenterOverride;
        public TerrainLayer WaterTerrainLayer;
        public float Radius = 6.5f;
        public float Feather = 1.6f;
        [Range(0f, 1f)] public float Opacity = 0.72f;
        public bool PaintInEditMode = true;
        public bool PaintInPlayMode = true;
        public bool PaintOnEnable = true;
        public bool DebugPaintWarnings;

        private Vector3 lastPaintPosition = new Vector3(float.PositiveInfinity, 0f, 0f);
        private float lastRadius = -1f;
        private float lastOpacity = -1f;
        private bool warnedNoTerrain;

        private void OnEnable()
        {
            if (PaintOnEnable)
            {
                PaintIfNeeded(true);
            }
        }

        private void OnValidate()
        {
            Radius = Mathf.Max(0.1f, Radius);
            Feather = Mathf.Max(0.01f, Feather);

            if (enabled && PaintOnEnable)
            {
                PaintIfNeeded(true);
            }
        }

        private void Update()
        {
            if (!PaintOnEnable)
            {
                return;
            }

            PaintIfNeeded(false);
        }

        public void PaintNow()
        {
            PaintIfNeeded(true);
        }

        private void PaintIfNeeded(bool force)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(gameObject))
            {
                return;
            }
#endif

            if (WaterTerrainLayer == null)
            {
                return;
            }

            if (Application.isPlaying && !PaintInPlayMode)
            {
                return;
            }

            if (!Application.isPlaying && !PaintInEditMode)
            {
                return;
            }

            Vector3 center = GetCenter();
            if (!force &&
                Vector3.SqrMagnitude(center - lastPaintPosition) < 0.04f &&
                Mathf.Abs(lastRadius - Radius) < 0.01f &&
                Mathf.Abs(lastOpacity - Opacity) < 0.01f)
            {
                return;
            }

            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
            {
                WarnNoTerrain(center);
                return;
            }

            bool painted = false;
            for (int i = 0; i < terrains.Length; i++)
            {
                if (PaintTerrain(terrains[i], center))
                {
                    painted = true;
                }
            }

            if (painted)
            {
                warnedNoTerrain = false;
                lastPaintPosition = center;
                lastRadius = Radius;
                lastOpacity = Opacity;
            }
            else
            {
                WarnNoTerrain(center);
            }
        }

        private void WarnNoTerrain(Vector3 center)
        {
            if (!DebugPaintWarnings || warnedNoTerrain)
            {
                return;
            }

            warnedNoTerrain = true;
            Debug.LogWarning(
                "[MiniVanTerrainWaterPainter] No Terrain under electrified water area at " +
                center.ToString("F2") +
                ". The wet-water splat layer is painted only on Unity Terrain; mesh floors need a separate visual.");
        }

        private Vector3 GetCenter()
        {
            return CenterOverride != null ? CenterOverride.position : transform.position;
        }

        private bool PaintTerrain(Terrain terrain, Vector3 center)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return false;
            }

            TerrainData data = terrain.terrainData;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 localCenter = center - terrainPosition;

            if (localCenter.x < -Radius ||
                localCenter.z < -Radius ||
                localCenter.x > data.size.x + Radius ||
                localCenter.z > data.size.z + Radius)
            {
                return false;
            }

            int layerIndex = EnsureTerrainLayer(data);
            if (layerIndex < 0)
            {
                return false;
            }

            int alphaWidth = data.alphamapWidth;
            int alphaHeight = data.alphamapHeight;
            int xMin = Mathf.Clamp(Mathf.FloorToInt((localCenter.x - Radius) / data.size.x * alphaWidth), 0, alphaWidth - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt((localCenter.x + Radius) / data.size.x * alphaWidth), 0, alphaWidth - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt((localCenter.z - Radius) / data.size.z * alphaHeight), 0, alphaHeight - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt((localCenter.z + Radius) / data.size.z * alphaHeight), 0, alphaHeight - 1);
            int width = xMax - xMin + 1;
            int height = yMax - yMin + 1;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            float[,,] maps = data.GetAlphamaps(xMin, yMin, width, height);
            int layerCount = data.alphamapLayers;
            float innerRadius = Mathf.Max(0.01f, Radius - Feather);
            bool changed = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float worldX = terrainPosition.x + (xMin + x + 0.5f) / alphaWidth * data.size.x;
                    float worldZ = terrainPosition.z + (yMin + y + 0.5f) / alphaHeight * data.size.z;
                    float distance = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(center.x, center.z));
                    if (distance > Radius)
                    {
                        continue;
                    }

                    float edge = Mathf.InverseLerp(Radius, innerRadius, distance);
                    float target = Mathf.Clamp01(Opacity * edge);
                    if (maps[y, x, layerIndex] >= target)
                    {
                        continue;
                    }

                    float remaining = Mathf.Clamp01(1f - target);
                    float otherSum = 0f;
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        if (layer != layerIndex)
                        {
                            otherSum += maps[y, x, layer];
                        }
                    }

                    if (otherSum <= 0.0001f)
                    {
                        float split = remaining / Mathf.Max(1, layerCount - 1);
                        for (int layer = 0; layer < layerCount; layer++)
                        {
                            maps[y, x, layer] = layer == layerIndex ? target : split;
                        }
                    }
                    else
                    {
                        for (int layer = 0; layer < layerCount; layer++)
                        {
                            maps[y, x, layer] = layer == layerIndex
                                ? target
                                : maps[y, x, layer] / otherSum * remaining;
                        }
                    }

                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Paint electrified water terrain");
            }
#endif
            data.SetAlphamaps(xMin, yMin, maps);
            terrain.Flush();
            return true;
        }

        private int EnsureTerrainLayer(TerrainData data)
        {
            TerrainLayer[] layers = data.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == WaterTerrainLayer)
                {
                    return i;
                }
            }

            TerrainLayer[] expanded = new TerrainLayer[layers.Length + 1];
            for (int i = 0; i < layers.Length; i++)
            {
                expanded[i] = layers[i];
            }

            expanded[expanded.Length - 1] = WaterTerrainLayer;
            data.terrainLayers = expanded;
            return expanded.Length - 1;
        }
    }
}
