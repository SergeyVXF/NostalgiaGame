using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MiniVanGame
{
    /// <summary>
    /// World pickup for the holy cross anti-vampire item.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanHolyCrossPickup : NetworkBehaviour, IMiniVanGameModeInteractable
    {
        public const string PrefabAssetPath = "Assets/MiniVan Game/Prefabs/Weapons/Melee/HolyCrossPickup.prefab";
        public const string HeldPrefabAssetPath = "Assets/MiniVan Game/Prefabs/Weapons/Melee/HolyCrossHeld.prefab";
        public const string ParticlesPrefabAssetPath = "Assets/MiniVan Game/Prefabs/Weapons/Melee/HolyCrossParticles.prefab";
        public const string ConePrefabAssetPath = "Assets/MiniVan Game/Prefabs/Weapons/Melee/HolyCrossCone.prefab";
        public const string MaterialFolder = "Assets/MiniVan Game/Materials/Weapons/HolyCross";

        public float PickupRadius = 2.4f;

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || !IsAvailable || !IsInReach(player.transform.position))
            {
                return string.Empty;
            }

            if (player.HasInventoryItemPublic(MiniVanInventoryItem.HolyCross))
            {
                return "Already have holy cross";
            }

            return "E - take holy cross";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || Input.GetMouseButton(1) || !IsAvailable)
            {
                return;
            }

            player.RequestTakeHolyCross(this);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool IsAvailable => !IsSpawned || gameObject.activeInHierarchy;

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public bool TryClaim()
        {
            if (!IsServer || !IsSpawned)
            {
                return false;
            }

            NetworkObject.Despawn(true);
            return true;
        }

        public static GameObject ResolvePrefab()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            }
