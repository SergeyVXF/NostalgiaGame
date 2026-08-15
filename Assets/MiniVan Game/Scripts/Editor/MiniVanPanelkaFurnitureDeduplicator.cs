using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.Editor
{
    public static class MiniVanPanelkaFurnitureDeduplicator
    {
        private const string Root =
            "Assets/MiniVan Game/Prefabs/Panelka/Interiors/Furniture";

        private static readonly string[] Sources =
        {
            Root + "/Bathroom/Bathtub_W252_D54.prefab",
            Root + "/LivingRoom/CRT_Television_W334_D38.prefab",
            Root + "/Entryway/FullHeight_Double_Wardrobe_R7_W251_D46.prefab",
            Root + "/Entryway/Hall_FullHeight_Wardrobe_R8.prefab",
            Root + "/Kitchen/Kitchen_Cabinet_Run_R7_W297_D42.prefab",
            Root + "/Kitchen/Kitchen_Stool.prefab",
            Root + "/Kitchen/Kitchen_Table_Set_W246_D38.prefab",
            Root + "/Bedroom/LowPoly_Bed_W251_D92.prefab",
            Root + "/Bathroom/Pedestal_Sink_W229_D38.prefab",
            Root + "/LivingRoom/Pixel_Carpet_W100_D147.prefab",
            Root + "/Kitchen/Rounded_Soviet_Fridge_R7_W246_D40.prefab",
            Root + "/Entryway/Soviet_Entry_Hall_Rug_V1.prefab",
            Root + "/Entryway/Soviet_Entryway_Set.prefab",
            Root + "/LivingRoom/Soviet_Sofa_W468_D62.prefab",
            Root + "/Kitchen/Soviet_Stove_W246_D40.prefab",
            Root + "/LivingRoom/Soviet_Wall_Unit_R7_W282_D32.prefab",
            Root + "/Storage/Storage_Shelves_W246_D42.prefab",
            Root + "/Toilet/Toilet_W229_D38.prefab",
            Root + "/Bedroom/Wardrobe_W251_D36.prefab"
        };

        private static readonly string[] Names =
        {
            "Bathtub", "CRT_Television", "FullHeight_Double_Wardrobe",
            "Hall_FullHeight_Wardrobe", "Kitchen_Cabinet_Run", "Kitchen_Stool",
            "Kitchen_Table_Set", "LowPoly_Bed", "Pedestal_Sink", "Pixel_Carpet",
            "Rounded_Soviet_Fridge", "Soviet_Entry_Hall_Rug", "Soviet_Entryway_Set",
            "Soviet_Sofa", "Soviet_Stove", "Soviet_Wall_Unit",
            "Storage_Shelves", "Toilet", "Wardrobe"
        };

        private static readonly string[] Rooms =
        {
            "Bathroom", "LivingRoom", "Bedroom", "Entryway", "Kitchen", "Kitchen",
            "Kitchen", "Bedroom", "Bathroom", "LivingRoom", "Kitchen", "Entryway",
            "Entryway", "LivingRoom", "Kitchen", "LivingRoom", "Storage", "Toilet",
            "Bedroom"
        };

        [MenuItem("MiniVan Game/Panelka/Deduplicate Furniture Prefabs")]
        public static void Deduplicate()
        {
            string[] targets = BuildTargetPaths();
            int moved = 0;
            int deleted = 0;
            int configured = 0;
            int failed = 0;

            for (int i = 0; i < Names.Length; i++)
            {
                if (Sources[i] == targets[i])
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(Sources[i]) == null)
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(targets[i]) != null &&
                    !AssetDatabase.DeleteAsset(targets[i]))
                {
                    Debug.LogError("[Furniture Deduplicator] Cannot replace " + targets[i]);
                    failed++;
                    continue;
                }

                string error = AssetDatabase.MoveAsset(Sources[i], targets[i]);
                if (string.IsNullOrEmpty(error))
                {
                    moved++;
                }
                else
                {
                    Debug.LogError("[Furniture Deduplicator] " + error);
                    failed++;
                }
            }

            string[] all = AssetDatabase.FindAssets("t:Prefab", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .ToArray();
            for (int i = 0; i < all.Length; i++)
            {
                if (Array.IndexOf(targets, all[i]) >= 0)
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(all[i]))
                {
                    deleted++;
                }
                else
                {
                    Debug.LogError("[Furniture Deduplicator] Cannot delete " + all[i]);
                    failed++;
                }
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (ConfigureCanonicalPrefab(targets[i], Names[i]))
                {
                    configured++;
                }
                else
                {
                    failed++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int remaining = AssetDatabase.FindAssets("t:Prefab", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Count();
            Debug.Log(
                "[Furniture Deduplicator] Complete. Moved=" + moved +
                ", deleted=" + deleted +
                ", configured=" + configured +
                ", remaining=" + remaining +
                ", failed=" + failed + ".");
        }

        private static string[] BuildTargetPaths()
        {
            string[] targets = new string[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                targets[i] = Root + "/" + Rooms[i] + "/" + Names[i] + ".prefab";
            }

            return targets;
        }

        private static bool ConfigureCanonicalPrefab(string path, string canonicalName)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
            {
                Debug.LogError("[Furniture Deduplicator] Missing canonical prefab " + path);
                return false;
            }

            try
            {
                Transform[] hierarchy = contents.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < hierarchy.Length; i++)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                        hierarchy[i].gameObject);
                }

                contents.name = canonicalName;
                MiniVanPanelkaPrefabLibrary.EnsureFurniturePlacementAnchors(
                    contents.transform);
                MiniVanPanelkaFurnitureInteractionUtility.Ensure(
                    contents.transform, canonicalName);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
