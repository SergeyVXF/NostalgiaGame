using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;

namespace Rowlan.Tools.MagicaCloth2.ColliderCreator
{
    /// <summary>
    /// A helper dialog which provides convenient 
    /// 
    /// * creation of colliders
    /// * selection of colliders
    /// * creation of magica cloth gameobject
    /// * registering the colliders in the magica cloth gameobject
    /// 
    /// </summary>
    public class HumanoidColliderCreator : EditorWindow
    {
        /// <summary>
        /// The collider data.
        /// Serialization required for domain reload.
        /// </summary>
        [SerializeField]
        private ColliderCreatorData data = new ColliderCreatorData();

        #region Features

        private BoneSelection boneSelection;
        private ClipboardManager clipboardManager;
        private ColliderFactory colliderFactory;
        private ColliderRegistry colliderRegistry;
        private SelectionManager selectionManager;

        #endregion Features

        Dictionary<HumanBodyBones, Transform> boneTransformMap;

        /// <summary>
        /// Paint indicator if mouse is over row
        /// </summary>
        private bool hoverRowEnabled = true;

        // Scroll position for the bone list.
        private Vector2 scrollPos;

        public static bool helpBoxVisible = false;

        [MenuItem("Tools/Rowlan/Magica Cloth 2/Humanoid Collider Creator")]
        public static void ShowWindow()
        {
            GetWindow<HumanoidColliderCreator>("Magica Cloth 2 - Humanoid Collider Creator");
        }

        private void OnEnable()
        {
            boneSelection = new BoneSelection(data);
            clipboardManager = new ClipboardManager(data);
            colliderFactory = new ColliderFactory(data);
            colliderRegistry = new ColliderRegistry(data);
            selectionManager = new SelectionManager(data);

            UpdateBoneValidityMap();

        }

        private void OnDisable()
        {
            boneSelection = null;
            clipboardManager = null;
            colliderFactory = null;
            colliderRegistry = null;
            selectionManager = null;
        }

        private void UpdateBoneValidityMap()
        {
            boneTransformMap = Validator.ValidateBones(data.animator);
        }

