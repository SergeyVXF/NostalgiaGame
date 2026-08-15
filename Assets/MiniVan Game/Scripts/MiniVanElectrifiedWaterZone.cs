using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanElectrifiedWaterZone : MonoBehaviour
    {
        public MiniVanElectrifiedGenerator PowerSource;
        public bool UseCircularArea = true;
        public float CircularRadius = 6.5f;
        public float CircularHalfHeight = 1.1f;
        public Vector3 CircularCenterOffset = Vector3.zero;
        public int PlayerDamagePerTick = 1;
        public float VehicleDamagePerTick = 2f;
        public float TickInterval = 1f;
        public Renderer[] WaterRenderers;
        public ParticleSystem[] ElectricityEffects;
        public Color PoweredWaterColor = new Color(0.27f, 0.63f, 0.78f, 0.78f);
        public Color SafeWaterColor = new Color(0.18f, 0.32f, 0.36f, 0.55f);
        public bool DebugDamage;

        private readonly Dictionary<MiniVanPlayer, float> nextPlayerDamageTime =
            new Dictionary<MiniVanPlayer, float>();
        private readonly Dictionary<MiniVanVehicle, float> nextVehicleDamageTime =
            new Dictionary<MiniVanVehicle, float>();
        private readonly HashSet<MiniVanPlayer> scannedPlayers = new HashSet<MiniVanPlayer>();
        private readonly HashSet<MiniVanVehicle> scannedVehicles = new HashSet<MiniVanVehicle>();
        private readonly Collider[] overlapBuffer = new Collider[192];

        private Collider zoneCollider;
        private bool hasVisualState;
        private bool lastVisualPowered;
        private bool localPowered = true;

        public bool IsPowered => PowerSource != null ? PowerSource.IsPowered : localPowered;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;
            RefreshVisuals();
        }

        private void Update()
        {
            RefreshVisuals();
            ApplyOverlapDamage();
        }

        public void SetPowered(bool powered)
        {
            localPowered = powered;
            RefreshVisuals();
        }

        private void ApplyOverlapDamage()
        {
            if (!IsPowered || zoneCollider == null)
            {
                return;
            }

            scannedPlayers.Clear();
            scannedVehicles.Clear();

            int count = ScanOverlaps();
            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit == null || hit == zoneCollider || !IsInsideDamageArea(hit))
                {
                    continue;
                }

                if (hit.transform != null && hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                MiniVanPlayer player = hit.GetComponentInParent<MiniVanPlayer>();
                if (player != null && scannedPlayers.Add(player))
                {
                    ApplyPlayerTick(player, GetTickTime());
                }

                MiniVanVehicle vehicle = hit.GetComponentInParent<MiniVanVehicle>();
                if (vehicle != null && scannedVehicles.Add(vehicle))
                {
                    ApplyVehicleTick(vehicle, GetTickTime());
                }
            }
        }

        private static float GetTickTime()
        {
            return Time.realtimeSinceStartup;
        }

        private int ScanOverlaps()
        {
            if (UseCircularArea)
            {
                return Physics.OverlapSphereNonAlloc(
                    GetCircularCenter(),
                    Mathf.Max(0.1f, CircularRadius),
                    overlapBuffer,
                    ~0,
                    QueryTriggerInteraction.Collide);
            }

            BoxCollider box = zoneCollider as BoxCollider;
            if (box != null)
            {
                Vector3 center = box.transform.TransformPoint(box.center);
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
                return Physics.OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    overlapBuffer,
                    box.transform.rotation,
                    ~0,
                    QueryTriggerInteraction.Collide);
            }

            Bounds bounds = zoneCollider.bounds;
            return Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                overlapBuffer,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);
        }

        private bool IsInsideDamageArea(Collider hit)
        {
            if (!UseCircularArea || hit == null)
            {
                return true;
            }

            Vector3 center = GetCircularCenter();
            Vector3 closest = hit.ClosestPoint(center);
            Vector2 delta = new Vector2(closest.x - center.x, closest.z - center.z);
            if (delta.sqrMagnitude > CircularRadius * CircularRadius)
            {
                return false;
            }

            return Mathf.Abs(closest.y - center.y) <= Mathf.Max(0.1f, CircularHalfHeight);
        }

        public Vector3 GetCircularCenter()
        {
            return transform.TransformPoint(CircularCenterOffset);
        }

        private void ApplyPlayerTick(MiniVanPlayer player, float now)
        {
            if (player == null || player.IsZombieDead)
            {
                return;
            }

            if (nextPlayerDamageTime.TryGetValue(player, out float nextTime) && now < nextTime)
            {
                return;
            }

            nextPlayerDamageTime[player] = now + Mathf.Max(0.05f, TickInterval);
            player.ReceiveZombieDamageServer(Mathf.Max(1, PlayerDamagePerTick));

            if (DebugDamage)
            {
                Debug.Log("[MiniVanElectric] player shock " + player.name);
            }
        }

        private void ApplyVehicleTick(MiniVanVehicle vehicle, float now)
        {
            if (vehicle == null)
            {
                return;
            }

            if (nextVehicleDamageTime.TryGetValue(vehicle, out float nextTime) && now < nextTime)
            {
                return;
            }

            nextVehicleDamageTime[vehicle] = now + Mathf.Max(0.05f, TickInterval);
            vehicle.ApplyVehicleDamage(Mathf.Max(0f, VehicleDamagePerTick), "electrified-water");

            if (DebugDamage)
            {
                Debug.Log("[MiniVanElectric] vehicle shock " + vehicle.name);
            }
        }

        private void RefreshVisuals()
        {
            bool powered = IsPowered;
            if (hasVisualState && powered == lastVisualPowered)
            {
                return;
            }

            hasVisualState = true;
            lastVisualPowered = powered;

            if (WaterRenderers != null)
            {
                Color color = powered ? PoweredWaterColor : SafeWaterColor;
                for (int i = 0; i < WaterRenderers.Length; i++)
                {
                    Renderer renderer = WaterRenderers[i];
                    if (renderer == null || renderer.sharedMaterial == null)
                    {
                        continue;
                    }

                    if (renderer.material.HasProperty("_BaseColor"))
                    {
                        renderer.material.SetColor("_BaseColor", color);
                    }
                    else if (renderer.material.HasProperty("_Color"))
                    {
                        renderer.material.SetColor("_Color", color);
                    }
                }
            }

            if (ElectricityEffects == null)
            {
                return;
            }

            for (int i = 0; i < ElectricityEffects.Length; i++)
            {
                ParticleSystem effect = ElectricityEffects[i];
                if (effect == null)
                {
                    continue;
                }

                if (powered && !effect.isPlaying)
                {
                    effect.Play(true);
                }
                else if (!powered && effect.isPlaying)
                {
                    effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!UseCircularArea)
            {
                return;
            }

            Vector3 center = GetCircularCenter();
            float radius = Mathf.Max(0.1f, CircularRadius);
            Gizmos.color = IsPowered ? new Color(0.15f, 0.9f, 1f, 0.35f) : new Color(0.2f, 0.4f, 0.45f, 0.25f);

            const int segments = 48;
            Vector3 previous = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
