using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanSeat : MonoBehaviour
    {
        public const float DefaultEnterRadius = 2.15f;

        [field: SerializeField] public int SeatIndex { get; set; }
        [field: SerializeField] public bool IsDriverSeat { get; set; }
        [field: SerializeField] public string DisplayName { get; set; } = "Passenger";
        [field: SerializeField] public MiniVanVehicle Vehicle { get; set; }
        [field: SerializeField] public Transform SitPoint { get; set; }
        [field: SerializeField] public Transform ExitPoint { get; set; }
        [field: SerializeField] public float EnterRadius { get; set; } = DefaultEnterRadius;

        public bool IsAvailable
        {
            get { return Vehicle != null && Vehicle.IsSeatAvailable(SeatIndex); }
        }

        public Transform GetEnterPoint()
        {
            return ExitPoint != null ? ExitPoint : transform;
        }

        /// <summary>
        /// True when the player is next to the door/exit, not e.g. on the hood aiming through glass.
        /// </summary>
        public bool IsPlayerInEnterRange(Vector3 playerPosition)
        {
            Transform entry = GetEnterPoint();
            if (entry == null)
            {
                return false;
            }

            float radius = EnterRadius > 0.1f ? EnterRadius : DefaultEnterRadius;
            if (Vector3.Distance(playerPosition, entry.position) > radius)
            {
                return false;
            }

            // Driver: block entry from the front of the van (hood / bumper).
            if (IsDriverSeat && Vehicle != null)
            {
                Vector3 local = Vehicle.transform.InverseTransformPoint(playerPosition);
                if (local.z > 1.35f && Mathf.Abs(local.x) < 1.35f)
                {
                    return false;
                }
            }

            return true;
        }

        private void Reset()
        {
            SitPoint = transform;
        }
    }
}
