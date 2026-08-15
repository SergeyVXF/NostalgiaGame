using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace MiniVanGame
{
    public class MiniVanRescuePassengerVisual : MonoBehaviour
    {
        private enum PassengerState
        {
            WalkingBoardingPath,
            Seated,
            WalkingExitPath
        }

        private static readonly List<MiniVanRescuePassengerVisual> Passengers = new List<MiniVanRescuePassengerVisual>();

        [Header("Movement")]
        public float WalkSpeed = 2.15f;
        public float PathReachDistance = 0.34f;
        public float BunkerReachDistance = 0.45f;
        public float TurnSharpness = 8f;
        public float Gravity = -18f;

        [Header("NavMesh Outside")]
        public bool UseNavMeshOutside = true;
        public bool StopWhenNavMeshPathInvalid = true;
        public float NavMeshSampleDistance = 3.5f;
        public float NavMeshCornerReachDistance = 0.55f;
        public float NavMeshRepathInterval = 0.22f;
        public float BlockedLogInterval = 1.5f;

        [Header("Passenger Body")]
        public float ControllerHeight = 1.68f;
        public float ControllerRadius = 0.28f;
        public float ControllerStepOffset = 0.35f;
        public float ControllerSlopeLimit = 50f;

        [Header("Minivan Door Path")]
        public float DoorOutsideOffset = 1.35f;
        public float DoorInsideOffset = 0.28f;
        public float CornerOutsideX = 2.55f;
        public float CornerOutsideZ = 4.45f;
        public float CabinAisleSideOffset = 0.18f;
        public float CabinBoardingStepHeight = 0.22f;
        public float VehicleDoorWaitSeconds = 0.65f;

        [Header("Grounding")]
        public float GroundProbeHeight = 2.6f;
        public float GroundProbeDistance = 6f;
        public float GroundOffset = 0.84f;
        public LayerMask GroundMask = ~0;

        [Header("Rescue Doors")]
        public float SavePlaceDoorOpenSeconds = 5.5f;
        public float BunkerDoorOpenDistance = 3.2f;
        public float BunkerDoorOpenSeconds = 3.5f;
        public float BunkerDoorFinishOpenSeconds = 1.2f;
        public float BunkerDisappearDistance = 1.15f;

        [Header("Vehicle No-Push Zone")]
        public float VehicleGhostHalfWidth = 3.15f;
        public float VehicleGhostRearZ = -4.9f;
        public float VehicleGhostFrontZ = 4.9f;

        [Header("Debug")]
        public bool DebugPassengerPath;

        private MiniVanVehicle vehicle;
        private int seatIndex;
        private MiniVanSeat seat;
        private CharacterController controller;
        private NavMeshPath navMeshPath;
                private MiniVanRescueRoute rescueRoute;
private readonly List<Vector3> activePath = new List<Vector3>();
        private readonly List<bool> activePathOutside = new List<bool>();
        private int activePathIndex;
        private PassengerState state;
        private Vector3 exitDoorPosition;
        private bool exitDoorOpened;
        private int exitDoorInnerPathIndex = -1;
        private int boardingDoorOuterPathIndex = -1;
        private float vehicleDoorOpenedAt = -1f;
        private float verticalVelocity;
        private float nextRepathTime;
        private float nextBlockedLogTime;
        private Vector3 cachedSteerTarget;
        private int cachedPathIndex = -1;
        private bool cachedPathValid;

        public static void StartBoarding(MiniVanVehicle targetVehicle, int targetSeatIndex, Vector3 spawnPosition, Vector3 doorPosition)
        {
            if (targetVehicle == null)
            {
                return;
            }

            MiniVanRescuePassengerVisual existing = FindPassenger(targetVehicle, targetSeatIndex);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            MiniVanRescuePassengerVisual passenger = CreatePassenger(spawnPosition);
            passenger.vehicle = targetVehicle;
            passenger.seatIndex = targetSeatIndex;
            passenger.seat = targetVehicle.GetSeat(targetSeatIndex);
                        passenger.rescueRoute = passenger.GetRescueRoute();
passenger.exitDoorOpened = false;
            passenger.exitDoorInnerPathIndex = -1;
            passenger.boardingDoorOuterPathIndex = -1;
            passenger.vehicleDoorOpenedAt = -1f;
            passenger.ResetNavCache();
            MiniVanRescueDoor.OpenNearest(spawnPosition, passenger.SavePlaceDoorOpenSeconds);
            passenger.BuildBoardingPath(spawnPosition, doorPosition);
            passenger.state = PassengerState.WalkingBoardingPath;
            passenger.LogPathState("boarding started");
        }

        public static void StartExit(MiniVanVehicle targetVehicle, int targetSeatIndex, Vector3 targetBunkerDoorPosition)
        {
            if (targetVehicle == null)
            {
                return;
            }

            MiniVanRescuePassengerVisual passenger = FindPassenger(targetVehicle, targetSeatIndex);
            if (passenger == null)
            {
                MiniVanSeat targetSeat = targetVehicle.GetSeat(targetSeatIndex);
                Vector3 startPosition = targetSeat != null && targetSeat.SitPoint != null ? targetSeat.SitPoint.position : targetVehicle.transform.position;
                passenger = CreatePassenger(startPosition);
                passenger.vehicle = targetVehicle;
                passenger.seatIndex = targetSeatIndex;
                passenger.seat = targetSeat;
                            passenger.rescueRoute = passenger.GetRescueRoute();
}

            passenger.rescueRoute = passenger.GetRescueRoute();
            passenger.transform.SetParent(targetVehicle.transform, true);
            passenger.SetControllerCollisionEnabled(false);
            passenger.exitDoorPosition = targetBunkerDoorPosition;
            passenger.exitDoorOpened = false;
            passenger.exitDoorInnerPathIndex = -1;
            passenger.boardingDoorOuterPathIndex = -1;
            passenger.vehicleDoorOpenedAt = -1f;
            passenger.ResetNavCache();
            passenger.SnapToExitAislePosition(targetBunkerDoorPosition);
            passenger.BuildExitPath(targetBunkerDoorPosition);
            passenger.state = PassengerState.WalkingExitPath;
            passenger.LogPathState("exit started");
        }

        private static MiniVanRescuePassengerVisual CreatePassenger(Vector3 position)
        {
            GameObject passengerObject = new GameObject("Rescue Passenger Visual");
            passengerObject.transform.position = position;

            GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visualObject.name = "Visual";
            visualObject.transform.SetParent(passengerObject.transform, false);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = new Vector3(0.48f, 0.9f, 0.48f);

            Collider visualCollider = visualObject.GetComponent<Collider>();
            if (visualCollider != null)
            {
                visualCollider.enabled = false;
            }

            Renderer renderer = visualObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                renderer.material.color = new Color(0.38f, 0.72f, 1f);
            }

            MiniVanRescuePassengerVisual passenger = passengerObject.AddComponent<MiniVanRescuePassengerVisual>();
            passenger.controller = passengerObject.AddComponent<CharacterController>();
            passenger.navMeshPath = new NavMeshPath();
            passenger.ConfigureController();
            passenger.transform.position = passenger.ProjectToGround(position);
            Passengers.Add(passenger);
            return passenger;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                ConfigureController();
            }

            if (navMeshPath == null)
            {
                navMeshPath = new NavMeshPath();
            }
        }

        private void ConfigureController()
        {
            if (controller == null)
            {
                return;
            }

            controller.height = Mathf.Max(0.5f, ControllerHeight);
            controller.radius = Mathf.Max(0.08f, ControllerRadius);
            controller.center = Vector3.zero;
            controller.stepOffset = Mathf.Clamp(ControllerStepOffset, 0.02f, controller.height * 0.45f);
            controller.slopeLimit = ControllerSlopeLimit;
            controller.minMoveDistance = 0f;
        }

        private static MiniVanRescuePassengerVisual FindPassenger(MiniVanVehicle targetVehicle, int targetSeatIndex)
        {
            for (int i = Passengers.Count - 1; i >= 0; i--)
            {
                MiniVanRescuePassengerVisual passenger = Passengers[i];
                if (passenger == null)
                {
                    Passengers.RemoveAt(i);
                    continue;
                }

                if (passenger.vehicle == targetVehicle && passenger.seatIndex == targetSeatIndex)
                {
                    return passenger;
                }
            }

            return null;
        }

        private void OnDestroy()
        {
            Passengers.Remove(this);
        }

