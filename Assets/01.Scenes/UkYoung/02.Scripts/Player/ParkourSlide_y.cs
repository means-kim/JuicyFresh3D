using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourSlide_y
    {
        private const float DownhillSlopeAcceleration = 18.0f;
        private const float SlideSteerAngularSpeed = 65.0f;
        private const float MaxSteerAngleFromInitial = 65.0f;
        private const float StandingHeight = 1.80f;
        private const float SlidingHeight = 0.90f;
        private const float StandingCenterY = 0.90f;
        private const float SlidingCenterY = 0.45f;
        private const float CeilingCheckRadius = 0.22f;

        private float _baseSlideSpeed = 13.5f;
        private float _minSlideSpeed = 6.0f;
        private float _minimumEntrySpeed = 6.5f;
        private float _flatFrictionRate = 8.0f;
        private float _slideInflectionTime = 0.53f;
        private float _maxSlideDuration = 1.35f;
        private float _minSlideDuration = 1.05f;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[4];
        private Vector3 _slideDirection;
        private Vector3 _initialSlideDirection;
        private float _currentSpeed;
        private float _elapsedTime;
        private bool _isSliding;
        private bool _hasGroundBeneath;

        public bool IsSliding => _isSliding;
        public float CurrentSpeed => _currentSpeed;
        public Vector3 SlideDirection => _slideDirection;
        public bool HasGroundBeneath => _hasGroundBeneath;

        public void ConfigureSettings(
            float baseSlideSpeed,
            float minSlideSpeed,
            float flatFrictionRate,
            float maxSlideDuration,
            float minSlideDuration,
            float slideInflectionTime)
        {
            _baseSlideSpeed = baseSlideSpeed;
            _minSlideSpeed = minSlideSpeed;
            _flatFrictionRate = flatFrictionRate;
            _maxSlideDuration = maxSlideDuration;
            _minSlideDuration = minSlideDuration;
            _slideInflectionTime = slideInflectionTime;
        }

        public bool CanStartSlide(bool isGrounded, float planarSpeed, bool wasSlidePressed)
        {
            return isGrounded && wasSlidePressed && planarSpeed >= _minimumEntrySpeed;
        }

        public void StartSlide(Vector3 forwardDirection, float currentSpeed, CharacterController controller)
        {
            _isSliding = true;
            _elapsedTime = 0f;
            _slideDirection = forwardDirection.sqrMagnitude > 0.01f
                ? Vector3.ProjectOnPlane(forwardDirection, Vector3.up).normalized
                : Vector3.forward;
            _initialSlideDirection = _slideDirection;

            _currentSpeed = Mathf.Max(currentSpeed * 1.12f, _baseSlideSpeed);

            ApplyColliderHeight(controller, SlidingHeight, SlidingCenterY);
        }

        public bool UpdateSlide(
            float deltaTime,
            CharacterController controller,
            Vector3 steerDirection,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            if (!_isSliding)
            {
                return false;
            }

            _elapsedTime += deltaTime;

            Vector3 groundNormal = Vector3.up;
            float groundDrop = 0f;
            var isOnSlope = DetectSlope(controller, out groundNormal, out groundDrop);
            _hasGroundBeneath = isOnSlope;

            if (steerDirection.sqrMagnitude > 0.01f)
            {
                Vector3 desiredSteer = Vector3.ProjectOnPlane(steerDirection, groundNormal).normalized;
                if (desiredSteer.sqrMagnitude > 0.01f)
                {
                    _slideDirection = Vector3.RotateTowards(
                        _slideDirection,
                        desiredSteer,
                        SlideSteerAngularSpeed * Mathf.Deg2Rad * deltaTime,
                        0f);

                    if (Vector3.Angle(_initialSlideDirection, _slideDirection) > MaxSteerAngleFromInitial)
                    {
                        _slideDirection = Vector3.RotateTowards(
                            _initialSlideDirection,
                            _slideDirection,
                            MaxSteerAngleFromInitial * Mathf.Deg2Rad,
                            0f);
                    }
                }
            }

            var downhillAlignment = Vector3.Dot(_slideDirection, Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized);
            var isDownhill = isOnSlope && Vector3.Angle(groundNormal, Vector3.up) > 6f && downhillAlignment > 0.1f;

            if (isDownhill)
            {
                var slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
                var slopeFactor = Mathf.Clamp01(slopeAngle / 35f);
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, _baseSlideSpeed + 3.0f, DownhillSlopeAcceleration * slopeFactor * deltaTime);
            }
            else
            {
                if (_elapsedTime > _slideInflectionTime)
                {
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, _minSlideSpeed, _flatFrictionRate * deltaTime);
                }
            }

            Vector3 projectedDirection = Vector3.ProjectOnPlane(_slideDirection, groundNormal).normalized;
            planarVelocity = projectedDirection * _currentSpeed;

            if (_hasGroundBeneath)
            {
                if (groundDrop > 0.02f && !controller.isGrounded)
                {
                    float snapSpeed = Mathf.Max(groundDrop / Mathf.Max(deltaTime, 0.001f), 12.0f);
                    verticalVelocity = -snapSpeed;
                }
                else
                {
                    verticalVelocity = isDownhill ? -8.0f : -2.5f;
                }
            }
            else if (controller.isGrounded)
            {
                verticalVelocity = -2.0f;
            }

            var reachedMinSpeed = _currentSpeed <= _minSlideSpeed + 0.1f;
            var durationExpired = _elapsedTime >= _maxSlideDuration;

            if (_elapsedTime >= _minSlideDuration && (reachedMinSpeed || durationExpired))
            {
                if (CanStandUp(controller))
                {
                    planarVelocity = projectedDirection * Mathf.Max(_currentSpeed, _minimumEntrySpeed);
                    StopSlide(controller);
                    return false;
                }
            }

            return true;
        }

        public bool CanStandUp(CharacterController controller)
        {
            Vector3 origin = controller.transform.position + Vector3.up * SlidingHeight;
            var castDistance = StandingHeight - SlidingHeight;

            var hitCount = Physics.SphereCastNonAlloc(
                origin,
                CeilingCheckRadius,
                Vector3.up,
                _hitBuffer,
                castDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < hitCount; i++)
            {
                Transform hitTransform = _hitBuffer[i].transform;
                if (hitTransform == controller.transform || hitTransform.IsChildOf(controller.transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        public void ApplySlideJump(
            CharacterController controller,
            float jumpHeight,
            float gravity,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            var exitSpeed = Mathf.Max(_currentSpeed, 10.5f);
            planarVelocity = _slideDirection * exitSpeed;
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            StopSlide(controller);
        }

        public void StopSlide(CharacterController controller)
        {
            if (!_isSliding)
            {
                return;
            }

            _isSliding = false;
            _hasGroundBeneath = false;
            _elapsedTime = 0f;
            _currentSpeed = 0f;

            ApplyColliderHeight(controller, StandingHeight, StandingCenterY);
        }

        public void ResetState(CharacterController controller)
        {
            _isSliding = false;
            _hasGroundBeneath = false;
            _elapsedTime = 0f;
            _currentSpeed = 0f;
            _slideDirection = Vector3.forward;
            _initialSlideDirection = Vector3.forward;

            if (controller != null)
            {
                ApplyColliderHeight(controller, StandingHeight, StandingCenterY);
            }
        }

        private static void ApplyColliderHeight(CharacterController controller, float height, float centerY)
        {
            if (controller == null)
            {
                return;
            }

            controller.height = height;
            controller.center = new Vector3(0f, centerY, 0f);
        }

        private bool DetectSlope(
            CharacterController controller,
            out Vector3 groundNormal,
            out float groundDrop)
        {
            groundNormal = Vector3.up;
            groundDrop = 0f;
            if (controller == null)
            {
                return false;
            }

            Vector3 origin = controller.transform.position + Vector3.up * 0.4f;

            var hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _hitBuffer,
                1.2f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.MaxValue;
            Vector3 bestNormal = Vector3.up;
            bool foundGround = false;

            for (var i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _hitBuffer[i].collider;
                if (hitCollider == null || hitCollider == controller || hitCollider.transform.IsChildOf(controller.transform))
                {
                    continue;
                }

                float distance = _hitBuffer[i].distance;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestNormal = _hitBuffer[i].normal;
                    foundGround = true;
                }
            }

            if (!foundGround)
            {
                return false;
            }

            float drop = closestDistance - 0.4f;
            if (drop > 0.55f)
            {
                return false;
            }

            groundNormal = bestNormal;
            groundDrop = Mathf.Max(0f, drop);
            return true;
        }
    }
}
