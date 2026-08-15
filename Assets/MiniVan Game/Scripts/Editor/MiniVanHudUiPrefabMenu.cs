using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MiniVanGame.EditorTools
{
    public static class MiniVanHudUiPrefabMenu
    {
        public const string PrefabPath = "Assets/MiniVan Game/Resources/MiniVanHUD/MiniVanHUD.prefab";
        private const string MenuPath = "MiniVan/HUD/Rebuild MiniVan HUD Prefab";

        [MenuItem(MenuPath)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MiniVanHUD] Exit Play Mode before rebuilding HUD prefab.");
                return;
            }

            BuildSilent(force: true);
        }

        [MenuItem("MiniVan/HUD/Add Enemy Combat Bar To Existing MiniVan HUD")]
        public static void AddEnemyCombatToExisting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MiniVanHUD] Exit Play Mode before editing HUD prefab.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                MiniVanHudUi ui = contents.GetComponent<MiniVanHudUi>();
                if (ui == null)
                {
                    Debug.LogError("[MiniVanHUD] MiniVanHudUi missing on prefab.");
                    return;
                }

                if (ui.EnemyCombatRoot != null && ui.EnemyCombatNameLabel != null && ui.EnemyCombatHealthFill != null)
                {
                    Debug.Log("[MiniVanHUD] Enemy combat bar already present.");
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                    return;
                }

                Transform old = contents.transform.Find("EnemyCombatCanvas");
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                    ui.EnemyCombatCanvas = null;
                    ui.EnemyCombatRoot = null;
                    ui.EnemyCombatNameLabel = null;
                    ui.EnemyCombatHealthFill = null;
                    ui.EnemyCombatHealthBackground = null;
                }

                ui.EnemyCombatCanvas = CreateEnemyCombatCanvas(contents.transform);
                MiniVanHudUiBuilder.BuildEnemyCombat(ui);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                Debug.Log("[MiniVanHUD] Enemy combat bar added. Edit EnemyCombatCanvas / EnemyCombatPanel on the prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Canvas CreateEnemyCombatCanvas(Transform root)
        {
            GameObject go = new GameObject("EnemyCombatCanvas", typeof(RectTransform));
            go.transform.SetParent(root, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 26;
            // Root MiniVanHUD CanvasScaler drives reference resolution.
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void BuildSilent(bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            string folder = "Assets/MiniVan Game/Resources/MiniVanHUD";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/MiniVan Game/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets/MiniVan Game", "Resources");
                }

                AssetDatabase.CreateFolder("Assets/MiniVan Game/Resources", "MiniVanHUD");
            }

            MiniVanHudUi ui = MiniVanHudUiBuilder.Build();
            PrefabUtility.SaveAsPrefabAsset(ui.gameObject, PrefabPath);
            Object.DestroyImmediate(ui.gameObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log("[MiniVanHUD] Editable HUD prefab ready: " + PrefabPath);
        }
    }
}
