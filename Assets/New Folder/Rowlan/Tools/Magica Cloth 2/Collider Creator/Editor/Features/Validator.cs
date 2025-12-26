using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Tools.MagicaCloth2.ColliderCreator
{
    public class Validator
    {
        /// <summary>
        /// Check if all main bones have a transform.
        /// Show console error if transforms are missing.
        /// </summary>
        /// <param name="animator"></param>
        /// <param name="boneValidityMap">map of bone as key and whether transform was found or not</param>
        /// <returns>true if all main bones have matching transforms, false otherwise</returns>
        public static Dictionary<HumanBodyBones, Transform> ValidateBones( Animator animator)
        {
            Dictionary<HumanBodyBones, Transform> boneValidityMap = new Dictionary<HumanBodyBones, Transform>();

            foreach(HumanBodyBones bone in BoneSelection.mainBones)
            {
                Transform boneTransform = animator == null ? null : animator.GetBoneTransform(bone);

                boneValidityMap[bone] = boneTransform;
            }

            return boneValidityMap;
        }

    }
}