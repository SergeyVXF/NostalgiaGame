using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanZombieSpawnerAny : MonoBehaviour
    {
        public GameObject ZombiePrefab;
        public bool SpawnOnServerStart = true;
        public bool AllowOfflineSpawn;

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
                    SpawnZombie();
                }

                return;
            }

            if (AllowOfflineSpawn)
            {
                SpawnZombie();
            }
        }

        [ContextMenu("Spawn Zombie Here")]
        public void SpawnZombie()
        {
            if (spawned || ZombiePrefab == null)
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
            GameObject zombie = Instantiate(ZombiePrefab, transform.position, transform.rotation);
            zombie.name = "Zombie_Any_" + name;

            NetworkObject networkObject = zombie.GetComponent<NetworkObject>();
            if (networked && networkObject != null && !networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }

            spawned = true;
        }

        public void ResetSpawner()
        {
            spawned = false;
        }

        private void RegisterNetworkPrefabIfNeeded(NetworkManager network)
        {
            if (network == null || network.NetworkConfig == null || ZombiePrefab == null)
            {
                return;
            }

            try
            {
                network.AddNetworkPrefab(ZombiePrefab);
            }
            catch
            {
                // The shared zombie prefab may already be registered by another spawner.
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.25f, 1f, 0.18f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.95f, 0.38f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.2f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.95f, new Vector3(0.7f, 1.9f, 0.7f));
        }
    }
}
