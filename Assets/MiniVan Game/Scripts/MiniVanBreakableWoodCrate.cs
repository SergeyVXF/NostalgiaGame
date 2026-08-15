using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Wooden crate that breaks from bat hits or minivan impact into physical debris.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanBreakableWoodCrate : MonoBehaviour
    {
        [Min(1)] public int HitPoints = 2;
        [Min(0f)] public float VehicleBreakSpeedKph = 12f;
        public GameObject IntactRoot;
        public Collider SolidCollider;
        public Material WoodMaterial;
        [Min(4)] public int DebrisPieceCount = 10;
        [Min(1f)] public float DebrisLifetime = 12f;
        [Min(0.05f)] public float BreakImpulse = 3.2f;
        [Min(0)] public int AmberButtonDropMin = 1;
        [Min(0)] public int AmberButtonDropMax = 5;
        [Min(0.35f)] public float AmberButtonScatterRadius = 3.3f;

        private int health;
        private bool broken;
        private bool debrisSpawned;

        public int CurrentHealth => health;
        public bool IsBroken => broken;

        private void Awake()
        {
            EnsureReady();
        }

        private void OnEnable()
        {
            EnsureReady();
        }

        private void EnsureReady()
        {
            if (health <= 0 && !broken)
            {
                health = Mathf.Max(1, HitPoints);
            }

            if (IntactRoot == null)
            {
                Transform visual = transform.Find("Intact");
                IntactRoot = visual != null ? visual.gameObject : gameObject;
            }

            if (SolidCollider == null)
            {
                SolidCollider = GetComponent<Collider>();
            }
        }

        public bool ServerApplyBatHit(int damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            EnsureReady();
            if (broken)
            {
                return false;
            }

            health = Mathf.Max(0, health - Mathf.Max(1, damage));
            if (health > 0)
            {
                return true;
            }

            Vector3 impact = hitDirection.sqrMagnitude > 0.0001f
                ? hitDirection.normalized * BreakImpulse
                : transform.forward * BreakImpulse;
            Break(hitPoint, impact);
            return true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (broken || collision == null)
            {
                return;
            }

            MiniVanVehicle vehicle = collision.collider != null
                ? collision.collider.GetComponentInParent<MiniVanVehicle>()
                : null;
            if (vehicle == null)
            {
                return;
            }

            float speedKph = collision.relativeVelocity.magnitude * 3.6f;
            if (speedKph < Mathf.Max(0f, VehicleBreakSpeedKph))
            {
                return;
            }

            Vector3 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            Break(point, collision.relativeVelocity);
        }

        public void Break(Vector3 impactPoint, Vector3 impactVelocity)
        {
            if (broken)
            {
                return;
            }

            broken = true;
            health = 0;

            if (IntactRoot != null)
            {
                IntactRoot.SetActive(false);
            }

            if (SolidCollider != null)
            {
                SolidCollider.enabled = false;
            }

            SpawnDebris(impactPoint, impactVelocity);
            SpawnAmberButtonLoot(impactPoint, impactVelocity);
        }

        private void SpawnAmberButtonLoot(Vector3 impactPoint, Vector3 impactVelocity)
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network != null && !network.IsServer)
            {
                return;
            }

            int min = Mathf.Max(0, AmberButtonDropMin);
            int max = Mathf.Max(min, AmberButtonDropMax);
            if (max <= 0)
            {
                return;
            }

            Vector3 center = IntactRoot != null ? IntactRoot.transform.position : transform.position;
            center += Vector3.up * 0.15f;

            // Push outward from the hit so loot fans away from the bat/van contact point.
            Vector3 burst = impactVelocity;
            if (burst.sqrMagnitude < 0.01f)
            {
                burst = center - impactPoint;
            }

            MiniVanAmberButtonPickup.ServerSpawnBurst(
                center,
                Mathf.Max(1, min),
                max,
                AmberButtonScatterRadius,
                burst);
        }

        private void SpawnDebris(Vector3 impactPoint, Vector3 impactVelocity)
        {
            if (debrisSpawned || !Application.isPlaying)
            {
                return;
            }

            debrisSpawned = true;

            Material material = WoodMaterial;
            if (material == null && IntactRoot != null)
            {
                Renderer renderer = IntactRoot.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    material = renderer.sharedMaterial;
                }
            }

            Vector3 center = IntactRoot != null ? IntactRoot.transform.position : transform.position;
            Vector3 crateSize = IntactRoot != null ? IntactRoot.transform.lossyScale : Vector3.one;
            int count = Mathf.Clamp(DebrisPieceCount, 4, 24);

            for (int i = 0; i < count; i++)
            {
                bool plank = i < count * 2 / 3;
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = plank ? "WoodPlank_" + i : "WoodChunk_" + i;
                piece.transform.position = center + Random.insideUnitSphere * 0.22f * Mathf.Max(crateSize.x, crateSize.z);
                piece.transform.rotation = Random.rotation;

                if (plank)
                {
                    piece.transform.localScale = new Vector3(
                        Random.Range(0.28f, 0.55f) * crateSize.x,
                        Random.Range(0.03f, 0.07f),
                        Random.Range(0.12f, 0.28f) * crateSize.z);
                }
                else
                {
                    float s = Random.Range(0.10f, 0.22f);
                    piece.transform.localScale = new Vector3(
                        s * crateSize.x,
                        s * crateSize.y,
                        s * crateSize.z);
                }

                Renderer pieceRenderer = piece.GetComponent<Renderer>();
                if (pieceRenderer != null && material != null)
                {
                    pieceRenderer.sharedMaterial = material;
                }

                float volume = Mathf.Max(0.0005f,
                    piece.transform.localScale.x *
                    piece.transform.localScale.y *
                    piece.transform.localScale.z);
                Rigidbody body = piece.AddComponent<Rigidbody>();
                body.mass = Mathf.Clamp(volume * 180f, 0.08f, 2.5f);
                body.useGravity = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearDamping = 0.35f;
                body.angularDamping = 0.55f;

                Vector3 outward = (piece.transform.position - impactPoint).normalized;
                if (outward.sqrMagnitude < 0.01f)
                {
                    outward = Random.onUnitSphere;
                }

                Vector3 burst = outward * Random.Range(BreakImpulse * 0.45f, BreakImpulse * 1.1f)
                    + Vector3.up * Random.Range(1.2f, 3.4f)
                    + impactVelocity * Random.Range(0.15f, 0.45f);
                body.linearVelocity = burst;
                body.angularVelocity = Random.insideUnitSphere * Random.Range(4f, 14f);

                Object.Destroy(piece, DebrisLifetime * Random.Range(0.85f, 1.2f));
            }
        }
    }
}
