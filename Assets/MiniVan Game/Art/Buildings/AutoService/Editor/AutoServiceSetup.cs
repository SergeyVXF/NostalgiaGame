using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Imports the Blender-built AutoService FBX, wires up the two URP materials,
    /// regroups the flat mesh list into the car hierarchy, adds colliders,
    /// saves the prefab and drops it into Game_v01 at the agreed spot.
    /// </summary>
    public static class AutoServiceSetup
    {
        const string Dir = "Assets/MiniVan Game/Art/Buildings/AutoService";
        const string FbxPath = Dir + "/AutoService.fbx";
        const string AtlasPath = Dir + "/AutoService_Atlas.png";
        const string LitPath = Dir + "/AS_Lit.mat";
        const string GlassPath = Dir + "/AS_Glass.mat";
        const string PrefabPath = Dir + "/AutoService_Building.prefab";
        const string ScenePath = "Assets/MiniVan Game/Scenes/Game_v01.unity";
        const string RootName = "AutoService_Building";
        const string MapHolder = "MapMeshes";

        static readonly Vector3 PlaceAt = new Vector3(415.5f, 0.1f, 649.8f);

        static readonly string[] CarPrefixes =
        {
            "AS_Car_Repair", "AS_Car_Blue", "AS_Car_Orange", "AS_Car_Beige", "AS_Car_Green"
        };

        // moving parts: cheap box colliders, everything else gets a mesh collider
        static bool IsMovingPart(string n)
        {
            return n.StartsWith("AS_Door_") || n.Contains("_Door_") ||
                   n.EndsWith("_Hood") || n.EndsWith("_Trunk");
        }

        [MenuItem("Tools/AutoService/Rebuild From FBX")]
        public static void Rebuild()
        {
            ConfigureAtlas();
            ConfigureModel();

            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitPath);
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (lit == null || glass == null)
            {
                Debug.LogError("[AutoService] materials missing at " + Dir);
                return;
            }
            SetupLit(lit, atlas);
            SetupGlass(glass, atlas);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogError("[AutoService] FBX not found: " + FbxPath);
                return;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = RootName;
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            AssignMaterials(root, lit, glass);
            GroupCars(root);
            AddColliders(root);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[AutoService] prefab saved: " + PrefabPath);

            PlaceInScene(prefab);
        }

        static void ConfigureAtlas()
        {
            var ti = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;   // atlas: never bleed across tiles
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        static void ConfigureModel()
        {
            var mi = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (mi == null) return;
            mi.globalScale = 1f;
            mi.useFileUnits = true;
            mi.useFileScale = true;
            // The FBX already ships axis-converted vertices (Blender exports it
            // with bake_space_transform and no parent empty), so Unity must not
            // convert a second time.
            mi.bakeAxisConversion = false;
            mi.isReadable = true;                 // required for MeshCollider baking
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.optimizeMeshPolygons = false;
            mi.optimizeMeshVertices = false;
            mi.weldVertices = false;              // keeps hard-edged low-poly shading
            mi.SaveAndReimport();
        }

        static void SetupLit(Material m, Texture2D atlas)
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = (int)RenderQueue.Geometry;
            if (atlas != null)
            {
                m.SetTexture("_BaseMap", atlas);
                m.SetTexture("_MainTex", atlas);
            }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.08f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
        }

        static void SetupGlass(Material m, Texture2D atlas)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            if (atlas != null)
            {
                m.SetTexture("_BaseMap", atlas);
                m.SetTexture("_MainTex", atlas);
            }
            m.SetColor("_BaseColor", new Color(0.72f, 0.80f, 0.82f, 0.34f));
            m.SetFloat("_Smoothness", 0.85f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
        }

        static void AssignMaterials(GameObject root, Material lit, Material glass)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                // building glass is "AS_Glass"; every car carries its own "<car>_Glass"
                var n = r.gameObject.name;
                var use = (n == "AS_Glass" || n.EndsWith("_Glass")) ? glass : lit;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = use;
                r.sharedMaterials = mats;
            }
        }

        static void GroupCars(GameObject root)
        {
            foreach (var prefix in CarPrefixes)
            {
                var members = new List<Transform>();
                foreach (Transform child in root.transform)
                {
                    if (child.name.StartsWith(prefix + "_")) members.Add(child);
                }
                if (members.Count == 0) continue;

                var body = members.FirstOrDefault(t => t.name == prefix + "_Body") ?? members[0];
                var group = new GameObject(prefix);
                group.transform.SetParent(root.transform, false);
                group.transform.position = body.position;
                group.transform.rotation = body.rotation;
                foreach (var m in members) m.SetParent(group.transform, true);

                WirePanels(group.transform);
            }
        }

        /// <summary>
        /// Make the doors / hood / boot openable with E. The hinge axis is read
        /// off the geometry rather than assumed, so a wreck parked at any angle
        /// still swings the right way.
        /// </summary>
        static void WirePanels(Transform car)
        {
            var carRenderers = car.GetComponentsInChildren<Renderer>(true);
            if (carRenderers.Length == 0) return;
            Bounds carBounds = carRenderers[0].bounds;
            foreach (var r in carRenderers) carBounds.Encapsulate(r.bounds);

            foreach (Transform part in car)
            {
                var renderer = part.GetComponent<Renderer>();
                if (renderer == null) continue;

                bool isDoor = part.name.Contains("_Door_");
                bool isHood = part.name.EndsWith("_Hood");
                bool isTrunk = part.name.EndsWith("_Trunk");
                if (!isDoor && !isHood && !isTrunk) continue;

                // hinge -> panel centre, flattened: this is the arm that swings
                Vector3 arm = renderer.bounds.center - part.position;
                arm.y = 0f;
                if (arm.sqrMagnitude < 0.0001f) continue;
                arm.Normalize();

                var panel = part.GetComponent<MiniVanScrapCarPanel>();
                if (panel == null) panel = part.gameObject.AddComponent<MiniVanScrapCarPanel>();

                if (isDoor)
                {
                    // swing away from the body: pick the sign that moves the free
                    // edge toward the outside of the car
                    Vector3 outward = renderer.bounds.center - carBounds.center;
                    outward.y = 0f;
                    float sign = Vector3.Dot(Vector3.Cross(Vector3.up, arm), outward) >= 0f ? 1f : -1f;
                    panel.Configure(MiniVanScrapCarPanel.PanelKind.Door, Vector3.up, sign * 68f);
                }
                else
                {
                    // rotating about (up x arm) always lifts the far edge
                    Vector3 axis = Vector3.Cross(Vector3.up, arm).normalized;
                    panel.Configure(
                        isHood ? MiniVanScrapCarPanel.PanelKind.Hood : MiniVanScrapCarPanel.PanelKind.Trunk,
                        axis,
                        isHood ? 52f : 58f);
                }
            }
        }

        static void AddColliders(GameObject root)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                if (mf.sharedMesh == null) continue;
                if (IsMovingPart(go.name))
                {
                    if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
                }
                else
                {
                    var mc = go.GetComponent<MeshCollider>();
                    if (mc == null) mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;
                }
            }
        }

        static void PlaceInScene(GameObject prefab)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("[AutoService] active scene is '" + scene.path +
                                 "', expected " + ScenePath + " - prefab saved, scene untouched.");
                return;
            }

            // Remove every copy, wherever it sits. Inheriting the found object's
            // position instead of the agreed one let the placement drift across
            // repeated runs, so the constant always wins.
            var stale = new List<Transform>();
            foreach (var go in scene.GetRootGameObjects())
                CollectByName(go.transform, RootName, stale);
            foreach (var t in stale)
            {
                Debug.Log("[AutoService] removing old " + RootName + " at " + t.position +
                          " parent=" + (t.parent == null ? "<root>" : t.parent.name));
                Undo.DestroyObjectImmediate(t.gameObject);
            }

            // lives with the other hand-placed map decor
            Transform holder = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == MapHolder) { holder = go.transform; break; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.name = RootName;
            if (holder != null) inst.transform.SetParent(holder, true);
            inst.transform.position = PlaceAt;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[AutoService] placed in scene at " + inst.transform.position +
                      " parent=" + (holder == null ? "<root>" : holder.name));
        }

        static void CollectByName(Transform t, string name, List<Transform> into)
        {
            if (t.name == name) { into.Add(t); return; }
            foreach (Transform c in t) CollectByName(c, name, into);
        }

        [MenuItem("Tools/AutoService/Save Scene")]
        public static void SaveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AutoService] scene saved: " + scene.path);
        }

        [MenuItem("Tools/AutoService/Report")]
        public static void Report()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.Log("[AutoService] no prefab yet"); return; }
            int meshes = 0, tris = 0;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                meshes++;
                tris += mf.sharedMesh.triangles.Length / 3;
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[AutoService] meshes=" + meshes + " tris=" + tris);
            foreach (Transform c in prefab.transform)
                sb.AppendLine("  " + c.name + " pos=" + c.localPosition + " rot=" + c.localEulerAngles);
            Debug.Log(sb.ToString());
        }
    }
}
