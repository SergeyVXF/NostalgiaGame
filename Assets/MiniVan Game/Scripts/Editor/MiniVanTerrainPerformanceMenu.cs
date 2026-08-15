using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniVanGame.Editor
{
    public static class MiniVanTerrainPerformanceMenu
    {
        [MenuItem("MiniVan Game/Performance/Apply Terrain Preset To Open Scene")]
        public static void ApplyTerrainPresetToOpenScene()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                Undo.RecordObject(terrain, "Apply MiniVan Terrain Preset");
                terrain.drawInstanced = true;
                terrain.drawTreesAndFoliage = false;
                terrain.treeDistance = 0f;
                terrain.detailObjectDistance = 0f;
                terrain.detailObjectDensity = 0f;
                terrain.treeBillboardDistance = 0f;
                terrain.treeMaximumFullLODCount = 0;
                terrain.heightmapPixelError = 8f;
                terrain.heightmapMaximumLOD = 0;
                terrain.basemapDistance = 200f;
                terrain.shadowCastingMode = ShadowCastingMode.Off;
                terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
                EditorUtility.SetDirty(terrain);
                count++;
            }

            MiniVanGameModeWorldGenerator world =
                Object.FindFirstObjectByType<MiniVanGameModeWorldGenerator>(
                    FindObjectsInactive.Include);
            if (world != null &&
                world.GetComponent<MiniVanGameModeRenderOptimizer>() == null)
            {
                Undo.AddComponent<MiniVanGameModeRenderOptimizer>(world.gameObject);
            }

            if (world != null)
            {
                EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            }

            Debug.Log("[MiniVan Performance] Terrain preset applied to " + count + " terrains.");
        }
    }
}
