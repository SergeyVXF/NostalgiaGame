using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanZombiePartPhysics : MonoBehaviour
    {
        [Header("Cleanup")]
        [Min(0f)] public float MinimumLifetimeSeconds = 60f;
        [Min(0f)] public float MaximumLifetimeSeconds = 90f;
        [Min(0.1f)] public float SinkDurationSeconds = 4f;
        [Min(0.1f)] public float SinkDistance = 1.4f;

        public bool IsRoadDebris { get; private set; }

        private Rigidbody body;
        private Collider[] partColliders;
        private MiniVanVehicle cabinVehicle;
        private Rigidbody cabinVehicleBody;
        private Vector3 lastVehiclePointVelocity;
        private float nextVehicleRefreshTime;
        private float cleanupTime;
        private float sinkStartTime;
        private Vector3 sinkStartPosition;
        private bool isSinking;

        public void ConfigureRoadDebris()
        {
            CacheComponents();
            IsRoadDebris = true;
            IgnoreAllVehicleCollisions();
        }

        public void ConfigureCabinCargo(Vector3 inheritedVelocity, Vector3 inheritedAngularVelocity)
        {
            CacheComponents();
            IsRoadDebris = false;
            if (body == null)
            {
                return;
            }

            body.maxLinearVelocity = Mathf.Max(body.maxLinearVelocity, 90f);
            body.linearVelocity = inheritedVelocity;
            body.angularVelocity += inheritedAngularVelocity;
            cabinVehicle = FindCabinVehicle(transform.position);
            cabinVehicleBody = cabinVehicle != null ? cabinVehicle.GetComponent<Rigidbody>() : null;
            lastVehiclePointVelocity = cabinVehicleBody != null
                ? cabinVehicleBody.GetPointVelocity(body.worldCenterOfMass)
                : inheritedVelocity;
            body.WakeUp();
        }

        private void Awake()
        {
            CacheComponents();
            float minimum = Mathf.Min(MinimumLifetimeSeconds, MaximumLifetimeSeconds);
            float maximum = Mathf.Max(MinimumLifetimeSeconds, MaximumLifetimeSeconds);
            ScheduleCleanup(Random.Range(minimum, maximum));
        }

        private void Update()
        {
            if (!isSinking)
            {
                if (Time.time >= cleanupTime)
                {
                    BeginSinking();
                }
                return;
            }

            float duration = Mathf.Max(0.1f, SinkDurationSeconds);
            float t = Mathf.Clamp01((Time.time - sinkStartTime) / duration);
            float eased = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(
                sinkStartPosition,
                sinkStartPosition + Vector3.down * Mathf.Max(0.1f, SinkDistance),
                eased);
            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void FixedUpdate()
        {
            if (isSinking)
            {
                return;
            }

            if (!IsRoadDebris)
            {
                UpdateCabinCargo();
                return;
            }

            if (Time.time < nextVehicleRefreshTime)
            {
                return;
            }

            nextVehicleRefreshTime = Time.time + 0.75f;
            IgnoreAllVehicleCollisions();
        }

        public void ScheduleCleanup(float lifetimeSeconds, float sinkDurationSeconds = -1f)
        {
            cleanupTime = Time.time + Mathf.Max(0f, lifetimeSeconds);
            if (sinkDurationSeconds > 0f)
            {
                SinkDurationSeconds = sinkDurationSeconds;
            }
        }

        private void BeginSinking()
        {
            isSinking = true;
            sinkStartTime = Time.time;
            sinkStartPosition = transform.position;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            if (partColliders == null)
            {
                return;
            }

            for (int i = 0; i < partColliders.Length; i++)
            {
                if (partColliders[i] != null)
                {
                    partColliders[i].enabled = false;
                }
            }
        }

        private void UpdateCabinCargo()
        {
            if (body == null)
            {
                return;
            }

            if (cabinVehicle == null)
            {
                cabinVehicle = FindCabinVehicle(body.position);
                cabinVehicleBody = cabinVehicle != null ? cabinVehicle.GetComponent<Rigidbody>() : null;
                if (cabinVehicleBody != null)
                {
                    lastVehiclePointVelocity = cabinVehicleBody.GetPointVelocity(body.worldCenterOfMass);
                }
            }

            if (cabinVehicle == null || cabinVehicleBody == null)
            {
                return;
            }

            Vector3 vehiclePointVelocity = cabinVehicleBody.GetPointVelocity(body.worldCenterOfMass);
            body.linearVelocity += vehiclePointVelocity - lastVehiclePointVelocity;
            lastVehiclePointVelocity = vehiclePointVelocity;
            KeepInsideCabin(vehiclePointVelocity);
        }

        private void KeepInsideCabin(Vector3 vehiclePointVelocity)
        {
            Vector3 local = cabinVehicle.transform.InverseTransformPoint(body.position);
            Vector3 clamped = new Vector3(
                Mathf.Clamp(local.x, -2.30f, 2.08f),
                Mathf.Clamp(local.y, 1.52f, 2.38f),
                Mathf.Clamp(local.z, -5.02f, 4.08f));
            if ((clamped - local).sqrMagnitude <= 0.0001f)
            {
                return;
            }

            body.position = cabinVehicle.transform.TransformPoint(clamped);
            Vector3 relativeLocalVelocity = cabinVehicle.transform.InverseTransformDirection(
                body.linearVelocity - vehiclePointVelocity);
            if ((local.x < -2.30f && relativeLocalVelocity.x < 0f) ||
                (local.x > 2.08f && relativeLocalVelocity.x > 0f))
            {
                relativeLocalVelocity.x = 0f;
            }

            if ((local.y < 1.52f && relativeLocalVelocity.y < 0f) ||
                (local.y > 2.38f && relativeLocalVelocity.y > 0f))
            {
                relativeLocalVelocity.y = 0f;
            }

            if ((local.z < -5.02f && relativeLocalVelocity.z < 0f) ||
                (local.z > 4.08f && relativeLocalVelocity.z > 0f))
            {
                relativeLocalVelocity.z = 0f;
            }

            Vector3 relativeVelocity = cabinVehicle.transform.TransformDirection(relativeLocalVelocity);
            body.linearVelocity = vehiclePointVelocity + Vector3.ClampMagnitude(relativeVelocity, 12f);
        }

        private void CacheComponents()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (partColliders == null || partColliders.Length == 0)
            {
                partColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void IgnoreAllVehicleCollisions()
        {
            CacheComponents();
            if (partColliders == null || partColliders.Length == 0)
            {
                return;
            }

            MiniVanVehicle[] vehicles = MiniVanSceneScan.Get<MiniVanVehicle>();
            for (int v = 0; v < vehicles.Length; v++)
            {
                MiniVanVehicle vehicle = vehicles[v];
                if (vehicle == null)
                {
                    continue;
                }

                Collider[] vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
                for (int p = 0; p < partColliders.Length; p++)
                {
                    Collider partCollider = partColliders[p];
                    if (partCollider == null)
                    {
                        continue;
                    }

                    for (int c = 0; c < vehicleColliders.Length; c++)
                    {
                        Collider vehicleCollider = vehicleColliders[c];
                        if (vehicleCollider != null && !vehicleCollider.isTrigger)
                        {
                            Physics.IgnoreCollision(partCollider, vehicleCollider, true);
                        }
                    }
                }
            }
        }

        private static MiniVanVehicle FindCabinVehicle(Vector3 worldPosition)
        {
            MiniVanVehicle[] vehicles = MiniVanSceneScan.Get<MiniVanVehicle>();
            MiniVanVehicle best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle == null)
                {
                    continue;
                }

                Vector3 local = vehicle.transform.InverseTransformPoint(worldPosition);
                bool inside = local.x >= -3.15f && local.x <= 2.85f &&
                              local.y >= 0.9f && local.y <= 4.25f &&
                              local.z >= -6.1f && local.z <= 5.15f;
                float distance = (vehicle.transform.position - worldPosition).sqrMagnitude;
                if (inside && distance < bestDistance)
                {
                    best = vehicle;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
