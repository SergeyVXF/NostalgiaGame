using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanRescueBunker : MonoBehaviour
    {
        public Transform Door;
        public Transform DoorOutsidePoint;
        public Transform DoorInsidePoint;

        [Header("Stop Zone")]
        public MiniVanRescueStopZone StopZone;
        public bool AutoFindSceneStopZone = true;
        public float StopZoneSearchRadius = 80f;
        public float DeliveryRadius = 30f;
        public float ZombieBlockRadius = 12f;
        public bool ShowStopZone = true;
        public Color StopZoneColor = new Color(0.15f, 0.55f, 1f, 0.24f);

        public Vector3 GetDoorPosition()
        {
            return Door != null ? Door.position : transform.position;
        }

        public Vector3 GetDoorOutsidePosition()
        {
            if (DoorOutsidePoint != null)
            {
                return DoorOutsidePoint.position;
            }

            return GetDoorPosition();
        }

        public Vector3 GetDoorInsidePosition()
        {
            if (DoorInsidePoint != null)
            {
                return DoorInsidePoint.position;
            }

            return GetDoorPosition();
        }

        public Vector3 GetCheckPosition()
        {
            if (StopZone != null)
            {
                return StopZone.Position;
            }

            return Door != null ? Door.position : transform.position;
        }

        public float GetDeliveryRadius()
        {
            return StopZone != null ? StopZone.EffectiveRadius : Mathf.Max(0.5f, DeliveryRadius);
        }

        public Vector3 GetZombieCheckPosition()
        {
            if (StopZone != null && StopZone.UseForZombieCheck)
            {
                return StopZone.Position;
            }

            return GetCheckPosition();
        }

        private void Awake()
        {
            EnsureStopZoneReference();
            EnsureStopZoneVisual();
        }

        private void Start()
        {
            EnsureStopZoneReference();
            EnsureStopZoneVisual();
        }

        private void EnsureStopZoneVisual()
        {
            if (!ShowStopZone)
            {
                return;
            }

            if (StopZone != null)
            {
                StopZone.ZoneKind = MiniVanRescueStopZone.StopZoneKind.Bunker;
                StopZone.Radius = GetDeliveryRadius();
                StopZone.BunkerColor = StopZoneColor;
                StopZone.ShowVisual = true;
                StopZone.EnsureVisual();
                return;
            }

            MiniVanRescueStopZoneVisual.Ensure(transform, "Bunker Stop Zone", GetDeliveryRadius(), StopZoneColor);
        }

        private void OnValidate()
        {
            EnsureStopZoneReference();

            if (Door == null)
            {
                Door = FindChild("Bunker_Door", "Door");
            }

            if (DoorOutsidePoint == null)
            {
                DoorOutsidePoint = FindChild("DoorOutsidePoint", "Bunker_DoorOutsidePoint", "OutsidePoint");
            }

            if (DoorInsidePoint == null)
            {
                DoorInsidePoint = FindChild("DoorInsidePoint", "Bunker_DoorInsidePoint", "InsidePoint");
            }
        }

        private void EnsureStopZoneReference()
        {
            if (StopZone != null)
            {
                StopZone.ZoneKind = MiniVanRescueStopZone.StopZoneKind.Bunker;
                return;
            }

            MiniVanRescueStopZone[] zones = GetComponentsInChildren<MiniVanRescueStopZone>(true);
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].ZoneKind == MiniVanRescueStopZone.StopZoneKind.Bunker)
                {
                    StopZone = zones[i];
                    return;
                }
            }

            if (!AutoFindSceneStopZone)
            {
                return;
            }

            zones = FindObjectsByType<MiniVanRescueStopZone>(FindObjectsSortMode.None);
            MiniVanRescueStopZone best = null;
            float bestDistance = Mathf.Max(0.5f, StopZoneSearchRadius);
            for (int i = 0; i < zones.Length; i++)
            {
                MiniVanRescueStopZone zone = zones[i];
                if (zone == null || zone.ZoneKind != MiniVanRescueStopZone.StopZoneKind.Bunker)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, zone.Position);
                if (distance <= bestDistance)
                {
                    best = zone;
                    bestDistance = distance;
                }
            }

            StopZone = best;
        }

        private Transform FindChild(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = transform.Find(names[i]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.55f, 1f, 0.25f);
            Gizmos.DrawWireSphere(GetCheckPosition(), GetDeliveryRadius());

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(GetDoorOutsidePosition(), 0.25f);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(GetDoorInsidePosition(), 0.25f);
        }
    }
}
