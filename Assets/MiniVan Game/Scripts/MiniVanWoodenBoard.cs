using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniVanGame
{
    /// <summary>
    /// Long wooden board. World-carry only (never inventory):
    /// E pick up, green ghost while aiming, E place with physics so it can slide/roll.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MiniVanWoodenBoard : MonoBehaviour, IMiniVanGameModeInteractable
    {
        private const float PickupReach = 2.6f;
        private const float PlaceMaxDistance = 6.5f;
        private const float OverlapPadding = 0.82f;

        // Camera-local: right side, ~quarter of the view, tip pointing forward-down.
        private static readonly Vector3 CarryLocalPosition = new Vector3(0.62f, -0.28f, 1.05f);
        private static readonly Vector3 CarryLocalEuler = new Vector3(8f, 8f, 72f);

        private static readonly Dictionary<MiniVanPlayer, MiniVanWoodenBoard> carriedByPlayer =
            new Dictionary<MiniVanPlayer, MiniVanWoodenBoard>();

        private static readonly Collider[] overlapBuffer = new Collider[32];
        private static Material ghostGreenMaterial;
        private static Material ghostRedMaterial;

        private Rigidbody body;
        private BoxCollider rootBox;
        private BoxCollider pickupTrigger;
        private Collider[] colliders;
        private MiniVanPlayer carrier;

        private GameObject placementGhost;
        private Renderer[] ghostRenderers;
        private bool placementValid;
        private bool placementHasHit;
        private Vector3 placementPosition;
        private Quaternion placementRotation = Quaternion.identity;
        private Vector3 placementSurfaceNormal = Vector3.up;
        private Collider placementSupport;
        private float placementPitchDegrees;
        private bool lastGhostValid = true;
        private float placeInputArmedAt = -1f;
        private bool placeInputArmed;

        public bool IsCarried => carrier != null;

        public static MiniVanWoodenBoard GetCarriedBy(MiniVanPlayer player)
        {
            return player != null && carriedByPlayer.TryGetValue(player, out MiniVanWoodenBoard board)
                ? board
                : null;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            rootBox = GetComponent<BoxCollider>();
            EnsurePickupTrigger();
            colliders = GetComponentsInChildren<Collider>(true);
            ConfigureFreeBody();
        }

        private void EnsurePickupTrigger()
        {
            Transform existing = transform.Find("PickupTrigger");
            if (existing != null)
            {
                pickupTrigger = existing.GetComponent<BoxCollider>();
            }

            if (pickupTrigger == null)
            {
                GameObject triggerObject = new GameObject("PickupTrigger");
                triggerObject.transform.SetParent(transform, false);
                triggerObject.layer = gameObject.layer;
                pickupTrigger = triggerObject.AddComponent<BoxCollider>();
            }

            Vector3 size = rootBox != null ? rootBox.size : new Vector3(0.22f, 0.045f, 2.35f);
            pickupTrigger.isTrigger = true;
            pickupTrigger.center = rootBox != null ? rootBox.center : Vector3.zero;
            // Tall enough to aim easily while looking down at the plank.
            pickupTrigger.size = new Vector3(
                Mathf.Max(0.35f, size.x * 1.35f),
                Mathf.Max(0.55f, size.y + 0.5f),
                Mathf.Max(0.8f, size.z * 1.05f));
        }

        private void OnDestroy()
        {
            DestroyPlacementGhost();
            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }
        }

        private void LateUpdate()
        {
            if (carrier == null)
            {
                HidePlacementGhost();
                return;
            }

            ApplyCarryPose(carrier);

            if (carrier != MiniVanPlayer.LocalPlayer)
            {
                HidePlacementGhost();
                return;
            }

            // While riding, Q is tap-drop / hold-exit and handled by the player itself.
            if (!carrier.IsRidingBoard && MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop))
            {
                DropNear(carrier);
                return;
            }

            // Same E that picked the board up must not immediately place it.
            if (!placeInputArmed)
            {
                HidePlacementGhost();
                if (Time.unscaledTime >= placeInputArmedAt &&
                    !MiniVanKeyBindings.GetKey(MiniVanKeyAction.Interact))
                {
                    placeInputArmed = true;
                }

                return;
            }

            UpdatePlacementPreview(carrier);
            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) && placementHasHit && placementValid)
            {
                PlaceAtPreview(carrier);
            }
        }

        private void OnGUI()
        {
            MiniVanPlayer local = MiniVanPlayer.LocalPlayer;
            if (local == null || carrier != local)
            {
                return;
            }

            string placePrompt;
            if (!placeInputArmed)
            {
                placePrompt = "Carrying board";
            }
            else if (!placementHasHit)
            {
                placePrompt = "Look at a surface to place board";
            }
            else if (!placementValid)
            {
                placePrompt = "No space";
            }
            else
            {
                placePrompt = "E - place  |  scroll↑ raise end  |  scroll↓ lower end  |  Q - drop";
            }

            GUI.Box(new Rect(Screen.width * 0.5f - 170f, Screen.height - 118f, 340f, 34f), placePrompt);
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (player == null || IsCarried || GetCarriedBy(player) != null)
            {
                return string.Empty;
            }

            if (PlayerBusyWithOtherCarry(player))
            {
                return string.Empty;
            }

            return IsInPickupReach(player) ? "E - take board" : string.Empty;
        }

        public void Interact(MiniVanPlayer player)
        {
            if (player == null || Input.GetMouseButton(1) || GetCarriedBy(player) == this)
            {
                return;
            }

            TryPickup(player);
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        public bool TryPickup(MiniVanPlayer player)
        {
            if (player == null || carrier != null)
            {
                return false;
            }

            if (GetCarriedBy(player) != null || PlayerBusyWithOtherCarry(player))
            {
                return false;
            }

            if (!IsInPickupReach(player))
            {
                return false;
            }

            carriedByPlayer[player] = this;
            carrier = player;
            placeInputArmed = false;
            placeInputArmedAt = Time.unscaledTime + 0.12f;
            placementPitchDegrees = 0f;
            transform.SetParent(null, true);
            ConfigureCarriedBody();
            ApplyCarryPose(player);
            HidePlacementGhost();
            return true;
        }

        public void PlaceAtPreview(MiniVanPlayer player)
        {
            if (player == null || carrier != player || !placeInputArmed || !placementHasHit || !placementValid)
            {
                return;
            }

            carriedByPlayer.Remove(player);
            carrier = null;
            placeInputArmed = false;
            DestroyPlacementGhost();
            transform.SetParent(null, true);
            // Tiny lift along surface normal so physics can settle / tip from overhang.
            Vector3 spawnPos = placementPosition + placementSurfaceNormal * 0.012f;
            transform.SetPositionAndRotation(spawnPos, placementRotation);
            ConfigureFreeBody();
            if (body != null)
            {
                body.position = spawnPos;
                body.rotation = placementRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
        }

        public void DropNear(MiniVanPlayer player)
        {
            placeInputArmed = false;
            if (carrier != null)
            {
                carriedByPlayer.Remove(carrier);
                carrier = null;
            }

            DestroyPlacementGhost();
            transform.SetParent(null, true);
            if (player != null)
            {
                Vector3 drop =
                    player.transform.position +
                    player.transform.forward * 1.1f +
                    Vector3.up * 0.55f;
                transform.position = drop;
                transform.rotation = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized,
                    Vector3.up);
            }

            ConfigureFreeBody();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Random.insideUnitSphere * 0.4f;
                body.WakeUp();
            }
        }

        private static bool PlayerBusyWithOtherCarry(MiniVanPlayer player)
        {
            return MiniVanBatteryCharger.GetCarriedBy(player) != null ||
                   MiniVanCarBattery.GetCarriedBy(player) != null ||
                   MiniVanBridgeBattery.GetCarriedBy(player) != null ||
                   MiniVanBridgePowerCable.HasCarriedEnd(player) ||
                   MiniVanDamValve.GetCarriedBy(player) != null ||
                   MiniVanDetachedWheel.GetCarriedBy(player) != null ||
                   MiniVanWinchHook.GetCarriedBy(player) != null;
        }

        private bool IsInPickupReach(MiniVanPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            Collider col = rootBox != null ? rootBox : GetComponent<Collider>();
            Vector3 sample = player.transform.position + Vector3.up * 0.9f;
            Vector3 closest = col != null ? col.ClosestPoint(sample) : transform.position;
            return Vector3.Distance(sample, closest) <= PickupReach;
        }

        private void ApplyCarryPose(MiniVanPlayer player)
        {
            if (player == null)
            {
                return;
            }

            Transform cam = player.PlayerCamera != null
                ? player.PlayerCamera.transform
                : (player.CameraRoot != null ? player.CameraRoot : player.transform);

            Vector3 worldPos = cam.TransformPoint(CarryLocalPosition);
            Quaternion worldRot = cam.rotation * Quaternion.Euler(CarryLocalEuler);
            transform.SetPositionAndRotation(worldPos, worldRot);
            if (body != null)
            {
                body.position = worldPos;
                body.rotation = worldRot;
            }
        }

        private void UpdatePlacementPreview(MiniVanPlayer player)
        {
            placementHasHit = false;
            placementValid = false;
            placementSupport = null;
            if (player == null || player.PlayerCamera == null)
            {
                HidePlacementGhost();
                return;
            }

            ApplyPlacementScrollInput();

            if (!TryGetAimHit(player, out RaycastHit hit))
            {
                HidePlacementGhost();
                return;
            }

            // Need a surface the near end can rest on.
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.15f)
            {
                HidePlacementGhost();
                return;
            }

            placementSupport = hit.collider;
            placementHasHit = true;
            if (!TryBuildPivotPlacement(player, hit, out placementPosition, out placementRotation, out placementSurfaceNormal))
            {
                HidePlacementGhost();
                placementHasHit = false;
                return;
            }

            placementValid = IsPlacementClear(
                placementPosition,
                placementRotation,
                placementSurfaceNormal,
                player,
                placementSupport);
            ShowPlacementGhost();
        }

        private void ApplyPlacementScrollInput()
        {
            Vector2 delta = Input.mouseScrollDelta;
            // Prefer horizontal tilt if present; otherwise vertical wheel.
            float scroll = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? delta.x : delta.y;
            if (Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }

            // Wheel up/right → raise far end; wheel down/left → lower far end (to vertical down).
            float step = scroll > 0f ? 6f : -6f;
            placementPitchDegrees = Mathf.Clamp(placementPitchDegrees + step, -90f, 90f);
        }

        private bool TryGetAimHit(MiniVanPlayer player, out RaycastHit hit)
        {
            hit = default;
            Ray ray = new Ray(player.PlayerCamera.transform.position, player.PlayerCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, PlaceMaxDistance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                if (hitCollider.transform.IsChildOf(transform) ||
                    hitCollider.transform.IsChildOf(player.transform) ||
                    (placementGhost != null && hitCollider.transform.IsChildOf(placementGhost.transform)))
                {
                    continue;
                }

                if (Vector3.Dot(hits[i].normal, Vector3.up) < -0.2f)
                {
                    continue;
                }

                hit = hits[i];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Near end rests on the aim point; yaw spins the board; pitch lifts the far end up to vertical.
        /// </summary>
        private bool TryBuildPivotPlacement(
            MiniVanPlayer player,
            RaycastHit hit,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 boardUp)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            boardUp = Vector3.up;

            float length = GetBoardLength();
            float thickness = GetBoardThickness();
            if (length < 0.2f)
            {
                return false;
            }

            Vector3 surfaceUp = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : Vector3.up;
            if (surfaceUp.y >= 0.95f)
            {
                surfaceUp = Vector3.up;
            }

            Vector3 away = Vector3.ProjectOnPlane(player.PlayerCamera.transform.forward, surfaceUp);
            if (away.sqrMagnitude < 0.001f)
            {
                away = Vector3.ProjectOnPlane(player.transform.forward, surfaceUp);
            }

            if (away.sqrMagnitude < 0.001f)
            {
                return false;
            }

            away.Normalize();

            Vector3 right = Vector3.Cross(surfaceUp, away);
            if (right.sqrMagnitude < 0.001f)
            {
                return false;
            }

            right.Normalize();

            float pitch = Mathf.Clamp(placementPitchDegrees, -90f, 90f);
            // Pitch+: raise far end; pitch-: lower far end. Pivot at near end.
            Quaternion pitchRotation = Quaternion.AngleAxis(-pitch, right);
            Vector3 forward = pitchRotation * away;
            boardUp = pitchRotation * surfaceUp;
            if (Vector3.Dot(boardUp, surfaceUp) < -0.01f)
            {
                boardUp = -boardUp;
            }

            rotation = Quaternion.LookRotation(forward, boardUp);

            // Anchor the near bottom edge on the aim hit.
            Vector3 localNearBottom = new Vector3(0f, -thickness * 0.5f, -length * 0.5f);
            position = hit.point - rotation * localNearBottom;
            placementSurfaceNormal = boardUp;
            return true;
        }

        private float GetBoardLength()
        {
            if (rootBox == null)
            {
                return 2.35f;
            }

            return Mathf.Abs(rootBox.size.z * transform.lossyScale.z);
        }

        private float GetBoardThickness()
        {
            if (rootBox == null)
            {
                return 0.045f;
            }

            return Mathf.Abs(rootBox.size.y * transform.lossyScale.y);
        }

        private void ShowPlacementGhost()
        {
            EnsurePlacementGhost();
            if (placementGhost == null)
            {
                return;
            }

            placementGhost.SetActive(true);
            placementGhost.transform.SetPositionAndRotation(placementPosition, placementRotation);
            if (lastGhostValid != placementValid)
            {
                lastGhostValid = placementValid;
                ApplyGhostColor(placementValid);
            }
        }

        private bool IsPlacementClear(
            Vector3 position,
            Quaternion rotation,
            Vector3 surfaceNormal,
            MiniVanPlayer player,
            Collider support)
        {
            if (rootBox == null)
            {
                return true;
            }

            Vector3 lossy = new Vector3(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z));
            Vector3 halfExtents = Vector3.Scale(rootBox.size * 0.5f, lossy) * OverlapPadding;
            // Ignore most of the thickness so resting / overhanging on a support stays valid.
            halfExtents.y = Mathf.Max(0.008f, halfExtents.y * 0.35f);
            Vector3 boardUp = rotation * Vector3.up;
            Vector3 worldCenter = position + rotation * Vector3.Scale(rootBox.center, lossy) + boardUp * 0.015f;

            int count = Physics.OverlapBoxNonAlloc(
                worldCenter,
                halfExtents,
                overlapBuffer,
                rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider other = overlapBuffer[i];
                if (other == null)
                {
                    continue;
                }

                if (other == support ||
                    other.transform.IsChildOf(transform) ||
                    (support != null && other.transform.IsChildOf(support.transform)) ||
                    (player != null && other.transform.IsChildOf(player.transform)) ||
                    (placementGhost != null && other.transform.IsChildOf(placementGhost.transform)))
                {
                    continue;
                }

                if (other.GetComponentInParent<Terrain>() != null)
                {
                    continue;
                }

                // Contact from below / along the aimed surface = support, not a block.
                // This allows placing on an edge so gravity can tip the board.
                Vector3 closest = other.ClosestPoint(worldCenter);
                Vector3 toClosest = closest - worldCenter;
                if (toClosest.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                Vector3 dir = toClosest.normalized;
                if (Vector3.Dot(dir, boardUp) < -0.2f || Vector3.Dot(dir, surfaceNormal) < -0.2f)
                {
                    continue;
                }

                Rigidbody otherBody = other.attachedRigidbody;
                if (otherBody == null && other.bounds.size.y < 0.12f && other.bounds.size.x > 2f && other.bounds.size.z > 2f)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void EnsurePlacementGhost()
        {
            if (placementGhost != null)
            {
                return;
            }

            placementGhost = new GameObject("WoodenBoard_PlacementGhost");
            placementGhost.hideFlags = HideFlags.HideAndDontSave;

            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            List<Transform> created = new List<Transform>(filters.Length);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                GameObject part = new GameObject(filter.name + "_Ghost");
                part.transform.SetParent(placementGhost.transform, false);
                part.transform.position = filter.transform.position;
                part.transform.rotation = filter.transform.rotation;
                part.transform.localScale = filter.transform.lossyScale;

                MeshFilter ghostFilter = part.AddComponent<MeshFilter>();
                ghostFilter.sharedMesh = filter.sharedMesh;
                MeshRenderer ghostRenderer = part.AddComponent<MeshRenderer>();
                ghostRenderer.sharedMaterial = GetGhostMaterial(true);
                ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;
                created.Add(part.transform);
            }

            for (int i = 0; i < created.Count; i++)
            {
                Transform part = created[i];
                Vector3 localPos = transform.InverseTransformPoint(part.position);
                Quaternion localRot = Quaternion.Inverse(transform.rotation) * part.rotation;
                Vector3 worldScale = part.lossyScale;
                Vector3 parentScale = transform.lossyScale;
                part.localPosition = localPos;
                part.localRotation = localRot;
                part.localScale = new Vector3(
                    SafeDiv(worldScale.x, parentScale.x),
                    SafeDiv(worldScale.y, parentScale.y),
                    SafeDiv(worldScale.z, parentScale.z));
            }

            ghostRenderers = placementGhost.GetComponentsInChildren<Renderer>(true);
            lastGhostValid = true;
            ApplyGhostColor(true);
            placementGhost.SetActive(false);
        }

        private void ApplyGhostColor(bool valid)
        {
            Material material = GetGhostMaterial(valid);
            if (ghostRenderers == null)
            {
                return;
            }

            for (int i = 0; i < ghostRenderers.Length; i++)
            {
                if (ghostRenderers[i] != null)
                {
                    ghostRenderers[i].sharedMaterial = material;
                }
            }
        }

        private void HidePlacementGhost()
        {
            if (placementGhost != null)
            {
                placementGhost.SetActive(false);
            }

            placementHasHit = false;
            placementValid = false;
        }

        private void DestroyPlacementGhost()
        {
            if (placementGhost != null)
            {
                Destroy(placementGhost);
                placementGhost = null;
                ghostRenderers = null;
            }
        }

        private static Material GetGhostMaterial(bool valid)
        {
            if (valid)
            {
                if (ghostGreenMaterial == null)
                {
                    ghostGreenMaterial = CreateGhostMaterial(new Color(0.15f, 1f, 0.3f, 0.45f));
                }

                return ghostGreenMaterial;
            }

            if (ghostRedMaterial == null)
            {
                ghostRedMaterial = CreateGhostMaterial(new Color(1f, 0.2f, 0.15f, 0.45f));
            }

            return ghostRedMaterial;
        }

        private static Material CreateGhostMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color,
                renderQueue = 3000
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            return material;
        }

        private static float SafeDiv(float a, float b)
        {
            return Mathf.Abs(b) < 0.0001f ? a : a / b;
        }

        private void ConfigureCarriedBody()
        {
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            SetCollidersEnabled(false);
        }

        private void ConfigureFreeBody()
        {
            EnsurePickupTrigger();
            if (body != null)
            {
                // Long plank: inertia from collider so overhang tips naturally.
                body.mass = 16f;
                body.linearDamping = 0.04f;
                body.angularDamping = 0.06f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.constraints = RigidbodyConstraints.None;
                body.isKinematic = false;
                body.useGravity = true;
                body.detectCollisions = true;
                body.ResetCenterOfMass();
                body.ResetInertiaTensor();
            }

            SetCollidersEnabled(true);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null)
                {
                    continue;
                }

                if (pickupTrigger != null && col == pickupTrigger)
                {
                    col.enabled = enabled;
                    col.isTrigger = true;
                    continue;
                }

                col.enabled = enabled;
                col.isTrigger = false;
            }
        }
    }
}
