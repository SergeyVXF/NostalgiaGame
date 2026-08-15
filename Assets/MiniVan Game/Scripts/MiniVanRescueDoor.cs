using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanRescueDoor : MonoBehaviour
    {
        public float OpenAngle = -86f;
        public float AnimationSpeed = 8f;
        public Vector3 LocalAxis = Vector3.up;

        private Quaternion closedRotation;
        private bool captured;
        private float openUntil;

        private void Awake()
        {
            CaptureClosedRotation();
        }

        private void Update()
        {
            CaptureClosedRotation();

            bool open = Time.time < openUntil;
            Vector3 axis = LocalAxis.sqrMagnitude > 0.001f ? LocalAxis.normalized : Vector3.up;
            Quaternion target = open ? closedRotation * Quaternion.AngleAxis(OpenAngle, axis) : closedRotation;
            float blend = 1f - Mathf.Exp(-AnimationSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, blend);
        }

        public void OpenFor(float seconds)
        {
            CaptureClosedRotation();
            openUntil = Mathf.Max(openUntil, Time.time + Mathf.Max(0.1f, seconds));
        }

        public static void OpenNearest(Vector3 position, float seconds)
        {
            MiniVanRescueDoor door = FindNearest(position);
            if (door != null)
            {
                door.OpenFor(seconds);
            }
        }

        private static MiniVanRescueDoor FindNearest(Vector3 position)
        {
            MiniVanRescueDoor[] doors = FindObjectsByType<MiniVanRescueDoor>(FindObjectsSortMode.None);
            MiniVanRescueDoor best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < doors.Length; i++)
            {
                MiniVanRescueDoor door = doors[i];
                if (door == null || !door.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, door.transform.position);
                if (distance < bestDistance)
                {
                    best = door;
                    bestDistance = distance;
                }
            }

            if (best != null && bestDistance <= 8f)
            {
                return best;
            }

            GameObject fallback = FindNearestDoorObject(position);
            return fallback != null ? fallback.AddComponent<MiniVanRescueDoor>() : null;
        }

        private static GameObject FindNearestDoorObject(Vector3 position)
        {
            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            GameObject best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null || !candidate.activeInHierarchy || !candidate.name.ToLowerInvariant().Contains("door"))
                {
                    continue;
                }

                float distance = Vector3.Distance(position, candidate.transform.position);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return bestDistance <= 8f ? best : null;
        }

        private void CaptureClosedRotation()
        {
            if (captured)
            {
                return;
            }

            closedRotation = transform.localRotation;
            captured = true;
        }
    }
}
