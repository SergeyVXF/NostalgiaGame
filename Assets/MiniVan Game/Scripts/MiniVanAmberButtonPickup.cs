using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// World collectible amber button: spins/bobs, picks up only by stepping on the trigger.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanAmberButtonPickup : NetworkBehaviour
    {
        public const string ResourcesPickupPath = "Collectibles/AmberButtonPickup";
        public const string VisualChildName = "AmberVisual";

        public float PickupRadius = 2.4f;
        public float VisualHeight = 0.18f;
        public float SpinDegreesPerSecond = 70f;
        public float BobAmplitude = 0.045f;
        public float BobSpeed = 2.4f;
        [Min(0.05f)] public float PopDuration = 0.45f;
        [Min(0f)] public float PopHopHeight = 0.35f;

        private readonly NetworkVariable<bool> available = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> scatterOrigin = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Transform visual;
        private Vector3 visualBaseLocal;
        private float bobPhase;
        private Vector3 pendingScatterOrigin;
        private Vector3 popLandPosition;
        private Vector3 popStartPosition;
        private float popT = -1f;
        private bool popStarted;

        public bool IsAvailable => !IsSpawned || available.Value;

        public static MiniVanAmberButtonPickup ServerSpawn(
            Vector3 position,
            Quaternion rotation,
            Vector3 scatterFrom = default)
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsServer)
            {
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(ResourcesPickupPath);
            if (prefab == null)
            {
                Debug.LogWarning("[MiniVan] Amber button prefab missing at Resources/" + ResourcesPickupPath);
                return null;
            }

            GameObject instance = Instantiate(prefab, position, rotation);
            MiniVanAmberButtonPickup pickup = instance.GetComponent<MiniVanAmberButtonPickup>();
            if (pickup != null)
            {
                pickup.pendingScatterOrigin = scatterFrom;
            }

            NetworkObject net = instance.GetComponent<NetworkObject>();
            if (net == null)
            {
                Destroy(instance);
                return null;
            }

            net.Spawn(true);
            return pickup;
        }

        /// <summary>
        /// Spawns buttons on a ring around the crate (not a pile), biased away from the hit,
        /// with a short pop arc from the crate center.
        /// </summary>
        public static int ServerSpawnBurst(
            Vector3 center,
            int minCount,
            int maxCount,
            float scatterRadius = 3.15f,
            Vector3 impactVelocity = default)
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsServer)
            {
                return 0;
            }

            int count = Random.Range(Mathf.Max(1, minCount), Mathf.Max(minCount, maxCount) + 1);
            float radius = Mathf.Max(0.35f, scatterRadius);
            float baseAngle = Random.Range(0f, 360f);
            Vector3 away = impactVelocity;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Random.onUnitSphere;
                away.y = 0f;
            }

            away.Normalize();

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                float slice = 360f / count;
                float angle = baseAngle + slice * i + Random.Range(-slice * 0.28f, slice * 0.28f);
                float dist = Random.Range(radius * 0.55f, radius * 1.15f);

                Vector3 ring = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
                ring += away * Random.Range(radius * 0.15f, radius * 0.45f);

                // Keep a little separation so 4–5 buttons never sit on top of each other.
                float yawJitter = (i - (count - 1) * 0.5f) * 0.08f;
                ring += Vector3.Cross(Vector3.up, away).normalized * yawJitter;

                Vector3 land = center + ring;
                land.y = center.y + Random.Range(0.10f, 0.22f);

                Quaternion rot = Quaternion.Euler(
                    Random.Range(-18f, 18f),
                    Random.Range(0f, 360f),
                    Random.Range(-18f, 18f));

                if (ServerSpawn(land, rot, center + Vector3.up * 0.12f) != null)
                {
                    spawned++;
                }
            }

            return spawned;
        }

        private void Awake()
        {
            ConfigureCollider();
            CacheVisual();
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!IsAvailable)
            {
                return;
            }

            if (popT >= 0f && popT < 1f)
            {
                popT = Mathf.Clamp01(popT + Time.deltaTime / Mathf.Max(0.05f, PopDuration));
                float t = popT * popT * (3f - 2f * popT);
                Vector3 pos = Vector3.Lerp(popStartPosition, popLandPosition, t);
                pos.y += Mathf.Sin(popT * Mathf.PI) * PopHopHeight;
                transform.position = pos;

                if (visual != null)
                {
                    float spinBoost = 220f;
                    visual.localRotation = Quaternion.Euler(
                        18f,
                        bobPhase * (SpinDegreesPerSecond + spinBoost),
                        12f);
                    visual.localPosition = visualBaseLocal;
                }

                bobPhase += Time.deltaTime;
                if (popT >= 1f)
                {
                    transform.position = popLandPosition;
                    SetPickupColliderEnabled(true);
                }

                return;
            }

            if (visual == null)
            {
                return;
            }

            bobPhase += Time.deltaTime;
            visual.localRotation = Quaternion.Euler(12f, bobPhase * SpinDegreesPerSecond, 8f);
            visual.localPosition = visualBaseLocal + new Vector3(
                0f,
                Mathf.Sin(bobPhase * BobSpeed) * BobAmplitude,
                0f);
        }

        public override void OnNetworkSpawn()
        {
            ConfigureCollider();
            CacheVisual();
            available.OnValueChanged += HandleAvailableChanged;
            scatterOrigin.OnValueChanged += HandleScatterOriginChanged;
            ApplyAvailable(available.Value);

            if (IsServer && pendingScatterOrigin.sqrMagnitude > 0.0001f)
            {
                scatterOrigin.Value = pendingScatterOrigin;
            }

            TryBeginPop(scatterOrigin.Value);
        }

        public override void OnNetworkDespawn()
        {
            available.OnValueChanged -= HandleAvailableChanged;
            scatterOrigin.OnValueChanged -= HandleScatterOriginChanged;
        }

        public bool IsInReach(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= PickupRadius;
        }

        public bool TryClaim()
        {
            if (!IsServer || !IsSpawned || !available.Value || IsPopping)
            {
                return false;
            }

            available.Value = false;
            ApplyAvailable(false);
            return true;
        }

        private bool IsPopping => popT >= 0f && popT < 1f;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsAvailable || IsPopping || other == null)
            {
                return;
            }

            MiniVanPlayer player = other.GetComponentInParent<MiniVanPlayer>();
            if (player == null || !player.IsOwner || player.IsDowned)
            {
                return;
            }

            player.TryPickupAmberButton(this);
        }

        private void HandleAvailableChanged(bool previous, bool current)
        {
            ApplyAvailable(current);
        }

        private void HandleScatterOriginChanged(Vector3 previous, Vector3 current)
        {
            TryBeginPop(current);
        }

        private void TryBeginPop(Vector3 origin)
        {
            if (popStarted || origin.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // Already at the origin — nothing to animate.
            if ((transform.position - origin).sqrMagnitude < 0.01f)
            {
                return;
            }

            popStarted = true;
            popLandPosition = transform.position;
            popStartPosition = origin;
            transform.position = origin;
            popT = 0f;
            SetPickupColliderEnabled(false);
        }

        private void ApplyAvailable(bool isAvailable)
        {
            if (visual != null && visual.gameObject.activeSelf != isAvailable)
            {
                visual.gameObject.SetActive(isAvailable);
            }

            SetPickupColliderEnabled(isAvailable && !IsPopping);
        }

        private void SetPickupColliderEnabled(bool enabled)
        {
            Collider box = GetComponent<Collider>();
            if (box != null)
            {
                box.enabled = enabled;
            }
        }

        private void CacheVisual()
        {
            if (visual != null)
            {
                return;
            }

            Transform found = transform.Find(VisualChildName);
            if (found == null && transform.childCount > 0)
            {
                found = transform.GetChild(0);
            }

            visual = found;
            if (visual != null)
            {
                visualBaseLocal = new Vector3(0f, VisualHeight, 0f);
                if (visual.localPosition.sqrMagnitude < 0.0001f)
                {
                    visual.localPosition = visualBaseLocal;
                }
                else
                {
                    visualBaseLocal = visual.localPosition;
                }
            }
        }

        private void ConfigureCollider()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }

            box.center = new Vector3(0f, 0.22f, 0f);
            box.size = new Vector3(0.55f, 0.55f, 0.55f);
            box.isTrigger = true;
        }
    }
}
