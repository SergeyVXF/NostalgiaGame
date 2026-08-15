using UnityEngine;
using UnityEngine.Rendering;

namespace MiniVanGame
{
    /// <summary>
    /// Roof cargo socket (RoofAttach_01). For now accepts only a carried wheel:
    /// look → green ghost silhouette of final pose + prompt, E → place or take.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanRoofAttachPoint : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public const string DefaultObjectName = "RoofAttach_01";

        [Min(0.5f)] public float InteractionReach = 2.6f;
        public Transform SnapPoint;
        public Vector3 OccupiedLocalEuler = new Vector3(-90f, 0f, 0f);
        public Vector3 OccupiedLocalPosition = Vector3.zero;
        public MiniVanDetachedWheel OccupiedWheel;

        private static Material ghostGreenMaterial;

        private MiniVanDetachedWheel highlightedWheel;
        private bool isHighlighted;
        private GameObject placementGhost;
        private Renderer[] ghostRenderers;
        private MiniVanDetachedWheel ghostSourceWheel;

        public bool HasWheel => OccupiedWheel != null && OccupiedWheel.IsOnRoofAttach;

        private void Awake()
        {
            EnsureCollider();
            EnsureNeutralSnapPoint();
        }

        private void OnDisable()
        {
            SetHighlighted(false);
            HidePlacementGhost();
        }

        private void OnDestroy()
        {
            SetHighlighted(false);
            DestroyPlacementGhost();
        }

