using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Ensures FacePaintHUD prefab exists after scripts compile (no blocking dialogs).
    /// </summary>
    [InitializeOnLoad]
    public static class MiniVanFacePaintUiPrefabAutoBuild
    {
        public const string PrefabPath = "Assets/MiniVan Game/Resources/FacePaintUI/FacePaintHUD.prefab";

        static MiniVanFacePaintUiPrefabAutoBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                {
                    return;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                {
                    BuildSilent(force: false);
                }
            };
        }

        public static void BuildSilent(bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            string folder = "Assets/MiniVan Game/Resources/FacePaintUI";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/MiniVan Game/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets/MiniVan Game", "Resources");
                }

                AssetDatabase.CreateFolder("Assets/MiniVan Game/Resources", "FacePaintUI");
            }

            MiniVanFacePaintUi ui = MiniVanFacePaintUiBuilder.Build();
            ui.EditorAutoWire();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ui.gameObject, PrefabPath);
            Object.DestroyImmediate(ui.gameObject);

            MiniVanFacePaintUi uiComp = prefab != null ? prefab.GetComponent<MiniVanFacePaintUi>() : null;
            if (uiComp != null)
            {
                AssignToStations(uiComp);
            }

            AssetDatabase.SaveAssets();
            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log("[FacePaint] Editable HUD prefab ready: " + PrefabPath);
        }

        public static void AssignToStations(MiniVanFacePaintUi ui)
        {
            if (ui == null)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/MiniVan Game" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    continue;
                }

                MiniVanFacePaintStation[] stations = root.GetComponentsInChildren<MiniVanFacePaintStation>(true);
                for (int s = 0; s < stations.Length; s++)
                {
                    SerializedObject so = new SerializedObject(stations[s]);
                    SerializedProperty prop = so.FindProperty("UiPrefab");
                    if (prop == null)
                    {
                        continue;
                    }

                    prop.objectReferenceValue = ui;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(stations[s]);
                    EditorUtility.SetDirty(root);
                }
            }
        }
    }
}
