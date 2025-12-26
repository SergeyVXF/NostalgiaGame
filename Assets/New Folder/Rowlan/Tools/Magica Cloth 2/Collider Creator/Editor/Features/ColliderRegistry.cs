using MagicaCloth2;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Tools.MagicaCloth2.ColliderCreator
{
    /// <summary>
    /// Modify the magica cloth gameobject data, particularly the collider list.
    /// </summary>
    public class ColliderRegistry
    {
        /// <summary>
        /// Reference to the editor data
        /// </summary>
        private ColliderCreatorData data;

        public ColliderRegistry(ColliderCreatorData data)
        {
            this.data = data;
        }

        /// <summary>
        /// Get the magica cloth and its collider list from the dialog data
        /// </summary>
        /// <param name="cloth"></param>
        /// <param name="colliderList"></param>
        /// <returns></returns>
        private bool GetColliderList(out MagicaCloth cloth, out List<ColliderComponent> colliderList)
        {
            // get components
            // note: the syntax with ? doesn't work here in case the gameobject was deleted, ie is missing and not null
            cloth = data.magicaClothGameObject == null ? null : data.magicaClothGameObject.GetComponent<MagicaCloth>();
            colliderList = cloth == null ? null : cloth.SerializeData.colliderCollisionConstraint?.colliderList;

            return cloth != null && colliderList != null;

        }

        /// <summary>
        /// Check if a collider is already registered with magica cloth
        /// </summary>
        /// <param name="collider"></param>
        /// <returns></returns>
        public bool IsColliderRegistered( MagicaCapsuleCollider collider)
        {
            if (GetColliderList(out MagicaCloth cloth, out List<ColliderComponent> colliderList))
            {
                return colliderList.Contains(collider);
            }

            return false;
        }

        /// <summary>
        /// Register a collider in the magica cloth collider list
        /// </summary>
        /// <param name="collider"></param>
        public void RegisterCollider(MagicaCapsuleCollider collider)
        {
            if (collider == null)
                return;

            if (GetColliderList(out MagicaCloth cloth, out List<ColliderComponent> colliderList))
            {
                if (!colliderList.Contains(collider))
                {
                    colliderList.Add(collider);

                    EditorUtility.SetDirty(cloth);

                    Debug.Log($"Registered collider {collider.name}");
                }
                else
                {
                    Debug.Log($"Collider {collider.name} is already registered.");
                }

                // remove nulls in case there are left-overs
                CleanUpMagicaColliderRegistry();
            }
        }

        /// <summary>
        /// Remove a collider from the magica cloth collider list
        /// </summary>
        /// <param name="collider"></param>
        public void UnregisterCollider(MagicaCapsuleCollider collider)
        {
            if (collider == null)
                return;

            if (GetColliderList(out MagicaCloth cloth, out List<ColliderComponent> colliderList))
            {
                if (colliderList.Contains(collider))
                {
                    colliderList.Remove(collider);

                    EditorUtility.SetDirty(cloth);

                    Debug.Log($"Unregistered collider {collider.name}");
                }
                else
                {
                    Debug.Log($"Collider {collider.name} was not registered.");
                }

                // remove all that are null, just in case to keep a clean registry
                CleanUpMagicaColliderRegistry();
            }
        }


        /// <summary>
        /// Clean up null references in the magica cloth collider list.
        /// </summary>
        public void CleanUpMagicaColliderRegistry()
        {
            if( GetColliderList(out MagicaCloth cloth, out List<ColliderComponent> colliderList))
            {
                colliderList.RemoveAll(item => item == null);
                
                EditorUtility.SetDirty(cloth);
            }
        }

    }
}