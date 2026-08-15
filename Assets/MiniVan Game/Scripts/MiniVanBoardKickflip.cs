using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Spins board visual parts 360° around local forward (kickflip),
    /// and dips them on local Y during the flip.
    /// </summary>
    public sealed class MiniVanBoardKickflip
    {
        public float Duration = 0.58f;
        public Vector3 LocalSpinAxis = Vector3.forward;
        public float DipLocalY = 0.5f;

        private Transform[] parts = System.Array.Empty<Transform>();
        private Quaternion[] baseLocalRotations = System.Array.Empty<Quaternion>();
        private Vector3[] baseLocalPositions = System.Array.Empty<Vector3>();
        private float progress = -1f;

        public bool IsPlaying => progress >= 0f;

        public void Bind(params Transform[] visualParts)
        {
            int count = 0;
            if (visualParts != null)
            {
                for (int i = 0; i < visualParts.Length; i++)
                {
                    if (visualParts[i] != null)
                    {
                        count++;
                    }
                }
            }

            parts = new Transform[count];
            baseLocalRotations = new Quaternion[count];
            baseLocalPositions = new Vector3[count];
            int write = 0;
            if (visualParts != null)
            {
                for (int i = 0; i < visualParts.Length; i++)
                {
                    if (visualParts[i] == null)
                    {
                        continue;
                    }

                    parts[write] = visualParts[i];
                    baseLocalRotations[write] = visualParts[i].localRotation;
                    baseLocalPositions[write] = visualParts[i].localPosition;
                    write++;
                }
            }
        }

        public void CaptureBases()
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null)
                {
                    baseLocalRotations[i] = parts[i].localRotation;
                    baseLocalPositions[i] = parts[i].localPosition;
                }
            }
        }

        public void StartFlip()
        {
            if (parts.Length == 0)
            {
                return;
            }

            CaptureBases();
            progress = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (progress < 0f || parts.Length == 0)
            {
                return;
            }

            float duration = Mathf.Max(0.08f, Duration);
            progress += deltaTime / duration;
            float t = Mathf.Clamp01(progress);
            // Ease in-out so the flip reads clearly in the air.
            float eased = t * t * (3f - 2f * t);
            float angle = eased * 360f;
            Quaternion spin = Quaternion.AngleAxis(angle, LocalSpinAxis.sqrMagnitude > 0.001f
                ? LocalSpinAxis.normalized
                : Vector3.forward);

            // Dip quickly at the start, hold, then restore near the end.
            float dipWeight;
            if (t < 0.12f)
            {
                dipWeight = t / 0.12f;
            }
            else if (t > 0.88f)
            {
                dipWeight = (1f - t) / 0.12f;
            }
            else
            {
                dipWeight = 1f;
            }

            Vector3 dip = Vector3.down * (Mathf.Max(0f, DipLocalY) * dipWeight);

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                parts[i].localRotation = baseLocalRotations[i] * spin;
                parts[i].localPosition = baseLocalPositions[i] + dip;
            }

            if (progress >= 1f)
            {
                ResetToBases();
                progress = -1f;
            }
        }

        public void ForceFinish()
        {
            if (progress < 0f)
            {
                return;
            }

            ResetToBases();
            progress = -1f;
        }

        private void ResetToBases()
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                parts[i].localRotation = baseLocalRotations[i];
                parts[i].localPosition = baseLocalPositions[i];
            }
        }
    }
}
