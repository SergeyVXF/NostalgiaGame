using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanGameModeInteriorZone : MonoBehaviour
    {
        private const float ZombieBoundaryPadding = 0.2f;
        private static readonly List<MiniVanGameModeInteriorZone> ZombieSafeZones =
            new List<MiniVanGameModeInteriorZone>();

        public int SiteIndex;
        public bool BlocksZombies;

        private BoxCollider zone;

        private void Awake()
        {
            CacheCollider();
        }

        private void OnEnable()
        {
            CacheCollider();
            RefreshZombieRegistry();
        }

        private void OnDisable()
        {
            ZombieSafeZones.Remove(this);
        }

        private void OnValidate()
        {
            CacheCollider();
            RefreshZombieRegistry();
        }

        public bool Contains(Vector3 worldPosition)
        {
            return zone != null && zone.bounds.Contains(worldPosition);
        }

        public bool ContainsPlanar(Vector3 worldPosition)
        {
            CacheCollider();
            if (zone == null) return false;
            Vector3 local = transform.InverseTransformPoint(worldPosition) - zone.center;
            Vector3 half = zone.size * 0.5f;
            return Mathf.Abs(local.x) < half.x && Mathf.Abs(local.z) < half.z;
        }

        public Vector3 NearestZombieOutsidePoint(Vector3 worldPosition)
        {
            CacheCollider();
            if (zone == null) return worldPosition;

            Vector3 local = transform.InverseTransformPoint(worldPosition) - zone.center;
            Vector3 half = zone.size * 0.5f;
            float distanceToX = half.x - Mathf.Abs(local.x);
            float distanceToZ = half.z - Mathf.Abs(local.z);
            if (distanceToX < distanceToZ)
            {
                float sign = Mathf.Approximately(local.x, 0f) ? 1f : Mathf.Sign(local.x);
                local.x = sign * (half.x + ZombieBoundaryPadding);
            }
            else
            {
                float sign = Mathf.Approximately(local.z, 0f) ? 1f : Mathf.Sign(local.z);
                local.z = sign * (half.z + ZombieBoundaryPadding);
            }
            return transform.TransformPoint(local + zone.center);
        }

        public static bool IsZombieProtected(Vector3 worldPosition)
        {
            CleanupZombieRegistry();
            for (int i = 0; i < ZombieSafeZones.Count; i++)
            {
                if (ZombieSafeZones[i].ContainsPlanar(worldPosition)) return true;
            }
            return false;
        }

        public static bool TryGetContainingZombieSafeZone(Vector3 worldPosition,
            out MiniVanGameModeInteriorZone safeZone)
        {
            CleanupZombieRegistry();
            for (int i = 0; i < ZombieSafeZones.Count; i++)
            {
                if (!ZombieSafeZones[i].ContainsPlanar(worldPosition)) continue;
                safeZone = ZombieSafeZones[i];
                return true;
            }
            safeZone = null;
            return false;
        }

        public static Vector3 ConstrainZombieMovement(Vector3 currentPosition, Vector3 desiredPosition)
        {
            CleanupZombieRegistry();
            for (int i = 0; i < ZombieSafeZones.Count; i++)
            {
                MiniVanGameModeInteriorZone safeZone = ZombieSafeZones[i];
                if (safeZone.ContainsPlanar(currentPosition) || !safeZone.ContainsPlanar(desiredPosition))
                    continue;

                Vector3 slideX = new Vector3(desiredPosition.x, desiredPosition.y, currentPosition.z);
                Vector3 slideZ = new Vector3(currentPosition.x, desiredPosition.y, desiredPosition.z);
                bool xAllowed = !safeZone.ContainsPlanar(slideX);
                bool zAllowed = !safeZone.ContainsPlanar(slideZ);
                if (xAllowed && zAllowed)
                {
                    desiredPosition = (slideX - currentPosition).sqrMagnitude >=
                                      (slideZ - currentPosition).sqrMagnitude ? slideX : slideZ;
                }
                else if (xAllowed) desiredPosition = slideX;
                else if (zAllowed) desiredPosition = slideZ;
                else
                {
                    desiredPosition.x = currentPosition.x;
                    desiredPosition.z = currentPosition.z;
                }
            }
            return desiredPosition;
        }

        public static MiniVanGameModeInteriorZone EnsureZombieSafeZone(Transform compound, int siteIndex)
        {
            if (compound == null) return null;
            MiniVanGameModeInteriorZone[] zones = compound.GetComponentsInChildren<MiniVanGameModeInteriorZone>(true);
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] == null || !zones[i].BlocksZombies) continue;
                zones[i].RefreshZombieRegistry();
                return zones[i];
            }

            GameObject volume = new GameObject("Zombie Safe Zone");
            volume.transform.SetParent(compound, false);
            volume.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            BoxCollider box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(43f, 6f, 33f);
            MiniVanGameModeInteriorZone safeZone = volume.AddComponent<MiniVanGameModeInteriorZone>();
            safeZone.SiteIndex = siteIndex;
            safeZone.BlocksZombies = true;
            safeZone.RefreshZombieRegistry();
            return safeZone;
        }

        private void CacheCollider()
        {
            if (zone == null) zone = GetComponent<BoxCollider>();
            if (zone != null) zone.isTrigger = true;
        }

        private void RefreshZombieRegistry()
        {
            ZombieSafeZones.Remove(this);
            if (isActiveAndEnabled && BlocksZombies) ZombieSafeZones.Add(this);
        }

        private static void CleanupZombieRegistry()
        {
            for (int i = ZombieSafeZones.Count - 1; i >= 0; i--)
            {
                if (ZombieSafeZones[i] == null || !ZombieSafeZones[i].isActiveAndEnabled ||
                    !ZombieSafeZones[i].BlocksZombies)
                {
                    ZombieSafeZones.RemoveAt(i);
                }
            }
        }

        private void Reset()
        {
            CacheCollider();
        }
    }
}
