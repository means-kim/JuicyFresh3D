namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourSprintStartState_y : ParkourStateBase_y
    {
        private readonly ParkourGroundedState_y _groundedState;
        private float _elapsedTime;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.SprintStart;

        public ParkourSprintStartState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourGroundedState_y groundedState)
            : base(motor, stateMachine)
        {
            _groundedState = groundedState;
        }

        public override void StartState()
        {
            _elapsedTime = 0f;
        }

        public override void UpdateState(float deltaTime)
        {
            if (!Motor.HasMovementInput)
            {
                _groundedState.SetGroundedSubState(_groundedState.IdleState);
                return;
            }

            _elapsedTime += deltaTime;
            var isCompleted = Motor.ApplySprintStartCurve(_elapsedTime);
            if (isCompleted)
            {
                _groundedState.SetGroundedSubState(_groundedState.SprintState);
            }
        }
    }
}
