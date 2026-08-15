using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Imports the tower crane and the scrap pile, rebuilds the crane's moving
    /// hierarchy (slew / trolley / rope / magnet / cab controls), adds colliders
    /// and drops both next to the auto service.
    /// </summary>
    public static class MiniVanPropsSetup
    {
        const string PropsDir = "Assets/MiniVan Game/Art/Props";
        const string ShareDir = "Assets/MiniVan Game/Art/Buildings/AutoService";
        const string LitPath = ShareDir + "/AS_Lit.mat";
        const string GlassPath = ShareDir + "/AS_Glass.mat";
        const string ScenePath = "Assets/MiniVan Game/Scenes/Game_v01.unity";
        const string MapHolder = "MapMeshes";

        const string CraneFbx = PropsDir + "/TowerCrane/TowerCrane.fbx";
        const string CranePrefab = PropsDir + "/TowerCrane/TowerCrane.prefab";
        const string PileFbx = PropsDir + "/CarPile/CarPile.fbx";
        const string PilePrefab = PropsDir + "/CarPile/CarPile.prefab";

        // world spots, chosen so the 14 m jib can swing over the pile
        // cab floor in crane-local space (mast top 12.0 + 0.30 deck + 0.01 plate)
        const float CabFloorHeight = 12.31f;

        // The district is a plateau at y~10.8 with the auto service sitting in a
        // carved-out pocket (x 408..416, z 648..680, ground ~0.1). Both props go
        // inside that pocket, in front of the facade and clear of the yard
        // wrecks, ~12 m apart so the 14 m jib reaches the pile.
        static readonly Vector3 CraneAt = new Vector3(414f, 0.1f, 668f);
        static readonly Vector3 PileAt = new Vector3(408f, 0.1f, 678f);

        [MenuItem("Tools/AutoService/Build Crane And Pile")]
        public static void BuildBoth()
        {
            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitPath);
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            if (lit == null || glass == null)
            {
                Debug.LogError("[Props] shared materials missing - run the AutoService rebuild first");
                return;
            }

            var crane = BuildCrane(lit, glass);
            var pile = BuildPile(lit);
            Place(crane, "TowerCrane", CraneAt);
            Place(pile, "CarPile", PileAt);
        }

        // -------------------------------------------------------------- import
        static void ConfigureModel(string path)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return;
            mi.globalScale = 1f;
            mi.useFileUnits = true;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;      // Blender already baked the axes
            mi.isReadable = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.optimizeMeshPolygons = false;
            mi.optimizeMeshVertices = false;
            mi.weldVertices = false;
            mi.SaveAndReimport();
        }

        static GameObject Instantiate(string fbx, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (asset == null) { Debug.LogError("[Props] missing FBX " + fbx); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        static void AssignMaterials(GameObject root, Material lit, Material glass)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var use = r.gameObject.name.EndsWith("_Glass") ? glass : lit;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = use;
                r.sharedMaterials = mats;
            }
        }

        static Transform Find(GameObject root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // --------------------------------------------------------------- crane
        static GameObject BuildCrane(Material lit, Material glass)
        {
            ConfigureModel(CraneFbx);
            var root = Instantiate(CraneFbx, "TowerCrane");
            if (root == null) return null;
            AssignMaterials(root, lit, glass);

            // rope and magnet ride the trolley; the trolley and the cab ride the slew
            var slew = Find(root, "TC_Slew");
            var trolley = Find(root, "TC_Trolley");
            foreach (var n in new[] { "TC_Rope", "TC_Magnet" })
            {
                var t = Find(root, n);
                if (t != null && trolley != null) t.SetParent(trolley, true);
            }
            var onSlew = new[]
            {
                "TC_Cab", "TC_Cab_Glass", "TC_Console", "TC_Lever_1", "TC_Lever_2",
                "TC_Lever_3", "TC_Button", "TC_Jib", "TC_CounterJib", "TC_Trolley"
            };
            foreach (var n in onSlew)
            {
                var t = Find(root, n);
                if (t != null && slew != null && t != slew) t.SetParent(slew, true);
            }

            // colliders: solid structure gets mesh colliders, controls get boxes
            var boxed = new HashSet<string>
            {
                "TC_Lever_1", "TC_Lever_2", "TC_Lever_3", "TC_Button", "TC_Trolley", "TC_Magnet"
            };
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                if (mf.sharedMesh == null || go.name == "TC_Rope") continue;
                if (go.name == "TC_Ladder") { SetupLadder(go); continue; }
                if (boxed.Contains(go.name))
                {
                    if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
                }
                else
                {
                    // note: ?? does not work here - Unity components compare
                    // equal to null through an overloaded operator, not by reference
                    var mc = go.GetComponent<MeshCollider>();
                    if (mc == null) mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;
                }
            }

            WireControls(root);   // keep the cab controls part of every rebuild

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CranePrefab);
            Object.DestroyImmediate(root);
            Debug.Log("[Props] crane prefab saved: " + CranePrefab);
            return prefab;
        }

        /// <summary>
        /// Climbable like every other ladder in the project: MiniVanLadder drives
        /// the climb and wants one clean BoxCollider, not a mesh collider full of
        /// rungs the player would snag on.
        /// </summary>
        static void SetupLadder(GameObject go)
        {
            foreach (var mc in go.GetComponents<MeshCollider>()) Object.DestroyImmediate(mc);

            var renderer = go.GetComponent<Renderer>();
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            if (renderer != null)
            {
                Vector3 center = go.transform.InverseTransformPoint(renderer.bounds.center);
                Vector3 size = renderer.bounds.size;
                box.center = center;
                // keep it thin through the rungs so the climber is not pushed off
                box.size = new Vector3(Mathf.Max(size.x, 0.5f), size.y, 0.22f);
            }
            box.isTrigger = false;

            var ladder = go.GetComponent<MiniVanLadder>();
            if (ladder == null) ladder = go.AddComponent<MiniVanLadder>();
            // step off into the cab doorway, which sits on +Z just above the top rung
            ladder.RoofEntryHeight = CabFloorHeight - 0.12f;
            ladder.RoofEntryLocalDirection = Vector3.forward;
            ladder.ClimbStandOffLocalDirection = Vector3.back;
            ladder.ClimbStandOffDistance = 0.42f;
            ladder.SyncClimbVolume();
        }

        // ---------------------------------------------------------------- pile
        static GameObject BuildPile(Material lit)
        {
            ConfigureModel(PileFbx);
            var root = Instantiate(PileFbx, "CarPile");
            if (root == null) return null;
            AssignMaterials(root, lit, lit);

            // decorative: one box collider over the whole heap, nothing finer
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                Object.DestroyImmediate(mc);
            var mesh = root.GetComponentInChildren<MeshRenderer>(true);
            if (mesh != null && mesh.GetComponent<Collider>() == null)
                mesh.gameObject.AddComponent<BoxCollider>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PilePrefab);
            Object.DestroyImmediate(root);
            Debug.Log("[Props] pile prefab saved: " + PilePrefab);
            return prefab;
        }

        // --------------------------------------------------------------- scene
        static void Place(GameObject prefab, string name, Vector3 at)
        {
            if (prefab == null) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("[Props] active scene is " + scene.path + " - prefab saved, scene untouched");
                return;
            }

            var stale = new List<Transform>();
            foreach (var go in scene.GetRootGameObjects()) Collect(go.transform, name, stale);

            // Whatever is already in the scene has usually been nudged, rotated or
            // rescaled by hand. Rebuilding the prefab must never undo that, so the
            // existing transform is carried over and only the first placement uses
            // the authored spot.
            bool hadOne = stale.Count > 0;
            Vector3 keepPos = Vector3.zero;
            Quaternion keepRot = Quaternion.identity;
            Vector3 keepScale = Vector3.one;
            Transform keepParent = null;
            if (hadOne)
            {
                keepPos = stale[0].position;
                keepRot = stale[0].rotation;
                keepScale = stale[0].localScale;
                keepParent = stale[0].parent;
            }
            foreach (var t in stale) Undo.DestroyObjectImmediate(t.gameObject);

            Transform holder = keepParent;
            if (holder == null)
                foreach (var go in scene.GetRootGameObjects())
                    if (go.name == MapHolder) { holder = go.transform; break; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.name = name;
            if (holder != null) inst.transform.SetParent(holder, true);
            if (hadOne)
            {
                inst.transform.position = keepPos;
                inst.transform.rotation = keepRot;
                inst.transform.localScale = keepScale;
            }
            else
            {
                inst.transform.rotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                inst.transform.position = SnapToGround(at);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Props] " + name + (hadOne ? " rebuilt, transform kept at " : " placed at ") +
                      inst.transform.position);
        }

        /// <summary>Lowest collider under the spot. A plain Raycast lands on the
        /// panelka rooftops that cover this district, which is how the props
        /// first ended up floating 10 m in the air.</summary>
        static bool GroundY(float x, float z, out float y)
        {
            // edit mode keeps a stale physics copy until this is called, which
            // makes freshly created props invisible to the raycast
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(new Vector3(x, 400f, z), Vector3.down, 2000f);
            y = 0f;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.point.y < -100f) continue;
                if (!found || h.point.y < y) { y = h.point.y; found = true; }
            }
            return found;
        }

        static Vector3 SnapToGround(Vector3 at)
        {
            float y;
            if (GroundY(at.x, at.z, out y))
                return new Vector3(at.x, y + 0.05f, at.z);
            Debug.LogWarning("[Props] nothing under " + at + " - using the requested height");
            return at;
        }

        /// <summary>
        /// Make the cab levers and the magnet button live. Edits the prefab asset
        /// only - no reimport, no transforms touched, so the crane stays exactly
        /// where and how it is.
        /// </summary>
        [MenuItem("Tools/AutoService/Wire Crane Controls")]
        public static void WireCraneControls()
        {
            var root = PrefabUtility.LoadPrefabContents(CranePrefab);
            if (root == null) { Debug.LogError("[Props] crane prefab not found"); return; }
            try
            {
                WireControls(root);
                PrefabUtility.SaveAsPrefabAsset(root, CranePrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void WireControls(GameObject root)
        {
            {
                var slew = Find(root, "TC_Slew");
                var trolley = Find(root, "TC_Trolley");
                var rope = Find(root, "TC_Rope");
                var magnet = Find(root, "TC_Magnet");
                var jib = Find(root, "TC_Jib");
                if (slew == null || trolley == null || magnet == null)
                {
                    Debug.LogError("[Props] crane rig incomplete - slew/trolley/magnet missing");
                    return;
                }

                var crane = root.GetComponent<MiniVanTowerCrane>();
                if (crane == null) crane = root.AddComponent<MiniVanTowerCrane>();
                crane.Slew = slew;
                crane.Trolley = trolley;
                crane.Rope = rope;
                crane.Magnet = magnet;

                // travel limits read off the jib, so they follow the model
                var jibRenderer = jib != null ? jib.GetComponent<Renderer>() : null;
                if (jibRenderer != null)
                {
                    float z0 = jibRenderer.bounds.min.z - slew.position.z;
                    float z1 = jibRenderer.bounds.max.z - slew.position.z;
                    crane.TrolleyNear = z0 + 2.0f;
                    crane.TrolleyFar = z1 - 1.2f;
                }

                Wire(root, "TC_Lever_1", MiniVanCraneControl.ControlKind.TrolleyLever, crane, null);
                Wire(root, "TC_Lever_2", MiniVanCraneControl.ControlKind.HoistLever, crane, null);
                Wire(root, "TC_Lever_3", MiniVanCraneControl.ControlKind.SlewLever, crane, null);
                Wire(root, "TC_Button", MiniVanCraneControl.ControlKind.MagnetButton, crane, GlowMaterial());

                Debug.Log("[Props] crane controls wired: trolley " +
                          crane.TrolleyNear.ToString("0.0") + " .. " + crane.TrolleyFar.ToString("0.0"));
            }
        }

        static void Wire(GameObject root, string name, MiniVanCraneControl.ControlKind kind,
                         MiniVanTowerCrane crane, Material glow)
        {
            var t = Find(root, name);
            if (t == null) { Debug.LogWarning("[Props] missing control " + name); return; }

            if (t.GetComponent<Collider>() == null)
                t.gameObject.AddComponent<BoxCollider>();

            var control = t.GetComponent<MiniVanCraneControl>();
            if (control == null) control = t.gameObject.AddComponent<MiniVanCraneControl>();
            control.Configure(kind, crane, glow);
        }

        /// <summary>Red emissive material the magnet button switches to while it is on.</summary>
        static Material GlowMaterial()
        {
            const string path = PropsDir + "/TowerCrane/AS_MagnetButtonOn.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = "AS_MagnetButtonOn" };
            mat.SetColor("_BaseColor", new Color(0.55f, 0.04f, 0.04f, 1f));
            mat.SetColor("_Color", new Color(0.55f, 0.04f, 0.04f, 1f));
            mat.SetColor("_EmissionColor", new Color(2.2f, 0.15f, 0.12f, 1f));
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetFloat("_Smoothness", 0.4f);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>Sample the ground around the auto service. The hit height is
        /// baked into each probe's name so one hierarchy read returns everything.</summary>
        [MenuItem("Tools/AutoService/Probe Ground")]
        public static void ProbeGround()
        {
            var old = GameObject.Find("GroundProbes");
            if (old != null) Object.DestroyImmediate(old);
            var parent = new GameObject("GroundProbes");

            for (int x = 392; x <= 440; x += 8)
            {
                for (int z = 640; z <= 688; z += 8)
                {
                    float gy;
                    string label = GroundY(x, z, out gy)
                        ? string.Format("GP_{0}_{1}_h{2}", x, z, Mathf.RoundToInt(gy * 10f))
                        : string.Format("GP_{0}_{1}_MISS", x, z);
                    var p = new GameObject(label);
                    p.transform.SetParent(parent.transform, true);
                }
            }
            Debug.Log("[Props] ground probed");
        }

        [MenuItem("Tools/AutoService/Clear Probes")]
        public static void ClearProbes()
        {
            var old = GameObject.Find("GroundProbes");
            if (old != null) Object.DestroyImmediate(old);
        }

        static void Collect(Transform t, string name, List<Transform> into)
        {
            if (t.name == name) { into.Add(t); return; }
            foreach (Transform c in t) Collect(c, name, into);
        }
    }
}
