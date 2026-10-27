namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourCrouchedToSprintState_y : ParkourStateBase_y
    {
        private readonly ParkourGroundedState_y _groundedState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.CrouchedToSprint;

        public ParkourCrouchedToSprintState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourGroundedState_y groundedState)
            : base(motor, stateMachine)
        {
            _groundedState = groundedState;
        }

        public override void StartState()
        {
            Motor.StartCrouchedToSprint();
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateClimbMovement(deltaTime);
        }

        public override void UpdateAfterMoveState()
        {
            if (!Motor.IsClimbMotionComplete)
            {
                return;
            }

            _groundedState.SetGroundedSubState(
                Motor.HasForwardInput
                    ? _groundedState.SprintState
                    : _groundedState.SprintStopState);
        }

        public override void StopState()
        {
            if (!Motor.IsClimbMotionComplete)
            {
                Motor.StopClimbMotion();
            }
        }
    }
}
