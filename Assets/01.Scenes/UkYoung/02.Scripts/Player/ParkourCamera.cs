using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    [RequireComponent(typeof(Camera))]
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

        [Header("Motion Response")]
        [SerializeField] private float _sprintPitchOffset = -3f;
        [SerializeField] private float _landingRecoverySpeed = 6.5f;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
        private Camera _camera;
        private float _landingImpulse;
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
            _yaw = transform.eulerAngles.y;
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
            var framedPitch = _pitch + _sprintPitchOffset * speedRatio;
            Quaternion orbitRotation = Quaternion.Euler(framedPitch, _yaw, 0f);

            Vector3 pivotPoint = _target.position + _targetOffset;
            var distance = Mathf.Lerp(_normalDistance, _sprintDistance, speedRatio);
            Vector3 shoulderOffset = orbitRotation * Vector3.right * _shoulderOffset;
            Vector3 desiredPosition = pivotPoint
                                      + shoulderOffset
                                      - orbitRotation * Vector3.forward * distance;
            desiredPosition = ResolveCameraCollision(pivotPoint, desiredPosition);

            _landingImpulse = Mathf.MoveTowards(
                _landingImpulse,
                0f,
                _landingRecoverySpeed * Time.deltaTime);
            Vector3 impactOffset = Vector3.down * _landingImpulse * _landingImpulse * 0.1f;
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition + impactOffset,
                1f - Mathf.Exp(-_positionSharpness * Time.deltaTime));

            Quaternion desiredRotation = Quaternion.Euler(framedPitch, _yaw, 0f);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                1f - Mathf.Exp(-_rotationSharpness * Time.deltaTime));

            _camera.fieldOfView = Mathf.Lerp(
                _camera.fieldOfView,
                Mathf.Lerp(_normalFieldOfView, _fastFieldOfView, speedRatio),
                1f - Mathf.Exp(-7f * Time.deltaTime));
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

        private void HandleLanded(float impactSpeed)
        {
            _landingImpulse = Mathf.Clamp01(Mathf.InverseLerp(5f, 20f, impactSpeed));
        }
    }
}
