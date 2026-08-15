using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Makes sure the layer used to hide worn cosmetics from their owner's own camera exists.
    /// </summary>
    [InitializeOnLoad]
    public static class MiniVanCosmeticLayerSetup
    {
        static MiniVanCosmeticLayerSetup()
        {
            EnsureLayer();
        }

        [MenuItem("MiniVan Game/Equipment/Ensure Cosmetic Layer")]
        public static void EnsureLayer()
        {
            if (LayerMask.NameToLayer(MiniVanCosmeticCatalog.OwnerHiddenLayerName) >= 0)
            {
                return;
            }

            Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject tagManager = new SerializedObject(tagManagerAsset);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = MiniVanCosmeticCatalog.OwnerHiddenLayerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("[Equipment] Added layer " + i + " '" + MiniVanCosmeticCatalog.OwnerHiddenLayerName + "'.");
                return;
            }

            Debug.LogError("[Equipment] No free layer slot for '" + MiniVanCosmeticCatalog.OwnerHiddenLayerName + "'.");
        }
    }
}
