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
        [SerializeField] private float _sprintStopDeceleration = 13.333f;
        [SerializeField] private float _rotationSharpness = 16f;

        [Header("Sprint Start")]
        [SerializeField] private float _sprintStartDuration = 0.86f;
        [SerializeField] private AnimationCurve _sprintStartSpeedCurve = CreateSprintStartSpeedCurve();

        [Header("Air Movement")]
        [SerializeField] private float _airAcceleration = 12f;
        [SerializeField] private float _airControl = 0.55f;
        [SerializeField] private float _jumpHeight = 2.3f;
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
        private Vector3 _planarVelocity;
        private Vector3 _spawnPosition;
        private Vector3 _wallNormal;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _verticalVelocity;
        private float _wallRunTimer;
        private float _rollTimer;
        private float _sprintStartElapsed;
        private bool _hadMovementInput;
        private bool _isStartingSprint;
        private bool _isStoppingSprint;
        private bool _wasSprinting;
        private bool _wasGrounded;

        public event Action<float> Landed;
        public event Action HardLanded;
        public event Action Jumped;
        public event Action Rolled;

        public float PlanarSpeed => _planarVelocity.magnitude;
        public Vector3 PlanarVelocity => _planarVelocity;
        public float VerticalSpeed => _verticalVelocity;
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsWallRunning { get; private set; }
        public bool IsRolling => _rollTimer > 0f;
        public bool HasMovementInput => _input != null && _input.Move.sqrMagnitude > 0.01f;
        public bool IsSprinting => _input != null
                                   && _input.IsSprinting
                                   && _input.Move.sqrMagnitude > 0.01f;
        public Vector3 WallNormal => _wallNormal;

        public void Initialize(ParkourInput input, Transform cameraTransform)
        {
            _input = input;
            _cameraTransform = cameraTransform;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _spawnPosition = transform.position;

            if (_sprintStartSpeedCurve == null || _sprintStartSpeedCurve.length == 0)
            {
                _sprintStartSpeedCurve = CreateSprintStartSpeedCurve();
            }
        }

        private void Update()
        {
            if (_input == null || _cameraTransform == null)
            {
                return;
            }

            UpdateTimers();
            UpdateGroundState();

            Vector3 desiredDirection = CalculateDesiredDirection(_input.Move);
            if (TryStartRoll(desiredDirection) || IsRolling)
            {
                UpdateRoll();
            }
            else if (CanStartOrContinueWallRun(desiredDirection))
            {
                UpdateWallRun(desiredDirection);
            }
            else
            {
                StopWallRun();
                UpdateFreeMovement(desiredDirection);
            }

            Vector3 motion = _planarVelocity + Vector3.up * _verticalVelocity;
            CollisionFlags collisionFlags = _controller.Move(motion * Time.deltaTime);
            ResolveCollision(collisionFlags);
            RotateTowardsMotion();

            if (transform.position.y < _respawnHeight)
            {
                Respawn();
            }
        }

        private void UpdateTimers()
        {
            _jumpBufferTimer -= Time.deltaTime;
            _coyoteTimer -= Time.deltaTime;
            _rollTimer -= Time.deltaTime;

            if (_input.WasJumpPressed)
            {
                _jumpBufferTimer = _jumpBufferTime;
            }
        }

        private void UpdateGroundState()
        {
            bool isGrounded = _controller.isGrounded;
            if (isGrounded)
            {
                _coyoteTimer = _coyoteTime;
                if (!_wasGrounded)
                {
                    float impactSpeed = Mathf.Abs(_verticalVelocity);
                    Landed?.Invoke(impactSpeed);
                    if (impactSpeed >= _hardLandingRollSpeed && _planarVelocity.magnitude >= _wallRunMinimumSpeed)
                    {
                        BeginRoll(_planarVelocity.normalized);
                        HardLanded?.Invoke();
                    }
                }

                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
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

        private void UpdateFreeMovement(Vector3 desiredDirection)
        {
            bool grounded = _controller.isGrounded;
            bool hasMovementInput = desiredDirection.sqrMagnitude > 0.01f;
            bool isSprinting = _input.IsSprinting && hasMovementInput;

            if (hasMovementInput && !_hadMovementInput)
            {
                StartSprintAcceleration();
            }

            if (!hasMovementInput)
            {
                StopSprintAcceleration();
            }

            if (_wasSprinting && !isSprinting && !hasMovementInput)
            {
                _isStoppingSprint = true;
            }

            if (hasMovementInput)
            {
                _isStoppingSprint = false;
            }

            float targetSpeed = _input.IsSprinting ? _sprintSpeed : _runSpeed;
            Vector3 targetVelocity = desiredDirection * targetSpeed;

            if (grounded && hasMovementInput && _isStartingSprint)
            {
                UpdateSprintAcceleration(desiredDirection, targetSpeed);
            }
            else
            {
                float acceleration;
                if (grounded)
                {
                    acceleration = hasMovementInput
                        ? _groundAcceleration
                        : _isStoppingSprint
                            ? _sprintStopDeceleration
                            : _groundDeceleration;
                }
                else
                {
                    targetVelocity = Vector3.Lerp(_planarVelocity, targetVelocity, _airControl);
                    acceleration = _airAcceleration;
                }

                _planarVelocity = Vector3.MoveTowards(
                    _planarVelocity,
                    targetVelocity,
                    acceleration * Time.deltaTime);
            }

            if (!hasMovementInput && _planarVelocity.sqrMagnitude <= 0.0001f)
            {
                _isStoppingSprint = false;
            }

            _wasSprinting = isSprinting;
            _hadMovementInput = hasMovementInput;

            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
                Jumped?.Invoke();
            }
            else
            {
                float gravityMultiplier = _verticalVelocity < 0f ? _fallGravityMultiplier : 1f;
                _verticalVelocity += _gravity * gravityMultiplier * Time.deltaTime;
            }
        }

        private void StartSprintAcceleration()
        {
            _sprintStartElapsed = 0f;
            _isStartingSprint = true;
        }

        private void StopSprintAcceleration()
        {
            _sprintStartElapsed = 0f;
            _isStartingSprint = false;
        }

        private void UpdateSprintAcceleration(Vector3 desiredDirection, float targetSpeed)
        {
            _sprintStartElapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(_sprintStartElapsed / Mathf.Max(_sprintStartDuration, 0.01f));
            float speedRatio = Mathf.Clamp01(_sprintStartSpeedCurve.Evaluate(normalizedTime));
            _planarVelocity = desiredDirection * (targetSpeed * speedRatio);

            if (normalizedTime >= 1f)
            {
                StopSprintAcceleration();
            }
        }

        private static AnimationCurve CreateSprintStartSpeedCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.1163f, 0f),
                new Keyframe(0.1744f, 0.1f),
                new Keyframe(0.4651f, 0.35f),
                new Keyframe(0.6977f, 0.54f),
                new Keyframe(0.9302f, 0.705f),
                new Keyframe(1f, 1f));

            Keyframe[] keys = curve.keys;
            for (int index = 0; index < keys.Length; index++)
            {
                float inTangent = index == 0 ? 0f : CalculateCurveSegmentSlope(keys[index - 1], keys[index]);
                float outTangent = index == keys.Length - 1 ? 0f : CalculateCurveSegmentSlope(keys[index], keys[index + 1]);
                keys[index] = new Keyframe(keys[index].time, keys[index].value, inTangent, outTangent);
            }

            curve.keys = keys;
            return curve;
        }

        private static float CalculateCurveSegmentSlope(Keyframe start, Keyframe end)
        {
            return (end.value - start.value) / (end.time - start.time);
        }

        private bool CanStartOrContinueWallRun(Vector3 desiredDirection)
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
            bool hitLeft = Physics.Raycast(
                origin,
                -transform.right,
                out RaycastHit leftHit,
                _wallDetectionDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            bool hitRight = Physics.Raycast(
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

            if (Vector3.Dot(alongWall, desiredDirection) < 0f)
            {
                alongWall = -alongWall;
            }

            _wallNormal = wallHit.normal;
            return desiredDirection.sqrMagnitude > 0.01f && Vector3.Dot(alongWall, desiredDirection) > 0.15f;
        }

        private static bool IsRunnableWall(Vector3 normal)
        {
            return Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.2f;
        }

        private void UpdateWallRun(Vector3 desiredDirection)
        {
            IsWallRunning = true;
            _wallRunTimer += Time.deltaTime;

            Vector3 alongWall = Vector3.ProjectOnPlane(_planarVelocity, _wallNormal).normalized;
            if (Vector3.Dot(alongWall, desiredDirection) < 0f)
            {
                alongWall = -alongWall;
            }

            float maintainedSpeed = Mathf.Max(_planarVelocity.magnitude, _wallRunMinimumSpeed);
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                alongWall * maintainedSpeed,
                _wallGripAcceleration * Time.deltaTime);
            _verticalVelocity = Mathf.MoveTowards(
                _verticalVelocity,
                _wallRunGravity,
                _wallGripAcceleration * Time.deltaTime);

            if (_jumpBufferTimer > 0f)
            {
                _planarVelocity = alongWall * maintainedSpeed + _wallNormal * _wallJumpAwaySpeed;
                _verticalVelocity = _wallJumpUpSpeed;
                _jumpBufferTimer = 0f;
                _wallRunTimer = _wallRunMaximumDuration;
                Jumped?.Invoke();
                StopWallRun();
            }
        }

        private bool TryStartRoll(Vector3 desiredDirection)
        {
            if (!_input.WasRollPressed || !_controller.isGrounded || desiredDirection.sqrMagnitude < 0.01f)
            {
                return false;
            }

            BeginRoll(desiredDirection);
            Rolled?.Invoke();
            return true;
        }

        private void BeginRoll(Vector3 direction)
        {
            float rollSpeed = Mathf.Max(_planarVelocity.magnitude + _rollBoost, _rollMinimumSpeed);
            _planarVelocity = direction * rollSpeed;
            _rollTimer = _rollDuration;
        }

        private void UpdateRoll()
        {
            _verticalVelocity = -2f;
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                transform.forward * _rollMinimumSpeed,
                _groundDeceleration * 0.35f * Time.deltaTime);
        }

        private void StopWallRun()
        {
            if (IsWallRunning)
            {
                IsWallRunning = false;
                _wallNormal = Vector3.zero;
            }

            if (_controller.isGrounded)
            {
                _wallRunTimer = 0f;
            }
        }

        private void ResolveCollision(CollisionFlags collisionFlags)
        {
            if ((collisionFlags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }
        }

        private void RotateTowardsMotion()
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
                1f - Mathf.Exp(-_rotationSharpness * Time.deltaTime));
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
            _hadMovementInput = false;
            StopSprintAcceleration();
            StopWallRun();
        }
    }
}