#endif
            return FindRegisteredPrefab("HolyCrossPickup");
        }

        public static MiniVanHolyCrossPickup ServerSpawn(Vector3 position, Quaternion rotation)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return null;
            }

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
#endif
            if (prefab == null)
            {
                prefab = FindRegisteredPrefab("HolyCrossPickup");
            }

            if (prefab == null)
            {
                Debug.LogWarning("[HolyCross] Prefab missing.");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            NetworkObject net = instance.GetComponent<NetworkObject>();
            MiniVanHolyCrossPickup pickup = instance.GetComponent<MiniVanHolyCrossPickup>();
            if (net == null || pickup == null)
            {
                Object.Destroy(instance);
                return null;
            }

            net.Spawn(true);
            return pickup;
        }

        private static GameObject FindRegisteredPrefab(string name)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.NetworkConfig == null || nm.NetworkConfig.Prefabs == null)
            {
                return null;
            }

            foreach (NetworkPrefab entry in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (entry != null && entry.Prefab != null && entry.Prefab.name == name)
                {
                    return entry.Prefab;
                }
            }

            return null;
        }

        private void Awake()
        {
            EnsureBuiltVisual(gameObject);
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.isTrigger = true;
                box.center = new Vector3(0f, 0.35f, 0f);
                box.size = new Vector3(0.45f, 0.85f, 0.25f);
            }
        }

        public static void EnsureBuiltVisual(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (root.transform.Find("Holy Cross Visual") == null)
            {
                GameObject visual = new GameObject("Holy Cross Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                Material wood = LoadWoodMaterial();
                CreateBeam(visual.transform, "Vertical", new Vector3(0f, 0.38f, 0f), new Vector3(0.08f, 0.72f, 0.06f), wood);
                CreateBeam(visual.transform, "Horizontal", new Vector3(0f, 0.52f, 0f), new Vector3(0.42f, 0.08f, 0.06f), wood);
            }

            ApplySharedMaterials(root);
        }

        public static void ApplySharedMaterials(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Material wood = LoadWoodMaterial();
            SetChildMaterial(root.transform, "Vertical", wood);
            SetChildMaterial(root.transform, "Horizontal", wood);

            ParticleSystemRenderer particleRenderer = root.GetComponentInChildren<ParticleSystemRenderer>(true);
            if (particleRenderer != null)
            {
                particleRenderer.sharedMaterial = LoadParticleMaterial();
            }

            Transform cone = FindDeepChild(root.transform, "HolyCrossCone");
            if (cone != null)
            {
                MeshRenderer coneRenderer = cone.GetComponent<MeshRenderer>();
                if (coneRenderer != null)
                {
                    coneRenderer.sharedMaterial = LoadConeMaterial();
                }
            }
        }

        public static GameObject CreateHeldVisual(Transform parent)
        {
            GameObject root = new GameObject("HolyCrossHeld");
            root.transform.SetParent(parent, false);
            EnsureBuiltVisual(root);
            DisableChildColliders(root);
            return root;
        }

        public static GameObject CreateParticlesObject(Transform parent)
        {
            GameObject fx = new GameObject("HolyCrossParticles");
            fx.transform.SetParent(parent, false);
            fx.transform.localPosition = new Vector3(0f, 0.55f, 0.02f);

            ParticleSystem ps = fx.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 1.4f;
            main.startSize = 0.05f;
            main.startColor = new Color(1f, 0.92f, 0.55f, 0.85f);
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 28f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.02f;

            ParticleSystemRenderer renderer = fx.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = LoadParticleMaterial();
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return fx;
        }

        public static GameObject CreateConeVisual(Transform parent, float range, float halfAngleDegrees)
        {
            GameObject cone = new GameObject("HolyCrossCone");
            cone.transform.SetParent(parent, false);
            cone.transform.localPosition = Vector3.zero;
            cone.transform.localRotation = Quaternion.identity;

            MeshFilter filter = cone.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildConeMeshPublic(range, halfAngleDegrees, 28);
            MeshRenderer renderer = cone.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LoadConeMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return cone;
        }

        public static Mesh BuildConeMeshPublic(float length, float halfAngleDegrees, int segments)
        {
            return BuildConeMesh(length, halfAngleDegrees, segments);
        }

        private static Mesh BuildConeMesh(float length, float halfAngleDegrees, int segments)
        {
            float radius = Mathf.Tan(halfAngleDegrees * Mathf.Deg2Rad) * length;
            segments = Mathf.Max(8, segments);
            Mesh mesh = new Mesh { name = "HolyCrossConeMesh" };

            // Apex + ring, duplicated so the cone is double-sided.
            int ring = segments;
            Vector3[] verts = new Vector3[(ring + 1) * 2];
            verts[0] = Vector3.zero;
            verts[ring + 1] = Vector3.zero;
            for (int i = 0; i < ring; i++)
            {
                float t = (i / (float)ring) * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, length);
                verts[i + 1] = p;
                verts[i + 1 + ring + 1] = p;
            }

            int[] tris = new int[ring * 6];
            int w = 0;
            for (int i = 0; i < ring; i++)
            {
                int a = i + 1;
                int b = (i + 1) % ring + 1;
                // outward
                tris[w++] = 0;
                tris[w++] = a;
                tris[w++] = b;
                // inward
                int a2 = a + ring + 1;
                int b2 = b + ring + 1;
                tris[w++] = ring + 1;
                tris[w++] = b2;
                tris[w++] = a2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateBeam(Transform parent, string name, Vector3 localPos, Vector3 scale, Material material)
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = name;
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = localPos;
            beam.transform.localRotation = Quaternion.identity;
            beam.transform.localScale = scale;
            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider col = beam.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
                if (Application.isPlaying)
                {
                    Object.Destroy(col);
                }
                else
                {
                    Object.DestroyImmediate(col);
                }
            }
        }

        private static void DisableChildColliders(GameObject root)
        {
            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                cols[i].enabled = false;
            }
        }

        private static void SetChildMaterial(Transform root, string childName, Material material)
        {
            Transform child = FindDeepChild(root, childName);
            if (child == null || material == null)
            {
                return;
            }

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static Material LoadWoodMaterial()
        {
            return LoadOrCreateLitMaterial("HolyCross_Wood", new Color(0.72f, 0.58f, 0.28f, 1f), 0.05f, 0.35f);
        }

        public static Material LoadParticleMaterial()
        {
            return LoadOrCreateParticleMaterial("HolyCross_Particles", new Color(1f, 0.9f, 0.5f, 0.8f));
        }

        public static Material LoadConeMaterial()
        {
            return LoadOrCreateTransparentMaterial("HolyCross_Cone", new Color(1f, 0.92f, 0.25f, 0.22f));
        }

        private static Material LoadOrCreateLitMaterial(string name, Color color, float metallic, float smoothness)
        {
            Material loaded = LoadMaterialAsset(name);
            if (loaded != null)
            {
                return loaded;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader) { name = name };
            ApplyLitColor(mat, color, metallic, smoothness);
            return mat;
        }

        private static Material LoadOrCreateParticleMaterial(string name, Color color)
        {
            Material loaded = LoadMaterialAsset(name);
            if (loaded != null)
            {
                return loaded;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            Material mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            mat.color = color;
            return mat;
        }

        private static Material LoadOrCreateTransparentMaterial(string name, Color color)
        {
            Material loaded = LoadMaterialAsset(name);
            if (loaded != null)
            {
                return loaded;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader) { name = name };
            ApplyTransparentColor(mat, color);
            return mat;
        }

        private static Material LoadMaterialAsset(string name)
        {
#if UNITY_EDITOR
            Material editorMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
            if (editorMat != null)
            {
                return editorMat;
            }
#endif
            return Resources.Load<Material>("Weapons/HolyCross/" + name);
        }

        private static void ApplyLitColor(Material mat, Color color, float metallic, float smoothness)
        {
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }
        }

        private static void ApplyTransparentColor(Material mat, Color color)
        {
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
            }

            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }
}
