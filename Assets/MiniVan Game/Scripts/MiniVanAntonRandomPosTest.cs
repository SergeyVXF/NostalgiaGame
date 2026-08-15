using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Test helper: picks a random grounded spot for Anton inside a radius around this object.
    /// Any floor height is allowed — ground, roofs, upper storeys — but never mid-air.
    /// </summary>
    public sealed class MiniVanAntonRandomPosTest : MonoBehaviour
    {
        [Header("Area")]
        public bool UseRandomPosition = true;
        public float MinRadius = 15f;
        public float MaxRadius = 120f;

        [Header("Surface search")]
        [Tooltip("Ray starts this high above the area centre.")]
        public float SearchHeightAbove = 250f;
        [Tooltip("Ray continues this far below the area centre.")]
        public float SearchDepthBelow = 60f;
        [Tooltip("Pick any floor in the column (roofs, storeys), not only the topmost surface.")]
        public bool AllowAnyFloorInColumn = true;
        public float MaxSlopeDegrees = 40f;
        public int Attempts = 40;

        [Header("Anton clearance")]
        public float ClearanceHeight = 1.9f;
        public float ClearanceRadius = 0.4f;
        public float GroundOffset = 0.15f;

        [Header("Randomness")]
        [Tooltip("0 = new spot every run. Any other value repeats the same spot.")]
        public int RandomSeed;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        private System.Random rng;

        public bool TryGetSpawnPosition(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            if (!UseRandomPosition)
            {
                return false;
            }

            // A fresh Random per call would repeat itself: several calls land in the same
            // TickCount. Keep one generator and only reseed when a fixed seed is requested.
            if (RandomSeed != 0)
            {
                rng = new System.Random(RandomSeed);
            }
            else if (rng == null)
            {
                rng = new System.Random(System.Guid.NewGuid().GetHashCode());
            }

            for (int attempt = 0; attempt < Mathf.Max(1, Attempts); attempt++)
            {
                Vector3 column = GetRandomColumn(rng);
                if (!TryFindFloor(column, rng, out Vector3 floor))
                {
                    continue;
                }

                position = floor + Vector3.up * GroundOffset;
                rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                return true;
            }

            Debug.LogWarning("[MiniVanAntonRandomPosTest] No free grounded spot found, using own transform.");
            return false;
        }

        private Vector3 GetRandomColumn(System.Random rng)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            // sqrt keeps the samples evenly spread over the disc instead of clustering in the middle.
            float t = Mathf.Sqrt((float)rng.NextDouble());
            float radius = Mathf.Lerp(Mathf.Min(MinRadius, MaxRadius), Mathf.Max(MinRadius, MaxRadius), t);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            return transform.position + offset;
        }

        private bool TryFindFloor(Vector3 column, System.Random rng, out Vector3 floor)
        {
            floor = column;
            Vector3 origin = column + Vector3.up * SearchHeightAbove;
            float distance = SearchHeightAbove + SearchDepthBelow;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, HitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count <= 0)
            {
                return false;
            }

            int candidates = 0;
            Vector3 chosen = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HitBuffer[i];
                if (Vector3.Angle(hit.normal, Vector3.up) > MaxSlopeDegrees || !HasClearance(hit.point))
                {
                    continue;
                }

                candidates++;
                if (!AllowAnyFloorInColumn)
                {
                    floor = hit.point;
                    return true;
                }

                // Reservoir sampling: one pass, uniform pick among all valid floors.
                if (rng.Next(candidates) == 0)
                {
                    chosen = hit.point;
                }
            }

            if (candidates == 0)
            {
                return false;
            }

            floor = chosen;
            return true;
        }

        private bool HasClearance(Vector3 point)
        {
            float radius = Mathf.Max(0.05f, ClearanceRadius);
            Vector3 bottom = point + Vector3.up * (radius + 0.05f);
            Vector3 top = point + Vector3.up * Mathf.Max(ClearanceHeight - radius, radius + 0.1f);
            return !Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            DrawDisc(Mathf.Max(MinRadius, MaxRadius));
            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.9f);
            DrawDisc(Mathf.Min(MinRadius, MaxRadius));
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.down * SearchDepthBelow, transform.position + Vector3.up * SearchHeightAbove);
        }

        private void DrawDisc(float radius)
        {
            const int segments = 64;
            Vector3 previous = transform.position + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
