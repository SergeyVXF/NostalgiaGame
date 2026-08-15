using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private const float TraceKnockdownSeconds = 5f;
        private const float KnockdownFallSeconds = 0.38f;
        private const float KnockdownCameraFloorY = 0.22f;
        private const float KnockdownSkyPitch = -78f;

        private float knockdownUntil;
        private float knockdownStartedAt;
        private float knockdownPitchBefore;
        private bool knockdownVisualApplied;
        private Vector3 knockdownStandPosition;

        public bool IsKnockedDown => Time.time < knockdownUntil && !IsDowned;

        public void ServerApplyTracePounce(int damage, Vector3 hitOrigin)
        {
            bool offlineAuthority =
                NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsListening;
            if ((!IsServer && !offlineAuthority) || IsDowned)
            {
                return;
            }

            ReceiveNonLethalZombieDamageServer(Mathf.Max(1, damage));
            BeginKnockdown(hitOrigin);
            if (IsSpawned && IsServer)
            {
                TraceKnockdownClientRpc(hitOrigin);
            }
        }

        [ClientRpc]
        private void TraceKnockdownClientRpc(Vector3 hitOrigin)
        {
            if (IsServer)
            {
                return;
            }

            BeginKnockdown(hitOrigin);
        }

        /// <summary>
        /// Trace pounce knocks the player down but never puts them into shock or death.
        /// Health can drop, but it stays at least 1 so other enemies still treat them as a live target.
        /// </summary>
        private void ReceiveNonLethalZombieDamageServer(int amount)
        {
            bool offlineAuthority =
                NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsListening;
            if ((!IsServer && !offlineAuthority) || IsDowned || networkHealth.Value <= 0)
            {
                return;
            }

            networkHealth.Value = Mathf.Max(1, networkHealth.Value - Mathf.Max(1, amount));
            ZombieDamageFeedbackClientRpc(BuildOwnerTarget());
        }

        private void BeginKnockdown(Vector3 hitOrigin)
        {
            if (IsDowned)
            {
                return;
            }

            knockdownUntil = Time.time + TraceKnockdownSeconds;
            knockdownStartedAt = Time.time;
            knockdownPitchBefore = pitch;
            knockdownStandPosition = transform.position;
            verticalVelocity = 0f;
            // Keep CharacterController on so other enemies still have a body to see and bite.

            ApplyKnockdownVisual(true);
        }

        private bool UpdateKnockdown()
        {
            if (IsDowned)
            {
                if (knockdownVisualApplied)
                {
                    ApplyKnockdownVisual(false);
                    RestoreKnockdownCamera();
                }

                knockdownUntil = 0f;
                return false;
            }

            if (!IsKnockedDown)
            {
                if (knockdownVisualApplied)
                {
                    FinishKnockdown();
                }

                return false;
            }

            if (IsOwner)
            {
                transform.position = knockdownStandPosition;
            }

            ApplyKnockdownVisual(true);
            ApplyKnockdownFallPose();
            return true;
        }

        private float KnockdownFallAmount()
        {
            float u = Mathf.Clamp01((Time.time - knockdownStartedAt) / Mathf.Max(0.05f, KnockdownFallSeconds));
            return u * u;
        }

        private void ApplyKnockdownFallPose()
        {
            float drop = KnockdownFallAmount();
            if (playerVisualRoot != null)
            {
                playerVisualRoot.transform.localRotation = Quaternion.Slerp(
                    Quaternion.identity,
                    LyingVisualLocalRotation,
                    drop);
                playerVisualRoot.transform.localPosition = PlayerVisualLocalPosition +
                    Vector3.Lerp(Vector3.zero, new Vector3(0f, 0.15f, 0.35f), drop);
            }

            if (!IsOwner || CameraRoot == null)
            {
                return;
            }

            Vector3 stand = hasInitialFirstPersonCameraPose
                ? initialFirstPersonCameraLocalPosition
                : CameraRoot.localPosition;
            Vector3 fallen = stand;
            fallen.y = KnockdownCameraFloorY;
            CameraRoot.localPosition = Vector3.Lerp(stand, fallen, drop);

            pitch = Mathf.Lerp(knockdownPitchBefore, KnockdownSkyPitch, drop);
            float roll = Mathf.Lerp(0f, 12f, drop);
            CameraRoot.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        private void RestoreKnockdownCamera()
        {
            if (CameraRoot == null)
            {
                return;
            }

            if (hasInitialFirstPersonCameraPose)
            {
                CameraRoot.localPosition = initialFirstPersonCameraLocalPosition;
            }

            pitch = knockdownPitchBefore;
            CameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void ApplyKnockdownVisual(bool down)
        {
            EnsurePlayerVisual();
            if (playerVisualRoot == null)
            {
                return;
            }

            if (down)
            {
                if (!knockdownVisualApplied)
                {
                    SnapAnimatorToIdlePose();
                    knockdownVisualApplied = true;
                }

                if (playerAnimator != null)
                {
                    playerAnimator.enabled = false;
                }

                return;
            }

            knockdownVisualApplied = false;
            if (playerAnimator != null)
            {
                playerAnimator.enabled = true;
                SnapAnimatorToIdlePose();
            }

            playerVisualRoot.transform.localPosition = PlayerVisualLocalPosition;
            playerVisualRoot.transform.localRotation = Quaternion.identity;
        }

        private void FinishKnockdown()
        {
            ApplyKnockdownVisual(false);
            RestoreKnockdownCamera();
            knockdownUntil = 0f;
            if (!IsOwner)
            {
                return;
            }

            Vector3 stand = ResolveReviveStandPosition(knockdownStandPosition);
            transform.SetPositionAndRotation(stand, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            verticalVelocity = 0f;
            if (CharacterController != null)
            {
                CharacterController.enabled = true;
            }

            PublishOwnedNetworkTransform(true);
        }
    }
}
