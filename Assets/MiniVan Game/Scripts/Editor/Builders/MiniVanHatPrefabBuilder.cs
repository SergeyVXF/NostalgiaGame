using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Bakes the procedural hat library into editable assets: one mesh, a set of materials,
    /// a model prefab and a ready to place world pickup per hat.
    /// </summary>
    public static class MiniVanHatPrefabBuilder
    {
        public const string MaterialsFolder = "Assets/MiniVan Game/Materials/Cosmetics";
        public const string MeshFolder = "Assets/MiniVan Game/Models/Cosmetics";
        public const string ModelsFolder = "Assets/MiniVan Game/Resources/MiniVan/Cosmetics";
        public const string PickupsFolder = ModelsFolder + "/Pickups";

        private static readonly MiniVanInventoryItem[] Hats =
        {
            MiniVanInventoryItem.StrawHat,
            MiniVanInventoryItem.ChopperHat,
            MiniVanInventoryItem.AshCap,
            MiniVanInventoryItem.NarutoHeadband,
            MiniVanInventoryItem.LawHat,
            MiniVanInventoryItem.GokuHair,
            MiniVanInventoryItem.SuperSaiyanHair,
            MiniVanInventoryItem.MarioCap,
            MiniVanInventoryItem.VikingHelmet,
            MiniVanInventoryItem.PirateTricorn
        };

        [MenuItem("MiniVan Game/Equipment/Rebuild All Hats")]
        public static void RebuildAll()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(ModelsFolder);
            EnsureFolder(PickupsFolder);

            for (int i = 0; i < Hats.Length; i++)
            {
                Rebuild(Hats[i]);
            }

            BuildPickup(MiniVanInventoryItem.ZoroBandana);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Equipment] Rebuilt " + Hats.Length + " hats in " + ModelsFolder);
        }

        public static void Rebuild(MiniVanInventoryItem item)
        {
            MiniVanHatLibrary.ClearCache();
            MiniVanHatModel model = MiniVanHatLibrary.Get(item);
            if (!model.IsValid)
            {
                Debug.LogWarning("[Equipment] No procedural model for " + item);
                return;
            }

            Mesh mesh = SaveMesh(model.Mesh, MeshFolder + "/" + item + "_Mesh.asset");
            Material[] materials = new Material[model.Materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = LoadOrCreateMaterial(model.Materials[i], MaterialsFolder + "/" + item + "_" + model.Materials[i].Name + ".mat");
            }

            GameObject root = new GameObject(item.ToString());
            GameObject body = new GameObject("Model");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterials = materials;

            PrefabUtility.SaveAsPrefabAsset(root, ModelsFolder + "/" + item + ".prefab");
            Object.DestroyImmediate(root);

            BuildPickup(item);
        }

        /// <summary>World pickup with the model already visible in the editor, ready to drag onto the map.</summary>
        private static void BuildPickup(MiniVanInventoryItem item)
        {
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsFolder + "/" + item + ".prefab");
            if (modelPrefab == null)
            {
                return;
            }

            GameObject root = new GameObject(item + "Pickup");
            root.AddComponent<NetworkObject>();

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.25f, 0f);
            box.size = new Vector3(0.5f, 0.5f, 0.5f);

            MiniVanCosmeticPickup pickup = root.AddComponent<MiniVanCosmeticPickup>();
            pickup.Item = item;

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
            visual.name = MiniVanCosmeticPickup.VisualChildName;
            visual.transform.localPosition = new Vector3(0f, pickup.VisualHeight, 0f);
            visual.transform.localScale = Vector3.one * pickup.VisualScale;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PickupsFolder + "/" + item + "Pickup.prefab");
            Object.DestroyImmediate(root);

            MiniVanEquipmentUiBuilder.RegisterNetworkPrefab(prefab);
        }

        private static Mesh SaveMesh(Mesh source, string path)
        {
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset == null)
            {
                asset = new Mesh();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Clear();
            asset.indexFormat = source.indexFormat;
            asset.vertices = source.vertices;
            asset.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
            {
                asset.SetTriangles(source.GetTriangles(i), i, false);
            }

            asset.RecalculateNormals();
            asset.RecalculateBounds();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Material LoadOrCreateMaterial(MiniVanHatMaterial description, string path)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Material material = MiniVanCosmeticVisual.CreateHatMaterial(description);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
