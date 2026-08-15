using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MiniVanGame
{
    public static class MiniVanPanelkaPrefabLibrary
    {
        public const string RootFolder = "Assets/MiniVan Game/Prefabs/Panelka";
        private const string LegacyBackWallId = "BACK_WALL";

        public static Transform InstantiateOrBuild(
            string category,
            string prefabName,
            Transform parent,
            Action<Transform> builder)
        {
#if UNITY_EDITOR
            EnsureFolder(RootFolder);
            string assetName = string.Equals(category, "Furniture", StringComparison.Ordinal)
                ? ResolveCanonicalFurniturePrefabName(prefabName)
                : prefabName;
            string categoryFolder = ResolveCategoryFolder(category, assetName);
            EnsureFolder(categoryFolder);
            string path = categoryFolder + "/" + Sanitize(assetName) + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                GameObject template = new GameObject(prefabName);
                builder(template.transform);
                if (string.Equals(category, "Furniture", StringComparison.Ordinal))
                {
                    EnsureFurniturePlacementAnchors(template.transform);
                    MiniVanPanelkaFurnitureInteractionUtility.Ensure(
                        template.transform, assetName);
                }

                prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
                UnityEngine.Object.DestroyImmediate(template);
                AssetDatabase.SaveAssets();
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab, parent);
            }

            instance.name = prefabName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            if (string.Equals(category, "Furniture", StringComparison.Ordinal))
            {
                EnsureFurniturePlacementAnchors(instance.transform);
                MiniVanPanelkaFurnitureInteractionUtility.Ensure(
                    instance.transform, assetName);
            }

            return instance.transform;
#else
            GameObject instance = new GameObject(prefabName);
            instance.transform.SetParent(parent, false);
            builder(instance.transform);
            if (string.Equals(category, "Furniture", StringComparison.Ordinal))
            {
                EnsureFurniturePlacementAnchors(instance.transform);
                MiniVanPanelkaFurnitureInteractionUtility.Ensure(
                    instance.transform,
                    ResolveCanonicalFurniturePrefabName(prefabName));
            }

            return instance.transform;
#endif
        }

        public static MiniVanPanelkaFurnitureAnchor EnsureFurnitureBackAnchor(Transform root)
        {
            return EnsureFurniturePlacementAnchors(root);
        }

        private static string ResolveCategoryFolder(string category, string prefabName)
        {
            if (string.Equals(category, "Furniture", StringComparison.Ordinal))
            {
                return RootFolder + "/Interiors/Furniture/" +
                       ResolveFurnitureRoomFolder(prefabName);
            }

            if (string.Equals(category, "HouseProps", StringComparison.Ordinal))
            {
                return RootFolder + "/Building/HouseProps";
            }

            return RootFolder + "/" + Sanitize(category);
        }

        private static string ResolveFurnitureRoomFolder(string prefabName)
        {
            if (StartsWithAny(prefabName, "Bathtub", "Pedestal_Sink"))
            {
                return "Bathroom";
            }

            if (StartsWithAny(prefabName, "Toilet"))
            {
                return "Toilet";
            }

            if (StartsWithAny(
                    prefabName,
                    "Kitchen_",
                    "Rounded_Soviet_Fridge",
                    "Soviet_Stove"))
            {
                return "Kitchen";
            }

            if (StartsWithAny(
                    prefabName,
                    "Hall_FullHeight_Wardrobe",
                    "Soviet_Entry_Hall_Rug",
                    "Soviet_Entryway_Set"))
            {
                return "Entryway";
            }

            if (StartsWithAny(
                    prefabName,
                    "FullHeight_Double_Wardrobe",
                    "LowPoly_Bed",
                    "Wardrobe"))
            {
                return "Bedroom";
            }

            if (StartsWithAny(
                    prefabName,
                    "CRT_Television",
                    "Pixel_Carpet",
                    "Soviet_Sofa",
                    "Soviet_Wall_Unit"))
            {
                return "LivingRoom";
            }

            if (StartsWithAny(prefabName, "Storage_Shelves"))
            {
                return "Storage";
            }

            return "Shared";
        }

        public static string ResolveCanonicalFurniturePrefabName(string prefabName)
        {
            string[] families =
            {
                "FullHeight_Double_Wardrobe",
                "Hall_FullHeight_Wardrobe",
                "Rounded_Soviet_Fridge",
                "Soviet_Entry_Hall_Rug",
                "Kitchen_Cabinet_Run",
                "Kitchen_Table_Set",
                "Soviet_Wall_Unit",
                "CRT_Television",
                "Soviet_Entryway_Set",
                "Storage_Shelves",
                "Pedestal_Sink",
                "Kitchen_Stool",
                "LowPoly_Bed",
                "Pixel_Carpet",
                "Soviet_Sofa",
                "Soviet_Stove",
                "Bathtub",
                "Toilet",
                "Wardrobe"
            };

            for (int i = 0; i < families.Length; i++)
            {
                if (prefabName.StartsWith(families[i], StringComparison.Ordinal))
                {
                    return families[i];
                }
            }

            return Sanitize(prefabName);
        }

        private static bool StartsWithAny(string value, params string[] prefixes)
        {
            if (string.IsNullOrEmpty(value) || prefixes == null)
            {
                return false;
            }

            for (int i = 0; i < prefixes.Length; i++)
            {
                if (value.StartsWith(prefixes[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static MiniVanPanelkaFurnitureAnchor EnsureFurniturePlacementAnchors(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            MiniVanPanelkaFurnitureAnchor marker =
                root.GetComponent<MiniVanPanelkaFurnitureAnchor>();
            if (marker == null)
            {
                marker = root.gameObject.AddComponent<MiniVanPanelkaFurnitureAnchor>();
            }

            Transform backAnchor = root.Find(MiniVanPanelkaFurnitureAnchor.BackWallId);
            if (backAnchor == null)
            {
                backAnchor = root.Find(LegacyBackWallId);
                if (backAnchor == null)
                {
                    GameObject anchorObject =
                        new GameObject(MiniVanPanelkaFurnitureAnchor.BackWallId);
                    backAnchor = anchorObject.transform;
                    backAnchor.SetParent(root, false);
                }
                else
                {
                    backAnchor.name = MiniVanPanelkaFurnitureAnchor.BackWallId;
                }
            }

            Transform frontAnchor = root.Find(MiniVanPanelkaFurnitureAnchor.FrontRoomId);
            if (frontAnchor == null)
            {
                GameObject anchorObject =
                    new GameObject(MiniVanPanelkaFurnitureAnchor.FrontRoomId);
                frontAnchor = anchorObject.transform;
                frontAnchor.SetParent(root, false);
            }

            Bounds localBounds;
            if (TryGetLocalRendererBounds(root, out localBounds))
            {
                backAnchor.localPosition = new Vector3(
                    localBounds.center.x,
                    0f,
                    localBounds.max.z);
                frontAnchor.localPosition = new Vector3(
                    localBounds.center.x,
                    0f,
                    localBounds.min.z);
            }
            else
            {
                backAnchor.localPosition = Vector3.zero;
                frontAnchor.localPosition = Vector3.back;
            }

            backAnchor.localRotation = Quaternion.identity;
            backAnchor.localScale = Vector3.one;
            frontAnchor.localRotation = Quaternion.identity;
            frontAnchor.localScale = Vector3.one;
            marker.Configure(backAnchor, frontAnchor);
            return marker;
        }

        private static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds rendererBounds = renderer.localBounds;
                Vector3 min = rendererBounds.min;
                Vector3 max = rendererBounds.max;
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 rendererLocalCorner = new Vector3(
                                x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z);
                            Vector3 worldCorner =
                                renderer.transform.TransformPoint(rendererLocalCorner);
                            Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                            if (!found)
                            {
                                bounds = new Bounds(localCorner, Vector3.zero);
                                found = true;
                            }
                            else
                            {
                                bounds.Encapsulate(localCorner);
                            }
                        }
                    }
                }
            }

            return found;
        }

#if UNITY_EDITOR
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
#endif

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unnamed";
            }

            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }
    }
}
