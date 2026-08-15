using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    public static class MiniVanFacePaintUiPrefabMenu
    {
        private const string MenuPath = "MiniVan/Face Paint/Rebuild HUD Prefab";

        [MenuItem(MenuPath)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[FacePaint] Exit Play Mode before rebuilding HUD prefab.");
                return;
            }

            // Force rebuild even if prefab already exists.
            MiniVanFacePaintUiPrefabAutoBuild.BuildSilent(force: true);
        }
    }
}
