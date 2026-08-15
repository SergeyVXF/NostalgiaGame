using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Rebuilds TraceZombie visual hierarchy + generated Animator clips from the Blender FBX.
    /// FBX parenting is ignored (same reason as the player visual builder).
    /// </summary>
    public static class MiniVanTraceZombieVisualBuilder
    {
        public const string ArtFolder = "Assets/MiniVan Game/Art/Characters/TraceZombie";
        public const string FbxPath = ArtFolder + "/TraceZombie.fbx";
        public const string TexPath = ArtFolder + "/TraceZombie.png";
        public const string MaterialsFolder = "Assets/MiniVan Game/Materials/Characters/TraceZombie";
        public const string MatPath = MaterialsFolder + "/M_TraceZombie.mat";
        public const string ControllerPath = ArtFolder + "/TraceZombie.controller";
        public const string PrefabFolder = "Assets/MiniVan Game/Prefabs/Characters/TraceZombies";
        public const string VisualPrefabPath = PrefabFolder + "/TraceZombie_Visual.prefab";
        public const string GamePrefabPath = PrefabFolder + "/TraceZombie.prefab";

        [MenuItem("MiniVan Game/Characters/Setup Trace Zombie")]
        public static void SetupTraceZombie()
        {
            RigExistingVisualAndAnimations();
        }

        [MenuItem("MiniVan Game/Characters/Rebuild Trace Zombie Meshes From FBX")]
        public static void RebuildMeshesFromFbx()
        {
            EnsureFolder("Assets/MiniVan Game/Materials/Characters");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabFolder);

            ConfigureFbxImporter();
            ConfigureTexture();
            Material mat = CreateLitMaterial();

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogError("[TraceZombie] FBX not imported: " + FbxPath);
                return;
            }

            GameObject root = CreateVisualHierarchy(fbx, mat);
            PrefabUtility.SaveAsPrefabAsset(root, VisualPrefabPath);
            Object.DestroyImmediate(root);
            RigExistingVisualAndAnimations();
            Debug.Log("[TraceZombie] Rebuilt meshes from FBX: " + VisualPrefabPath);
        }

        public static void RigExistingVisualAndAnimations()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(VisualPrefabPath);
            if (root == null)
            {
                Debug.LogError("[TraceZombie] Missing visual prefab: " + VisualPrefabPath);
                return;
            }

            UnpackExistingBones(root.transform);
            InsertAnimationBones(root);
            AnimatorController controller = BuildAnimatorController();
            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.avatar = BuildGenericAvatar(root);

            PrefabUtility.SaveAsPrefabAsset(root, VisualPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TraceZombie] Rigged animations on existing visual: " + VisualPrefabPath);
        }

        private static void InsertAnimationBones(GameObject root)
        {
            Transform rootT = root.transform;
            UnpackExistingBones(rootT);
            Transform bodyMesh = FindDeep(rootT, "Body_Mesh");
            if (bodyMesh == null)
            {
                Debug.LogWarning("[TraceZombie] Body_Mesh not found, skip bone insert.");
                return;
            }

            Transform body = NewBone("Body", rootT, bodyMesh.position);
            bodyMesh.SetParent(body, true);

            Transform headMesh = FindDeep(rootT, "Head_Mesh");
            Vector3 headPos = headMesh != null ? headMesh.position : body.position + Vector3.forward * 0.4f;
            Transform head = NewBone("Head", body, headPos);
            if (headMesh != null)
            {
                headMesh.SetParent(head, true);
            }

            string[] legs = { "LegFL", "LegFR", "LegBL", "LegBR" };
            for (int i = 0; i < legs.Length; i++)
            {
                string name = legs[i];
                Transform upper = FindDeep(rootT, name + "_Upper");
                Transform mid = FindDeep(rootT, name + "_Mid");
                Transform foot = FindDeep(rootT, name + "_Foot");
                Vector3 hipPos = upper != null ? upper.position : body.position;
                Vector3 kneePos = mid != null ? mid.position : hipPos;
                Vector3 anklePos = foot != null ? foot.position : kneePos;
                Transform hip = NewBone(name, body, hipPos);
                Transform knee = NewBone(name + "_Knee", hip, kneePos);
                Transform ankle = NewBone(name + "_Ankle", knee, anklePos);
                if (upper != null)
                {
                    upper.SetParent(hip, true);
                }

                if (mid != null)
                {
                    mid.SetParent(knee, true);
                }

                if (foot != null)
                {
                    foot.SetParent(ankle, true);
                }

                ReparentClaws(rootT, name, ankle);
            }
        }

        private static void ReparentClaws(Transform root, string legName, Transform ankle)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            string prefix = legName + "_Claw";
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name.StartsWith(prefix))
                {
                    t.SetParent(ankle, true);
                }
            }
        }

        private static Transform NewBone(string name, Transform parent, Vector3 worldPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = worldPosition;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static void RestoreAuthoredMeshTransforms(GameObject root)
        {
            Transform rootT = root.transform;
            SetLocal(rootT, "Body_Mesh", new Vector3(0f, 0.503f, 0f), Vector3.zero, new Vector3(1f, 0.54f, 1.61f));
            SetLocal(rootT, "Head_Mesh", new Vector3(0f, 0.723f, -0.79f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegFL_Upper", new Vector3(0.4f, 0.5875f, -0.3925f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegFR_Upper", new Vector3(-0.4f, 0.5875f, -0.3925f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegBL_Upper", new Vector3(0.4f, 0.5875f, 0.615f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegBR_Upper", new Vector3(-0.4f, 0.5875f, 0.615f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegFL_Mid", new Vector3(0.57f, 0.4725f, -0.54f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegFR_Mid", new Vector3(-0.57f, 0.4725f, -0.54f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegBL_Mid", new Vector3(0.57f, 0.4725f, 0.7625f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegBR_Mid", new Vector3(-0.57f, 0.4725f, 0.7625f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegFL_Foot", new Vector3(0.62f, 0.09375f, -0.64f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegFR_Foot", new Vector3(-0.62f, 0.09375f, -0.64f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegBL_Foot", new Vector3(0.62f, 0.09375f, 0.8625f), Vector3.zero, Vector3.one);
            SetLocal(rootT, "LegBR_Foot", new Vector3(-0.62f, 0.09375f, 0.8625f), Vector3.zero, Vector3.one);

            Vector3 clawEuler = new Vector3(-19.15f, 0f, 0f);
            Vector3 clawScale = new Vector3(0.5f, 0.31f, 1.75f);
            SetLocal(rootT, "LegFR_Claw0", new Vector3(-0.475f, 0.07f, -0.77125f), clawEuler, clawScale);
            SetLocal(rootT, "LegFR_Claw1", new Vector3(-0.62f, 0.07f, -0.77125f), clawEuler, clawScale);
            SetLocal(rootT, "LegFR_Claw2", new Vector3(-0.757f, 0.07f, -0.77125f), clawEuler, clawScale);
        }

        private static void SetLocal(
            Transform root,
            string name,
            Vector3 position,
            Vector3 euler,
            Vector3 scale)
        {
            Transform t = FindDeep(root, name);
            if (t == null)
            {
                return;
            }

            t.localPosition = position;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = scale;
        }

        private static void UnpackExistingBones(Transform rootT)
        {
            Transform body = FindDirectChild(rootT, "Body");
            if (body == null)
            {
                return;
            }

            MeshFilter[] meshes = body.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null)
                {
                    meshes[i].transform.SetParent(rootT, true);
                }
            }

            Object.DestroyImmediate(body.gameObject);
        }

        private static void ConfigureFbxImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[TraceZombie] Missing FBX at " + FbxPath);
                return;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture()
        {
            TextureImporter texImporter = AssetImporter.GetAtPath(TexPath) as TextureImporter;
            if (texImporter == null)
            {
                return;
            }

            texImporter.sRGBTexture = true;
            texImporter.filterMode = FilterMode.Point;
            texImporter.wrapMode = TextureWrapMode.Clamp;
            texImporter.textureCompression = TextureImporterCompression.Uncompressed;
            texImporter.mipmapEnabled = false;
            texImporter.SaveAndReimport();
        }

        private static Material CreateLitMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            else
            {
                mat.shader = shader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", tex);
                }

                mat.mainTexture = tex;
            }

            Color white = Color.white;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", white);
            }

            mat.color = white;
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.08f);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateVisualHierarchy(GameObject fbx, Material mat)
        {
            GameObject root = Object.Instantiate(fbx);
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.UnpackPrefabInstance(
                    root,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            root.name = "TraceZombie Visual";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            StripNonMesh(root);
            ApplyMaterialRecursive(root, mat);
            if (root.GetComponent<Animator>() == null)
            {
                root.AddComponent<Animator>();
            }

            return root;
        }

        private static void StripNonMesh(GameObject root)
        {
            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Object.DestroyImmediate(cameras[i].gameObject);
            }

            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                Object.DestroyImmediate(lights[i].gameObject);
            }
        }

        private static void ApplyMaterialRecursive(GameObject root, Material mat)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sharedMaterial = mat;
                }
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static AnimatorController BuildAnimatorController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            while (controller.parameters.Length > 0)
            {
                controller.RemoveParameter(0);
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Cling", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Pounce", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);

            AnimationClip idle = MakeClip("Idle", true, clip =>
            {
                FloatX(clip, "Body", 0f, 4f, 0f, -4f, 0f);
                FloatX(clip, "Body/Head", 6f, 0f, -6f, 0f, 6f);
                PoseLegs(clip, 8f, -7f, 7f, -8f);
            });
            AnimationClip crawl = MakeClip("Crawl", true, clip =>
            {
                FloatX(clip, "Body", 6f, 0f, -5f, 0f, 6f);
                PoseLegs(clip, 22f, -20f, -20f, 22f);
                PoseLegsMid(clip, -16f, 14f, 14f, -16f);
            });
            AnimationClip cling = MakeClip("Cling", true, clip =>
            {
                SetConst(clip, "Body", 2f, 0f, 0f);
                SetConst(clip, "Body/Head", -6f, 0f, 0f);
                PoseLegsConst(clip, 8f, 8f, 6f, 6f);
            });
            AnimationClip pounce = MakeClip("Pounce", false, clip =>
            {
                SetKeysEuler(clip, "Body",
                    (0.00f, 6f, 0f, 0f),
                    (0.12f, -12f, 0f, 0f),
                    (0.40f, 10f, 0f, 0f));
                PoseLegsConst(clip, -14f, -14f, 18f, 18f, 0.4f);
            });
            AnimationClip land = MakeClip("Land", false, clip =>
            {
                SetKeysEuler(clip, "Body",
                    (0.00f, 10f, 0f, 0f),
                    (0.10f, 3f, 0f, 0f),
                    (0.35f, 0f, 0f, 0f));
                PoseLegsConst(clip, 10f, 10f, 8f, 8f, 0.35f);
            });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            while (machine.states.Length > 0)
            {
                machine.RemoveState(machine.states[0].state);
            }

            AnimatorState idleState = AddState(machine, "Idle", idle);
            AnimatorState crawlState = AddState(machine, "Crawl", crawl);
            AnimatorState clingState = AddState(machine, "Cling", cling);
            AnimatorState pounceState = AddState(machine, "Pounce", pounce);
            AnimatorState landState = AddState(machine, "Land", land);
            machine.defaultState = idleState;

            AddFloatTransition(idleState, crawlState, "Speed", 0.35f, true);
            AddFloatTransition(crawlState, idleState, "Speed", 0.22f, false);
            AddAnyBool(machine, clingState, "Cling", true);
            AddBoolTransition(clingState, idleState, "Cling", false);
            AddAnyTrigger(machine, pounceState, "Pounce");
            AddExitTime(pounceState, landState, 0.9f);
            AddAnyTrigger(machine, landState, "Land");
            AddExitTime(landState, idleState, 0.9f);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void PoseLegs(AnimationClip clip, float fl, float fr, float bl, float br)
        {
            FloatX(clip, "Body/LegFL", fl, -fl, fl);
            FloatX(clip, "Body/LegFR", fr, -fr, fr);
            FloatX(clip, "Body/LegBL", bl, -bl, bl);
            FloatX(clip, "Body/LegBR", br, -br, br);
        }

        private static void PoseLegsMid(AnimationClip clip, float fl, float fr, float bl, float br)
        {
            FloatX(clip, "Body/LegFL/LegFL_Knee", fl, -fl, fl);
            FloatX(clip, "Body/LegFR/LegFR_Knee", fr, -fr, fr);
            FloatX(clip, "Body/LegBL/LegBL_Knee", bl, -bl, bl);
            FloatX(clip, "Body/LegBR/LegBR_Knee", br, -br, br);
        }

        private static void PoseLegsConst(AnimationClip clip, float fl, float fr, float bl, float br, float duration = 1f)
        {
            SetConst(clip, "Body/LegFL", fl, 0f, -8f, duration);
            SetConst(clip, "Body/LegFR", fr, 0f, 8f, duration);
            SetConst(clip, "Body/LegBL", bl, 0f, -8f, duration);
            SetConst(clip, "Body/LegBR", br, 0f, 8f, duration);
        }

        private static Avatar BuildGenericAvatar(GameObject visualRoot)
        {
            const string avatarPath = ArtFolder + "/TraceZombieAvatar.asset";
            if (AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath) != null)
            {
                AssetDatabase.DeleteAsset(avatarPath);
            }

            Avatar avatar = AvatarBuilder.BuildGenericAvatar(visualRoot, "");
            avatar.name = "TraceZombieAvatar";
            AssetDatabase.CreateAsset(avatar, avatarPath);
            return AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            return state;
        }

        private static AnimationClip MakeClip(string name, bool loop, System.Action<AnimationClip> fill)
        {
            string path = ArtFolder + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.name = name;
            clip.ClearCurves();
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            fill(clip);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void FloatX(AnimationClip clip, string path, params float[] values)
        {
            Keyframe[] keys = new Keyframe[values.Length];
            float step = values.Length <= 1 ? 0f : 1f / (values.Length - 1);
            for (int i = 0; i < values.Length; i++)
            {
                keys[i] = new Keyframe(i * step, values[i]);
            }

            clip.SetCurve(path, typeof(Transform), "localEulerAngles.x", new AnimationCurve(keys));
            clip.SetCurve(path, typeof(Transform), "localEulerAngles.y", AnimationCurve.Constant(0f, 1f, 0f));
            clip.SetCurve(path, typeof(Transform), "localEulerAngles.z", AnimationCurve.Constant(0f, 1f, 0f));
        }

        private static void SetConst(AnimationClip clip, string path, float x, float y, float z, float duration = 1f)
        {
            clip.SetCurve(path, typeof(Transform), "localEulerAngles.x", AnimationCurve.Constant(0f, duration, x));
            clip.SetCurve(path, typeof(Transform), "localEulerAngles.y", AnimationCurve.Constant(0f, duration, y));
            clip.SetCurve(path, typeof(Transform), "localEulerAngles.z", AnimationCurve.Constant(0f, duration, z));
        }

        private static void SetKeysEuler(AnimationClip clip, string path, params (float t, float x, float y, float z)[] keys)
        {
            SetLinearCurve(clip, path, "localEulerAngles.x", keys, k => k.x);
            SetLinearCurve(clip, path, "localEulerAngles.y", keys, k => k.y);
            SetLinearCurve(clip, path, "localEulerAngles.z", keys, k => k.z);
        }

        private static void SetLinearCurve(
            AnimationClip clip,
            string path,
            string property,
            (float t, float x, float y, float z)[] keys,
            System.Func<(float t, float x, float y, float z), float> pick)
        {
            Keyframe[] frames = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                frames[i] = new Keyframe(keys[i].t, pick(keys[i]));
            }

            AnimationCurve curve = new AnimationCurve(frames);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }

            clip.SetCurve(path, typeof(Transform), property, curve);
        }

        private static void AddFloatTransition(
            AnimatorState from,
            AnimatorState to,
            string param,
            float threshold,
            bool greater)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.12f;
            t.AddCondition(
                greater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less,
                threshold,
                param);
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to, string param, bool value)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        private static void AddAnyBool(AnimatorStateMachine machine, AnimatorState to, string param, bool value)
        {
            AnimatorStateTransition t = machine.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = 0.08f;
            t.canTransitionToSelf = false;
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        private static void AddAnyTrigger(AnimatorStateMachine machine, AnimatorState to, string param)
        {
            AnimatorStateTransition t = machine.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = 0.06f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, param);
        }

        private static void AddExitTime(AnimatorState from, AnimatorState to, float exitTime)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = 0.08f;
            t.hasFixedDuration = true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