        private void OnGUI()
        {
            DrawDialogHeader();

            // humanoid
            EditorGUILayout.BeginHorizontal();
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    data.humanoidRoot = (GameObject)EditorGUILayout.ObjectField("Humanoid Root", data.humanoidRoot, typeof(GameObject), true);
                    if (data.humanoidRoot != null)
                        data.animator = data.humanoidRoot.GetComponent<Animator>();
                    else
                        data.animator = null;

                    if (check.changed)
                    {
                        UpdateBoneValidityMap();
                    }
                }


                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    if (data.humanoidRoot != null)
                        Selection.activeGameObject = data.humanoidRoot;
                }
            }
            EditorGUILayout.EndHorizontal();


            // magica cloth gameobject
            EditorGUILayout.BeginHorizontal();
            {
                data.magicaClothGameObject = (GameObject)EditorGUILayout.ObjectField(
                    "Magica Cloth GameObject",
                    data.magicaClothGameObject,
                    typeof(GameObject),
                    true
                );

                if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    CreateMagicaClothGameObject();
                }

                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    if (data.magicaClothGameObject != null)
                        Selection.activeGameObject = data.magicaClothGameObject;
                }
            }
            EditorGUILayout.EndHorizontal();

            // name prefix
            data.colliderPrefix = EditorGUILayout.TextField("Name Prefix", data.colliderPrefix);

            // use all bones or only main bones
            if (ProjectSetup.useAllBonesAvailable)
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    data.useAllBones = EditorGUILayout.Toggle("Use All Bones", data.useAllBones);

                    if (check.changed)
                    {
                        boneSelection.UpdateAvailableBonesList(data.useAllBones);
                    }
                }
            }

            EditorGUILayout.Space();

            // colliders
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("Colliders", EditorStyles.boldLabel);

                if (data.animator != null)
                {
                    DrawColliderGUI();
                }
            }
            EditorGUILayout.EndVertical();

            // selection
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("Selection", EditorStyles.miniBoldLabel);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Select Current Colliders"))
                    {
                        selectionManager.SelectCurrentColliders(GetCurrentColliders());
                    }
                    if (GUILayout.Button("Select All Colliders"))
                    {
                        selectionManager.SelectAllColliders();
                    }
                    if (GUILayout.Button("Clear Selection"))
                    {
                        selectionManager.ClearSelection();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // clipboard
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("Clipboard (eg copy at runtime, paste in editor mode)", EditorStyles.miniBoldLabel);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Copy All Collider Data"))
                    {
                        clipboardManager.CopyAllMagicaCollidersData();
                    }
                    if (GUILayout.Button("Paste Data to All Colliders"))
                    {
                        clipboardManager.PasteAllMagicaCollidersData();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // batch
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("Batch", EditorStyles.miniBoldLabel);

                // Global bottom buttons.
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Remove All Colliders"))
                    {
                        RemoveAllColliders();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draw a table with bones and colliders and features to create, select and register the colliders.
        /// </summary>
        private void DrawColliderGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            {
                // create map of bone <-> collider for all existing colliders
                // used also for checking if a collider exists for a mirror bone
                Dictionary<HumanBodyBones, MagicaCapsuleCollider> boneColliderMap = new Dictionary<HumanBodyBones, MagicaCapsuleCollider>();

                foreach (HumanBodyBones bone in boneSelection.GetAvailableBones())
                {
                    // Retrieve the existing MagicaCapsuleCollider for this bone (if any).
                    MagicaCapsuleCollider existingCollider = null;
                    if (data.animator != null)
                    {
                        Transform boneTransform = data.animator.GetBoneTransform(bone);
                        if (boneTransform != null)
                        {
                            Transform dedicated = boneTransform.Find(data.CreateName(bone));
                            if (dedicated != null)
                                existingCollider = dedicated.GetComponent<MagicaCapsuleCollider>();
                        }
                    }

                    boneColliderMap.Add(bone, existingCollider);
                }

                // list all bones and colliders
                foreach (HumanBodyBones bone in boneSelection.GetAvailableBones())
                {
                    bool boneFound = boneTransformMap.TryGetValue(bone, out Transform transform);

                    boneFound = boneFound && transform != null;

                    EditorGUILayout.BeginHorizontal();
                    {
                        // mouse-over hover row
                        if (hoverRowEnabled)
                        {
                            Rect rowRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(0));
                            rowRect.width = EditorGUIUtility.currentViewWidth;

                            if (rowRect.Contains(Event.current.mousePosition))
                            {
                                EditorGUI.DrawRect(rowRect, GUIStyles.hoverRowColor);
                            }
                        }

                        // bone name
                        int width = data.useAllBones ? 150 : 106;

                        GUIContent content = boneFound ? new GUIContent(bone.ToString()) : new GUIContent(bone.ToString(), GUIStyles.ErrorIconContent.image, $"Bone {bone} not found");
                        EditorGUILayout.LabelField(content, GUILayout.Width(width));

                        EditorGUI.BeginDisabledGroup(!boneFound);
                        {

                            // get the collider for the bone if it exists
                            MagicaCapsuleCollider existingCollider = boneColliderMap[bone];

                            // collider object, can be used for pinging
                            EditorGUI.BeginDisabledGroup(true);
                            {
                                EditorGUILayout.ObjectField(existingCollider, typeof(MagicaCapsuleCollider), false);
                            }
                            EditorGUI.EndDisabledGroup();

                            // select button
                            EditorGUI.BeginDisabledGroup(existingCollider == null);
                            {
                                if (GUILayout.Button("Select", GUILayout.Width(60)))
                                {
                                    Selection.activeObject = existingCollider.gameObject;
                                }
                            }
                            EditorGUI.EndDisabledGroup();

                            // create button
                            bool canCreate = existingCollider == null;

                            EditorGUI.BeginDisabledGroup(!canCreate);
                            {
                                if (GUILayout.Button("Create", GUILayout.Width(60)))
                                {
                                    if (colliderFactory.CreateColliderForBone(bone, out MagicaCapsuleCollider capsuleCollider))
                                    {
                                        selectionManager.SelectCollider(capsuleCollider);
                                    }
                                }
                            }
                            EditorGUI.EndDisabledGroup();

                            // remove button
                            bool canRemove = existingCollider != null;

                            EditorGUI.BeginDisabledGroup(!canRemove);
                            {
                                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                                {
                                    colliderFactory.RemoveColliderForBone(bone);

                                    // remove all that are null, just in case to keep a clean registry
                                    colliderRegistry.CleanUpMagicaColliderRegistry();

                                }
                            }
                            EditorGUI.EndDisabledGroup();


                            // mirror button
                            bool isMirrorBone = BoneSelection.mirrorMap.ContainsKey(bone) || BoneSelection.mirrorMap.Values.Contains(bone);
                            bool hasMirror = false;

                            // check if mirror collider exists
                            if (isMirrorBone)
                            {
                                HumanBodyBones mirrorBone = BoneSelection.mirrorMap[bone];
                                hasMirror = boneColliderMap[mirrorBone] != null;
                            }

                            EditorGUI.BeginDisabledGroup(!isMirrorBone || !hasMirror);
                            {
                                if (GUILayout.Button("Use Mirror", GUILayout.Width(90)))
                                {
                                    HumanBodyBones sourceBone;
                                    if (BoneSelection.mirrorMap.ContainsKey(bone))
                                        sourceBone = BoneSelection.mirrorMap[bone];
                                    else
                                        sourceBone = BoneSelection.mirrorMap.First(kvp => kvp.Value.Equals(bone)).Key;

                                    if (colliderFactory.MirrorCollider(bone, sourceBone, out MagicaCapsuleCollider targetCollider))
                                    {
                                        selectionManager.SelectCollider(targetCollider);
                                    }
                                }
                            }
                            EditorGUI.EndDisabledGroup();

                            // register/unregister conditions
                            bool hasObject = existingCollider != null;

                            // check if collider is already registered
                            bool isRegistered = false;
                            if (data.magicaClothGameObject)
                            {
                                var cloth = data.magicaClothGameObject.GetComponent<MagicaCloth>();
                                if (cloth)
                                {
                                    isRegistered = colliderRegistry.IsColliderRegistered(existingCollider);
                                }
                            }

                            // register button
                            EditorGUI.BeginDisabledGroup(isRegistered || !hasObject);
                            {
                                if (GUILayout.Button("Register", GUILayout.Width(70)))
                                {
                                    colliderRegistry.RegisterCollider(existingCollider);
                                    Selection.activeGameObject = data.magicaClothGameObject;

                                }
                            }
                            EditorGUI.EndDisabledGroup();

                            // unregister button
                            EditorGUI.BeginDisabledGroup(!isRegistered || !hasObject);
                            {
                                if (GUILayout.Button("Unregister", GUILayout.Width(85)))
                                {
                                    colliderRegistry.UnregisterCollider(existingCollider);
                                    Selection.activeGameObject = data.magicaClothGameObject;
                                }
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        EditorGUI.EndDisabledGroup();

                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            // repaint continuously so the hover effect shows up as the move moves
            if (hoverRowEnabled)
            {
                if (Event.current.type == EventType.Repaint)
                    Repaint();
            }
        }

        /// <summary>
        /// Remove colliders from all bones.
        /// </summary>
        private void RemoveAllColliders()
        {
            foreach (HumanBodyBones bone in boneSelection.GetAvailableBones())
            {
                colliderFactory.RemoveColliderForBone( bone);
            }
             
            Debug.Log("All colliders removed.");

            colliderRegistry.CleanUpMagicaColliderRegistry();

            Debug.Log("Magica collider registry cleaned up.");

        }

        /// <summary>
        /// Get a list of the colliders currently available in the dialog
        /// </summary>
        /// <returns></returns>
        private List<MagicaCapsuleCollider> GetCurrentColliders()
        {
            if (data.animator == null)
                return new List<MagicaCapsuleCollider>();

            List<MagicaCapsuleCollider> list = new List<MagicaCapsuleCollider>();

            foreach (HumanBodyBones bone in boneSelection.GetAvailableBones())
            {
                // retrieve the existing MagicaCapsuleCollider for this bone
                MagicaCapsuleCollider existingCollider = null;

                Transform boneTransform = data.animator.GetBoneTransform(bone);

                if (boneTransform != null)
                {
                    Transform childTransform = boneTransform.Find(data.CreateName(bone));

                    if (childTransform != null)
                    {
                        existingCollider = childTransform.GetComponent<MagicaCapsuleCollider>();
                    }

                    if (existingCollider != null)
                    {
                        list.Add(existingCollider);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Create a gameobject with the magica cloth component
        /// </summary>
        private void CreateMagicaClothGameObject()
        {
            if (!data.humanoidRoot) 
                return;

            GameObject go = new GameObject("Magica Cloth Object");

            go.transform.SetParent(data.humanoidRoot.transform, false);

            // add magica cloth component
            go.AddComponent<MagicaCloth>();

            // assign to dialog data
            data.magicaClothGameObject = go;

            // auto-select
            Selection.activeGameObject = go;
        }

        #region Header
        private void DrawDialogHeader()
        {
            // common header
            GUIUtils.DrawHeader("Humanoid Collider Creator", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox("Visualize and create Magica Cloth 2 colliders. Register and unregister the colliders. Quick access using jump-to and selection feature. Quick processing for getting fast into fine-tuning the colliders, the created ones are a rough approximation.", MessageType.None);
                EditorGUILayout.HelpBox("Note: The detection is currently based on the name prefix. Make sure it's set properly for your specific character and cloth", MessageType.Info);
            }
        }
        #endregion Header
    }
}