private void Update()
        {
            switch (state)
            {
                case PassengerState.WalkingBoardingPath:
                    if (WalkActivePath(PathReachDistance))
                    {
                        SitDown();
                    }
                    break;
                case PassengerState.Seated:
                    FollowSeat();
                    break;
                case PassengerState.WalkingExitPath:
                    OpenExitDoorIfClose();
                    if (IsNearExitDoor(BunkerDisappearDistance))
                    {
                        FinishAtBunker();
                        break;
                    }

                    if (WalkActivePath(BunkerReachDistance))
                    {
                        FinishAtBunker();
                    }
                    break;
            }
        }

        private bool WalkActivePath(float finalReachDistance)
        {
            if (activePath.Count == 0)
            {
                return true;
            }

            RefreshVehicleRelativePathPoints();
            bool outsideTarget = IsCurrentTargetOutside();
            bool useControllerCollision = outsideTarget && ShouldUseOutsideControllerCollision(activePath[activePathIndex]);
            if (!useControllerCollision)
            {
                AttachToVehicleForCabinMotion();
            }
            SetControllerCollisionEnabled(useControllerCollision);

            if (ShouldWaitForVehicleDoorAtMinivan())
            {
                return false;
            }

            float reach = activePathIndex >= activePath.Count - 1 ? finalReachDistance : PathReachDistance;
            if (outsideTarget && UseNavMeshOutside)
            {
                reach = Mathf.Max(reach, NavMeshCornerReachDistance);
            }

            if (MoveToward(activePath[activePathIndex], reach, outsideTarget, useControllerCollision))
            {
                activePathIndex++;
                ResetNavCache();
                LogPathState("reached path point " + activePathIndex);
                return activePathIndex >= activePath.Count;
            }

            return false;
        }

        private bool MoveToward(Vector3 target, float reachDistance, bool outsideTarget, bool useControllerCollision)
        {
            Vector3 current = transform.position;
            bool cabinTarget = !outsideTarget && IsCabinPoint(target);
            Vector3 moveTarget = cabinTarget ? target : new Vector3(target.x, current.y, target.z);

            if (outsideTarget && useControllerCollision && UseNavMeshOutside)
            {
                if (TryGetNavMeshSteerTarget(target, out Vector3 steerTarget))
                {
                    moveTarget = new Vector3(steerTarget.x, current.y, steerTarget.z);
                }
                else if (StopWhenNavMeshPathInvalid && HasNavMeshNear(transform.position) && HasNavMeshNear(target))
                {
                    LogBlocked("outside NavMesh path invalid to " + target.ToString("F2"));
                    ApplyGravityOnly();
                    return false;
                }
            }

            Vector3 toTarget = moveTarget - current;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Vector3 direction = toTarget.normalized;
                Vector3 lookDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-TurnSharpness * Time.deltaTime));
                }

                Vector3 motion = direction * WalkSpeed * Time.deltaTime;
                if (outsideTarget && useControllerCollision)
                {
                    MoveWithControllerAndGravity(motion);
                }
                else if (outsideTarget)
                {
                    MoveWithoutCollision(motion, true);
                }
                else
                {
                    MoveManual(motion);
                }
            }
            else if (outsideTarget)
            {
                ApplyGravityOnly();
            }

            return IsAtTarget(target, reachDistance, cabinTarget, outsideTarget);
        }

        private bool IsAtTarget(Vector3 target, float reachDistance, bool cabinTarget, bool outsideTarget)
        {
            Vector3 flatPosition = transform.position;
            flatPosition.y = 0f;
            Vector3 flatEnd = target;
            flatEnd.y = 0f;
            float flatDistance = Vector3.Distance(flatPosition, flatEnd);

            if (outsideTarget)
            {
                return flatDistance <= reachDistance;
            }

            if (!cabinTarget)
            {
                return flatDistance <= reachDistance;
            }

            return flatDistance <= reachDistance && Mathf.Abs(transform.position.y - target.y) <= Mathf.Max(0.18f, reachDistance);
        }

        private void MoveWithControllerAndGravity(Vector3 horizontalMotion)
        {
            if (controller == null || !controller.enabled)
            {
                transform.position = ProjectToGround(transform.position + horizontalMotion);
                return;
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            verticalVelocity += Gravity * Time.deltaTime;
            Vector3 motion = horizontalMotion;
            motion.y = verticalVelocity * Time.deltaTime;
            controller.Move(motion);
        }

        private void ApplyGravityOnly()
        {
            MoveWithControllerAndGravity(Vector3.zero);
        }

        private void MoveManual(Vector3 motion)
        {
            transform.position += motion;
            verticalVelocity = 0f;
        }

        private void MoveWithoutCollision(Vector3 motion, bool projectToGround)
        {
            Vector3 nextPosition = transform.position + motion;
            transform.position = projectToGround ? ProjectToGround(nextPosition) : nextPosition;
            verticalVelocity = 0f;
        }

        private void SetControllerCollisionEnabled(bool enabled)
        {
            if (enabled && transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            if (controller == null || controller.enabled == enabled)
            {
                return;
            }

            controller.enabled = enabled;
            if (enabled)
            {
                ConfigureController();
                verticalVelocity = 0f;
            }
        }

        private void AttachToVehicleForCabinMotion()
        {
            if (vehicle != null && transform.parent == null)
            {
                transform.SetParent(vehicle.transform, true);
            }
        }

        private bool ShouldUseOutsideControllerCollision(Vector3 target)
        {
            if (vehicle == null)
            {
                return true;
            }

            return !IsInVehicleNoPushZone(transform.position) && !IsInVehicleNoPushZone(target);
        }

        private bool IsInVehicleNoPushZone(Vector3 worldPosition)
        {
            if (vehicle == null)
            {
                return false;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(worldPosition);
            float minZ = Mathf.Min(VehicleGhostRearZ, VehicleGhostFrontZ);
            float maxZ = Mathf.Max(VehicleGhostRearZ, VehicleGhostFrontZ);
            return Mathf.Abs(local.x) <= Mathf.Abs(VehicleGhostHalfWidth) && local.z >= minZ && local.z <= maxZ;
        }

        private void RefreshVehicleRelativePathPoints()
        {
            if (vehicle == null || activePath.Count == 0)
            {
                return;
            }

            if (state == PassengerState.WalkingBoardingPath)
            {
                RefreshBoardingPathPoints();
                return;
            }

            if (state == PassengerState.WalkingExitPath)
            {
                RefreshExitPathPoints();
            }
        }

        private void RefreshBoardingPathPoints()
        {
            MiniVanRescueRoute route = GetRescueRoute();
            if (route != null && route.TryRefreshBoardingPath(seatIndex, GetSeatPosition(), activePath, boardingDoorOuterPathIndex))
            {
                return;
            }

            if (boardingDoorOuterPathIndex < 0 || boardingDoorOuterPathIndex >= activePath.Count)
            {
                return;
            }

            Vector3 doorOuter = GetDoorOuterPosition(Vector3.zero);
            Vector3 doorInner = GetDoorInnerPosition(Vector3.zero);
            Vector3 aislePoint = GetCabinAislePosition(doorInner);
            Vector3 seatPosition = GetSeatPosition();

            activePath[boardingDoorOuterPathIndex] = ProjectToGround(doorOuter);
            int doorInnerIndex = boardingDoorOuterPathIndex + 1;
            if (doorInnerIndex < activePath.Count)
            {
                activePath[doorInnerIndex] = doorInner;
            }

            int aisleIndex = boardingDoorOuterPathIndex + 2;
            if (aisleIndex < activePath.Count)
            {
                activePath[aisleIndex] = aislePoint;
            }

            int seatPathIndex = boardingDoorOuterPathIndex + 3;
            if (seatPathIndex < activePath.Count)
            {
                activePath[seatPathIndex] = seatPosition;
            }
        }

        private void RefreshExitPathPoints()
        {
            MiniVanRescueRoute route = GetRescueRoute();
            if (route != null && route.TryRefreshExitPath(seatIndex, exitDoorPosition, activePath, out int routeExitDoorInnerPathIndex))
            {
                exitDoorInnerPathIndex = routeExitDoorInnerPathIndex;
                return;
            }

            if (activePath.Count < 3)
            {
                return;
            }

            Vector3 doorInner = GetDoorInnerPosition(exitDoorPosition);
            Vector3 aislePoint = GetCabinAislePosition(doorInner);
            Vector3 doorOuter = GetDoorOuterPosition(exitDoorPosition);

            activePath[0] = aislePoint;
            activePath[1] = doorInner;
            activePath[2] = ProjectToGround(doorOuter);
        }

        private MiniVanRescueRoute GetRescueRoute()
        {
            if (rescueRoute != null)
            {
                rescueRoute.EnsureDefaultRoute();
                return rescueRoute;
            }

            if (vehicle == null)
            {
                return null;
            }

            rescueRoute = vehicle.GetComponentInChildren<MiniVanRescueRoute>(true);
            if (rescueRoute == null)
            {
                rescueRoute = vehicle.gameObject.AddComponent<MiniVanRescueRoute>();
            }

            rescueRoute.EnsureDefaultRoute();
            return rescueRoute;
        }

        private bool TryGetNavMeshSteerTarget(Vector3 targetPosition, out Vector3 steerTarget)
        {
            steerTarget = targetPosition;
            if (navMeshPath == null)
            {
                navMeshPath = new NavMeshPath();
            }

            if (cachedPathValid && cachedPathIndex == activePathIndex && Time.time < nextRepathTime)
            {
                steerTarget = cachedSteerTarget;
                return true;
            }

            nextRepathTime = Time.time + Mathf.Max(0.02f, NavMeshRepathInterval);
            cachedPathValid = false;
            cachedPathIndex = activePathIndex;

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, NavMeshSampleDistance, NavMesh.AllAreas))
            {
                return false;
            }

            if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit targetHit, NavMeshSampleDistance, NavMesh.AllAreas))
            {
                return false;
            }

            if (!NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, navMeshPath) || navMeshPath.status == NavMeshPathStatus.PathInvalid || navMeshPath.corners.Length < 2)
            {
                return false;
            }

            for (int i = 1; i < navMeshPath.corners.Length; i++)
            {
                Vector3 toCorner = Vector3.ProjectOnPlane(navMeshPath.corners[i] - transform.position, Vector3.up);
                if (toCorner.sqrMagnitude > 0.18f)
                {
                    cachedSteerTarget = navMeshPath.corners[i];
                    cachedPathValid = true;
                    steerTarget = cachedSteerTarget;
                    return true;
                }
            }

            cachedSteerTarget = targetHit.position;
            cachedPathValid = true;
            steerTarget = cachedSteerTarget;
            return true;
        }

        private bool HasNavMeshNear(Vector3 position)
        {
            return NavMesh.SamplePosition(position, out _, NavMeshSampleDistance, NavMesh.AllAreas);
        }

        private Vector3 ProjectToGround(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * GroundProbeHeight;
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, GroundProbeHeight + GroundProbeDistance, GroundMask, QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (vehicle != null && hitCollider.transform.IsChildOf(vehicle.transform))
                {
                    continue;
                }

                if (hits[i].point.y > bestY)
                {
                    bestY = hits[i].point.y;
                }
            }

            if (bestY > float.NegativeInfinity)
            {
                position.y = bestY + GroundOffset;
            }

            return position;
        }

        private void BuildBoardingPath(Vector3 spawnPosition, Vector3 fallbackDoorPosition)
        {
            ClearPath();
            exitDoorInnerPathIndex = -1;
            boardingDoorOuterPathIndex = -1;
            vehicleDoorOpenedAt = -1f;

            if (vehicle == null)
            {
                AddPathPoint(ProjectToGround(fallbackDoorPosition), true);
                return;
            }

            MiniVanRescueRoute route = GetRescueRoute();
            if (route != null && route.TryBuildBoardingPath(seatIndex, GetSeatPosition(), spawnPosition, activePath, activePathOutside, out boardingDoorOuterPathIndex))
            {
                return;
            }

            Vector3 doorOuter = GetDoorOuterPosition(fallbackDoorPosition);
            Vector3 doorInner = GetDoorInnerPosition(fallbackDoorPosition);
            Vector3 aislePoint = GetCabinAislePosition(doorInner);
            Vector3 seatPosition = GetSeatPosition();
            if (!UseNavMeshOutside)
            {
                AddOutsideCornerPathIfNeeded(spawnPosition, doorOuter);
            }

            AddPathPoint(ProjectToGround(doorOuter), true);
            boardingDoorOuterPathIndex = activePath.Count - 1;
            AddPathPoint(doorInner, false);
            AddPathPoint(aislePoint, false);
            AddPathPoint(seatPosition, false);
        }

        private void BuildExitPath(Vector3 bunkerDoorPosition)
        {
            ClearPath();
            exitDoorInnerPathIndex = -1;
            boardingDoorOuterPathIndex = -1;
            vehicleDoorOpenedAt = -1f;

            MiniVanRescueRoute route = GetRescueRoute();
            if (route != null && route.TryBuildExitPath(seatIndex, bunkerDoorPosition, activePath, activePathOutside, out exitDoorInnerPathIndex))
            {
                return;
            }

            Vector3 fallbackDoor = bunkerDoorPosition;
            Vector3 doorInner = GetDoorInnerPosition(fallbackDoor);
            Vector3 doorOuter = GetDoorOuterPosition(fallbackDoor);
            Vector3 aislePoint = GetCabinAislePosition(doorInner);

            AddPathPoint(aislePoint, false);
            AddPathPoint(doorInner, false);
            exitDoorInnerPathIndex = activePath.Count - 1;
            AddPathPoint(ProjectToGround(doorOuter), false);
            if (!UseNavMeshOutside)
            {
                AddOutsideCornerPathIfNeeded(doorOuter, bunkerDoorPosition);
            }

            AddPathPoint(ProjectToGround(bunkerDoorPosition), true);
        }

        private void ClearPath()
        {
            activePath.Clear();
            activePathOutside.Clear();
            activePathIndex = 0;
            ResetNavCache();
        }

        private void AddPathPoint(Vector3 position, bool outside)
        {
            activePath.Add(position);
            activePathOutside.Add(outside);
        }

        private bool IsCurrentTargetOutside()
        {
            return activePathIndex >= 0 && activePathIndex < activePathOutside.Count && activePathOutside[activePathIndex];
        }

        private void ResetNavCache()
        {
            cachedPathValid = false;
            cachedPathIndex = -1;
            nextRepathTime = 0f;
        }

        private void SnapToExitAislePosition(Vector3 bunkerDoorPosition)
        {
            if (vehicle == null)
            {
                return;
            }

            MiniVanRescueRoute route = GetRescueRoute();
            Vector3 doorInner = GetDoorInnerPosition(bunkerDoorPosition);
            Vector3 aislePoint = route != null ? route.GetExitStartPosition(seatIndex, GetCabinAislePosition(doorInner)) : GetCabinAislePosition(doorInner);
            transform.position = aislePoint;
            verticalVelocity = 0f;

            Vector3 lookDirection = Vector3.ProjectOnPlane(doorInner - aislePoint, Vector3.up);
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        private bool ShouldWaitForVehicleDoorAtMinivan()
        {
            if (vehicle == null)
            {
                return false;
            }

            bool waitingForBoardingDoor = state == PassengerState.WalkingBoardingPath
                && boardingDoorOuterPathIndex >= 0
                && activePathIndex > boardingDoorOuterPathIndex;
            bool waitingForExitDoor = state == PassengerState.WalkingExitPath
                && exitDoorInnerPathIndex >= 0
                && activePathIndex > exitDoorInnerPathIndex;

            if (!waitingForBoardingDoor && !waitingForExitDoor)
            {
                return false;
            }

            OpenVehicleDoorIfServer();
            if (!vehicle.SideDoorOpen.Value)
            {
                vehicleDoorOpenedAt = -1f;
                return true;
            }

            if (vehicleDoorOpenedAt < 0f)
            {
                vehicleDoorOpenedAt = Time.time;
                return true;
            }

            return Time.time - vehicleDoorOpenedAt < Mathf.Max(0f, VehicleDoorWaitSeconds);
        }

        private void OpenVehicleDoorIfServer()
        {
            if (vehicle == null || vehicle.SideDoorOpen.Value)
            {
                return;
            }

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
            {
                vehicle.SideDoorOpen.Value = true;
                LogPathState("vehicle side door opened");
            }
        }

        private void OpenExitDoorIfClose()
        {
            if (exitDoorOpened)
            {
                return;
            }

            Vector3 flatPosition = transform.position;
            Vector3 flatDoor = exitDoorPosition;
            flatPosition.y = 0f;
            flatDoor.y = 0f;
            if (Vector3.Distance(flatPosition, flatDoor) > BunkerDoorOpenDistance)
            {
                return;
            }

            exitDoorOpened = true;
            MiniVanRescueDoor.OpenNearest(exitDoorPosition, BunkerDoorOpenSeconds);
        }

        private bool IsNearExitDoor(float distance)
        {
            Vector3 flatPosition = transform.position;
            Vector3 flatDoor = exitDoorPosition;
            flatPosition.y = 0f;
            flatDoor.y = 0f;
            return Vector3.Distance(flatPosition, flatDoor) <= Mathf.Max(0.05f, distance);
        }

        private void FinishAtBunker()
        {
            MiniVanRescueDoor.OpenNearest(exitDoorPosition, BunkerDoorFinishOpenSeconds);
            ClearVehicleMovementLockAfterExit();
            Destroy(gameObject);
        }

        private void AddOutsideCornerPathIfNeeded(Vector3 startPosition, Vector3 endPosition)
        {
            if (vehicle == null)
            {
                return;
            }

            Vector3 localStart = vehicle.transform.InverseTransformPoint(startPosition);
            Vector3 localEnd = vehicle.transform.InverseTransformPoint(endPosition);
            if (Mathf.Sign(localStart.x) == Mathf.Sign(localEnd.x) || Mathf.Abs(localStart.x) <= 0.45f)
            {
                return;
            }

            float sideX = Mathf.Sign(localEnd.x) * CornerOutsideX;
            float startSideX = Mathf.Sign(localStart.x) * CornerOutsideX;
            float cornerZ = Mathf.Abs(localStart.z) < 0.15f ? -CornerOutsideZ : Mathf.Sign(localStart.z) * CornerOutsideZ;
            AddPathPoint(ProjectToGround(vehicle.transform.TransformPoint(new Vector3(startSideX, localEnd.y, cornerZ))), true);
            AddPathPoint(ProjectToGround(vehicle.transform.TransformPoint(new Vector3(sideX, localEnd.y, cornerZ))), true);
        }

        private Vector3 GetDoorOuterPosition(Vector3 fallbackDoorPosition)
        {
            Vector3 doorBase = GetDoorBasePosition(fallbackDoorPosition);
            float side = GetDoorSide(fallbackDoorPosition);
            Vector3 result = doorBase + vehicle.transform.right * side * DoorOutsideOffset;
            result.y = doorBase.y;
            return result;
        }

        private Vector3 GetDoorInnerPosition(Vector3 fallbackDoorPosition)
        {
            Vector3 doorBase = GetDoorBasePosition(fallbackDoorPosition);
            float side = GetDoorSide(fallbackDoorPosition);
            Vector3 result = doorBase - vehicle.transform.right * side * DoorInsideOffset;
            result.y = GetCabinFloorWorldY() + CabinBoardingStepHeight;
            return result;
        }

        private Vector3 GetDoorBasePosition(Vector3 fallbackDoorPosition)
        {
            MiniVanDoor sideDoor = FindSideDoor();
            if (sideDoor != null)
            {
                return sideDoor.transform.position;
            }

            UpdateSeat();
            if (seat != null && seat.ExitPoint != null)
            {
                return seat.ExitPoint.position;
            }

            if (vehicle != null)
            {
                return vehicle.transform.TransformPoint(new Vector3(1.15f, 0.9f, -0.7f));
            }

            return fallbackDoorPosition;
        }

        private MiniVanDoor FindSideDoor()
        {
            if (vehicle == null)
            {
                return null;
            }

            MiniVanDoor[] doors = vehicle.GetComponentsInChildren<MiniVanDoor>(true);
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null && !doors[i].IsRoofDoor)
                {
                    return doors[i];
                }
            }

            return null;
        }

        private float GetDoorSide(Vector3 fallbackDoorPosition)
        {
            if (vehicle == null)
            {
                return 1f;
            }

            Vector3 localDoor = vehicle.transform.InverseTransformPoint(GetDoorBasePosition(fallbackDoorPosition));
            if (Mathf.Abs(localDoor.x) > 0.05f)
            {
                return Mathf.Sign(localDoor.x);
            }

            return 1f;
        }

        private Vector3 GetCabinAislePosition(Vector3 doorInner)
        {
            if (vehicle == null)
            {
                return doorInner;
            }

            Vector3 localDoorInner = vehicle.transform.InverseTransformPoint(doorInner);
            Vector3 localSeat = vehicle.transform.InverseTransformPoint(GetSeatPosition());
            float side = Mathf.Abs(localDoorInner.x) > 0.05f ? Mathf.Sign(localDoorInner.x) : 1f;
            Vector3 localAisle = new Vector3(side * CabinAisleSideOffset, localSeat.y, localSeat.z);
            return vehicle.transform.TransformPoint(localAisle);
        }

        private bool IsCabinPoint(Vector3 worldPoint)
        {
            if (vehicle == null)
            {
                return false;
            }

            Vector3 local = vehicle.transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x) <= 1.9f && local.z >= -3.65f && local.z <= 3.15f && local.y >= 0.35f && local.y <= 3.1f;
        }

        private float GetCabinFloorWorldY()
        {
            if (vehicle == null)
            {
                return transform.position.y;
            }

            UpdateSeat();
            if (seat != null && seat.SitPoint != null)
            {
                return seat.SitPoint.position.y;
            }

            if (seat != null)
            {
                return seat.transform.position.y;
            }

            return vehicle.transform.TransformPoint(new Vector3(0f, 0.95f, 0f)).y;
        }

        private void UpdateSeat()
        {
            if (seat == null && vehicle != null)
            {
                seat = vehicle.GetSeat(seatIndex);
            }
        }

        private Vector3 GetSeatPosition()
        {
            UpdateSeat();
            if (seat != null && seat.SitPoint != null)
            {
                return seat.SitPoint.position;
            }

            if (seat != null)
            {
                return seat.transform.position;
            }

            return vehicle != null ? vehicle.transform.position : transform.position;
        }

