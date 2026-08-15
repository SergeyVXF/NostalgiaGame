using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanVehicleHood : MonoBehaviour, IMiniVanGameModeInteractable
    {
        public MiniVanVehicle Vehicle;
        public float InteractRadius = 2.8f;
        public float ClosedX = 90f;
        public float OpenX = -47.3f;
        public float AnimationSpeed = 9f;
        public Vector3 LocalHingeOffset = new Vector3(0f, 0f, -0.5f);
        public bool InteractionColliderIsTrigger = true;

        private Vector3 closedLocalPosition;
        private Quaternion closedLocalRotation;
        private bool capturedClosedPose;

        private void Awake()
        {
            EnsureSetup();
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

            bool open = Vehicle != null && Vehicle.FrontCapotOpen.Value;
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

            if (GetComponent<Collider>() == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.size = Vector3.one;
                box.center = Vector3.zero;
            }

            Collider[] hoodColliders = GetComponents<Collider>();
            for (int i = 0; i < hoodColliders.Length; i++)
            {
                if (hoodColliders[i] != null)
                {
                    hoodColliders[i].isTrigger = InteractionColliderIsTrigger;
                }
            }
        }

        public string GetPrompt(MiniVanPlayer player)
        {
            if (!CanPlayerOperateHood(player))
            {
                return string.Empty;
            }

            bool open = Vehicle != null && Vehicle.FrontCapotOpen.Value;
            return open ? "E - close hood" : "E - open hood";
        }

        public void Interact(MiniVanPlayer player)
        {
            if (Input.GetMouseButton(1))
            {
                return;
            }

            if (!CanPlayerOperateHood(player))
            {
                return;
            }

            if (Vehicle.IsSpawned)
            {
                Vehicle.RequestToggleFrontCapotServerRpc();
            }
            else
            {
                Vehicle.SetFrontCapotOpenLocal(!Vehicle.FrontCapotOpen.Value);
            }
        }

        public void PrimaryAction(MiniVanPlayer player)
        {
        }

        private bool CanPlayerOperateHood(MiniVanPlayer player)
        {
            if (Vehicle == null || player == null)
            {
                return false;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > InteractRadius)
            {
                return false;
            }

            // Hood is only operable from outside — never from the cabin / seats.
            return !Vehicle.IsPlayerInsideCabinForHood(player);
        }

        private void CaptureClosedPose()
        {
            Vector3 currentEuler = transform.localEulerAngles;
            closedLocalRotation = Quaternion.Euler(ClosedX, currentEuler.y, currentEuler.z);
            closedLocalPosition = transform.localPosition;

            if (Mathf.Abs(Mathf.DeltaAngle(currentEuler.x, ClosedX)) > 8f)
            {
                Quaternion currentRotation = transform.localRotation;
                Vector3 hingeOffset = closedLocalRotation * GetHingeOffsetInParentLocal();
                Quaternion rotationFromClosed = currentRotation * Quaternion.Inverse(closedLocalRotation);
                closedLocalPosition = transform.localPosition - hingeOffset + rotationFromClosed * hingeOffset;
            }

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

            Quaternion targetLocalRotation = Quaternion.Euler(OpenX, closedLocalRotation.eulerAngles.y, closedLocalRotation.eulerAngles.z);
            Vector3 hingePosition = closedLocalPosition + closedLocalRotation * GetHingeOffsetInParentLocal();
            Vector3 centerFromHinge = closedLocalPosition - hingePosition;

            targetPosition = hingePosition + targetLocalRotation * Quaternion.Inverse(closedLocalRotation) * centerFromHinge;
            targetRotation = targetLocalRotation;
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
    }
}
