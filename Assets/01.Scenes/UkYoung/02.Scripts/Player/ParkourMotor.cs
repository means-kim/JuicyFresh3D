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
        [SerializeField] private AnimationCurve _sprintStartSpeedCurve = ParkourSprintStartState_y.CreateDefaultSpeedCurve();

        [Header("Air Movement")]
        [SerializeField] private float _airAcceleration = 12f;
        [SerializeField] private float _airControl = 0.55f;
        [SerializeField] private float _jumpHeight = 1.9f;
        [SerializeField] private float _gravity = -28f;
        [SerializeField] private float _fallGravityMultiplier = 1.45f;
        [SerializeField] private float _coyoteTime = 0.14f;
        [SerializeField] private float _jumpBufferTime = 0.14f;

        [Header("Obstacle Jump")]
        [SerializeField] private float _obstacleJumpDetectionDistance = 3.8f;
        [SerializeField] private float _minimumObstacleHeight = 0.25f;
        [SerializeField] private float _maximumObstacleHeight = 1.65f;

        [Header("Recovery")]
        [SerializeField] private float _respawnHeight = -12f;

        private CharacterController _controller;
        private ParkourObstacleJump_y _obstacleJump;
        private ParkourStateMachine_y _stateMachine;
        private ParkourGroundedState_y _groundedState;
        private ParkourAirborneState_y _airborneState;

        private Vector3 _desiredDirection;
        private Vector3 _planarVelocity;
        private Vector3 _spawnPosition;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _verticalVelocity;
        private bool _wasGrounded;

        public event Action<float> Landed;
        public event Action Jumped;

        public float PlanarSpeed => _planarVelocity.magnitude;
        public Vector3 PlanarVelocity => _planarVelocity;
        public Vector3 DesiredDirection => _desiredDirection;
        public float VerticalSpeed => _verticalVelocity;
        public float GroundAcceleration => _groundAcceleration;
        public float GroundDeceleration => _groundDeceleration;
        public float SprintStopDeceleration => _sprintStopDeceleration;
        public float SprintStartDuration => _sprintStartDuration;
        public AnimationCurve SprintStartSpeedCurve => _sprintStartSpeedCurve;
        public int CurrentJumpTier => _obstacleJump != null ? _obstacleJump.CurrentJumpTier : 0;
        public bool IsObstacleVaulting => _obstacleJump != null && _obstacleJump.IsVaulting;
        public bool IsGrounded => _controller != null
                                  && _controller.isGrounded
                                  && _verticalVelocity <= 0.05f
                                  && !IsObstacleVaulting;
        public bool HasMovementInput => _input != null && _input.Move.sqrMagnitude > 0.01f;
        public bool IsSprinting => _input != null
                                   && _input.IsSprinting
                                   && _input.Move.sqrMagnitude > 0.01f;

        public ParkourRootStateType_y CurrentRootStateType =>
            _stateMachine != null ? _stateMachine.CurrentRootStateType : ParkourRootStateType_y.None;

        public ParkourSubStateType_y CurrentSubStateType =>
            _stateMachine != null ? _stateMachine.CurrentSubStateType : ParkourSubStateType_y.None;

        internal ParkourGroundedState_y GroundedState => _groundedState;
        internal ParkourAirborneState_y AirborneState => _airborneState;

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
                _sprintStartSpeedCurve = ParkourSprintStartState_y.CreateDefaultSpeedCurve();
            }

            EnsureObstacleJumpInitialized();
            InitializeStateMachine();
        }

        private void OnDisable()
        {
            _obstacleJump?.StopVault();
        }

        private void EnsureObstacleJumpInitialized()
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }

            if (_obstacleJump == null)
            {
                _obstacleJump = new ParkourObstacleJump_y(
                    transform,
                    _controller,
                    _obstacleJumpDetectionDistance,
                    _minimumObstacleHeight,
                    _maximumObstacleHeight);
            }
        }

        private void InitializeStateMachine()
        {
            _stateMachine = new ParkourStateMachine_y();
            _groundedState = new ParkourGroundedState_y(this, _stateMachine);
            _airborneState = new ParkourAirborneState_y(this, _stateMachine);
            _stateMachine.Initialize(_groundedState);
        }

        private void Update()
        {
            if (_input == null || _cameraTransform == null)
            {
                return;
            }

            EnsureObstacleJumpInitialized();
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

        public void SetPlanarVelocity(Vector3 planarVelocity)
        {
            _planarVelocity = planarVelocity;
        }

        public void UpdatePlanarVelocityTowards(Vector3 targetVelocity, float acceleration, float deltaTime)
        {
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                targetVelocity,
                acceleration * deltaTime);
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
            return _jumpBufferTimer > 0f && _coyoteTimer > 0f && !IsObstacleVaulting;
        }

        public void ApplyGroundJumpImpulse()
        {
            EnsureObstacleJumpInitialized();
            _obstacleJump.StartJump(
                _desiredDirection,
                _jumpHeight,
                _gravity,
                ref _planarVelocity,
                ref _verticalVelocity);

            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            Jumped?.Invoke();
        }

        public void UpdateObstacleVaultMovement(float deltaTime)
        {
            EnsureObstacleJumpInitialized();
            _obstacleJump.UpdateVaultMovement(
                deltaTime,
                HasMovementInput,
                _desiredDirection,
                GetTargetGroundSpeed(),
                _groundAcceleration,
                ref _planarVelocity,
                ref _verticalVelocity);

            if (_controller != null && !_controller.isGrounded)
            {
                ApplyGravity(deltaTime);
            }
        }

        public void StopObstacleVault()
        {
            _obstacleJump?.StopVault();
        }

        private void UpdateTimers(float deltaTime)
        {
            _jumpBufferTimer -= deltaTime;
            _coyoteTimer -= deltaTime;

            if (_input.WasJumpPressed)
            {
                _jumpBufferTimer = _jumpBufferTime;
            }
        }

        private void UpdateGroundState()
        {
            var isGrounded = IsGrounded;
            if (isGrounded)
            {
                _coyoteTimer = _coyoteTime;
                if (!_wasGrounded)
                {
                    var impactSpeed = Mathf.Abs(_verticalVelocity);
                    Landed?.Invoke(impactSpeed);
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
            _obstacleJump?.ResetState();
            _controller.enabled = false;
            transform.position = _spawnPosition;
            transform.rotation = Quaternion.identity;
            _controller.enabled = true;
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _stateMachine.SetRootState(_groundedState);
        }
    }
}
