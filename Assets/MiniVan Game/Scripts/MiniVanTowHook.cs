using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanTowHook : MonoBehaviour
    {
        public Transform HookPoint;
        public float AttachRadius = 2.2f;
        public Vector3 DefaultLocalHookPosition = new Vector3(0f, 0.85f, -3.65f);

        private Rigidbody vehicleBody;

        private void Awake()
        {
            EnsureHookPoint();
            vehicleBody = GetComponentInParent<Rigidbody>();
        }



        public Vector3 AnchorPosition
        {
            get
            {
                EnsureHookPoint();
                return HookPoint != null ? HookPoint.position : transform.TransformPoint(DefaultLocalHookPosition);
            }
        }

        public Vector3 VehicleVelocity
        {
            get
            {
                if (vehicleBody == null)
                {
                    vehicleBody = GetComponentInParent<Rigidbody>();
                }

                return vehicleBody != null ? vehicleBody.linearVelocity : Vector3.zero;
            }
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, AnchorPosition) <= AttachRadius;
        }

        public static MiniVanTowHook FindNearest(Vector3 worldPosition, float extraRadius = 0f)
        {
            MiniVanTowHook[] hooks = FindObjectsByType<MiniVanTowHook>(FindObjectsSortMode.None);
            MiniVanTowHook bestHook = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hooks.Length; i++)
            {
                if (hooks[i] == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(worldPosition, hooks[i].AnchorPosition);
                float allowedDistance = hooks[i].AttachRadius + Mathf.Max(0f, extraRadius);
                if (distance <= allowedDistance && distance < bestDistance)
                {
                    bestHook = hooks[i];
                    bestDistance = distance;
                }
            }

            return bestHook;
        }

        public void EnsureHookPoint()
        {
            if (HookPoint != null)
            {
                return;
            }

            Transform existing = transform.Find("Tow Hook Point");
            if (existing != null)
            {
                HookPoint = existing;
                return;
            }

            GameObject hookPointObject = new GameObject("Tow Hook Point");
            hookPointObject.transform.SetParent(transform, false);
            hookPointObject.transform.localPosition = DefaultLocalHookPosition;
            hookPointObject.transform.localRotation = Quaternion.identity;
            HookPoint = hookPointObject.transform;
        }
    }
}
