using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Spawns puddle prefabs where dripping particles hit the ground.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class MiniVanAcidDripCollision : MonoBehaviour
    {
        public GameObject PuddlePrefab;
        [Min(0.05f)] public float MinSpacing = 0.14f;
        [Min(0.1f)] public float PuddleLifetime = 6f;
        [Min(0.05f)] public float SpacingMemorySeconds = 0.45f;
        [Range(0f, 1f)] public float MinGroundNormalY = 0.15f;
        [Min(0.05f)] public float GroundProbeDistance = 0.45f;
        [Min(0.02f)] public float ProbeInterval = 0.08f;

        private ParticleSystem drip;
        private float nextProbeTime;
        private ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[96];
        private readonly List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>(16);
        private static readonly List<Vector3> recentPuddles = new List<Vector3>(64);
        private static readonly List<float> recentTimes = new List<float>(64);

        private void Awake()
        {
            drip = GetComponent<ParticleSystem>();
            ConfigureCollision(drip);
        }

        private void LateUpdate()
        {
            if (PuddlePrefab == null || drip == null || Time.time < nextProbeTime)
            {
                return;
            }

            nextProbeTime = Time.time + ProbeInterval;
            ProbeParticlesNearGround();
        }

        private void OnParticleCollision(GameObject other)
        {
            if (PuddlePrefab == null || drip == null || other == null || IsIgnored(other))
            {
                return;
            }

            int count = drip.GetCollisionEvents(other, collisionEvents);
            for (int i = 0; i < count; i++)
            {
                ParticleCollisionEvent hit = collisionEvents[i];
                if (hit.normal.y < MinGroundNormalY)
                {
                    continue;
                }

                TrySpawnPuddle(hit.intersection, hit.normal);
            }
        }

        private void ProbeParticlesNearGround()
        {
            int max = drip.main.maxParticles;
            if (particleBuffer == null || particleBuffer.Length < max)
            {
                particleBuffer = new ParticleSystem.Particle[Mathf.Max(96, max)];
            }

            int count = drip.GetParticles(particleBuffer);
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = particleBuffer[i].position;
                if (!Physics.Raycast(
                        origin,
                        Vector3.down,
                        out RaycastHit hit,
                        GroundProbeDistance,
                        ~0,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (hit.normal.y < MinGroundNormalY || IsIgnored(hit.collider.gameObject))
                {
                    continue;
                }

                TrySpawnPuddle(hit.point, hit.normal);
            }
        }

        private void TrySpawnPuddle(Vector3 point, Vector3 normal)
        {
            PruneRecent();
            float minSq = MinSpacing * MinSpacing;
            for (int i = 0; i < recentPuddles.Count; i++)
            {
                if ((recentPuddles[i] - point).sqrMagnitude < minSq)
                {
                    return;
                }
            }

            Vector3 up = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up);
            GameObject puddle = Instantiate(PuddlePrefab, point + up * 0.02f, rotation);
            puddle.name = PuddlePrefab.name;
            MiniVanAcidPuddle life = puddle.GetComponent<MiniVanAcidPuddle>();
            if (life == null)
            {
                life = puddle.AddComponent<MiniVanAcidPuddle>();
            }

            life.Lifetime = PuddleLifetime;
            recentPuddles.Add(point);
            recentTimes.Add(Time.time);
        }

        private void PruneRecent()
        {
            float expireAt = Time.time - SpacingMemorySeconds;
            for (int i = recentTimes.Count - 1; i >= 0; i--)
            {
                if (recentTimes[i] < expireAt)
                {
                    recentTimes.RemoveAt(i);
                    recentPuddles.RemoveAt(i);
                }
            }
        }

        private static bool IsIgnored(GameObject other)
        {
            return other != null &&
                   (other.GetComponentInParent<MiniVanPlayer>() != null ||
                    other.GetComponentInParent<MiniVanZombie>() != null);
        }

        public static void ConfigureCollision(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.sendCollisionMessages = true;
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.lifetimeLoss = 1f;
            collision.radiusScale = 2.5f;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.enableDynamicColliders = false;
            collision.collidesWith = ~0;
            collision.maxCollisionShapes = 256;
        }
    }
}
