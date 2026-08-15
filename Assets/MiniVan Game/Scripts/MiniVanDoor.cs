using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanDoor : MonoBehaviour
    {
        public MiniVanVehicle Vehicle;
        public bool IsRoofDoor;
        public float InteractRadius = 2.25f;
        /// <summary>Tighter range for roof hatch — must stand near the lid.</summary>
        public float RoofInteractRadius = 1.75f;
        public float OpenAngle = -88f;
        public float AnimationSpeed = 10f;
        public Vector3 ParentHingeAxis = Vector3.up;
        public Vector3 LocalHingeOffset = new Vector3(0f, -0.5f, 0f);

        private Vector3 closedLocalPosition;
        private Quaternion closedLocalRotation;
        private bool capturedClosedPose;

        private Renderer[] highlightRenderers;
        private Material[][] originalMaterials;
        private Material outlineMaterial;
        private bool isHighlighted;

        private void Awake()
        {
            EnsureSetup();
            highlightRenderers = GetComponentsInChildren<Renderer>(true);
            CacheOriginalMaterials();
        }

        private void OnDisable()
        {
            SetHighlighted(false);
        }

        private void OnDestroy()
        {
            SetHighlighted(false);
            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
                outlineMaterial = null;
            }
        }

        private void Update()
        {
            if (Vehicle == null)
            {
                Vehicle = GetComponentInParent<MiniVanVehicle>();
            }

            if (!capturedClosedPose)
            {
                CaptureClosedPose();
            }

            bool open = Vehicle != null && (IsRoofDoor ? Vehicle.RoofDoorOpen.Value : Vehicle.SideDoorOpen.Value);
            GetTargetPose(open, out Vector3 targetPosition, out Quaternion targetRotation);
            float blend = 1f - Mathf.Exp(-AnimationSpeed * Time.deltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, blend);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, blend);
        }

        public void EnsureSetup()
        {
            if (Vehicle == null)
            {
                Vehicle = GetComponentInParent<MiniVanVehicle>();
            }

            if (!capturedClosedPose)
            {
                CaptureClosedPose();
            }

            Collider collider = GetComponent<Collider>();
            if (collider == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.size = Vector3.one;
                box.center = Vector3.zero;
            }
        }

        public bool IsInRange(Vector3 worldPosition)
        {
            float radius = IsRoofDoor ? Mathf.Max(0.5f, RoofInteractRadius) : InteractRadius;
            return Vector3.Distance(worldPosition, transform.position) <= radius;
        }

        public float GetInteractRadius()
        {
            return IsRoofDoor ? Mathf.Max(0.5f, RoofInteractRadius) : InteractRadius;
        }

        public void SetHighlighted(bool highlighted)
        {
            if (isHighlighted == highlighted)
            {
                return;
            }

            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                highlightRenderers = GetComponentsInChildren<Renderer>(true);
                CacheOriginalMaterials();
            }

            isHighlighted = highlighted;
            if (highlighted)
            {
                Material outline = GetOutlineMaterial();
                for (int i = 0; i < highlightRenderers.Length; i++)
                {
                    Renderer renderer = highlightRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    Material[] source = renderer.sharedMaterials;
                    Material[] materials = new Material[source.Length + 1];
                    for (int j = 0; j < source.Length; j++)
                    {
                        materials[j] = source[j];
                    }

                    materials[materials.Length - 1] = outline;
                    renderer.sharedMaterials = materials;
                }
            }
            else if (originalMaterials != null)
            {
                for (int i = 0; i < highlightRenderers.Length; i++)
                {
                    if (highlightRenderers[i] != null && i < originalMaterials.Length)
                    {
                        highlightRenderers[i].sharedMaterials = originalMaterials[i];
                    }
                }
            }
        }

        private void CaptureClosedPose()
        {
            closedLocalPosition = transform.localPosition;
            closedLocalRotation = transform.localRotation;
            capturedClosedPose = true;
        }

        private void GetTargetPose(bool open, out Vector3 targetPosition, out Quaternion targetRotation)
        {
            if (!open)
            {
                targetPosition = closedLocalPosition;
                targetRotation = closedLocalRotation;
                return;
            }

            Vector3 hingeAxis = ParentHingeAxis.sqrMagnitude > 0.001f ? ParentHingeAxis.normalized : Vector3.up;
            Quaternion openRotation = Quaternion.AngleAxis(OpenAngle, hingeAxis);
            Vector3 hingePosition = closedLocalPosition + closedLocalRotation * GetHingeOffsetInParentLocal();
            Vector3 centerFromHinge = closedLocalPosition - hingePosition;

            targetPosition = hingePosition + openRotation * centerFromHinge;
            targetRotation = openRotation * closedLocalRotation;
        }

        private Vector3 GetHingeOffsetInParentLocal()
        {
            Vector3 localOffset = LocalHingeOffset;
            if (TryGetLocalBounds(out Bounds localBounds))
            {
                localOffset = localBounds.center + Vector3.Scale(LocalHingeOffset, localBounds.size);
            }

            return Vector3.Scale(localOffset, transform.localScale);
        }

        private bool TryGetLocalBounds(out Bounds bounds)
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                bounds = new Bounds(box.center, box.size);
                return true;
            }

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                bounds = meshFilter.sharedMesh.bounds;
                return true;
            }

            bounds = default;
            return false;
        }

        private void CacheOriginalMaterials()
        {
            if (highlightRenderers == null)
            {
                return;
            }

            originalMaterials = new Material[highlightRenderers.Length][];
            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                originalMaterials[i] = highlightRenderers[i] != null
                    ? highlightRenderers[i].sharedMaterials
                    : System.Array.Empty<Material>();
            }
        }

        private Material GetOutlineMaterial()
        {
            if (outlineMaterial != null)
            {
                return outlineMaterial;
            }

            Material shared = Resources.Load<Material>("Panelka/ThinWhiteOutline");
            Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
            if (shared != null)
            {
                outlineMaterial = new Material(shared) { name = "MiniVan Door Outline" };
            }
            else
            {
                outlineMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
                {
                    name = "MiniVan Door Outline"
                };
            }

            MiniVanSnesOutline.ApplyOutlineSettings(outlineMaterial, Color.white);
            return outlineMaterial;
        }
    }
}
