using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    public static class MiniVanMainMenuUiPrefabMenu
    {
        public const string PrefabPath = "Assets/MiniVan Game/Resources/MiniVanMainMenu/MiniVanMainMenu.prefab";
        private const string MenuPath = "MiniVan/Menu/Rebuild Main Menu Prefab";
        private const string HoverFxMenuPath = "MiniVan/Menu/Apply Button Hover FX";

        [MenuItem(HoverFxMenuPath)]
        public static void ApplyButtonHoverFx()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MiniVanMainMenu] Exit Play Mode before editing the menu prefab.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("[MiniVanMainMenu] Prefab not found: " + PrefabPath);
                return;
            }

            try
            {
                MiniVanMainMenuUi ui = root.GetComponent<MiniVanMainMenuUi>();
                if (ui == null)
                {
                    Debug.LogError("[MiniVanMainMenu] MiniVanMainMenuUi component missing on prefab root.");
                    return;
                }

                MiniVanMenuButtonHoverFx.Attach(ui.MainCreateButton);
                MiniVanMenuButtonHoverFx.Attach(ui.MainJoinButton);
                MiniVanMenuButtonHoverFx.Attach(ui.MainQuitButton);
                MiniVanMenuButtonHoverFx.Attach(ui.MainSettingsButton);

                MiniVanMenuButtonHoverFx.Attach(ui.CreateBackButton);
                MiniVanMenuButtonHoverFx.Attach(ui.CreateConfirmButton);
                if (ui.MapButtons != null)
                {
                    foreach (UnityEngine.UI.Button mapButton in ui.MapButtons)
                    {
                        MiniVanMenuButtonHoverFx.Attach(mapButton);
                    }
                }

                MiniVanMenuButtonHoverFx.Attach(ui.JoinBackButton);
                MiniVanMenuButtonHoverFx.Attach(ui.JoinRefreshButton);
                if (ui.RoomRowTemplate != null)
                {
                    MiniVanMenuButtonHoverFx.Attach(ui.RoomRowTemplate.GetComponent<UnityEngine.UI.Button>());
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[MiniVanMainMenu] Button hover FX applied to: " + PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(MenuPath)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MiniVanMainMenu] Exit Play Mode before rebuilding menu prefab.");
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

            string folder = "Assets/MiniVan Game/Resources/MiniVanMainMenu";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/MiniVan Game/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets/MiniVan Game", "Resources");
                }

                AssetDatabase.CreateFolder("Assets/MiniVan Game/Resources", "MiniVanMainMenu");
            }

            MiniVanMainMenuUi ui = MiniVanMainMenuUiBuilder.Build();
            PrefabUtility.SaveAsPrefabAsset(ui.gameObject, PrefabPath);
            Object.DestroyImmediate(ui.gameObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log("[MiniVanMainMenu] Prefab ready: " + PrefabPath);
        }
    }
}
