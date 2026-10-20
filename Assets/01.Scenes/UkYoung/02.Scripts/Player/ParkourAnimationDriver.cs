using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourAnimationDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int HardLandId = Animator.StringToHash("HardLand");
        private static readonly int JumpId = Animator.StringToHash("Jump");
        private static readonly int MovingId = Animator.StringToHash("Moving");
        private static readonly int RollId = Animator.StringToHash("Roll");
        private static readonly int SprintingId = Animator.StringToHash("Sprinting");
        private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        private static readonly int WallRunningId = Animator.StringToHash("WallRunning");

        [SerializeField] private Animator _animator;
        [SerializeField] private ParkourMotor _motor;

        public void Initialize(Animator animator, ParkourMotor motor)
        {
            Unsubscribe();
            _animator = animator;
            _motor = motor;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (_animator == null || _motor == null)
            {
                return;
            }

            _animator.SetFloat(SpeedId, _motor.PlanarSpeed, 0.12f, Time.deltaTime);
            _animator.SetFloat(VerticalSpeedId, _motor.VerticalSpeed);
            _animator.SetBool(GroundedId, _motor.IsGrounded);
            _animator.SetBool(MovingId, _motor.HasMovementInput);
            _animator.SetBool(WallRunningId, _motor.IsWallRunning);
            _animator.SetBool(SprintingId, _motor.IsSprinting);
        }

        private void Subscribe()
        {
            if (_motor == null)
            {
                return;
            }

            _motor.Jumped -= HandleJumped;
            _motor.Rolled -= HandleRolled;
            _motor.HardLanded -= HandleHardLanded;
            _motor.Jumped += HandleJumped;
            _motor.Rolled += HandleRolled;
            _motor.HardLanded += HandleHardLanded;
        }

        private void Unsubscribe()
        {
            if (_motor == null)
            {
                return;
            }

            _motor.Jumped -= HandleJumped;
            _motor.Rolled -= HandleRolled;
            _motor.HardLanded -= HandleHardLanded;
        }

        private void HandleJumped()
        {
            _animator?.SetTrigger(JumpId);
        }

        private void HandleRolled()
        {
            _animator?.SetTrigger(RollId);
        }

        private void HandleHardLanded()
        {
            _animator?.SetTrigger(HardLandId);
        }
    }
}
