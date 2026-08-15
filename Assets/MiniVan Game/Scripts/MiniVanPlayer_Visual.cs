using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        public const string PlayerHeadObjectName = "Head";
        public const string PlayerRightHandObjectName = "RightHand";

        [Header("Player Visual")]
        public GameObject PlayerVisualPrefab;
        public Vector3 PlayerVisualLocalPosition = new Vector3(0f, -0.99f, 0f);
        public Vector3 PlayerSitVisualOffset = new Vector3(0f, -0.18f, 0.08f);
        public float PlayerVisualUniformScale = 1.234568f;

        [Header("Held Items (RightHand local)")]
        [Tooltip("Tune in Play Mode on MiniVanPlayer, then copy values onto the MiniVanPlayer prefab. Menu: MiniVan Game/Characters/Held Item Offsets.")]
        public Vector3 BatHandLocalPosition = new Vector3(0.04f, 0.05f, 0.02f);
        public Vector3 BatHandLocalRotation = new Vector3(12f, 8f, -48f);
        public Vector3 StakeHandLocalPosition = new Vector3(0f, 0.02f, 0f);
        public Vector3 StakeHandLocalRotation = new Vector3(90f, 0f, 0f);
        [Tooltip("Cross in the hand while walking / not using LMB.")]
        public Vector3 CrossHandLocalPosition = new Vector3(0f, 0.04f, 0.02f);
        public Vector3 CrossHandLocalRotation = new Vector3(0f, 0f, 0f);
        [Tooltip("Cross in the hand while LMB is held (arm raised forward).")]
        public Vector3 CrossHandRaisedPosition = new Vector3(0f, 0.04f, 0.02f);
        public Vector3 CrossHandRaisedRotation = new Vector3(85f, 0f, 0f);

        private GameObject playerVisualRoot;
        private Animator playerAnimator;
        private Transform playerRightHand;
        private Transform playerHeadTransform;
        private Renderer[] playerSkinRenderers;
        private Renderer[] playerArmRenderers;
        private Renderer[] playerHiddenInFirstPerson;
        private Renderer playerCapsuleRenderer;
        private Vector3 lastVisualNetworkPosition;
        private bool hasLastVisualNetworkPosition;
        private bool playerVisualReady;
        private bool playerVisualFrozenForDeath;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimSit = Animator.StringToHash("Sit");
        private static readonly int AnimHoldItem = Animator.StringToHash("HoldItem");
        private static readonly int AnimHoldCross = Animator.StringToHash("HoldCross");
        private static readonly int AnimDriveSit = Animator.StringToHash("DriveSit");
        private static readonly int AnimBatSwing = Animator.StringToHash("BatSwing");
        private static readonly int AnimStakeStab = Animator.StringToHash("StakeStab");

        private void EnsurePlayerVisual()
        {
            if (playerVisualReady && playerVisualRoot != null)
            {
                return;
            }

            playerCapsuleRenderer = GetComponent<MeshRenderer>();
            if (playerCapsuleRenderer != null)
            {
                playerCapsuleRenderer.enabled = false;
                playerCapsuleRenderer.forceRenderingOff = true;
            }

            if (playerVisualRoot == null)
            {
                if (PlayerVisualPrefab == null)
                {
                    PlayerVisualPrefab = Resources.Load<GameObject>("MiniVan/Player/MiniVanPlayerVisual");
                }

                if (PlayerVisualPrefab == null)
                {
                    return;
                }

                playerVisualRoot = Instantiate(PlayerVisualPrefab, transform);
                playerVisualRoot.name = "PlayerVisual";
                playerVisualRoot.transform.localPosition = PlayerVisualLocalPosition;
                playerVisualRoot.transform.localRotation = Quaternion.identity;
                playerVisualRoot.transform.localScale = Vector3.one * Mathf.Max(0.01f, PlayerVisualUniformScale);
            }

            playerAnimator = playerVisualRoot.GetComponentInChildren<Animator>(true);
            if (playerAnimator != null)
            {
                playerAnimator.applyRootMotion = false;
                playerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            playerRightHand = FindChildByName(playerVisualRoot.transform, PlayerRightHandObjectName);
            playerHeadTransform = FindHeadMeshTransform(playerVisualRoot.transform);
            CachePlayerVisualRenderers();
            playerVisualReady = playerVisualRoot != null;
            ApplyFirstPersonVisibility(IsOwner && !IsFacePainting());
            ApplyPlayerSkinColor();
        }

        private void CachePlayerVisualRenderers()
        {
            if (playerVisualRoot == null)
            {
                return;
            }

            Renderer[] all = playerVisualRoot.GetComponentsInChildren<Renderer>(true);
            System.Collections.Generic.List<Renderer> skin = new System.Collections.Generic.List<Renderer>(8);
            System.Collections.Generic.List<Renderer> arms = new System.Collections.Generic.List<Renderer>(8);
            System.Collections.Generic.List<Renderer> hidden = new System.Collections.Generic.List<Renderer>(12);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer renderer = all[i];
                if (renderer == null)
                {
                    continue;
                }

                string name = renderer.gameObject.name;
                bool isArm = name.IndexOf("Arm", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Sleeve", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Forearm", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool isSkin = name.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Forearm", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Shin", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isSkin)
                {
                    skin.Add(renderer);
                }

                if (isArm)
                {
                    arms.Add(renderer);
                }
                else
                {
                    hidden.Add(renderer);
                }
            }

            playerSkinRenderers = skin.ToArray();
            playerArmRenderers = arms.ToArray();
            playerHiddenInFirstPerson = hidden.ToArray();
        }

        /// <summary>
        /// Freezing the animator keeps whatever limb pose the walk cycle was on, so the body has to
        /// be evaluated back into the idle state before (and after) the death freeze.
        /// </summary>
        private void SnapAnimatorToIdlePose()
        {
            if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            bool wasEnabled = playerAnimator.enabled;
            playerAnimator.enabled = true;
            playerAnimator.Rebind();
            playerAnimator.SetFloat(AnimSpeed, 0f);
            playerAnimator.SetBool(AnimSit, false);
            playerAnimator.SetBool(AnimHoldItem, false);
            playerAnimator.SetBool(AnimHoldCross, false);
            playerAnimator.SetBool(AnimDriveSit, false);
            if (playerAnimator.layerCount > 1)
            {
                playerAnimator.SetLayerWeight(1, 0f);
            }

            playerAnimator.Update(0f);
            playerAnimator.enabled = wasEnabled;
        }

        private void UpdatePlayerVisual()
        {
            EnsurePlayerVisual();
            if (!playerVisualReady || playerAnimator == null)
            {
                return;
            }

            if (IsDowned)
            {
                if (!playerVisualFrozenForDeath)
                {
                    playerVisualFrozenForDeath = true;
                    SnapAnimatorToIdlePose();
                }

                playerAnimator.enabled = false;
                return;
            }

            if (IsKnockedDown)
            {
                return;
            }

            if (playerVisualFrozenForDeath)
            {
                playerVisualFrozenForDeath = false;
                playerAnimator.enabled = true;
                SnapAnimatorToIdlePose();
            }
            else if (!playerAnimator.enabled)
            {
                playerAnimator.enabled = true;
            }

            bool sitting = currentSeat != null;
            playerVisualRoot.transform.localPosition = sitting
                ? PlayerVisualLocalPosition + PlayerSitVisualOffset
                : PlayerVisualLocalPosition;

            float speed = 0f;
            if (!sitting)
            {
                if (IsOwner && CharacterController != null)
                {
                    Vector3 planar = CharacterController.velocity;
                    planar.y = 0f;
                    speed = planar.magnitude;
                }
                else
                {
                    Vector3 pos = transform.position;
                    if (hasLastVisualNetworkPosition)
                    {
                        Vector3 delta = pos - lastVisualNetworkPosition;
                        delta.y = 0f;
                        speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                    }

                    lastVisualNetworkPosition = pos;
                    hasLastVisualNetworkPosition = true;
                }
            }

            MiniVanInventoryItem selected = GetInventorySlot(IsOwner ? localSelectedSlot : networkSelectedSlot.Value);
            bool holdCross = selected == MiniVanInventoryItem.HolyCross && !sitting && IsHolyCrossRepelling;
            bool holdItem = !sitting && !holdCross && (IsBatWeapon(selected) || selected == MiniVanInventoryItem.AspenStake);
            bool driveSit = sitting && currentSeat != null && currentSeat.IsDriverSeat;

            playerAnimator.SetFloat(AnimSpeed, speed);
            playerAnimator.SetBool(AnimSit, sitting);
            playerAnimator.SetBool(AnimHoldCross, holdCross);
            playerAnimator.SetBool(AnimHoldItem, holdItem);
            playerAnimator.SetBool(AnimDriveSit, driveSit);

            bool upperActive = holdItem || holdCross || driveSit || batSwingTimer > 0f || aspenStakeThrustTimer > 0f;
            if (playerAnimator.layerCount > 1)
            {
                playerAnimator.SetLayerWeight(1, upperActive ? 1f : 0f);
            }
        }

        private void PlayPlayerBatSwingAnimation()
        {
            EnsurePlayerVisual();
            if (playerAnimator != null)
            {
                playerAnimator.SetTrigger(AnimBatSwing);
            }
        }

        private void PlayPlayerStakeStabAnimation()
        {
            EnsurePlayerVisual();
            if (playerAnimator != null)
            {
                playerAnimator.SetTrigger(AnimStakeStab);
            }
        }

        private Transform GetHeldItemParent()
        {
            EnsurePlayerVisual();
            if (playerRightHand != null)
            {
                return playerRightHand;
            }

            return CameraRoot != null ? CameraRoot : transform;
        }

        private bool UsesSkeletalHeldItems()
        {
            EnsurePlayerVisual();
            return playerRightHand != null;
        }

        public Transform GetFacePaintHeadTransform()
        {
            EnsurePlayerVisual();
            return playerHeadTransform;
        }

        public MeshFilter GetFacePaintMeshFilter()
        {
            EnsurePlayerVisual();
            if (playerHeadTransform == null)
            {
                return GetComponent<MeshFilter>();
            }

            return playerHeadTransform.GetComponent<MeshFilter>();
        }

        public MeshRenderer GetFacePaintMeshRenderer()
        {
            EnsurePlayerVisual();
            if (playerHeadTransform == null)
            {
                return GetComponent<MeshRenderer>();
            }

            return playerHeadTransform.GetComponent<MeshRenderer>();
        }

        private void ApplyFirstPersonVisibility(bool firstPerson)
        {
            EnsurePlayerVisual();
            if (playerCapsuleRenderer != null)
            {
                playerCapsuleRenderer.enabled = false;
                playerCapsuleRenderer.forceRenderingOff = true;
            }

            if (playerHiddenInFirstPerson == null)
            {
                return;
            }

            for (int i = 0; i < playerHiddenInFirstPerson.Length; i++)
            {
                if (playerHiddenInFirstPerson[i] != null)
                {
                    playerHiddenInFirstPerson[i].enabled = !firstPerson;
                }
            }

            if (playerArmRenderers == null)
            {
                return;
            }

            for (int i = 0; i < playerArmRenderers.Length; i++)
            {
                if (playerArmRenderers[i] != null)
                {
                    playerArmRenderers[i].enabled = true;
                }
            }
        }

        private void ApplyPlayerSkinColor(Color? overrideColor = null)
        {
            if (IsFacePainting() && !overrideColor.HasValue)
            {
                return;
            }

            EnsurePlayerVisual();
            Color color = overrideColor ?? (networkAvatarIndex.Value >= 0
                ? MiniVanAvatarCatalog.GetBodyColor(networkAvatarIndex.Value)
                : VectorToColor(networkHatColor.Value));

            if (playerSkinRenderers == null)
            {
                return;
            }

            for (int i = 0; i < playerSkinRenderers.Length; i++)
            {
                Renderer renderer = playerSkinRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (facePaintMaterial != null && renderer.sharedMaterial == facePaintMaterial)
                {
                    continue;
                }

                Material material = renderer.material;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                material.color = color;
            }
        }

        public Transform GetPlayerVisualRoot()
        {
            EnsurePlayerVisual();
            return playerVisualRoot != null ? playerVisualRoot.transform : null;
        }

        public Transform GetPlayerHeadBone()
        {
            Transform root = GetPlayerVisualRoot();
            return root != null ? root.Find("Body/Head") : null;
        }

        private static Transform FindHeadMeshTransform(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] != null && filters[i].gameObject.name == PlayerHeadObjectName)
                {
                    return filters[i].transform;
                }
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
