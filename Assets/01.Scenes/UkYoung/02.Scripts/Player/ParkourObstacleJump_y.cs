using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourObstacleJump_y
    {
        private const float HighObstacleThreshold = 0.95f;
        private const float MaximumVaultableDepth = 2.6f;
        private static readonly float[] ProbeHeights = { 0.30f, 0.60f, 0.95f, 1.30f };

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[12];
        private readonly Transform _actorTransform;
        private readonly CharacterController _controller;
        private readonly float _detectionDistance;
        private readonly float _minimumHeight;
        private readonly float _maximumHeight;

        private Vector3 _vaultDirection;
        private Collider _ignoredObstacleCollider;
        private float _vaultTimer;
        private float _vaultApexTimer;
        private float _vaultApproachSpeed;
        private float _vaultRolloutSpeed;
        private int _currentJumpTier;
        private int _nextLowObstacleTier = 1;

        public int CurrentJumpTier => _currentJumpTier;
        public bool IsVaulting => _vaultTimer > 0f;

        public ParkourObstacleJump_y(
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

        public void StartJump(
            Vector3 desiredDirection,
            float baseJumpHeight,
            float gravity,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            if (CanDetectObstacle(
                    desiredDirection,
                    planarVelocity,
                    out var obstacleHeight,
                    out Collider obstacleCollider,
                    out var distanceToCenter,
                    out var obstacleDepth,
                    out Vector3 vaultDirection))
            {
                _currentJumpTier = CalculateJumpTier(obstacleHeight);
                if (obstacleDepth <= MaximumVaultableDepth)
                {
                    StartVault(
                        _currentJumpTier,
                        obstacleCollider,
                        distanceToCenter,
                        vaultDirection,
                        ref planarVelocity,
                        ref verticalVelocity);
                    return;
                }

                StopVault();
                var requiredHeight = Mathf.Max(baseJumpHeight, obstacleHeight + 0.35f);
                verticalVelocity = Mathf.Sqrt(requiredHeight * -2f * gravity);
                return;
            }

            StopVault();
            _currentJumpTier = 0;
            verticalVelocity = Mathf.Sqrt(baseJumpHeight * -2f * gravity);
        }

        public void UpdateVaultMovement(
            float deltaTime,
            bool hasMovementInput,
            Vector3 desiredDirection,
            float targetGroundSpeed,
            float groundAcceleration,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            if (_vaultTimer <= 0f)
            {
                return;
            }

            _vaultTimer -= deltaTime;
            _vaultApexTimer -= deltaTime;

            if (_vaultApexTimer > 0f)
            {
                planarVelocity = _vaultDirection * _vaultApproachSpeed;
            }
            else
            {
                var targetExitSpeed = hasMovementInput
                    ? Mathf.Max(_vaultRolloutSpeed, targetGroundSpeed)
                    : _vaultRolloutSpeed;
                Vector3 exitDirection = desiredDirection.sqrMagnitude > 0.01f
                    ? desiredDirection
                    : _vaultDirection;
                planarVelocity = Vector3.MoveTowards(
                    planarVelocity,
                    exitDirection * targetExitSpeed,
                    groundAcceleration * deltaTime);
            }

            if (_controller != null && _controller.isGrounded)
            {
                verticalVelocity = -2f;
            }

            if (_vaultTimer <= 0f)
            {
                StopVault();
            }
        }

        public void StopVault()
        {
            _vaultTimer = 0f;
            _vaultApexTimer = 0f;
            DisableObstacleCollisionIgnore();
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
            out float obstacleHeight,
            out Collider obstacleCollider,
            out float distanceToCenter,
            out float obstacleDepth,
            out Vector3 vaultDirection)
        {
            obstacleHeight = 0f;
            obstacleCollider = null;
            distanceToCenter = 0f;
            obstacleDepth = 0f;

            if (desiredDirection.sqrMagnitude > 0.01f)
            {
                vaultDirection = desiredDirection.normalized;
            }
            else if (planarVelocity.sqrMagnitude > 0.25f)
            {
                vaultDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up).normalized;
            }
            else
            {
                vaultDirection = _actorTransform.forward;
            }

            const float sphereRadius = 0.2f;
            const float backOffset = 0.15f;
            var castDistance = _detectionDistance + backOffset;
            var closestHitDistance = float.MaxValue;
            Collider bestCollider = null;
            Vector3 bestHitNormal = -vaultDirection;

            for (var probeIndex = 0; probeIndex < ProbeHeights.Length; probeIndex++)
            {
                Vector3 origin = _actorTransform.position
                                 - vaultDirection * backOffset
                                 + Vector3.up * ProbeHeights[probeIndex];
                var hitCount = Physics.SphereCastNonAlloc(
                    origin,
                    sphereRadius,
                    vaultDirection,
                    _hitBuffer,
                    castDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = _hitBuffer[hitIndex];
                    if (hit.collider == null)
                    {
                        continue;
                    }

                    Transform hitTransform = hit.transform;
                    if (hitTransform == _actorTransform || hitTransform.IsChildOf(_actorTransform))
                    {
                        continue;
                    }

                    if (Mathf.Abs(hit.normal.y) >= 0.45f)
                    {
                        continue;
                    }

                    var candidateHeight = hit.collider.bounds.max.y - _actorTransform.position.y;
                    if (candidateHeight < _minimumHeight || candidateHeight > _maximumHeight)
                    {
                        continue;
                    }

                    var frontDistance = Mathf.Max(0.1f, hit.distance - backOffset);
                    if (frontDistance < closestHitDistance)
                    {
                        closestHitDistance = frontDistance;
                        bestCollider = hit.collider;
                        bestHitNormal = hit.normal;
                    }
                }
            }

            if (bestCollider == null)
            {
                return false;
            }

            Bounds bounds = bestCollider.bounds;
            obstacleHeight = bounds.max.y - _actorTransform.position.y;
            obstacleCollider = bestCollider;

            Vector3 wallForward = Vector3.ProjectOnPlane(-bestHitNormal, Vector3.up);
            if (wallForward.sqrMagnitude < 0.01f)
            {
                wallForward = vaultDirection;
            }
            else
            {
                wallForward.Normalize();
            }

            obstacleDepth = Mathf.Abs(wallForward.x) * bounds.size.x
                            + Mathf.Abs(wallForward.z) * bounds.size.z;
            var alignment = Mathf.Max(Vector3.Dot(vaultDirection, wallForward), 0.5f);
            distanceToCenter = closestHitDistance + (obstacleDepth * 0.5f) / alignment;
            return true;
        }

        private int CalculateJumpTier(float obstacleHeight)
        {
            if (obstacleHeight > HighObstacleThreshold)
            {
                return 4;
            }

            var tier = _nextLowObstacleTier;
            _nextLowObstacleTier = (_nextLowObstacleTier % 3) + 1;
            return tier;
        }

        private void StartVault(
            int jumpTier,
            Collider obstacleCollider,
            float distanceToCenter,
            Vector3 vaultDirection,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            EnableObstacleCollisionIgnore(obstacleCollider);
            _vaultDirection = vaultDirection;

            switch (jumpTier)
            {
                case 1:
                    _vaultTimer = 0.84f;
                    _vaultApexTimer = 0.34f;
                    _vaultRolloutSpeed = 6.5f;
                    break;
                case 2:
                    _vaultTimer = 0.90f;
                    _vaultApexTimer = 0.38f;
                    _vaultRolloutSpeed = 6.5f;
                    break;
                case 3:
                    _vaultTimer = 1.22f;
                    _vaultApexTimer = 0.37f;
                    _vaultRolloutSpeed = 5.8f;
                    break;
                default:
                    _vaultTimer = 1.25f;
                    _vaultApexTimer = 0.43f;
                    _vaultRolloutSpeed = 6.0f;
                    break;
            }

            _vaultApproachSpeed = Mathf.Clamp(
                distanceToCenter / Mathf.Max(_vaultApexTimer, 0.1f),
                4.8f,
                12.5f);
            planarVelocity = _vaultDirection * _vaultApproachSpeed;
            verticalVelocity = -2f;
        }

        private void EnableObstacleCollisionIgnore(Collider obstacleCollider)
        {
            DisableObstacleCollisionIgnore();
            if (_controller != null && obstacleCollider != null)
            {
                Physics.IgnoreCollision(_controller, obstacleCollider, true);
                _ignoredObstacleCollider = obstacleCollider;
            }
        }

        private void DisableObstacleCollisionIgnore()
        {
            if (_controller != null && _ignoredObstacleCollider != null)
            {
                Physics.IgnoreCollision(_controller, _ignoredObstacleCollider, false);
            }

            _ignoredObstacleCollider = null;
        }
    }
}
