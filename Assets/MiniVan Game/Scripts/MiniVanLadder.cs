// Procedural vertical ladder engagement.
using UnityEngine;

namespace MiniVanGame
{
    [RequireComponent(typeof(BoxCollider))]
    public class MiniVanLadder : MonoBehaviour
    {
        public float ClimbSpeed = 3.2f;
        public float RoofEntryHeight = 3.25f;
        public float RoofEntryPushSpeed = 1.2f;
        [Min(0.1f)] public float EngageHalfWidth = 1.05f;
        [Min(0.1f)] public float EngageDepth = 1.05f;

        public Vector3 RoofEntryLocalDirection = Vector3.left;

        [Tooltip("Legacy hard magnet (unused by soft climb). Kept for prefab compatibility.")]
        public float StickToLadderStrength = 6f;

        [Tooltip("Local direction from the ladder toward the climber (away from the wall). " +
                 "If zero: vehicle ladders use outward-from-van; others use opposite of Roof Entry.")]
        public Vector3 ClimbStandOffLocalDirection = Vector3.zero;

        [Tooltip("Preferred gap in front of the rails while climbing (soft target, not a hard rail).")]
        [Min(0f)] public float ClimbStandOffDistance = 0.4f;

        [Tooltip("How strongly the climber is eased toward the preferred stand-off (HL2 soft stick).")]
        [Min(0f)] public float ClimbSoftStickStrength = 5.5f;

        [Tooltip("Free planar radius around the preferred hold before soft stick engages.")]
        [Min(0f)] public float ClimbLateralDeadzone = 0.28f;

        [Tooltip("Hard planar clamp so the climber cannot drift out of the ladder volume.")]
        [Min(0.05f)] public float ClimbMaxLateralDrift = 0.65f;

        [Tooltip("Standalone ladders: at the top, blend the climber onto the landing behind the " +
                 "rails instead of shoving with RoofEntryPushSpeed. Leave off for van/Panelka " +
                 "ladders that rely on the legacy push.")]
        public bool AutoTopExit;

        [Tooltip("How far past the rails (along RoofEntryDirection) the auto top exit looks for " +
                 "the landing and places the climber.")]
        [Min(0.2f)] public float TopExitForward = 0.95f;

        [Tooltip("Climb volume. If empty, a ClimbTrigger child is created at runtime.")]
        [SerializeField] private BoxCollider climbTrigger;

        [Tooltip("When enabled, ClimbTrigger center/size are copied from the root BoxCollider. Turn OFF to edit and save ClimbTrigger by hand.")]
        [SerializeField] private bool syncClimbTriggerFromRoot = true;

        private BoxCollider[] solidBlockers;

        public Vector3 ClimbDirection => transform.up;
        public Vector3 RoofEntryDirection => transform.TransformDirection(RoofEntryLocalDirection.normalized);

        private void Awake()
        {
            SyncClimbVolume();
        }

        private void OnValidate()
        {
            SyncClimbVolume();
        }

        public void SyncClimbVolume()
        {
            // Older baked Panelka ladders still carry the narrow van-era engage box; bump so
            // the player can re-grab from a roof/floor exit and start descending.
            if (EngageHalfWidth < 1.05f)
            {
                EngageHalfWidth = 1.05f;
            }

            if (EngageDepth < 1.05f)
            {
                EngageDepth = 1.05f;
            }

            EnsureClimbTrigger();
            CacheSolidBlockers();
        }

        /// <summary>
        /// Authoring hook for hand-built ladders (see <see cref="MiniVanUniversalLadder"/>):
        /// points the component at an explicit climb volume and stops OnValidate from
        /// re-deriving that volume from the root box.
        /// </summary>
        public void SetAuthoredClimbTrigger(BoxCollider trigger)
        {
            if (trigger == null)
            {
                return;
            }

            climbTrigger = trigger;
            syncClimbTriggerFromRoot = false;
            SyncClimbVolume();
        }

