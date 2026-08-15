using UnityEditor;
using UnityEngine;

namespace MiniVanGame.Editor
{
    [CustomEditor(typeof(MiniVanPanelkaManualBuilder))]
    public sealed class MiniVanPanelkaManualBuilderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            MiniVanPanelkaManualBuilder builder = (MiniVanPanelkaManualBuilder)target;

            EditorGUILayout.HelpBox(
                "Поставь объект куда нужно, выставь параметры, затем «Собрать панельку».\n" +
                "Открытые подъезды: First From Left или список номеров (с 1).\n" +
                "Шаблоны 1..5 — индексы каталога квартир. Auto Rebuild пересобирает при правках.",
                MessageType.Info);

            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
                if (GUILayout.Button("Случайный seed", GUILayout.Height(28f)))
                {
                    Undo.RecordObject(builder, "Randomize Manual Panelka Seed");
                    builder.RandomizeSeed();
                    EditorUtility.SetDirty(builder);
                }

                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
                if (GUILayout.Button("Собрать панельку", GUILayout.Height(28f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Rebuild Manual Panelka");
                    builder.Rebuild();
                }

                GUI.backgroundColor = new Color(0.95f, 0.55f, 0.45f);
                if (GUILayout.Button("Очистить", GUILayout.Height(28f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Clear Manual Panelka");
                    builder.ClearGenerated();
                }

                GUI.backgroundColor = Color.white;
            }

            if (!string.IsNullOrEmpty(builder.LastBuildPreview))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Превью последней сборки", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(builder.LastBuildPreview, MessageType.None);
            }
        }
    }
}
