using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanReviveStationKind
    {
        DoctorTable,
        CitySeller
    }

    [DisallowMultipleComponent]
    public sealed class MiniVanReviveStation : MonoBehaviour
    {
        public MiniVanReviveStationKind Kind;
        [Min(0)] public int Price = 100;
        [Min(0.1f)] public float UseRadius = 3.25f;
        public Transform BodyPoint;

        public Vector3 GetBodyPosition()
        {
            return BodyPoint != null ? BodyPoint.position : transform.position + transform.up * 0.9f;
        }

        public Quaternion GetBodyRotation()
        {
            return BodyPoint != null ? BodyPoint.rotation : transform.rotation;
        }
    }
}
