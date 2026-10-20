using System;
using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ParkourMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ParkourInput _input;
        [SerializeField] private Transform _cameraTransform;

        [Header("Ground Movement")]
        [SerializeField] private float _runSpeed = 7f;
        [SerializeField] private float _sprintSpeed = 12f;
        [SerializeField] private float _groundAcceleration = 32f;
        [SerializeField] private float _groundDeceleration = 26f;
        [SerializeField] private float _sprintStopDeceleration = 24f;
        [SerializeField] private float _rotationSharpness = 16f;

        [Header("Sprint Start")]
        [SerializeField] private float _sprintStartDuration = 0.8f;
        [SerializeField] private AnimationCurve _sprintStartSpeedCurve = CreateSprintStartSpeedCurve();

        [Header("Air Movement")]
        [SerializeField] private float _airAcceleration = 12f;
        [SerializeField] private float _airControl = 0.55f;
        [SerializeField] private float _jumpHeight = 1.9f;
        [SerializeField] private float _gravity = -28f;
        [SerializeField] private float _fallGravityMultiplier = 1.45f;
        [SerializeField] private float _coyoteTime = 0.14f;
        [SerializeField] private float _jumpBufferTime = 0.14f;

        [Header("Wall Run")]
        [SerializeField] private float _wallDetectionDistance = 0.85f;
        [SerializeField] private float _wallRunMinimumSpeed = 6f;
        [SerializeField] private float _wallRunGravity = -3f;
        [SerializeField] private float _wallGripAcceleration = 18f;
        [SerializeField] private float _wallJumpUpSpeed = 8f;
        [SerializeField] private float _wallJumpAwaySpeed = 7f;
        [SerializeField] private float _wallRunMaximumDuration = 2.2f;

        [Header("Recovery")]
        [SerializeField] private float _respawnHeight = -12f;

        [Header("Forward Roll")]
        [SerializeField] private float _rollDuration = 0.95f;
        [SerializeField] private float _rollMinimumSpeed = 8f;
        [SerializeField] private float _rollBoost = 2f;
        [SerializeField] private float _hardLandingRollSpeed = 17.5f;

        private CharacterController _controller;
        private ParkourStateMachine_y _stateMachine;
        private ParkourGroundedState_y _groundedState;
        private ParkourAirborneState_y _airborneState;
        private ParkourWallRunState_y _wallRunState;
        private ParkourRollState_y _rollState;

        private Vector3 _desiredDirection;
        private Vector3 _planarVelocity;
        private Vector3 _spawnPosition;
        private Vector3 _wallNormal;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _verticalVelocity;
        private float _wallRunTimer;
        private float _rollTimer;
        private bool _wasGrounded;
        private bool _hasPendingHardLandRoll;

        public event Action<float> Landed;
        public event Action HardLanded;
        public event Action Jumped;
        public event Action Rolled;

        public float PlanarSpeed => _planarVelocity.magnitude;
        public Vector3 PlanarVelocity => _planarVelocity;
        public Vector3 DesiredDirection => _desiredDirection;
        public float VerticalSpeed => _verticalVelocity;
        public float GroundAcceleration => _groundAcceleration;
        public float GroundDeceleration => _groundDeceleration;
        public float SprintStopDeceleration => _sprintStopDeceleration;
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsWallRunning { get; private set; }
        public bool IsRolling => _rollTimer > 0f;
        public bool HasMovementInput => _input != null && _input.Move.sqrMagnitude > 0.01f;
        public bool IsSprinting => _input != null
                                   && _input.IsSprinting
                                   && _input.Move.sqrMagnitude > 0.01f;
        public Vector3 WallNormal => _wallNormal;

        public ParkourRootStateType_y CurrentRootStateType =>
            _stateMachine != null ? _stateMachine.CurrentRootStateType : ParkourRootStateType_y.None;

        public ParkourSubStateType_y CurrentSubStateType =>
            _stateMachine != null ? _stateMachine.CurrentSubStateType : ParkourSubStateType_y.None;

        internal ParkourGroundedState_y GroundedState => _groundedState;
        internal ParkourAirborneState_y AirborneState => _airborneState;
        internal ParkourWallRunState_y WallRunState => _wallRunState;
        internal ParkourRollState_y RollState => _rollState;

        public void Initialize(ParkourInput input, Transform cameraTransform)
        {
            _input = input;
            _cameraTransform = cameraTransform;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _spawnPosition = transform.position;

            if (_sprintStartSpeedCurve == null || _sprintStartSpeedCurve.length != 6)
            {
                _sprintStartDuration = 0.8f;
                _sprintStartSpeedCurve = CreateSprintStartSpeedCurve();
            }

            InitializeStateMachine();
        }

        private void InitializeStateMachine()
        {
            _stateMachine = new ParkourStateMachine_y();
            _groundedState = new ParkourGroundedState_y(this, _stateMachine);
            _airborneState = new ParkourAirborneState_y(this, _stateMachine);
            _wallRunState = new ParkourWallRunState_y(this, _stateMachine);
            _rollState = new ParkourRollState_y(this, _stateMachine);
            _stateMachine.Initialize(_groundedState);
        }

        private void Update()
        {
            if (_input == null || _cameraTransform == null)
            {
                return;
            }

            if (_stateMachine == null)
            {
                InitializeStateMachine();
            }

            var deltaTime = Time.deltaTime;
            UpdateTimers(deltaTime);
            UpdateGroundState();

            _desiredDirection = CalculateDesiredDirection(_input.Move);
            _stateMachine.UpdateState(deltaTime);

            Vector3 motion = _planarVelocity + Vector3.up * _verticalVelocity;
            CollisionFlags collisionFlags = _controller.Move(motion * deltaTime);
            SnapCeilingCollision(collisionFlags);
            RotateTowardsMotion(deltaTime);

            if (transform.position.y < _respawnHeight)
            {
                Respawn();
            }
        }

        public float GetTargetGroundSpeed()
        {
            return _input != null && _input.IsSprinting ? _sprintSpeed : _runSpeed;
        }

        public void UpdatePlanarVelocityTowards(Vector3 targetVelocity, float acceleration, float deltaTime)
        {
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                targetVelocity,
                acceleration * deltaTime);
        }

        public bool ApplySprintStartCurve(float elapsedTime)
        {
            var duration = Mathf.Max(_sprintStartDuration, 0.01f);
            var normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            var speedRatio = Mathf.Clamp01(_sprintStartSpeedCurve.Evaluate(normalizedTime));
            _planarVelocity = _desiredDirection * (GetTargetGroundSpeed() * speedRatio);
            return normalizedTime >= 1f;
        }

        public void UpdateAirPlanarVelocity(float deltaTime)
        {
            Vector3 desiredVelocity = _desiredDirection * GetTargetGroundSpeed();
            Vector3 airTargetVelocity = Vector3.Lerp(_planarVelocity, desiredVelocity, _airControl);
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                airTargetVelocity,
                _airAcceleration * deltaTime);
        }

        public void ApplyGravity(float deltaTime)
        {
            var gravityMultiplier = _verticalVelocity < 0f ? _fallGravityMultiplier : 1f;
            _verticalVelocity += _gravity * gravityMultiplier * deltaTime;
        }

        public void SnapGroundedVerticalVelocity()
        {
            if (_verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
        }

        public bool CanStartJump()
        {
            return _jumpBufferTimer > 0f && _coyoteTimer > 0f;
        }

        public bool HasBufferedJump()
        {
            return _jumpBufferTimer > 0f;
        }

        public void ApplyGroundJumpImpulse()
        {
            _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            Jumped?.Invoke();
        }

        public bool CanStartRoll()
        {
            if (!_input.WasRollPressed || !_controller.isGrounded || _desiredDirection.sqrMagnitude < 0.01f)
            {
                return false;
            }

            StartRoll(_desiredDirection);
            Rolled?.Invoke();
            return true;
        }

        public bool ConsumePendingHardLandRoll()
        {
            if (!_hasPendingHardLandRoll)
            {
                return false;
            }

            _hasPendingHardLandRoll = false;
            return true;
        }

        public void UpdateRollMovement(float deltaTime)
        {
            _verticalVelocity = -2f;
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                transform.forward * _rollMinimumSpeed,
                _groundDeceleration * 0.35f * deltaTime);
        }

        public bool CanStartOrContinueWallRun()
        {
            if (_controller.isGrounded || _planarVelocity.magnitude < _wallRunMinimumSpeed)
            {
                return false;
            }

            if (_wallRunTimer >= _wallRunMaximumDuration)
            {
                return false;
            }

            Vector3 origin = transform.position + Vector3.up * (_controller.height * 0.5f);
            var hitLeft = Physics.Raycast(
                origin,
                -transform.right,
                out RaycastHit leftHit,
                _wallDetectionDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            var hitRight = Physics.Raycast(
                origin,
                transform.right,
                out RaycastHit rightHit,
                _wallDetectionDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            RaycastHit wallHit;
            if (hitLeft && IsRunnableWall(leftHit.normal))
            {
                wallHit = leftHit;
            }
            else if (hitRight && IsRunnableWall(rightHit.normal))
            {
                wallHit = rightHit;
            }
            else
            {
                return false;
            }

            Vector3 alongWall = Vector3.ProjectOnPlane(_planarVelocity, wallHit.normal).normalized;
            if (alongWall.sqrMagnitude < 0.01f)
            {
                alongWall = Vector3.Cross(Vector3.up, wallHit.normal);
            }

            if (Vector3.Dot(alongWall, _desiredDirection) < 0f)
            {
                alongWall = -alongWall;
            }

            _wallNormal = wallHit.normal;
            return _desiredDirection.sqrMagnitude > 0.01f && Vector3.Dot(alongWall, _desiredDirection) > 0.15f;
        }

        public void StartWallRunStatus()
        {
            IsWallRunning = true;
        }

        public void StopWallRunStatus()
        {
            IsWallRunning = false;
            _wallNormal = Vector3.zero;
            if (_controller != null && _controller.isGrounded)
            {
                _wallRunTimer = 0f;
            }
        }

        public void ResetWallRunTimer()
        {
            _wallRunTimer = 0f;
        }

        public void UpdateWallRunMovement(float deltaTime)
        {
            _wallRunTimer += deltaTime;

            Vector3 alongWall = Vector3.ProjectOnPlane(_planarVelocity, _wallNormal).normalized;
            if (Vector3.Dot(alongWall, _desiredDirection) < 0f)
            {
                alongWall = -alongWall;
            }

            var maintainedSpeed = Mathf.Max(_planarVelocity.magnitude, _wallRunMinimumSpeed);
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                alongWall * maintainedSpeed,
                _wallGripAcceleration * deltaTime);
            _verticalVelocity = Mathf.MoveTowards(
                _verticalVelocity,
                _wallRunGravity,
                _wallGripAcceleration * deltaTime);
        }

        public void ApplyWallJumpImpulse()
        {
            Vector3 alongWall = Vector3.ProjectOnPlane(_planarVelocity, _wallNormal).normalized;
            if (Vector3.Dot(alongWall, _desiredDirection) < 0f)
            {
                alongWall = -alongWall;
            }

            var maintainedSpeed = Mathf.Max(_planarVelocity.magnitude, _wallRunMinimumSpeed);
            _planarVelocity = alongWall * maintainedSpeed + _wallNormal * _wallJumpAwaySpeed;
            _verticalVelocity = _wallJumpUpSpeed;
            _jumpBufferTimer = 0f;
            _wallRunTimer = _wallRunMaximumDuration;
            Jumped?.Invoke();
        }

        private void UpdateTimers(float deltaTime)
        {
            _jumpBufferTimer -= deltaTime;
            _coyoteTimer -= deltaTime;
            _rollTimer -= deltaTime;

            if (_input.WasJumpPressed)
            {
                _jumpBufferTimer = _jumpBufferTime;
            }
        }

        private void UpdateGroundState()
        {
            var isGrounded = _controller.isGrounded;
            if (isGrounded)
            {
                _coyoteTimer = _coyoteTime;
                if (!_wasGrounded)
                {
                    var impactSpeed = Mathf.Abs(_verticalVelocity);
                    Landed?.Invoke(impactSpeed);
                    if (impactSpeed >= _hardLandingRollSpeed && _planarVelocity.magnitude >= _wallRunMinimumSpeed)
                    {
                        StartRoll(_planarVelocity.normalized);
                        _hasPendingHardLandRoll = true;
                        HardLanded?.Invoke();
                    }
                }

                SnapGroundedVerticalVelocity();
            }

            _wasGrounded = isGrounded;
        }

        private Vector3 CalculateDesiredDirection(Vector2 moveInput)
        {
            Vector3 cameraForward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
            Vector3 cameraRight = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
            Vector3 desiredDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;
            return Vector3.ClampMagnitude(desiredDirection, 1f);
        }

        private void StartRoll(Vector3 direction)
        {
            var rollSpeed = Mathf.Max(_planarVelocity.magnitude + _rollBoost, _rollMinimumSpeed);
            _planarVelocity = direction * rollSpeed;
            _rollTimer = _rollDuration;
        }

        private static bool IsRunnableWall(Vector3 normal)
        {
            return Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.2f;
        }

        private void SnapCeilingCollision(CollisionFlags collisionFlags)
        {
            if ((collisionFlags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }
        }

        private void RotateTowardsMotion(float deltaTime)
        {
            Vector3 direction = Vector3.ProjectOnPlane(_planarVelocity, Vector3.up);
            if (direction.sqrMagnitude < 0.1f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-_rotationSharpness * deltaTime));
        }

        private void Respawn()
        {
            _controller.enabled = false;
            transform.position = _spawnPosition;
            transform.rotation = Quaternion.identity;
            _controller.enabled = true;
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _wallRunTimer = 0f;
            _rollTimer = 0f;
            _hasPendingHardLandRoll = false;
            StopWallRunStatus();
            _stateMachine.SetRootState(_groundedState);
        }

        public static AnimationCurve CreateSprintStartSpeedCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.12f, 0f),
                new Keyframe(0.25f, 0.22f),
                new Keyframe(0.55f, 0.6f),
                new Keyframe(0.8f, 0.85f),
                new Keyframe(1f, 1f));

            Keyframe[] keys = curve.keys;
            for (var index = 0; index < keys.Length; index++)
            {
                var inTangent = index == 0 ? 0f : CalculateCurveSegmentSlope(keys[index - 1], keys[index]);
                var outTangent = index == keys.Length - 1 ? 0f : CalculateCurveSegmentSlope(keys[index], keys[index + 1]);
                keys[index] = new Keyframe(keys[index].time, keys[index].value, inTangent, outTangent);
            }

            curve.keys = keys;
            return curve;
        }

        private static float CalculateCurveSegmentSlope(Keyframe start, Keyframe end)
        {
            return (end.value - start.value) / (end.time - start.time);
        }
    }
}
