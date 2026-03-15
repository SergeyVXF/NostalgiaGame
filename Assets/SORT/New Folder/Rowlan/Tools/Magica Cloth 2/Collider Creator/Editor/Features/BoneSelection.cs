using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rowlan.Tools.MagicaCloth2.ColliderCreator
{
    /// <summary>
    /// Humanoid bones which are supported
    /// </summary>
    public class BoneSelection
    {
        /// <summary>
        /// Humanoid bones which are commonly used. 
        /// It's not always necessary to use all bones, eg fingers, etc.
        /// </summary>
        public static readonly HumanBodyBones[] mainBones = new HumanBodyBones[]
        {
            HumanBodyBones.Head,
            HumanBodyBones.Neck,
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder
        };

        /// <summary>
        /// Mirror mapping for symmetric bones (both directions)
        /// </summary>
        public static readonly Dictionary<HumanBodyBones, HumanBodyBones> mirrorMap = new Dictionary<HumanBodyBones, HumanBodyBones>()
        {
            { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm },
            { HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperArm },
            { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm },
            { HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerArm },
            { HumanBodyBones.LeftHand, HumanBodyBones.RightHand },
            { HumanBodyBones.RightHand, HumanBodyBones.LeftHand },
            { HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder },
            { HumanBodyBones.RightShoulder, HumanBodyBones.LeftShoulder },
            { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg },
            { HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftUpperLeg },
            { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg },
            { HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftLowerLeg },
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot },
            { HumanBodyBones.RightFoot, HumanBodyBones.LeftFoot }
        };

        // Mapping from one HumanBodyBone to the next bone in the chain.
        // Note: UpperChest is optional and not all rigs use it.
        public static readonly Dictionary<HumanBodyBones, HumanBodyBones> boneChainMap = new Dictionary<HumanBodyBones, HumanBodyBones>
        {
            // Torso
            { HumanBodyBones.Hips,          HumanBodyBones.Spine },
            { HumanBodyBones.Spine,         HumanBodyBones.Chest },
            { HumanBodyBones.Chest,         HumanBodyBones.UpperChest },  // Optional
            { HumanBodyBones.UpperChest,    HumanBodyBones.Neck },
            { HumanBodyBones.Neck,          HumanBodyBones.Head },

            // Left arm
            { HumanBodyBones.LeftShoulder,  HumanBodyBones.LeftUpperArm },
            { HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm },
            { HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand },
            { HumanBodyBones.LeftHand,      HumanBodyBones.LeftMiddleDistal },

            // Right arm
            { HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm },
            { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm },
            { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
            { HumanBodyBones.RightHand,     HumanBodyBones.RightMiddleDistal },

            // Left leg
            { HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg },
            { HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftFoot },
            { HumanBodyBones.LeftFoot,      HumanBodyBones.LeftToes },

            // Right leg
            { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg },
            { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot },
            { HumanBodyBones.RightFoot,     HumanBodyBones.RightToes },
        };

        /// <summary>
        /// Inverted map, values become keys, keys become values
        /// eg to get left lower arm from left hand
        /// </summary>
        public static readonly Dictionary<HumanBodyBones, HumanBodyBones> boneChainMapInverted = boneChainMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        /// <summary>
        /// Bones that are available for modification
        /// </summary>
        private List<HumanBodyBones> availableBones = new List<HumanBodyBones>();

        /// <summary>
        /// Reference to the editor data
        /// </summary>
        private ColliderCreatorData data;

        public BoneSelection(ColliderCreatorData data)
        {
            this.data = data;

            // initial setup of the list
            UpdateAvailableBonesList(data.useAllBones);
        }

        /// <summary>
        /// Setup list of bones which are available for modification
        /// </summary>
        /// <param name="useAllBones"></param>
        public void UpdateAvailableBonesList( bool useAllBones)
        {
            availableBones.Clear();

            // main bones
            availableBones.AddRange(mainBones);

            // optionally all other bones of the humanoid
            if (useAllBones)
            {
                foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (bone == HumanBodyBones.LastBone)
                        continue;

                    if (availableBones.Contains(bone))
                        continue;

                    availableBones.Add(bone);
                }
            }
        }

        /// <summary>
        /// Get list of bones which are available for modification
        /// </summary>
        /// <returns></returns>
        public List<HumanBodyBones> GetAvailableBones()
        {
            return availableBones;
        }

    }
}