using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class MiniVanElectrifiedCableSpline : MonoBehaviour
    {
        public bool AutoFindInsulators = true;
        public Transform StartPoint;
        public Transform[] OrderedInsulators;
        public string StartPointName = "GeneratorInsulator";
        public string InsulatorNamePrefix = "Insulator";
        public float CableWidth = 0.12f;
        public float SagPerMeter = 0.075f;
        public float MaxSag = 1.15f;
        public int SegmentsPerSpan = 12;
        public Material CableMaterial;

        private readonly List<Transform> cablePoints = new List<Transform>();
        private readonly List<Vector3> renderPoints = new List<Vector3>();
        private LineRenderer lineRenderer;
        private int lastHash;

        private void OnEnable()
        {
            EnsureRenderer();
            Rebuild(true);
        }

        private void OnValidate()
        {
            CableWidth = Mathf.Max(0.01f, CableWidth);
            SagPerMeter = Mathf.Max(0f, SagPerMeter);
            MaxSag = Mathf.Max(0f, MaxSag);
            SegmentsPerSpan = Mathf.Clamp(SegmentsPerSpan, 2, 48);
            EnsureRenderer();
            Rebuild(true);
        }

        private void LateUpdate()
        {
            Rebuild(false);
        }

        public void RebuildNow()
        {
            Rebuild(true);
        }

        private void EnsureRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.useWorldSpace = true;
            lineRenderer.widthMultiplier = CableWidth;
            lineRenderer.numCapVertices = 5;
            lineRenderer.numCornerVertices = 5;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            lineRenderer.receiveShadows = true;
            if (CableMaterial != null)
            {
                lineRenderer.sharedMaterial = CableMaterial;
            }
        }

        private void Rebuild(bool force)
        {
            EnsureRenderer();
            if (lineRenderer == null)
            {
                return;
            }

            CollectPoints();
            int hash = CalculatePointHash();
            if (!force && hash == lastHash)
            {
                return;
            }

            lastHash = hash;
            renderPoints.Clear();
            for (int i = 0; i < cablePoints.Count - 1; i++)
            {
                AppendSpan(cablePoints[i].position, cablePoints[i + 1].position, i == 0);
            }

            lineRenderer.positionCount = renderPoints.Count;
            if (renderPoints.Count > 0)
            {
                lineRenderer.SetPositions(renderPoints.ToArray());
            }
        }

        private void CollectPoints()
        {
            cablePoints.Clear();
            if (!AutoFindInsulators)
            {
                if (StartPoint != null)
                {
                    cablePoints.Add(StartPoint);
                }

                if (OrderedInsulators != null)
                {
                    for (int i = 0; i < OrderedInsulators.Length; i++)
                    {
                        if (OrderedInsulators[i] != null && !cablePoints.Contains(OrderedInsulators[i]))
                        {
                            cablePoints.Add(OrderedInsulators[i]);
                        }
                    }
                }

                return;
            }

            Transform root = transform.root;
            Transform autoStart = FindFirstByName(root, StartPointName);
            if (autoStart != null)
            {
                cablePoints.Add(autoStart);
            }

            List<Transform> candidates = new List<Transform>();
            CollectByPrefix(root, InsulatorNamePrefix, candidates);
            candidates.Remove(autoStart);
            Vector3 from = autoStart != null ? autoStart.position : transform.position;
            while (candidates.Count > 0)
            {
                int nearestIndex = 0;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float distance = Vector3.SqrMagnitude(candidates[i].position - from);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestIndex = i;
                    }
                }

                Transform next = candidates[nearestIndex];
                candidates.RemoveAt(nearestIndex);
                cablePoints.Add(next);
                from = next.position;
            }
        }

        private static Transform FindFirstByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == targetName)
                {
                    return children[i];
                }
            }

            return null;
        }

        private static void CollectByPrefix(Transform root, string prefix, List<Transform> results)
        {
            if (root == null || results == null || string.IsNullOrEmpty(prefix))
            {
                return;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal) && child.name != prefix)
                {
                    results.Add(child);
                }
            }
        }

        private int CalculatePointHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + cablePoints.Count;
                hash = hash * 31 + Mathf.RoundToInt(CableWidth * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(SagPerMeter * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(MaxSag * 1000f);
                hash = hash * 31 + SegmentsPerSpan;
                for (int i = 0; i < cablePoints.Count; i++)
                {
                    Vector3 position = cablePoints[i] != null ? cablePoints[i].position : Vector3.zero;
                    hash = hash * 31 + Mathf.RoundToInt(position.x * 100f);
                    hash = hash * 31 + Mathf.RoundToInt(position.y * 100f);
                    hash = hash * 31 + Mathf.RoundToInt(position.z * 100f);
                }

                return hash;
            }
        }

        private void AppendSpan(Vector3 a, Vector3 b, bool includeFirst)
        {
            float distance = Vector3.Distance(a, b);
            float sag = Mathf.Min(MaxSag, distance * SagPerMeter);
            int start = includeFirst ? 0 : 1;
            for (int i = start; i <= SegmentsPerSpan; i++)
            {
                float t = i / (float)SegmentsPerSpan;
                Vector3 point = Vector3.Lerp(a, b, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * sag;
                renderPoints.Add(point);
            }
        }
    }
}
