using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Assembles the player from exported meshes into a clean Y-up hierarchy.
    /// FBX bone-parenting from Blender is ignored — it comes in with broken axes.
    /// </summary>
    public static class MiniVanPlayerVisualBuilder
    {
        public const string ArtFolder = "Assets/MiniVan Game/Art/Characters/Player";
        public const string FbxPath = ArtFolder + "/MiniVanPlayer.fbx";
        public const string BodyTexPath = ArtFolder + "/MiniVanPlayer_Body.png";
        public const string HeadTexPath = ArtFolder + "/MiniVanPlayer_Head.png";
        public const string MaterialsFolder = "Assets/MiniVan Game/Materials/Characters/Player";
        public const string ClothesMatPath = MaterialsFolder + "/M_PlayerClothes.mat";
        public const string SkinMatPath = MaterialsFolder + "/M_PlayerSkin.mat";
        public const string HeadMatPath = MaterialsFolder + "/M_PlayerHead.mat";
        public const string ControllerPath = ArtFolder + "/MiniVanPlayer.controller";
        public const string PrefabFolder = "Assets/MiniVan Game/Resources/MiniVan/Player";
        public const string PrefabPath = PrefabFolder + "/MiniVanPlayerVisual.prefab";
        public const string PlayerPrefabPath = "Assets/MiniVan Game/Prefabs/Characters/Players/MiniVanPlayer.prefab";

        private const float FootH = 0.08f;
        private const float LegShortsH = 0.28f;
        private const float LegShinH = 0.28f;
        private const float TorsoH = 0.50f;
        private const float TorsoW = 0.44f;
        private const float SleeveH = 0.22f;
        private const float ForearmH = 0.32f;
        private const float ArmW = 0.16f;
        private const float HipY = FootH + LegShinH + LegShortsH;
        private const float ShoulderY = HipY + TorsoH;
        private const float ModelHeight = FootH + LegShinH + LegShortsH + TorsoH + 0.48f;
        private const float TargetHeight = 2f;
        private static readonly Quaternion MeshFix = Quaternion.Euler(-90f, 0f, 0f);

        [MenuItem("MiniVan Game/Characters/Setup Player Visual")]
        public static void SetupPlayerVisual()
        {
            EnsureFolder("Assets/MiniVan Game/Materials/Characters");
            EnsureFolder(MaterialsFolder);
            EnsureFolder("Assets/MiniVan Game/Resources/MiniVan");
            EnsureFolder(PrefabFolder);

            ConfigureFbxImporter();
            Material clothes = CreateLitMaterial(ClothesMatPath, BodyTexPath, Color.white);
            Material skin = CreateLitMaterial(SkinMatPath, "", new Color(0.93f, 0.88f, 0.80f, 1f));
            Material head = CreateLitMaterial(HeadMatPath, HeadTexPath, Color.white);

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogError("[PlayerVisual] FBX not imported: " + FbxPath);
                return;
            }

            GameObject root = CreateVisualHierarchy(fbx, clothes, skin, head);
            AnimatorController controller = BuildAnimatorController(root);
            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.avatar = BuildGenericAvatar(root);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            GameObject visualPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssignToPlayerPrefab(visualPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerVisual] Rebuilt clean hierarchy: " + PrefabPath);
        }

        private static void ConfigureFbxImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[PlayerVisual] Missing FBX at " + FbxPath);
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

        private static Material CreateLitMaterial(string path, string texturePath, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            mat.color = color;
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.12f);
            }

            if (!string.IsNullOrEmpty(texturePath))
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex != null)
                {
                    TextureImporter texImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (texImporter != null)
                    {
                        texImporter.sRGBTexture = true;
                        texImporter.filterMode = FilterMode.Point;
                        texImporter.wrapMode = TextureWrapMode.Clamp;
                        texImporter.textureCompression = TextureImporterCompression.Uncompressed;
                        texImporter.SaveAndReimport();
                    }

                    if (mat.HasProperty("_BaseMap"))
                    {
                        mat.SetTexture("_BaseMap", tex);
                    }

                    mat.mainTexture = tex;
                }
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateVisualHierarchy(GameObject fbx, Material clothes, Material skin, Material head)
        {
            float shoulderX = TorsoW * 0.5f + ArmW * 0.5f;
            float hipX = TorsoW * 0.25f;

            GameObject root = new GameObject("MiniVanPlayerVisual");
            root.transform.localScale = Vector3.one * (TargetHeight / ModelHeight);
            root.AddComponent<Animator>();

            Transform body = Bone("Body", root.transform, new Vector3(0f, HipY, 0f));
            Transform headBone = Bone("Head", body, new Vector3(0f, TorsoH, 0f));
            Transform armL = Bone("Arm_L", body, new Vector3(-shoulderX, TorsoH - 0.02f, 0f));
            Transform armR = Bone("Arm_R", body, new Vector3(shoulderX, TorsoH - 0.02f, 0f));
            Transform legL = Bone("Leg_L", root.transform, new Vector3(-hipX, HipY, 0f));
            Transform legR = Bone("Leg_R", root.transform, new Vector3(hipX, HipY, 0f));

            AddMesh("Torso", fbx, clothes, body, new Vector3(0f, TorsoH * 0.5f, 0f));
            AddMesh("Head", fbx, head, headBone, Vector3.zero);
            AddMesh("ArmL_Sleeve", fbx, clothes, armL, new Vector3(0f, -SleeveH * 0.5f, 0f));
            AddMesh("ArmL_Forearm", fbx, skin, armL, new Vector3(0f, -(SleeveH + ForearmH * 0.5f), 0f));
            AddMesh("ArmR_Sleeve", fbx, clothes, armR, new Vector3(0f, -SleeveH * 0.5f, 0f));
            AddMesh("ArmR_Forearm", fbx, skin, armR, new Vector3(0f, -(SleeveH + ForearmH * 0.5f), 0f));
            AddMesh("LegL_Shorts", fbx, clothes, legL, new Vector3(0f, -LegShortsH * 0.5f, 0f));
            AddMesh("LegL_Shin", fbx, skin, legL, new Vector3(0f, -(LegShortsH + LegShinH * 0.5f), 0f));
            AddMesh("FootL", fbx, clothes, legL, new Vector3(0f, -(LegShortsH + LegShinH + FootH * 0.5f), 0.04f));
            AddMesh("LegR_Shorts", fbx, clothes, legR, new Vector3(0f, -LegShortsH * 0.5f, 0f));
            AddMesh("LegR_Shin", fbx, skin, legR, new Vector3(0f, -(LegShortsH + LegShinH * 0.5f), 0f));
            AddMesh("FootR", fbx, clothes, legR, new Vector3(0f, -(LegShortsH + LegShinH + FootH * 0.5f), 0.04f));

            GameObject rightHand = new GameObject("RightHand");
            rightHand.transform.SetParent(armR, false);
            rightHand.transform.localPosition = new Vector3(0f, -(SleeveH + ForearmH), 0.02f);
            rightHand.transform.localRotation = Quaternion.identity;
            return root;
        }

        private static Transform Bone(string name, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static void AddMesh(string meshName, GameObject fbx, Material material, Transform parent, Vector3 localPos)
        {
            Mesh mesh = FindMesh(fbx, meshName);
            if (mesh == null)
            {
                Debug.LogWarning("[PlayerVisual] Missing mesh " + meshName);
                return;
            }

            GameObject go = new GameObject(meshName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = MeshFix;
            go.transform.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh FindMesh(GameObject fbx, string meshName)
        {
            MeshFilter[] filters = fbx.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] != null && filters[i].gameObject.name == meshName && filters[i].sharedMesh != null)
                {
                    return filters[i].sharedMesh;
                }
            }

            return null;
        }

        private static AnimatorController BuildAnimatorController(GameObject visualRoot)
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
            controller.AddParameter("Sit", AnimatorControllerParameterType.Bool);
            controller.AddParameter("HoldItem", AnimatorControllerParameterType.Bool);
            controller.AddParameter("HoldCross", AnimatorControllerParameterType.Bool);
            controller.AddParameter("DriveSit", AnimatorControllerParameterType.Bool);
            controller.AddParameter("BatSwing", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("StakeStab", AnimatorControllerParameterType.Trigger);

            AnimationClip idle = MakeClip("Idle", true, (clip) =>
            {
                FloatX(clip, "Body", 0f, 2f, 0f, -2f, 0f);
                FloatX(clip, "Body/Head", 0f, -0.8f, 0f, 0.8f, 0f);
                FloatX(clip, "Body/Arm_L", 8f, 12f, 8f, 4f, 8f);
                FloatX(clip, "Body/Arm_R", 8f, 4f, 8f, 12f, 8f);
            });
            AnimationClip walk = MakeClip("Walk", true, (clip) =>
            {
                FloatX(clip, "Body/Arm_L", -40f, 0f, 40f, 0f, -40f);
                FloatX(clip, "Body/Arm_R", 40f, 0f, -40f, 0f, 40f);
                FloatX(clip, "Leg_L", 36f, 0f, -36f, 0f, 36f);
                FloatX(clip, "Leg_R", -36f, 0f, 36f, 0f, -36f);
            });
            AnimationClip sit = MakeClip("Sit", true, (clip) =>
            {
                SetConst(clip, "Body", 12f, 0f, 0f);
                SetConst(clip, "Body/Head", -6f, 0f, 0f);
                SetConst(clip, "Body/Arm_L", -55f, 0f, -8f);
                SetConst(clip, "Body/Arm_R", -55f, 0f, 8f);
                SetConst(clip, "Leg_L", -82f, 0f, -6f);
                SetConst(clip, "Leg_R", -82f, 0f, 6f);
            });
            AnimationClip holdItem = MakeClip("HoldItem", true, (clip) =>
            {
                SetConst(clip, "Body/Arm_R", -50f, 0f, 10f);
                SetConst(clip, "Body/Arm_L", 8f, 0f, -6f);
            });
            AnimationClip driveSit = MakeClip("DriveSit", true, (clip) =>
            {
                SetConst(clip, "Body/Arm_L", -70f, 0f, -8f);
                SetConst(clip, "Body/Arm_R", -70f, 0f, 8f);
            });
            AnimationClip cross = MakeClip("CrossHold", true, (clip) =>
            {
                SetConst(clip, "Body/Arm_R", -82f, 0f, 0f);
                SetConst(clip, "Body/Arm_L", 8f, 0f, -6f);
            });
            AnimationClip bat = MakeClip("BatSwing", false, (clip) =>
            {
                SetKeysEuler(clip, "Body/Arm_R",
                    (0.00f, -50f, 0f, 10f),
                    (0.10f, -105f, 0f, 10f),
                    (0.18f, 20f, 0f, 10f),
                    (0.36f, -50f, 0f, 10f));
                SetConst(clip, "Body/Arm_L", 8f, 0f, -6f, 0.36f);
            });
            AnimationClip stake = MakeClip("StakeStab", false, (clip) =>
            {
                SetKeysX(clip, "Body/Arm_R",
                    (0f, -50f), (0.1f, 15f), (0.22f, -85f), (0.4f, -50f));
            });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            while (machine.states.Length > 0)
            {
                machine.RemoveState(machine.states[0].state);
            }

            AnimatorState idleState = AddState(machine, "Idle", idle);
            AnimatorState walkState = AddState(machine, "Walk", walk);
            AnimatorState sitState = AddState(machine, "Sit", sit);
            machine.defaultState = idleState;
            AddFloatTransition(idleState, walkState, "Speed", 0.35f, true);
            AddFloatTransition(walkState, idleState, "Speed", 0.25f, false);
            AddAnyBool(machine, sitState, "Sit", true);
            AddBoolTransition(sitState, idleState, "Sit", false);

            while (controller.layers.Length > 1)
            {
                controller.RemoveLayer(1);
            }

            AvatarMask mask = BuildUpperBodyMask(visualRoot);
            controller.AddLayer("Upper");
            AnimatorControllerLayer[] layers = controller.layers;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[1].defaultWeight = 0f;
            layers[1].avatarMask = mask;
            controller.layers = layers;

            AnimatorStateMachine upper = controller.layers[1].stateMachine;
            while (upper.states.Length > 0)
            {
                upper.RemoveState(upper.states[0].state);
            }

            AnimatorState emptyState = upper.AddState("Empty");
            AnimatorState holdState = AddState(upper, "HoldItem", holdItem);
            AnimatorState driveSitState = AddState(upper, "DriveSit", driveSit);
            AnimatorState crossState = AddState(upper, "CrossHold", cross);
            AnimatorState batState = AddState(upper, "BatSwing", bat);
            AnimatorState stakeState = AddState(upper, "StakeStab", stake);
            upper.defaultState = emptyState;

            AddBoolTransition(emptyState, holdState, "HoldItem", true);
            AddBoolTransition(holdState, emptyState, "HoldItem", false);
            AddAnyBool(upper, driveSitState, "DriveSit", true);
            AddBoolTransition(driveSitState, emptyState, "DriveSit", false);
            AddBoolTransition(emptyState, crossState, "HoldCross", true);
            AddBoolTransition(crossState, emptyState, "HoldCross", false);
            AddBoolTransition(holdState, crossState, "HoldCross", true);
            AddBoolTransition(crossState, holdState, "HoldItem", true);
            AddAnyTrigger(upper, batState, "BatSwing");
            AddExitTime(batState, holdState, 0.95f);
            AddAnyTrigger(upper, stakeState, "StakeStab");
            AddExitTime(stakeState, holdState, 0.9f);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Avatar BuildGenericAvatar(GameObject visualRoot)
        {
            const string avatarPath = ArtFolder + "/MiniVanPlayerAvatar.asset";
            if (AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath) != null)
            {
                AssetDatabase.DeleteAsset(avatarPath);
            }

            Avatar avatar = AvatarBuilder.BuildGenericAvatar(visualRoot, "");
            avatar.name = "MiniVanPlayerAvatar";
            AssetDatabase.CreateAsset(avatar, avatarPath);
            return AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
        }

        private static AvatarMask BuildUpperBodyMask(GameObject visualRoot)
        {
            const string maskPath = ArtFolder + "/PlayerUpperMask.mask";
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, maskPath);
            }

            SerializedObject so = new SerializedObject(mask);
            SerializedProperty transforms = so.FindProperty("m_Elements");
            if (transforms != null)
            {
                transforms.ClearArray();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            mask.AddTransformPath(visualRoot.transform, true);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                bool arm = path == "Body/Arm_L" || path.StartsWith("Body/Arm_L/")
                    || path == "Body/Arm_R" || path.StartsWith("Body/Arm_R/");
                mask.SetTransformActive(i, arm);
            }

            EditorUtility.SetDirty(mask);
            return mask;
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

        private static void SetKeysX(AnimationClip clip, string path, params (float t, float x)[] keys)
        {
            Keyframe[] frames = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                frames[i] = new Keyframe(keys[i].t, keys[i].x);
            }

            clip.SetCurve(path, typeof(Transform), "localEulerAngles.x", new AnimationCurve(frames));
        }

        private static void AddFloatTransition(AnimatorState from, AnimatorState to, string param, float threshold, bool greater)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.08f;
            t.AddCondition(greater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, threshold, param);
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to, string param, bool value)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.08f;
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        private static void AddAnyBool(AnimatorStateMachine machine, AnimatorState to, string param, bool value)
        {
            AnimatorStateTransition t = machine.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        private static void AddAnyTrigger(AnimatorStateMachine machine, AnimatorState to, string param)
        {
            AnimatorStateTransition t = machine.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.canTransitionToSelf = true;
            t.AddCondition(AnimatorConditionMode.If, 0f, param);
        }

        private static void AddExitTime(AnimatorState from, AnimatorState to, float exitTime)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = 0.04f;
        }

        private static void AssignToPlayerPrefab(GameObject visualPrefab)
        {
            if (visualPrefab == null)
            {
                return;
            }

            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                MiniVanPlayer component = player.GetComponent<MiniVanPlayer>();
                if (component != null)
                {
                    component.PlayerVisualPrefab = visualPrefab;
                    component.PlayerVisualUniformScale = TargetHeight / ModelHeight;
                    EditorUtility.SetDirty(component);
                }

                MeshRenderer capsule = player.GetComponent<MeshRenderer>();
                if (capsule != null)
                {
                    capsule.enabled = false;
                    capsule.forceRenderingOff = true;
                }

                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
