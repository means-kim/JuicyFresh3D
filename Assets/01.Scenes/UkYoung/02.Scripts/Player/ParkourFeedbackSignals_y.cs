using System;
using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ParkourFeedbackSignals_y : MonoBehaviour
    {
        private const float LeftFootContactPhase = 0.31f;
        private const float RightFootContactPhase = 0.81f;
        private const float MinimumFootstepSpeed = 2.5f;

        [SerializeField] private ParkourMotor _motor;
        [SerializeField] private ParkourAnimationDriver _animationDriver;
        [SerializeField] private CharacterController _controller;
        [SerializeField] private LayerMask _surfaceMask = -1;

        private readonly RaycastHit[] _surfaceHits = new RaycastHit[8];
        private float _previousSprintPhase;
        private float _previousVaultContactWeight;
        private float _previousLedgeContactWeight;
        private bool _hasSprintPhase;

        public event Action<Vector3, Vector3, float> FootContact;
        public event Action<Vector3, Vector3, float> JumpTakeoff;
        public event Action<Vector3, Vector3, float> LandingContact;
        public event Action<Vector3, Vector3, float> HandContact;

        private void Awake()
        {
            _motor ??= GetComponent<ParkourMotor>();
            _animationDriver ??= GetComponent<ParkourAnimationDriver>();
            _controller ??= GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (_motor == null)
            {
                return;
            }

            _motor.Jumped += HandleJumped;
            _motor.Landed += HandleLanded;
        }

        private void OnDisable()
        {
            if (_motor == null)
            {
                return;
            }

            _motor.Jumped -= HandleJumped;
            _motor.Landed -= HandleLanded;
        }

        private void Update()
        {
            UpdateFootContacts();
            UpdateHandContacts();
        }

        private void UpdateFootContacts()
        {
            if (_motor == null || _animationDriver == null || !_motor.IsGrounded
                || _motor.PlanarSpeed < MinimumFootstepSpeed
                || !_animationDriver.TryGetSprintCyclePhase(out float phase))
            {
                _hasSprintPhase = false;
                return;
            }

            if (_hasSprintPhase
                && (DidCrossPhase(_previousSprintPhase, phase, LeftFootContactPhase)
                    || DidCrossPhase(_previousSprintPhase, phase, RightFootContactPhase)))
            {
                GetGroundContact(out Vector3 point, out Vector3 normal);
                float intensity = Mathf.InverseLerp(MinimumFootstepSpeed, _motor.RunSpeed * 1.75f,
                    _motor.PlanarSpeed);
                FootContact?.Invoke(point, normal, intensity);
            }

            _previousSprintPhase = phase;
            _hasSprintPhase = true;
        }

        private void UpdateHandContacts()
        {
            if (_motor == null)
            {
                return;
            }

            float vaultWeight = _motor.VaultHandContactWeight;
            if (_previousVaultContactWeight < 0.55f && vaultWeight >= 0.55f)
            {
                HandContact?.Invoke(_motor.VaultContactPoint, Vector3.up, 0.45f);
            }

            float ledgeWeight = _motor.LedgeHandContactWeight;
            if (_previousLedgeContactWeight < 0.85f && ledgeWeight >= 0.85f)
            {
                Vector3 point = Vector3.Lerp(
                    _motor.LedgeLeftHandContactPoint,
                    _motor.LedgeRightHandContactPoint,
                    0.5f);
                HandContact?.Invoke(point, -_motor.LedgeDirection, 0.7f);
            }

            _previousVaultContactWeight = vaultWeight;
            _previousLedgeContactWeight = ledgeWeight;
        }

        private void HandleJumped()
        {
            GetGroundContact(out Vector3 point, out Vector3 normal);
            JumpTakeoff?.Invoke(point, normal, 0.55f);
        }

        private void HandleLanded(float impactSpeed)
        {
            GetGroundContact(out Vector3 point, out Vector3 normal);
            float intensity = Mathf.InverseLerp(
                _motor.LightLandingSpeedThreshold,
                _motor.HardLandingSpeedThreshold,
                impactSpeed);
            LandingContact?.Invoke(point, normal, intensity);
        }

        public void GetGroundContact(out Vector3 point, out Vector3 normal)
        {
            Bounds bounds = _controller.bounds;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.3f, bounds.center.z);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _surfaceHits,
                0.8f,
                _surfaceMask,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.MaxValue;
            int closestIndex = -1;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _surfaceHits[index];
                if (hit.collider == _controller || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestIndex = index;
            }

            if (closestIndex >= 0)
            {
                point = _surfaceHits[closestIndex].point;
                normal = _surfaceHits[closestIndex].normal;
                return;
            }

            point = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            normal = Vector3.up;
        }

        private static bool DidCrossPhase(float previous, float current, float target)
        {
            return current >= previous
                ? previous < target && current >= target
                : previous < target || current >= target;
        }
    }
}
