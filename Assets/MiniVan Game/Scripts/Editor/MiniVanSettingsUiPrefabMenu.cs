using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    public static class MiniVanSettingsUiPrefabMenu
    {
        public const string PrefabPath = "Assets/MiniVan Game/Resources/MiniVanSettings/MiniVanSettings.prefab";
        private const string MenuPath = "MiniVan/Menu/Rebuild Settings Prefab";

        [MenuItem(MenuPath)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MiniVanSettings] Exit Play Mode before rebuilding.");
                return;
            }

            BuildSilent(force: true);
        }

        public static void BuildSilent(bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            string folder = "Assets/MiniVan Game/Resources/MiniVanSettings";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/MiniVan Game/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets/MiniVan Game", "Resources");
                }

                AssetDatabase.CreateFolder("Assets/MiniVan Game/Resources", "MiniVanSettings");
            }

            MiniVanSettingsUi ui = MiniVanSettingsUiBuilder.Build();
            PrefabUtility.SaveAsPrefabAsset(ui.gameObject, PrefabPath);
            Object.DestroyImmediate(ui.gameObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MiniVanSettings] Prefab ready: " + PrefabPath);
        }
    }
}
