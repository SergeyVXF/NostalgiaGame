using MagicaCloth2;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Tools.MagicaCloth2.ColliderCreator
{
    public class ColliderFactory
    {
        /// <summary>
        /// Reference to the editor data
        /// </summary>
        private ColliderCreatorData data;
        public ColliderFactory(ColliderCreatorData data)
        {
            this.data = data;
        }

        /// <summary>
        /// Create a MagicaCapsuleCollider for the specified bone.
        /// The collider is placed on a new child GameObject with its local rotation set.
        /// </summary>
        public bool CreateColliderForBone( HumanBodyBones bone, out MagicaCapsuleCollider capsuleCollider)
        {
            capsuleCollider = null;

            Animator animator = data.animator;

            if ( animator == null)
            {
                Debug.LogWarning("Humanoid root or Animator not assigned.");
                return false;
            }

            Transform boneTransform = animator.GetBoneTransform(bone);

            if (boneTransform == null)
            {
                Debug.LogWarning($"Bone not found: {bone.ToString()}");
                return false;
            }

            // check for existing collider
            if (boneTransform.Find(data.CreateName(bone)) != null)
            {
                Debug.Log($"Collider already exists for {bone.ToString()}");
                return false;
            }

            // determine collider size and orientation

            Vector3 worldDirection = Vector3.up;

            // default height
            float height = 0.02f;
            float radius = 0.02f;
            Vector3 center = Vector3.zero;

            // special handling, eg head, hips, etc
            if (bone == HumanBodyBones.Head) // head is top of the chain
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);

                worldDirection = head.position - neck.position;

                Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);

                Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

                radius = Vector3.Distance(leftUpperArm.transform.position, rightUpperArm.transform.position);
                radius /= 4f;
                height = radius * 1.5f;

                center = new Vector3(0, height * 0.5f, 0);
            }
            else if (bone == HumanBodyBones.Hips)
            {
                Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                Transform leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);

                float dSpine = (spine != null) ? Vector3.Distance(boneTransform.position, spine.position) : 0f;
                float dLeft = (leftUpperLeg != null) ? Vector3.Distance(boneTransform.position, leftUpperLeg.position) : 0f;
                float dRight = (rightUpperLeg != null) ? Vector3.Distance(boneTransform.position, rightUpperLeg.position) : 0f;

                worldDirection = (spine != null) ? (spine.position - boneTransform.position).normalized : Vector3.up;

                height = Mathf.Max(dSpine, dLeft, dRight);

                radius = height * 1.5f;

                center = new Vector3(0, height * 0.5f, 0);

            }
            else if (bone == HumanBodyBones.Chest) // heuristics ... similar to Hips
            {
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

                float dNeck = (neck != null) ? Vector3.Distance(boneTransform.position, neck.position) : 0f;
                float dLeft = (leftUpperArm != null) ? Vector3.Distance(boneTransform.position, leftUpperArm.position) : 0f;
                float dRight = (rightUpperArm != null) ? Vector3.Distance(boneTransform.position, rightUpperArm.position) : 0f;

                worldDirection = (neck != null) ? (neck.position - boneTransform.position).normalized : Vector3.up;

                height = Mathf.Max(dNeck, dLeft, dRight);

                radius = height * 0.5f;

                center = new Vector3(0, height * 0.5f, 0);

            }
            else if (bone == HumanBodyBones.Spine) // heuristics ... similar to Hips
            {
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

                float dNeck = (neck != null) ? Vector3.Distance(boneTransform.position, neck.position) : 0f;
                float dLeft = (leftUpperArm != null) ? Vector3.Distance(boneTransform.position, leftUpperArm.position) : 0f;
                float dRight = (rightUpperArm != null) ? Vector3.Distance(boneTransform.position, rightUpperArm.position) : 0f;

                worldDirection = (neck != null) ? (neck.position - boneTransform.position).normalized : Vector3.up;

                height = Mathf.Max(dNeck, dLeft, dRight);

                radius = height * 0.25f;

                center = new Vector3(0, height * 0.5f, 0);

            }
            // all other bones
            else if (boneTransform.childCount > 0)
            {
                bool hasTargetBone = BoneSelection.boneChainMap.TryGetValue(bone, out HumanBodyBones targetBone);

                if (hasTargetBone)
                {
                    Transform targetTransform = animator.GetBoneTransform(targetBone);

                    Vector3 localBonePosition = boneTransform.InverseTransformPoint(boneTransform.position);
                    Vector3 localTargetPosition = boneTransform.InverseTransformPoint(targetTransform.position);

                    worldDirection = (targetTransform.position - boneTransform.position).normalized;

                    float localDistance = Vector3.Distance(boneTransform.position, targetTransform.position);

                    height = localDistance;

                    float radiusFactor;

                    switch (bone)
                    {
                        case HumanBodyBones.Neck:
                            radiusFactor = 0.4f;
                            break;

                        case HumanBodyBones.LeftShoulder:
                        case HumanBodyBones.RightShoulder:
                            radiusFactor = 0.5f;
                            break;

                        case HumanBodyBones.LeftUpperArm:
                        case HumanBodyBones.RightUpperArm:
                            radiusFactor = 0.16f;
                            break;
                        case HumanBodyBones.LeftLowerArm:
                        case HumanBodyBones.RightLowerArm:
                            radiusFactor = 0.12f;
                            break;
                        case HumanBodyBones.LeftUpperLeg:
                        case HumanBodyBones.RightUpperLeg:
                            radiusFactor = 0.15f;
                            break;
                        case HumanBodyBones.LeftLowerLeg:
                        case HumanBodyBones.RightLowerLeg:
                            radiusFactor = 0.1f;
                            break;
                        default:
                            radiusFactor = 0.25f;
                            break;
                    }
                    radius = height * radiusFactor;

                    center = new Vector3(0, height * 0.5f, 0);
                }
            }

            // create dedicated collider gameobject
            GameObject colliderGO = new GameObject(data.CreateName(bone));
            colliderGO.transform.SetParent(boneTransform, false);
            colliderGO.transform.localPosition = Vector3.zero;

            // rotate the collider object by converting world direction into the local space of the bone
            Vector3 localDirection = boneTransform.InverseTransformDirection(worldDirection);
            Quaternion localRotation = Quaternion.FromToRotation(Vector3.up, localDirection);
            colliderGO.transform.localRotation = localRotation;

            // add collider component
            capsuleCollider = colliderGO.AddComponent<MagicaCapsuleCollider>();

            // collider settings            
            capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
            capsuleCollider.SetSize(radius, radius, height);
            capsuleCollider.center = center;

            // undo
            Undo.RegisterCreatedObjectUndo(colliderGO, "Create collider on " + bone.ToString());

            return true;
        }


        /// <summary>
        /// Mirror collider settings and gameobject transform from the source to the target bone
        /// </summary>
        public bool MirrorCollider( HumanBodyBones targetBone, HumanBodyBones sourceBone, out MagicaCapsuleCollider targetCollider)
        {
            targetCollider = null;

            Animator animator = data.animator;

            if (animator == null)
            {
                Debug.LogWarning("Humanoid root or Animator not assigned.");
                return false;
            }

            // get the source collider.
            MagicaCapsuleCollider sourceCollider = null;

            Transform sourceTransform = animator.GetBoneTransform(sourceBone);
            if (sourceTransform != null)
            {
                Transform dedicatedTransform = sourceTransform.Find(data.CreateName(sourceBone));
                if (dedicatedTransform != null)
                    sourceCollider = dedicatedTransform.GetComponent<MagicaCapsuleCollider>();
            }

            if (sourceCollider == null)
            {
                Debug.LogWarning($"Source collider not found for bone {sourceBone}");
                return false;
            }

            // get or create the target collider.
            targetCollider = null;

            Transform targetTransform = animator.GetBoneTransform(targetBone);
            if (targetTransform == null)
            {
                Debug.LogWarning($"Target collider could not be created for bone {targetBone}");
                return false;
            }

            Transform childTargetTransform = targetTransform.Find(data.CreateName(targetBone));
            if (childTargetTransform == null)
            {
                if (CreateColliderForBone( targetBone, out MagicaCapsuleCollider capsuleCollider))
                {
                    // nothing to do, will be selected outside
                }
                childTargetTransform = targetTransform.Find(data.CreateName(targetBone));
            }

            if (childTargetTransform != null)
                targetCollider = childTargetTransform.GetComponent<MagicaCapsuleCollider>();

            if (targetCollider == null)
            {
                Debug.LogWarning($"Target collider could not be created for bone {targetBone}");
                return false;
            }

            // mirror collider
            // ------------------------

            // copy basic collider settings.
            float startRadius = sourceCollider.GetSize().x;
            float endRadius = sourceCollider.GetSize().y;
            float length = sourceCollider.GetSize().z;

            targetCollider.SetSize(startRadius, endRadius, length);
            targetCollider.direction = sourceCollider.direction;
            targetCollider.center = sourceCollider.center;

            // mirror transform
            // ------------------------

            // mirror the collider gameobject transform
            Transform sourceT = sourceCollider.transform;
            Transform targetT = targetCollider.transform;

            // define the mirror plane's normal (e.g., for YZ plane)
            Vector3 mirrorNormal = animator.transform.right;

            // get the original forward and up directions from the current rotation
            Vector3 originalForward = sourceT.rotation * Vector3.forward;
            Vector3 originalUp = sourceT.rotation * Vector3.up;

            // reflect these directions about the mirror plane
            Vector3 mirroredForward = Vector3.Reflect(originalForward, mirrorNormal);
            Vector3 mirroredUp = Vector3.Reflect(originalUp, mirrorNormal);

            // construct the new mirrored rotation
            Quaternion mirroredRotation = Quaternion.LookRotation(mirroredForward, mirroredUp);

            // apply the mirrored rotation
            targetT.rotation = mirroredRotation;


            Debug.Log($"Target collider could not be created for bone {sourceBone} to {targetBone}");

            return true;
        }

        /// <summary>
        /// Removes the MagicaCapsuleCollider for the specified bone (if it exists).
        /// </summary>
        public void RemoveColliderForBone( HumanBodyBones bone)
        {
            if (data.humanoidRoot == null || data.animator == null)
                return;

            Transform boneTransform = data.animator.GetBoneTransform(bone);

            if (boneTransform == null)
                return;

            Transform childTransform = boneTransform.Find(data.CreateName(bone));

            if (childTransform != null)
            {
                Undo.DestroyObjectImmediate(childTransform.gameObject);
            }
        }

    }
}