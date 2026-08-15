using UnityEngine;

namespace MiniVanGame
{
    public static class MiniVanTowRopeUtility
    {
        public const int MaxPathPoints = 16;
        public static string LastPathDebug { get; private set; } = "";
        private const int MaxWrapPoints = MaxPathPoints - 2;
        private const float WrapSurfaceClearance = 0.035f;
        private const float MaxSlidingStep = 4f;
        private const float DefaultSlideAssist = 2.5f;
        private const float MaxRopeSolveLength = 80f;
        private const float CornerSlideBoost = 5f;
        private const float CornerSlideRetention = 0.985f;

        public sealed class RopeState
        {
            public readonly Vector3[] WrapPoints = new Vector3[MaxWrapPoints];
            public readonly Collider[] WrapColliders = new Collider[MaxWrapPoints];
            public int WrapCount;

            public void Clear()
            {
                for (int i = 0; i < WrapCount && i < MaxWrapPoints; i++)
                {
                    WrapColliders[i] = null;
                }

                WrapCount = 0;
            }

            public void DropFirstWrap()
            {
                if (WrapCount <= 0)
                {
                    return;
                }

                for (int i = 0; i < WrapCount - 1; i++)
                {
                    WrapPoints[i] = WrapPoints[i + 1];
                    WrapColliders[i] = WrapColliders[i + 1];
                }

                WrapColliders[WrapCount - 1] = null;
                WrapCount--;
            }
        }

        public static int BuildPath(
            Vector3 attach,
            Vector3 anchor,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            RopeState state,
            Vector3[] points)
        {
            if (points == null || points.Length < 2)
            {
                LastPathDebug = "invalid points buffer";
                return 0;
            }

            if (!IsFinite(attach) || !IsFinite(anchor))
            {
                state?.Clear();
                LastPathDebug = "invalid attach/anchor attach=" + attach.ToString("F2") + " anchor=" + anchor.ToString("F2");
                return 0;
            }

            castRadius = Mathf.Max(0.03f, castRadius);
            state ??= new RopeState();
            state.Clear();

            int visibilityCount = BuildVisibilityPath(attach, anchor, castRadius, ignoredA, ignoredB, points);
            if (visibilityCount >= 2)
            {
                return visibilityCount;
            }

            points[0] = attach;
            points[1] = anchor;
            return 2;
        }

        public static float GetPathLength(Vector3[] points, int count)
        {
            float length = 0f;
            for (int i = 1; i < count; i++)
            {
                if (!IsFinite(points[i - 1]) || !IsFinite(points[i]))
                {
                    return float.PositiveInfinity;
                }

                float segmentLength = Vector3.Distance(points[i - 1], points[i]);
                if (!IsFinite(segmentLength) || segmentLength > MaxRopeSolveLength)
                {
                    return float.PositiveInfinity;
                }

                length += segmentLength;
                if (!IsFinite(length))
                {
                    return float.PositiveInfinity;
                }
            }

            return length;
        }

        public static Vector3 GetTensionDirection(Vector3[] points, int count)
        {
            if (points == null || count < 2)
            {
                return Vector3.zero;
            }

            Vector3 direction = points[1] - points[0];
            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.000001f)
            {
                return Vector3.zero;
            }

            direction.Normalize();
            return IsFinite(direction) ? direction : Vector3.zero;
        }

