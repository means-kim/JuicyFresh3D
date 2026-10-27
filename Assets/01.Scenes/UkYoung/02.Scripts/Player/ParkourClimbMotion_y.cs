using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourClimbMotion_y
    {
        private enum MotionMode
        {
            None,
            SprintWallClimb,
            JumpingToHanging,
            HangingIdle,
            HangHop,
            BracedHangToCrouch,
            CrouchedToSprint,
            JumpFromWall
        }

        private static readonly float[] SprintWallClimbForward =
            { 0f, 0.766f, 1.533f, 2.053f, 2.329f, 2.483f, 2.636f, 2.759f, 2.881f, 3.004f, 3.065f };
        private static readonly float[] SprintWallClimbVertical =
            { 0f, -0.113f, 0.113f, 0.546f, 0.979f, 1.336f, 1.581f, 1.788f, 1.901f, 1.939f, 1.882f };
        private static readonly float[] CrouchedToSprintForward =
            { 0f, 0.025f, 0.063f, 0.126f, 0.227f, 0.353f, 0.492f, 0.656f, 0.845f, 1.046f, 1.261f };
        private static readonly float[] CrouchedToSprintVisualVertical =
            { 0f, -0.012f, -0.007f, 0.034f, 0.093f, 0.167f, 0.232f, 0.270f, 0.311f, 0.334f, 0.347f };
        private static readonly float[] JumpingToHangingHandHeight =
            { 1.865f, 1.771f, 1.809f, 1.863f, 1.911f, 1.908f, 1.943f, 1.978f, 2.048f, 2.118f, 2.101f };
        private static readonly float[] HangHopLeftLateral =
            { 0f, -0.080f, -0.257f, -0.464f, -0.699f, -0.999f, -1.192f, -1.331f, -1.379f, -1.379f, -1.379f };
        private static readonly float[] HangHopLeftVertical =
            { 0f, 0.068f, 0.242f, 0.439f, 0.491f, 0.359f, 0.122f, -0.031f, -0.046f, -0.020f, 0f };
        private static readonly float[] HangHopRightLateral =
            { 0f, 0.090f, 0.314f, 0.718f, 1.008f, 1.122f, 1.244f, 1.377f, 1.417f, 1.417f, 1.417f };
        private static readonly float[] HangHopRightVertical =
            { 0f, 0.159f, 0.463f, 0.592f, 0.428f, 0.172f, 0.056f, 0.077f, 0.079f, 0.026f, 0f };
        private static readonly float[] BracedHangToCrouchForward =
            { 0f, -0.044f, 0.063f, 0.190f, 0.216f, 0.249f, 0.303f, 0.413f, 0.531f, 0.616f, 0.656f };
        private static readonly float[] BracedHangToCrouchVertical =
            { 0f, 0.118f, 0.412f, 0.700f, 0.912f, 1.049f, 1.177f, 1.276f, 1.333f, 1.343f, 1.305f };
        private static readonly float[] JumpFromWallForward =
            { 0f, -0.028f, -0.076f, -0.142f, -0.183f, -0.284f, -0.545f, -0.813f, -1.106f, -1.404f, -1.758f };
        private static readonly float[] JumpFromWallVertical =
            { 0f, 0.074f, 0.206f, 0.391f, 0.551f, 0.704f, 0.805f, 0.807f, 0.641f, 0.316f, -0.166f };
        private const float RightHangHopPoseHeightOffset = 0.134f;
        private const float LedgeActionAnimationSpeed = 1.5f;
        private const float JumpFromWallAnimationSpeed = 1.5f;
        private const float JumpingToHangingBlendStart = 0.82f;
        private const float HangingPoseBlendDuration = 0.16f;

        private readonly Transform _actorTransform;
        private readonly CharacterController _controller;

        private MotionMode _mode;
        private Collider _activeWallCollider;
        private ParkourLedgeTarget_y _ledgeTarget;
        private Vector3 _startPosition;
        private Vector3 _endPosition;
        private Vector3 _direction;
        private Vector3 _tangent;
        private float[] _forwardSamples;
        private float[] _lateralSamples;
        private float[] _verticalSamples;
        private float _naturalForwardDistance;
        private float _naturalLateralDistance;
        private float _naturalVerticalDistance;
        private float _duration;
        private float _elapsed;
        private float _animationProgress;
        private float _lastProgress;
        private bool _hasAnimationProgress;
        private bool _shouldCompleteAfterMove;
        private bool _isComplete;
        private Vector3 _leftContactStartPoint;
        private Vector3 _rightContactStartPoint;
        private Vector3 _leftContactEndPoint;
        private Vector3 _rightContactEndPoint;
        private float _hangHopPoseHeightOffset;

        public ParkourClimbMotion_y(Transform actorTransform, CharacterController controller)
        {
            _actorTransform = actorTransform;
            _controller = controller;
        }

        public bool IsActive => _mode != MotionMode.None;
        public bool IsHanging => _mode == MotionMode.HangingIdle;
        public bool IsHangPoseMatched => IsHanging && _elapsed >= _duration;
        public bool IsComplete => _isComplete;
        public float NormalizedProgress => _lastProgress;
        public Vector3 Direction => _direction;
        public float VisualRootHeightOffset => _mode == MotionMode.CrouchedToSprint
            ? Sample(CrouchedToSprintVisualVertical, _lastProgress)
            : 0f;
        public float AnimationSpeed => _mode switch
        {
            MotionMode.JumpingToHanging => LedgeActionAnimationSpeed,
            MotionMode.HangHop => LedgeActionAnimationSpeed,
            MotionMode.BracedHangToCrouch => LedgeActionAnimationSpeed,
            MotionMode.CrouchedToSprint => LedgeActionAnimationSpeed,
            MotionMode.JumpFromWall => JumpFromWallAnimationSpeed,
            _ => 1f
        };
        public ParkourLedgeTarget_y LedgeTarget => _ledgeTarget;
        public Vector3 LeftContactPoint => _mode == MotionMode.HangHop && _lastProgress < 0.5f
            ? _leftContactStartPoint
            : _leftContactEndPoint;
        public Vector3 RightContactPoint => _mode == MotionMode.HangHop && _lastProgress < 0.5f
            ? _rightContactStartPoint
            : _rightContactEndPoint;

        public void StartSprintWallClimb(ParkourLedgeTarget_y target)
        {
            SetLedgeTarget(target);
            AlignActorToDirection();
            StartMotion(MotionMode.SprintWallClimb, target.LandingRootPosition, 1.783f,
                SprintWallClimbForward, null, SprintWallClimbVertical, 3.065f, 0f, 1.882f);
        }

        public void StartJumpingToHanging(ParkourLedgeTarget_y target)
        {
            SetLedgeTarget(target);
            AlignActorToDirection();
            StartMotion(MotionMode.JumpingToHanging, target.HangRootPosition,
                1.5f / LedgeActionAnimationSpeed,
                null, null, null, 0f, 0f, 0f);
        }

        public void HoldHanging()
        {
            AlignActorToDirection();
            _mode = MotionMode.HangingIdle;
            _startPosition = _actorTransform.position;
            _endPosition = _ledgeTarget.HangRootPosition;
            _duration = HangingPoseBlendDuration;
            _elapsed = 0f;
            _isComplete = false;
            _shouldCompleteAfterMove = false;
            _hasAnimationProgress = false;
        }

        public void StartHangHop(ParkourLedgeTarget_y target, bool moveRight)
        {
            Vector3 leftStartContactPoint = _ledgeTarget.LeftHandContactPoint;
            Vector3 rightStartContactPoint = _ledgeTarget.RightHandContactPoint;
            SetLedgeTarget(target);
            AlignActorToDirection();
            _leftContactStartPoint = leftStartContactPoint;
            _rightContactStartPoint = rightStartContactPoint;
            _leftContactEndPoint = target.LeftHandContactPoint;
            _rightContactEndPoint = target.RightHandContactPoint;
            _hangHopPoseHeightOffset = moveRight ? RightHangHopPoseHeightOffset : 0f;
            if (_hangHopPoseHeightOffset > 0f)
            {
                SnapRootPosition(_actorTransform.position + Vector3.up * _hangHopPoseHeightOffset);
            }

            Vector3 endPosition = target.HangRootPosition
                                  + Vector3.up * _hangHopPoseHeightOffset;
            float naturalDuration = moveRight ? 1.667f : 1.5f;
            StartMotion(MotionMode.HangHop, endPosition,
                naturalDuration / LedgeActionAnimationSpeed,
                null, moveRight ? HangHopRightLateral : HangHopLeftLateral,
                moveRight ? HangHopRightVertical : HangHopLeftVertical,
                0f, moveRight ? 1.417f : -1.379f, 0f);
        }

        public void StartBracedHangToCrouch()
        {
            AlignActorToDirection();
            StartMotion(MotionMode.BracedHangToCrouch, _ledgeTarget.LandingRootPosition,
                1.133f / LedgeActionAnimationSpeed,
                BracedHangToCrouchForward, null,
                BracedHangToCrouchVertical, 0.656f, 0f, 1.305f);
        }

        public void StartCrouchedToSprint(Vector3 direction)
        {
            _direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (_direction.sqrMagnitude < 0.9f)
            {
                _direction = _actorTransform.forward;
            }

            AlignActorToDirection();
            _tangent = Vector3.Cross(Vector3.up, _direction).normalized;
            StartMotion(MotionMode.CrouchedToSprint,
                _actorTransform.position + _direction * 1.261f,
                0.5f / LedgeActionAnimationSpeed,
                CrouchedToSprintForward, null, null, 1.261f, 0f, 0f);
        }

        public void StartJumpFromWall()
        {
            Vector3 destination = _actorTransform.position
                                  - _direction * 1.758f
                                  - _tangent * 0.073f
                                  - Vector3.up * 0.166f;
            StartMotion(MotionMode.JumpFromWall, destination, 1.25f / JumpFromWallAnimationSpeed,
                JumpFromWallForward, null, JumpFromWallVertical, -1.758f, 0f, -0.166f);
        }

        public void SetAnimationProgress(float normalizedTime)
        {
            if (!IsActive || IsHanging)
            {
                return;
            }

            _animationProgress = Mathf.Clamp01(normalizedTime);
            _hasAnimationProgress = true;
        }

        public void UpdateMovement(float deltaTime, ref Vector3 planarVelocity, ref float verticalVelocity)
        {
            if (!IsActive)
            {
                return;
            }

            if (_isComplete)
            {
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                return;
            }

            if (IsHanging)
            {
                _elapsed = Mathf.Min(_elapsed + deltaTime, _duration);
                float matchProgress = _duration > 0f
                    ? Mathf.Clamp01(_elapsed / _duration)
                    : 1f;
                Vector3 holdPosition = Vector3.Lerp(_startPosition, _endPosition, matchProgress);
                holdPosition = KeepBodyOutsideWall(holdPosition);
                Vector3 holdDisplacement = holdPosition - _actorTransform.position;
                planarVelocity = Vector3.ProjectOnPlane(holdDisplacement, Vector3.up)
                                 / Mathf.Max(deltaTime, 0.001f);
                verticalVelocity = holdDisplacement.y / Mathf.Max(deltaTime, 0.001f);
                return;
            }

            _elapsed = Mathf.Min(_elapsed + deltaTime, _duration);
            float fallbackProgress = _elapsed / Mathf.Max(_duration, 0.001f);
            float progress = _hasAnimationProgress
                ? Mathf.Max(_lastProgress, _animationProgress)
                : fallbackProgress;
            progress = Mathf.Clamp01(progress);

            Vector3 targetPosition;
            if (_mode == MotionMode.JumpingToHanging)
            {
                targetPosition = _ledgeTarget.HangRootPosition;
                targetPosition.y = _ledgeTarget.TopPoint.y
                                   - Sample(JumpingToHangingHandHeight, progress);
            }
            else
            {
                Vector3 endpointDelta = _endPosition - _startPosition;
                float forward = ScaleSample(_forwardSamples, progress, _naturalForwardDistance,
                    Vector3.Dot(endpointDelta, _direction));
                float lateral = ScaleSample(_lateralSamples, progress, _naturalLateralDistance,
                    Vector3.Dot(endpointDelta, _tangent));
                float vertical = ScaleSample(_verticalSamples, progress, _naturalVerticalDistance,
                    endpointDelta.y);
                targetPosition = _startPosition
                                 + _direction * forward
                                 + _tangent * lateral
                                 + Vector3.up * vertical;
            }
            if (_mode == MotionMode.JumpingToHanging
                || _mode == MotionMode.HangingIdle
                || _mode == MotionMode.HangHop)
            {
                targetPosition = KeepBodyOutsideWall(targetPosition);
            }

            if (progress >= 0.999f && _mode != MotionMode.JumpingToHanging)
            {
                targetPosition = _endPosition;
            }

            Vector3 displacement = targetPosition - _actorTransform.position;
            planarVelocity = Vector3.ProjectOnPlane(displacement, Vector3.up)
                             / Mathf.Max(deltaTime, 0.001f);
            verticalVelocity = displacement.y / Mathf.Max(deltaTime, 0.001f);
            _lastProgress = progress;
            float completionProgress = _mode == MotionMode.JumpingToHanging
                ? JumpingToHangingBlendStart
                : 0.999f;
            _shouldCompleteAfterMove = progress >= completionProgress;
        }

        public void CompleteAfterMove(ref Vector3 planarVelocity, ref float verticalVelocity)
        {
            if (!_shouldCompleteAfterMove)
            {
                return;
            }

            MotionMode completedMode = _mode;
            _shouldCompleteAfterMove = false;
            _isComplete = true;
            if (completedMode == MotionMode.JumpingToHanging || completedMode == MotionMode.HangHop)
            {
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                return;
            }

            _mode = MotionMode.None;
            RestoreWallCollision();
            if (completedMode != MotionMode.JumpFromWall)
            {
                verticalVelocity = -2f;
            }
        }

        public void Stop()
        {
            RestoreWallCollision();
            _mode = MotionMode.None;
            _shouldCompleteAfterMove = false;
            _hasAnimationProgress = false;
            _isComplete = false;
        }

        private void SetLedgeTarget(ParkourLedgeTarget_y target)
        {
            if (_activeWallCollider != target.WallCollider)
            {
                RestoreWallCollision();
                _activeWallCollider = target.WallCollider;
                if (_activeWallCollider != null)
                {
                    Physics.IgnoreCollision(_controller, _activeWallCollider, true);
                }
            }

            _ledgeTarget = target;
            _direction = target.ClimbDirection;
            _tangent = Vector3.Cross(Vector3.up, _direction).normalized;
            _leftContactStartPoint = target.LeftHandContactPoint;
            _rightContactStartPoint = target.RightHandContactPoint;
            _leftContactEndPoint = target.LeftHandContactPoint;
            _rightContactEndPoint = target.RightHandContactPoint;
        }

        private void StartMotion(MotionMode mode, Vector3 endPosition, float duration,
            float[] forwardSamples, float[] lateralSamples, float[] verticalSamples,
            float naturalForwardDistance, float naturalLateralDistance, float naturalVerticalDistance)
        {
            _mode = mode;
            _startPosition = _actorTransform.position;
            _endPosition = endPosition;
            _duration = duration;
            _forwardSamples = forwardSamples;
            _lateralSamples = lateralSamples;
            _verticalSamples = verticalSamples;
            _naturalForwardDistance = naturalForwardDistance;
            _naturalLateralDistance = naturalLateralDistance;
            _naturalVerticalDistance = naturalVerticalDistance;
            _elapsed = 0f;
            _animationProgress = 0f;
            _lastProgress = 0f;
            _hasAnimationProgress = false;
            _shouldCompleteAfterMove = false;
            _isComplete = false;
        }

        private void RestoreWallCollision()
        {
            if (_activeWallCollider != null)
            {
                Physics.IgnoreCollision(_controller, _activeWallCollider, false);
                _activeWallCollider = null;
            }
        }

        private void SnapRootPosition(Vector3 targetPosition)
        {
            Vector3 displacement = targetPosition - _actorTransform.position;
            if (displacement.sqrMagnitude > Mathf.Epsilon)
            {
                _controller.Move(displacement);
            }
        }

        private void AlignActorToDirection()
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(_direction, Vector3.up).normalized;
            if (planarDirection.sqrMagnitude >= 0.9f)
            {
                _actorTransform.rotation = Quaternion.LookRotation(planarDirection, Vector3.up);
            }
        }

        private Vector3 KeepBodyOutsideWall(Vector3 targetPosition)
        {
            Vector3 wallNormal = Vector3.ProjectOnPlane(_ledgeTarget.WallNormal, Vector3.up).normalized;
            if (wallNormal.sqrMagnitude < 0.9f)
            {
                return targetPosition;
            }

            float requiredClearance = _controller.radius + _controller.skinWidth;
            float currentClearance = Vector3.Dot(
                targetPosition - _ledgeTarget.TopPoint,
                wallNormal);
            if (currentClearance < requiredClearance)
            {
                targetPosition += wallNormal * (requiredClearance - currentClearance);
            }

            return targetPosition;
        }

        private static float ScaleSample(float[] samples, float normalizedTime,
            float naturalDistance, float targetDistance)
        {
            if (samples == null || samples.Length == 0)
            {
                return targetDistance * normalizedTime;
            }

            float sampledDistance = Sample(samples, normalizedTime);
            return Mathf.Abs(naturalDistance) > 0.001f
                ? sampledDistance * (targetDistance / naturalDistance)
                : sampledDistance;
        }

        private static float Sample(float[] samples, float normalizedTime)
        {
            float samplePosition = Mathf.Clamp01(normalizedTime) * (samples.Length - 1);
            int firstIndex = Mathf.FloorToInt(samplePosition);
            int secondIndex = Mathf.Min(firstIndex + 1, samples.Length - 1);
            return Mathf.Lerp(samples[firstIndex], samples[secondIndex], samplePosition - firstIndex);
        }
    }
}
