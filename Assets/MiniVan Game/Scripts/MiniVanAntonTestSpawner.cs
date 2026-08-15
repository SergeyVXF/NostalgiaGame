using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Spawns Anton + stretcher + locator near the minivan for MVP testing when the server starts.
    /// </summary>
    public sealed class MiniVanAntonTestSpawner : MonoBehaviour
    {
        public GameObject AntonPrefab;
        public GameObject StretcherPrefab;
        public GameObject AntonLocatorPrefab;
        public Vector3 StretcherExtraOffset = new Vector3(1.4f, 0f, 0f);

        /// <summary>Locator spawn spot relative to the minivan (right side, slightly behind).</summary>
        public Vector3 LocatorVanOffset = new Vector3(2.6f, 0f, -1.2f);

        /// <summary>Cosmetic spawn spot relative to the minivan (left side, slightly behind).</summary>
        public Vector3 CosmeticVanOffset = new Vector3(-2.6f, 0f, -1.2f);

        /// <summary>Cosmetic dropped near the van for testing.</summary>
        public MiniVanInventoryItem CosmeticTestItem = MiniVanInventoryItem.ZoroBandana;

        /// <summary>Gap kept between the van hull and any prop spawned beside it.</summary>
        public float VanSideClearance = 1.1f;
        public float LocatorClearRadius = 0.5f;
        public float VanWaitSeconds = 12f;
        public bool SpawnOnServerStart = true;

        private static MiniVanAntonTestSpawner instance;
        private bool spawned;
        private bool subscribed;

        private void Awake()
        {
            instance = this;
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null && subscribed)
            {
                NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
                subscribed = false;
            }
        }

        private void Update()
        {
            if (!subscribed)
            {
                TrySubscribe();
            }
        }

        private void TrySubscribe()
        {
            if (subscribed || NetworkManager.Singleton == null)
            {
                return;
            }

            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
            subscribed = true;
            if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsListening)
            {
                HandleServerStarted();
            }
        }

        private void HandleServerStarted()
        {
            if (SpawnOnServerStart)
            {
                SpawnTestObjects();
            }
        }

        public static GameObject ResolveStretcherPrefab()
        {
            if (instance != null && instance.StretcherPrefab != null)
            {
                return instance.StretcherPrefab;
            }

            return Resources.Load<GameObject>("MiniVan/Stretcher");
        }

        public static GameObject ResolveAntonPrefab()
        {
            if (instance != null && instance.AntonPrefab != null)
            {
                return instance.AntonPrefab;
            }

            return Resources.Load<GameObject>("MiniVan/Anton");
        }

        public static GameObject ResolveAntonLocatorPrefab()
        {
            if (instance != null && instance.AntonLocatorPrefab != null)
            {
                return instance.AntonLocatorPrefab;
            }

            return Resources.Load<GameObject>("MiniVan/AntonLocator");
        }

        public static GameObject ResolveCosmeticPickupPrefab()
        {
            return Resources.Load<GameObject>(MiniVanCosmeticPickup.PickupResourcePath);
        }

        [ContextMenu("Spawn Test Anton + Stretcher + Locator")]
        public void SpawnTestObjects()
        {
            if (spawned)
            {
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            bool hasAnton = FindFirstObjectByType<MiniVanAnton>() != null;
            bool hasStretcher = FindFirstObjectByType<MiniVanStretcher>() != null;
            bool hasLocator = FindFirstObjectByType<MiniVanAntonLocatorPickup>() != null;
            bool hasCosmetic = FindFirstObjectByType<MiniVanCosmeticPickup>() != null;
            if (hasAnton && hasStretcher && hasLocator && hasCosmetic)
            {
                spawned = true;
                return;
            }

            // Spawn at this object's transform — move the spawner in the scene to relocate the test props.
            Vector3 origin = transform.position;
            Quaternion rot = transform.rotation;

            if (!hasAnton)
            {
                MiniVanAntonRandomPosTest randomArea = FindFirstObjectByType<MiniVanAntonRandomPosTest>();
                if (randomArea != null &&
                    randomArea.TryGetSpawnPosition(out Vector3 antonPos, out Quaternion antonRot))
                {
                    SpawnAt(ResolveAntonPrefab(), antonPos, antonRot);
                }
                else
                {
                    SpawnGrounded(ResolveAntonPrefab(), origin, rot);
                }
            }

            if (!hasStretcher)
            {
                SpawnGrounded(ResolveStretcherPrefab(), origin + rot * StretcherExtraOffset, rot);
            }

            if (!hasLocator)
            {
                StartCoroutine(SpawnLocatorNearVanRoutine());
            }

            if (!hasCosmetic)
            {
                StartCoroutine(SpawnCosmeticNearVanRoutine());
            }

            spawned = true;
        }

        /// <summary>
        /// The minivan can be spawned after the server starts, so wait for it before placing the locator.
        /// </summary>
        private System.Collections.IEnumerator SpawnLocatorNearVanRoutine()
        {
            float deadline = Time.time + VanWaitSeconds;
            while (Time.time < deadline && FindFirstObjectByType<MiniVanVehicle>() == null)
            {
                yield return new WaitForSeconds(0.25f);
            }

            if (FindFirstObjectByType<MiniVanAntonLocatorPickup>() != null)
            {
                yield break;
            }

            GetVanSideSpawnPose(LocatorVanOffset, out Vector3 locatorPos, out Quaternion locatorRot);
            SpawnAt(ResolveAntonLocatorPrefab(), locatorPos, locatorRot);
        }

        private System.Collections.IEnumerator SpawnCosmeticNearVanRoutine()
        {
            float deadline = Time.time + VanWaitSeconds;
            while (Time.time < deadline && FindFirstObjectByType<MiniVanVehicle>() == null)
            {
                yield return new WaitForSeconds(0.25f);
            }

            // Give the van a moment to settle on its wheels before measuring its hull.
            yield return new WaitForSeconds(0.5f);

            if (FindFirstObjectByType<MiniVanCosmeticPickup>() != null)
            {
                yield break;
            }

            GetVanSideSpawnPose(CosmeticVanOffset, out Vector3 hatPos, out Quaternion hatRot);
            MiniVanCosmeticPickup.ServerSpawn(CosmeticTestItem, hatPos, hatRot);
        }

        /// <summary>
        /// Test props belong next to the minivan, not next to Anton: search a free grounded spot
        /// in rings around the van-side offset.
        /// </summary>
        private void GetVanSideSpawnPose(Vector3 vanOffset, out Vector3 position, out Quaternion rotation)
        {
            MiniVanVehicle van = FindFirstObjectByType<MiniVanVehicle>();
            Transform anchor = van != null ? van.transform : transform;
            rotation = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);

            Vector3 offset = vanOffset;
            Bounds hull = default;
            bool hasHull = van != null && TryGetVehicleHull(van, out hull);
            if (hasHull)
            {
                // Push the prop past the real hull width instead of trusting a hand-typed offset.
                float lateral = hull.extents.x + VanSideClearance;
                float side = Mathf.Sign(vanOffset.x == 0f ? 1f : vanOffset.x);
                offset = new Vector3(hull.center.x + side * lateral, 0f, hull.center.z + vanOffset.z);
            }

            Vector3 basePosition = anchor.position + rotation * offset;
            for (int ring = 0; ring < 5; ring++)
            {
                float radius = ring * 0.9f;
                int samples = ring == 0 ? 1 : 8;
                for (int i = 0; i < samples; i++)
                {
                    float angle = 360f / samples * i;
                    Vector3 candidate = basePosition + Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * radius);
                    if (IsInsideVehicleHull(van, hasHull, hull, candidate))
                    {
                        continue;
                    }

                    if (TryGroundFreeSpot(candidate, out position) && !IsInsideVehicleHull(van, hasHull, hull, position))
                    {
                        return;
                    }
                }
            }

            position = basePosition + Vector3.up * 0.15f;
        }

        /// <summary>Vehicle hull in van-local space, built from its non-trigger colliders.</summary>
        private static bool TryGetVehicleHull(MiniVanVehicle van, out Bounds localBounds)
        {
            localBounds = default;
            Collider[] colliders = van.GetComponentsInChildren<Collider>(true);
            bool any = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                Bounds world = collider.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);

                    Vector3 local = van.transform.InverseTransformPoint(point);
                    if (!any)
                    {
                        localBounds = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(local);
                    }
                }
            }

            return any;
        }

        private bool IsInsideVehicleHull(MiniVanVehicle van, bool hasHull, Bounds localHull, Vector3 worldPoint)
        {
            if (van == null || !hasHull)
            {
                return false;
            }

            Vector3 local = van.transform.InverseTransformPoint(worldPoint);
            Bounds padded = localHull;
            padded.Expand(new Vector3(VanSideClearance, 0f, VanSideClearance));
            padded.Expand(new Vector3(0f, 100f, 0f));
            return padded.Contains(local);
        }

        private bool TryGroundFreeSpot(Vector3 candidate, out Vector3 position)
        {
            position = candidate;
            if (!Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            Vector3 grounded = hit.point + Vector3.up * 0.15f;
            if (Physics.CheckSphere(grounded + Vector3.up * 0.2f, LocatorClearRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            position = grounded;
            return true;
        }

        private static void SpawnAt(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[MiniVanAntonTestSpawner] Missing prefab.");
                return;
            }

            GameObject instance = Instantiate(prefab, position, rotation);
            NetworkObject net = instance.GetComponent<NetworkObject>();
            if (net == null)
            {
                Debug.LogWarning("[MiniVanAntonTestSpawner] Prefab missing NetworkObject.");
                return;
            }

            if (!net.IsSpawned)
            {
                net.Spawn(true);
            }
        }

        private static void SpawnGrounded(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[MiniVanAntonTestSpawner] Missing prefab.");
                return;
            }

            Vector3 spawnPos = position;
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPos = hit.point + Vector3.up * 0.15f;
            }

            GameObject instance = Instantiate(prefab, spawnPos, rotation);
            NetworkObject net = instance.GetComponent<NetworkObject>();
            if (net == null)
            {
                Debug.LogWarning("[MiniVanAntonTestSpawner] Prefab missing NetworkObject.");
                return;
            }

            if (net.IsSpawned)
            {
                return;
            }

            net.Spawn(true);
        }
    }
}
