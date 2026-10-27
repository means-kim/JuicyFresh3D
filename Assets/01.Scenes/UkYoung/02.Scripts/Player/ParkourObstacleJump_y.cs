using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    [System.Serializable]
    public struct ObstacleJumpFeel_y
    {
        [Min(0.1f)] [SerializeField] private float _forwardDistance;
        [Range(-0.5f, 1f)] [SerializeField] private float _heightOffset;
        [Range(0.5f, 1.5f)] [SerializeField] private float _animationSpeed;
        [Range(0f, 1.2f)] [SerializeField] private float _speedRetention;

        public float ForwardDistance => _forwardDistance > 0f ? _forwardDistance : 1f;
        public float HeightOffset => _heightOffset;
        public float AnimationSpeed => _animationSpeed > 0f ? _animationSpeed : 1f;
        public float SpeedRetention => _speedRetention;

        public static ObstacleJumpFeel_y Create(
            float forwardDistance,
            float heightOffset,
            float animationSpeed,
            float speedRetention)
        {
            return new ObstacleJumpFeel_y
            {
                _forwardDistance = forwardDistance,
                _heightOffset = heightOffset,
                _animationSpeed = animationSpeed,
                _speedRetention = speedRetention,
            };
        }
    }

    public sealed class ParkourObstacleJump_y
    {
        private const float DetectionLeadTime = 0.18f;
        private const float MinimumDetectionDistance = 0.85f;
        private const float TopProbeInset = 0.08f;
        private const float HandContactBlendStartDistance = 0.9f;
        private const float HandContactFullDistance = 0.45f;
        private const float HandContactReleaseStartDistance = -0.3f;
        private const float HandContactReleaseEndDistance = -0.65f;
        private const float TorsoSurfaceMargin = 0.08f;
        private const float MinimumCrossingWindow = 0.16f;
        private const float MinimumMotionInterval = 0.04f;
        private const float ExitRunoutDistance = 0.25f;
        private static readonly float[] ProbeHeights = { 0.45f, 0.75f, 1.05f, 1.35f };
        private static readonly VaultMotionProfile[] MotionProfiles =
        {
            new(
                1.166667f / 1.15f,
                0.1564f,
                0.49f,
                new[]
                {
                    0.712f, 0.7779f, 0.7349f, 0.7485f, 0.8956f, 0.9794f,
                    1.0156f, 1.026f, 1.0487f, 1.0868f, 1.1031f, 1.0034f,
                    0.8683f, 0.7577f, 0.7687f, 0.8237f, 0.7238f,
                }),
            new(
                1.2f / 1.2f,
                0.2819f,
                0.216f,
                new[]
                {
                    0.6974f, 0.7423f, 0.9034f, 1.0244f, 1.0489f, 1.0322f,
                    0.9688f, 0.8824f, 0.8504f, 0.8459f, 0.8508f, 0.8318f,
                    0.7894f, 0.7654f, 0.7443f, 0.7269f, 0.7495f,
                }),
            new(
                2.083333f / 1.45f,
                0.1145f,
                0.434f,
                new[]
                {
                    0.6788f, 0.6523f, 0.7196f, 0.8966f, 1.1516f, 1.3194f,
                    1.3515f, 1.1901f, 0.9886f, 0.6887f, 0.497f, 0.6094f,
                    0.6301f, 0.6607f, 0.6913f, 0.6933f, 0.7181f,
                }),
            new(
                2.25f / 1.45f,
                0.1573f,
                0.594f,
                new[]
                {
                    0.6719f, 0.8025f, 0.7895f, 0.6269f, 1.0045f, 1.4087f,
                    1.6606f, 1.6222f, 1.3126f, 0.8754f, 0.3325f, -0.0969f,
                    -0.0151f, 0.1969f, 0.4048f, 0.5772f, 0.7203f,
                }),
        };

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[12];
        private readonly Collider[] _overlapBuffer = new Collider[12];
        private readonly Transform _actorTransform;
        private readonly CharacterController _controller;
        private readonly float _configuredDetectionDistance;
        private readonly float _minimumHeight;
        private readonly float _maximumHeight;
        private readonly ObstacleJumpFeel_y[] _feelByTier;

        private Vector3 _vaultDirection;
        private Collider _activeObstacleCollider;
        private float _vaultDuration;
        private float _vaultElapsed;
        private float _vaultAnimationProgress;
        private float _lastMovementProgress;
        private bool _hasAnimationProgress;
        private bool _shouldCompleteVaultAfterMove;
        private Vector3 _vaultStartPosition;
        private float _vaultContactProgress;
        private float _vaultContactDistance;
        private float _vaultCrossStartProgress;
        private float _vaultCrossStartDistance;
        private float _vaultCrossEndProgress;
        private float _vaultCrossEndDistance;
        private float _vaultTravelDistance;
        private float _vaultLandingHeight;
        private float _vaultLift;
        private float _vaultEntrySpeed;
        private float _vaultExitSpeed;
        private Vector3 _vaultContactPoint;
        private int _currentJumpTier;
        private int _nextLowObstacleTier = 1;

        public int CurrentJumpTier => _currentJumpTier;
        public bool IsVaulting => _vaultDuration > 0f;
        public Collider ActiveObstacleCollider => IsVaulting ? _activeObstacleCollider : null;
        public float CameraLift => 0f;
        public float AnimationSpeed => IsVaulting ? GetFeel(_currentJumpTier).AnimationSpeed : 1f;
        public float NormalizedProgress
        {
            get
            {
                if (!IsVaulting)
                {
                    return 0f;
                }

                var fallbackProgress = _vaultElapsed / Mathf.Max(_vaultDuration, 0.001f);
                var progress = _hasAnimationProgress
                    ? Mathf.Max(_lastMovementProgress, _vaultAnimationProgress)
                    : fallbackProgress;
                return Mathf.Clamp01(progress);
            }
        }
        public Vector3 VaultDirection => _vaultDirection;
        public Vector3 VaultContactPoint => _vaultContactPoint;
        public float HandContactWeight => CalculateHandContactWeight();

        public ParkourObstacleJump_y(
            Transform actorTransform,
            CharacterController controller,
            float detectionDistance,
            float minimumHeight,
            float maximumHeight,
            ObstacleJumpFeel_y jumping1Feel,
            ObstacleJumpFeel_y jumping2Feel,
            ObstacleJumpFeel_y jumping3Feel,
            ObstacleJumpFeel_y jumpOverFeel)
        {
            _actorTransform = actorTransform;
            _controller = controller;
            _configuredDetectionDistance = detectionDistance;
            _minimumHeight = minimumHeight;
            _maximumHeight = maximumHeight;
            _feelByTier = new[]
            {
                jumping1Feel,
                jumping2Feel,
                jumping3Feel,
                jumpOverFeel,
            };
        }

        public void StartJump(
            Vector3 desiredDirection,
            float baseJumpHeight,
            float gravity,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            if (!CanDetectObstacle(desiredDirection, planarVelocity, out ObstacleData obstacle))
            {
                StopVault();
                _currentJumpTier = 0;
                verticalVelocity = Mathf.Sqrt(baseJumpHeight * -2f * gravity);
                return;
            }

            _currentJumpTier = CalculateJumpTier(obstacle.Collider, obstacle.Height);
            if (!TryStartVault(obstacle, ref planarVelocity, ref verticalVelocity))
            {
                StopVault();
                _currentJumpTier = 0;
                verticalVelocity = Mathf.Sqrt(baseJumpHeight * -2f * gravity);
            }
        }

        public void SetFeelSettings(
            ObstacleJumpFeel_y jumping1Feel,
            ObstacleJumpFeel_y jumping2Feel,
            ObstacleJumpFeel_y jumping3Feel,
            ObstacleJumpFeel_y jumpOverFeel)
        {
            _feelByTier[0] = jumping1Feel;
            _feelByTier[1] = jumping2Feel;
            _feelByTier[2] = jumping3Feel;
            _feelByTier[3] = jumpOverFeel;
        }

        public void UpdateVaultMovement(
            float deltaTime,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            if (!IsVaulting)
            {
                return;
            }

            _vaultElapsed = Mathf.Min(_vaultElapsed + deltaTime, _vaultDuration);
            var progress = NormalizedProgress;

            var traveledDistance = CalculateTraveledDistance();
            var targetDistance = CalculateTargetDistance(progress);
            var remainingDistance = targetDistance - traveledDistance;
            var targetSpeed = remainingDistance / Mathf.Max(deltaTime, 0.001f);
            var minimumSpeed = _vaultEntrySpeed
                               * GetFeel(_currentJumpTier).SpeedRetention;
            planarVelocity = _vaultDirection * Mathf.Max(targetSpeed, minimumSpeed);

            var targetHeight = CalculateTargetHeight(progress);
            verticalVelocity = (targetHeight - _actorTransform.position.y)
                               / Mathf.Max(deltaTime, 0.001f);
            _lastMovementProgress = progress;

            if (progress >= 0.995f)
            {
                _shouldCompleteVaultAfterMove = true;
            }
        }

        public void SetAnimationProgress(float normalizedTime)
        {
            if (!IsVaulting)
            {
                return;
            }

            _vaultAnimationProgress = Mathf.Clamp01(normalizedTime);
            _hasAnimationProgress = true;
        }

        public void CompleteVaultAfterMove()
        {
            if (_shouldCompleteVaultAfterMove)
            {
                StopVault();
            }
        }

        public void StopVault()
        {
            _vaultElapsed = 0f;
            _vaultDuration = 0f;
            _vaultAnimationProgress = 0f;
            _lastMovementProgress = 0f;
            _hasAnimationProgress = false;
            _shouldCompleteVaultAfterMove = false;
            _vaultEntrySpeed = 0f;
            _vaultExitSpeed = 0f;
            ClearActiveObstacle();
        }

        public void ResetState()
        {
            StopVault();
            _currentJumpTier = 0;
            _nextLowObstacleTier = 1;
        }

        private bool CanDetectObstacle(
            Vector3 desiredDirection,
            Vector3 planarVelocity,
            out ObstacleData obstacle)
        {
            obstacle = default;
            Vector3 vaultDirection = CalculateVaultDirection(desiredDirection, planarVelocity);
            var planarSpeed = Vector3.ProjectOnPlane(planarVelocity, Vector3.up).magnitude;
            var effectiveMinimumHeight = Mathf.Max(
                _minimumHeight,
                _controller != null ? _controller.stepOffset + 0.08f : _minimumHeight);
            var detectionDistance = Mathf.Min(
                _configuredDetectionDistance,
                Mathf.Max(MinimumDetectionDistance, planarSpeed * DetectionLeadTime));

            if (!TryFindFrontFace(
                    vaultDirection,
                    detectionDistance,
                    effectiveMinimumHeight,
                    out RaycastHit frontHit))
            {
                return false;
            }

            Vector3 obstacleForward = Vector3.ProjectOnPlane(-frontHit.normal, Vector3.up);
            if (obstacleForward.sqrMagnitude < 0.01f)
            {
                obstacleForward = vaultDirection;
            }
            else
            {
                obstacleForward.Normalize();
            }

            if (!TryMeasureObstacle(
                    frontHit,
                    obstacleForward,
                    effectiveMinimumHeight,
                    out Vector3 topPoint,
                    out float height,
                    out float depth))
            {
                return false;
            }

            var frontDistance = Vector3.Dot(
                Vector3.ProjectOnPlane(frontHit.point - _actorTransform.position, Vector3.up),
                obstacleForward);
            obstacle = new ObstacleData(
                frontHit.collider,
                obstacleForward,
                topPoint,
                height,
                depth,
                Mathf.Max(0.1f, frontDistance));
            return true;
        }

        private Vector3 CalculateVaultDirection(Vector3 desiredDirection, Vector3 planarVelocity)
        {
            if (desiredDirection.sqrMagnitude > 0.01f)
            {
                return desiredDirection.normalized;
            }

            Vector3 planarDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
            return planarDirection.sqrMagnitude > 0.25f
                ? planarDirection.normalized
                : _actorTransform.forward;
        }

        private bool TryFindFrontFace(
            Vector3 direction,
            float detectionDistance,
            float minimumHeight,
            out RaycastHit closestHit)
        {
            const float sphereRadius = 0.16f;
            closestHit = default;
            var closestDistance = float.MaxValue;

            foreach (float probeHeight in ProbeHeights)
            {
                if (probeHeight <= minimumHeight)
                {
                    continue;
                }

                Vector3 origin = _actorTransform.position + Vector3.up * probeHeight;
                var hitCount = Physics.SphereCastNonAlloc(
                    origin,
                    sphereRadius,
                    direction,
                    _hitBuffer,
                    detectionDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = _hitBuffer[hitIndex];
                    if (!IsValidFrontHit(hit, direction, minimumHeight))
                    {
                        continue;
                    }

                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        closestHit = hit;
                    }
                }
            }

            return closestHit.collider != null;
        }

        private bool IsValidFrontHit(RaycastHit hit, Vector3 direction, float minimumHeight)
        {
            if (hit.collider == null)
            {
                return false;
            }

            Transform hitTransform = hit.transform;
            if (hitTransform == _actorTransform || hitTransform.IsChildOf(_actorTransform))
            {
                return false;
            }

            if (Mathf.Abs(hit.normal.y) >= 0.45f || Vector3.Dot(-hit.normal, direction) < 0.55f)
            {
                return false;
            }

            var boundsHeight = hit.collider.bounds.max.y - _actorTransform.position.y;
            return boundsHeight >= minimumHeight && boundsHeight <= _maximumHeight + 0.25f;
        }

        private bool TryMeasureObstacle(
            RaycastHit frontHit,
            Vector3 obstacleForward,
            float minimumHeight,
            out Vector3 topPoint,
            out float height,
            out float depth)
        {
            topPoint = default;
            height = 0f;
            depth = 0f;

            Vector3 firstProbePoint = frontHit.point + obstacleForward * TopProbeInset;
            if (!TryGetColliderTop(frontHit.collider, firstProbePoint, out topPoint))
            {
                return false;
            }

            height = topPoint.y - _actorTransform.position.y;
            if (height < minimumHeight || height > _maximumHeight)
            {
                return false;
            }

            Bounds bounds = frontHit.collider.bounds;
            var boundsDepth = Mathf.Abs(obstacleForward.x) * bounds.size.x
                              + Mathf.Abs(obstacleForward.y) * bounds.size.y
                              + Mathf.Abs(obstacleForward.z) * bounds.size.z;
            var rearRayOffset = boundsDepth + TopProbeInset;
            var rearRay = new Ray(
                frontHit.point + obstacleForward * rearRayOffset,
                -obstacleForward);
            if (!frontHit.collider.Raycast(
                    rearRay,
                    out RaycastHit rearHit,
                    rearRayOffset + TopProbeInset))
            {
                return false;
            }

            depth = Vector3.Dot(rearHit.point - frontHit.point, obstacleForward);
            return depth >= TopProbeInset;
        }

        private bool TryGetColliderTop(Collider collider, Vector3 horizontalPoint, out Vector3 topPoint)
        {
            var rayHeight = _actorTransform.position.y + _maximumHeight + 0.35f;
            var ray = new Ray(
                new Vector3(horizontalPoint.x, rayHeight, horizontalPoint.z),
                Vector3.down);

            if (collider.Raycast(ray, out RaycastHit hit, _maximumHeight + 0.7f))
            {
                topPoint = hit.point;
                return hit.normal.y > 0.55f;
            }

            topPoint = default;
            return false;
        }

        private int CalculateJumpTier(Collider obstacleCollider, float obstacleHeight)
        {
            ParkourObstacleProfile_y profile = obstacleCollider.GetComponentInParent<ParkourObstacleProfile_y>();
            if (profile != null)
            {
                return profile.JumpTier;
            }

            if (obstacleHeight > 1f)
            {
                return 4;
            }

            var tier = _nextLowObstacleTier;
            _nextLowObstacleTier = (_nextLowObstacleTier % 3) + 1;
            return tier;
        }

        private bool TryStartVault(
            ObstacleData obstacle,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            _vaultDirection = obstacle.Direction;
            _vaultContactPoint = obstacle.TopPoint;
            _vaultStartPosition = _actorTransform.position;
            _vaultElapsed = 0f;
            _vaultAnimationProgress = 0f;
            _lastMovementProgress = 0f;
            _hasAnimationProgress = false;
            _shouldCompleteVaultAfterMove = false;
            ObstacleJumpFeel_y feel = GetFeel(_currentJumpTier);
            _vaultEntrySpeed = planarVelocity.magnitude;
            _vaultExitSpeed = _vaultEntrySpeed;

            VaultMotionProfile motionProfile = GetMotionProfile(_currentJumpTier);
            _vaultDuration = motionProfile.Duration;
            _vaultContactProgress = motionProfile.ContactProgress;
            _vaultContactDistance = Mathf.Max(
                0f,
                obstacle.FrontDistance - motionProfile.ContactSetback);

            var controllerBottomOffset = _controller.center.y - _controller.height * 0.5f;
            var exitClearance = _controller.radius + _controller.skinWidth;
            var obstacleBackDistance = obstacle.FrontDistance + obstacle.Depth;
            _vaultCrossStartDistance = Mathf.Max(
                _vaultContactDistance,
                obstacle.FrontDistance - TorsoSurfaceMargin);
            _vaultCrossEndDistance = obstacleBackDistance + TorsoSurfaceMargin;
            CalculateCrossingWindow(motionProfile, obstacle.Height);

            var baseTravelDistance = obstacleBackDistance + exitClearance + ExitRunoutDistance;
            _vaultTravelDistance = _vaultContactDistance
                                   + (baseTravelDistance - _vaultContactDistance)
                                   * feel.ForwardDistance;
            var exitTangent = _vaultExitSpeed * _vaultDuration;
            var minimumExitDistance = exitTangent
                                      * (1f - _vaultCrossEndProgress)
                                      / 3f;
            _vaultTravelDistance = Mathf.Max(
                _vaultTravelDistance,
                _vaultCrossEndDistance + minimumExitDistance);
            _vaultLift = Mathf.Max(0f, _vaultLift + feel.HeightOffset);

            if (!TryFindLandingHeight(
                    _vaultStartPosition + _vaultDirection * _vaultTravelDistance,
                    controllerBottomOffset,
                    obstacle.Collider,
                    out _vaultLandingHeight))
            {
                return false;
            }

            SetActiveObstacle(obstacle.Collider);
            planarVelocity = _vaultDirection * _vaultEntrySpeed;
            verticalVelocity = 0f;
            return true;
        }

        private bool TryFindLandingHeight(
            Vector3 landingPosition,
            float controllerBottomOffset,
            Collider obstacleCollider,
            out float landingHeight)
        {
            var rayStartHeight = Mathf.Max(_vaultContactPoint.y, _actorTransform.position.y)
                                 + _controller.height;
            var ray = new Ray(
                new Vector3(landingPosition.x, rayStartHeight, landingPosition.z),
                Vector3.down);
            var rayDistance = _controller.height + _maximumHeight;

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    rayDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)
                && hit.collider != obstacleCollider
                && hit.normal.y > 0.55f)
            {
                landingHeight = hit.point.y - controllerBottomOffset;
                return IsLandingCapsuleClear(landingPosition, landingHeight, hit.collider);
            }

            landingHeight = 0f;
            return false;
        }

        private bool IsLandingCapsuleClear(
            Vector3 landingPosition,
            float landingHeight,
            Collider landingSurface)
        {
            var radius = Mathf.Max(0.01f, _controller.radius - _controller.skinWidth);
            Vector3 rootPosition = new(
                landingPosition.x,
                landingHeight,
                landingPosition.z);
            Vector3 center = rootPosition + _actorTransform.rotation * _controller.center;
            var halfSegment = Mathf.Max(0f, _controller.height * 0.5f - radius);
            Vector3 bottom = center - Vector3.up * halfSegment;
            Vector3 top = center + Vector3.up * halfSegment;
            var overlapCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                _overlapBuffer,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (var index = 0; index < overlapCount; index++)
            {
                Collider overlap = _overlapBuffer[index];
                if (overlap == null || overlap == landingSurface)
                {
                    continue;
                }

                Transform overlapTransform = overlap.transform;
                if (overlapTransform == _actorTransform || overlapTransform.IsChildOf(_actorTransform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private float CalculateTargetDistance(float normalizedTime)
        {
            var firstSpeed = CalculateSegmentSpeed(
                0f,
                0f,
                _vaultContactProgress,
                _vaultContactDistance);
            var secondSpeed = CalculateSegmentSpeed(
                _vaultContactProgress,
                _vaultContactDistance,
                _vaultCrossStartProgress,
                _vaultCrossStartDistance);
            var thirdSpeed = CalculateSegmentSpeed(
                _vaultCrossStartProgress,
                _vaultCrossStartDistance,
                _vaultCrossEndProgress,
                _vaultCrossEndDistance);
            var fourthSpeed = CalculateSegmentSpeed(
                _vaultCrossEndProgress,
                _vaultCrossEndDistance,
                1f,
                _vaultTravelDistance);

            var entrySlope = ClampEndpointSlope(
                _vaultEntrySpeed * _vaultDuration,
                firstSpeed);
            var contactSlope = CalculateMonotoneSlope(firstSpeed, secondSpeed);
            var crossStartSlope = CalculateMonotoneSlope(secondSpeed, thirdSpeed);
            var crossEndSlope = CalculateMonotoneSlope(thirdSpeed, fourthSpeed);
            var exitSlope = ClampEndpointSlope(
                _vaultExitSpeed * _vaultDuration,
                fourthSpeed);

            if (normalizedTime <= _vaultContactProgress)
            {
                return CalculateHermiteDistance(
                    normalizedTime,
                    0f,
                    0f,
                    entrySlope,
                    _vaultContactProgress,
                    _vaultContactDistance,
                    contactSlope);
            }

            if (normalizedTime <= _vaultCrossStartProgress)
            {
                return CalculateHermiteDistance(
                    normalizedTime,
                    _vaultContactProgress,
                    _vaultContactDistance,
                    contactSlope,
                    _vaultCrossStartProgress,
                    _vaultCrossStartDistance,
                    crossStartSlope);
            }

            if (normalizedTime <= _vaultCrossEndProgress)
            {
                return CalculateHermiteDistance(
                    normalizedTime,
                    _vaultCrossStartProgress,
                    _vaultCrossStartDistance,
                    crossStartSlope,
                    _vaultCrossEndProgress,
                    _vaultCrossEndDistance,
                    crossEndSlope);
            }

            return CalculateHermiteDistance(
                normalizedTime,
                _vaultCrossEndProgress,
                _vaultCrossEndDistance,
                crossEndSlope,
                1f,
                _vaultTravelDistance,
                exitSlope);
        }

        private static float CalculateSegmentSpeed(
            float startTime,
            float startDistance,
            float endTime,
            float endDistance)
        {
            return (endDistance - startDistance)
                   / Mathf.Max(endTime - startTime, 0.001f);
        }

        private static float CalculateMonotoneSlope(float previousSpeed, float nextSpeed)
        {
            if (previousSpeed <= 0f || nextSpeed <= 0f)
            {
                return 0f;
            }

            return 2f * previousSpeed * nextSpeed / (previousSpeed + nextSpeed);
        }

        private static float ClampEndpointSlope(float desiredSlope, float segmentSpeed)
        {
            return Mathf.Clamp(desiredSlope, 0f, 3f * Mathf.Max(0f, segmentSpeed));
        }

        private static float CalculateHermiteDistance(
            float normalizedTime,
            float startTime,
            float startDistance,
            float startSlope,
            float endTime,
            float endDistance,
            float endSlope)
        {
            var duration = Mathf.Max(endTime - startTime, 0.001f);
            var progress = Mathf.Clamp01((normalizedTime - startTime) / duration);
            var progressSquared = progress * progress;
            var progressCubed = progressSquared * progress;
            var startWeight = 2f * progressCubed - 3f * progressSquared + 1f;
            var startSlopeWeight = progressCubed - 2f * progressSquared + progress;
            var endWeight = -2f * progressCubed + 3f * progressSquared;
            var endSlopeWeight = progressCubed - progressSquared;

            return startWeight * startDistance
                   + startSlopeWeight * duration * startSlope
                   + endWeight * endDistance
                   + endSlopeWeight * duration * endSlope;
        }

        private float CalculateTargetHeight(float normalizedTime)
        {
            var landingProgress = SmoothProgress(normalizedTime);
            var baseHeight = Mathf.Lerp(
                _vaultStartPosition.y,
                _vaultLandingHeight,
                landingProgress);
            if (_vaultLift <= 0f)
            {
                return baseHeight;
            }

            float liftWeight;
            if (normalizedTime < _vaultCrossStartProgress)
            {
                liftWeight = SmoothProgress(Mathf.InverseLerp(
                    _vaultContactProgress,
                    _vaultCrossStartProgress,
                    normalizedTime));
            }
            else if (normalizedTime <= _vaultCrossEndProgress)
            {
                liftWeight = 1f;
            }
            else
            {
                liftWeight = 1f - SmoothProgress(Mathf.InverseLerp(
                    _vaultCrossEndProgress,
                    1f,
                    normalizedTime));
            }

            return baseHeight + _vaultLift * liftWeight;
        }

        private void CalculateCrossingWindow(
            VaultMotionProfile motionProfile,
            float obstacleHeight)
        {
            const int sampleCount = 65;
            var requiredClearance = obstacleHeight + TorsoSurfaceMargin;
            var bestStartIndex = -1;
            var bestEndIndex = -1;
            var currentStartIndex = -1;
            var peakIndex = 0;
            var peakClearance = float.MinValue;

            for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                var normalizedTime = sampleIndex / (float)(sampleCount - 1);
                var clearance = motionProfile.CalculateTorsoClearance(normalizedTime);
                if (normalizedTime >= motionProfile.ContactProgress && clearance > peakClearance)
                {
                    peakClearance = clearance;
                    peakIndex = sampleIndex;
                }

                var isClear = normalizedTime >= motionProfile.ContactProgress
                              && clearance >= requiredClearance;
                if (isClear && currentStartIndex < 0)
                {
                    currentStartIndex = sampleIndex;
                }

                var isLastSample = sampleIndex == sampleCount - 1;
                if (currentStartIndex >= 0 && (!isClear || isLastSample))
                {
                    var currentEndIndex = isClear ? sampleIndex : sampleIndex - 1;
                    if (bestStartIndex < 0
                        || currentEndIndex - currentStartIndex > bestEndIndex - bestStartIndex)
                    {
                        bestStartIndex = currentStartIndex;
                        bestEndIndex = currentEndIndex;
                    }

                    currentStartIndex = -1;
                }
            }

            var minimumWindowSamples = Mathf.CeilToInt(MinimumCrossingWindow * (sampleCount - 1));
            int windowStartIndex;
            int windowEndIndex;
            if (bestStartIndex >= 0 && bestEndIndex - bestStartIndex >= minimumWindowSamples)
            {
                windowStartIndex = bestStartIndex;
                windowEndIndex = bestEndIndex;
            }
            else
            {
                var halfWindowSamples = Mathf.CeilToInt(minimumWindowSamples * 0.5f);
                var contactIndex = Mathf.CeilToInt(
                    motionProfile.ContactProgress * (sampleCount - 1));
                windowStartIndex = Mathf.Max(contactIndex, peakIndex - halfWindowSamples);
                windowEndIndex = Mathf.Min(
                    sampleCount - 2,
                    windowStartIndex + minimumWindowSamples);
                windowStartIndex = Mathf.Max(
                    contactIndex,
                    windowEndIndex - minimumWindowSamples);
            }

            var firstCrossingIndex = Mathf.CeilToInt(
                (motionProfile.ContactProgress + MinimumMotionInterval)
                * (sampleCount - 1));
            if (windowStartIndex < firstCrossingIndex)
            {
                windowStartIndex = firstCrossingIndex;
                windowEndIndex = Mathf.Min(
                    sampleCount - 2,
                    windowStartIndex + minimumWindowSamples);
            }

            _vaultCrossStartProgress = windowStartIndex / (float)(sampleCount - 1);
            _vaultCrossEndProgress = windowEndIndex / (float)(sampleCount - 1);

            var minimumClearance = float.MaxValue;
            for (var sampleIndex = windowStartIndex; sampleIndex <= windowEndIndex; sampleIndex++)
            {
                var normalizedTime = sampleIndex / (float)(sampleCount - 1);
                minimumClearance = Mathf.Min(
                    minimumClearance,
                    motionProfile.CalculateTorsoClearance(normalizedTime));
            }

            _vaultLift = Mathf.Max(0f, requiredClearance - minimumClearance);
        }

        private static float SmoothProgress(float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            return normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
        }

        private static VaultMotionProfile GetMotionProfile(int jumpTier)
        {
            var profileIndex = Mathf.Clamp(jumpTier, 1, MotionProfiles.Length) - 1;
            return MotionProfiles[profileIndex];
        }

        private ObstacleJumpFeel_y GetFeel(int jumpTier)
        {
            var feelIndex = Mathf.Clamp(jumpTier, 1, _feelByTier.Length) - 1;
            return _feelByTier[feelIndex];
        }

        private float CalculateTraveledDistance()
        {
            return Vector3.Dot(
                Vector3.ProjectOnPlane(_actorTransform.position - _vaultStartPosition, Vector3.up),
                _vaultDirection);
        }

        private float CalculateHandContactWeight()
        {
            if (!IsVaulting)
            {
                return 0f;
            }

            var signedDistance = Vector3.Dot(
                Vector3.ProjectOnPlane(_vaultContactPoint - _actorTransform.position, Vector3.up),
                _vaultDirection);
            if (signedDistance > HandContactFullDistance)
            {
                return Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        HandContactBlendStartDistance,
                        HandContactFullDistance,
                        signedDistance));
            }

            if (signedDistance >= HandContactReleaseStartDistance)
            {
                return 1f;
            }

            return Mathf.SmoothStep(
                1f,
                0f,
                Mathf.InverseLerp(
                    HandContactReleaseStartDistance,
                    HandContactReleaseEndDistance,
                    signedDistance));
        }

        private void SetActiveObstacle(Collider obstacleCollider)
        {
            ClearActiveObstacle();
            if (_controller != null && obstacleCollider != null)
            {
                Physics.IgnoreCollision(_controller, obstacleCollider, true);
            }

            _activeObstacleCollider = obstacleCollider;
        }

        private void ClearActiveObstacle()
        {
            if (_controller != null && _activeObstacleCollider != null)
            {
                Physics.IgnoreCollision(_controller, _activeObstacleCollider, false);
            }

            _activeObstacleCollider = null;
        }

        private readonly struct VaultMotionProfile
        {
            private readonly float[] _torsoClearanceSamples;

            public VaultMotionProfile(
                float duration,
                float contactProgress,
                float contactSetback,
                float[] torsoClearanceSamples)
            {
                Duration = duration;
                ContactProgress = contactProgress;
                ContactSetback = contactSetback;
                _torsoClearanceSamples = torsoClearanceSamples;
            }

            public float Duration { get; }
            public float ContactProgress { get; }
            public float ContactSetback { get; }

            public float CalculateTorsoClearance(float normalizedTime)
            {
                if (_torsoClearanceSamples == null || _torsoClearanceSamples.Length == 0)
                {
                    return 0f;
                }

                var samplePosition = Mathf.Clamp01(normalizedTime)
                                     * (_torsoClearanceSamples.Length - 1);
                var firstIndex = Mathf.FloorToInt(samplePosition);
                var secondIndex = Mathf.Min(
                    firstIndex + 1,
                    _torsoClearanceSamples.Length - 1);
                return Mathf.Lerp(
                    _torsoClearanceSamples[firstIndex],
                    _torsoClearanceSamples[secondIndex],
                    samplePosition - firstIndex);
            }
        }

        private readonly struct ObstacleData
        {
            public ObstacleData(
                Collider collider,
                Vector3 direction,
                Vector3 topPoint,
                float height,
                float depth,
                float frontDistance)
            {
                Collider = collider;
                Direction = direction;
                TopPoint = topPoint;
                Height = height;
                Depth = depth;
                FrontDistance = frontDistance;
            }

            public Collider Collider { get; }
            public Vector3 Direction { get; }
            public Vector3 TopPoint { get; }
            public float Height { get; }
            public float Depth { get; }
            public float FrontDistance { get; }
        }
    }
}
