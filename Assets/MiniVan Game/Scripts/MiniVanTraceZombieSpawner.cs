using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanTraceZombieSpawner : MonoBehaviour
    {
        public GameObject TraceZombiePrefab;
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
                    SpawnTraceZombies();
                }

                return;
            }

            if (AllowOfflineSpawn)
            {
                SpawnTraceZombies();
            }
        }

        [ContextMenu("Spawn Trace Zombies Here")]
        public void SpawnTraceZombies()
        {
            if (spawned || TraceZombiePrefab == null || Count <= 0)
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
            Vector3 spawnerWorldPosition = transform.position;
            Quaternion spawnerWorldRotation = transform.rotation;
            for (int i = 0; i < Count; i++)
            {
                Vector3 spawnPosition = spawnerWorldPosition + GetSpawnOffset(i, Count);
                Terrain terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    float terrainHeight = terrain.SampleHeight(spawnPosition) + terrain.transform.position.y;
                    if (spawnPosition.y < terrainHeight + 0.2f)
                    {
                        spawnPosition.y = terrainHeight + 1.6f;
                    }
                }
                GameObject zombieObject = Instantiate(TraceZombiePrefab, spawnPosition, spawnerWorldRotation);
                zombieObject.name = "TraceZombie_" + (i + 1).ToString("00") + "_" + name;

                NetworkObject networkObject = zombieObject.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.SynchronizeTransform = false;
                }

                CharacterController characterController = zombieObject.GetComponent<CharacterController>();
                if (characterController != null)
                {
                    characterController.enabled = false;
                }

                MiniVanTraceZombie zombie = zombieObject.GetComponent<MiniVanTraceZombie>();
                if (zombie != null)
                {
                    zombie.ApplySpawnPose(spawnPosition, spawnerWorldRotation);
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
                            "MiniVanTraceZombieSpawner: NetworkObject.Spawn failed for " +
                            zombieObject.name + " — " + ex.Message,
                            zombieObject);
                    }
                }

                if (characterController != null)
                {
                    characterController.enabled = false;
                }

                if (zombie != null)
                {
                    zombie.ApplySpawnPose(spawnPosition, spawnerWorldRotation);
                }
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
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SpawnRadius + Vector3.up * 0.1f;
        }

        private void RegisterNetworkPrefabIfNeeded(NetworkManager network)
        {
            if (network == null || network.NetworkConfig == null || TraceZombiePrefab == null)
            {
                return;
            }

            var prefabsList = network.NetworkConfig.Prefabs;
            if (prefabsList != null)
            {
                for (int i = 0; i < prefabsList.Prefabs.Count; i++)
                {
                    if (prefabsList.Prefabs[i] != null &&
                        prefabsList.Prefabs[i].Prefab == TraceZombiePrefab)
                    {
                        return;
                    }
                }
            }

            try
            {
                network.AddNetworkPrefab(TraceZombiePrefab);
            }
            catch
            {
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.78f, 0.62f, 0.28f, 0.95f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.4f, 0.38f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.3f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.35f, new Vector3(0.9f, 0.55f, 1.1f));
        }
    }
}