        public bool ContainsPoint(Vector3 worldPoint)
        {
            BoxCollider box = climbTrigger != null ? climbTrigger : GetComponent<BoxCollider>();
            if (box == null)
            {
                return false;
            }

            Vector3 local = transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        public bool CanEngage(Vector3 worldPoint)
        {
            BoxCollider box = climbTrigger != null ? climbTrigger : GetComponent<BoxCollider>();
            if (box == null)
            {
                return false;
            }

            Vector3 local = box.transform.InverseTransformPoint(worldPoint);
            Vector3 center = box.center;
            float minimumY = center.y - box.size.y * 0.5f - 0.1f;
            float maximumY = center.y + box.size.y * 0.5f + 0.1f;
            return local.y >= minimumY && local.y <= maximumY &&
                   Mathf.Abs(local.x - center.x) <= EngageHalfWidth &&
                   Mathf.Abs(local.z - center.z) <= EngageDepth;
        }

        /// <summary>
        /// World direction from the ladder toward open space where the climber should stand.
        /// Uses the ladder's thin horizontal axis (not diagonal "away from van"), so the player
        /// stays centered in front of the rungs instead of drifting to the side.
        /// </summary>
        public Vector3 GetClimbFaceDirection()
        {
            Vector3 up = transform.up.sqrMagnitude > 0.0001f ? transform.up.normalized : Vector3.up;

            if (ClimbStandOffLocalDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 authored = Vector3.ProjectOnPlane(
                    transform.TransformDirection(ClimbStandOffLocalDirection.normalized),
                    up);
                if (authored.sqrMagnitude > 0.0001f)
                {
                    return authored.normalized;
                }
            }

            Vector3 preferred = Vector3.zero;
            MiniVanVehicle vehicle = GetComponentInParent<MiniVanVehicle>();
            if (vehicle != null)
            {
                preferred = Vector3.ProjectOnPlane(
                    transform.position - vehicle.transform.position,
                    up);
            }

            if (preferred.sqrMagnitude < 0.0001f && RoofEntryLocalDirection.sqrMagnitude > 0.0001f)
            {
                preferred = Vector3.ProjectOnPlane(
                    transform.TransformDirection((-RoofEntryLocalDirection).normalized),
                    up);
            }

            BoxCollider box = climbTrigger != null ? climbTrigger : GetComponent<BoxCollider>();
            Vector3 localAxis = Vector3.right;
            if (box != null)
            {
                // Thin horizontal side of the climb volume = depth into/out of the wall.
                localAxis = Mathf.Abs(box.size.x) <= Mathf.Abs(box.size.z)
                    ? Vector3.right
                    : Vector3.forward;
            }

            Vector3 axis = Vector3.ProjectOnPlane(transform.TransformDirection(localAxis), up);
            if (axis.sqrMagnitude < 0.0001f)
            {
                axis = Vector3.ProjectOnPlane(transform.right, up);
            }

            if (axis.sqrMagnitude < 0.0001f)
            {
                return transform.forward;
            }

            axis.Normalize();
            if (preferred.sqrMagnitude > 0.0001f && Vector3.Dot(axis, preferred) < 0f)
            {
                axis = -axis;
            }

            return axis;
        }

        /// <summary>
        /// World origin of the climb axis (solid rails when present, else climb trigger).
        /// </summary>
        public Vector3 GetClimbAxisOrigin()
        {
            BoxCollider root = GetComponent<BoxCollider>();
            if (root != null && root != climbTrigger)
            {
                return root.transform.TransformPoint(root.center);
            }

            BoxCollider box = climbTrigger != null ? climbTrigger : root;
            return box != null ? box.transform.TransformPoint(box.center) : transform.position;
        }

        /// <summary>
        /// Preferred hold point in front of the rails (same height as <paramref name="worldPoint"/>).
        /// Soft climb eases toward this; it is not a hard snap target.
        /// </summary>
        public Vector3 GetClimbHoldPosition(Vector3 worldPoint)
        {
            BoxCollider heightBox = climbTrigger != null ? climbTrigger : GetComponent<BoxCollider>();
            Vector3 up = transform.up.sqrMagnitude > 0.0001f ? transform.up.normalized : Vector3.up;
            Vector3 face = GetClimbFaceDirection();
            Vector3 axisOrigin = GetClimbAxisOrigin();
            float along = Vector3.Dot(worldPoint - axisOrigin, up);
            if (heightBox != null)
            {
                Vector3 heightOrigin = heightBox.transform.TransformPoint(heightBox.center);
                float halfHeight = Mathf.Abs(heightBox.size.y) * 0.5f;
                float alongFromTrigger = Vector3.Dot(worldPoint - heightOrigin, up);
                along = Mathf.Clamp(alongFromTrigger, -halfHeight - 0.15f, halfHeight + 0.35f);
                // Keep along measured from the rail axis so stand-off stays in front of solids.
                along = Vector3.Dot(heightOrigin + up * along - axisOrigin, up);
            }

            return axisOrigin + up * along + face * Mathf.Max(0f, ClimbStandOffDistance);
        }

        public Vector3 GetStickCorrection(Vector3 worldPoint)
        {
            Vector3 target = GetClimbHoldPosition(worldPoint);
            Vector3 delta = Vector3.ProjectOnPlane(target - worldPoint, transform.up);
            return delta * StickToLadderStrength;
        }

        /// <summary>
        /// HL2/Minecraft-style: free motion inside a planar deadzone, gentle pull toward the
        /// preferred stand-off, hard clamp only past max drift.
        /// </summary>
        public Vector3 SoftConstrainClimbPosition(Vector3 worldPoint, float deltaTime)
        {
            Vector3 up = transform.up.sqrMagnitude > 0.0001f ? transform.up.normalized : Vector3.up;
            Vector3 hold = GetClimbHoldPosition(worldPoint);
            Vector3 planar = Vector3.ProjectOnPlane(hold - worldPoint, up);
            float distance = planar.magnitude;
            if (distance < 0.0001f)
            {
                return worldPoint;
            }

            Vector3 planarDir = planar / distance;
            float maxDrift = Mathf.Max(0.05f, ClimbMaxLateralDrift);
            if (distance > maxDrift)
            {
                worldPoint += planarDir * (distance - maxDrift);
                planar = Vector3.ProjectOnPlane(hold - worldPoint, up);
                distance = planar.magnitude;
                if (distance < 0.0001f)
                {
                    return worldPoint;
                }

                planarDir = planar / distance;
            }

            float pullDistance = Mathf.Max(0f, distance - Mathf.Max(0f, ClimbLateralDeadzone));
            if (pullDistance <= 0f || ClimbSoftStickStrength <= 0f)
            {
                return worldPoint;
            }

            float step = Mathf.Min(pullDistance, ClimbSoftStickStrength * pullDistance * Mathf.Max(0f, deltaTime));
            return worldPoint + planarDir * step;
        }

        public bool ShouldPushOntoRoof(Vector3 worldPoint)
        {
            return transform.InverseTransformPoint(worldPoint).y >= RoofEntryHeight;
        }

        public bool OwnsSolidCollider(Collider collider)
        {
            if (collider == null || collider.isTrigger)
            {
                return false;
            }

            if (solidBlockers == null || solidBlockers.Length == 0)
            {
                CacheSolidBlockers();
            }

            for (int i = 0; i < solidBlockers.Length; i++)
            {
                if (solidBlockers[i] == collider)
                {
                    return true;
                }
            }

            return collider.GetComponentInParent<MiniVanLadder>() == this && !collider.isTrigger;
        }

        private void EnsureClimbTrigger()
        {
            BoxCollider rootBox = GetComponent<BoxCollider>();

            // Repair prefab miswires (cabin ladder once pointed at the exterior ClimbTrigger).
            if (climbTrigger != null && !climbTrigger.transform.IsChildOf(transform))
            {
                climbTrigger = null;
            }

            if (climbTrigger == null)
            {
                // Prefer a dedicated ClimbTrigger child if it already exists.
                Transform triggerTransform = transform.Find("ClimbTrigger");
                if (triggerTransform != null)
                {
                    climbTrigger = triggerTransform.GetComponent<BoxCollider>();
                    if (climbTrigger == null)
                    {
                        climbTrigger = triggerTransform.gameObject.AddComponent<BoxCollider>();
                    }
                }
            }

            if (climbTrigger == null)
            {
                GameObject triggerObject = new GameObject("ClimbTrigger");
                triggerObject.transform.SetParent(transform, false);
                climbTrigger = triggerObject.AddComponent<BoxCollider>();
            }

            // Keep ClimbTrigger matched to the root volume unless the author opted out
            // (MiniVan roof ladders need a hand-tuned box that survives Prefab/OnValidate saves).
            if (syncClimbTriggerFromRoot)
            {
                if (rootBox != null && rootBox != climbTrigger && rootBox.size.y >= 2f)
                {
                    climbTrigger.center = rootBox.center;
                    climbTrigger.size = rootBox.size + new Vector3(0.15f, 0.1f, 0.2f);
                }
                else if (climbTrigger.size.sqrMagnitude < 0.01f)
                {
                    climbTrigger.center = new Vector3(0f, 1.2f, 0f);
                    climbTrigger.size = new Vector3(0.7f, 2.6f, 0.55f);
                }
            }
            else if (climbTrigger.size.sqrMagnitude < 0.01f)
            {
                climbTrigger.center = new Vector3(0f, 1.2f, 0f);
                climbTrigger.size = new Vector3(0.7f, 2.6f, 0.55f);
            }

            climbTrigger.isTrigger = true;

            // Thin root box is the walk-blocking solid (Panelka uses Ladder_Physical_Collider).
            // ClimbTrigger must sit in front of the rails so the player can latch without being
            // shoved out; solids are ignored only while actively climbing this ladder.
            if (rootBox != null && rootBox != climbTrigger)
            {
                rootBox.enabled = true;
                rootBox.isTrigger = false;
            }
        }

        private void CacheSolidBlockers()
        {
            BoxCollider[] boxes = GetComponentsInChildren<BoxCollider>(true);
            int count = 0;
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] != null &&
                    boxes[i].enabled &&
                    !boxes[i].isTrigger &&
                    boxes[i] != climbTrigger)
                {
                    count++;
                }
            }

            solidBlockers = new BoxCollider[count];
            int write = 0;
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] != null &&
                    boxes[i].enabled &&
                    !boxes[i].isTrigger &&
                    boxes[i] != climbTrigger)
                {
                    solidBlockers[write++] = boxes[i];
                }
            }
        }
    }
}
