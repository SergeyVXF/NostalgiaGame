using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// SNES props: carried/placed stay kinematic; Q-drop uses real gravity.
    /// Vehicle collisions are ignored so loose props never shove the minivan;
    /// cabin support is applied via raycasts so items still rest on the floor inside.
    /// </summary>
    public static class MiniVanSnesPhysics
    {
        private const float CabinSupportRayUp = 0.35f;
        private const float CabinSupportRayDown = 2.4f;
        private const float CabinRestOffset = 0.02f;
        private const float CabinSettleSpeed = 0.45f;

        public static Rigidbody EnsureKinematicBody(GameObject root)
        {
            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = root.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.detectCollisions = true;
            return body;
        }

        public static Collider[] EnsureTriggerColliders(GameObject root, Vector3 fallbackSize, Vector3 fallbackCenter)
        {
            BoxCollider rootBox = root.GetComponent<BoxCollider>();
            if (rootBox == null)
            {
                rootBox = root.AddComponent<BoxCollider>();
                Bounds bounds = CalculateRendererBounds(root);
                if (bounds.size.sqrMagnitude > 0.0001f)
                {
                    rootBox.center = root.transform.InverseTransformPoint(bounds.center);
                    Vector3 lossy = root.transform.lossyScale;
                    rootBox.size = new Vector3(
                        SafeDiv(bounds.size.x, Mathf.Abs(lossy.x)),
                        SafeDiv(bounds.size.y, Mathf.Abs(lossy.y)),
                        SafeDiv(bounds.size.z, Mathf.Abs(lossy.z)));
                }
                else
                {
                    rootBox.center = fallbackCenter;
                    rootBox.size = fallbackSize;
                }
            }

            Collider[] existing = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    existing[i].isTrigger = true;
                }
            }

            return existing;
        }

        public static void ApplyCarryState(Rigidbody body, Collider[] colliders, bool carried)
        {
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = !carried;
                if (!carried)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            SetColliders(colliders, enabled: !carried, isTrigger: true);
        }

        /// <summary>E-place: kinematic, parented if near van, no push.</summary>
        public static void ApplyPlacedState(Rigidbody body, Collider[] colliders, Transform target, Vector3 worldPosition)
        {
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            SetColliders(colliders, enabled: true, isTrigger: true);
            TryParentToVehicleAt(target, worldPosition);
            IgnoreVehicleCollisions(colliders);
        }

        /// <summary>Q-drop: dynamic gravity fall; never applies forces to the minivan.</summary>
        public static void ApplyDroppedState(Rigidbody body, Collider[] colliders, Transform target, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (target != null)
            {
                target.SetParent(null, true);
                target.SetPositionAndRotation(worldPosition, worldRotation);
            }

            SetColliders(colliders, enabled: true, isTrigger: false);
            IgnoreVehicleCollisions(colliders);

            if (body == null)
            {
                return;
            }

            body.mass = Mathf.Max(0.35f, body.mass > 0.01f ? body.mass : 1.2f);
            body.linearDamping = 0.4f;
            body.angularDamping = 0.85f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.detectCollisions = true;
            body.isKinematic = false;
            body.useGravity = true;
            body.position = worldPosition;
            body.rotation = worldRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        /// <summary>
        /// While free-falling with vehicle collisions ignored, pin props to cabin floor
        /// via raycasts and settle to kinematic+parented when resting.
        /// </summary>
        public static void TickLooseBody(Rigidbody body, Collider[] colliders, Transform target)
        {
            if (body == null || target == null || body.isKinematic || !body.useGravity)
            {
                return;
            }

            IgnoreVehicleCollisions(colliders);

            Vector3 origin = body.worldCenterOfMass + Vector3.up * CabinSupportRayUp;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, CabinSupportRayDown, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            MiniVanVehicle vehicle = hit.collider.GetComponentInParent<MiniVanVehicle>();
            if (vehicle == null)
            {
                return;
            }

            float restY = hit.point.y + CabinRestOffset;
            Vector3 pos = body.position;
            if (pos.y < restY)
            {
                pos.y = restY;
                body.position = pos;
                target.position = pos;
            }

            Vector3 vel = body.linearVelocity;
            if (vel.y < 0f)
            {
                vel.y = 0f;
                body.linearVelocity = vel;
            }

            if (vel.magnitude <= CabinSettleSpeed && Mathf.Abs(body.angularVelocity.magnitude) <= CabinSettleSpeed)
            {
                target.SetParent(vehicle.transform, true);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                SetColliders(colliders, enabled: true, isTrigger: true);
                IgnoreVehicleCollisions(colliders);
            }
        }

        public static bool TryParentToVehicleAt(Transform target, Vector3 worldPosition)
        {
            if (target == null)
            {
                return false;
            }

            MiniVanVehicle[] vehicles = Object.FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
            MiniVanVehicle best = null;
            float bestDist = 2.8f;
            for (int i = 0; i < vehicles.Length; i++)
            {
                MiniVanVehicle vehicle = vehicles[i];
                if (vehicle == null)
                {
                    continue;
                }

                Collider[] vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < vehicleColliders.Length; c++)
                {
                    Collider col = vehicleColliders[c];
                    if (col == null || col.isTrigger || col is WheelCollider)
                    {
                        continue;
                    }

                    Vector3 closest = col.ClosestPoint(worldPosition);
                    float d = Vector3.Distance(worldPosition, closest);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = vehicle;
                    }
                }
            }

            if (best != null)
            {
                target.SetParent(best.transform, true);
                return true;
            }

            target.SetParent(null, true);
            return false;
        }

        public static void IgnoreVehicleCollisions(Collider[] colliders)
        {
            if (colliders == null || colliders.Length == 0)
            {
                return;
            }

            MiniVanVehicle[] vehicles = Object.FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
            for (int v = 0; v < vehicles.Length; v++)
            {
                MiniVanVehicle vehicle = vehicles[v];
                if (vehicle == null)
                {
                    continue;
                }

                Collider[] vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider ours = colliders[i];
                    if (ours == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < vehicleColliders.Length; j++)
                    {
                        Collider vehicleCollider = vehicleColliders[j];
                        if (vehicleCollider != null)
                        {
                            Physics.IgnoreCollision(ours, vehicleCollider, true);
                        }
                    }
                }
            }
        }

        private static void SetColliders(Collider[] colliders, bool enabled, bool isTrigger)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null)
                {
                    continue;
                }

                col.enabled = enabled;
                col.isTrigger = isTrigger;
            }
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            bool has = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                if (!has)
                {
                    bounds = renderers[i].bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return has ? bounds : new Bounds(root.transform.position, Vector3.zero);
        }

        private static float SafeDiv(float value, float divisor)
        {
            return divisor > 0.0001f ? value / divisor : value;
        }
    }
}
