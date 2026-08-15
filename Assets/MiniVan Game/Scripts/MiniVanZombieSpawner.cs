using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanZombieSpawner : NetworkBehaviour
    {
        public GameObject ZombiePrefab;
        public int ZombieCount = 3;
        public float SpawnRadius = 4f;
        public bool SpawnOnNetworkStart = true;

        private bool spawned;

        private void Awake()
        {
            TryRegisterZombiePrefab();
        }

        public override void OnNetworkSpawn()
        {
            TryRegisterZombiePrefab();
            if (IsServer && SpawnOnNetworkStart)
            {
                SpawnZombies();
            }
        }

        [ContextMenu("Spawn Zombies Now")]
        public void SpawnZombies()
        {
            if (!IsServer || spawned || ZombiePrefab == null)
            {
                return;
            }

            int count = Mathf.Max(0, ZombieCount);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = GetSpawnOffset(i, count);
                GameObject zombieObject = Instantiate(ZombiePrefab, transform.position + offset, transform.rotation);
                NetworkObject networkObject = zombieObject.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.Spawn();
                }
            }

            spawned = true;
        }

        private Vector3 GetSpawnOffset(int index, int count)
        {
            if (count <= 1 || SpawnRadius <= 0.01f)
            {
                return Vector3.zero;
            }

            float angle = (Mathf.PI * 2f * index) / count;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SpawnRadius;
        }

        private void TryRegisterZombiePrefab()
        {
            if (ZombiePrefab == null || NetworkManager.Singleton == null || NetworkManager.Singleton.NetworkConfig == null)
            {
                return;
            }

            var prefabsList = NetworkManager.Singleton.NetworkConfig.Prefabs;
            if (prefabsList != null)
            {
                for (int i = 0; i < prefabsList.Prefabs.Count; i++)
                {
                    if (prefabsList.Prefabs[i] != null && prefabsList.Prefabs[i].Prefab == ZombiePrefab)
                    {
                        return;
                    }
                }
            }

            NetworkManager.Singleton.AddNetworkPrefab(ZombiePrefab);
        }
    }
}
