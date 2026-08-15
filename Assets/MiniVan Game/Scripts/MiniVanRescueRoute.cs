using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanRescueRoute : MonoBehaviour
    {
        [Header("Route Points")]
        public Transform DoorOuterPoint;
        public Transform DoorInnerPoint;
        public Transform AisleDoorPoint;
        public Transform AisleMiddlePoint;
        public Transform Seat2ApproachPoint;
        public Transform Seat3ApproachPoint;

        [Header("Defaults")]
        public bool AutoCreateDefaultRoute = true;
        public bool DrawGizmos = true;
        public Vector3 DoorOuterLocal = new Vector3(1.95f, 1.05f, -0.7f);
        public Vector3 DoorInnerLocal = new Vector3(0.78f, 1.12f, -0.7f);
        public Vector3 AisleDoorLocal = new Vector3(0.2f, 1.12f, -0.7f);
        public Vector3 AisleMiddleLocal = new Vector3(0.2f, 1.12f, 0.35f);
        public Vector3 Seat2ApproachLocal = new Vector3(0.2f, 1.12f, 0.95f);
        public Vector3 Seat3ApproachLocal = new Vector3(0.2f, 1.12f, 1.55f);

        [Header("Door Binding")]
        public bool FollowSideDoor = true;
        public float DoorOutsideDistance = 0.65f;
        public float DoorInsideDistance = 1.0f;
        public float AisleLocalX = 0.2f;
        public float CabinFloorLocalY = 1.12f;

        [Header("Outside Vehicle Bypass")]
        public bool UseOutsideBypassPath = true;
        public float BypassHalfWidth = 2.65f;
        public float BypassFrontZ = 4.45f;
        public float BypassRearZ = -4.45f;

        private void Awake()
        {
            EnsureDefaultRoute();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                CacheExistingRoutePoints();
            }
        }

        public void EnsureDefaultRoute()
        {
            CacheExistingRoutePoints();
            if (!AutoCreateDefaultRoute)
            {
                return;
            }

            Transform root = GetOrCreateRouteRoot();
            DoorOuterPoint = DoorOuterPoint != null ? DoorOuterPoint : GetOrCreatePoint(root, "DoorOuter", DoorOuterLocal);
            DoorInnerPoint = DoorInnerPoint != null ? DoorInnerPoint : GetOrCreatePoint(root, "DoorInner", DoorInnerLocal);
            AisleDoorPoint = AisleDoorPoint != null ? AisleDoorPoint : GetOrCreatePoint(root, "AisleDoor", AisleDoorLocal);
            AisleMiddlePoint = AisleMiddlePoint != null ? AisleMiddlePoint : GetOrCreatePoint(root, "AisleMiddle", AisleMiddleLocal);
            Seat2ApproachPoint = Seat2ApproachPoint != null ? Seat2ApproachPoint : GetOrCreatePoint(root, "Seat2Approach", Seat2ApproachLocal);
            Seat3ApproachPoint = Seat3ApproachPoint != null ? Seat3ApproachPoint : GetOrCreatePoint(root, "Seat3Approach", Seat3ApproachLocal);
            AlignRouteToSideDoor();
        }

        public bool TryBuildBoardingPath(int seatIndex, Vector3 seatPosition, List<Vector3> points, List<bool> outsideFlags, out int doorOuterPathIndex)
        {
            return TryBuildBoardingPath(seatIndex, seatPosition, DoorOuterPoint != null ? DoorOuterPoint.position : transform.position, points, outsideFlags, out doorOuterPathIndex);
        }

        public bool TryBuildBoardingPath(int seatIndex, Vector3 seatPosition, Vector3 startPosition, List<Vector3> points, List<bool> outsideFlags, out int doorOuterPathIndex)
        {
            doorOuterPathIndex = -1;
            if (!HasRequiredPoints())
            {
                return false;
            }

            AddOutsideBypassPath(startPosition, DoorOuterPoint.position, points, outsideFlags);
            points.Add(DoorOuterPoint.position);
            outsideFlags.Add(true);
            doorOuterPathIndex = points.Count - 1;

            points.Add(DoorInnerPoint.position);
            outsideFlags.Add(false);
            points.Add(AisleDoorPoint.position);
            outsideFlags.Add(false);
            points.Add(AisleMiddlePoint.position);
            outsideFlags.Add(false);
            points.Add(GetSeatApproachPoint(seatIndex).position);
            outsideFlags.Add(false);
            points.Add(seatPosition);
            outsideFlags.Add(false);
            return true;
        }

        public bool TryRefreshBoardingPath(int seatIndex, Vector3 seatPosition, List<Vector3> points, int doorOuterPathIndex)
        {
            if (!HasRequiredPoints() || doorOuterPathIndex < 0 || doorOuterPathIndex >= points.Count)
            {
                return false;
            }

            SetPoint(points, doorOuterPathIndex, DoorOuterPoint.position);
            SetPoint(points, doorOuterPathIndex + 1, DoorInnerPoint.position);
            SetPoint(points, doorOuterPathIndex + 2, AisleDoorPoint.position);
            SetPoint(points, doorOuterPathIndex + 3, AisleMiddlePoint.position);
            SetPoint(points, doorOuterPathIndex + 4, GetSeatApproachPoint(seatIndex).position);
            SetPoint(points, doorOuterPathIndex + 5, seatPosition);
            return true;
        }

        public bool TryBuildExitPath(int seatIndex, Vector3 bunkerDoorPosition, List<Vector3> points, List<bool> outsideFlags, out int exitDoorInnerPathIndex)
        {
            exitDoorInnerPathIndex = -1;
            if (!HasRequiredPoints())
            {
                return false;
            }

            points.Add(GetSeatApproachPoint(seatIndex).position);
            outsideFlags.Add(false);
            points.Add(AisleMiddlePoint.position);
            outsideFlags.Add(false);
            points.Add(AisleDoorPoint.position);
            outsideFlags.Add(false);
            points.Add(DoorInnerPoint.position);
            outsideFlags.Add(false);
            exitDoorInnerPathIndex = points.Count - 1;
            points.Add(DoorOuterPoint.position);
            outsideFlags.Add(false);
            AddOutsideBypassPath(DoorOuterPoint.position, bunkerDoorPosition, points, outsideFlags);
            points.Add(bunkerDoorPosition);
            outsideFlags.Add(true);
            return true;
        }

        public bool TryRefreshExitPath(int seatIndex, Vector3 bunkerDoorPosition, List<Vector3> points, out int exitDoorInnerPathIndex)
        {
            exitDoorInnerPathIndex = -1;
            if (!HasRequiredPoints() || points.Count < 6)
            {
                return false;
            }

            points[0] = GetSeatApproachPoint(seatIndex).position;
            points[1] = AisleMiddlePoint.position;
            points[2] = AisleDoorPoint.position;
            points[3] = DoorInnerPoint.position;
            exitDoorInnerPathIndex = 3;
            points[4] = DoorOuterPoint.position;
            points[points.Count - 1] = bunkerDoorPosition;
            return true;
        }

        public Vector3 GetExitStartPosition(int seatIndex, Vector3 fallback)
        {
            if (!HasRequiredPoints())
            {
                return fallback;
            }

            return GetSeatApproachPoint(seatIndex).position;
        }

        private bool HasRequiredPoints()
        {
            EnsureDefaultRoute();
            return DoorOuterPoint != null
                && DoorInnerPoint != null
                && AisleDoorPoint != null
                && AisleMiddlePoint != null
                && Seat2ApproachPoint != null
                && Seat3ApproachPoint != null;
        }

        private Transform GetSeatApproachPoint(int seatIndex)
        {
            return seatIndex == 3 ? Seat3ApproachPoint : Seat2ApproachPoint;
        }

        private static void SetPoint(List<Vector3> points, int index, Vector3 value)
        {
            if (index >= 0 && index < points.Count)
            {
                points[index] = value;
            }
        }

        private void AddOutsideBypassPath(Vector3 startPosition, Vector3 endPosition, List<Vector3> points, List<bool> outsideFlags)
        {
            if (!UseOutsideBypassPath)
            {
                return;
            }

            Vector3 localStart = transform.InverseTransformPoint(startPosition);
            Vector3 localEnd = transform.InverseTransformPoint(endPosition);
            if (!NeedsVehicleBypass(localStart, localEnd))
            {
                return;
            }

            float endSide = Mathf.Abs(localEnd.x) > 0.05f ? Mathf.Sign(localEnd.x) : 1f;
            float startSide = Mathf.Abs(localStart.x) > 0.05f ? Mathf.Sign(localStart.x) : -endSide;
            float width = Mathf.Max(Mathf.Abs(localEnd.x) + 0.35f, Mathf.Abs(BypassHalfWidth));
            float startX = startSide * width;
            float endX = endSide * width;
            float bypassZ = ChooseBypassZ(localStart, localEnd);
            float y = localEnd.y;

            AddOutsidePoint(points, outsideFlags, new Vector3(startX, y, localStart.z));
            AddOutsidePoint(points, outsideFlags, new Vector3(startX, y, bypassZ));
            AddOutsidePoint(points, outsideFlags, new Vector3(endX, y, bypassZ));
            AddOutsidePoint(points, outsideFlags, new Vector3(endX, y, localEnd.z));
        }

        private bool NeedsVehicleBypass(Vector3 localStart, Vector3 localEnd)
        {
            float width = Mathf.Abs(BypassHalfWidth);
            float minZ = Mathf.Min(BypassRearZ, BypassFrontZ);
            float maxZ = Mathf.Max(BypassRearZ, BypassFrontZ);
            bool startInsideFootprint = Mathf.Abs(localStart.x) < width && localStart.z > minZ && localStart.z < maxZ;
            bool endInsideFootprint = Mathf.Abs(localEnd.x) < width && localEnd.z > minZ && localEnd.z < maxZ;
            bool oppositeSides = Mathf.Abs(localStart.x) > 0.05f && Mathf.Abs(localEnd.x) > 0.05f && Mathf.Sign(localStart.x) != Mathf.Sign(localEnd.x);
            return startInsideFootprint || endInsideFootprint || oppositeSides || SegmentCrossesVehicleFootprint(localStart, localEnd, width, minZ, maxZ);
        }

        private static bool SegmentCrossesVehicleFootprint(Vector3 localStart, Vector3 localEnd, float width, float minZ, float maxZ)
        {
            const int Steps = 12;
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                Vector3 sample = Vector3.Lerp(localStart, localEnd, t);
                if (Mathf.Abs(sample.x) < width && sample.z > minZ && sample.z < maxZ)
                {
                    return true;
                }
            }

            return false;
        }

        private float ChooseBypassZ(Vector3 localStart, Vector3 localEnd)
        {
            float frontCost = Mathf.Abs(localStart.z - BypassFrontZ) + Mathf.Abs(localEnd.z - BypassFrontZ);
            float rearCost = Mathf.Abs(localStart.z - BypassRearZ) + Mathf.Abs(localEnd.z - BypassRearZ);
            return frontCost <= rearCost ? BypassFrontZ : BypassRearZ;
        }

        private void AddOutsidePoint(List<Vector3> points, List<bool> outsideFlags, Vector3 localPosition)
        {
            Vector3 worldPosition = transform.TransformPoint(localPosition);
            if (points.Count > 0)
            {
                Vector3 previous = points[points.Count - 1];
                previous.y = 0f;
                Vector3 flatWorld = worldPosition;
                flatWorld.y = 0f;
                if (Vector3.Distance(previous, flatWorld) < 0.2f)
                {
                    return;
                }
            }

            points.Add(worldPosition);
            outsideFlags.Add(true);
        }

        private void CacheExistingRoutePoints()
        {
            Transform root = transform.Find("Rescue Passenger Route");
            if (root == null)
            {
                return;
            }

            DoorOuterPoint = DoorOuterPoint != null ? DoorOuterPoint : root.Find("DoorOuter");
            DoorInnerPoint = DoorInnerPoint != null ? DoorInnerPoint : root.Find("DoorInner");
            AisleDoorPoint = AisleDoorPoint != null ? AisleDoorPoint : root.Find("AisleDoor");
            AisleMiddlePoint = AisleMiddlePoint != null ? AisleMiddlePoint : root.Find("AisleMiddle");
            Seat2ApproachPoint = Seat2ApproachPoint != null ? Seat2ApproachPoint : root.Find("Seat2Approach");
            Seat3ApproachPoint = Seat3ApproachPoint != null ? Seat3ApproachPoint : root.Find("Seat3Approach");
        }

        private Transform GetOrCreateRouteRoot()
        {
            Transform root = transform.Find("Rescue Passenger Route");
            if (root != null)
            {
                return root;
            }

            GameObject rootObject = new GameObject("Rescue Passenger Route");
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private static Transform GetOrCreatePoint(Transform root, string name, Vector3 localPosition)
        {
            Transform existing = root.Find(name);
            if (existing != null)
            {
                return existing;
            }

            GameObject pointObject = new GameObject(name);
            pointObject.transform.SetParent(root, false);
            pointObject.transform.localPosition = localPosition;
            pointObject.transform.localRotation = Quaternion.identity;
            pointObject.transform.localScale = Vector3.one;
            return pointObject.transform;
        }

        private void AlignRouteToSideDoor()
        {
            if (!FollowSideDoor)
            {
                return;
            }

            Transform sideDoor = FindSideDoorTransform();
            if (sideDoor == null)
            {
                return;
            }

            Vector3 doorLocal = transform.InverseTransformPoint(sideDoor.position);
            float side = Mathf.Abs(doorLocal.x) > 0.05f ? Mathf.Sign(doorLocal.x) : 1f;
            float floorY = ResolveCabinFloorLocalY();
            float doorZ = doorLocal.z;
            float aisleX = AisleLocalX;

            SetLocalPosition(DoorOuterPoint, new Vector3(doorLocal.x + side * Mathf.Abs(DoorOutsideDistance), floorY, doorZ));
            SetLocalPosition(DoorInnerPoint, new Vector3(doorLocal.x - side * Mathf.Abs(DoorInsideDistance), floorY, doorZ));
            SetLocalPosition(AisleDoorPoint, new Vector3(aisleX, floorY, doorZ));

            Vector3 seat2 = ResolveSeatApproachLocal(2, Seat2ApproachLocal);
            Vector3 seat3 = ResolveSeatApproachLocal(3, Seat3ApproachLocal);
            SetLocalPosition(AisleMiddlePoint, new Vector3(aisleX, floorY, Mathf.Lerp(doorZ, seat2.z, 0.5f)));
            SetLocalPosition(Seat2ApproachPoint, seat2);
            SetLocalPosition(Seat3ApproachPoint, seat3);
        }

        private Vector3 ResolveSeatApproachLocal(int seatIndex, Vector3 fallback)
        {
            MiniVanSeat[] seats = GetComponentsInChildren<MiniVanSeat>(true);
            for (int i = 0; i < seats.Length; i++)
            {
                MiniVanSeat seat = seats[i];
                if (seat == null || seat.SeatIndex != seatIndex)
                {
                    continue;
                }

                Transform seatPoint = seat.SitPoint != null ? seat.SitPoint : seat.transform;
                Vector3 localSeat = transform.InverseTransformPoint(seatPoint.position);
                return new Vector3(AisleLocalX, localSeat.y, localSeat.z);
            }

            return fallback;
        }

        private float ResolveCabinFloorLocalY()
        {
            MiniVanSeat[] seats = GetComponentsInChildren<MiniVanSeat>(true);
            for (int i = 0; i < seats.Length; i++)
            {
                MiniVanSeat seat = seats[i];
                if (seat == null || seat.SeatIndex != 2)
                {
                    continue;
                }

                Transform seatPoint = seat.SitPoint != null ? seat.SitPoint : seat.transform;
                return transform.InverseTransformPoint(seatPoint.position).y;
            }

            return CabinFloorLocalY;
        }

        private Transform FindSideDoorTransform()
        {
            MiniVanDoor[] doors = GetComponentsInChildren<MiniVanDoor>(true);
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null && !doors[i].IsRoofDoor)
                {
                    return doors[i].transform;
                }
            }

            Transform namedDoor = FindChildRecursive(transform, "Door");
            if (namedDoor != null)
            {
                return namedDoor;
            }

            return FindChildByNamePart(transform, "side");
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static Transform FindChildByNamePart(Transform root, string namePart)
        {
            if (root == null)
            {
                return null;
            }

            string loweredPart = namePart.ToLowerInvariant();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.ToLowerInvariant().Contains(loweredPart))
                {
                    return child;
                }

                Transform nested = FindChildByNamePart(child, namePart);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void SetLocalPosition(Transform target, Vector3 localPosition)
        {
            if (target != null)
            {
                target.localPosition = localPosition;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmos)
            {
                return;
            }

            Gizmos.color = new Color(0.1f, 0.95f, 1f, 0.9f);
            DrawRoute(Seat2ApproachPoint);
            DrawRoute(Seat3ApproachPoint);
        }

        private void DrawRoute(Transform seatApproach)
        {
            if (DoorOuterPoint == null || DoorInnerPoint == null || AisleDoorPoint == null || AisleMiddlePoint == null || seatApproach == null)
            {
                return;
            }

            DrawSegment(DoorOuterPoint.position, DoorInnerPoint.position);
            DrawSegment(DoorInnerPoint.position, AisleDoorPoint.position);
            DrawSegment(AisleDoorPoint.position, AisleMiddlePoint.position);
            DrawSegment(AisleMiddlePoint.position, seatApproach.position);
        }

        private static void DrawSegment(Vector3 a, Vector3 b)
        {
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.08f);
            Gizmos.DrawWireSphere(b, 0.08f);
        }
    }
}
