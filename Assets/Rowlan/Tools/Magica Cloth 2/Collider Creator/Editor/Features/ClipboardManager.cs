using MagicaCloth2;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Tools.MagicaCloth2.ColliderCreator
{
    /// <summary>
    /// Copy and paste of bone and collider data
    /// </summary>
    public class ClipboardManager
    {
        #region json data classes

        [System.Serializable]
        private class MagicaColliderDataCollection
        {
            public MagicaColliderData[] colliderDataList;
        }

        [System.Serializable]
        private class MagicaColliderData
        {
            public string path;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Vector3 center;
            public int direction;
            public float startRadius;
            public float endRadius;
            public float length;
        }

        #endregion json data classes

        /// <summary>
        /// Clipboard for storing MagicaCapsuleCollider data in JSON format
        /// </summary>
        private string colliderDataClipboard = string.Empty;

        /// <summary>
        /// Reference to the humanoid from which the data were copied
        /// </summary>
        private GameObject sourceHumanoidRoot;

        /// <summary>
        /// Reference to the editor data
        /// </summary>
        private ColliderCreatorData data;

        public ClipboardManager(ColliderCreatorData data)
        {
            this.data = data;
        }

        /// <summary>
        /// Copy the collider data of the humanoid to the clipboard
        /// </summary>
        public void CopyAllMagicaCollidersData()
        {
            // register for paste check
            sourceHumanoidRoot = data.humanoidRoot;

            var colliders = data.humanoidRoot.GetComponentsInChildren<MagicaCapsuleCollider>(true);

            List<MagicaColliderData> dataList = new List<MagicaColliderData>();

            foreach (var collider in colliders)
            {
                var colliderData = new MagicaColliderData();

                colliderData.path = GetTransformPath(collider.transform, data.humanoidRoot);
                colliderData.position = collider.transform.localPosition;
                colliderData.rotation = collider.transform.localRotation;
                colliderData.scale = collider.transform.localScale;
                colliderData.center = collider.center;
                colliderData.direction = (int)collider.direction;

                Vector3 s = collider.GetSize(); // e.g. (startRadius, endRadius, length)
                colliderData.startRadius = s.x;
                colliderData.endRadius = s.y;
                colliderData.length = s.z;

                dataList.Add(colliderData);
            }
            
            var collection = new MagicaColliderDataCollection { colliderDataList = dataList.ToArray() };

            colliderDataClipboard = JsonUtility.ToJson(collection, true);

            Debug.Log( $"Copied collider data\n{colliderDataClipboard}");
        }

        /// <summary>
        /// Paste the collider data from the clipboard to the humanoid
        /// </summary>
        public void PasteAllMagicaCollidersData()
        {
            // clipboard check: ensure the source matches the target
            if( sourceHumanoidRoot == null || data.humanoidRoot == null || sourceHumanoidRoot.transform != data.humanoidRoot.transform )
            {
                Debug.LogError("Humanoid mismatch, can't paste data");
                return;
            }

            // check clipboard data validity
            if (string.IsNullOrEmpty(colliderDataClipboard))
            {
                Debug.LogWarning("No collider data to paste.");
                return;
            }

            // get data from clipboard
            var colliderDataCollection = JsonUtility.FromJson<MagicaColliderDataCollection>(colliderDataClipboard);
            if (colliderDataCollection == null || colliderDataCollection.colliderDataList == null)
            {
                Debug.LogWarning("Invalid data in clipboard.");
                return;
            }

            foreach (var colliderData in colliderDataCollection.colliderDataList)
            {
                var transform = data.humanoidRoot.transform.Find(colliderData.path);

                if (!transform)
                {
                    Debug.LogWarning($"Could not find transform path: {colliderData.path}");
                    continue;
                }

                var magicaCapsuleCollider = transform.GetComponent<MagicaCapsuleCollider>();
                if (!magicaCapsuleCollider)
                {
                    Debug.LogWarning($"No MagicaCapsuleCollider on {transform.name}.");
                    continue;
                }

                transform.localPosition = colliderData.position;
                transform.localRotation = colliderData.rotation;
                transform.localScale = colliderData.scale;

                magicaCapsuleCollider.center = colliderData.center;
                magicaCapsuleCollider.direction = (MagicaCapsuleCollider.Direction)colliderData.direction;

                magicaCapsuleCollider.SetSize(colliderData.startRadius, colliderData.endRadius, colliderData.length);
            }

            Debug.Log("Pasted collider data from clipboard");
        }

        private static string GetTransformPath(Transform transform, GameObject humanoidRoot)
        {
            if (transform == null || transform == humanoidRoot.transform)
                return string.Empty;

            return GetParentPath(transform, humanoidRoot);
        }

        private static string GetParentPath(Transform transform, GameObject humanoidRoot)
        {
            if (!transform.parent || transform.parent == humanoidRoot.transform)
                return transform.name;

            return GetParentPath(transform.parent, humanoidRoot) + "/" + transform.name;

        }
    }
}