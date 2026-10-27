using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    [System.Serializable]
    public struct ObstacleJumpCameraFeel_y
    {
        [Range(-0.5f, 0.8f)] [SerializeField] private float _distanceOffset;
        [Range(0f, 0.5f)] [SerializeField] private float _heightOffset;
        [Range(0f, 8f)] [SerializeField] private float _fieldOfViewOffset;
        [Range(-8f, 8f)] [SerializeField] private float _pitchOffset;

        public float DistanceOffset => _distanceOffset;
        public float HeightOffset => _heightOffset;
        public float FieldOfViewOffset => _fieldOfViewOffset;
        public float PitchOffset => _pitchOffset;

        public static ObstacleJumpCameraFeel_y Create(
            float distanceOffset,
            float heightOffset,
            float fieldOfViewOffset,
            float pitchOffset)
        {
            return new ObstacleJumpCameraFeel_y
            {
                _distanceOffset = distanceOffset,
                _heightOffset = heightOffset,
                _fieldOfViewOffset = fieldOfViewOffset,
                _pitchOffset = pitchOffset,
            };
        }
    }

    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(ParkourCameraImpact_y))]
    [RequireComponent(typeof(ParkourCameraStride_y))]
    public sealed class ParkourCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private ParkourInput _input;
        [SerializeField] private ParkourMotor _motor;
        [Header("Parkour Framing")]
        [SerializeField] private Vector3 _targetOffset = new(0f, 1.25f, 0f);
        [SerializeField] private float _normalDistance = 3.15f;
        [SerializeField] private float _sprintDistance = 3.55f;
        [SerializeField] private float _shoulderOffset = 0.42f;
        [SerializeField] private float _lookAheadDistance = 0.85f;
        [SerializeField] private float _lookSensitivity = 0.12f;
        [SerializeField] private float _positionSharpness = 16f;
        [SerializeField] private float _rotationSharpness = 18f;
        [SerializeField] private float _minimumPitch = -25f;
        [SerializeField] private float _maximumPitch = 65f;
        [SerializeField] private float _normalFieldOfView = 69f;
        [SerializeField] private float _fastFieldOfView = 83f;
        [SerializeField] private float _collisionRadius = 0.2f;

        [Header("Obstacle Jump Camera Feel")]
        [SerializeField] private ObstacleJumpCameraFeel_y _jumping1CameraFeel =
            ObstacleJumpCameraFeel_y.Create(0.8f, 0.142f, 3.51f, 5.4f);
        [SerializeField] private ObstacleJumpCameraFeel_y _jumping2CameraFeel =
            ObstacleJumpCameraFeel_y.Create(0.8f, 0.154f, 3.58f, 5.3f);
        [SerializeField] private ObstacleJumpCameraFeel_y _jumping3CameraFeel =
            ObstacleJumpCameraFeel_y.Create(0.65f, 0.22f, 7f, 2.5f);
        [SerializeField] private ObstacleJumpCameraFeel_y _jumpOverCameraFeel =
            ObstacleJumpCameraFeel_y.Create(0.8f, 0.45f, 8f, 4f);
        [Min(0.01f)] [SerializeField] private float _obstacleJumpBlendTime = 0.08f;

        [Header("Motion Response")]
        [SerializeField] private float _sprintPitchOffset = -3f;
        [SerializeField] private float _landingRecoverySpeed = 6.5f;

        [Header("Slide Framing")]
        [SerializeField] private float _slideDistance = 2.15f;
        [SerializeField] private float _slideTargetOffsetY = 0.85f;
        [SerializeField] private float _slideRoll = -5.0f;
        [SerializeField] private float _slideFieldOfView = 68f;

        [Header("Wall Run Framing")]
        [SerializeField] private float _wallRunRoll = 5f;

        [Header("Ledge Framing")]
        [SerializeField] private float _ledgeDistance = 4.1f;
        [SerializeField] private float _ledgeTargetOffsetY = 1.55f;
        [SerializeField] private float _ledgeFieldOfView = 69f;
        [SerializeField] private float _ledgeMaximumOrbitAngle = 65f;
        [SerializeField] private float _ledgePositionSharpness = 8f;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
        private Camera _camera;
        private ParkourCameraImpact_y _impact;
        private ParkourCameraStride_y _stride;
        private Vector3 _smoothedPosition;
        private Quaternion _smoothedRotation;
        private float _smoothedFieldOfView;
        private float _currentRoll;
        private float _currentDistance = 3.15f;
        private float _currentTargetOffsetY = 1.25f;
        private float _landingImpulse;
        private float _obstacleJumpBlend;
        private float _obstacleJumpBlendVelocity;
        private float _pitch = 12f;
        private float _yaw;

        public void Initialize(Transform target, ParkourInput input, ParkourMotor motor)
        {
            if (_motor != null)
            {
                _motor.Landed -= HandleLanded;
            }

            _target = target;
            _input = input;
            _motor = motor;

            if (isActiveAndEnabled && _motor != null)
            {
                _motor.Landed += HandleLanded;
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _impact = GetComponent<ParkourCameraImpact_y>();
            _stride = GetComponent<ParkourCameraStride_y>();
            _smoothedPosition = transform.position;
            _smoothedRotation = transform.rotation;
            _smoothedFieldOfView = _camera.fieldOfView;
            _yaw = transform.eulerAngles.y;
            _currentTargetOffsetY = _targetOffset.y;
            _currentDistance = _normalDistance;
        }

        private void OnEnable()
        {
            if (_motor != null)
            {
                _motor.Landed -= HandleLanded;
                _motor.Landed += HandleLanded;
            }
        }

        private void OnDisable()
        {
            if (_motor != null)
            {
                _motor.Landed -= HandleLanded;
            }
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (_target == null || _input == null || _motor == null)
            {
                return;
            }

            Vector2 look = _input.Look;
            _yaw += look.x * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * _lookSensitivity, _minimumPitch, _maximumPitch);

            var speedRatio = Mathf.InverseLerp(7f, 14f, _motor.PlanarSpeed);
            _impact.UpdateImpact(Time.deltaTime);
            _stride.UpdateStride(speedRatio, Time.deltaTime);
            var isSliding = _motor.IsSliding;
            var isLedgeTraversing = _motor.IsLedgeTraversing;
            if (isLedgeTraversing)
            {
                float ledgeYaw = _motor.transform.eulerAngles.y;
                float yawOffset = Mathf.DeltaAngle(ledgeYaw, _yaw);
                _yaw = ledgeYaw + Mathf.Clamp(
                    yawOffset,
                    -_ledgeMaximumOrbitAngle,
                    _ledgeMaximumOrbitAngle);
            }

            var targetJumpWeight = _motor.IsObstacleVaulting
                ? CalculateObstacleJumpCameraWeight(
                    _motor.CurrentJumpTier,
                    _motor.ObstacleJumpProgress)
                : 0f;
            _obstacleJumpBlend = Mathf.SmoothDamp(
                _obstacleJumpBlend,
                targetJumpWeight,
                ref _obstacleJumpBlendVelocity,
                _obstacleJumpBlendTime,
                Mathf.Infinity,
                Time.deltaTime);
            ObstacleJumpCameraFeel_y jumpFeel = GetObstacleJumpCameraFeel();
            var framedPitch = _pitch
                              + _sprintPitchOffset * speedRatio
                              + jumpFeel.PitchOffset * _obstacleJumpBlend
                              + _impact.PitchKick;
            Quaternion orbitRotation = Quaternion.Euler(framedPitch, _yaw, 0f);
            var targetDistance = isLedgeTraversing
                ? _ledgeDistance
                : isSliding
                    ? _slideDistance
                    : Mathf.Lerp(_normalDistance, _sprintDistance, speedRatio)
                      + jumpFeel.DistanceOffset * _obstacleJumpBlend;

            var targetOffsetY = isLedgeTraversing
                ? _ledgeTargetOffsetY
                : isSliding
                    ? _slideTargetOffsetY
                    : _targetOffset.y + jumpFeel.HeightOffset * _obstacleJumpBlend;
            var targetRoll = (isSliding ? _slideRoll : 0f)
                             - _motor.WallSide * _wallRunRoll
                             + _impact.RollKick;

            _currentDistance = Mathf.Lerp(_currentDistance, targetDistance, 1f - Mathf.Exp(-8f * Time.deltaTime));
            _currentTargetOffsetY = Mathf.Lerp(_currentTargetOffsetY, targetOffsetY, 1f - Mathf.Exp(-8f * Time.deltaTime));
            _currentRoll = Mathf.Lerp(_currentRoll, targetRoll, 1f - Mathf.Exp(-8f * Time.deltaTime));

            Vector3 pivotPoint = _target.position
                                 + new Vector3(_targetOffset.x, _currentTargetOffsetY, _targetOffset.z);
            Vector3 shoulderOffset = orbitRotation * Vector3.right * _shoulderOffset;
            Vector3 desiredPosition = pivotPoint
                                      + shoulderOffset
                                      - orbitRotation * Vector3.forward * (_currentDistance + _impact.DistanceKick);
            desiredPosition = ResolveCameraCollision(pivotPoint, desiredPosition);

            _landingImpulse = Mathf.MoveTowards(
                _landingImpulse,
                0f,
                _landingRecoverySpeed * Time.deltaTime);
            Vector3 impactOffset = Vector3.down * _landingImpulse * _landingImpulse * 0.1f;
            float positionSharpness = isLedgeTraversing
                ? _ledgePositionSharpness
                : _positionSharpness;
            _smoothedPosition = Vector3.Lerp(
                _smoothedPosition,
                desiredPosition + impactOffset,
                1f - Mathf.Exp(-positionSharpness * Time.deltaTime));
            // 보폭 바운스는 평활을 거치면 발 접지와 위상이 어긋나므로 평활 뒤에 더한다.
            transform.position = _smoothedPosition + Vector3.up * _stride.HeightOffset;

            Quaternion desiredRotation = Quaternion.Euler(framedPitch, _yaw, _currentRoll);
            _smoothedRotation = Quaternion.Slerp(
                _smoothedRotation,
                desiredRotation,
                1f - Mathf.Exp(-_rotationSharpness * Time.deltaTime));
            transform.rotation = _smoothedRotation
                                 * Quaternion.Euler(_impact.ShakeEuler + Vector3.forward * _stride.RollOffset);

            var targetFieldOfView = isLedgeTraversing
                ? _ledgeFieldOfView
                : isSliding
                    ? _slideFieldOfView
                    : Mathf.Lerp(_normalFieldOfView, _fastFieldOfView, speedRatio)
                      + jumpFeel.FieldOfViewOffset * _obstacleJumpBlend;

            _smoothedFieldOfView = Mathf.Lerp(
                _smoothedFieldOfView,
                targetFieldOfView,
                1f - Mathf.Exp(-8f * Time.deltaTime));
            _camera.fieldOfView = _smoothedFieldOfView + _impact.FieldOfViewKick;
        }

        private Vector3 ResolveCameraCollision(Vector3 pivotPoint, Vector3 desiredPosition)
        {
            Vector3 direction = desiredPosition - pivotPoint;
            var distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return desiredPosition;
            }

            Vector3 normalizedDirection = direction / distance;
            var hitCount = Physics.SphereCastNonAlloc(
                pivotPoint,
                _collisionRadius,
                normalizedDirection,
                _hitBuffer,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            var closestDistance = distance;
            var hasObstacleHit = false;
            for (var index = 0; index < hitCount; index++)
            {
                Transform hitTransform = _hitBuffer[index].transform;
                if (hitTransform == _target || hitTransform.IsChildOf(_target))
                {
                    continue;
                }

                if (_motor.IsObstacleVaulting
                    && _hitBuffer[index].collider == _motor.ActiveVaultObstacle)
                {
                    continue;
                }

                if (_hitBuffer[index].distance < closestDistance)
                {
                    closestDistance = _hitBuffer[index].distance;
                    hasObstacleHit = true;
                }
            }

            if (hasObstacleHit)
            {
                return pivotPoint + normalizedDirection * Mathf.Max(closestDistance - _collisionRadius, 0.5f);
            }

            return desiredPosition;
        }

        private ObstacleJumpCameraFeel_y GetObstacleJumpCameraFeel()
        {
            return _motor.CurrentJumpTier switch
            {
                1 => _jumping1CameraFeel,
                2 => _jumping2CameraFeel,
                3 => _jumping3CameraFeel,
                4 => _jumpOverCameraFeel,
                _ => default,
            };
        }

        private static float CalculateObstacleJumpCameraWeight(int jumpTier, float progress)
        {
            return jumpTier switch
            {
                1 => CalculatePulse(progress, 0.03f, 0.2f, 0.58f),
                2 => CalculatePulse(progress, 0.06f, 0.32f, 0.72f),
                3 => CalculatePlateau(progress, 0.03f, 0.2f, 0.7f, 0.98f),
                4 => CalculatePlateau(progress, 0.02f, 0.2f, 0.68f, 0.96f),
                _ => 0f,
            };
        }

        private static float CalculatePulse(
            float progress,
            float riseStart,
            float peak,
            float end)
        {
            if (progress <= peak)
            {
                return SmoothProgress(Mathf.InverseLerp(riseStart, peak, progress));
            }

            return 1f - SmoothProgress(Mathf.InverseLerp(peak, end, progress));
        }

        private static float CalculatePlateau(
            float progress,
            float riseStart,
            float fullStart,
            float fullEnd,
            float end)
        {
            if (progress < fullStart)
            {
                return SmoothProgress(Mathf.InverseLerp(riseStart, fullStart, progress));
            }

            if (progress <= fullEnd)
            {
                return 1f;
            }

            return 1f - SmoothProgress(Mathf.InverseLerp(fullEnd, end, progress));
        }

        private static float SmoothProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            return progress * progress * (3f - 2f * progress);
        }

        private void HandleLanded(float impactSpeed)
        {
            _landingImpulse = Mathf.Clamp01(Mathf.InverseLerp(5f, 20f, impactSpeed));
        }
    }
}