private void SitDown()
        {
            UpdateSeat();
            Transform seatRoot = seat != null && seat.SitPoint != null ? seat.SitPoint : (seat != null ? seat.transform : null);
            if (seatRoot != null)
            {
                SetControllerCollisionEnabled(false);

                transform.SetParent(seatRoot, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            state = PassengerState.Seated;
            MiniVanRescueMission.ServerNotifyPassengerSeated(vehicle, seatIndex);
            LogPathState("seated");
        }

        private void FollowSeat()
        {
            UpdateSeat();
            if (seat == null)
            {
                return;
            }

            Transform seatRoot = seat.SitPoint != null ? seat.SitPoint : seat.transform;
            if (transform.parent != seatRoot)
            {
                transform.SetParent(seatRoot, false);
            }

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        private void LogBlocked(string message)
        {
            if (!DebugPassengerPath || Time.time < nextBlockedLogTime)
            {
                return;
            }

            nextBlockedLogTime = Time.time + Mathf.Max(0.1f, BlockedLogInterval);
            Debug.Log("[MiniVanRescuePassenger] " + message + " state=" + state + " pathIndex=" + activePathIndex + "/" + activePath.Count);
        }

        private void LogPathState(string message)
        {
            if (DebugPassengerPath)
            {
                Debug.Log("[MiniVanRescuePassenger] " + message + " state=" + state + " pathIndex=" + activePathIndex + "/" + activePath.Count);
            }
        }
    

private void ClearVehicleMovementLockAfterExit()
        {
            if (vehicle == null)
            {
                return;
            }

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
            {
                vehicle.ClearRescueMovementLock("passenger exited bunker");
            }
        }
}
}








