using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanPlayerLifeState
    {
        Alive,
        Unconscious,
        Dead,
        Reviving
    }

    public enum MiniVanCorpseState
    {
        World,
        Carried,
        PassengerSeat,
        DoctorTable
    }

    public partial class MiniVanPlayer
    {
        private const ulong NoCorpseCarrier = ulong.MaxValue;
        private const int VendorRevivePrice = 100;
        private const float CorpseUseDistance = 3.4f;
        private const float UnconsciousDurationSeconds = 30f;

        private readonly NetworkVariable<int> networkLifeState = new NetworkVariable<int>(
            (int)MiniVanPlayerLifeState.Alive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> networkUnconsciousEndTime = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> networkCorpseState = new NetworkVariable<int>(
            (int)MiniVanCorpseState.World,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Vector3> networkCorpsePosition = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Quaternion> networkCorpseRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> networkCorpseCarrier = new NetworkVariable<ulong>(
            NoCorpseCarrier,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> networkCorpseVehicle = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> networkCorpseSeat = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private static readonly Color PermanentDeathSkinColor = new Color(0.48f, 0.49f, 0.52f, 1f);
        private static readonly Vector3 CorpseCapsuleScale = new Vector3(0.72f, 0.9f, 0.72f);
        private static readonly Vector3 LyingCapsuleLocalPosition = new Vector3(0f, 0.28f, 0f);
        private static readonly Quaternion LyingCapsuleLocalRotation = Quaternion.Euler(0f, 0f, 90f);
        private static readonly Quaternion LyingVisualLocalRotation = Quaternion.Euler(-90f, 0f, 0f);
        // Seated corpse matches living player: upright capsule at SitPoint (no extra lean offsets).
        private static readonly Vector3 SeatedCapsuleLocalPosition = Vector3.zero;
        private static readonly Quaternion SeatedCapsuleLocalRotation = Quaternion.identity;
        private const float CorpseOutlineWidth = 0.025f;

        private GameObject corpseVisual;
        private Transform corpseBodyCapsule;
        private bool localCorpseDropPredictionActive;
        private Vector3 localCorpseDropPosition;
        private Rigidbody corpseBody;
        private Rigidbody[] corpseRigidbodies;
        private Collider[] corpseColliders;
        private Collider corpseInteractionCollider;
        private Renderer corpseBodyRenderer;
        private Material corpseOutlineMaterial;
        private Material[] corpseOutlineOriginalMaterials;
        private bool corpseOutlined;
        private MiniVanPlayer outlinedCorpse;
        private bool corpseCollisionsEnabled = true;
        private MiniVanPlayer locallyCarriedCorpse;
        private bool deathInteractionConsumedThisFrame;
        private float nextCorpsePublishTime;
        private MiniVanCorpseState lastAppliedCorpseBodyPose = (MiniVanCorpseState)(-1);
        private bool ownerWasDownedLastFrame;

        /// <summary>Fully dead: only R / revive stations (not defibrillator).</summary>
        public bool IsPermanentlyDead =>
            networkLifeState.Value == (int)MiniVanPlayerLifeState.Dead;

        /// <summary>Knocked out: 30s window for defibrillator; body can be carried.</summary>
        public bool IsUnconscious =>
            networkLifeState.Value == (int)MiniVanPlayerLifeState.Unconscious;

        /// <summary>Unconscious or permanently dead — no control, corpse visual.</summary>
        public bool IsDowned => IsUnconscious || IsPermanentlyDead;
        public bool IsIgnoredByEnemies => !isActiveAndEnabled || IsZombieDead || IsDowned;
        public bool IsCarryingCorpse => locallyCarriedCorpse != null;

        public float UnconsciousSecondsRemaining
        {
            get
            {
                if (!IsUnconscious || networkUnconsciousEndTime.Value <= 0f)
                {
                    return 0f;
                }

                float now = NetworkManager != null
                    ? (float)NetworkManager.ServerTime.Time
                    : Time.time;
                return Mathf.Max(0f, networkUnconsciousEndTime.Value - now);
            }
        }

        private void InitializeDeathSystemOnNetworkSpawn()
        {
            if (IsServer)
            {
                networkLifeState.Value = (int)MiniVanPlayerLifeState.Alive;
                networkUnconsciousEndTime.Value = 0f;
                networkCorpseCarrier.Value = NoCorpseCarrier;
                networkCorpseSeat.Value = -1;
                networkCorpseVehicle.Value = 0;
            }
        }

        private void ShutdownDeathSystem()
        {
            ClearCorpseHoverHighlight();
            DestroyCorpseVisual();
            if (locallyCarriedCorpse != null)
            {
                locallyCarriedCorpse = null;
            }
        }

        private bool UpdateDeathSystem()
        {
            deathInteractionConsumedThisFrame = false;
            RefreshCorpseVisual();

            if (IsServer)
            {
                if (IsUnconscious)
                {
                    float now = NetworkManager != null
                        ? (float)NetworkManager.ServerTime.Time
                        : Time.time;
                    if (networkUnconsciousEndTime.Value > 0f && now >= networkUnconsciousEndTime.Value)
                    {
                        ServerPromoteUnconsciousToDead();
                    }
                }

                if (IsDowned)
                {
                    UpdateServerCorpsePose();
                }
            }

            if (!IsOwner)
            {
                return IsDowned;
            }

            if (IsDowned)
            {
                currentSeat = null;
                currentVehicle = null;
                currentLadder = null;
                if (CharacterController != null) CharacterController.enabled = false;
                HandleWalkingLook();
                ClearCorpseHoverHighlight();
                Vector3 cameraFollow = ResolveDownedCameraFollowPosition();
                Vector3 cameraPosition = cameraFollow + Vector3.up * 1.15f;
                transform.position = Vector3.Lerp(transform.position, cameraPosition, Time.deltaTime * 8f);
                // R only after the 30s window ends (permanent death).
                if (IsPermanentlyDead && Input.GetKeyDown(KeyCode.R))
                {
                    RequestSelfReviveServerRpc();
                }

                ownerWasDownedLastFrame = true;
                return true;
            }

            if (ownerWasDownedLastFrame)
            {
                ownerWasDownedLastFrame = false;
                // The revive RPC can land before the life state replicates, so the downed camera
                // follow may have dragged the body back down for a frame — re-seat it on the ground.
                if (currentSeat == null)
                {
                    float footOffset = CharacterController != null
                        ? CharacterController.height * 0.5f - CharacterController.center.y +
                          Mathf.Max(CharacterController.skinWidth, 0.025f)
                        : 1f;
                    transform.position = ResolveReviveStandPosition(transform.position - Vector3.up * footOffset);
                    verticalVelocity = 0f;
                }
            }

            if (CharacterController != null && !CharacterController.enabled && currentSeat == null)
            {
                CharacterController.enabled = true;
            }

            UpdateCorpseHoverHighlight();
            UpdateCorpseInteractionInput();
            return false;
        }

        private void RefreshCorpseVisual()
        {
            if (!IsDowned)
            {
                SetCorpseOutlined(false);
                RestorePlayerVisualToLivingBody();
                if (corpseVisual != null)
                {
                    corpseVisual.SetActive(false);
                }

                SetLivingRenderersVisible(true);
                ApplyPlayerSkinColor();
                return;
            }

            EnsureCorpseVisual();
            corpseVisual.SetActive(true);
            AttachPlayerVisualToCorpse();
            ApplyCorpseLifeAppearance();
            ApplyFirstPersonVisibility(false);

            MiniVanCorpseState corpseState = (MiniVanCorpseState)networkCorpseState.Value;
            ApplyCorpseBodyLocalPose(corpseState);

            bool seated = corpseState == MiniVanCorpseState.PassengerSeat;
            if (seated)
            {
                AttachCorpseVisualToSeat();
            }
            else
            {
                DetachCorpseVisualFromSeat();
                if (localCorpseDropPredictionActive)
                {
                    if (corpseState != MiniVanCorpseState.Carried)
                    {
                        localCorpseDropPredictionActive = false;
                        corpseVisual.transform.SetPositionAndRotation(networkCorpsePosition.Value, networkCorpseRotation.Value);
                    }
                    else
                    {
                        corpseVisual.transform.SetPositionAndRotation(
                            localCorpseDropPosition,
                            GetLyingCorpseRotation(transform.eulerAngles.y));
                    }
                }
                else
                {
                    corpseVisual.transform.SetPositionAndRotation(networkCorpsePosition.Value, networkCorpseRotation.Value);
                }
            }

            ApplyDownedPlayerVisualPose(seated);

            bool worldPhysics = corpseState == MiniVanCorpseState.World || localCorpseDropPredictionActive;
            SetCorpseCollisionsEnabled(worldPhysics);
            if (corpseInteractionCollider != null)
            {
                corpseInteractionCollider.enabled = seated;
            }
            if (corpseBody != null)
            {
                // Seated: always kinematic and parented to SitPoint — prevents drive shake from NV lag/physics.
                corpseBody.isKinematic = seated || !IsServer || !worldPhysics;
                corpseBody.useGravity = !seated && IsServer && worldPhysics;
                corpseBody.interpolation = seated
                    ? RigidbodyInterpolation.None
                    : RigidbodyInterpolation.Interpolate;
                if (seated)
                {
                    corpseBody.linearVelocity = Vector3.zero;
                    corpseBody.angularVelocity = Vector3.zero;
                }
            }
            if (corpseRigidbodies != null)
            {
                for (int i = 0; i < corpseRigidbodies.Length; i++)
                {
                    Rigidbody partBody = corpseRigidbodies[i];
                    if (partBody == null || partBody == corpseBody) continue;
                    partBody.isKinematic = seated || !IsServer || !worldPhysics;
                    partBody.useGravity = !seated && IsServer && worldPhysics;
                }
            }
        }

        private bool TryGetCorpseSeat(out MiniVanSeat seat)
        {
            seat = null;
            MiniVanVehicle vehicle = FindVehicle(networkCorpseVehicle.Value);
            if (vehicle == null)
            {
                return false;
            }

            seat = vehicle.GetSeat(networkCorpseSeat.Value);
            return seat != null && seat.SitPoint != null;
        }

        private void AttachCorpseVisualToSeat()
        {
            if (corpseVisual == null || !TryGetCorpseSeat(out MiniVanSeat seat))
            {
                DetachCorpseVisualFromSeat();
                return;
            }

            if (corpseVisual.transform.parent != seat.SitPoint)
            {
                corpseVisual.transform.SetParent(seat.SitPoint, false);
            }

            corpseVisual.transform.localPosition = Vector3.zero;
            corpseVisual.transform.localRotation = Quaternion.identity;
            corpseVisual.transform.localScale = Vector3.one;
        }

        private void DetachCorpseVisualFromSeat()
        {
            if (corpseVisual == null || corpseVisual.transform.parent == null)
            {
                return;
            }

            corpseVisual.transform.SetParent(null, true);
        }

        private Vector3 ResolveDownedCameraFollowPosition()
        {
            if ((MiniVanCorpseState)networkCorpseState.Value == MiniVanCorpseState.PassengerSeat &&
                TryGetCorpseSeat(out MiniVanSeat seat))
            {
                return seat.SitPoint.position;
            }

            if (corpseVisual != null && corpseVisual.activeInHierarchy)
            {
                return corpseVisual.transform.position;
            }

            return networkCorpsePosition.Value;
        }

        private void DestroyCorpseVisual()
        {
            corpseOutlined = false;
            corpseOutlineOriginalMaterials = null;
            RestorePlayerVisualToLivingBody();
            DetachCorpseVisualFromSeat();
            if (corpseVisual != null)
            {
                Destroy(corpseVisual);
            }

            corpseVisual = null;
            corpseBodyCapsule = null;
            corpseBody = null;
            corpseRigidbodies = null;
            corpseColliders = null;
            corpseInteractionCollider = null;
            corpseBodyRenderer = null;
            lastAppliedCorpseBodyPose = (MiniVanCorpseState)(-1);
        }

        private void EnsureCorpseVisual()
        {
            bool visualOnCorpse = playerVisualRoot != null &&
                                  corpseVisual != null &&
                                  playerVisualRoot.transform.IsChildOf(corpseVisual.transform);
            if (corpseVisual != null && corpseVisual.transform.Find("BodyCapsule") != null && !visualOnCorpse)
            {
                DestroyCorpseVisual();
            }

            if (corpseVisual != null)
            {
                return;
            }

            corpseVisual = new GameObject("PLAYER CORPSE");
            MiniVanPlayerCorpseProxy proxy = corpseVisual.AddComponent<MiniVanPlayerCorpseProxy>();
            proxy.Player = this;
            corpseBody = corpseVisual.AddComponent<Rigidbody>();
            corpseBody.mass = 38f;
            corpseBody.linearDamping = 0.8f;
            corpseBody.angularDamping = 2.5f;
            corpseBody.interpolation = RigidbodyInterpolation.Interpolate;
            corpseBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            GameObject hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hull.name = "BodyCollider";
            hull.transform.SetParent(corpseVisual.transform, false);
            hull.transform.localScale = CorpseCapsuleScale;
            corpseBodyCapsule = hull.transform;
            MeshRenderer hullRenderer = hull.GetComponent<MeshRenderer>();
            if (hullRenderer != null)
            {
                hullRenderer.enabled = false;
                hullRenderer.forceRenderingOff = true;
            }

            GameObject interaction = new GameObject("Corpse Interaction");
            interaction.transform.SetParent(corpseVisual.transform, false);
            BoxCollider interactionBox = interaction.AddComponent<BoxCollider>();
            interactionBox.isTrigger = true;
            interactionBox.enabled = false;
            corpseInteractionCollider = interactionBox;

            corpseRigidbodies = corpseVisual.GetComponentsInChildren<Rigidbody>(true);
            corpseColliders = corpseVisual.GetComponentsInChildren<Collider>(true);
            corpseCollisionsEnabled = true;
            ApplyCorpseBodyLocalPose(MiniVanCorpseState.World, force: true);
        }

        private void AttachPlayerVisualToCorpse()
        {
            EnsurePlayerVisual();
            if (playerVisualRoot == null || corpseVisual == null)
            {
                return;
            }

            if (playerVisualRoot.transform.parent != corpseVisual.transform)
            {
                playerVisualRoot.transform.SetParent(corpseVisual.transform, false);
            }

            RefreshCorpseBodyRenderer();
            if (playerAnimator != null)
            {
                if (!playerVisualFrozenForDeath)
                {
                    playerVisualFrozenForDeath = true;
                    SnapAnimatorToIdlePose();
                }

                playerAnimator.enabled = false;
            }
        }

        private void RestorePlayerVisualToLivingBody()
        {
            if (playerVisualRoot == null)
            {
                return;
            }

            if (playerVisualRoot.transform.parent != transform)
            {
                playerVisualRoot.transform.SetParent(transform, false);
            }

            if (playerAnimator != null)
            {
                playerAnimator.enabled = true;
                if (playerVisualFrozenForDeath)
                {
                    playerVisualFrozenForDeath = false;
                    SnapAnimatorToIdlePose();
                }
            }

            playerVisualRoot.transform.localPosition = PlayerVisualLocalPosition;
            playerVisualRoot.transform.localRotation = Quaternion.identity;
            playerVisualRoot.transform.localScale = Vector3.one * Mathf.Max(0.01f, PlayerVisualUniformScale);
            ApplyFirstPersonVisibility(IsOwner && !IsFacePainting());
            ApplyPlayerSkinColor();
        }

        private void RefreshCorpseBodyRenderer()
        {
            if (playerVisualRoot == null)
            {
                corpseBodyRenderer = null;
                return;
            }

            Transform head = playerHeadTransform != null
                ? playerHeadTransform
                : FindChildByName(playerVisualRoot.transform, PlayerHeadObjectName);
            corpseBodyRenderer = head != null
                ? head.GetComponent<Renderer>()
                : playerVisualRoot.GetComponentInChildren<Renderer>(true);
        }

        private void ApplyCorpseBodyLocalPose(MiniVanCorpseState state, bool force = false)
        {
            if (corpseBodyCapsule == null)
            {
                return;
            }

            MiniVanCorpseState poseState = state == MiniVanCorpseState.PassengerSeat
                ? MiniVanCorpseState.PassengerSeat
                : MiniVanCorpseState.World;
            if (!force && lastAppliedCorpseBodyPose == poseState)
            {
                return;
            }

            lastAppliedCorpseBodyPose = poseState;
            bool seated = poseState == MiniVanCorpseState.PassengerSeat;
            if (seated)
            {
                corpseBodyCapsule.localPosition = SeatedCapsuleLocalPosition;
                corpseBodyCapsule.localRotation = SeatedCapsuleLocalRotation;
                corpseBodyCapsule.localScale = CorpseCapsuleScale;
                if (corpseInteractionCollider is BoxCollider seatBox)
                {
                    seatBox.center = SeatedCapsuleLocalPosition;
                    seatBox.size = new Vector3(0.85f, 1.85f, 0.85f);
                }
            }
            else
            {
                corpseBodyCapsule.localPosition = LyingCapsuleLocalPosition;
                corpseBodyCapsule.localRotation = LyingCapsuleLocalRotation;
                corpseBodyCapsule.localScale = CorpseCapsuleScale;
                if (corpseInteractionCollider is BoxCollider worldBox)
                {
                    worldBox.center = LyingCapsuleLocalPosition;
                    worldBox.size = new Vector3(1.85f, 0.85f, 0.95f);
                }
            }
        }

        private void ApplyDownedPlayerVisualPose(bool seated)
        {
            if (playerVisualRoot == null ||
                corpseVisual == null ||
                playerVisualRoot.transform.parent != corpseVisual.transform)
            {
                return;
            }

            if (playerAnimator != null)
            {
                playerAnimator.enabled = false;
            }

            playerVisualRoot.transform.localScale = Vector3.one * Mathf.Max(0.01f, PlayerVisualUniformScale);
            if (seated)
            {
                playerVisualRoot.transform.localPosition = PlayerVisualLocalPosition + PlayerSitVisualOffset;
                playerVisualRoot.transform.localRotation = Quaternion.identity;
                return;
            }

            playerVisualRoot.transform.localRotation = LyingVisualLocalRotation;
            playerVisualRoot.transform.localPosition = Vector3.zero;
            LiftTransformOntoGround(playerVisualRoot.transform, corpseVisual.transform.position.y);
        }

        private static void LiftTransformOntoGround(Transform target, float groundY)
        {
            if (target == null || !TryGetMeshBounds(target, out Bounds bounds))
            {
                return;
            }

            float lift = groundY - bounds.min.y + 0.03f;
            if (Mathf.Abs(lift) > 0.0001f)
            {
                target.position += Vector3.up * lift;
            }
        }

        private static bool TryGetMeshBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return any;
        }

        private void ApplyCorpseLifeAppearance()
        {
            if (IsPermanentlyDead)
            {
                ApplyPlayerSkinColor(PermanentDeathSkinColor);
                return;
            }

            ApplyPlayerSkinColor();
        }

        private void SetCorpseCollisionsEnabled(bool enabled)
        {
            if (corpseColliders == null || corpseCollisionsEnabled == enabled)
            {
                return;
            }

            corpseCollisionsEnabled = enabled;
            for (int i = 0; i < corpseColliders.Length; i++)
            {
                Collider corpseCollider = corpseColliders[i];
                if (corpseCollider != null)
                {
                    corpseCollider.enabled = enabled;
                }
            }

            if (corpseRigidbodies == null)
            {
                return;
            }

            for (int i = 0; i < corpseRigidbodies.Length; i++)
            {
                Rigidbody body = corpseRigidbodies[i];
                if (body != null)
                {
                    body.detectCollisions = enabled;
                }
            }
        }

        private void SetLivingRenderersVisible(bool visible)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer == playerCapsuleRenderer)
                {
                    continue;
                }

                if (corpseVisual != null && renderer.transform.IsChildOf(corpseVisual.transform))
                {
                    continue;
                }

                renderer.enabled = visible;
            }

            if (playerCapsuleRenderer != null)
            {
                playerCapsuleRenderer.enabled = false;
                playerCapsuleRenderer.forceRenderingOff = true;
            }
        }

        private void UpdateServerCorpsePose()
        {
            MiniVanCorpseState state = (MiniVanCorpseState)networkCorpseState.Value;
            if (state == MiniVanCorpseState.Carried)
            {
                MiniVanPlayer carrier = FindPlayerByClientId(networkCorpseCarrier.Value);
                if (carrier == null || carrier.IsDowned)
                {
                    ServerDropCorpse(networkCorpsePosition.Value);
                    return;
                }
                networkCorpsePosition.Value = carrier.transform.position + carrier.transform.forward * 0.85f + Vector3.up * 1.05f;
                // Capsule already lies locally (Z=90); root only aims with the carrier.
                networkCorpseRotation.Value = Quaternion.LookRotation(carrier.transform.right, Vector3.up);
            }
            else if (state == MiniVanCorpseState.PassengerSeat)
            {
                MiniVanVehicle vehicle = FindVehicle(networkCorpseVehicle.Value);
                MiniVanSeat seat = vehicle != null ? vehicle.GetSeat(networkCorpseSeat.Value) : null;
                if (seat == null || seat.SitPoint == null)
                {
                    ServerDropCorpse(networkCorpsePosition.Value);
                    return;
                }

                // Capsule local pose is seat-tuned; root tracks SitPoint 1:1.
                networkCorpsePosition.Value = seat.SitPoint.position;
                networkCorpseRotation.Value = seat.SitPoint.rotation;
            }
            else if (state == MiniVanCorpseState.World && corpseBody != null && Time.time >= nextCorpsePublishTime)
            {
                nextCorpsePublishTime = Time.time + 0.1f;
                Vector3 grounded = SnapCorpseToGround(corpseVisual.transform.position);
                // Keep yaw from physics/root, but pin to ground so the capsule never floats.
                networkCorpsePosition.Value = grounded;
                networkCorpseRotation.Value = GetLyingCorpseRotation(corpseVisual.transform.eulerAngles.y);
                corpseBody.MovePosition(grounded);
                corpseBody.MoveRotation(networkCorpseRotation.Value);
            }
        }

        private void UpdateCorpseHoverHighlight()
        {
            MiniVanPlayer target = null;
            if (PlayerCamera != null && locallyCarriedCorpse == null)
            {
                Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
                if (TryFindLookedAtCorpse(ray, out MiniVanPlayer corpse) && corpse != this)
                {
                    target = corpse;
                }
            }

            if (outlinedCorpse == target)
            {
                return;
            }

            if (outlinedCorpse != null)
            {
                outlinedCorpse.SetCorpseOutlined(false);
            }

            outlinedCorpse = target;
            if (outlinedCorpse != null)
            {
                outlinedCorpse.SetCorpseOutlined(true);
            }
        }

        private void ClearCorpseHoverHighlight()
        {
            if (outlinedCorpse != null)
            {
                outlinedCorpse.SetCorpseOutlined(false);
                outlinedCorpse = null;
            }
        }

        private void SetCorpseOutlined(bool highlighted)
        {
            if (corpseOutlined == highlighted)
            {
                return;
            }

            if (!IsDowned && highlighted)
            {
                return;
            }

            EnsureCorpseVisual();
            if (corpseBodyRenderer == null)
            {
                return;
            }

            corpseOutlined = highlighted;
            if (highlighted)
            {
                corpseOutlineOriginalMaterials = corpseBodyRenderer.sharedMaterials;
                Material outline = GetCorpseOutlineMaterial();
                Material[] materials = new Material[corpseOutlineOriginalMaterials.Length + 1];
                for (int i = 0; i < corpseOutlineOriginalMaterials.Length; i++)
                {
                    materials[i] = corpseOutlineOriginalMaterials[i];
                }

                materials[materials.Length - 1] = outline;
                corpseBodyRenderer.sharedMaterials = materials;
            }
            else if (corpseOutlineOriginalMaterials != null)
            {
                corpseBodyRenderer.sharedMaterials = corpseOutlineOriginalMaterials;
                corpseOutlineOriginalMaterials = null;
            }
        }

        private Material GetCorpseOutlineMaterial()
        {
            if (corpseOutlineMaterial != null)
            {
                return corpseOutlineMaterial;
            }

            Material shared = Resources.Load<Material>("Panelka/ThinWhiteOutline");
            Shader shader = Shader.Find("MiniVanGame/ThinWhiteOutline");
            if (shared != null)
            {
                corpseOutlineMaterial = new Material(shared)
                {
                    name = "Corpse Thin White Outline"
                };
            }
            else if (shader != null)
            {
                corpseOutlineMaterial = new Material(shader)
                {
                    name = "Corpse Thin White Outline"
                };
            }
            else
            {
                corpseOutlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"))
                {
                    name = "Corpse Outline Fallback",
                    color = Color.white
                };
            }

            if (corpseOutlineMaterial.HasProperty("_OutlineColor"))
            {
                corpseOutlineMaterial.SetColor("_OutlineColor", Color.white);
            }

            if (corpseOutlineMaterial.HasProperty("_OutlineWidth"))
            {
                corpseOutlineMaterial.SetFloat("_OutlineWidth", CorpseOutlineWidth);
            }

            if (corpseOutlineMaterial.HasProperty("_OutlineWidthIndependent"))
            {
                corpseOutlineMaterial.SetFloat("_OutlineWidthIndependent", 0f);
            }

            if (corpseOutlineMaterial.HasProperty("_OutlineZPos"))
            {
                corpseOutlineMaterial.SetFloat("_OutlineZPos", -0.1f);
            }

            return corpseOutlineMaterial;
        }

        private static Vector3 SnapCorpseToGround(Vector3 position)
        {
            Vector3 rayOrigin = position + Vector3.up * 2.5f;
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                if (hitCollider.GetComponentInParent<MiniVanPlayer>() != null ||
                    hitCollider.GetComponentInParent<MiniVanPlayerCorpseProxy>() != null)
                {
                    continue;
                }

                return new Vector3(position.x, hits[i].point.y, position.z);
            }

            return new Vector3(position.x, position.y, position.z);
        }

        private void UpdateCorpseInteractionInput()
        {
            if (MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Drop) && locallyCarriedCorpse != null)
            {
                MiniVanPlayer carriedCorpse = locallyCarriedCorpse;
                Vector3 dropPos = transform.position + transform.forward * 1.1f + Vector3.up * 0.4f;
                if (!IsServer)
                {
                    locallyCarriedCorpse = null;
                    carriedCorpse.BeginLocalCorpseDropPrediction(dropPos);
                }

                RequestCorpseDropServerRpc(carriedCorpse.OwnerClientId, dropPos);
                deathInteractionConsumedThisFrame = true;
                return;
            }
            if (!MiniVanKeyBindings.GetKeyDown(MiniVanKeyAction.Interact) || PlayerCamera == null) return;

            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            if (locallyCarriedCorpse != null &&
                Physics.Raycast(ray, out RaycastHit hit, CorpseUseDistance, ~0, QueryTriggerInteraction.Collide))
            {
                MiniVanReviveStation station = hit.collider.GetComponentInParent<MiniVanReviveStation>();
                if (station != null)
                {
                    RequestCorpseReviveServerRpc(locallyCarriedCorpse.OwnerClientId, station.Kind == MiniVanReviveStationKind.CitySeller, station.Price);
                    deathInteractionConsumedThisFrame = true;
                    return;
                }
                MiniVanSeat seat = hit.collider.GetComponentInParent<MiniVanSeat>();
                if (seat != null && !seat.IsDriverSeat)
                {
                    RequestCorpseSeatServerRpc(locallyCarriedCorpse.OwnerClientId, new NetworkObjectReference(seat.Vehicle.NetworkObject), seat.SeatIndex);
                    deathInteractionConsumedThisFrame = true;
                    return;
                }
            }

            if (locallyCarriedCorpse == null && TryFindLookedAtCorpse(ray, out MiniVanPlayer corpse))
            {
                RequestCorpsePickupServerRpc(corpse.OwnerClientId);
                deathInteractionConsumedThisFrame = true;
            }
        }

        private bool TryFindLookedAtCorpse(Ray ray, out MiniVanPlayer corpse)
        {
            corpse = null;
            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.3f, CorpseUseDistance, ~0,
                QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform)) continue;

                MiniVanSeat hitSeat = hitCollider.GetComponentInParent<MiniVanSeat>();
                MiniVanPlayer seatedCorpse = hitSeat != null
                    ? GetCorpseInSeat(hitSeat.Vehicle, hitSeat.SeatIndex)
                    : null;
                if (seatedCorpse != null && seatedCorpse != this)
                {
                    corpse = seatedCorpse;
                    return true;
                }

                MiniVanPlayerCorpseProxy proxy = hitCollider.GetComponentInParent<MiniVanPlayerCorpseProxy>();
                if (proxy != null && proxy.Player != null && proxy.Player != this &&
                    proxy.Player.IsDowned)
                {
                    corpse = proxy.Player;
                    return true;
                }

                if (hitCollider.GetComponentInParent<MiniVanVehicle>() != null || hitCollider.isTrigger)
                {
                    continue;
                }

                break;
            }
            return TryFindSeatedCorpseNearAim(ray, out corpse);
        }

        private bool TryFindSeatedCorpseNearAim(Ray ray, out MiniVanPlayer corpse)
        {
            corpse = null;
            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            float bestAimDistance = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer candidate = players[i];
                if (candidate == null || candidate == this || !candidate.IsDowned ||
                    candidate.networkCorpseState.Value != (int)MiniVanCorpseState.PassengerSeat)
                {
                    continue;
                }

                Vector3 toCorpse = candidate.networkCorpsePosition.Value - ray.origin;
                float forwardDistance = Vector3.Dot(toCorpse, ray.direction);
                if (forwardDistance < 0f || forwardDistance > CorpseUseDistance + 1f)
                {
                    continue;
                }

                float aimDistance = Vector3.Distance(
                    candidate.networkCorpsePosition.Value,
                    ray.origin + ray.direction * forwardDistance);
                if (aimDistance <= 0.95f && aimDistance < bestAimDistance)
                {
                    corpse = candidate;
                    bestAimDistance = aimDistance;
                }
            }

            return corpse != null;
        }

        [ServerRpc]
        private void RequestCorpsePickupServerRpc(ulong deadClientId)
        {
            MiniVanPlayer dead = FindPlayerByClientId(deadClientId);
            if (dead == null || !dead.IsDowned || IsDowned || Vector3.Distance(transform.position, dead.networkCorpsePosition.Value) > CorpseUseDistance + 1f) return;
            if (FindCarriedCorpse(OwnerClientId) != null) return;
            if (dead.networkCorpseState.Value == (int)MiniVanCorpseState.Carried) return;
            dead.networkCorpseState.Value = (int)MiniVanCorpseState.Carried;
            dead.networkCorpseCarrier.Value = OwnerClientId;
            dead.networkCorpseVehicle.Value = 0;
            dead.networkCorpseSeat.Value = -1;
            SetLocalCarriedCorpseClientRpc(deadClientId, true, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestCorpseDropServerRpc(ulong deadClientId, Vector3 requestedPosition)
        {
            MiniVanPlayer dead = FindPlayerByClientId(deadClientId);
            if (dead == null || dead.networkCorpseCarrier.Value != OwnerClientId) return;
            dead.ServerDropCorpse(requestedPosition);
            SetLocalCarriedCorpseClientRpc(deadClientId, false, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestCorpseSeatServerRpc(ulong deadClientId, NetworkObjectReference vehicleReference, int seatIndex)
        {
            MiniVanPlayer dead = FindPlayerByClientId(deadClientId);
            if (dead == null || dead.networkCorpseCarrier.Value != OwnerClientId || !vehicleReference.TryGet(out NetworkObject vehicleObject)) return;
            MiniVanVehicle vehicle = vehicleObject.GetComponent<MiniVanVehicle>();
            MiniVanSeat seat = vehicle != null ? vehicle.GetSeat(seatIndex) : null;
            if (seat == null || seat.IsDriverSeat || !vehicle.IsSeatAvailable(seatIndex)) return;
            dead.networkCorpseState.Value = (int)MiniVanCorpseState.PassengerSeat;
            dead.networkCorpseCarrier.Value = NoCorpseCarrier;
            dead.networkCorpseVehicle.Value = vehicle.NetworkObjectId;
            dead.networkCorpseSeat.Value = seatIndex;
            SetLocalCarriedCorpseClientRpc(deadClientId, false, BuildOwnerTarget());
        }

        [ServerRpc]
        private void RequestCorpseReviveServerRpc(ulong deadClientId, bool paidRevive, int requestedPrice)
        {
            MiniVanPlayer dead = FindPlayerByClientId(deadClientId);
            if (dead == null || dead.networkCorpseCarrier.Value != OwnerClientId || !dead.IsDowned) return;
            MiniVanReviveStation station = FindNearbyReviveStation(paidRevive);
            if (station == null) return;
            int price = paidRevive ? Mathf.Max(VendorRevivePrice, station.Price) : 0;
            if (price > 0 && !GameModeServerSpendMoney(price)) return;
            dead.networkCorpseState.Value = (int)MiniVanCorpseState.DoctorTable;
            dead.networkCorpseCarrier.Value = NoCorpseCarrier;
            dead.networkCorpsePosition.Value = station.GetBodyPosition();
            dead.networkCorpseRotation.Value = station.GetBodyRotation();
            SetLocalCarriedCorpseClientRpc(deadClientId, false, BuildOwnerTarget());
            StartCoroutine(ServerReviveRoutine(dead, station));
        }

        private IEnumerator ServerReviveRoutine(MiniVanPlayer dead, MiniVanReviveStation station)
        {
            yield return new WaitForSeconds(station != null && station.Kind == MiniVanReviveStationKind.DoctorTable ? 5f : 2f);
            if (dead != null && dead.IsDowned && station != null)
            {
                Vector3 revivePosition = station.GetBodyPosition() + station.transform.right * 1.25f + Vector3.up * 0.15f;
                dead.ServerReviveAt(revivePosition);
            }
        }

        [ServerRpc]
        private void RequestSelfReviveServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsPermanentlyDead ||
                serverRpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            ServerReviveAt(ResolveSelfRevivePosition());
        }

        private Vector3 ResolveSelfRevivePosition()
        {
            Vector3 position = networkCorpsePosition.Value;

            if (networkCorpseCarrier.Value != NoCorpseCarrier)
            {
                MiniVanPlayer carrier = FindPlayerByClientId(networkCorpseCarrier.Value);
                if (carrier != null)
                {
                    position = carrier.transform.position + carrier.transform.right * 1.2f;
                }
            }
            else if (networkCorpseVehicle.Value != 0)
            {
                MiniVanVehicle[] vehicles = FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
                for (int i = 0; i < vehicles.Length; i++)
                {
                    MiniVanVehicle vehicle = vehicles[i];
                    if (vehicle != null && vehicle.NetworkObjectId == networkCorpseVehicle.Value)
                    {
                        position = vehicle.transform.position + vehicle.transform.right * 2.2f;
                        break;
                    }
                }
            }

            Vector3 rayOrigin = position + Vector3.up * 2.5f;
            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                6f,
                ~0,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null ||
                    hitCollider.transform.IsChildOf(transform) ||
                    (corpseVisual != null && hitCollider.transform.IsChildOf(corpseVisual.transform)))
                {
                    continue;
                }

                position.y = hits[i].point.y + 0.15f;
                break;
            }

            return position;
        }

        [ClientRpc]
        private void SetLocalCarriedCorpseClientRpc(ulong deadClientId, bool carried, ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner) return;
            locallyCarriedCorpse = carried ? FindPlayerByClientId(deadClientId) : null;
        }

        public void BeginLocalCorpseDropPrediction(Vector3 dropPosition)
        {
            localCorpseDropPredictionActive = true;
            localCorpseDropPosition = dropPosition;
            EnsureCorpseVisual();
            if (corpseVisual != null)
            {
                corpseVisual.SetActive(true);
                corpseVisual.transform.SetPositionAndRotation(
                    dropPosition,
                    GetLyingCorpseRotation(transform.eulerAngles.y));
            }
        }

        private void ServerDropCorpse(Vector3 position)
        {
            networkCorpseState.Value = (int)MiniVanCorpseState.World;
            networkCorpseCarrier.Value = NoCorpseCarrier;
            networkCorpseVehicle.Value = 0;
            networkCorpseSeat.Value = -1;
            Vector3 grounded = SnapCorpseToGround(position);
            Quaternion lyingRotation = GetLyingCorpseRotation(networkCorpseRotation.Value.eulerAngles.y);
            networkCorpsePosition.Value = grounded;
            networkCorpseRotation.Value = lyingRotation;
            if (corpseVisual != null)
            {
                corpseVisual.transform.SetPositionAndRotation(grounded, lyingRotation);
                if (corpseBody != null)
                {
                    corpseBody.linearVelocity = Vector3.zero;
                    corpseBody.angularVelocity = Vector3.zero;
                }
            }
        }

        private static Quaternion GetLyingCorpseRotation(float yawDegrees)
        {
            // Yaw only — the capsule itself is rotated locally to lie flat on the ground.
            return Quaternion.Euler(0f, yawDegrees, 0f);
        }

        private void ServerEnterDeathState()
        {
            // Health hit zero → unconscious window first (defibrillator), then permanent death.
            ServerEnterUnconsciousState();
        }

        private void ServerEnterUnconsciousState()
        {
            bool offlineAuthority =
                NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsListening;
            if ((!IsServer && !offlineAuthority) || IsDowned)
            {
                return;
            }

            networkLifeState.Value = (int)MiniVanPlayerLifeState.Unconscious;
            networkHealth.Value = 0;
            float now = NetworkManager != null
                ? (float)NetworkManager.ServerTime.Time
                : Time.time;
            networkUnconsciousEndTime.Value = now + UnconsciousDurationSeconds;
            networkCorpseState.Value = (int)MiniVanCorpseState.World;
            networkCorpseCarrier.Value = NoCorpseCarrier;
            networkCorpseVehicle.Value = 0;
            networkCorpseSeat.Value = -1;
            networkCorpsePosition.Value = SnapCorpseToGround(transform.position);
            networkCorpseRotation.Value = GetLyingCorpseRotation(transform.eulerAngles.y);
            MiniVanVehicle[] vehicles = FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
            for (int i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i] != null)
                {
                    vehicles[i].ServerReleaseClientSeat(OwnerClientId);
                }
            }
        }

        private void ServerPromoteUnconsciousToDead()
        {
            if (!IsServer || !IsUnconscious)
            {
                return;
            }

            networkLifeState.Value = (int)MiniVanPlayerLifeState.Dead;
            networkUnconsciousEndTime.Value = 0f;
            // Keep corpse pose / carrier / seat as-is.
        }

        private void ServerReviveAt(Vector3 position)
        {
            ServerReviveAt(position, MaxPlayerHealth);
        }

        private void ServerReviveAt(Vector3 position, int healthAmount)
        {
            if (!IsServer || !IsDowned)
            {
                return;
            }

            MiniVanPlayer previousCarrier = networkCorpseCarrier.Value != NoCorpseCarrier
                ? FindPlayerByClientId(networkCorpseCarrier.Value)
                : null;
            if (previousCarrier != null)
            {
                previousCarrier.SetLocalCarriedCorpseClientRpc(
                    OwnerClientId,
                    false,
                    previousCarrier.BuildOwnerTarget());
            }

            networkLifeState.Value = (int)MiniVanPlayerLifeState.Alive;
            networkUnconsciousEndTime.Value = 0f;
            networkHealth.Value = Mathf.Clamp(healthAmount, 1, MaxPlayerHealth);
            networkCorpseState.Value = (int)MiniVanCorpseState.World;
            networkCorpseCarrier.Value = NoCorpseCarrier;
            networkCorpseVehicle.Value = 0;
            networkCorpseSeat.Value = -1;
            // networkPosition/networkRotation are Owner-writable only — owner applies via ClientRpc.
            CompleteReviveClientRpc(position, BuildOwnerTarget());
        }

        /// <summary>Defibrillator revive: unconscious only, 25% HP.</summary>
        public bool ServerReviveFromDefibrillator(Vector3 position)
        {
            if (!IsServer || !IsUnconscious)
            {
                return false;
            }

            int health = Mathf.Max(1, Mathf.RoundToInt(MaxPlayerHealth * 0.25f));
            ServerReviveAt(position, health);
            return true;
        }

        [ClientRpc]
        private void CompleteReviveClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner) return;

            // The corpse colliders live one more frame; they must not catch the stand-up probe.
            SetCorpseCollisionsEnabled(false);
            if (corpseVisual != null)
            {
                corpseVisual.SetActive(false);
            }

            Vector3 standPosition = ResolveReviveStandPosition(position);
            transform.SetPositionAndRotation(standPosition, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            verticalVelocity = 0f;
            if (CharacterController != null) CharacterController.enabled = true;
            PublishOwnedNetworkTransform(true);
        }

        /// <summary>
        /// Revive points are authored at ground level, while the controller pivot sits a full
        /// foot offset above the feet — placing the root straight on them buries the player.
        /// </summary>
        private Vector3 ResolveReviveStandPosition(Vector3 requestedPosition)
        {
            float footOffset = CharacterController != null
                ? CharacterController.height * 0.5f - CharacterController.center.y +
                  Mathf.Max(CharacterController.skinWidth, 0.025f)
                : 1f;

            float groundY = requestedPosition.y;
            Vector3 rayOrigin = requestedPosition + Vector3.up * 1.2f;
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null ||
                    hitCollider.transform.IsChildOf(transform) ||
                    hitCollider.GetComponentInParent<MiniVanPlayer>() != null ||
                    hitCollider.GetComponentInParent<MiniVanPlayerCorpseProxy>() != null ||
                    hits[i].normal.y < 0.3f)
                {
                    continue;
                }

                groundY = hits[i].point.y;
                break;
            }

            return new Vector3(requestedPosition.x, groundY + footOffset, requestedPosition.z);
        }

        private static MiniVanPlayer FindPlayerByClientId(ulong clientId)
        {
            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) if (players[i] != null && players[i].OwnerClientId == clientId) return players[i];
            return null;
        }

        private static MiniVanPlayer FindCarriedCorpse(ulong carrierClientId)
        {
            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsDowned && players[i].networkCorpseCarrier.Value == carrierClientId) return players[i];
            }
            return null;
        }

        private MiniVanReviveStation FindNearbyReviveStation(bool paidRevive)
        {
            MiniVanReviveStationKind kind = paidRevive ? MiniVanReviveStationKind.CitySeller : MiniVanReviveStationKind.DoctorTable;
            MiniVanReviveStation[] stations = FindObjectsByType<MiniVanReviveStation>(FindObjectsSortMode.None);
            MiniVanReviveStation best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < stations.Length; i++)
            {
                MiniVanReviveStation station = stations[i];
                if (station == null || station.Kind != kind) continue;
                float distance = Vector3.Distance(transform.position, station.transform.position);
                if (distance <= station.UseRadius && distance < bestDistance)
                {
                    best = station;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static MiniVanVehicle FindVehicle(ulong networkObjectId)
        {
            MiniVanVehicle[] vehicles = FindObjectsByType<MiniVanVehicle>(FindObjectsSortMode.None);
            for (int i = 0; i < vehicles.Length; i++) if (vehicles[i] != null && vehicles[i].NetworkObjectId == networkObjectId) return vehicles[i];
            return null;
        }

        public static bool IsCorpseSeatOccupied(MiniVanVehicle vehicle, int seatIndex)
        {
            return GetCorpseInSeat(vehicle, seatIndex) != null;
        }

        public static MiniVanPlayer GetCorpseInSeat(MiniVanVehicle vehicle, int seatIndex)
        {
            if (vehicle == null) return null;
            MiniVanPlayer[] players = FindObjectsByType<MiniVanPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                MiniVanPlayer player = players[i];
                if (player != null && player.IsDowned && player.networkCorpseState.Value == (int)MiniVanCorpseState.PassengerSeat &&
                    player.networkCorpseVehicle.Value == vehicle.NetworkObjectId && player.networkCorpseSeat.Value == seatIndex) return player;
            }
            return null;
        }

        private bool TryResolveTestBatonPlayerHit(Vector3 origin, Vector3 direction, float radius, float range)
        {
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, range, ~0, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            MiniVanPlayer target = null;
            for (int i = 0; i < hits.Length; i++)
            {
                MiniVanPlayer candidate = hits[i].collider != null ? hits[i].collider.GetComponentInParent<MiniVanPlayer>() : null;
                if (candidate != null && candidate != this && !candidate.IsDowned && hits[i].distance < bestDistance)
                {
                    target = candidate;
                    bestDistance = hits[i].distance;
                }
            }
            if (target == null) return false;
            target.ReceiveZombieDamageServer(MaxPlayerHealth);
            return true;
        }

        private void DrawDeathSystemGui()
        {
            if (!IsOwner) return;
            if (IsUnconscious)
            {
                float remaining = UnconsciousSecondsRemaining;
                float width = 320f;
                float height = 22f;
                Rect back = new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height - 140f, width, height);
                GUI.Box(back, GUIContent.none);
                float t = Mathf.Clamp01(remaining / UnconsciousDurationSeconds);
                Rect fill = new Rect(back.x + 3f, back.y + 3f, (back.width - 6f) * t, back.height - 6f);
                Color old = GUI.color;
                GUI.color = new Color(0.95f, 0.35f, 0.2f, 0.95f);
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 13
                };
                style.normal.textColor = Color.white;
                GUI.Label(back, "UNCONSCIOUS  " + Mathf.CeilToInt(remaining).ToString() + "s  — wait for defibrillator", style);
                GUI.color = old;
            }
            else if (IsPermanentlyDead)
            {
                GUI.color = new Color(1f, 0.32f, 0.25f, 1f);
                GUI.Box(new Rect(Screen.width * 0.5f - 190f, 70f, 380f, 86f),
                    "YOU ARE DEAD\nPress R to revive\nOr wait for a teammate to use a doctor or city seller");
                GUI.color = Color.white;
            }
            else if (locallyCarriedCorpse != null)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 175f, 70f, 350f, 58f), "Carrying a player body: E - seat/revive, Q - drop");
            }
        }
    }
}
