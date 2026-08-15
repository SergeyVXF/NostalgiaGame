// Authoring tools for the drop-anywhere ladder: a create menu, a rebuild button and a
// prefab exporter.
using MiniVanGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    public static class MiniVanUniversalLadderMenu
    {
        private const string LadderMaterialPath =
            "Assets/MiniVan Game/Materials/Vehicles/MiniVan/MVG_Ladder_DarkMetal.mat";

        private const string PrefabPath =
            "Assets/MiniVan Game/Prefabs/Props/UniversalLadder.prefab";

        [MenuItem("GameObject/MiniVan/Universal Ladder", false, 10)]
        public static void CreateFromHierarchy(MenuCommand command)
        {
            GameObject ladder = Create();
            GameObjectUtility.SetParentAndAlign(ladder, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(ladder, "Create Universal Ladder");
            Selection.activeGameObject = ladder;
        }

        [MenuItem("Tools/MiniVan/Ladder/Create Universal Ladder")]
        public static void CreateFromTools()
        {
            GameObject ladder = Create();
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                ladder.transform.position = view.pivot;
            }

            Undo.RegisterCreatedObjectUndo(ladder, "Create Universal Ladder");
            Selection.activeGameObject = ladder;
            EditorGUIUtility.PingObject(ladder);
        }

        [MenuItem("Tools/MiniVan/Ladder/Save Universal Ladder Prefab")]
        public static void SavePrefab()
        {
            GameObject ladder = Create();
            string directory = System.IO.Path.GetDirectoryName(PrefabPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            PrefabUtility.SaveAsPrefabAsset(ladder, PrefabPath);
            Object.DestroyImmediate(ladder);
            AssetDatabase.Refresh();

            Object asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[UniversalLadder] saved " + PrefabPath);
        }

        /// <summary>Builds a fully configured, unparented ladder at the origin.</summary>
        public static GameObject Create()
        {
            GameObject root = new GameObject("UniversalLadder");
            root.AddComponent<MiniVanLadder>();

            MiniVanUniversalLadder universal = root.AddComponent<MiniVanUniversalLadder>();
            universal.RailMaterial = AssetDatabase.LoadAssetAtPath<Material>(LadderMaterialPath);
            universal.Rebuild();

            ApplyStaticFlags(root);
            return root;
        }

        /// <summary>
        /// Static colliders are the cheap kind in PhysX and the rails batch away; lightmap
        /// contribution is left off because the boxes carry no lightmap UVs.
        /// </summary>
        public static void ApplyStaticFlags(GameObject root)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccludeeStatic;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                GameObjectUtility.SetStaticEditorFlags(all[i].gameObject, flags);
            }
        }
    }

    [CustomEditor(typeof(MiniVanUniversalLadder))]
    public class MiniVanUniversalLadderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            MiniVanUniversalLadder ladder = (MiniVanUniversalLadder)target;
            bool isInstance = PrefabUtility.IsPartOfPrefabInstance(ladder.gameObject) &&
                              !PrefabUtility.IsPartOfPrefabAsset(ladder.gameObject);

            EditorGUILayout.HelpBox(
                "Put the root at the FOOT of the ladder. Local +Z is the climbing side, " +
                "local -Z is the wall and the landing you step onto at the top. " +
                "Set Height to the landing height and rotate so +Z faces open space.",
                MessageType.Info);

            if (isInstance)
            {
                EditorGUILayout.HelpBox(
                    "This is a prefab instance, so the shape cannot be rebuilt here. " +
                    "Edit Height/Width in Prefab Mode, or unpack the instance " +
                    "(right-click > Prefab > Unpack) to shape this one on its own.",
                    MessageType.Warning);
            }

            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(isInstance))
            {
                if (GUILayout.Button("Rebuild Ladder"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(ladder.gameObject, "Rebuild Ladder");
                    ladder.Rebuild();
                    MiniVanUniversalLadderMenu.ApplyStaticFlags(ladder.gameObject);
                    EditorSceneManager.MarkSceneDirty(ladder.gameObject.scene);
                }
            }
        }
    }
}
