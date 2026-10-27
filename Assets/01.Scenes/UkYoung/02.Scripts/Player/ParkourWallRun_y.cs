using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourWallRun_y
    {
        private const float ProbeHeight = 1.0f;
        private const float ProbeRadius = 0.22f;
        private const float ProbeBackOffset = 0.15f;
        private const float MaximumWallNormalY = 0.25f;
        private const float MinimumTangentAlignment = 0.18f;
        private const float MaximumAwayAlignment = 0.28f;
        private const float MinimumEntryPlanarSpeed = 3.5f;
        private const float DesiredWallDistance = 0.44f;
        private const float WallAdhesionSpeed = 7.5f;
        private const float WallSpeedAdjustmentRate = 6.0f;
        private const float SameWallCooldownDuration = 0.45f;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
        private readonly Transform _actorTransform;
        private readonly float _detectionDistance;
        private readonly float _maxDuration;
        private readonly float _wallRunSpeed;
        private readonly float _wallJumpHeight;
        private readonly float _wallJumpHorizontalSpeed;
        private readonly float _wallJumpNormalWeight;

        private Vector3 _wallNormal;
        private Vector3 _wallTangent;
        private Collider _lastWallCollider;
        private float _currentWallSpeed;
        private float _elapsedTimer;
        private float _sameWallCooldownTimer;
        private int _wallSide;
        private int _lastWallSide;
        private bool _isWallRunning;

        public bool IsWallRunning => _isWallRunning;
        public int WallSide => _isWallRunning ? _wallSide : 0;
        public Vector3 WallNormal => _wallNormal;
        public Vector3 WallTangent => _wallTangent;
        public Collider ActiveWallCollider => _isWallRunning ? _lastWallCollider : null;

        public ParkourWallRun_y(
            Transform actorTransform,
            float detectionDistance,
            float maxDuration,
            float wallRunSpeed,
            float wallJumpHeight,
            float wallJumpHorizontalSpeed,
            float wallJumpNormalWeight)
        {
            _actorTransform = actorTransform;
            _detectionDistance = detectionDistance;
            _maxDuration = maxDuration;
            _wallRunSpeed = wallRunSpeed;
            _wallJumpHeight = wallJumpHeight;
            _wallJumpHorizontalSpeed = wallJumpHorizontalSpeed;
            _wallJumpNormalWeight = wallJumpNormalWeight;
        }

        public void UpdateTimers(float deltaTime, bool isGrounded)
        {
            if (_sameWallCooldownTimer > 0f)
            {
                _sameWallCooldownTimer -= deltaTime;
            }

            if (isGrounded)
            {
                _sameWallCooldownTimer = 0f;
                _lastWallCollider = null;
                _lastWallSide = 0;
            }
        }

        public bool CanDetectWall(
            Vector3 planarVelocity,
            Vector3 desiredDirection,
            out Vector3 detectedWallNormal,
            out Vector3 detectedWallTangent,
            out int detectedWallSide,
            out Collider detectedCollider)
        {
            detectedWallNormal = Vector3.zero;
            detectedWallTangent = Vector3.zero;
            detectedWallSide = 0;
            detectedCollider = null;

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
            if (horizontalVelocity.magnitude < MinimumEntryPlanarSpeed)
            {
                return false;
            }

            Vector3 referenceForward = CalculateReferenceForward(horizontalVelocity, desiredDirection);
            Vector3 rightDirection = Vector3.Cross(Vector3.up, referenceForward).normalized;
            Vector3 baseOrigin = _actorTransform.position + Vector3.up * ProbeHeight;

            Vector3[] probeDirections =
            {
                (referenceForward * 0.866f + rightDirection * 0.5f).normalized,
                (referenceForward * 0.866f - rightDirection * 0.5f).normalized,
                (referenceForward * 0.5f + rightDirection * 0.866f).normalized,
                (referenceForward * 0.5f - rightDirection * 0.866f).normalized,
                rightDirection,
                -rightDirection
            };

            var closestDistance = float.MaxValue;
            var foundWall = false;

            for (var dirIndex = 0; dirIndex < probeDirections.Length; dirIndex++)
            {
                Vector3 probeDirection = probeDirections[dirIndex];
                Vector3 castOrigin = baseOrigin - probeDirection * ProbeBackOffset;
                var hitCount = Physics.SphereCastNonAlloc(
                    castOrigin,
                    ProbeRadius,
                    probeDirection,
                    _hitBuffer,
                    _detectionDistance + ProbeBackOffset,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = _hitBuffer[hitIndex];
                    if (!IsValidWallHit(hit))
                    {
                        continue;
                    }

                    Vector3 wallNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    if (wallNormal.sqrMagnitude < 0.5f)
                    {
                        continue;
                    }

                    Vector3 wallTangent = CalculateWallTangent(wallNormal, referenceForward);
                    var tangentAlignment = Vector3.Dot(referenceForward, wallTangent);
                    var normalAlignment = Vector3.Dot(referenceForward, wallNormal);
                    if (tangentAlignment < MinimumTangentAlignment || normalAlignment > MaximumAwayAlignment)
                    {
                        continue;
                    }

                    var candidateWallSide = CalculateWallSide(wallNormal, wallTangent);
                    if (IsSameWallOnCooldown(hit.collider, candidateWallSide))
                    {
                        continue;
                    }

                    var actualDistance = Mathf.Max(0f, hit.distance - ProbeBackOffset);
                    if (actualDistance < closestDistance)
                    {
                        closestDistance = actualDistance;
                        detectedWallNormal = wallNormal;
                        detectedWallTangent = wallTangent;
                        detectedWallSide = candidateWallSide;
                        detectedCollider = hit.collider;
                        foundWall = true;
                    }
                }
            }

            return foundWall;
        }

        public void StartWallRun(
            Vector3 wallNormal,
            Vector3 wallTangent,
            int wallSide,
            Collider wallCollider,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            _isWallRunning = true;
            _wallNormal = wallNormal;
            _wallTangent = wallTangent;
            _wallSide = wallSide;
            _lastWallCollider = wallCollider;
            _lastWallSide = wallSide;
            _elapsedTimer = 0f;

            var projectedSpeed = Vector3.Dot(planarVelocity, wallTangent);
            var baseEntrySpeed = Mathf.Max(projectedSpeed, planarVelocity.magnitude * 0.8f);
            _currentWallSpeed = Mathf.Clamp(baseEntrySpeed, 6.5f, Mathf.Max(_wallRunSpeed, 8.8f));

            planarVelocity = _wallTangent * _currentWallSpeed;
            verticalVelocity = 0f;
        }

        public bool UpdateWallRun(
            float deltaTime,
            float gravity,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            if (!_isWallRunning)
            {
                return false;
            }

            _elapsedTimer += deltaTime;
            if (_elapsedTimer >= _maxDuration)
            {
                return false;
            }

            if (!CanTrackActiveWall(out Vector3 updatedNormal, out var wallDistance, out Collider updatedCollider))
            {
                return false;
            }

            _wallNormal = Vector3.Slerp(_wallNormal, updatedNormal, 1f - Mathf.Exp(-20f * deltaTime)).normalized;
            _wallTangent = CalculateWallTangent(_wallNormal, _wallTangent);
            _lastWallCollider = updatedCollider;

            _currentWallSpeed = Mathf.MoveTowards(_currentWallSpeed, _wallRunSpeed, WallSpeedAdjustmentRate * deltaTime);
            var distanceError = wallDistance - DesiredWallDistance;
            Vector3 adhesionVelocity = -_wallNormal * Mathf.Clamp(distanceError * WallAdhesionSpeed, -2.5f, 3.0f);
            planarVelocity = _wallTangent * _currentWallSpeed + adhesionVelocity;

            // Maintain stable level height along the wall without downward sinking
            verticalVelocity = Mathf.MoveTowards(verticalVelocity, 0f, 15f * deltaTime);

            return true;
        }

        public void StopWallRun()
        {
            if (!_isWallRunning)
            {
                return;
            }

            _isWallRunning = false;
            _sameWallCooldownTimer = SameWallCooldownDuration;
        }

        public void ApplyWallJumpImpulse(
            Vector3 desiredDirection,
            float gravity,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            var awayRatio = 0f;
            var forwardRatio = 0f;
            if (desiredDirection.sqrMagnitude > 0.01f)
            {
                Vector3 inputDir = desiredDirection.normalized;
                awayRatio = Mathf.Clamp01(Vector3.Dot(inputDir, _wallNormal));
                forwardRatio = Mathf.Clamp01(Vector3.Dot(inputDir, _wallTangent));
            }

            var normalWeight = Mathf.Lerp(_wallJumpNormalWeight, 1.45f, awayRatio);
            var tangentWeight = Mathf.Lerp(1.0f, 0.40f, awayRatio);
            if (forwardRatio > 0.3f && awayRatio < 0.3f)
            {
                var forwardIntensity = (forwardRatio - 0.3f) / 0.7f;
                tangentWeight = Mathf.Lerp(1.0f, 1.20f, forwardIntensity);
                normalWeight = Mathf.Lerp(normalWeight, 0.55f, forwardIntensity);
            }

            Vector3 jumpDirection = (_wallTangent * tangentWeight + _wallNormal * normalWeight).normalized;
            if (desiredDirection.sqrMagnitude > 0.01f && awayRatio > 0.1f)
            {
                jumpDirection = Vector3.Slerp(jumpDirection, desiredDirection.normalized, 0.35f * awayRatio).normalized;
            }

            var speedBoost = Mathf.Lerp(0f, 3.2f, awayRatio);
            if (forwardRatio > 0.5f && awayRatio < 0.2f)
            {
                speedBoost = Mathf.Lerp(0f, 0.8f, (forwardRatio - 0.5f) / 0.5f);
            }

            var baseSpeed = Mathf.Clamp(_currentWallSpeed, 7.2f, _wallJumpHorizontalSpeed);
            var horizontalSpeed = baseSpeed + speedBoost;
            planarVelocity = jumpDirection * horizontalSpeed;

            var jumpHeight = _wallJumpHeight * Mathf.Lerp(1.0f, 1.25f, awayRatio);
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            StopWallRun();
        }

        public void ResetState()
        {
            _isWallRunning = false;
            _wallSide = 0;
            _lastWallSide = 0;
            _lastWallCollider = null;
            _currentWallSpeed = 0f;
            _elapsedTimer = 0f;
            _sameWallCooldownTimer = 0f;
        }

        private bool CanTrackActiveWall(
            out Vector3 updatedNormal,
            out float wallDistance,
            out Collider updatedCollider)
        {
            updatedNormal = _wallNormal;
            wallDistance = DesiredWallDistance;
            updatedCollider = _lastWallCollider;

            Vector3 baseOrigin = _actorTransform.position + Vector3.up * ProbeHeight;
            Vector3 intoWallDirection = -_wallNormal;
            Vector3 forwardIntoWallDirection = (_wallTangent * 0.45f - _wallNormal).normalized;

            Vector3[] trackDirections = { intoWallDirection, forwardIntoWallDirection };
            var closestDistance = float.MaxValue;
            var foundWall = false;

            for (var dirIndex = 0; dirIndex < trackDirections.Length; dirIndex++)
            {
                Vector3 probeDirection = trackDirections[dirIndex];
                Vector3 castOrigin = baseOrigin - probeDirection * ProbeBackOffset;
                var hitCount = Physics.SphereCastNonAlloc(
                    castOrigin,
                    ProbeRadius,
                    probeDirection,
                    _hitBuffer,
                    _detectionDistance + 0.35f + ProbeBackOffset,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = _hitBuffer[hitIndex];
                    if (!IsValidWallHit(hit))
                    {
                        continue;
                    }

                    Vector3 candidateNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    if (candidateNormal.sqrMagnitude < 0.5f || Vector3.Dot(candidateNormal, _wallNormal) < 0.45f)
                    {
                        continue;
                    }

                    var actualDistance = Mathf.Max(0f, hit.distance - ProbeBackOffset);
                    if (actualDistance < closestDistance)
                    {
                        closestDistance = actualDistance;
                        updatedNormal = candidateNormal;
                        wallDistance = actualDistance;
                        updatedCollider = hit.collider;
                        foundWall = true;
                    }
                }
            }

            return foundWall;
        }

        private Vector3 CalculateReferenceForward(Vector3 horizontalVelocity, Vector3 desiredDirection)
        {
            if (horizontalVelocity.sqrMagnitude > 0.25f)
            {
                return horizontalVelocity.normalized;
            }

            if (desiredDirection.sqrMagnitude > 0.01f)
            {
                return desiredDirection.normalized;
            }

            return Vector3.ProjectOnPlane(_actorTransform.forward, Vector3.up).normalized;
        }

        private static Vector3 CalculateWallTangent(Vector3 wallNormal, Vector3 referenceForward)
        {
            Vector3 tangent = Vector3.Cross(wallNormal, Vector3.up).normalized;
            if (Vector3.Dot(tangent, referenceForward) < 0f)
            {
                tangent = -tangent;
            }

            return tangent;
        }

        private static int CalculateWallSide(Vector3 wallNormal, Vector3 wallTangent)
        {
            Vector3 rightOfTangent = Vector3.Cross(Vector3.up, wallTangent);
            return Vector3.Dot(rightOfTangent, wallNormal) < 0f ? 1 : -1;
        }

        private bool IsSameWallOnCooldown(Collider candidateCollider, int candidateSide)
        {
            if (_sameWallCooldownTimer <= 0f)
            {
                return false;
            }

            return candidateCollider == _lastWallCollider || candidateSide == _lastWallSide;
        }

        private bool IsValidWallHit(RaycastHit hit)
        {
            if (hit.collider == null || hit.distance <= 0.001f)
            {
                return false;
            }

            if (IsSelf(hit.transform))
            {
                return false;
            }

            if (Mathf.Abs(hit.normal.y) > MaximumWallNormalY)
            {
                return false;
            }

            if (hit.collider is BoxCollider boxCollider)
            {
                Transform boxTransform = boxCollider.transform;
                var worldSizeX = boxCollider.size.x * Mathf.Abs(boxTransform.lossyScale.x);
                var worldSizeZ = boxCollider.size.z * Mathf.Abs(boxTransform.lossyScale.z);
                var dotRight = Mathf.Abs(Vector3.Dot(hit.normal, boxTransform.right));
                var dotForward = Mathf.Abs(Vector3.Dot(hit.normal, boxTransform.forward));

                if (dotForward > dotRight && worldSizeX < 1.0f)
                {
                    return false;
                }

                if (dotRight > dotForward && worldSizeZ < 1.0f)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSelf(Transform candidate)
        {
            return candidate == _actorTransform || candidate.IsChildOf(_actorTransform);
        }
    }
}
