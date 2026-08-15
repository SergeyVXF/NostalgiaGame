using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanCarBatteryReceiver : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float InteractionReach = 2.35f;

        public MiniVanVehicle Vehicle;
        public Transform PlacementPoint;
        public MiniVanCarBattery InstalledBattery;

        public bool HasBattery => InstalledBattery != null && InstalledBattery.IsInstalled;
        public bool CanPlayerAccess(MiniVanPlayer player)
        {
            return player != null &&
                   Vector3.Distance(player.transform.position, transform.position) <= InteractionReach &&
                   IsHoodOpen() &&
                   HasInteractionLineOfSight(player);
        }

        private void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.25f, 0.9f, 1.15f);
            box.center = new Vector3(0f, 0.25f, 0f);
        }

        private void Update()
        {
            ClearStaleInstalledBattery();
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach ||
                !HasInteractionLineOfSight(player) ||
                !IsHoodOpen())
            {
                return string.Empty;
            }

            if (MiniVanCarBattery.GetCarriedBy(player) != null && !HasBattery)
            {
                return "E - insert car battery";
            }

            return HasBattery ? "E - remove car battery" : "Battery tray empty";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1))
            {
                return;
            }

            if (player == null || Vector3.Distance(player.transform.position, transform.position) > InteractionReach)
            {
                return;
            }

            if (!CanPlayerAccess(player))
            {
                return;
            }

            ClearStaleInstalledBattery();
            MiniVanCarBattery carried = MiniVanCarBattery.GetCarriedBy(player);
            if (carried != null && !HasBattery)
            {
                AttachBattery(carried);
                return;
            }

            if (InstalledBattery != null)
            {
                InstalledBattery.TryPickup(player);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool AttachBattery(MiniVanCarBattery battery)
        {
            ClearStaleInstalledBattery();
            if (battery == null || HasBattery)
            {
                return false;
            }

            InstalledBattery = battery;
            battery.PlaceInto(this);
            if (Vehicle != null)
            {
                Vehicle.NotifyCarBatteryInstalled(battery, true, battery.Charge01);
            }
            return true;
        }

        public void DetachBattery(MiniVanCarBattery battery)
        {
            if (InstalledBattery != battery)
            {
                return;
            }

            float charge = battery != null ? battery.Charge01 : 0f;
            InstalledBattery = null;
            if (battery != null)
            {
                battery.ClearInstalledReceiver(this);
            }

            if (Vehicle != null)
            {
                Vehicle.NotifyCarBatteryInstalled(null, false, charge);
            }
        }

        private void ClearStaleInstalledBattery()
        {
            if (InstalledBattery != null && !InstalledBattery.IsInstalled)
            {
                InstalledBattery = null;
                if (Vehicle != null)
                {
                    Vehicle.NotifyCarBatteryInstalled(null, false, 0f);
                }
            }
        }

        private bool IsHoodOpen()
        {
            return Vehicle != null && Vehicle.IsFrontCapotOpenForInteraction();
        }

        private bool HasInteractionLineOfSight(MiniVanPlayer player)
        {
            Transform eye = player.CameraRoot != null ? player.CameraRoot : player.transform;
            Vector3 origin = eye.position;
            Vector3 target = transform.position + Vector3.up * 0.25f;
            Vector3 toTarget = target - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.05f)
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(origin, toTarget / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null ||
                    hit.transform.IsChildOf(player.transform) ||
                    hit.transform.IsChildOf(transform) ||
                    InstalledBattery != null && hit.transform.IsChildOf(InstalledBattery.transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
