using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanBridgeCableVisual : MonoBehaviour
    {
        public Transform StartPoint;
        public Transform EndPoint;
        public float Radius = 0.28f;
        [Range(4, 32)] public int SegmentCount = 16;
        public float SagPerMeter = 0.12f;
        public float MaxSag = 1.1f;
        public Material SegmentMaterial;
        public float SegmentOverlap = 0.08f;
        public bool FillBendsWithCaps = true;
        public bool UseContinuousTubeMesh = true;
        [Range(6, 18)] public int TubeSides = 10;

        [Header("Physical Cable")]
        public float PointColliderRadius = 0.14f;
        public float PointMass = 0.12f;
        public float EndMass = 0.9f;
        public float SpringForce = 200f;
        public float SpringDamper = 0.2f;
        public float RestLinkLength = 0.24f;
        public float MaxLinkStretch = 1.16f;

        [Tooltip("When true, physics points are frozen along the A-B line (no flopping into props).")]
        public bool SuspendPhysics;

        private readonly List<Rigidbody> points = new List<Rigidbody>(32);
        private readonly List<SpringJoint> joints = new List<SpringJoint>(32);
        private readonly List<Collider> pointColliders = new List<Collider>(32);
        private readonly List<Transform> segments = new List<Transform>(32);
        private readonly List<Transform> caps = new List<Transform>(34);

        private Transform rigRoot;
        private Mesh tubeMesh;
        private MeshFilter tubeFilter;
        private MeshRenderer tubeRenderer;
        private Renderer ownRenderer;
        private Rigidbody startBody;
        private Rigidbody endBody;
        private int cachedSegmentCount;
        private float nextPlayerCollisionRefreshTime;

        public int RuntimePointCount => points.Count;
        public int RuntimeSegmentCount => segments.Count;
        public float CableLengthLimit => Mathf.Max(0.05f, RestLinkLength) * (Mathf.Clamp(SegmentCount, 4, 32) + 1) * Mathf.Max(1f, MaxLinkStretch);

        private void Awake()
        {
            ownRenderer = GetComponent<Renderer>();
            if (SegmentMaterial == null && ownRenderer != null)
            {
                SegmentMaterial = ownRenderer.sharedMaterial;
            }
        }

        private void OnEnable()
        {
            EnsureRig();
        }

        private void OnDisable()
        {
            DestroyRig();
        }

        private void FixedUpdate()
        {
            EnsureRig();
            if (SuspendPhysics)
            {
                FreezePointsAlongLine();
                return;
            }

            RefreshJointDistances();
            RefreshPlayerCollisionIgnores(false);
        }

        private void LateUpdate()
        {
            if (ownRenderer != null)
            {
                ownRenderer.enabled = false;
            }

            EnsureRig();
            if (SuspendPhysics)
            {
                FreezePointsAlongLine();
            }

            UpdateSegments();
        }

        private void EnsureRig()
        {
            if (!Application.isPlaying || StartPoint == null || EndPoint == null)
            {
                return;
            }

            int count = Mathf.Clamp(SegmentCount, 4, 32);
            if (rigRoot != null && cachedSegmentCount == count)
            {
                return;
            }

            DestroyRig();
            cachedSegmentCount = count;
            startBody = EnsureEndpointBody(StartPoint);
            endBody = EnsureEndpointBody(EndPoint);

            GameObject rigObject = new GameObject("Generated Physical Cable Rig");
            rigObject.hideFlags = HideFlags.DontSave;
            rigRoot = rigObject.transform;
            rigRoot.SetParent(transform, true);

            Vector3 start = StartPoint.position;
            Vector3 end = EndPoint.position;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / (count + 1f);
                Rigidbody point = CreatePoint(i, GetInitialPoint(start, end, t));
                points.Add(point);
            }

            CreateJoints();

            if (UseContinuousTubeMesh)
            {
                CreateTubeMeshObject();
            }
            else
            {
                for (int i = 0; i < count + 1; i++)
                {
                    segments.Add(CreateSegment(i));
                }

                if (FillBendsWithCaps)
                {
                    for (int i = 0; i < count + 2; i++)
                    {
                        caps.Add(CreateCap(i));
                    }
                }
            }

            RefreshJointDistances();
            UpdateSegments();
            RefreshPlayerCollisionIgnores(true);
        }

        private Rigidbody EnsureEndpointBody(Transform endpoint)
        {
            Rigidbody body = endpoint.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = endpoint.gameObject.AddComponent<Rigidbody>();
                body.hideFlags = HideFlags.DontSave;
            }

            body.mass = EndMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return body;
        }

        private Rigidbody CreatePoint(int index, Vector3 position)
        {
            GameObject point = new GameObject("Cable Physics Point " + (index + 1).ToString("00"));
            point.hideFlags = HideFlags.DontSave;
            point.transform.SetParent(rigRoot, true);
            point.transform.position = position;

            SphereCollider collider = point.AddComponent<SphereCollider>();
            collider.radius = Mathf.Max(0.02f, PointColliderRadius);
            collider.isTrigger = false;
            pointColliders.Add(collider);

            Rigidbody body = point.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(0.01f, PointMass);
            body.useGravity = true;
            body.linearDamping = 0.35f;
            body.angularDamping = 0.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.solverVelocityIterations = 6;
            body.maxLinearVelocity = 80f;
            return body;
        }

        private void RefreshPlayerCollisionIgnores(bool force)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!force && Time.time < nextPlayerCollisionRefreshTime)
            {
                return;
            }

            nextPlayerCollisionRefreshTime = Time.time + 0.5f;
            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int p = 0; p < players.Length; p++)
            {
                MiniVanPlayer player = players[p];
                if (player == null)
                {
                    continue;
                }

                Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
                IgnoreAgainstPlayerColliders(playerColliders);
            }
        }

        private void IgnoreAgainstPlayerColliders(Collider[] playerColliders)
        {
            if (playerColliders == null || playerColliders.Length == 0)
            {
                return;
            }

            for (int c = 0; c < pointColliders.Count; c++)
            {
                IgnoreColliderAgainstPlayers(pointColliders[c], playerColliders);
            }

            IgnoreEndpointCollidersAgainstPlayers(StartPoint, playerColliders);
            IgnoreEndpointCollidersAgainstPlayers(EndPoint, playerColliders);
        }

        private static void IgnoreEndpointCollidersAgainstPlayers(Transform endpoint, Collider[] playerColliders)
        {
            if (endpoint == null)
            {
                return;
            }

            Collider[] endpointColliders = endpoint.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < endpointColliders.Length; i++)
            {
                IgnoreColliderAgainstPlayers(endpointColliders[i], playerColliders);
            }
        }

        private static void IgnoreColliderAgainstPlayers(Collider cableCollider, Collider[] playerColliders)
        {
            if (cableCollider == null)
            {
                return;
            }

            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider != null && playerCollider != cableCollider)
                {
                    Physics.IgnoreCollision(cableCollider, playerCollider, true);
                }
            }
        }

        private void CreateJoints()
        {
            Rigidbody previous = startBody;
            for (int i = 0; i < points.Count; i++)
            {
                joints.Add(CreateJoint(points[i], previous));
                previous = points[i];
            }

            if (points.Count > 0 && endBody != null)
            {
                joints.Add(CreateJoint(points[points.Count - 1], endBody));
            }
        }

        private SpringJoint CreateJoint(Rigidbody owner, Rigidbody connectedBody)
        {
            SpringJoint joint = owner.gameObject.AddComponent<SpringJoint>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = Vector3.zero;
            joint.spring = SpringForce;
            joint.damper = SpringDamper;
            joint.tolerance = 0.03f;
            joint.enableCollision = false;
            joint.breakForce = Mathf.Infinity;
            joint.hideFlags = HideFlags.DontSave;
            return joint;
        }

        private Transform CreateSegment(int index)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = "Cable Segment " + (index + 1).ToString("00");
            segment.hideFlags = HideFlags.DontSave;
            segment.transform.SetParent(rigRoot, true);

            Collider collider = segment.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyRuntime(collider);
            }

            Renderer renderer = segment.GetComponent<Renderer>();
            if (renderer != null && SegmentMaterial != null)
            {
                renderer.sharedMaterial = SegmentMaterial;
            }

            return segment.transform;
        }

        private void CreateTubeMeshObject()
        {
            GameObject tube = new GameObject("Cable Continuous Tube");
            tube.hideFlags = HideFlags.DontSave;
            tube.transform.SetParent(rigRoot, true);
            tube.transform.localPosition = Vector3.zero;
            tube.transform.localRotation = Quaternion.identity;
            tube.transform.localScale = Vector3.one;

            tubeFilter = tube.AddComponent<MeshFilter>();
            tubeRenderer = tube.AddComponent<MeshRenderer>();
            if (SegmentMaterial != null)
            {
                tubeRenderer.sharedMaterial = SegmentMaterial;
            }

            tubeMesh = new Mesh { name = "Generated Bridge Cable Tube" };
            tubeMesh.MarkDynamic();
            tubeFilter.sharedMesh = tubeMesh;
        }

        private Transform CreateCap(int index)
        {
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Cable Bend Cap " + (index + 1).ToString("00");
            cap.hideFlags = HideFlags.DontSave;
            cap.transform.SetParent(rigRoot, true);

            Collider collider = cap.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyRuntime(collider);
            }

            Renderer renderer = cap.GetComponent<Renderer>();
            if (renderer != null && SegmentMaterial != null)
            {
                renderer.sharedMaterial = SegmentMaterial;
            }

            return cap.transform;
        }

        private void RefreshJointDistances()
        {
            if (StartPoint == null || EndPoint == null || joints.Count == 0)
            {
                return;
            }

            float linkLength = Mathf.Max(0.05f, RestLinkLength);
            float maxLength = linkLength * Mathf.Max(1f, MaxLinkStretch);
            for (int i = 0; i < joints.Count; i++)
            {
                SpringJoint joint = joints[i];
                if (joint == null)
                {
                    continue;
                }

                joint.spring = SpringForce;
                joint.damper = SpringDamper;
                joint.minDistance = linkLength;
                joint.maxDistance = maxLength;
            }
        }

        private void UpdateSegments()
        {
            if (StartPoint == null || EndPoint == null || points.Count == 0)
            {
                return;
            }

            if (UseContinuousTubeMesh)
            {
                UpdateTubeMesh();
                return;
            }

            if (segments.Count == 0)
            {
                return;
            }

            Vector3 previous = StartPoint.position;
            int count = Mathf.Min(segments.Count, points.Count + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 next = i < points.Count && points[i] != null ? points[i].position : EndPoint.position;
                UpdateSegment(segments[i], previous, next);
                previous = next;
            }

            UpdateCaps();
        }

        private void UpdateTubeMesh()
        {
            if (tubeMesh == null || tubeFilter == null)
            {
                return;
            }

            int anchorCount = points.Count + 2;
            int sides = Mathf.Clamp(TubeSides, 6, 18);
            Vector3[] vertices = new Vector3[anchorCount * sides];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[(anchorCount - 1) * sides * 6];

            float visualRadius = Mathf.Max(0.01f, Radius * 0.5f);
            float accumulatedLength = 0f;
            Vector3 previousAnchor = GetAnchorPoint(0);
            Transform meshTransform = tubeFilter.transform;

            for (int ring = 0; ring < anchorCount; ring++)
            {
                Vector3 anchor = GetAnchorPoint(ring);
                if (ring > 0)
                {
                    accumulatedLength += Vector3.Distance(previousAnchor, anchor);
                }

                Vector3 tangent = GetRingTangent(ring, anchorCount);
                Quaternion frame = Quaternion.LookRotation(tangent, GetFrameUp(tangent));
                for (int side = 0; side < sides; side++)
                {
                    float angle = side / (float)sides * Mathf.PI * 2f;
                    Vector3 normal = frame * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    int index = ring * sides + side;
                    vertices[index] = meshTransform.InverseTransformPoint(anchor + normal * visualRadius);
                    normals[index] = meshTransform.InverseTransformDirection(normal);
                    uvs[index] = new Vector2(side / (float)sides, accumulatedLength);
                }

                previousAnchor = anchor;
            }

            int triangle = 0;
            for (int ring = 0; ring < anchorCount - 1; ring++)
            {
                int ringStart = ring * sides;
                int nextStart = (ring + 1) * sides;
                for (int side = 0; side < sides; side++)
                {
                    int nextSide = (side + 1) % sides;
                    triangles[triangle++] = ringStart + side;
                    triangles[triangle++] = nextStart + side;
                    triangles[triangle++] = ringStart + nextSide;
                    triangles[triangle++] = ringStart + nextSide;
                    triangles[triangle++] = nextStart + side;
                    triangles[triangle++] = nextStart + nextSide;
                }
            }

            tubeMesh.Clear();
            tubeMesh.vertices = vertices;
            tubeMesh.normals = normals;
            tubeMesh.uv = uvs;
            tubeMesh.triangles = triangles;
            tubeMesh.RecalculateBounds();
        }

        private Vector3 GetAnchorPoint(int index)
        {
            if (index <= 0)
            {
                return StartPoint.position;
            }

            int pointIndex = index - 1;
            if (pointIndex < points.Count && points[pointIndex] != null)
            {
                return points[pointIndex].position;
            }

            return EndPoint.position;
        }

        private Vector3 GetRingTangent(int index, int anchorCount)
        {
            Vector3 previous = GetAnchorPoint(Mathf.Max(0, index - 1));
            Vector3 next = GetAnchorPoint(Mathf.Min(anchorCount - 1, index + 1));
            Vector3 tangent = next - previous;
            return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
        }

        private static Vector3 GetFrameUp(Vector3 tangent)
        {
            return Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.92f ? Vector3.right : Vector3.up;
        }

        private Vector3 GetInitialPoint(Vector3 start, Vector3 end, float t)
        {
            Vector3 point = Vector3.Lerp(start, end, t);
            float sag = Mathf.Min(MaxSag, Vector3.Distance(start, end) * SagPerMeter);
            return point + Vector3.down * (Mathf.Sin(t * Mathf.PI) * sag);
        }

        /// <summary>
        /// Snaps all physics points back onto the straight line between the two cable ends and
        /// zeroes their velocities. Removes any stale sprawled rope shape after teleporting ends.
        /// </summary>
        public void RedrapeBetweenEnds()
        {
            FreezePointsAlongLine();
        }

        private void FreezePointsAlongLine()
        {
            if (StartPoint == null || EndPoint == null)
            {
                return;
            }

            EnsureRig();
            Vector3 start = StartPoint.position;
            Vector3 end = EndPoint.position;
            for (int i = 0; i < points.Count; i++)
            {
                Rigidbody point = points[i];
                if (point == null)
                {
                    continue;
                }

                float t = (i + 1f) / (points.Count + 1f);
                Vector3 target = GetInitialPoint(start, end, t);
                point.isKinematic = true;
                point.useGravity = false;
                point.detectCollisions = false;
                point.transform.position = target;
                point.position = target;
            }

            UpdateSegments();
        }

        public void ResumePointPhysics()
        {
            for (int i = 0; i < points.Count; i++)
            {
                Rigidbody point = points[i];
                if (point == null)
                {
                    continue;
                }

                point.isKinematic = false;
                point.useGravity = true;
                point.detectCollisions = true;
            }
        }

        private void UpdateSegment(Transform segment, Vector3 start, Vector3 end)
        {
            if (segment == null)
            {
                return;
            }

            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                segment.gameObject.SetActive(false);
                return;
            }

            segment.gameObject.SetActive(true);
            segment.position = (start + end) * 0.5f;
            segment.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            float overlappedLength = length + Mathf.Max(0f, SegmentOverlap);
            segment.localScale = new Vector3(Radius, overlappedLength * 0.5f, Radius);
        }

        private void UpdateCaps()
        {
            if (!FillBendsWithCaps || caps.Count == 0)
            {
                return;
            }

            int capIndex = 0;
            UpdateCap(capIndex++, StartPoint.position);
            for (int i = 0; i < points.Count && capIndex < caps.Count; i++)
            {
                if (points[i] != null)
                {
                    UpdateCap(capIndex++, points[i].position);
                }
            }

            if (capIndex < caps.Count)
            {
                UpdateCap(capIndex, EndPoint.position);
            }
        }

        private void UpdateCap(int index, Vector3 position)
        {
            Transform cap = caps[index];
            if (cap == null)
            {
                return;
            }

            cap.position = position;
            cap.rotation = Quaternion.identity;
            cap.localScale = Vector3.one * Radius;
        }

        private void DestroyRig()
        {
            for (int i = 0; i < joints.Count; i++)
            {
                if (joints[i] != null)
                {
                    DestroyRuntime(joints[i]);
                }
            }

            joints.Clear();
            points.Clear();
            pointColliders.Clear();
            segments.Clear();
            caps.Clear();
            tubeFilter = null;
            tubeRenderer = null;

            if (tubeMesh != null)
            {
                DestroyRuntime(tubeMesh);
                tubeMesh = null;
            }

            if (rigRoot != null)
            {
                DestroyRuntime(rigRoot.gameObject);
                rigRoot = null;
            }
        }

        private static void DestroyRuntime(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
