using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Manual / test spawner for acid zombies. Mirrors <see cref="MiniVanVampireSpawner"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanAcidZombieSpawner : MonoBehaviour
    {
        public GameObject AcidZombiePrefab;
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
                    SpawnAcidZombies();
                }

                return;
            }

            if (AllowOfflineSpawn)
            {
                SpawnAcidZombies();
            }
        }

        [ContextMenu("Spawn Acid Zombies Here")]
        public void SpawnAcidZombies()
        {
            if (spawned || AcidZombiePrefab == null || Count <= 0)
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
                GameObject zombieObject = Instantiate(
                    AcidZombiePrefab,
                    spawnPosition,
                    spawnerWorldRotation);
                zombieObject.name = "AcidZombie_" + (i + 1).ToString("00") + "_" + name;

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

                MiniVanAcidZombie zombie = zombieObject.GetComponent<MiniVanAcidZombie>();
                if (zombie != null)
                {
                    zombie.ApplySpawnPose(spawnPosition, spawnerWorldRotation);
                }
                else
                {
                    zombieObject.transform.SetPositionAndRotation(spawnPosition, spawnerWorldRotation);
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
                            "MiniVanAcidZombieSpawner: NetworkObject.Spawn failed for " +
                            zombieObject.name + " — AI will still run on the server. " + ex.Message,
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
                else
                {
                    zombieObject.transform.SetPositionAndRotation(spawnPosition, spawnerWorldRotation);
                }

                Debug.Log(
                    "MiniVanAcidZombieSpawner: spawned " + zombieObject.name +
                    " at " + spawnPosition + " (spawner " + name + " @ " + spawnerWorldPosition + ")",
                    zombieObject);
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
            if (network == null || network.NetworkConfig == null || AcidZombiePrefab == null)
            {
                return;
            }

            var prefabsList = network.NetworkConfig.Prefabs;
            if (prefabsList != null)
            {
                for (int i = 0; i < prefabsList.Prefabs.Count; i++)
                {
                    if (prefabsList.Prefabs[i] != null &&
                        prefabsList.Prefabs[i].Prefab == AcidZombiePrefab)
                    {
                        return;
                    }
                }
            }

            try
            {
                network.AddNetworkPrefab(AcidZombiePrefab);
            }
            catch
            {
                // Already registered through another path.
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.45f, 0.95f, 0.12f, 0.95f);
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
