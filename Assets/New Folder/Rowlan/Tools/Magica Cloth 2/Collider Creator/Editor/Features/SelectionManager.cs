using MagicaCloth2;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Tools.MagicaCloth2.ColliderCreator
{
    /// <summary>
    /// Utilities for selection of colliders, magica cloth gameobjects, etc
    /// </summary>
    public class SelectionManager
    {
        /// <summary>
        /// Reference to the editor data
        /// </summary>
        private ColliderCreatorData data;

        public SelectionManager(ColliderCreatorData data)
        {
            this.data = data;
        }

        /// <summary>
        /// Select specific collider
        /// </summary>
        /// <param name="collider"></param>
        public void SelectCollider( MagicaCapsuleCollider collider)
        {
            Selection.activeGameObject = collider.gameObject;
        }

        /// <summary>
        /// Clear the selection
        /// </summary>
        public void ClearSelection()
        {
            Selection.objects = new Object[0];
        }

        /// <summary>
        /// Select all magica colliders in the hierarchy
        /// </summary>
        public void SelectAllColliders()
        {
            if (!data.humanoidRoot)
                return;

            MagicaCapsuleCollider[] colliderList = data.humanoidRoot.GetComponentsInChildren<MagicaCapsuleCollider>(true);

            List<Object> objects = new List<Object>();

            foreach (var collider in colliderList)
            {
                objects.Add(collider.gameObject);
            }

            Selection.objects = objects.ToArray();
        }

        /// <summary>
        /// Select all magica colliders which are used in the dialog
        /// </summary>
        /// <param name="currentColliders">The colliders which are used in the dialog</param>
        public void SelectCurrentColliders(List<MagicaCapsuleCollider> currentColliders)
        {
            if (!data.humanoidRoot)
                return;

            MagicaCapsuleCollider[] colliderList = data.humanoidRoot.GetComponentsInChildren<MagicaCapsuleCollider>(true);

            List<Object> objects = new List<Object>();

            foreach (var collider in colliderList)
            {
                if (currentColliders.Contains(collider))
                {
                    objects.Add(collider.gameObject);
                }
            }

            Selection.objects = objects.ToArray();
        }
    }
}