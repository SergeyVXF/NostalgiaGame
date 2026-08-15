using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Builds cosmetic models from primitives. One place to add new hats and clothes.
    /// </summary>
    public static class MiniVanCosmeticVisual
    {
        private static Material bandanaFabric;

        private static readonly System.Collections.Generic.Dictionary<MiniVanInventoryItem, Material[]> libraryMaterials =
            new System.Collections.Generic.Dictionary<MiniVanInventoryItem, Material[]>();

        public static Transform Build(MiniVanInventoryItem item, Transform parent, string name)
        {
            Transform fromPrefab = TryBuildFromPrefab(item, parent, name);
            if (fromPrefab != null)
            {
                return fromPrefab;
            }

            GameObject root = new GameObject(string.IsNullOrEmpty(name) ? item.ToString() : name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            switch (item)
            {
                case MiniVanInventoryItem.TestHat:
                    BuildTestHat(root.transform);
                    break;
                case MiniVanInventoryItem.ZoroBandana:
                    BuildZoroBandana(root.transform);
                    break;
                default:
                    if (MiniVanHatLibrary.HasModel(item))
                    {
                        BuildFromLibrary(root.transform, item);
                    }
                    else
                    {
                        BuildPlaceholder(root.transform);
                    }

                    break;
            }

            return root.transform;
        }

        /// <summary>Instantiates the editable model prefab when the item has one.</summary>
        private static Transform TryBuildFromPrefab(MiniVanInventoryItem item, Transform parent, string name)
        {
            string resource = MiniVanCosmeticCatalog.GetModelResource(item);
            if (string.IsNullOrEmpty(resource))
            {
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(resource);
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.name = string.IsNullOrEmpty(name) ? item.ToString() : name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            StripColliders(instance);
            return instance.transform;
        }

        /// <summary>Worn cosmetics are visuals only: they must never take part in physics.</summary>
        private static void StripColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(colliders[i]);
                }
                else
                {
                    Object.DestroyImmediate(colliders[i]);
                }
            }
        }

        /// <summary>Fallback for procedural hats that have not been baked into a prefab yet.</summary>
        private static void BuildFromLibrary(Transform root, MiniVanInventoryItem item)
        {
            MiniVanHatModel model = MiniVanHatLibrary.Get(item);
            if (!model.IsValid)
            {
                BuildPlaceholder(root);
                return;
            }

            if (!libraryMaterials.TryGetValue(item, out Material[] materials) || materials == null)
            {
                materials = new Material[model.Materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = CreateHatMaterial(model.Materials[i]);
                }

                libraryMaterials[item] = materials;
            }

            GameObject body = new GameObject(item.ToString());
            body.transform.SetParent(root, false);
            body.AddComponent<MeshFilter>().sharedMesh = model.Mesh;
            body.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        public static Material CreateHatMaterial(MiniVanHatMaterial description)
        {
            Material material = MakeMaterial(description.Color);
            material.name = description.Name;
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", description.Smoothness);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", description.Smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", description.Metallic);
            }

            return material;
        }

        private static void BuildZoroBandana(Transform root)
        {
            GameObject cloth = new GameObject("Bandana");
            cloth.transform.SetParent(root, false);

            MeshFilter filter = cloth.AddComponent<MeshFilter>();
            filter.sharedMesh = MiniVanZoroBandanaMesh.Get();

            if (bandanaFabric == null)
            {
                bandanaFabric = MakeMaterial(new Color(0.09f, 0.30f, 0.17f));
                bandanaFabric.name = "MiniVan_ZoroBandanaFabric";
                if (bandanaFabric.HasProperty("_Smoothness"))
                {
                    bandanaFabric.SetFloat("_Smoothness", 0.12f);
                }

                if (bandanaFabric.HasProperty("_Metallic"))
                {
                    bandanaFabric.SetFloat("_Metallic", 0f);
                }
            }

            cloth.AddComponent<MeshRenderer>().sharedMaterial = bandanaFabric;
        }

        private static void BuildTestHat(Transform root)
        {
            Material felt = MakeMaterial(new Color(0.55f, 0.09f, 0.12f));
            Material band = MakeMaterial(new Color(0.92f, 0.78f, 0.25f));

            GameObject crown = AddPrimitive(PrimitiveType.Cylinder, "Crown", root);
            crown.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            crown.transform.localScale = new Vector3(0.34f, 0.13f, 0.34f);
            SetMaterial(crown, felt);

            GameObject top = AddPrimitive(PrimitiveType.Cylinder, "CrownTop", root);
            top.transform.localPosition = new Vector3(0f, 0.26f, 0f);
            top.transform.localScale = new Vector3(0.3f, 0.015f, 0.3f);
            SetMaterial(top, band);

            GameObject ribbon = AddPrimitive(PrimitiveType.Cylinder, "Ribbon", root);
            ribbon.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            ribbon.transform.localScale = new Vector3(0.36f, 0.028f, 0.36f);
            SetMaterial(ribbon, band);

            GameObject brim = AddPrimitive(PrimitiveType.Cylinder, "Brim", root);
            brim.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            brim.transform.localScale = new Vector3(0.58f, 0.012f, 0.58f);
            SetMaterial(brim, felt);
        }

        private static void BuildPlaceholder(Transform root)
        {
            GameObject box = AddPrimitive(PrimitiveType.Cube, "Placeholder", root);
            box.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            box.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            SetMaterial(box, MakeMaterial(new Color(0.8f, 0.2f, 0.8f)));
        }

        private static GameObject AddPrimitive(PrimitiveType type, string name, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(collider);
                }
                else
                {
                    Object.DestroyImmediate(collider);
                }
            }

            return go;
        }

        private static void SetMaterial(GameObject go, Material material)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }
    }
}
