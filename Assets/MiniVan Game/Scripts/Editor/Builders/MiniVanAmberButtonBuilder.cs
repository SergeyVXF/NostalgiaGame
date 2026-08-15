using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    public static class MiniVanAmberButtonBuilder
    {
        public const string ArtFbx = "Assets/MiniVan Game/Art/AmberButton/MiniVan_AmberButton.fbx";
        public const string MeshAssetPath = "Assets/MiniVan Game/Art/AmberButton/MiniVan_AmberButton_Welded.asset";
        public const string PrefabFolder = "Assets/MiniVan Game/Prefabs/Collectibles/AmberButton";
        public const string PrefabPath = PrefabFolder + "/AmberButtonPickup.prefab";
        public const string ResourcesFolder = "Assets/MiniVan Game/Resources/Collectibles";
        public const string ResourcesPrefabPath = ResourcesFolder + "/AmberButtonPickup.prefab";
        public const string MaterialPath = "Assets/MiniVan Game/Art/AmberButton/MVG_AmberButton.mat";

        [MenuItem("MiniVan/Collectibles/Build Amber Button Pickup")]
        public static void BuildPrefab()
        {
            EnsureFolders();
            Material amber = EnsureAmberMaterial();
            Mesh welded = BuildWeldedMeshAsset();
            if (welded == null)
            {
                Debug.LogError("[AmberButton] Failed to build welded mesh from " + ArtFbx);
                return;
            }

            GameObject root = new GameObject("AmberButtonPickup");
            try
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 0.22f, 0f);
                box.size = new Vector3(0.55f, 0.55f, 0.55f);

                root.AddComponent<NetworkObject>();
                MiniVanAmberButtonPickup pickup = root.AddComponent<MiniVanAmberButtonPickup>();

                GameObject visual = new GameObject(MiniVanAmberButtonPickup.VisualChildName);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, pickup.VisualHeight, 0f);
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                MeshFilter filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = welded;
                MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = amber;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.DeleteAsset(ResourcesPrefabPath);
                AssetDatabase.CopyAsset(PrefabPath, ResourcesPrefabPath);

                GameObject registered = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                MiniVanEquipmentUiBuilder.RegisterNetworkPrefab(registered);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                if (registered != null)
                {
                    EditorGUIUtility.PingObject(registered);
                }

                Debug.Log("[AmberButton] Prefab ready: " + PrefabPath + " weldedVerts=" + welded.vertexCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh BuildWeldedMeshAsset()
        {
            GameObject meshSource = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFbx);
            if (meshSource == null)
            {
                return null;
            }

            MeshFilter sourceFilter = meshSource.GetComponentInChildren<MeshFilter>();
            Mesh source = sourceFilter != null ? sourceFilter.sharedMesh : null;
            if (source == null)
            {
                // FBX sometimes stores meshes as sub-assets only.
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ArtFbx);
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Mesh mesh)
                    {
                        source = mesh;
                        break;
                    }
                }
            }

            if (source == null)
            {
                return null;
            }

            Mesh welded = WeldByPositionAndSmoothNormals(source);
            welded.name = "MiniVan_AmberButton_Welded";

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(welded, MeshAssetPath);
            }
            else
            {
                EditorUtility.CopySerialized(welded, existing);
                Object.DestroyImmediate(welded);
                welded = existing;
                EditorUtility.SetDirty(welded);
            }

            AssetDatabase.SaveAssets();
            return welded;
        }

        /// <summary>
        /// Inverted-hull outlines need shared verts + averaged normals.
        /// FBX hard-edges/UVs otherwise explode the outline into flying panels.
        /// </summary>
        private static Mesh WeldByPositionAndSmoothNormals(Mesh source)
        {
            Vector3[] srcPos = source.vertices;
            Vector3[] srcNorm = source.normals;
            Vector2[] srcUv = source.uv;
            int[] srcTris = source.triangles;

            var keyToNew = new Dictionary<Vector3Int, int>();
            var positions = new List<Vector3>(srcPos.Length / 2);
            var normalAccum = new List<Vector3>(srcPos.Length / 2);
            var uvAccum = new List<Vector2>(srcPos.Length / 2);
            var uvCount = new List<int>(srcPos.Length / 2);
            var remap = new int[srcPos.Length];

            for (int i = 0; i < srcPos.Length; i++)
            {
                Vector3Int key = Quantize(srcPos[i]);
                if (!keyToNew.TryGetValue(key, out int newIndex))
                {
                    newIndex = positions.Count;
                    keyToNew.Add(key, newIndex);
                    positions.Add(srcPos[i]);
                    normalAccum.Add(srcNorm != null && srcNorm.Length == srcPos.Length ? srcNorm[i] : Vector3.zero);
                    uvAccum.Add(srcUv != null && srcUv.Length == srcPos.Length ? srcUv[i] : Vector2.zero);
                    uvCount.Add(1);
                }
                else
                {
                    if (srcNorm != null && srcNorm.Length == srcPos.Length)
                    {
                        normalAccum[newIndex] += srcNorm[i];
                    }

                    if (srcUv != null && srcUv.Length == srcPos.Length)
                    {
                        uvAccum[newIndex] += srcUv[i];
                        uvCount[newIndex]++;
                    }
                }

                remap[i] = newIndex;
            }

            // Rebuild normals from triangle areas so outline inflate is continuous.
            var normals = new Vector3[positions.Count];
            var tris = new int[srcTris.Length];
            for (int i = 0; i < srcTris.Length; i++)
            {
                tris[i] = remap[srcTris[i]];
            }

            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i];
                int b = tris[i + 1];
                int c = tris[i + 2];
                if (a == b || b == c || a == c)
                {
                    continue;
                }

                Vector3 n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                normals[a] += n;
                normals[b] += n;
                normals[c] += n;
            }

            for (int i = 0; i < normals.Length; i++)
            {
                if (normals[i].sqrMagnitude < 1e-10f)
                {
                    normals[i] = normalAccum[i].sqrMagnitude > 1e-10f
                        ? normalAccum[i].normalized
                        : Vector3.up;
                }
                else
                {
                    normals[i].Normalize();
                }
            }

            var uvs = new Vector2[positions.Count];
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = uvCount[i] > 0 ? uvAccum[i] / uvCount[i] : Vector2.zero;
            }

            var mesh = new Mesh
            {
                name = source.name + "_Welded"
            };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            // Tangents optional; outline shader does not need them.
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Vector3Int Quantize(Vector3 p)
        {
            const float scale = 100000f;
            return new Vector3Int(
                Mathf.RoundToInt(p.x * scale),
                Mathf.RoundToInt(p.y * scale),
                Mathf.RoundToInt(p.z * scale));
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/MiniVan Game/Prefabs");
            EnsureFolder("Assets/MiniVan Game/Prefabs/Collectibles");
            EnsureFolder(PrefabFolder);
            EnsureFolder("Assets/MiniVan Game/Resources");
            EnsureFolder(ResourcesFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static Material EnsureAmberMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                ForceOpaqueSurface(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            mat.name = "MVG_AmberButton";
            Color amber = new Color(0.92f, 0.52f, 0.12f, 1f);
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", amber);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", amber);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.78f);
            }

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", 0.05f);
            }

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.12f) * 0.28f);
            }

            ForceOpaqueSurface(mat);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        private static void ForceOpaqueSurface(Material mat)
        {
            if (mat == null)
            {
                return;
            }

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 0f);
            }

            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f);
            }

            mat.SetOverrideTag("RenderType", "Opaque");
            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }

            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            }

            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetInt("_ZWrite", 1);
            }

            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = 1f;
                mat.SetColor("_BaseColor", c);
            }
        }
    }
}