        public static Vector3 GetSlidingTensionDirection(Vector3[] points, int count)
        {
            if (points == null || count < 2)
            {
                return Vector3.zero;
            }

            Vector3 firstSegment = points[1] - points[0];
            Vector3 direction = Vector3.ProjectOnPlane(firstSegment, Vector3.up);
            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.000001f)
            {
                direction = firstSegment;
            }

            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.000001f)
            {
                return Vector3.zero;
            }

            direction.Normalize();
            return IsFinite(direction) ? direction : Vector3.zero;
        }

        private static int BuildVisibilityPath(
            Vector3 attach,
            Vector3 anchor,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            Vector3[] points)
        {
            if (points == null || points.Length < 2)
            {
                return 0;
            }

            points[0] = attach;
            int count = 1;
            Vector3 current = attach;
            Collider lastBlocker = null;
            string debug = "visibility";

            for (int safety = 0; safety < points.Length - 2; safety++)
            {
                if (!TryGetFirstBlocker(current, anchor, castRadius, ignoredA, ignoredB, lastBlocker, out RaycastHit blockerHit))
                {
                    points[count] = anchor;
                    LastPathDebug = debug + " directFrom=" + safety + " points=" + (count + 1);
                    return count + 1;
                }

                string blockerName = blockerHit.collider != null ? blockerHit.collider.name : "null";
                if (!TryFindVisibilityWaypoint(
                    current,
                    anchor,
                    blockerHit,
                    lastBlocker,
                    castRadius,
                    ignoredA,
                    ignoredB,
                    out Vector3 waypoint))
                {
                    waypoint = blockerHit.point + blockerHit.normal.normalized * Mathf.Clamp(castRadius * 2.5f, 0.3f, 0.9f);
                    waypoint.y = Mathf.Lerp(current.y, anchor.y, 0.5f);
                    debug += " fallback(" + blockerName + "->" + waypoint.ToString("F2") + ")";
                }
                else
                {
                    debug += " wrap(" + blockerName + "->" + waypoint.ToString("F2") + ")";
                }

                if (!IsFinite(waypoint) || (waypoint - current).sqrMagnitude < 0.01f)
                {
                    points[count] = anchor;
                    LastPathDebug = debug + " badWaypoint points=" + (count + 1);
                    return count + 1;
                }

                points[count] = waypoint;
                count++;
                current = waypoint;
                lastBlocker = blockerHit.collider;
            }

            points[count] = anchor;
            LastPathDebug = debug + " maxPoints points=" + (count + 1);
            return count + 1;
        }

        private static bool TryFindVisibilityWaypoint(
            Vector3 current,
            Vector3 anchor,
            RaycastHit blockerHit,
            Collider lastBlocker,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            out Vector3 waypoint)
        {
            waypoint = default;
            Collider blocker = blockerHit.collider;
            if (blocker == null)
            {
                return false;
            }

            Bounds bounds = blocker.bounds;
            if (!IsFinite(bounds.min) || !IsFinite(bounds.max) || !IsFinite(bounds.center))
            {
                return false;
            }

            float clearance = Mathf.Clamp(castRadius * 2.25f, 0.28f, 0.9f);
            float y = Mathf.Lerp(current.y, anchor.y, 0.5f);
            Vector3 center = bounds.center;
            Vector3 bestPoint = default;
            float bestScore = float.PositiveInfinity;

            for (int xi = 0; xi < 2; xi++)
            {
                for (int zi = 0; zi < 2; zi++)
                {
                    Vector3 corner = new Vector3(
                        xi == 0 ? bounds.min.x : bounds.max.x,
                        y,
                        zi == 0 ? bounds.min.z : bounds.max.z);

                    Vector3 outward = Vector3.ProjectOnPlane(corner - center, Vector3.up);
                    if (outward.sqrMagnitude <= 0.0001f)
                    {
                        continue;
                    }

                    Vector3 candidate = corner + outward.normalized * clearance;
                    ConsiderVisibilityWaypoint(candidate, current, anchor, blocker, lastBlocker, castRadius, ignoredA, ignoredB, ref bestPoint, ref bestScore);
                }
            }

            Vector3 hitNormal = Vector3.ProjectOnPlane(blockerHit.normal, Vector3.up);
            if (hitNormal.sqrMagnitude > 0.0001f)
            {
                hitNormal.Normalize();
                Vector3 tangent = Vector3.Cross(Vector3.up, hitNormal).normalized;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 sideCandidate = blockerHit.point + hitNormal * clearance + tangent * side * clearance * 2f;
                    sideCandidate.y = y;
                    ConsiderVisibilityWaypoint(sideCandidate, current, anchor, blocker, lastBlocker, castRadius, ignoredA, ignoredB, ref bestPoint, ref bestScore);
                }
            }

            if (!IsFinite(bestScore))
            {
                return false;
            }

            waypoint = bestPoint;
            return IsFinite(waypoint);
        }

        private static void ConsiderVisibilityWaypoint(
            Vector3 candidate,
            Vector3 current,
            Vector3 anchor,
            Collider blocker,
            Collider lastBlocker,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            ref Vector3 bestPoint,
            ref float bestScore)
        {
            if (!IsFinite(candidate) || (candidate - current).sqrMagnitude < 0.01f)
            {
                return;
            }

            float candidateCastRadius = Mathf.Min(castRadius, 0.08f);
            if (!IsSegmentClear(current, candidate, candidateCastRadius, ignoredA, ignoredB, blocker, lastBlocker))
            {
                return;
            }

            float currentToCandidate = Vector3.Distance(current, candidate);
            float candidateToAnchor = Vector3.Distance(candidate, anchor);
            if (!IsFinite(currentToCandidate) || !IsFinite(candidateToAnchor))
            {
                return;
            }

            float score = currentToCandidate + candidateToAnchor;
            if (TryGetFirstBlocker(candidate, anchor, castRadius, ignoredA, ignoredB, blocker, out RaycastHit nextHit))
            {
                score += nextHit.collider == blocker ? 8f : 2f;
            }

            if (blocker == lastBlocker)
            {
                score += 1f;
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestPoint = candidate;
            }
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static void MoveBodyWithSliding(
            Rigidbody body,
            Vector3 correction,
            Transform self,
            Transform ignoredRoot,
            float skin = 0.04f,
            int iterations = 4)
        {
            MoveBodyWithSliding(body, correction, self, ignoredRoot, skin, iterations, DefaultSlideAssist);
        }

        public static void MoveBodyWithSliding(
            Rigidbody body,
            Vector3 correction,
            Transform self,
            Transform ignoredRoot,
            float skin,
            int iterations,
            float slideAssist)
        {
            if (body == null || !IsFinite(body.position) || !IsFinite(correction) || correction.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            float correctionMagnitude = correction.magnitude;
            if (!IsFinite(correctionMagnitude))
            {
                return;
            }

            if (correctionMagnitude > MaxSlidingStep)
            {
                correction = correction / correctionMagnitude * MaxSlidingStep;
            }

            skin = Mathf.Clamp(skin, 0.012f, 0.08f);
            slideAssist = Mathf.Clamp(slideAssist, 0.25f, 30f);
            Vector3 remaining = correction;
            int maxIterations = Mathf.Clamp(iterations, 1, 16);
            for (int i = 0; i < maxIterations; i++)
            {
                if (!IsFinite(body.position) || !IsFinite(remaining))
                {
                    return;
                }

                float distance = remaining.magnitude;
                if (!IsFinite(distance) || distance <= 0.0001f)
                {
                    break;
                }

                Vector3 direction = remaining / distance;
                if (!IsFinite(direction))
                {
                    break;
                }

                if (!body.SweepTest(direction, out RaycastHit hit, distance + skin, QueryTriggerInteraction.Ignore))
                {
                    Vector3 nextPosition = body.position + remaining;
                    if (IsFinite(nextPosition))
                    {
                        body.position = nextPosition;
                    }

                    break;
                }

                if (ShouldIgnoreHit(hit.collider, self, ignoredRoot))
                {
                    Vector3 nextPosition = body.position + remaining;
                    if (IsFinite(nextPosition))
                    {
                        body.position = nextPosition;
                    }

                    break;
                }

                if (!IsFinite(hit.distance) || !IsFinite(hit.normal))
                {
                    break;
                }

                float moveDistance = Mathf.Max(0f, hit.distance - skin);
                if (moveDistance > 0f)
                {
                    Vector3 nextPosition = body.position + direction * moveDistance;
                    if (IsFinite(nextPosition))
                    {
                        body.position = nextPosition;
                    }
                }

                Vector3 surfaceNormal = hit.normal.normalized;
                if (!IsFinite(surfaceNormal) || surfaceNormal.sqrMagnitude <= 0.0001f)
                {
                    break;
                }

                RemoveVelocityIntoSurface(body, surfaceNormal);
                Vector3 leftover = remaining - direction * moveDistance;
                Vector3 slide = BuildBoostedCornerSlide(correction, leftover, direction, surfaceNormal, slideAssist);

                if (!IsFinite(slide))
                {
                    break;
                }

                remaining = Vector3.ClampMagnitude(slide * CornerSlideRetention, MaxSlidingStep);
            }
        }

        private static bool ShouldIgnoreHit(Collider collider, Transform self, Transform ignoredRoot)
        {
            if (collider == null)
            {
                return true;
            }

            Transform hitTransform = collider.transform;
            return (self != null && hitTransform.IsChildOf(self))
                || (ignoredRoot != null && hitTransform.IsChildOf(ignoredRoot));
        }

        private static void RemoveVelocityIntoSurface(Rigidbody body, Vector3 normal)
        {
            if (body == null || normal.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            if (!IsFinite(velocity))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                return;
            }

            float intoSurface = Vector3.Dot(velocity, -normal.normalized);
            if (intoSurface > 0f)
            {
                body.linearVelocity = velocity + normal.normalized * intoSurface;
            }
        }

        private static Vector3 BuildCornerEscapeSlide(Vector3 desiredCorrection, Vector3 surfaceNormal, float distance)
        {
            if (!IsFinite(desiredCorrection) || !IsFinite(surfaceNormal) || !IsFinite(distance))
            {
                return Vector3.zero;
            }

            Vector3 horizontalNormal = Vector3.ProjectOnPlane(surfaceNormal, Vector3.up);
            if (horizontalNormal.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 tangent = Vector3.Cross(Vector3.up, horizontalNormal.normalized);
            Vector3 desiredHorizontal = Vector3.ProjectOnPlane(desiredCorrection, Vector3.up);
            if (desiredHorizontal.sqrMagnitude > 0.0001f && Vector3.Dot(tangent, desiredHorizontal) < 0f)
            {
                tangent = -tangent;
            }

            return tangent.normalized * distance;
        }

        private static Vector3 BuildBoostedCornerSlide(Vector3 correction, Vector3 leftover, Vector3 incomingDirection, Vector3 surfaceNormal, float slideAssist)
        {
            if (!IsFinite(correction) || !IsFinite(leftover) || !IsFinite(incomingDirection) || !IsFinite(surfaceNormal))
            {
                return Vector3.zero;
            }

            Vector3 slide = Vector3.ProjectOnPlane(leftover, surfaceNormal);
            float leftoverMagnitude = leftover.magnitude;
            if (!IsFinite(leftoverMagnitude) || leftoverMagnitude <= 0.0001f)
            {
                return slide;
            }

            float intoSurface = Mathf.Clamp01(Vector3.Dot(incomingDirection, -surfaceNormal));
            Vector3 escapeSlide = BuildCornerEscapeSlide(correction, surfaceNormal, leftoverMagnitude);
            if (escapeSlide.sqrMagnitude <= 0.0001f)
            {
                return slide;
            }

            Vector3 escapeDirection = escapeSlide.normalized;
            if (!IsFinite(escapeDirection))
            {
                return slide;
            }

            if (slide.sqrMagnitude <= leftoverMagnitude * leftoverMagnitude * 0.12f)
            {
                slide = escapeDirection * leftoverMagnitude;
            }
            else if (Vector3.Dot(slide, escapeDirection) < 0f)
            {
                slide = Vector3.ProjectOnPlane(slide, escapeDirection) + escapeDirection * Mathf.Min(slide.magnitude, leftoverMagnitude);
            }

            float boost = Mathf.Lerp(1f, CornerSlideBoost * slideAssist, intoSurface);
            slide += escapeDirection * leftoverMagnitude * intoSurface * (CornerSlideBoost * slideAssist - 1f);
            return Vector3.ClampMagnitude(slide * boost, MaxSlidingStep);
        }

        private static void UpdateWrapPoints(
            Vector3 attach,
            Vector3 anchor,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            RopeState state)
        {
            UnwrapIfPossible(attach, anchor, castRadius, ignoredA, ignoredB, state);

            for (int safety = 0; safety < MaxWrapPoints; safety++)
            {
                bool addedWrap = false;
                for (int segmentIndex = -1; segmentIndex < state.WrapCount; segmentIndex++)
                {
                    Vector3 start = segmentIndex < 0 ? attach : state.WrapPoints[segmentIndex];
                    Vector3 target = segmentIndex + 1 < state.WrapCount ? state.WrapPoints[segmentIndex + 1] : anchor;
                    Collider allowedStartCollider = segmentIndex >= 0 ? state.WrapColliders[segmentIndex] : null;
                    Collider allowedTargetCollider = segmentIndex + 1 < state.WrapCount ? state.WrapColliders[segmentIndex + 1] : null;
                    if (!TryGetFirstBlocker(start, target, castRadius, ignoredA, ignoredB, allowedStartCollider, allowedTargetCollider, out RaycastHit hit))
                    {
                        continue;
                    }

                    if (state.WrapCount >= MaxWrapPoints)
                    {
                        return;
                    }

                    Vector3 wrapPoint = FindWrapPoint(hit, start, target, castRadius, ignoredA, ignoredB, allowedStartCollider, allowedTargetCollider);
                    if (!IsFinite(wrapPoint) || Vector3.Distance(wrapPoint, attach) > MaxRopeSolveLength || Vector3.Distance(wrapPoint, anchor) > MaxRopeSolveLength)
                    {
                        state.Clear();
                        return;
                    }

                    int insertIndex = segmentIndex + 1;
                    if (insertIndex > 0 && state.WrapColliders[insertIndex - 1] == hit.collider && (state.WrapPoints[insertIndex - 1] - wrapPoint).sqrMagnitude < 0.05f)
                    {
                        continue;
                    }

                    if (insertIndex < state.WrapCount && state.WrapColliders[insertIndex] == hit.collider && (state.WrapPoints[insertIndex] - wrapPoint).sqrMagnitude < 0.05f)
                    {
                        continue;
                    }

                    for (int i = Mathf.Min(state.WrapCount, MaxWrapPoints - 1); i > insertIndex; i--)
                    {
                        state.WrapPoints[i] = state.WrapPoints[i - 1];
                        state.WrapColliders[i] = state.WrapColliders[i - 1];
                    }

                    state.WrapPoints[insertIndex] = wrapPoint;
                    state.WrapColliders[insertIndex] = hit.collider;
                    state.WrapCount++;
                    addedWrap = true;
                    break;
                }

                if (!addedWrap)
                {
                    return;
                }
            }
        }

        private static void UnwrapIfPossible(
            Vector3 attach,
            Vector3 anchor,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            RopeState state)
        {
            while (state.WrapCount > 0)
            {
                Vector3 nextTarget = state.WrapCount > 1 ? state.WrapPoints[1] : anchor;
                Collider allowedTargetCollider = state.WrapCount > 1 ? state.WrapColliders[1] : null;
                if (!IsFinite(nextTarget))
                {
                    state.Clear();
                    return;
                }

                if (!IsSegmentClear(attach, nextTarget, castRadius, ignoredA, ignoredB, allowedTargetCollider))
                {
                    break;
                }

                for (int i = 0; i < state.WrapCount - 1; i++)
                {
                    state.WrapPoints[i] = state.WrapPoints[i + 1];
                    state.WrapColliders[i] = state.WrapColliders[i + 1];
                }

                state.WrapColliders[state.WrapCount - 1] = null;
                state.WrapCount--;
            }
        }

        private static Vector3 FindWrapPoint(
            RaycastHit hit,
            Vector3 attach,
            Vector3 target,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            Collider allowedStartCollider,
            Collider allowedTargetCollider)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null || !IsFinite(hit.point) || !IsFinite(hit.normal))
            {
                return attach;
            }

            Bounds bounds = hitCollider.bounds;
            if (!IsFinite(bounds.min) || !IsFinite(bounds.max) || !IsFinite(bounds.center))
            {
                return attach;
            }

            float y = Mathf.Clamp(hit.point.y, bounds.min.y + 0.15f, bounds.max.y - 0.15f);
            Vector3 bestPoint = hit.point + hit.normal * WrapSurfaceClearance;
            float bestScore = float.MaxValue;
            Vector3 horizontalNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
            if (horizontalNormal.sqrMagnitude > 0.0001f)
            {
                horizontalNormal.Normalize();
                Vector3 tangent = Vector3.Cross(Vector3.up, horizontalNormal).normalized;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (TryFindSurfaceEdge(hitCollider, hit.point, horizontalNormal, tangent * side, castRadius, out Vector3 edgePoint))
                    {
                        ConsiderWrapCandidate(edgePoint, hitCollider, allowedStartCollider, allowedTargetCollider, attach, target, castRadius, ignoredA, ignoredB, ref bestPoint, ref bestScore);
                    }
                }
            }

            for (int xi = 0; xi < 2; xi++)
            {
                for (int zi = 0; zi < 2; zi++)
                {
                    Vector3 corner = new Vector3(
                        xi == 0 ? bounds.min.x : bounds.max.x,
                        y,
                        zi == 0 ? bounds.min.z : bounds.max.z);

                    Vector3 outward = Vector3.ProjectOnPlane(corner - bounds.center, Vector3.up);
                    if (outward.sqrMagnitude < 0.0001f)
                    {
                        outward = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
                    }

                    if (outward.sqrMagnitude < 0.0001f)
                    {
                        outward = hit.normal;
                    }

                    Vector3 candidate = corner + outward.normalized * WrapSurfaceClearance;
                    ConsiderWrapCandidate(candidate, hitCollider, allowedStartCollider, allowedTargetCollider, attach, target, castRadius, ignoredA, ignoredB, ref bestPoint, ref bestScore);
                }
            }

            return bestPoint;
        }

        private static bool TryFindSurfaceEdge(
            Collider collider,
            Vector3 hitPoint,
            Vector3 normal,
            Vector3 sideDirection,
            float castRadius,
            out Vector3 edgePoint)
        {
            float offset = WrapSurfaceClearance;
            float leaveDistance = Mathf.Max(0.18f, castRadius * 0.55f);
            Vector3 lastGood = hitPoint + normal * offset;

            for (float distance = 0.25f; distance <= 28f; distance += 0.25f)
            {
                Vector3 sample = hitPoint + sideDirection * distance + normal * offset;
                if (!IsFinite(sample))
                {
                    break;
                }

                Vector3 closest = collider.ClosestPoint(sample);
                if (!IsFinite(closest))
                {
                    break;
                }

                if ((closest - sample).sqrMagnitude > leaveDistance * leaveDistance)
                {
                    edgePoint = lastGood + sideDirection * 0.08f;
                    return true;
                }

                lastGood = sample;
            }

            edgePoint = default;
            return false;
        }

        private static void ConsiderWrapCandidate(
            Vector3 candidate,
            Collider hitCollider,
            Collider allowedStartCollider,
            Collider allowedTargetCollider,
            Vector3 attach,
            Vector3 target,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            ref Vector3 bestPoint,
            ref float bestScore)
        {
            if (!IsFinite(candidate))
            {
                return;
            }

            if (!IsSegmentClear(attach, candidate, Mathf.Min(castRadius, 0.09f), ignoredA, ignoredB, hitCollider, allowedStartCollider))
            {
                return;
            }

            if (!IsSegmentClear(candidate, target, Mathf.Min(castRadius, 0.09f), ignoredA, ignoredB, hitCollider, allowedTargetCollider))
            {
                return;
            }

            float score = Vector3.Distance(attach, candidate) + Vector3.Distance(candidate, target);
            if (IsFinite(score) && score < bestScore)
            {
                bestScore = score;
                bestPoint = candidate;
            }
        }

        private static bool IsSegmentClear(
            Vector3 start,
            Vector3 end,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            Collider allowedCollider)
        {
            return IsSegmentClear(start, end, castRadius, ignoredA, ignoredB, allowedCollider, null);
        }

        private static bool IsSegmentClear(
            Vector3 start,
            Vector3 end,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            Collider allowedColliderA,
            Collider allowedColliderB)
        {
            return !TryGetFirstBlocker(start, end, castRadius, ignoredA, ignoredB, allowedColliderA, allowedColliderB, out _);
        }

        private static bool TryGetFirstBlocker(
            Vector3 start,
            Vector3 end,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            Collider allowedCollider,
            out RaycastHit bestHit)
        {
            return TryGetFirstBlocker(start, end, castRadius, ignoredA, ignoredB, allowedCollider, null, out bestHit);
        }

        private static bool TryGetFirstBlocker(
            Vector3 start,
            Vector3 end,
            float castRadius,
            Transform ignoredA,
            Transform ignoredB,
            Collider allowedColliderA,
            Collider allowedColliderB,
            out RaycastHit bestHit)
        {
            bestHit = default;
            Vector3 toEnd = end - start;
            float distance = toEnd.magnitude;
            if (!IsFinite(start) || !IsFinite(end) || !IsFinite(distance) || distance <= 0.08f || distance > MaxRopeSolveLength)
            {
                return false;
            }

            Vector3 direction = toEnd / distance;
            if (TryGetFirstBlockerFromHits(
                Physics.SphereCastAll(start, castRadius, direction, distance, ~0, QueryTriggerInteraction.Ignore),
                ignoredA,
                ignoredB,
                allowedColliderA,
                allowedColliderB,
                out bestHit))
            {
                return true;
            }

            return TryGetFirstBlockerFromHits(
                Physics.RaycastAll(start, direction, distance, ~0, QueryTriggerInteraction.Ignore),
                ignoredA,
                ignoredB,
                allowedColliderA,
                allowedColliderB,
                out bestHit);
        }

        private static bool TryGetFirstBlockerFromHits(
            RaycastHit[] hits,
            Transform ignoredA,
            Transform ignoredB,
            Collider allowedColliderA,
            Collider allowedColliderB,
            out RaycastHit bestHit)
        {
            bestHit = default;
            int validCount = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != null && IsFinite(hits[i].distance) && IsFinite(hits[i].point) && IsFinite(hits[i].normal))
                {
                    hits[validCount] = hits[i];
                    validCount++;
                }
            }

            if (validCount <= 0)
            {
                return false;
            }

            System.Array.Sort(hits, 0, validCount, Comparer.Instance);
            for (int i = 0; i < validCount; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider == allowedColliderA || hitCollider == allowedColliderB || hits[i].distance <= 0.03f)
                {
                    continue;
                }

                Transform hitTransform = hitCollider.transform;
                if ((ignoredA != null && hitTransform.IsChildOf(ignoredA)) || (ignoredB != null && hitTransform.IsChildOf(ignoredB)))
                {
                    continue;
                }

                bestHit = hits[i];
                return true;
            }

            return false;
        }

        private static void SanitizeWrapPoints(RopeState state)
        {
            if (state == null)
            {
                return;
            }

            int write = 0;
            for (int read = 0; read < state.WrapCount && read < MaxWrapPoints; read++)
            {
                Vector3 point = state.WrapPoints[read];
                if (IsFinite(point) && state.WrapColliders[read] != null)
                {
                    state.WrapPoints[write] = point;
                    state.WrapColliders[write] = state.WrapColliders[read];
                    write++;
                }
            }

            for (int i = write; i < MaxWrapPoints; i++)
            {
                state.WrapColliders[i] = null;
            }

            state.WrapCount = write;
        }

        private sealed class Comparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly Comparer Instance = new Comparer();

            public int Compare(RaycastHit left, RaycastHit right)
            {
                return left.distance.CompareTo(right.distance);
            }
        }
    }
}
