using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    [RequireComponent(typeof(Animator))]
    public sealed class ParkourHandContact_y : MonoBehaviour
    {
        private const float VaultHandSurfaceOffset = 0.015f;

        private Animator _animator;
        private ParkourMotor _motor;
        private Transform _leftHand;
        private Transform _rightHand;

        public void Initialize(Animator animator, ParkourMotor motor)
        {
            _animator = animator;
            _motor = motor;
            CacheHands();
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            CacheHands();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            ResetHandWeights();

            if (layerIndex != 0)
            {
                return;
            }

            if (_animator == null || !_animator.isHuman || _motor == null)
            {
                return;
            }

            AlignHangingBodyDirection();

            if (_motor.IsLedgeTraversing && ApplyLedgeHandContact())
            {
                return;
            }

            if (!_motor.IsObstacleVaulting)
            {
                return;
            }

            var contactWeight = _motor.VaultHandContactWeight;
            if (contactWeight <= 0f)
            {
                return;
            }

            CacheHands();
            var useLeftHand = _leftHand != null;
            var useRightHand = _rightHand != null
                               && (_motor.CurrentJumpTier == 2 || _motor.CurrentJumpTier == 4);
            if (!useLeftHand && !useRightHand)
            {
                return;
            }

            Vector3 contactPoint = _motor.VaultContactPoint + Vector3.up * VaultHandSurfaceOffset;
            Vector3 vaultDirection = Vector3.ProjectOnPlane(_motor.VaultDirection, Vector3.up).normalized;
            if (vaultDirection.sqrMagnitude < 0.01f)
            {
                return;
            }

            if (useLeftHand)
            {
                ApplyHandContact(
                    AvatarIKGoal.LeftHand,
                    _leftHand.position,
                    contactPoint,
                    vaultDirection,
                    contactWeight);
            }

            if (useRightHand)
            {
                ApplyHandContact(
                    AvatarIKGoal.RightHand,
                    _rightHand.position,
                    contactPoint,
                    vaultDirection,
                    contactWeight);
            }
        }

        private void AlignHangingBodyDirection()
        {
            if (!_motor.ShouldAlignHangingBodyDirection)
            {
                return;
            }

            Vector3 desiredForward = Vector3.ProjectOnPlane(
                _motor.LedgeDirection,
                Vector3.up).normalized;
            Quaternion bodyRotation = _animator.bodyRotation;
            Vector3 currentForward = Vector3.ProjectOnPlane(
                bodyRotation * Vector3.forward,
                Vector3.up).normalized;
            if (desiredForward.sqrMagnitude < 0.9f || currentForward.sqrMagnitude < 0.9f)
            {
                return;
            }

            float yawDifference = Vector3.SignedAngle(
                currentForward,
                desiredForward,
                Vector3.up);
            _animator.bodyRotation = Quaternion.AngleAxis(yawDifference, Vector3.up)
                                     * bodyRotation;
        }

        private bool ApplyLedgeHandContact()
        {
            float contactWeight = _motor.LedgeHandContactWeight;
            if (contactWeight <= 0f)
            {
                return false;
            }

            CacheHands();
            Vector3 ledgeDirection = Vector3.ProjectOnPlane(_motor.LedgeDirection, Vector3.up).normalized;
            if (ledgeDirection.sqrMagnitude < 0.9f)
            {
                return false;
            }

            if (_leftHand != null)
            {
                ApplyLedgeHandContact(
                    AvatarIKGoal.LeftHand,
                    _leftHand,
                    _motor.LedgeLeftHandContactPoint,
                    ledgeDirection,
                    contactWeight);
            }

            if (_rightHand != null)
            {
                ApplyLedgeHandContact(
                    AvatarIKGoal.RightHand,
                    _rightHand,
                    _motor.LedgeRightHandContactPoint,
                    ledgeDirection,
                    contactWeight);
            }

            return _leftHand != null || _rightHand != null;
        }

        private void CacheHands()
        {
            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            _leftHand ??= _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightHand ??= _animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void ApplyLedgeHandContact(
            AvatarIKGoal handGoal,
            Transform hand,
            Vector3 contactPoint,
            Vector3 ledgeDirection,
            float contactWeight)
        {
            float forwardDistance = Vector3.Dot(hand.position - contactPoint, ledgeDirection);
            Vector3 targetHandPosition = hand.position - ledgeDirection * forwardDistance;
            targetHandPosition.y = contactPoint.y;

            _animator.SetIKPositionWeight(handGoal, contactWeight);
            _animator.SetIKRotationWeight(handGoal, 0f);
            _animator.SetIKPosition(handGoal, targetHandPosition);
        }

        private void ApplyHandContact(
            AvatarIKGoal handGoal,
            Vector3 handPosition,
            Vector3 contactPoint,
            Vector3 vaultDirection,
            float contactWeight)
        {
            var forwardDistance = Vector3.Dot(handPosition - contactPoint, vaultDirection);
            Vector3 targetPosition = handPosition - vaultDirection * forwardDistance;
            targetPosition.y = contactPoint.y;

            _animator.SetIKPositionWeight(handGoal, contactWeight);
            _animator.SetIKRotationWeight(handGoal, 0f);
            _animator.SetIKPosition(handGoal, targetPosition);
        }

        private void ResetHandWeights()
        {
            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        }
    }
}
