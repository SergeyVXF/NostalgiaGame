using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Acid glob projectile. Uses a kinematic trigger so it can hit a CharacterController.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class MiniVanAcidSpit : MonoBehaviour
    {
        public Vector3 Velocity;
        public bool DealsDamage;
        public int PlayerDamage = 1;
        public float VehicleDamage = 4f;
        public float Lifetime = 2.4f;
        [Min(0.05f)] public float Radius = 0.38f;
        public MiniVanAcidZombie Owner;
        public GameObject SplashPrefab;
        [Min(0f)] public float WorldHitGraceSeconds = 0.12f;

        private Rigidbody body;
        private SphereCollider trigger;
        private float dieAt;
        private float spawnedAt;
        private bool consumed;
        private static MiniVanPlayer[] cachedPlayers;
        private static float nextPlayerCacheTime;

        public static MiniVanAcidSpit Spawn(
            GameObject prefab,
            Vector3 origin,
            Vector3 velocity,
            bool dealsDamage,
            MiniVanAcidZombie owner,
            GameObject splashOverride)
        {
            MiniVanAcidSpit spit;
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, origin, Quaternion.identity);
                instance.name = "AcidSpit";
                spit = instance.GetComponent<MiniVanAcidSpit>();
                if (spit == null)
                {
                    spit = instance.AddComponent<MiniVanAcidSpit>();
                }
            }
            else
            {
                spit = CreateFallback(origin);
            }

            spit.Initialize(velocity, dealsDamage, owner, splashOverride);
            return spit;
        }

        public void Initialize(
            Vector3 velocity,
            bool dealsDamage,
            MiniVanAcidZombie owner,
            GameObject splashOverride)
        {
            Velocity = velocity;
            DealsDamage = dealsDamage;
            Owner = owner;
            if (splashOverride != null)
            {
                SplashPrefab = splashOverride;
            }

            spawnedAt = Time.time;
            dieAt = Time.time + Lifetime;
            consumed = false;
            EnsurePhysics();
            IgnoreOwnerCollision();
        }

        private void Awake()
        {
            EnsurePhysics();
        }

        private void FixedUpdate()
        {
            if (consumed)
            {
                return;
            }

            if (Time.time >= dieAt)
            {
                Consume(transform.position, false);
                return;
            }

            EnsurePhysics();
            Velocity += Physics.gravity * 0.42f * Time.fixedDeltaTime;
            body.MovePosition(body.position + Velocity * Time.fixedDeltaTime);
            TryHitNearbyPlayers();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHitCollider(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHitCollider(other);
        }

        private void TryHitCollider(Collider other)
        {
            if (consumed || other == null || IsOwnerCollider(other))
            {
                return;
            }

            MiniVanPlayer player = other.GetComponentInParent<MiniVanPlayer>();
            if (player != null && !player.IsZombieDead)
            {
                HitPlayer(player, other.ClosestPoint(transform.position));
                return;
            }

            MiniVanVehicle vehicle = other.GetComponentInParent<MiniVanVehicle>();
            if (vehicle != null)
            {
                HitVehicle(vehicle, other.ClosestPoint(transform.position));
                return;
            }

            if (other.isTrigger || Time.time < spawnedAt + WorldHitGraceSeconds)
            {
                return;
            }

            Consume(other.ClosestPoint(transform.position), true);
        }

        private void TryHitNearbyPlayers()
        {
            MiniVanPlayer[] players = CollectPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player == null || player.IsZombieDead)
                {
                    continue;
                }

                MiniVanVehicle vehicle = player.CurrentVehicle;
                if (vehicle != null)
                {
                    Vector3 closest = vehicle.GetClosestVehicleBodyPoint(transform.position);
                    if ((closest - transform.position).sqrMagnitude <= Radius * Radius)
                    {
                        HitVehicle(vehicle, closest);
                        return;
                    }

                    continue;
                }

                Vector3 point = GetPlayerClosestPoint(player, transform.position);
                if ((point - transform.position).sqrMagnitude <= Radius * Radius)
                {
                    HitPlayer(player, point);
                    return;
                }
            }
        }

        private void HitPlayer(MiniVanPlayer player, Vector3 point)
        {
            if (consumed || player == null)
            {
                return;
            }

            if (DealsDamage)
            {
                player.ReceiveZombieDamageServer(Mathf.Max(1, PlayerDamage));
            }

            Consume(point, true);
        }

        private void HitVehicle(MiniVanVehicle vehicle, Vector3 point)
        {
            if (consumed || vehicle == null)
            {
                return;
            }

            if (DealsDamage)
            {
                vehicle.ApplyVehicleDamage(Mathf.Max(0.1f, VehicleDamage), "acid spit");
            }

            Consume(point, true);
        }

        private bool IsOwnerCollider(Collider collider)
        {
            if (Owner == null || collider == null)
            {
                return false;
            }

            Transform t = collider.transform;
            return t == Owner.transform || t.IsChildOf(Owner.transform);
        }

        private void Consume(Vector3 point, bool splash)
        {
            if (consumed)
            {
                return;
            }

            consumed = true;
            if (splash)
            {
                MiniVanAcidSplash.Spawn(SplashPrefab, point, Velocity);
            }

            Destroy(gameObject);
        }

        private void EnsurePhysics()
        {
            if (trigger == null)
            {
                trigger = GetComponent<SphereCollider>();
                if (trigger == null)
                {
                    trigger = gameObject.AddComponent<SphereCollider>();
                }
            }

            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.05f, Radius);
            trigger.center = Vector3.zero;

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = gameObject.AddComponent<Rigidbody>();
                }
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.detectCollisions = true;
        }

        private void IgnoreOwnerCollision()
        {
            if (Owner == null || trigger == null)
            {
                return;
            }

            CharacterController ownerController = Owner.GetComponent<CharacterController>();
            if (ownerController != null)
            {
                Physics.IgnoreCollision(trigger, ownerController, true);
            }

            Collider[] ownerColliders = Owner.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] != null)
                {
                    Physics.IgnoreCollision(trigger, ownerColliders[i], true);
                }
            }
        }

        private static Vector3 GetPlayerClosestPoint(MiniVanPlayer player, Vector3 from)
        {
            CharacterController controller = player.CharacterController != null
                ? player.CharacterController
                : player.GetComponent<CharacterController>();
            if (controller == null || !controller.enabled)
            {
                return player.transform.position + Vector3.up * 1.0f;
            }

            Transform t = controller.transform;
            Vector3 worldCenter = t.TransformPoint(controller.center);
            float radius = controller.radius + controller.skinWidth;
            float half = Mathf.Max(0f, controller.height * 0.5f - radius);
            Vector3 axis = t.up;
            Vector3 p1 = worldCenter + axis * half;
            Vector3 p2 = worldCenter - axis * half;
            Vector3 segment = p2 - p1;
            float lengthSq = segment.sqrMagnitude;
            Vector3 onSegment = p1;
            if (lengthSq > 0.0001f)
            {
                float u = Mathf.Clamp01(Vector3.Dot(from - p1, segment) / lengthSq);
                onSegment = p1 + segment * u;
            }

            Vector3 to = from - onSegment;
            float distance = to.magnitude;
            if (distance <= radius)
            {
                return from;
            }

            return onSegment + to * (radius / distance);
        }

        private static MiniVanPlayer[] CollectPlayers()
        {
            if (cachedPlayers == null || Time.time >= nextPlayerCacheTime)
            {
                cachedPlayers = Object.FindObjectsByType<MiniVanPlayer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                nextPlayerCacheTime = Time.time + 0.25f;
            }

            return cachedPlayers;
        }

        private static MiniVanAcidSpit CreateFallback(Vector3 origin)
        {
            GameObject glob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glob.name = "AcidSpit";
            glob.transform.position = origin;
            glob.transform.localScale = Vector3.one * 0.58f;
            Collider primitiveCollider = glob.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                Destroy(primitiveCollider);
            }

            Renderer renderer = glob.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = MiniVanAcidClotVisual.CreateAcidMaterial();
            }

            Rigidbody createdBody = glob.AddComponent<Rigidbody>();
            createdBody.isKinematic = true;
            SphereCollider createdTrigger = glob.AddComponent<SphereCollider>();
            createdTrigger.isTrigger = true;
            MiniVanAcidSpit spit = glob.AddComponent<MiniVanAcidSpit>();
            spit.Radius = 0.38f;
            return spit;
        }
    }
}
