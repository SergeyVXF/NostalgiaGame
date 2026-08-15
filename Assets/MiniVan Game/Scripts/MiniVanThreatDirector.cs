using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanThreatDirector : MonoBehaviour
    {
        public GameObject ZombiePrefab;
        public Vector3 StartSafeCenter;
        [Min(10f)] public float StartSafeRadius = 48f;
        [Min(1)] public int MinimumStartingAlive = 3;
        [Min(1)] public int MaximumStartingAlive = 4;
        [Min(1)] public int InitialAliveBudget = 10;
        [Min(0.1f)] public float AliveBudgetGrowthPerMinute = 6f;
        [Min(1)] public int MaximumAliveBudget = 55;
        [Min(0.1f)] public float CatchUpSpawnInterval = 0.35f;
        [Min(0.25f)] public float InitialSpawnInterval = 3.5f;
        [Min(0.25f)] public float MinimumSpawnInterval = 1.5f;
        [Min(0f)] public float SpawnRateAccelerationPerMinute = 0.25f;
        [Min(0.5f)] public float MaintenanceCheckInterval = 1f;
        [Min(5f)] public float MinimumPlayerDistance = 13f;
        [Min(10f)] public float MaximumPlayerDistance = 38f;
        [Min(0.1f)] public float EmergenceDepth = 1.7f;
        [Min(0.1f)] public float MinimumEmergenceSeconds = 1f;
        [Min(0.1f)] public float MaximumEmergenceSeconds = 2f;
        [Range(4f, 30f)] public float ZombieAiUpdateRate = 12f;
        [Range(2f, 20f)] public float ZombieNetworkTransformRate = 8f;
        [Min(0.05f)] public float ZombieTargetScanInterval = 0.35f;
        [Min(0.05f)] public float ZombieNavPathRefreshInterval = 0.4f;
        public bool AllowOfflineSimulation;

        private bool activated;
        private float activationTime;
        private float nextSpawnTime;
        private int startingAliveBudget;
        private MiniVanGameModeSpawnPoint[] spawnPoints;
        private MiniVanGameModeInteriorZone[] interiorZones;
        private MiniVanPlayer[] cachedPlayers;
        private float nextPlayerCacheTime;
        private float nextActivationCheckTime;
        private float nextAliveCountTime;
        private int cachedAliveCount;

        public bool IsActivated => activated;
        public float ThreatTime => activated ? Mathf.Max(0f, Time.time - activationTime) : 0f;

        private void Update()
        {
            if (!Application.isPlaying || !CanRunAuthority())
            {
                return;
            }

            if (!activated && Time.time < nextActivationCheckTime)
            {
                return;
            }

            if (activated && Time.time < nextSpawnTime)
            {
                return;
            }

            MiniVanPlayer[] players = GetPlayersCached();
            if (!activated)
            {
                nextActivationCheckTime = Time.time + 0.25f;
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && Vector3.Distance(players[i].transform.position, StartSafeCenter) > StartSafeRadius)
                    {
                        activated = true;
                        activationTime = Time.time;
                        startingAliveBudget = Random.Range(
                            Mathf.Min(MinimumStartingAlive, MaximumStartingAlive),
                            Mathf.Max(MinimumStartingAlive, MaximumStartingAlive) + 1);
                        nextSpawnTime = Time.time;
                        break;
                    }
                }
                return;
            }

            if (players.Length == 0 || Time.time < nextSpawnTime)
            {
                return;
            }

            float elapsed = Time.time - activationTime;
            int budget = Mathf.Min(MaximumAliveBudget,
                Mathf.FloorToInt(Mathf.Max(startingAliveBudget, InitialAliveBudget) +
                                 elapsed * AliveBudgetGrowthPerMinute / 60f));
            int alive = GetAliveCountCached();
            if (alive >= budget)
            {
                nextSpawnTime = Time.time + MaintenanceCheckInterval;
                return;
            }

            float elapsedMinutes = elapsed / 60f;
            float steadySpawnInterval = InitialSpawnInterval /
                (1f + elapsedMinutes * SpawnRateAccelerationPerMinute);
            steadySpawnInterval = Mathf.Max(MinimumSpawnInterval, steadySpawnInterval);
            nextSpawnTime = Time.time +
                (alive < startingAliveBudget ? CatchUpSpawnInterval : steadySpawnInterval);
            if (TryPickSpawnPose(players, out Vector3 position, out Quaternion rotation))
            {
                SpawnZombie(position, rotation);
                cachedAliveCount++;
            }
        }

        private MiniVanPlayer[] GetPlayersCached()
        {
            if (cachedPlayers == null || Time.time >= nextPlayerCacheTime)
            {
                cachedPlayers = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
                nextPlayerCacheTime = Time.time + 1f;
            }
            return cachedPlayers;
        }

        private int GetAliveCountCached()
        {
            if (Time.time >= nextAliveCountTime)
            {
                cachedAliveCount = FindObjectsByType<MiniVanZombie>(FindObjectsSortMode.None).Length;
                nextAliveCountTime = Time.time + 2f;
            }
            return cachedAliveCount;
        }

        private bool CanRunAuthority()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network != null && network.IsListening)
            {
                return network.IsServer;
            }
            return AllowOfflineSimulation;
        }

        private bool TryPickSpawnPose(MiniVanPlayer[] players, out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                spawnPoints = FindObjectsByType<MiniVanGameModeSpawnPoint>(FindObjectsSortMode.None);
            }

            List<MiniVanGameModeSpawnPoint> valid = new List<MiniVanGameModeSpawnPoint>();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                MiniVanGameModeSpawnPoint point = spawnPoints[i];
                if (point == null || !point.ExteriorOnly || IsInsideAnyInterior(point.transform.position))
                {
                    continue;
                }

                float nearest = float.MaxValue;
                for (int p = 0; p < players.Length; p++)
                {
                    if (players[p] != null)
                    {
                        nearest = Mathf.Min(nearest,
                            PlanarDistance(point.transform.position, players[p].transform.position));
                    }
                }
                if (nearest >= MinimumPlayerDistance && nearest <= MaximumPlayerDistance)
                {
                    valid.Add(point);
                }
            }

            if (valid.Count > 0)
            {
                MiniVanGameModeSpawnPoint point = valid[Random.Range(0, valid.Count)];
                position = point.transform.position;
                rotation = point.transform.rotation;
                return true;
            }

            return TryPickDynamicSpawnPose(players, out position, out rotation);
        }

        private bool TryPickDynamicSpawnPose(MiniVanPlayer[] players, out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (players == null || players.Length == 0)
            {
                return false;
            }

            for (int attempt = 0; attempt < 16; attempt++)
            {
                MiniVanPlayer player = players[Random.Range(0, players.Length)];
                if (player == null)
                {
                    continue;
                }

                Vector2 direction2D = Random.insideUnitCircle.normalized;
                float distance = Random.Range(MinimumPlayerDistance, MaximumPlayerDistance);
                Vector3 candidate = player.transform.position +
                    new Vector3(direction2D.x, 0f, direction2D.y) * distance;
                Vector3 rayOrigin = candidate + Vector3.up * 120f;
                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 260f, ~0,
                    QueryTriggerInteraction.Ignore) || hit.normal.y < 0.45f)
                {
                    continue;
                }

                candidate = hit.point + Vector3.up * 0.12f;
                if (Vector3.Distance(candidate, StartSafeCenter) <= StartSafeRadius ||
                    IsInsideAnyInterior(candidate))
                {
                    continue;
                }

                if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 7f, NavMesh.AllAreas))
                {
                    candidate = navHit.position;
                }

                position = candidate;
                Vector3 look = player.transform.position - candidate;
                look.y = 0f;
                rotation = look.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(look.normalized, Vector3.up)
                    : Quaternion.identity;
                return true;
            }

            return false;
        }

        private bool IsInsideAnyInterior(Vector3 position)
        {
            if (interiorZones == null || interiorZones.Length == 0)
            {
                interiorZones = FindObjectsByType<MiniVanGameModeInteriorZone>(FindObjectsSortMode.None);
            }
            for (int i = 0; i < interiorZones.Length; i++)
            {
                if (interiorZones[i] != null && interiorZones[i].Contains(position))
                {
                    return true;
                }
            }
            return false;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void SpawnZombie(Vector3 position, Quaternion rotation)
        {
            if (ZombiePrefab == null)
            {
                return;
            }

            NetworkManager network = NetworkManager.Singleton;
            bool networked = network != null && network.IsListening;
            if (networked)
            {
                try { network.AddNetworkPrefab(ZombiePrefab); }
                catch { }
            }

            GameObject zombie = Instantiate(ZombiePrefab, position, rotation);
            zombie.name = "Threat_Zombie";
            MiniVanZombie zombieController = zombie.GetComponent<MiniVanZombie>();
            if (zombieController != null)
            {
                zombieController.AiUpdateRate = ZombieAiUpdateRate;
                zombieController.NetworkTransformRate = ZombieNetworkTransformRate;
                zombieController.TargetScanInterval = ZombieTargetScanInterval;
                zombieController.NavPathRefreshInterval = ZombieNavPathRefreshInterval;
                float emergenceSeconds = Random.Range(
                    Mathf.Min(MinimumEmergenceSeconds, MaximumEmergenceSeconds),
                    Mathf.Max(MinimumEmergenceSeconds, MaximumEmergenceSeconds));
                zombieController.BeginEmergence(position, emergenceSeconds, EmergenceDepth);
            }
            NetworkObject networkObject = zombie.GetComponent<NetworkObject>();
            if (networked && networkObject != null && !networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }
        }
    }
}
