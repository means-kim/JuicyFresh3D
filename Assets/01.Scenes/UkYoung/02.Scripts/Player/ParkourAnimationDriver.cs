using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourAnimationDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int JumpId = Animator.StringToHash("Jump");
        private static readonly int JumpTierId = Animator.StringToHash("JumpTier");
        private static readonly int MovingId = Animator.StringToHash("Moving");
        private static readonly int SprintingId = Animator.StringToHash("Sprinting");
        private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        private static readonly int WallRunningId = Animator.StringToHash("WallRunning");
        private static readonly int WallSideId = Animator.StringToHash("WallSide");
        private static readonly int SlidingId = Animator.StringToHash("Sliding");
        private static readonly int SlideStateId = Animator.StringToHash("Slide");
        private static readonly int Jumping1StateId = Animator.StringToHash("Base Layer.Jumping 1");
        private static readonly int Jumping2StateId = Animator.StringToHash("Base Layer.Jumping 2");
        private static readonly int Jumping3StateId = Animator.StringToHash("Base Layer.Jumping 3");
        private static readonly int JumpOver1StateId = Animator.StringToHash("Base Layer.Jump Over 1");
        private static readonly int SprintWallClimbStateId =
            Animator.StringToHash("Base Layer.Sprint Wall Climb");
        private static readonly int CrouchedToSprintStateId =
            Animator.StringToHash("Base Layer.Crouched To Sprint");
        private static readonly int SprintStateId = Animator.StringToHash("Base Layer.Sprint");
        private static readonly int IdleStateId = Animator.StringToHash("Base Layer.Idle");
        private static readonly int RunToStopStateId = Animator.StringToHash("Base Layer.Run To Stop");
        private static readonly int JumpingToHangingStateId =
            Animator.StringToHash("Base Layer.Jumping To Hanging");
        private static readonly int HangingIdleStateId =
            Animator.StringToHash("Base Layer.Hanging Idle");
        private static readonly int BracedHangHopLeftStateId =
            Animator.StringToHash("Base Layer.Braced Hang Hop Left");
        private static readonly int BracedHangHopRightStateId =
            Animator.StringToHash("Base Layer.Braced Hang Hop Right");
        private static readonly int BracedHangToCrouchStateId =
            Animator.StringToHash("Base Layer.Braced Hang To Crouch");
        private static readonly int JumpFromWallStateId =
            Animator.StringToHash("Base Layer.Jump From Wall");
        private static readonly int LandStateId = Animator.StringToHash("Base Layer.Land");
        private static readonly int RollStateId = Animator.StringToHash("Base Layer.Roll");

        [SerializeField] private Animator _animator;
        [SerializeField] private ParkourMotor _motor;

        private Transform _hipsTransform;
        private Vector3 _visualBaseLocalPosition;
        private bool _hasVisualBaseLocalPosition;
        private bool _wasReportingObstacleJump;

        public void Initialize(Animator animator, ParkourMotor motor)
        {
            Unsubscribe();
            _animator = animator;
            _motor = motor;
            CacheVisualBasePosition();
            if (_animator != null && _animator.isHuman)
            {
                _hipsTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);
            }
            InitializeHandContact();
            Subscribe();
        }

        private void OnEnable()
        {
            InitializeHandContact();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_animator != null)
            {
                _animator.speed = 1f;
                if (_hasVisualBaseLocalPosition)
                {
                    _animator.transform.localPosition = _visualBaseLocalPosition;
                }
            }
        }

        private void Update()
        {
            if (_animator == null || _motor == null)
            {
                return;
            }

            var isGrounded = _motor.CurrentRootStateType == ParkourRootStateType_y.Grounded
                             && !_motor.IsObstacleVaulting;

            _animator.SetFloat(SpeedId, _motor.PlanarSpeed, 0.12f, Time.deltaTime);
            _animator.SetFloat(VerticalSpeedId, _motor.VerticalSpeed);
            _animator.SetBool(GroundedId, isGrounded);
            _animator.SetBool(MovingId, _motor.HasMovementInput);
            _animator.SetBool(SprintingId, _motor.IsSprinting);
            _animator.SetBool(WallRunningId, _motor.IsWallRunning);
            _animator.SetInteger(WallSideId, _motor.WallSide);
            _animator.SetBool(SlidingId, _motor.IsSliding);
            UpdateAnimatorSpeed();
        }

        private void LateUpdate()
        {
            UpdateClimbAnimationProgress();
            UpdateVisualRootCompensation();

            if (_animator == null || _motor == null || !_motor.IsObstacleVaulting)
            {
                _wasReportingObstacleJump = false;
                return;
            }

            var targetStateId = GetObstacleJumpStateId(_motor.CurrentJumpTier);
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo nextStateInfo = _animator.GetNextAnimatorStateInfo(0);
                if (nextStateInfo.fullPathHash == targetStateId)
                {
                    stateInfo = nextStateInfo;
                }
            }

            if (stateInfo.fullPathHash == targetStateId)
            {
                _motor.SetObstacleVaultAnimationProgress(stateInfo.normalizedTime);
                _wasReportingObstacleJump = true;
            }
            else if (_wasReportingObstacleJump)
            {
                _motor.SetObstacleVaultAnimationProgress(1f);
                _wasReportingObstacleJump = false;
            }
        }

        private void UpdateActionAnimation(
            ParkourSubStateType_y previousSubStateType,
            ParkourSubStateType_y currentSubStateType)
        {
            if (currentSubStateType == ParkourSubStateType_y.SprintWallClimb)
            {
                _animator.CrossFadeInFixedTime(SprintWallClimbStateId, 0.06f, 0, 0f);
            }
            else if (currentSubStateType == ParkourSubStateType_y.CrouchedToSprint)
            {
                _animator.CrossFadeInFixedTime(CrouchedToSprintStateId, 0.16f, 0, 0f);
            }
            else if (previousSubStateType == ParkourSubStateType_y.CrouchedToSprint
                     && currentSubStateType == ParkourSubStateType_y.Sprint)
            {
                _animator.Play(SprintStateId, 0, 0f);
            }
            else if (previousSubStateType == ParkourSubStateType_y.CrouchedToSprint
                     && currentSubStateType == ParkourSubStateType_y.SprintStop)
            {
                _animator.CrossFadeInFixedTime(RunToStopStateId, 0.05f, 0, 0f);
            }
            else if (currentSubStateType == ParkourSubStateType_y.JumpingToHanging)
            {
                _animator.CrossFadeInFixedTime(JumpingToHangingStateId, 0.06f, 0, 0f);
            }
            else if (currentSubStateType == ParkourSubStateType_y.HangingIdle)
            {
                _animator.CrossFadeInFixedTime(HangingIdleStateId, 0.16f, 0, 0f);
            }
            else if (currentSubStateType == ParkourSubStateType_y.BracedHangHopLeft)
            {
                _animator.Play(BracedHangHopLeftStateId, 0, 0f);
            }
            else if (currentSubStateType == ParkourSubStateType_y.BracedHangHopRight)
            {
                _animator.Play(BracedHangHopRightStateId, 0, 0f);
            }
            else if (currentSubStateType == ParkourSubStateType_y.BracedHangToCrouch)
            {
                _animator.CrossFadeInFixedTime(BracedHangToCrouchStateId, 0.12f, 0, 0f);
            }
            else if (currentSubStateType == ParkourSubStateType_y.JumpFromWall)
            {
                _animator.CrossFadeInFixedTime(JumpFromWallStateId, 0.05f, 0, 0f);
            }
            else if (currentSubStateType == ParkourSubStateType_y.Land)
            {
                _animator.CrossFadeInFixedTime(LandStateId, 0.08f, 0, 0.28f);
            }
            else if (currentSubStateType == ParkourSubStateType_y.Roll)
            {
                _animator.CrossFadeInFixedTime(RollStateId, 0.06f, 0, 0.23f);
            }
            else if (previousSubStateType == ParkourSubStateType_y.Roll
                     && currentSubStateType == ParkourSubStateType_y.Sprint)
            {
                _animator.CrossFadeInFixedTime(SprintStateId, 0.06f, 0, 0f);
            }
            else if (previousSubStateType == ParkourSubStateType_y.Roll
                     && currentSubStateType == ParkourSubStateType_y.Idle)
            {
                _animator.CrossFadeInFixedTime(RunToStopStateId, 0.10f, 0, 0f);
            }
            else if (previousSubStateType == ParkourSubStateType_y.Land
                     && (currentSubStateType == ParkourSubStateType_y.Sprint || currentSubStateType == ParkourSubStateType_y.SprintStart))
            {
                _animator.CrossFadeInFixedTime(SprintStateId, 0.08f, 0, 0f);
            }
            else if (previousSubStateType == ParkourSubStateType_y.Land
                     && currentSubStateType == ParkourSubStateType_y.Idle)
            {
                _animator.CrossFadeInFixedTime(IdleStateId, 0.10f, 0, 0f);
            }
        }

        private void UpdateAnimatorSpeed()
        {
            _animator.speed = _motor.IsClimbing
                ? _motor.ClimbAnimationSpeed
                : _motor.ObstacleJumpAnimationSpeed;
        }

        private void UpdateClimbAnimationProgress()
        {
            if (_animator == null || _motor == null || !_motor.IsClimbing)
            {
                return;
            }

            int targetStateId = GetClimbStateId(_motor.CurrentSubStateType);
            if (targetStateId == 0)
            {
                return;
            }
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo nextStateInfo = _animator.GetNextAnimatorStateInfo(0);
                if (nextStateInfo.fullPathHash == targetStateId)
                {
                    stateInfo = nextStateInfo;
                }
            }

            if (stateInfo.fullPathHash == targetStateId)
            {
                _motor.SetClimbAnimationProgress(stateInfo.normalizedTime);
            }
        }

        private static int GetClimbStateId(ParkourSubStateType_y stateType)
        {
            return stateType switch
            {
                ParkourSubStateType_y.SprintWallClimb => SprintWallClimbStateId,
                ParkourSubStateType_y.CrouchedToSprint => CrouchedToSprintStateId,
                ParkourSubStateType_y.JumpingToHanging => JumpingToHangingStateId,
                ParkourSubStateType_y.HangingIdle => HangingIdleStateId,
                ParkourSubStateType_y.BracedHangHopLeft => BracedHangHopLeftStateId,
                ParkourSubStateType_y.BracedHangHopRight => BracedHangHopRightStateId,
                ParkourSubStateType_y.BracedHangToCrouch => BracedHangToCrouchStateId,
                ParkourSubStateType_y.JumpFromWall => JumpFromWallStateId,
                _ => 0
            };
        }

        private void UpdateVisualRootCompensation()
        {
            if (_motor == null || _animator == null)
            {
                return;
            }

            CacheVisualBasePosition();
            if (_hipsTransform == null && _animator.isHuman)
            {
                _hipsTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);
            }

            if (_hipsTransform == null || !_hasVisualBaseLocalPosition)
            {
                return;
            }

            AnimatorStateInfo currentInfo = _animator.GetCurrentAnimatorStateInfo(0);
            bool isCurrentSlide = currentInfo.IsName("Slide");
            bool isTransitionFromSlide = _animator.IsInTransition(0) && currentInfo.IsName("Slide");
            bool isNextSlide = _animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName("Slide");
            bool isSlideActive = _motor.IsSliding || isCurrentSlide || isTransitionFromSlide || isNextSlide;

            Vector3 desiredLocalPosition = _visualBaseLocalPosition;
            desiredLocalPosition.y += _motor.ClimbVisualRootHeightOffset;
            if (isSlideActive)
            {
                var forwardDisplacement = _hipsTransform.localPosition.z;
                desiredLocalPosition.z -= forwardDisplacement;
            }

            bool requiresExactClimbCompensation = _motor.CurrentSubStateType
                                                   == ParkourSubStateType_y.CrouchedToSprint;
            if (isSlideActive || requiresExactClimbCompensation)
            {
                _animator.transform.localPosition = desiredLocalPosition;
            }
            else if ((_animator.transform.localPosition - desiredLocalPosition).sqrMagnitude > 0.0001f)
            {
                _animator.transform.localPosition = Vector3.Lerp(
                    _animator.transform.localPosition,
                    desiredLocalPosition,
                    1f - Mathf.Exp(-16f * Time.deltaTime));

                if ((_animator.transform.localPosition - desiredLocalPosition).sqrMagnitude <= 0.0001f)
                {
                    _animator.transform.localPosition = desiredLocalPosition;
                }
            }
        }

        private void CacheVisualBasePosition()
        {
            if (_animator == null || _hasVisualBaseLocalPosition)
            {
                return;
            }

            _visualBaseLocalPosition = _animator.transform.localPosition;
            _hasVisualBaseLocalPosition = true;
        }

        private void Subscribe()
        {
            if (_motor == null)
            {
                return;
            }

            _motor.Jumped -= HandleJumped;
            _motor.Landed -= HandleLanded;
            _motor.SubStateChanged -= HandleSubStateChanged;
            _motor.Jumped += HandleJumped;
            _motor.Landed += HandleLanded;
            _motor.SubStateChanged += HandleSubStateChanged;
            HandleSubStateChanged(ParkourSubStateType_y.None, _motor.CurrentSubStateType);
        }

        private void InitializeHandContact()
        {
            if (_animator == null || _motor == null)
            {
                return;
            }

            ParkourHandContact_y handContact = _animator.GetComponent<ParkourHandContact_y>();
            if (handContact == null)
            {
                handContact = _animator.gameObject.AddComponent<ParkourHandContact_y>();
            }

            handContact.Initialize(_animator, _motor);
        }

        private void Unsubscribe()
        {
            if (_motor == null)
            {
                return;
            }

            _motor.Jumped -= HandleJumped;
            _motor.Landed -= HandleLanded;
            _motor.SubStateChanged -= HandleSubStateChanged;
        }

        private void HandleSubStateChanged(
            ParkourSubStateType_y previousSubStateType,
            ParkourSubStateType_y currentSubStateType)
        {
            if (_animator == null || _motor == null)
            {
                return;
            }

            UpdateAnimatorSpeed();
            UpdateActionAnimation(previousSubStateType, currentSubStateType);
        }

        private void HandleJumped()
        {
            if (_animator == null || _motor == null)
            {
                return;
            }

            _animator.SetInteger(JumpTierId, _motor.CurrentJumpTier);
            _animator.SetTrigger(JumpId);
        }

        private void HandleLanded(float impactSpeed)
        {
            _animator?.ResetTrigger(JumpId);
        }

        private static int GetObstacleJumpStateId(int jumpTier)
        {
            return jumpTier switch
            {
                1 => Jumping1StateId,
                2 => Jumping2StateId,
                3 => Jumping3StateId,
                _ => JumpOver1StateId,
            };
        }
    }
}
