namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourSlideState_y : ParkourStateBase_y
    {
        private readonly ParkourGroundedState_y _groundedState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.Slide;

        public ParkourSlideState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourGroundedState_y groundedState)
            : base(motor, stateMachine)
        {
            _groundedState = groundedState;
        }

        public override void StartState()
        {
            Motor.StartSlide();
        }

        public override void UpdateState(float deltaTime)
        {
            if (Motor.WasJumpPressed)
            {
                Motor.ApplySlideJump();
                Motor.AirborneState.SetStartWithExistingAscent();
                StateMachine.SetRootState(Motor.AirborneState);
                return;
            }

            if (!Motor.UpdateSlideMovement(deltaTime))
            {
                ParkourStateBase_y nextState = Motor.HasMovementInput
                    ? _groundedState.SprintState
                    : _groundedState.IdleState;
                _groundedState.SetGroundedSubState(nextState);
                return;
            }

            if (!Motor.IsGrounded && !Motor.HasSlideGroundContact())
            {
                Motor.AirborneState.SetStartWithJump(false);
                StateMachine.SetRootState(Motor.AirborneState);
                return;
            }
        }

        public override void StopState()
        {
            Motor.StopSlide();
            base.StopState();
        }
    }
}
