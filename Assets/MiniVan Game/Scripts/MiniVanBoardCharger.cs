using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniVanGame
{
    /// <summary>
    /// Roof-mounted hoverboard charger with 4 independent slots.
    /// Always powered (solar panels later). Full charge takes ChargeSecondsToFull.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanBoardCharger : MonoBehaviour
    {
        public const int SlotCount = 4;

        [Min(60f)] public float ChargeSecondsToFull = 900f;
        public float InteractRadius = 2.6f;
        [Tooltip("Local offset from slot anchor applied to docked boards (lower Y = sits deeper in cradle).")]
        public Vector3 DockedBoardLocalOffset = new Vector3(0f, -0.1f, 0f);
        public float GhostAimMaxRayDistance = 0.85f;
        public Transform[] SlotAnchors = new Transform[SlotCount];
        public Renderer[] SlotLights = new Renderer[SlotCount];
        public Material LightChargedMaterial;
        public Material LightChargingMaterial;

        private const float OccupancyRefreshInterval = 0.5f;

        private readonly MiniVanHoverboardM[] dockedBoards = new MiniVanHoverboardM[SlotCount];
        private static Material ghostGreenMaterial;
        private GameObject placementGhost;
        private Renderer[] ghostRenderers;
        private MiniVanHoverboardM ghostSourceBoard;
        private int ghostSlotIndex = -1;
        private float nextOccupancyRefreshTime;

        public Vector3 InteractCenter => transform.position + transform.up * 0.25f;
        public int GhostSlotIndex => ghostSlotIndex;

        public bool IsInRange(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, InteractCenter) <= InteractRadius;
        }

        public static MiniVanBoardCharger FindNearest(Vector3 position, float maxDistance)
        {
            MiniVanBoardCharger[] chargers = MiniVanSceneScan.Get<MiniVanBoardCharger>();
            MiniVanBoardCharger best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < chargers.Length; i++)
            {
                MiniVanBoardCharger charger = chargers[i];
                if (charger == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, charger.InteractCenter);
                if (distance < bestDistance && distance <= maxDistance)
                {
                    best = charger;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void OnDisable()
        {
            HidePlacementGhost();
        }

        private void OnDestroy()
        {
            DestroyPlacementGhost();
        }

        private void LateUpdate()
        {
            if (IsServerAuthority())
            {
                TickCharging(Time.deltaTime);
            }

            RefreshSlotLights();
        }

        public bool TryFindEmptySlot(out int slotIndex)
        {
            RefreshDockOccupancy();
            for (int i = 0; i < SlotCount; i++)
            {
                if (IsSlotEmpty(i))
                {
                    slotIndex = i;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        public bool IsSlotEmpty(int slotIndex)
        {
            RefreshDockOccupancy();
            return slotIndex >= 0 &&
                   slotIndex < SlotCount &&
                   dockedBoards[slotIndex] == null &&
                   SlotAnchors != null &&
                   slotIndex < SlotAnchors.Length &&
                   SlotAnchors[slotIndex] != null;
        }

        public bool TryGetSlotAnchor(int slotIndex, out Transform anchor)
        {
            anchor = null;
            if (SlotAnchors == null || slotIndex < 0 || slotIndex >= SlotAnchors.Length)
            {
                return false;
            }

            anchor = SlotAnchors[slotIndex];
            return anchor != null;
        }

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (!TryGetSlotAnchor(slotIndex, out Transform anchor) || anchor == null)
            {
                return InteractCenter;
            }

            return anchor.position;
        }

        public Quaternion GetSlotWorldRotation(int slotIndex)
        {
            if (!TryGetSlotAnchor(slotIndex, out Transform anchor) || anchor == null)
            {
                return transform.rotation;
            }

            return anchor.rotation;
        }

        public void GetDockWorldPose(int slotIndex, out Vector3 position, out Quaternion rotation)
        {
            if (!TryGetSlotAnchor(slotIndex, out Transform anchor) || anchor == null)
            {
                position = InteractCenter;
                rotation = transform.rotation;
                return;
            }

            position = anchor.TransformPoint(DockedBoardLocalOffset);
            rotation = anchor.rotation;
        }

        public bool TryFindAimedEmptySlot(Ray lookRay, out int slotIndex)
        {
            RefreshDockOccupancy();
            slotIndex = -1;
            float bestScore = float.MaxValue;
            bool found = false;

            for (int i = 0; i < SlotCount; i++)
            {
                if (!IsSlotEmpty(i))
                {
                    continue;
                }

                GetDockWorldPose(i, out Vector3 slotPosition, out _);
                Vector3 toSlot = slotPosition - lookRay.origin;
                float along = Vector3.Dot(toSlot, lookRay.direction);
                if (along < 0.2f || along > 12f)
                {
                    continue;
                }

                float rayDistance = Vector3.Cross(lookRay.direction, toSlot).magnitude;
                if (rayDistance > GhostAimMaxRayDistance)
                {
                    continue;
                }

                float score = rayDistance * 2.5f + along * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    slotIndex = i;
                    found = true;
                }
            }

            if (found)
            {
                return true;
            }

            // Fallback: nearest empty slot to look-ray hit on charger / charger center.
            Vector3 aimPoint = InteractCenter;
            if (Physics.Raycast(lookRay, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Collide) &&
                hit.collider != null &&
                hit.collider.transform.IsChildOf(transform))
            {
                aimPoint = hit.point;
            }
            else
            {
                aimPoint = lookRay.origin + lookRay.direction * 3f;
            }

            return TryFindNearestEmptySlot(aimPoint, out slotIndex);
        }

        public bool TryFindNearestEmptySlot(Vector3 worldPoint, out int slotIndex)
        {
            RefreshDockOccupancy();
            slotIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!IsSlotEmpty(i))
                {
                    continue;
                }

                float distance = Vector3.Distance(worldPoint, GetSlotWorldPosition(i));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    slotIndex = i;
                }
            }

            return slotIndex >= 0;
        }

        public void UpdatePlacementGhost(MiniVanHoverboardM sourceBoard, Camera camera)
        {
            if (sourceBoard == null || camera == null)
            {
                HidePlacementGhost();
                return;
            }

            if (!IsInRange(camera.transform.position) && !IsInRange(sourceBoard.transform.position))
            {
                HidePlacementGhost();
                return;
            }

            Ray lookRay = new Ray(camera.transform.position, camera.transform.forward);
            if (!TryFindAimedEmptySlot(lookRay, out int slotIndex))
            {
                HidePlacementGhost();
                return;
            }

            EnsurePlacementGhost(sourceBoard);
            if (placementGhost == null)
            {
                return;
            }

            GetDockWorldPose(slotIndex, out Vector3 position, out Quaternion rotation);
            placementGhost.transform.SetPositionAndRotation(position, rotation);
            placementGhost.SetActive(true);
            ghostSlotIndex = slotIndex;
        }

        public void HidePlacementGhost()
        {
            ghostSlotIndex = -1;
            if (placementGhost != null)
            {
                placementGhost.SetActive(false);
            }
        }

        public void RegisterDockedBoard(int slotIndex, MiniVanHoverboardM board)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                return;
            }

            dockedBoards[slotIndex] = board;
            RefreshSlotLights();
        }

        public void UnregisterDockedBoard(int slotIndex, MiniVanHoverboardM board)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                return;
            }

            if (dockedBoards[slotIndex] == board)
            {
                dockedBoards[slotIndex] = null;
            }

            RefreshSlotLights();
        }

        public MiniVanHoverboardM FindNearestDockedBoard(Vector3 lookPoint)
        {
            RefreshDockOccupancy();
            MiniVanHoverboardM best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < SlotCount; i++)
            {
                MiniVanHoverboardM board = dockedBoards[i];
                if (board == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(lookPoint, board.transform.position);
                if (distance < bestDistance)
                {
                    best = board;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public bool HasEmptySlot()
        {
            return TryFindEmptySlot(out _);
        }

        private void TickCharging(float deltaTime)
        {
            float seconds = Mathf.Max(60f, ChargeSecondsToFull);
            float step = deltaTime / seconds;
            for (int i = 0; i < SlotCount; i++)
            {
                MiniVanHoverboardM board = dockedBoards[i];
                if (board == null || !board.IsOnCharger.Value)
                {
                    continue;
                }

                board.ServerApplyCharge(step);
            }
        }

        private void RefreshDockOccupancy()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                MiniVanHoverboardM board = dockedBoards[i];
                if (board != null && (!board.IsOnCharger.Value || board.ChargerSlotIndex != i || board.BoundCharger != this))
                {
                    dockedBoards[i] = null;
                }
            }

            // Full scene scan is expensive; throttle it. Register/Unregister
            // keep the slots correct between scans.
            if (Time.unscaledTime < nextOccupancyRefreshTime)
            {
                return;
            }

            nextOccupancyRefreshTime = Time.unscaledTime + OccupancyRefreshInterval;

            MiniVanHoverboardM[] boards = MiniVanSceneScan.Get<MiniVanHoverboardM>();
            for (int i = 0; i < boards.Length; i++)
            {
                MiniVanHoverboardM board = boards[i];
                if (board == null || !board.IsOnCharger.Value || board.BoundCharger != this)
                {
                    continue;
                }

                int slot = board.ChargerSlotIndex;
                if (slot >= 0 && slot < SlotCount)
                {
                    dockedBoards[slot] = board;
                }
            }
        }

        public void RefreshSlotLights()
        {
            RefreshDockOccupancy();
            for (int i = 0; i < SlotCount; i++)
            {
                if (SlotLights == null || i >= SlotLights.Length || SlotLights[i] == null)
                {
                    continue;
                }

                MiniVanHoverboardM board = dockedBoards[i];
                bool charged = board != null && board.IsFullyCharged;
                Material material = charged ? LightChargedMaterial : LightChargingMaterial;
                if (material != null)
                {
                    SlotLights[i].sharedMaterial = material;
                }
            }
        }

        private void EnsurePlacementGhost(MiniVanHoverboardM sourceBoard)
        {
            if (placementGhost != null && ghostSourceBoard == sourceBoard)
            {
                return;
            }

            DestroyPlacementGhost();
            if (sourceBoard == null)
            {
                return;
            }

            ghostSourceBoard = sourceBoard;
            placementGhost = new GameObject("BoardCharger_PlacementGhost");
            placementGhost.hideFlags = HideFlags.HideAndDontSave;

            MeshFilter[] filters = sourceBoard.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                // Skip tiny UI/display text meshes.
                if (filter.GetComponent<TextMesh>() != null)
                {
                    continue;
                }

                GameObject part = new GameObject(filter.name + "_Ghost");
                part.transform.SetParent(placementGhost.transform, false);
                part.transform.localPosition = sourceBoard.transform.InverseTransformPoint(filter.transform.position);
                part.transform.localRotation = Quaternion.Inverse(sourceBoard.transform.rotation) * filter.transform.rotation;
                part.transform.localScale = SafeLossyScale(sourceBoard.transform, filter.transform);

                MeshFilter ghostFilter = part.AddComponent<MeshFilter>();
                ghostFilter.sharedMesh = filter.sharedMesh;
                MeshRenderer ghostRenderer = part.AddComponent<MeshRenderer>();
                ghostRenderer.sharedMaterial = GetGhostGreenMaterial();
                ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;
            }

            ghostRenderers = placementGhost.GetComponentsInChildren<Renderer>(true);
            placementGhost.SetActive(false);
        }

        private void DestroyPlacementGhost()
        {
            if (placementGhost != null)
            {
                Destroy(placementGhost);
            }

            placementGhost = null;
            ghostRenderers = null;
            ghostSourceBoard = null;
            ghostSlotIndex = -1;
        }

        private static Vector3 SafeLossyScale(Transform root, Transform child)
        {
            Vector3 rootScale = root.lossyScale;
            Vector3 childScale = child.lossyScale;
            return new Vector3(
                Mathf.Abs(rootScale.x) > 0.0001f ? childScale.x / rootScale.x : childScale.x,
                Mathf.Abs(rootScale.y) > 0.0001f ? childScale.y / rootScale.y : childScale.y,
                Mathf.Abs(rootScale.z) > 0.0001f ? childScale.z / rootScale.z : childScale.z);
        }

        private static Material GetGhostGreenMaterial()
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

            Color color = new Color(0.15f, 1f, 0.3f, 0.45f);
            ghostGreenMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color,
                renderQueue = 3000
            };

            if (ghostGreenMaterial.HasProperty("_BaseColor"))
            {
                ghostGreenMaterial.SetColor("_BaseColor", color);
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
                ghostGreenMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (ghostGreenMaterial.HasProperty("_DstBlend"))
            {
                ghostGreenMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            ghostGreenMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ghostGreenMaterial.SetOverrideTag("RenderType", "Transparent");
            return ghostGreenMaterial;
        }

        private static bool IsServerAuthority()
        {
            return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        }
    }
}