        private void Update()
        {
            if (OccupiedWheel != null && !OccupiedWheel.IsOnRoofAttach)
            {
                OccupiedWheel = null;
            }

            MiniVanPlayer player = MiniVanPlayer.LocalPlayer;
            bool showPlaceGhost = isHighlighted && !HasWheel &&
                                  MiniVanDetachedWheel.GetCarriedBy(player) != null &&
                                  IsInReach(player);

            if (showPlaceGhost)
            {
                UpdatePlacementGhost(MiniVanDetachedWheel.GetCarriedBy(player));
            }
            else
            {
                HidePlacementGhost();
            }

            if (isHighlighted)
            {
                MiniVanDetachedWheel target = ResolveHighlightWheel(player);
                if (target != highlightedWheel)
                {
                    if (highlightedWheel != null)
                    {
                        highlightedWheel.SetPlacementHighlighted(false);
                    }

                    highlightedWheel = target;
                    if (highlightedWheel != null)
                    {
                        highlightedWheel.SetPlacementHighlighted(true);
                    }
                }
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (!IsInReach(player))
            {
                return string.Empty;
            }

            MiniVanDetachedWheel carried = MiniVanDetachedWheel.GetCarriedBy(player);
            if (carried != null && !HasWheel)
            {
                return "E - put wheel";
            }

            if (HasWheel && carried == null)
            {
                return "E - take wheel";
            }

            return string.Empty;
        }

        public bool UsesGreenPrompt => true;

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1) || !IsInReach(player))
            {
                return;
            }

            MiniVanDetachedWheel carried = MiniVanDetachedWheel.GetCarriedBy(player);
            if (carried != null && !HasWheel)
            {
                TryPlaceWheel(carried);
                return;
            }

            if (HasWheel && carried == null)
            {
                OccupiedWheel.TryPickup(player);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool TryPlaceWheel(MiniVanDetachedWheel wheel)
        {
            if (wheel == null || HasWheel)
            {
                return false;
            }

            HidePlacementGhost();
            OccupiedWheel = wheel;
            wheel.PlaceOnRoofAttach(this);
            // Belt-and-suspenders: snap pose again after ghost/highlight teardown.
            if (OccupiedWheel != null && OccupiedWheel.IsOnRoofAttach)
            {
                Transform snap = GetSnapTransform();
                OccupiedWheel.transform.SetParent(snap, false);
                OccupiedWheel.transform.localPosition = Vector3.zero;
                OccupiedWheel.transform.localRotation = Quaternion.identity;
            }

            return true;
        }

        public void NotifyWheelRemoved(MiniVanDetachedWheel wheel)
        {
            if (OccupiedWheel == wheel)
            {
                OccupiedWheel = null;
            }
        }

        /// <summary>
        /// Prefer roof rack while carrying a wheel even if the crosshair hits the van body.
        /// </summary>
        public static MiniVanRoofAttachPoint FindBestPlaceTarget(MiniVanPlayer player, MiniVanDetachedWheel wheel)
        {
            if (player == null || wheel == null)
            {
                return null;
            }

            MiniVanRoofAttachPoint[] points = FindObjectsByType<MiniVanRoofAttachPoint>(FindObjectsSortMode.None);
            MiniVanRoofAttachPoint best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                MiniVanRoofAttachPoint point = points[i];
                if (point == null || point.HasWheel || !point.IsInReach(player))
                {
                    continue;
                }

                float distance = Vector3.Distance(player.transform.position, point.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }

            return best;
        }

        /// <summary>
        /// Green outline on the wheel object; placement ghost shows final pose on the socket.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (!highlighted)
            {
                isHighlighted = false;
                if (highlightedWheel != null)
                {
                    highlightedWheel.SetPlacementHighlighted(false);
                    highlightedWheel = null;
                }

                HidePlacementGhost();
                return;
            }

            isHighlighted = true;
            MiniVanDetachedWheel target = ResolveHighlightWheel(MiniVanPlayer.LocalPlayer);
            if (highlightedWheel != null && highlightedWheel != target)
            {
                highlightedWheel.SetPlacementHighlighted(false);
            }

            highlightedWheel = target;
            if (highlightedWheel != null)
            {
                highlightedWheel.SetPlacementHighlighted(true);
            }
        }

        public Transform GetSnapTransform()
        {
            EnsureNeutralSnapPoint();
            return SnapPoint != null ? SnapPoint : transform;
        }

        private MiniVanDetachedWheel ResolveHighlightWheel(MiniVanPlayer player)
        {
            if (HasWheel)
            {
                return OccupiedWheel;
            }

            return MiniVanDetachedWheel.GetCarriedBy(player);
        }

        public bool IsInReach(MiniVanPlayer player)
        {
            return player != null &&
                   !player.IsDowned &&
                   Vector3.Distance(player.transform.position, transform.position) <= InteractionReach;
        }

        private void UpdatePlacementGhost(MiniVanDetachedWheel sourceWheel)
        {
            if (sourceWheel == null)
            {
                HidePlacementGhost();
                return;
            }

            if (placementGhost == null || ghostSourceWheel != sourceWheel)
            {
                DestroyPlacementGhost();
                BuildPlacementGhost(sourceWheel);
            }

            if (placementGhost == null)
            {
                return;
            }

            Transform snap = GetSnapTransform();
            placementGhost.transform.SetPositionAndRotation(snap.position, snap.rotation);
            placementGhost.transform.localScale = sourceWheel.transform.localScale;
            placementGhost.SetActive(true);
        }

        private void BuildPlacementGhost(MiniVanDetachedWheel sourceWheel)
        {
            Transform snap = GetSnapTransform();
            Material ghostMat = GetGhostMaterial();

            ghostSourceWheel = sourceWheel;
            placementGhost = new GameObject("RoofAttach_WheelGhost");
            placementGhost.hideFlags = HideFlags.HideAndDontSave;
            placementGhost.transform.SetPositionAndRotation(snap.position, snap.rotation);

            MeshFilter[] sourceFilters = sourceWheel.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < sourceFilters.Length; i++)
            {
                MeshFilter filter = sourceFilters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                GameObject part = new GameObject(filter.gameObject.name + "_Ghost");
                part.transform.SetParent(placementGhost.transform, false);

                // Relative pose as if source wheel were already parented to snap.
                Matrix4x4 relative = sourceWheel.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                part.transform.localPosition = relative.GetColumn(3);
                part.transform.localRotation = relative.rotation;
                Vector3 lossy = filter.transform.lossyScale;
                Vector3 rootLossy = sourceWheel.transform.lossyScale;
                part.transform.localScale = new Vector3(
                    SafeDiv(lossy.x, rootLossy.x),
                    SafeDiv(lossy.y, rootLossy.y),
                    SafeDiv(lossy.z, rootLossy.z));

                MeshFilter ghostFilter = part.AddComponent<MeshFilter>();
                ghostFilter.sharedMesh = filter.sharedMesh;
                MeshRenderer ghostRenderer = part.AddComponent<MeshRenderer>();
                ghostRenderer.sharedMaterial = ghostMat;
                ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;
            }

            if (placementGhost.transform.childCount == 0)
            {
                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = "WheelGhostFallback";
                cyl.transform.SetParent(placementGhost.transform, false);
                cyl.transform.localScale = new Vector3(1f, 0.21f, 1f);
                Object.DestroyImmediate(cyl.GetComponent<Collider>());
                MeshRenderer renderer = cyl.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = ghostMat;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            ghostRenderers = placementGhost.GetComponentsInChildren<Renderer>(true);
            placementGhost.SetActive(false);
        }

        private void HidePlacementGhost()
        {
            if (placementGhost != null)
            {
                placementGhost.SetActive(false);
            }
        }

        private void DestroyPlacementGhost()
        {
            if (placementGhost != null)
            {
                Destroy(placementGhost);
                placementGhost = null;
                ghostRenderers = null;
                ghostSourceWheel = null;
            }
        }

        private static Material GetGhostMaterial()
        {
            if (ghostGreenMaterial != null)
            {
                return ghostGreenMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            ghostGreenMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "RoofAttach Wheel Ghost Green",
                renderQueue = 3000
            };

            Color color = new Color(0.15f, 1f, 0.3f, 0.42f);
            if (ghostGreenMaterial.HasProperty("_BaseColor"))
            {
                ghostGreenMaterial.SetColor("_BaseColor", color);
            }

            if (ghostGreenMaterial.HasProperty("_Color"))
            {
                ghostGreenMaterial.SetColor("_Color", color);
            }

            if (ghostGreenMaterial.HasProperty("_Surface"))
            {
                ghostGreenMaterial.SetFloat("_Surface", 1f);
            }

            if (ghostGreenMaterial.HasProperty("_ZWrite"))
            {
                ghostGreenMaterial.SetFloat("_ZWrite", 0f);
            }

            if (ghostGreenMaterial.HasProperty("_SrcBlend"))
            {
                ghostGreenMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (ghostGreenMaterial.HasProperty("_DstBlend"))
            {
                ghostGreenMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            ghostGreenMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ghostGreenMaterial.SetOverrideTag("RenderType", "Transparent");
            return ghostGreenMaterial;
        }

        private void EnsureCollider()
        {
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 0.2f, 0f);
                box.size = new Vector3(1.3f, 0.7f, 1.3f);
                return;
            }

            if (col is BoxCollider boxCol)
            {
                boxCol.isTrigger = true;
            }
        }

        private void EnsureNeutralSnapPoint()
        {
            if (SnapPoint == null)
            {
                Transform existing = transform.Find("SnapPoint");
                if (existing != null)
                {
                    SnapPoint = existing;
                }
                else
                {
                    GameObject snap = new GameObject("SnapPoint");
                    snap.transform.SetParent(transform, false);
                    SnapPoint = snap.transform;
                }
            }

            if (SnapPoint.parent != transform)
            {
                SnapPoint.SetParent(transform, true);
            }

            SnapPoint.localPosition = OccupiedLocalPosition;
            SnapPoint.localRotation = Quaternion.Euler(OccupiedLocalEuler);
            SnapPoint.localScale = Vector3.one;

            Vector3 parentLossy = transform.lossyScale;
            if (!ApproximatelyOne(parentLossy))
            {
                SnapPoint.localScale = new Vector3(
                    SafeDiv(1f, parentLossy.x),
                    SafeDiv(1f, parentLossy.y),
                    SafeDiv(1f, parentLossy.z));
            }
        }

        private static bool ApproximatelyOne(Vector3 scale)
        {
            return Mathf.Abs(scale.x - 1f) < 0.001f &&
                   Mathf.Abs(scale.y - 1f) < 0.001f &&
                   Mathf.Abs(scale.z - 1f) < 0.001f;
        }

        private static float SafeDiv(float value, float divisor)
        {
            float abs = Mathf.Abs(divisor);
            return abs < 1e-4f ? value : value / divisor;
        }
    }
}
