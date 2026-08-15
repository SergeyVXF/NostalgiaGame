using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Manual / test spawner for vampires. Count can be any number; default 1 for tuning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanVampireSpawner : MonoBehaviour
    {
        public GameObject VampirePrefab;
        [Min(0)] public int Count = 1;
        [Min(0f)] public float SpawnRadius = 1.5f;
        public bool SpawnOnServerStart = true;
        public bool AllowOfflineSpawn = true;

        private bool spawned;

        private void Update()
        {
            if (!Application.isPlaying || !SpawnOnServerStart || spawned)
            {
                return;
            }

            NetworkManager network = NetworkManager.Singleton;
            if (network != null && network.IsListening)
            {
                if (network.IsServer)
                {
                    SpawnVampires();
                }

                return;
            }

            if (AllowOfflineSpawn)
            {
                SpawnVampires();
            }
        }

        [ContextMenu("Spawn Vampires Here")]
        public void SpawnVampires()
        {
            if (spawned || VampirePrefab == null || Count <= 0)
            {
                return;
            }

            NetworkManager network = NetworkManager.Singleton;
            bool networked = network != null && network.IsListening;
            if (networked && !network.IsServer)
            {
                return;
            }

            RegisterNetworkPrefabIfNeeded(network);
            int spawnCount = Mathf.Max(0, Count);
            Vector3 spawnerWorldPosition = transform.position;
            Quaternion spawnerWorldRotation = transform.rotation;
            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 spawnPosition = spawnerWorldPosition + GetSpawnOffset(i, spawnCount);
                GameObject vampireObject = Instantiate(
                    VampirePrefab,
                    spawnPosition,
                    spawnerWorldRotation);
                vampireObject.name = "Vampire_" + (i + 1).ToString("00") + "_" + name;

                // NGO SynchronizeTransform + CharacterController both love to discard Instantiate pose.
                NetworkObject networkObject = vampireObject.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.SynchronizeTransform = false;
                }

                CharacterController characterController = vampireObject.GetComponent<CharacterController>();
                if (characterController != null)
                {
                    characterController.enabled = false;
                }

                MiniVanVampire vampire = vampireObject.GetComponent<MiniVanVampire>();
                if (vampire != null)
                {
                    vampire.ApplySpawnPose(spawnPosition, spawnerWorldRotation);
                }
                else
                {
                    vampireObject.transform.SetPositionAndRotation(spawnPosition, spawnerWorldRotation);
                }

                if (networked && networkObject != null && !networkObject.IsSpawned)
                {
                    try
                    {
                        networkObject.Spawn(true);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning(
                            "MiniVanVampireSpawner: NetworkObject.Spawn failed for " +
                            vampireObject.name + " — AI will still run on the server. " + ex.Message,
                            vampireObject);
                    }
                }

                if (characterController != null)
                {
                    characterController.enabled = false;
                }

                if (vampire != null)
                {
                    vampire.ApplySpawnPose(spawnPosition, spawnerWorldRotation);
                }
                else
                {
                    vampireObject.transform.SetPositionAndRotation(spawnPosition, spawnerWorldRotation);
                }

                Debug.Log(
                    "MiniVanVampireSpawner: spawned " + vampireObject.name +
                    " at " + spawnPosition + " (spawner " + name + " @ " + spawnerWorldPosition + ")",
                    vampireObject);
            }

            spawned = true;
        }

        public void ResetSpawner()
        {
            spawned = false;
        }

        private Vector3 GetSpawnOffset(int index, int count)
        {
            if (count <= 1 || SpawnRadius <= 0.01f)
            {
                return Vector3.up * 0.1f;
            }

            float angle = (Mathf.PI * 2f * index) / count;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SpawnRadius +
                   Vector3.up * 0.1f;
        }

        private void RegisterNetworkPrefabIfNeeded(NetworkManager network)
        {
            if (network == null || network.NetworkConfig == null || VampirePrefab == null)
            {
                return;
            }

            var prefabsList = network.NetworkConfig.Prefabs;
            if (prefabsList != null)
            {
                for (int i = 0; i < prefabsList.Prefabs.Count; i++)
                {
                    if (prefabsList.Prefabs[i] != null &&
                        prefabsList.Prefabs[i].Prefab == VampirePrefab)
                    {
                        return;
                    }
                }
            }

            try
            {
                network.AddNetworkPrefab(VampirePrefab);
            }
            catch
            {
                // Already registered through another path.
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.75f, 0.1f, 0.85f, 0.95f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.0f, 0.42f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.3f);
            Gizmos.DrawWireCube(
                transform.position + Vector3.up * 0.95f,
                new Vector3(0.7f, 1.9f, 0.7f));
            if (Count > 1 && SpawnRadius > 0.01f)
            {
                Gizmos.DrawWireSphere(transform.position, SpawnRadius);
            }
        }
    }
}
