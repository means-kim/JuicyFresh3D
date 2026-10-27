using System;
using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ParkourMotor : MonoBehaviour
    {
        private const float WallRunBufferTime = 0.28f;
        private const float HangHopReleaseEnd = 0.18f;
        private const float HangHopRecontactStart = 0.68f;
        private const float HangHopRecontactEnd = 0.9f;
        private const float HangHopDistance = 1.5f;
        private const float JumpingToHangingContactStart = 0.78f;
        private const float JumpingToHangingContactEnd = 0.82f;

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

        [Header("Slide")]
        [Tooltip("기본 슬라이딩 진입 속도")]
        [SerializeField] private float _baseSlideSpeed = 13.5f;
        [Tooltip("슬라이딩 최소 속도")]
        [SerializeField] private float _minSlideSpeed = 6.0f;
        [Tooltip("평지 슬라이딩 마찰 감속률 (높을수록 이동 거리 감소)")]
        [SerializeField] private float _slideFrictionRate = 8.0f;
        [Tooltip("슬라이딩 최대 지속 시간 (낮출수록 이동 거리 감소)")]
        [SerializeField] private float _maxSlideDuration = 1.35f;
        [Tooltip("슬라이딩 최소 지속 시간")]
        [SerializeField] private float _minSlideDuration = 1.05f;
        [Tooltip("감속 전 최고 속도 유지 시간 (낮출수록 이동 거리 감소)")]
        [SerializeField] private float _slideInflectionTime = 0.53f;

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
        [SerializeField] private float _obstacleJumpDetectionDistance = 2.2f;
        [SerializeField] private float _minimumObstacleHeight = 0.45f;
        [SerializeField] private float _maximumObstacleHeight = 1.65f;

        [Header("Obstacle Jump Feel")]
        [SerializeField]
        private ObstacleJumpFeel_y _jumping1Feel =
            ObstacleJumpFeel_y.Create(1.45f, 0.27f, 1.118f, 0.127f);
        [SerializeField]
        private ObstacleJumpFeel_y _jumping2Feel =
            ObstacleJumpFeel_y.Create(2f, 0.07f, 1.321f, 0.185f);
        [SerializeField]
        private ObstacleJumpFeel_y _jumping3Feel =
            ObstacleJumpFeel_y.Create(3f, 0.18f, 1.069f, 0.254f);
        [SerializeField]
        private ObstacleJumpFeel_y _jumpOverFeel =
            ObstacleJumpFeel_y.Create(2.5f, 0.17f, 1.191f, 0.234f);

        [Header("Wall Run")]
        [SerializeField] private float _wallDetectionDistance = 1.35f;
        [SerializeField] private float _wallRunMaxDuration = 0.95f;
        [SerializeField] private float _wallRunSpeed = 7.8f;
        [SerializeField] private float _wallJumpHeight = 1.65f;
        [SerializeField] private float _wallJumpHorizontalSpeed = 8.2f;
        [SerializeField] private float _wallJumpNormalWeight = 0.8f;

        [Header("Sprint Wall Climb")]
        [SerializeField] private float _wallClimbDetectionDistance = 2.1f;
        [SerializeField] private float _minimumWallClimbHeight = 1.5f;
        [SerializeField] private float _maximumWallClimbHeight = 2.25f;

        [Header("Landing")]
        [Tooltip("소프트 랜딩 착지 수직 속도 기준")]
        [SerializeField] private float _lightLandingSpeedThreshold = 4.5f;
        [Tooltip("하드 랜딩 착지 수직 속도 기준")]
        [SerializeField] private float _hardLandingSpeedThreshold = 10.5f;
        [Tooltip("구르기 발동 최소 낙하 높이차")]
        [SerializeField] private float _minRollFallHeight = 0.8f;
        [Tooltip("구르기 발동 최소 전진 관성 속도")]
        [SerializeField] private float _minRollPlanarSpeed = 1.5f;
        [Tooltip("평지 점프 판정을 위한 최대 고도 오차 허용치")]
        [SerializeField] private float _flatJumpMaxElevationTolerance = 0.6f;
        [SerializeField] private float _softLandDuration = 0.40f;
        [SerializeField] private float _hardLandDuration = 0.65f;
        [SerializeField] private float _minLandCancelDuration = 0.22f;
        [SerializeField] private float _minHardLandCancelDuration = 0.42f;
        [SerializeField] private float _rollDuration = 0.88f;
        [SerializeField] private float _minRollCancelDuration = 0.65f;

        [Header("Recovery")]
        [SerializeField] private float _respawnHeight = -12f;

        private CharacterController _controller;
        private ParkourObstacleJump_y _obstacleJump;
        private ParkourWallRun_y _wallRun;
        private ParkourSlide_y _slide;
        private ParkourLedgeDetector_y _ledgeDetector;
        private ParkourClimbMotion_y _climbMotion;
        private ParkourStateMachine_y _stateMachine;
        private ParkourGroundedState_y _groundedState;
        private ParkourAirborneState_y _airborneState;

        private Vector3 _desiredDirection;
        private Vector3 _planarVelocity;
        private Vector3 _spawnPosition;
        private Vector3 _pendingWallNormal;
        private Vector3 _pendingWallTangent;
        private Collider _pendingWallCollider;
        private ParkourLedgeTarget_y _pendingLedgeTarget;
        private ParkourLedgeTarget_y _pendingHangHopTarget;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _wallRunBufferTimer;
        private float _verticalVelocity;
        private int _pendingWallSide;
        private bool _wasGrounded;
        private bool _canGrabLedgeFromJump;
        private bool _isHangHopInputReady = true;

        public event Action<float> Landed;
        public event Action Jumped;
        public event Action<ParkourSubStateType_y, ParkourSubStateType_y> SubStateChanged;

        public float LastLandingImpactSpeed { get; private set; }
        public float LastVerticalDropDistance { get; private set; }
        public bool LastAirborneWasJump { get; private set; }
        public float LightLandingSpeedThreshold => _lightLandingSpeedThreshold;
        public float HardLandingSpeedThreshold => _hardLandingSpeedThreshold;
        public float MinRollFallHeight => _minRollFallHeight;
        public float MinRollPlanarSpeed => _minRollPlanarSpeed;
        public float FlatJumpMaxElevationTolerance => _flatJumpMaxElevationTolerance;
        public float LastFallDistance => LastVerticalDropDistance;
        public float SoftLandDuration => _softLandDuration;
        public float HardLandDuration => _hardLandDuration;
        public float MinLandCancelDuration => _minLandCancelDuration;
        public float MinHardLandCancelDuration => _minHardLandCancelDuration;
        public float RollDuration => _rollDuration;
        public float MinRollCancelDuration => _minRollCancelDuration;
        public float WallJumpHeight => _wallJumpHeight;
        public bool IsLanding => CurrentSubStateType == ParkourSubStateType_y.Land;
        public bool IsRolling => CurrentSubStateType == ParkourSubStateType_y.Roll;

        public void SetLandingFallMetrics(float dropDistance, bool wasJump)
        {
            LastVerticalDropDistance = Mathf.Max(dropDistance, 0f);
            LastAirborneWasJump = wasJump;
            if (LastLandingImpactSpeed <= 0f)
            {
                LastLandingImpactSpeed = Mathf.Abs(_verticalVelocity);
            }
        }

        public float PlanarSpeed => _planarVelocity.magnitude;
        public float RunSpeed => _runSpeed;
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
        public Collider ActiveVaultObstacle => _obstacleJump != null ? _obstacleJump.ActiveObstacleCollider : null;
        public float VaultCameraLift => _obstacleJump != null ? _obstacleJump.CameraLift : 0f;
        public float ObstacleJumpAnimationSpeed =>
            _obstacleJump != null ? _obstacleJump.AnimationSpeed : 1f;
        public float ObstacleJumpProgress =>
            _obstacleJump != null ? _obstacleJump.NormalizedProgress : 0f;
        public Vector3 VaultDirection => _obstacleJump != null ? _obstacleJump.VaultDirection : transform.forward;
        public Vector3 VaultContactPoint => _obstacleJump != null ? _obstacleJump.VaultContactPoint : transform.position;
        public float VaultHandContactWeight => _obstacleJump != null ? _obstacleJump.HandContactWeight : 0f;
        public bool IsWallRunning => _wallRun != null && _wallRun.IsWallRunning;
        public int WallSide => _wallRun != null ? _wallRun.WallSide : 0;
        public Collider ActiveWallCollider => _wallRun != null ? _wallRun.ActiveWallCollider : null;
        public bool IsSliding => _slide != null && _slide.IsSliding;
        public bool IsClimbing => _climbMotion != null && _climbMotion.IsActive;
        public bool IsHanging => _climbMotion != null && _climbMotion.IsHanging;
        public bool IsHangPoseMatched => _climbMotion != null && _climbMotion.IsHangPoseMatched;
        public bool IsLedgeTraversing => CurrentSubStateType >= ParkourSubStateType_y.JumpingToHanging
                                         && CurrentSubStateType <= ParkourSubStateType_y.JumpFromWall;
        public bool IsClimbMotionComplete => _climbMotion != null && _climbMotion.IsComplete;
        public float ClimbProgress => _climbMotion != null ? _climbMotion.NormalizedProgress : 0f;
        public Vector3 LedgeLeftHandContactPoint => _climbMotion != null
            ? _climbMotion.LeftContactPoint
            : transform.position;
        public Vector3 LedgeRightHandContactPoint => _climbMotion != null
            ? _climbMotion.RightContactPoint
            : transform.position;
        public Vector3 LedgeDirection => _climbMotion != null
            ? _climbMotion.Direction
            : transform.forward;
        public float ClimbAnimationSpeed => _climbMotion != null
            ? _climbMotion.AnimationSpeed
            : 1f;
        public float ClimbVisualRootHeightOffset => _climbMotion != null
            ? _climbMotion.VisualRootHeightOffset
            : 0f;
        public bool ShouldAlignHangingBodyDirection => CurrentSubStateType switch
        {
            ParkourSubStateType_y.JumpingToHanging => true,
            ParkourSubStateType_y.HangingIdle => true,
            ParkourSubStateType_y.BracedHangHopLeft => true,
            ParkourSubStateType_y.BracedHangHopRight => true,
            _ => false
        };
        public float LedgeHandContactWeight
        {
            get
            {
                if (!IsLedgeTraversing || _climbMotion == null)
                {
                    return 0f;
                }

                float progress = _climbMotion.NormalizedProgress;
                return CurrentSubStateType switch
                {
                    ParkourSubStateType_y.JumpingToHanging => Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(
                            JumpingToHangingContactStart,
                            JumpingToHangingContactEnd,
                            progress)),
                    ParkourSubStateType_y.BracedHangHopLeft => CalculateHangHopContactWeight(progress),
                    ParkourSubStateType_y.BracedHangHopRight => CalculateHangHopContactWeight(progress),
                    ParkourSubStateType_y.JumpFromWall => 1f - Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0f, 0.22f, progress)),
                    ParkourSubStateType_y.BracedHangToCrouch => 1f - Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.35f, 0.7f, progress)),
                    _ => 1f
                };
            }
        }
        public bool WasJumpPressed => _input != null && _input.WasJumpPressed;
        public bool IsGrounded => _controller != null
                                  && _controller.isGrounded
                                  && _verticalVelocity <= 0.05f
                                  && !IsObstacleVaulting
                                  && !IsWallRunning
                                  && !IsClimbing;
        public bool HasMovementInput => _input != null && _input.Move.sqrMagnitude > 0.01f;
        public bool HasForwardInput => _input != null && _input.Move.y > 0.1f;
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

            EnsureSubsystemsInitialized();
            InitializeStateMachine();
        }

        private void OnDisable()
        {
            _obstacleJump?.StopVault();
            _wallRun?.StopWallRun();
            _slide?.StopSlide(_controller);
            _climbMotion?.Stop();
        }

        private void EnsureSubsystemsInitialized()
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
                    _maximumObstacleHeight,
                    _jumping1Feel,
                    _jumping2Feel,
                    _jumping3Feel,
                    _jumpOverFeel);
            }

            if (_wallRun == null)
            {
                _wallRun = new ParkourWallRun_y(
                    transform,
                    _wallDetectionDistance,
                    _wallRunMaxDuration,
                    _wallRunSpeed,
                    _wallJumpHeight,
                    _wallJumpHorizontalSpeed,
                    _wallJumpNormalWeight);
            }

            if (_slide == null)
            {
                _slide = new ParkourSlide_y();
            }

            if (_ledgeDetector == null)
            {
                _ledgeDetector = new ParkourLedgeDetector_y(
                    transform,
                    _controller,
                    _wallClimbDetectionDistance,
                    _minimumWallClimbHeight,
                    _maximumWallClimbHeight);
            }

            if (_climbMotion == null)
            {
                _climbMotion = new ParkourClimbMotion_y(transform, _controller);
            }

            _slide.ConfigureSettings(
                _baseSlideSpeed,
                _minSlideSpeed,
                _slideFrictionRate,
                _maxSlideDuration,
                _minSlideDuration,
                _slideInflectionTime);
        }

        private void InitializeStateMachine()
        {
            if (_stateMachine != null)
            {
                _stateMachine.SubStateChanged -= HandleSubStateChanged;
            }

            _stateMachine = new ParkourStateMachine_y();
            _stateMachine.SubStateChanged += HandleSubStateChanged;
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

            EnsureSubsystemsInitialized();
            if (_stateMachine == null)
            {
                InitializeStateMachine();
            }

            var deltaTime = Time.deltaTime;
            UpdateTimers(deltaTime);
            UpdateGroundState();
            _wallRun.UpdateTimers(deltaTime, IsGrounded);

            _desiredDirection = CalculateDesiredDirection(_input.Move);
            _stateMachine.UpdateState(deltaTime);

            Vector3 motion = _planarVelocity + Vector3.up * _verticalVelocity;
            CollisionFlags collisionFlags = _controller.Move(motion * deltaTime);
            _obstacleJump.CompleteVaultAfterMove();
            _climbMotion.CompleteAfterMove(ref _planarVelocity, ref _verticalVelocity);
            _stateMachine.UpdateAfterMoveState();
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

        private void HandleSubStateChanged(
            ParkourSubStateType_y previousSubStateType,
            ParkourSubStateType_y currentSubStateType)
        {
            SubStateChanged?.Invoke(previousSubStateType, currentSubStateType);
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
            if (IsSliding)
            {
                return;
            }

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
            EnsureSubsystemsInitialized();
            _obstacleJump.SetFeelSettings(
                _jumping1Feel,
                _jumping2Feel,
                _jumping3Feel,
                _jumpOverFeel);
            _obstacleJump.StartJump(
                _desiredDirection,
                _jumpHeight,
                _gravity,
                ref _planarVelocity,
                ref _verticalVelocity);

            _jumpBufferTimer = 0f;
            _wallRunBufferTimer = 0f;
            _coyoteTimer = 0f;
            _canGrabLedgeFromJump = !IsObstacleVaulting;
            Jumped?.Invoke();
        }

        public void UpdateObstacleVaultMovement(float deltaTime)
        {
            EnsureSubsystemsInitialized();
            var wasVaulting = _obstacleJump.IsVaulting;
            _obstacleJump.UpdateVaultMovement(
                deltaTime,
                ref _planarVelocity,
                ref _verticalVelocity);

            if (_controller != null
                && !_controller.isGrounded
                && !wasVaulting)
            {
                ApplyGravity(deltaTime);
            }
        }

        public void SetObstacleVaultAnimationProgress(float normalizedTime)
        {
            _obstacleJump?.SetAnimationProgress(normalizedTime);
        }

        public void StopObstacleVault()
        {
            _obstacleJump?.StopVault();
        }

        public bool CanStartWallRun()
        {
            if (IsGrounded || IsObstacleVaulting || (_jumpBufferTimer <= 0f && _wallRunBufferTimer <= 0f))
            {
                return false;
            }

            EnsureSubsystemsInitialized();
            return _wallRun.CanDetectWall(
                _planarVelocity,
                _desiredDirection,
                out _pendingWallNormal,
                out _pendingWallTangent,
                out _pendingWallSide,
                out _pendingWallCollider);
        }

        public void StartWallRun()
        {
            EnsureSubsystemsInitialized();
            _jumpBufferTimer = 0f;
            _wallRunBufferTimer = 0f;
            _coyoteTimer = 0f;
            _wallRun.StartWallRun(
                _pendingWallNormal,
                _pendingWallTangent,
                _pendingWallSide,
                _pendingWallCollider,
                ref _planarVelocity,
                ref _verticalVelocity);
        }

        public bool UpdateWallRunMovement(float deltaTime)
        {
            EnsureSubsystemsInitialized();
            return _wallRun.UpdateWallRun(
                deltaTime,
                _gravity,
                ref _planarVelocity,
                ref _verticalVelocity);
        }

        public void StopWallRun()
        {
            _wallRun?.StopWallRun();
        }

        public bool CanStartSprintWallClimb()
        {
            if (!IsGrounded
                || !IsSprinting
                || !HasForwardInput
                || _jumpBufferTimer <= 0f
                || IsObstacleVaulting
                || IsWallRunning
                || IsSliding)
            {
                return false;
            }

            EnsureSubsystemsInitialized();
            return _ledgeDetector.TryFindSprintWallClimbTarget(
                _desiredDirection,
                out _pendingLedgeTarget);
        }

        public void StartSprintWallClimb()
        {
            EnsureSubsystemsInitialized();
            _jumpBufferTimer = 0f;
            _wallRunBufferTimer = 0f;
            _coyoteTimer = 0f;
            _obstacleJump.ResetState();
            _canGrabLedgeFromJump = false;
            _climbMotion.StartSprintWallClimb(_pendingLedgeTarget);
        }

        public bool CanStartJumpingToHanging()
        {
            if (!_canGrabLedgeFromJump
                || IsGrounded
                || IsObstacleVaulting
                || IsWallRunning
                || IsClimbing
                || _verticalVelocity > 0.05f)
            {
                return false;
            }

            Vector3 approachDirection = _planarVelocity.sqrMagnitude > 0.25f
                ? _planarVelocity.normalized
                : _desiredDirection;
            if (!_ledgeDetector.TryFindHangingTarget(approachDirection, out _pendingLedgeTarget))
            {
                return false;
            }

            Vector3 entryDelta = _pendingLedgeTarget.HangEntryRootPosition - transform.position;
            Vector3 tangent = Vector3.Cross(Vector3.up, _pendingLedgeTarget.ClimbDirection).normalized;
            float maximumPlanarCorrection = PlanarSpeed * Time.deltaTime + _controller.skinWidth;
            float maximumVerticalCorrection = Mathf.Abs(_verticalVelocity) * Time.deltaTime
                                              + _controller.skinWidth;
            return Mathf.Abs(Vector3.Dot(entryDelta, _pendingLedgeTarget.ClimbDirection))
                       <= maximumPlanarCorrection
                   && Mathf.Abs(Vector3.Dot(entryDelta, tangent)) <= _controller.skinWidth
                   && Mathf.Abs(entryDelta.y) <= maximumVerticalCorrection;
        }

        public void StartJumpingToHanging()
        {
            _canGrabLedgeFromJump = false;
            _jumpBufferTimer = 0f;
            _wallRunBufferTimer = 0f;
            _climbMotion.StartJumpingToHanging(_pendingLedgeTarget);
        }

        public void HoldHanging()
        {
            _climbMotion.HoldHanging();
        }

        public bool CanStartHangHop(bool moveRight)
        {
            float horizontalInput = _input != null ? _input.Move.x : 0f;
            if (Mathf.Abs(horizontalInput) < 0.25f)
            {
                _isHangHopInputReady = true;
                return false;
            }

            if (!_isHangHopInputReady
                || !_climbMotion.IsHangPoseMatched
                || (moveRight ? horizontalInput < 0.55f : horizontalInput > -0.55f))
            {
                return false;
            }

            float lateralDistance = moveRight ? HangHopDistance : -HangHopDistance;
            return _ledgeDetector.TryFindHangHopTarget(
                _climbMotion.LedgeTarget,
                lateralDistance,
                out _pendingHangHopTarget);
        }

        public void StartHangHop(bool moveRight)
        {
            _isHangHopInputReady = false;
            _climbMotion.StartHangHop(_pendingHangHopTarget, moveRight);
        }

        private static float CalculateHangHopContactWeight(float progress)
        {
            if (progress < HangHopReleaseEnd)
            {
                return 1f - Mathf.SmoothStep(0f, 1f, progress / HangHopReleaseEnd);
            }

            return progress > HangHopRecontactStart
                ? Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(HangHopRecontactStart, HangHopRecontactEnd, progress))
                : 0f;
        }

        public bool CanStartBracedHangToCrouch()
        {
            return IsHanging
                   && _climbMotion.LedgeTarget.HasLandingPosition
                   && _jumpBufferTimer > 0f
                   && _input != null
                   && _input.Move.y > 0.5f;
        }

        public void StartBracedHangToCrouch()
        {
            _jumpBufferTimer = 0f;
            _climbMotion.StartBracedHangToCrouch();
        }

        public bool CanStartJumpFromWall()
        {
            return IsHanging && _jumpBufferTimer > 0f && (_input == null || _input.Move.y <= 0.5f);
        }

        public bool CanDropFromHanging()
        {
            return IsHanging && _input != null && (_input.Move.y < -0.5f || _input.WasSlidePressed);
        }

        public void StartJumpFromWall()
        {
            _jumpBufferTimer = 0f;
            _climbMotion.StartJumpFromWall();
        }

        public void StartCrouchedToSprint()
        {
            EnsureSubsystemsInitialized();
            Vector3 direction = _climbMotion.Direction.sqrMagnitude > 0.9f
                ? _climbMotion.Direction
                : transform.forward;
            _climbMotion.StartCrouchedToSprint(direction);
        }

        public void UpdateClimbMovement(float deltaTime)
        {
            EnsureSubsystemsInitialized();
            _climbMotion.UpdateMovement(
                deltaTime,
                ref _planarVelocity,
                ref _verticalVelocity);
        }

        public void SetClimbAnimationProgress(float normalizedTime)
        {
            _climbMotion?.SetAnimationProgress(normalizedTime);
        }

        public void StopClimbMotion()
        {
            _climbMotion?.Stop();
        }

        public bool CanStartWallJump()
        {
            return IsWallRunning && _jumpBufferTimer > 0f;
        }

        public void ApplyWallJumpImpulse()
        {
            EnsureSubsystemsInitialized();
            _obstacleJump.ResetState();
            _wallRun.ApplyWallJumpImpulse(
                _desiredDirection,
                _gravity,
                ref _planarVelocity,
                ref _verticalVelocity);

            _jumpBufferTimer = 0f;
            _wallRunBufferTimer = 0f;
            _coyoteTimer = 0f;
            Jumped?.Invoke();
        }

        public bool CanStartSlide()
        {
            return _slide != null
                   && !IsObstacleVaulting
                   && !IsWallRunning
                   && _slide.CanStartSlide(IsGrounded, PlanarSpeed, _input != null && _input.WasSlidePressed);
        }

        public void StartSlide()
        {
            EnsureSubsystemsInitialized();
            _slide.ConfigureSettings(
                _baseSlideSpeed,
                _minSlideSpeed,
                _slideFrictionRate,
                _maxSlideDuration,
                _minSlideDuration,
                _slideInflectionTime);
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _wallRunBufferTimer = 0f;
            Vector3 forward = _desiredDirection.sqrMagnitude > 0.01f ? _desiredDirection : transform.forward;
            _slide.StartSlide(forward, PlanarSpeed, _controller);
        }

        public bool UpdateSlideMovement(float deltaTime)
        {
            EnsureSubsystemsInitialized();
            return _slide.UpdateSlide(deltaTime, _controller, _desiredDirection, ref _planarVelocity, ref _verticalVelocity);
        }

        public void ApplySlideJump()
        {
            EnsureSubsystemsInitialized();
            _slide.ApplySlideJump(_controller, _jumpHeight, _gravity, ref _planarVelocity, ref _verticalVelocity);
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _wallRunBufferTimer = 0f;
            _canGrabLedgeFromJump = true;
            Jumped?.Invoke();
        }

        public void StopSlide()
        {
            _slide?.StopSlide(_controller);
        }

        public bool HasSlideGroundContact()
        {
            return _slide != null && _slide.HasGroundBeneath;
        }

        public bool HasSpaceAhead(Vector3 direction, float distance)
        {
            if (_controller == null || direction.sqrMagnitude < 0.01f)
            {
                return false;
            }

            Vector3 normalizedDirection = direction.normalized;
            Vector3 lateralRight = Vector3.Cross(Vector3.up, normalizedDirection).normalized * 0.35f;
            Vector3 origin = transform.position + Vector3.up * 0.6f;

            if (Physics.SphereCast(
                origin,
                0.35f,
                normalizedDirection,
                out RaycastHit obstacleHit,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                if (obstacleHit.collider != null && obstacleHit.collider != _controller && !obstacleHit.collider.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            int checkSteps = Mathf.Max(2, Mathf.RoundToInt(distance / 0.6f));
            for (int i = 1; i <= checkSteps; i++)
            {
                float stepDist = (i / (float)checkSteps) * distance;
                Vector3 centerCheckOrigin = transform.position + normalizedDirection * stepDist + Vector3.up * 0.6f;

                if (!Physics.Raycast(
                    centerCheckOrigin,
                    Vector3.down,
                    out RaycastHit centerHit,
                    1.4f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    return false;
                }

                if (centerHit.collider == null || centerHit.collider == _controller || centerHit.collider.transform.IsChildOf(transform))
                {
                    return false;
                }

                if (!Physics.Raycast(
                    centerCheckOrigin + lateralRight,
                    Vector3.down,
                    out RaycastHit rightHit,
                    1.4f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    return false;
                }

                if (!Physics.Raycast(
                    centerCheckOrigin - lateralRight,
                    Vector3.down,
                    out RaycastHit leftHit,
                    1.4f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    return false;
                }

                if (Mathf.Abs(rightHit.point.y - centerHit.point.y) > 0.45f || Mathf.Abs(leftHit.point.y - centerHit.point.y) > 0.45f)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateTimers(float deltaTime)
        {
            _jumpBufferTimer -= deltaTime;
            _wallRunBufferTimer -= deltaTime;
            _coyoteTimer -= deltaTime;

            if (_input.WasJumpPressed)
            {
                _jumpBufferTimer = _jumpBufferTime;
                if (!IsGrounded)
                {
                    _wallRunBufferTimer = WallRunBufferTime;
                }
            }
        }

        private void UpdateGroundState()
        {
            var isGrounded = IsGrounded;
            if (isGrounded)
            {
                _canGrabLedgeFromJump = false;
                _coyoteTimer = _coyoteTime;
                _wallRunBufferTimer = 0f;
                if (!_wasGrounded)
                {
                    LastLandingImpactSpeed = Mathf.Abs(_verticalVelocity);
                    Landed?.Invoke(LastLandingImpactSpeed);
                }

                SnapGroundedVerticalVelocity();
            }
            else
            {
                LastLandingImpactSpeed = 0f;
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
            Vector3 direction = IsWallRunning
                ? _wallRun.WallTangent
                : (IsSliding
                    ? _slide.SlideDirection
                    : (IsRolling && _groundedState != null
                        ? _groundedState.RollLandState.RollDirection
                        : (IsClimbing
                            ? _climbMotion.Direction
                            : Vector3.ProjectOnPlane(_planarVelocity, Vector3.up))));

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
            _wallRun?.ResetState();
            _climbMotion?.Stop();
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
