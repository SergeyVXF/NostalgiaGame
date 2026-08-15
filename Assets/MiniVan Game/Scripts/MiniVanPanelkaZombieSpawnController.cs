using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaZombieSpawnController : MonoBehaviour
    {
        public GameObject ZombiePrefab;
        [Min(0)] public int ZombieCount = 4;
        public int GenerationSeed = 9137;
        public bool SpawnOnServerStart = true;
        public bool AllowOfflineSpawn;
        [Tooltip("If true, only MainRoute apartments are used (not KeySource / TransferArrival).")]
        public bool MainRouteOnly;
        [Min(0f)] public float SpawnJitterRadius = 0.45f;
        [Min(0.2f)] public float GroundProbeHeight = 2.5f;
        [Min(0.2f)] public float GroundProbeDistance = 5f;
        [Min(0.2f)] public float SpawnNavMeshSampleDistance = 2.0f;

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
                    SpawnZombies();
                }

                return;
            }

            if (AllowOfflineSpawn)
            {
                SpawnZombies();
            }
        }

        [ContextMenu("Spawn Panelka Zombies")]
        public void SpawnZombies()
        {
            if (spawned || ZombieCount <= 0 || ZombiePrefab == null)
            {
                return;
            }

            RegisterNetworkPrefabIfNeeded();

            List<Transform> candidates = CollectCandidateRoomMarkers();
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[Panelka Zombies] No reachable apartment room markers found.", this);
                return;
            }

            System.Random random = new System.Random(GenerationSeed ^ 0x5A02B1);
            Shuffle(candidates, random);

            int spawnedCount = 0;
            int attempts = Mathf.Min(candidates.Count, ZombieCount * 4);
            for (int i = 0; i < attempts && spawnedCount < ZombieCount; i++)
            {
                Transform marker = candidates[i];
                if (marker == null || !TryResolveSpawnPoint(marker, random, out Vector3 spawnPosition))
                {
                    continue;
                }

                Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                GameObject zombie = Instantiate(ZombiePrefab, spawnPosition, rotation, transform);
                zombie.name = "Panelka_Zombie_" + (spawnedCount + 1).ToString("00") + "_Floor_" + GetFloorLabel(marker);
                ConfigureZombie(zombie);

                NetworkObject networkObject = zombie.GetComponent<NetworkObject>();
                NetworkManager network = NetworkManager.Singleton;
                if (network != null && network.IsListening && network.IsServer && networkObject != null && !networkObject.IsSpawned)
                {
                    networkObject.Spawn(true);
                }

                spawnedCount++;
            }

            spawned = true;
            Debug.Log("[Panelka Zombies] Spawned " + spawnedCount + " zombies from " + candidates.Count + " reachable room markers.", this);
        }

        private List<Transform> CollectCandidateRoomMarkers()
        {
            List<Transform> candidates = new List<Transform>();
            MiniVanPanelkaApartmentRouteMarker[] apartments = GetComponentsInChildren<MiniVanPanelkaApartmentRouteMarker>(true);
            for (int i = 0; i < apartments.Length; i++)
            {
                MiniVanPanelkaApartmentRouteMarker apartment = apartments[i];
                if (apartment == null || !apartment.PlayerCanEnter)
                {
                    continue;
                }

                if (MainRouteOnly &&
                    apartment.Role != MiniVanPanelkaApartmentRouteRole.MainRoute)
                {
                    continue;
                }

                Transform apartmentTransform = apartment.transform;
                for (int childIndex = 0; childIndex < apartmentTransform.childCount; childIndex++)
                {
                    Transform child = apartmentTransform.GetChild(childIndex);
                    if (child != null && child.name.StartsWith("ROOM_"))
                    {
                        candidates.Add(child);
                    }
                }
            }

            return candidates;
        }

        private bool TryResolveSpawnPoint(Transform marker, System.Random random, out Vector3 spawnPosition)
        {
            Vector2 jitter = RandomPointInCircle(random) * SpawnJitterRadius;
            Vector3 origin = marker.position + new Vector3(jitter.x, GroundProbeHeight, jitter.y);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbeHeight + GroundProbeDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPosition = marker.position;
                return false;
            }

            Vector3 point = hit.point + Vector3.up * 0.05f;
            if (NavMesh.SamplePosition(point, out NavMeshHit navHit, SpawnNavMeshSampleDistance, NavMesh.AllAreas))
            {
                point = navHit.position;
            }

            spawnPosition = point;
            return true;
        }

        private void ConfigureZombie(GameObject zombie)
        {
            MiniVanZombie zombieBrain = zombie.GetComponent<MiniVanZombie>();
            if (zombieBrain == null)
            {
                return;
            }

            zombieBrain.UseNavMeshWhenAvailable = true;
            zombieBrain.DetectReachablePlayersThroughNavigation = false;
            zombieBrain.NavigationDetectionRange = Mathf.Max(zombieBrain.NavigationDetectionRange, 60f);
            zombieBrain.NavMeshSampleDistance = Mathf.Max(zombieBrain.NavMeshSampleDistance, 2.5f);
            zombieBrain.OpenPanelkaDoorsWhenChasing = true;
            zombieBrain.PatrolRadius = Mathf.Min(Mathf.Max(zombieBrain.PatrolRadius, 2.0f), 3.25f);
        }

        private void RegisterNetworkPrefabIfNeeded()
        {
            NetworkManager network = NetworkManager.Singleton;
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
                // Netcode throws when the prefab is already registered. That is fine.
            }
        }

        private static Vector2 RandomPointInCircle(System.Random random)
        {
            float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
            float radius = Mathf.Sqrt((float)random.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                T value = list[i];
                list[i] = list[swapIndex];
                list[swapIndex] = value;
            }
        }

        private static string GetFloorLabel(Transform marker)
        {
            MiniVanPanelkaApartmentRouteMarker apartment = marker.GetComponentInParent<MiniVanPanelkaApartmentRouteMarker>();
            return apartment != null ? apartment.FloorNumber.ToString("00") : "Unknown";
        }
    }
}
