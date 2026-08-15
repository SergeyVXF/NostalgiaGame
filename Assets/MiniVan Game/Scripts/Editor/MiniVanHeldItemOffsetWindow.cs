using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    public sealed class MiniVanHeldItemOffsetWindow : EditorWindow
    {
        private const string PlayerPrefabPath = MiniVanPlayerVisualBuilder.PlayerPrefabPath;
        private Vector2 scroll;

        [MenuItem("MiniVan Game/Characters/Held Item Offsets")]
        public static void Open()
        {
            MiniVanHeldItemOffsetWindow window = GetWindow<MiniVanHeldItemOffsetWindow>("Held Item Offsets");
            window.minSize = new Vector2(360f, 420f);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            MiniVanPlayer live = FindLivePlayer();
            MiniVanPlayer prefab = LoadPrefabPlayer();
            MiniVanPlayer target = live != null ? live : prefab;
            if (target == null)
            {
                EditorGUILayout.HelpBox("MiniVanPlayer prefab not found.", MessageType.Error);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (live != null)
            {
                EditorGUILayout.HelpBox(
                    "Play Mode: values apply immediately on the live player. Press Save to Prefab to keep them.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Edit Mode: you are editing the MiniVanPlayer prefab. Enter Play Mode to see the grip on the character.",
                    MessageType.Info);
            }

            SerializedObject so = new SerializedObject(target);
            so.Update();
            DrawOffset(so, "BatHandLocalPosition", "BatHandLocalRotation", "Bat");
            DrawOffset(so, "StakeHandLocalPosition", "StakeHandLocalRotation", "Stake");
            DrawOffset(so, "CrossHandLocalPosition", "CrossHandLocalRotation", "Cross (idle / walk)");
            DrawOffset(so, "CrossHandRaisedPosition", "CrossHandRaisedRotation", "Cross (LMB held)");
            so.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(live == null))
            {
                if (GUILayout.Button("Save Live Values To Prefab", GUILayout.Height(28f)))
                {
                    SaveLiveToPrefab(live, prefab);
                }
            }

            if (GUILayout.Button("Select MiniVanPlayer Prefab"))
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (prefabAsset != null)
                {
                    Selection.activeObject = prefabAsset;
                    EditorGUIUtility.PingObject(prefabAsset);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawOffset(SerializedObject so, string posName, string rotName, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            SerializedProperty pos = so.FindProperty(posName);
            SerializedProperty rot = so.FindProperty(rotName);
            if (pos != null)
            {
                EditorGUILayout.PropertyField(pos, new GUIContent("Position"));
            }

            if (rot != null)
            {
                EditorGUILayout.PropertyField(rot, new GUIContent("Rotation"));
            }

            EditorGUILayout.Space(6f);
        }

        private static MiniVanPlayer FindLivePlayer()
        {
            if (!EditorApplication.isPlaying)
            {
                return null;
            }

            MiniVanPlayer[] players = Object.FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsOwner)
                {
                    return players[i];
                }
            }

            return players.Length > 0 ? players[0] : null;
        }

        private static MiniVanPlayer LoadPrefabPlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return prefab != null ? prefab.GetComponent<MiniVanPlayer>() : null;
        }

        private static void SaveLiveToPrefab(MiniVanPlayer live, MiniVanPlayer prefab)
        {
            if (live == null || prefab == null)
            {
                return;
            }

            Undo.RecordObject(prefab, "Save held item offsets");
            prefab.BatHandLocalPosition = live.BatHandLocalPosition;
            prefab.BatHandLocalRotation = live.BatHandLocalRotation;
            prefab.StakeHandLocalPosition = live.StakeHandLocalPosition;
            prefab.StakeHandLocalRotation = live.StakeHandLocalRotation;
            prefab.CrossHandLocalPosition = live.CrossHandLocalPosition;
            prefab.CrossHandLocalRotation = live.CrossHandLocalRotation;
            prefab.CrossHandRaisedPosition = live.CrossHandRaisedPosition;
            prefab.CrossHandRaisedRotation = live.CrossHandRaisedRotation;
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab.gameObject);
            Debug.Log("[HeldItemOffsets] Saved live values to " + PlayerPrefabPath);
        }
    }
}
