using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public readonly struct ParkourLedgeTarget_y
    {
        public ParkourLedgeTarget_y(
            Collider wallCollider,
            Vector3 climbDirection,
            Vector3 wallNormal,
            Vector3 topPoint,
            Vector3 leftHandContactPoint,
            Vector3 rightHandContactPoint,
            Vector3 hangEntryRootPosition,
            Vector3 hangRootPosition,
            Vector3 landingRootPosition,
            bool hasLandingPosition,
            float wallHeight,
            float frontDistance,
            float totalForwardDistance)
        {
            WallCollider = wallCollider;
            ClimbDirection = climbDirection;
            WallNormal = wallNormal;
            TopPoint = topPoint;
            LeftHandContactPoint = leftHandContactPoint;
            RightHandContactPoint = rightHandContactPoint;
            HangEntryRootPosition = hangEntryRootPosition;
            HangRootPosition = hangRootPosition;
            LandingRootPosition = landingRootPosition;
            HasLandingPosition = hasLandingPosition;
            WallHeight = wallHeight;
            FrontDistance = frontDistance;
            TotalForwardDistance = totalForwardDistance;
        }

        public Collider WallCollider { get; }
        public Vector3 ClimbDirection { get; }
        public Vector3 WallNormal { get; }
        public Vector3 TopPoint { get; }
        public Vector3 LeftHandContactPoint { get; }
        public Vector3 RightHandContactPoint { get; }
        public Vector3 HangEntryRootPosition { get; }
        public Vector3 HangRootPosition { get; }
        public Vector3 LandingRootPosition { get; }
        public bool HasLandingPosition { get; }
        public float WallHeight { get; }
        public float FrontDistance { get; }
        public float TotalForwardDistance { get; }
    }

    public sealed class ParkourLedgeDetector_y
    {
        private const float NaturalContactDistance = 1.70f;
        private const float NaturalTotalForwardDistance = 3.065f;
        private const float MinimumDistanceScale = 0.8f;
        private const float MaximumDistanceScale = 1.2f;
        private const float MaximumApproachAngle = 25f;
        private const float TopProbeInset = 0.08f;
        private const float TopHeightTolerance = 0.16f;
        private const float HangingHandHeight = 1.664f;
        private const float JumpingToHangingEntryHandHeight = 1.865f;
        private const float HangingHandForward = 0.247f;
        private const float HangingHandHalfSpan = 0.223f;
        private const float MinimumHangHopDistance = HangingHandHalfSpan * 2f;
        private const float JumpingToHangingForwardTravel = 0.766f;
        private const float BracedHangToCrouchForwardTravel = 0.656f;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[12];
        private readonly Collider[] _overlapBuffer = new Collider[12];
        private readonly Transform _actorTransform;
        private readonly CharacterController _controller;
        private readonly float _detectionDistance;
        private readonly float _minimumHeight;
        private readonly float _maximumHeight;

        public ParkourLedgeDetector_y(
            Transform actorTransform,
            CharacterController controller,
            float detectionDistance,
            float minimumHeight,
            float maximumHeight)
        {
            _actorTransform = actorTransform;
            _controller = controller;
            _detectionDistance = detectionDistance;
            _minimumHeight = minimumHeight;
            _maximumHeight = maximumHeight;
        }

        public bool TryFindSprintWallClimbTarget(
            Vector3 desiredDirection,
            out ParkourLedgeTarget_y target)
        {
            target = default;
            Vector3 direction = Vector3.ProjectOnPlane(desiredDirection, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.9f)
            {
                return false;
            }

            if (!TryFindFrontFace(direction, out RaycastHit frontHit))
            {
                return false;
            }

            Vector3 wallDirection = Vector3.ProjectOnPlane(-frontHit.normal, Vector3.up).normalized;
            if (wallDirection.sqrMagnitude < 0.9f
                || Vector3.Angle(direction, wallDirection) > MaximumApproachAngle)
            {
                return false;
            }

            float frontDistance = Vector3.Dot(
                Vector3.ProjectOnPlane(frontHit.point - _actorTransform.position, Vector3.up),
                wallDirection);
            float distanceScale = frontDistance / NaturalContactDistance;
            if (distanceScale < MinimumDistanceScale || distanceScale > MaximumDistanceScale)
            {
                return false;
            }

            if (!TryFindTop(frontHit, wallDirection, out RaycastHit topHit))
            {
                return false;
            }

            float controllerBottomOffset = _controller.center.y - _controller.height * 0.5f;
            float wallHeight = topHit.point.y - (_actorTransform.position.y + controllerBottomOffset);
            if (wallHeight < _minimumHeight || wallHeight > _maximumHeight)
            {
                return false;
            }

            float totalForwardDistance = NaturalTotalForwardDistance * distanceScale;
            Vector3 landingPlanarPosition = _actorTransform.position + wallDirection * totalForwardDistance;
            if (!TryFindLanding(
                    landingPlanarPosition,
                    topHit.point.y,
                    frontHit.collider,
                    controllerBottomOffset,
                    out Vector3 landingRootPosition))
            {
                return false;
            }

            Vector3 ledgePoint = CalculateLedgePoint(frontHit, topHit);
            if (!TryResolveHandContacts(
                    ledgePoint,
                    wallDirection,
                    topHit.point.y,
                    out Vector3 leftHandContactPoint,
                    out Vector3 rightHandContactPoint))
            {
                return false;
            }

            Vector3 contactCenter = (leftHandContactPoint + rightHandContactPoint) * 0.5f;
            Vector3 hangRootPosition = CalculateHangRootPosition(contactCenter, wallDirection);
            target = new ParkourLedgeTarget_y(
                frontHit.collider,
                wallDirection,
                frontHit.normal,
                contactCenter,
                leftHandContactPoint,
                rightHandContactPoint,
                CalculateHangEntryRootPosition(hangRootPosition),
                hangRootPosition,
                landingRootPosition,
                true,
                wallHeight,
                frontDistance,
                totalForwardDistance);
            return true;
        }

        public bool TryFindHangingTarget(
            Vector3 approachDirection,
            out ParkourLedgeTarget_y target)
        {
            target = default;
            Vector3 direction = Vector3.ProjectOnPlane(approachDirection, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.9f || !TryFindFrontFace(direction, out RaycastHit frontHit))
            {
                return false;
            }

            Vector3 wallDirection = Vector3.ProjectOnPlane(-frontHit.normal, Vector3.up).normalized;
            if (wallDirection.sqrMagnitude < 0.9f ||
                Vector3.Angle(direction, wallDirection) > MaximumApproachAngle ||
                !TryFindTop(frontHit, wallDirection, out RaycastHit topHit))
            {
                return false;
            }

            Vector3 ledgePoint = CalculateLedgePoint(frontHit, topHit);
            if (!TryResolveHandContacts(
                    ledgePoint,
                    wallDirection,
                    topHit.point.y,
                    out Vector3 leftHandContactPoint,
                    out Vector3 rightHandContactPoint))
            {
                return false;
            }

            Vector3 contactCenter = (leftHandContactPoint + rightHandContactPoint) * 0.5f;
            Vector3 hangRootPosition = CalculateHangRootPosition(contactCenter, wallDirection);
            Vector3 hangEntryRootPosition = CalculateHangEntryRootPosition(hangRootPosition);
            Vector3 toHangRoot = hangEntryRootPosition - _actorTransform.position;
            float forwardDistance = Vector3.Dot(
                Vector3.ProjectOnPlane(toHangRoot, Vector3.up),
                wallDirection);
            float dropDistance = -toHangRoot.y;
            float lateralDistance = Vector3.ProjectOnPlane(
                toHangRoot - wallDirection * forwardDistance,
                Vector3.up).magnitude;
            float maximumDrop = _controller.height * 0.5f + _controller.skinWidth;
            if (forwardDistance < -_controller.skinWidth ||
                forwardDistance > JumpingToHangingForwardTravel + _controller.radius ||
                dropDistance < -_controller.skinWidth ||
                dropDistance > maximumDrop ||
                lateralDistance > _controller.radius)
            {
                return false;
            }

            return TryCreateHangingTarget(
                frontHit,
                topHit,
                wallDirection,
                leftHandContactPoint,
                rightHandContactPoint,
                out target);
        }

        public bool TryFindHangHopTarget(
            ParkourLedgeTarget_y currentTarget,
            float lateralDistance,
            out ParkourLedgeTarget_y target)
        {
            float directionSign = Mathf.Sign(lateralDistance);
            float requestedDistance = Mathf.Abs(lateralDistance);
            if (requestedDistance < MinimumHangHopDistance)
            {
                target = default;
                return false;
            }

            if (TryFindHangHopTargetAtDistance(
                    currentTarget,
                    directionSign * requestedDistance,
                    out target))
            {
                return true;
            }

            if (!TryFindHangHopTargetAtDistance(
                    currentTarget,
                    directionSign * MinimumHangHopDistance,
                    out ParkourLedgeTarget_y minimumTarget))
            {
                target = default;
                return false;
            }

            float minimumDistance = MinimumHangHopDistance;
            float maximumDistance = requestedDistance;
            target = minimumTarget;
            for (int iteration = 0; iteration < 8; iteration++)
            {
                float candidateDistance = (minimumDistance + maximumDistance) * 0.5f;
                if (TryFindHangHopTargetAtDistance(
                        currentTarget,
                        directionSign * candidateDistance,
                        out ParkourLedgeTarget_y candidateTarget))
                {
                    minimumDistance = candidateDistance;
                    target = candidateTarget;
                }
                else
                {
                    maximumDistance = candidateDistance;
                }
            }

            return true;
        }

        private bool TryFindHangHopTargetAtDistance(
            ParkourLedgeTarget_y currentTarget,
            float lateralDistance,
            out ParkourLedgeTarget_y target)
        {
            target = default;
            Vector3 tangent = Vector3.Cross(Vector3.up, currentTarget.ClimbDirection).normalized;
            Vector3 desiredHangRoot = currentTarget.HangRootPosition + tangent * lateralDistance;
            Vector3 faceOrigin = desiredHangRoot + Vector3.up * (HangingHandHeight * 0.55f);
            if (!Physics.Raycast(
                    faceOrigin,
                    currentTarget.ClimbDirection,
                    out RaycastHit frontHit,
                    HangingHandForward + _controller.radius + 0.25f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) ||
                !IsUsableEnvironmentCollider(frontHit.collider) ||
                Mathf.Abs(frontHit.normal.y) > 0.25f)
            {
                return false;
            }

            Vector3 wallDirection = Vector3.ProjectOnPlane(-frontHit.normal, Vector3.up).normalized;
            if (Vector3.Angle(currentTarget.ClimbDirection, wallDirection) > MaximumApproachAngle ||
                !TryFindTop(frontHit, wallDirection, out RaycastHit topHit) ||
                Mathf.Abs(topHit.point.y - currentTarget.TopPoint.y) > TopHeightTolerance)
            {
                return false;
            }

            Vector3 ledgePoint = CalculateLedgePoint(frontHit, topHit);
            if (!TryResolveHandContacts(
                    ledgePoint,
                    wallDirection,
                    currentTarget.TopPoint.y,
                    out Vector3 leftHandContactPoint,
                    out Vector3 rightHandContactPoint))
            {
                return false;
            }

            return TryCreateHangingTarget(
                frontHit,
                topHit,
                wallDirection,
                leftHandContactPoint,
                rightHandContactPoint,
                out target);
        }

        private bool TryResolveHandContacts(
            Vector3 ledgeCenter,
            Vector3 wallDirection,
            float expectedTopHeight,
            out Vector3 leftHandContactPoint,
            out Vector3 rightHandContactPoint)
        {
            leftHandContactPoint = default;
            rightHandContactPoint = default;
            Vector3 tangent = Vector3.Cross(Vector3.up, wallDirection).normalized;
            if (!TryResolveHandContact(
                    ledgeCenter - tangent * HangingHandHalfSpan,
                    wallDirection,
                    expectedTopHeight,
                    out leftHandContactPoint))
            {
                return false;
            }

            return TryResolveHandContact(
                ledgeCenter + tangent * HangingHandHalfSpan,
                wallDirection,
                expectedTopHeight,
                out rightHandContactPoint);
        }

        private bool TryResolveHandContact(
            Vector3 expectedContactPoint,
            Vector3 wallDirection,
            float expectedTopHeight,
            out Vector3 contactPoint)
        {
            contactPoint = default;
            float faceProbeDistance = HangingHandForward + _controller.radius + 0.25f;
            Vector3 faceOrigin = expectedContactPoint
                                 - wallDirection * faceProbeDistance
                                 - Vector3.up * TopProbeInset;
            int faceHitCount = Physics.RaycastNonAlloc(
                faceOrigin,
                wallDirection,
                _hitBuffer,
                faceProbeDistance * 2f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            RaycastHit frontHit = default;
            float closestFaceDistance = float.MaxValue;
            for (int index = 0; index < faceHitCount; index++)
            {
                RaycastHit candidate = _hitBuffer[index];
                if (!IsUsableEnvironmentCollider(candidate.collider)
                    || Mathf.Abs(candidate.normal.y) > 0.25f
                    || Vector3.Angle(-candidate.normal, wallDirection) > MaximumApproachAngle
                    || candidate.distance >= closestFaceDistance)
                {
                    continue;
                }

                frontHit = candidate;
                closestFaceDistance = candidate.distance;
            }

            if (frontHit.collider == null)
            {
                return false;
            }

            Vector3 topOrigin = frontHit.point
                                + wallDirection * TopProbeInset
                                + Vector3.up * (TopHeightTolerance + TopProbeInset);
            int topHitCount = Physics.RaycastNonAlloc(
                topOrigin,
                Vector3.down,
                _hitBuffer,
                TopHeightTolerance * 2f + TopProbeInset * 2f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            RaycastHit topHit = default;
            float smallestHeightDifference = float.MaxValue;
            for (int index = 0; index < topHitCount; index++)
            {
                RaycastHit candidate = _hitBuffer[index];
                float heightDifference = Mathf.Abs(candidate.point.y - expectedTopHeight);
                if (!IsUsableEnvironmentCollider(candidate.collider)
                    || candidate.normal.y < 0.65f
                    || heightDifference > TopHeightTolerance
                    || heightDifference >= smallestHeightDifference)
                {
                    continue;
                }

                topHit = candidate;
                smallestHeightDifference = heightDifference;
            }

            if (topHit.collider == null)
            {
                return false;
            }

            contactPoint = frontHit.point;
            contactPoint.y = topHit.point.y;
            return true;
        }

        private bool TryCreateHangingTarget(
            RaycastHit frontHit,
            RaycastHit topHit,
            Vector3 wallDirection,
            Vector3 leftHandContactPoint,
            Vector3 rightHandContactPoint,
            out ParkourLedgeTarget_y target)
        {
            Vector3 contactCenter = (leftHandContactPoint + rightHandContactPoint) * 0.5f;
            Vector3 hangRootPosition = CalculateHangRootPosition(contactCenter, wallDirection);
            float controllerBottomOffset = _controller.center.y - _controller.height * 0.5f;
            Vector3 landingPlanarPosition = hangRootPosition
                                            + wallDirection * BracedHangToCrouchForwardTravel;
            bool hasLandingPosition = TryFindLanding(
                landingPlanarPosition,
                topHit.point.y,
                frontHit.collider,
                controllerBottomOffset,
                out Vector3 landingRootPosition);

            float wallHeight = topHit.point.y - (_actorTransform.position.y + controllerBottomOffset);
            target = new ParkourLedgeTarget_y(
                frontHit.collider,
                wallDirection,
                frontHit.normal,
                contactCenter,
                leftHandContactPoint,
                rightHandContactPoint,
                CalculateHangEntryRootPosition(hangRootPosition),
                hangRootPosition,
                landingRootPosition,
                hasLandingPosition,
                wallHeight,
                Vector3.Distance(_actorTransform.position, frontHit.point),
                hasLandingPosition
                    ? Vector3.Distance(_actorTransform.position, landingRootPosition)
                    : 0f);
            return true;
        }

        private Vector3 CalculateHangRootPosition(Vector3 topPoint, Vector3 wallDirection)
        {
            float bodyClearance = Mathf.Max(
                HangingHandForward,
                _controller.radius + _controller.skinWidth);
            return topPoint - wallDirection * bodyClearance - Vector3.up * HangingHandHeight;
        }

        private static Vector3 CalculateHangEntryRootPosition(Vector3 hangRootPosition)
        {
            float entryHeightDifference = JumpingToHangingEntryHandHeight - HangingHandHeight;
            return hangRootPosition - Vector3.up * entryHeightDifference;
        }

        private static Vector3 CalculateLedgePoint(RaycastHit frontHit, RaycastHit topHit)
        {
            Vector3 ledgePoint = frontHit.point;
            ledgePoint.y = topHit.point.y;
            return ledgePoint;
        }

        private bool TryFindFrontFace(Vector3 direction, out RaycastHit closestHit)
        {
            float radius = Mathf.Max(0.01f, _controller.radius - _controller.skinWidth);
            Vector3 center = _actorTransform.TransformPoint(_controller.center);
            float halfSegment = Mathf.Max(0f, _controller.height * 0.5f - radius);
            Vector3 bottom = center - Vector3.up * halfSegment;
            Vector3 top = center + Vector3.up * halfSegment;
            int hitCount = Physics.CapsuleCastNonAlloc(
                bottom,
                top,
                radius,
                direction,
                _hitBuffer,
                _detectionDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            closestHit = default;
            float closestDistance = float.MaxValue;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _hitBuffer[index];
                if (!IsUsableEnvironmentCollider(hit.collider)
                    || Mathf.Abs(hit.normal.y) > 0.25f
                    || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestHit = hit;
                closestDistance = hit.distance;
            }

            return closestHit.collider != null;
        }

        private bool TryFindTop(
            RaycastHit frontHit,
            Vector3 wallDirection,
            out RaycastHit topHit)
        {
            Vector3 origin = frontHit.point
                             + wallDirection * TopProbeInset
                             + Vector3.up * (_maximumHeight + 0.4f);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _hitBuffer,
                _maximumHeight + 0.8f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            topHit = default;
            float expectedTopHeight = frontHit.collider.bounds.max.y;
            float smallestHeightDifference = float.MaxValue;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _hitBuffer[index];
                float heightDifference = Mathf.Abs(hit.point.y - expectedTopHeight);
                if (!IsUsableEnvironmentCollider(hit.collider)
                    || hit.normal.y < 0.65f
                    || heightDifference >= smallestHeightDifference)
                {
                    continue;
                }

                topHit = hit;
                smallestHeightDifference = heightDifference;
            }

            return topHit.collider != null && smallestHeightDifference <= TopHeightTolerance;
        }

        private bool TryFindLanding(
            Vector3 planarPosition,
            float expectedTopHeight,
            Collider wallCollider,
            float controllerBottomOffset,
            out Vector3 landingRootPosition)
        {
            Vector3 origin = new(
                planarPosition.x,
                expectedTopHeight + 0.5f,
                planarPosition.z);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _hitBuffer,
                1f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            RaycastHit landingHit = default;
            float smallestHeightDifference = float.MaxValue;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _hitBuffer[index];
                if (!IsUsableEnvironmentCollider(hit.collider) || hit.normal.y < 0.65f)
                {
                    continue;
                }

                float heightDifference = Mathf.Abs(hit.point.y - expectedTopHeight);
                if (heightDifference >= smallestHeightDifference)
                {
                    continue;
                }

                landingHit = hit;
                smallestHeightDifference = heightDifference;
            }

            if (landingHit.collider == null || smallestHeightDifference > TopHeightTolerance)
            {
                landingRootPosition = default;
                return false;
            }

            landingRootPosition = new Vector3(
                planarPosition.x,
                landingHit.point.y - controllerBottomOffset,
                planarPosition.z);
            return IsLandingCapsuleClear(landingRootPosition, wallCollider, landingHit.collider);
        }

        private bool IsLandingCapsuleClear(
            Vector3 rootPosition,
            Collider wallCollider,
            Collider landingCollider)
        {
            float radius = Mathf.Max(0.01f, _controller.radius - _controller.skinWidth);
            Vector3 center = rootPosition + _actorTransform.rotation * _controller.center;
            float halfSegment = Mathf.Max(0f, _controller.height * 0.5f - radius);
            Vector3 bottom = center - Vector3.up * halfSegment;
            Vector3 top = center + Vector3.up * halfSegment;
            int overlapCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                _overlapBuffer,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int index = 0; index < overlapCount; index++)
            {
                Collider overlap = _overlapBuffer[index];
                if (overlap == null
                    || overlap == wallCollider
                    || overlap == landingCollider
                    || !IsUsableEnvironmentCollider(overlap))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private bool IsUsableEnvironmentCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            Transform colliderTransform = collider.transform;
            return colliderTransform != _actorTransform
                   && !colliderTransform.IsChildOf(_actorTransform);
        }
    }
}
