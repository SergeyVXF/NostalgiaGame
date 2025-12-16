using System;
using UnityEngine;

namespace Rowlan.Tools.MagicaCloth2.ColliderCreator
{
    [Serializable]
    public class ColliderCreatorData
    {
        public GameObject humanoidRoot;
        public GameObject magicaClothGameObject;
        public GameObject lastMagicaClothReference; // For detecting changes in the cloth reference
        public string colliderPrefix = "MagicaCapsuleCollider_";
        public bool useAllBones = false;

        public Animator animator;


        public string CreateName(HumanBodyBones bone)
        {
            return $"{colliderPrefix}{bone.ToString()}";
        }
    }
}