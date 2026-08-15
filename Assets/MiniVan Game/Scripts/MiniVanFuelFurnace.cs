using UnityEngine;

namespace MiniVanGame
{
    public sealed class MiniVanFuelFurnace : MonoBehaviour
    {
        public MiniVanVehicle Vehicle;
        public float InteractRadius = 3f;

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, transform.position) <= InteractRadius;
        }
    }
}
