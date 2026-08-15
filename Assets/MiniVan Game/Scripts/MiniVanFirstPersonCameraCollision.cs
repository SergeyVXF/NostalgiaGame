using UnityEngine;

namespace MiniVanGame
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class MiniVanFirstPersonCameraCollision : MonoBehaviour
    {
        public Transform CameraRoot;
        public CharacterController CharacterController;
        public Vector3 FirstPersonLocalPosition = new Vector3(0f, 0.565f, 0.26f);
        [Min(0.01f)] public float ProbeRadius = 0.08f;
        [Min(0.005f)] public float WallPadding = 0.035f;
        [Min(0.01f)] public float NearClipPlane = 0.04f;
        [Min(0.5f)] public float FirstPersonPoseThreshold = 0.9f;
        public LayerMask CollisionMask = ~0;

        private Camera playerCamera;
        private MiniVanPlayer ownerPlayer;

        private void Awake()
        {
            ResolveReferences();
            ApplyNearClip();
        }

private void LateUpdate()
        {
            ResolveReferences();
            if (CameraRoot == null)
            {
                return;
            }

            // Third-person board/poop cameras intentionally stay outside this first-person clamp.
            if (CameraRoot.localPosition.sqrMagnitude > FirstPersonPoseThreshold * FirstPersonPoseThreshold)
            {
                return;
            }

            Transform player = CharacterController != null ? CharacterController.transform : transform;
            Vector3 anchorLocal = new Vector3(0f, FirstPersonLocalPosition.y, 0f);
            // The capsule stays upright; the surface lean and step smoothing live on the eye.
            Vector3 anchorWorld = ownerPlayer != null
                ? ownerPlayer.GetVisualEyeWorldPosition(anchorLocal)
                : player.TransformPoint(anchorLocal);
            Vector3 desiredWorld = ownerPlayer != null
                ? ownerPlayer.GetVisualEyeWorldPosition(FirstPersonLocalPosition)
                : player.TransformPoint(FirstPersonLocalPosition);
            Vector3 delta = desiredWorld - anchorWorld;
            float distance = delta.magnitude;

            if (distance <= 0.0001f)
            {
                CameraRoot.position = anchorWorld;
                return;
            }

            Vector3 direction = delta / distance;
            float allowedDistance = distance;
            RaycastHit[] hits = Physics.SphereCastAll(
                anchorWorld,
                ProbeRadius,
                direction,
                distance + WallPadding,
                CollisionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(player))
                {
                    continue;
                }

                // The van is itself a moving reference frame. Clamping the camera against its
                // interpolated colliders causes a one-frame oscillation while riding inside.
                // CharacterController and cabin collision still keep the player inside.
                if (hitCollider.GetComponentInParent<MiniVanVehicle>() != null)
                {
                    continue;
                }

                allowedDistance = Mathf.Min(
                    allowedDistance,
                    Mathf.Max(0f, hits[i].distance - WallPadding));
            }

            CameraRoot.position = anchorWorld + direction * allowedDistance;
            ApplyNearClip();
        }

        private void ResolveReferences()
        {
            MiniVanPlayer player = ownerPlayer != null ? ownerPlayer : GetComponent<MiniVanPlayer>();
            ownerPlayer = player;
            if (player != null)
            {
                CameraRoot = CameraRoot != null ? CameraRoot : player.CameraRoot;
                CharacterController = CharacterController != null
                    ? CharacterController
                    : player.CharacterController;
                playerCamera = playerCamera != null ? playerCamera : player.PlayerCamera;
            }

            if (playerCamera == null && CameraRoot != null)
            {
                playerCamera = CameraRoot.GetComponentInChildren<Camera>(true);
            }
        }

        private void ApplyNearClip()
        {
            if (playerCamera != null)
            {
                playerCamera.nearClipPlane = Mathf.Max(0.01f, NearClipPlane);
            }
        }
    }
}
