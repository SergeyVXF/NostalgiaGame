using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace MiniVanGame.EditorTools
{
    /// <summary>
    /// Builds editable prefabs for the car-battery charger and wall socket from the scene prototypes.
    /// </summary>
    public static class MiniVanBatteryChargerBuilder
    {
        public const string PrefabFolder = "Assets/MiniVan Game/Prefabs/BatteryCharger";
        public const string ChargerPrefabPath = PrefabFolder + "/AKB_Recharger.prefab";
        public const string SocketPrefabPath = PrefabFolder + "/ElectricySoketDefault.prefab";
        public const string CablePrefabPath = "Assets/MiniVan Game/Prefabs/Bridge/PoweredBridge_PowerCable.prefab";

        [MenuItem("MiniVan Game/Battery Charger/Build Prefabs From Scene")]
        public static void BuildPrefabsFromScene()
        {
            EnsureFolder(PrefabFolder);

            GameObject sceneCharger = GameObject.Find("AKB_Recharger");
            GameObject sceneSocket = GameObject.Find("ElectricySoketDefault");
            if (sceneCharger == null || sceneSocket == null)
            {
                Debug.LogError("[BatteryCharger] Scene needs AKB_Recharger and ElectricySoketDefault.");
                return;
            }

            GameObject chargerPrefab = BuildChargerPrefab(sceneCharger);
            GameObject socketPrefab = BuildSocketPrefab(sceneSocket);

            MiniVanEquipmentUiBuilder.RegisterNetworkPrefab(chargerPrefab);
            // Wall socket is a static scene prop; no NetworkObject required.

            // Replace scene instances with prefab instances so the map uses the same assets.
            ReplaceSceneObjectWithPrefab(sceneCharger, chargerPrefab);
            ReplaceSceneObjectWithPrefab(sceneSocket, socketPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BatteryCharger] Prefabs ready:\n  " + ChargerPrefabPath + "\n  " + SocketPrefabPath);
            EditorGUIUtility.PingObject(chargerPrefab);
        }

        private static GameObject BuildChargerPrefab(GameObject sceneSource)
        {
            GameObject working = Object.Instantiate(sceneSource);
            working.name = "AKB_Recharger";

            // Drop leftover bridge receiver behaviour if any was copied.
            MiniVanBridgeBatteryReceiver bridgeReceiver = working.GetComponent<MiniVanBridgeBatteryReceiver>();
            if (bridgeReceiver != null)
            {
                Object.DestroyImmediate(bridgeReceiver);
            }

            Rigidbody body = working.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = working.AddComponent<Rigidbody>();
            }

            body.mass = 22f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            NetworkObject net = working.GetComponent<NetworkObject>();
            if (net == null)
            {
                net = working.AddComponent<NetworkObject>();
            }

            MiniVanBatteryCharger charger = working.GetComponent<MiniVanBatteryCharger>();
            if (charger == null)
            {
                charger = working.AddComponent<MiniVanBatteryCharger>();
            }

            Transform batterySocket = working.transform.Find("Battery Socket");
            Transform cableSocketTransform = working.transform.Find("Receiver Cable Socket");
            MiniVanBridgeCableSocket cableSocket = cableSocketTransform != null
                ? cableSocketTransform.GetComponent<MiniVanBridgeCableSocket>()
                : null;

            if (cableSocket != null)
            {
                cableSocket.Role = MiniVanBridgeCableSocketRole.Receiver;
                cableSocket.OwnerCharger = charger;
                if (cableSocket.PlugPose == null)
                {
                    Transform plug = cableSocketTransform.Find("Plug Pose");
                    cableSocket.PlugPose = plug;
                }
            }

            // Attach a dedicated power cable whose End A is permanently locked to the charger.
            MiniVanBridgePowerCable existingCable = working.GetComponentInChildren<MiniVanBridgePowerCable>(true);
            if (existingCable == null)
            {
                GameObject cablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CablePrefabPath);
                if (cablePrefab != null)
                {
                    GameObject cableInstance = (GameObject)PrefabUtility.InstantiatePrefab(cablePrefab);
                    cableInstance.name = "Charger_PowerCable";
                    cableInstance.transform.SetParent(working.transform, false);
                    existingCable = cableInstance.GetComponent<MiniVanBridgePowerCable>();
                }
            }

            if (existingCable != null)
            {
                existingCable.PermanentlyAnchoredEndIndex = 0;
                if (existingCable.CableVisual == null)
                {
                    existingCable.CableVisual = existingCable.GetComponentInChildren<MiniVanBridgeCableVisual>(true);
                }

                if (cableSocket != null)
                {
                    existingCable.ConnectEndToSocket(0, cableSocket);
                }

                // Park free end near the charger so it is easy to grab in the editor.
                if (existingCable.EndB != null)
                {
                    existingCable.EndB.position = working.transform.position + working.transform.right * 0.55f + Vector3.up * 0.35f;
                    existingCable.EndB.rotation = working.transform.rotation;
                }
            }

            charger.BatteryPlacementPoint = batterySocket;
            charger.ChargerCableSocket = cableSocket;
            charger.PowerCable = existingCable;

            // Ensure display TextMesh exists before saving.
            charger.EnsureDisplayText();
            charger.DisplayText = working.GetComponentInChildren<TextMesh>(true);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(working, ChargerPrefabPath);
            Object.DestroyImmediate(working);
            return prefab;
        }

        private static GameObject BuildSocketPrefab(GameObject sceneSource)
        {
            GameObject working = Object.Instantiate(sceneSource);
            working.name = "ElectricySoketDefault";

            MiniVanBridgeCableSocket socket = working.GetComponent<MiniVanBridgeCableSocket>();
            if (socket == null)
            {
                socket = working.AddComponent<MiniVanBridgeCableSocket>();
            }

            socket.Role = MiniVanBridgeCableSocketRole.Mechanism;
            if (socket.PlugPose == null)
            {
                Transform plug = working.transform.Find("Plug Pose");
                socket.PlugPose = plug;
            }

            BoxCollider box = working.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.isTrigger = true;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(working, SocketPrefabPath);
            Object.DestroyImmediate(working);
            return prefab;
        }

        private static void ReplaceSceneObjectWithPrefab(GameObject sceneObject, GameObject prefab)
        {
            if (sceneObject == null || prefab == null)
            {
                return;
            }

            Vector3 position = sceneObject.transform.position;
            Quaternion rotation = sceneObject.transform.rotation;
            Vector3 scale = sceneObject.transform.localScale;
            Transform parent = sceneObject.transform.parent;
            string name = sceneObject.name;

            Object.DestroyImmediate(sceneObject);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.scene);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
