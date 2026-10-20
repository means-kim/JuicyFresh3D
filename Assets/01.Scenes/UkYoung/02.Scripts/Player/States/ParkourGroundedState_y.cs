namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourGroundedState_y : ParkourStateBase_y
    {
        private readonly ParkourIdleState_y _idleState;
        private readonly ParkourSprintStartState_y _sprintStartState;
        private readonly ParkourSprintState_y _sprintState;
        private readonly ParkourSprintStopState_y _sprintStopState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;

        public ParkourIdleState_y IdleState => _idleState;
        public ParkourSprintStartState_y SprintStartState => _sprintStartState;
        public ParkourSprintState_y SprintState => _sprintState;
        public ParkourSprintStopState_y SprintStopState => _sprintStopState;

        public ParkourGroundedState_y(ParkourMotor motor, ParkourStateMachine_y stateMachine)
            : base(motor, stateMachine)
        {
            _idleState = new ParkourIdleState_y(motor, stateMachine, this);
            _sprintStartState = new ParkourSprintStartState_y(motor, stateMachine, this);
            _sprintState = new ParkourSprintState_y(motor, stateMachine, this);
            _sprintStopState = new ParkourSprintStopState_y(motor, stateMachine, this);
        }

        public override void StartState()
        {
            Motor.SnapGroundedVerticalVelocity();

            if (Motor.HasMovementInput)
            {
                SetSubState(Motor.PlanarSpeed > 0.5f ? _sprintState : _sprintStartState);
            }
            else
            {
                SetSubState(_idleState);
            }
        }

        public override void UpdateState(float deltaTime)
        {
            if (Motor.CanStartJump())
            {
                Motor.AirborneState.SetStartWithJump(true);
                StateMachine.SetRootState(Motor.AirborneState);
                return;
            }

            if (!Motor.IsGrounded)
            {
                Motor.AirborneState.SetStartWithJump(false);
                StateMachine.SetRootState(Motor.AirborneState);
                return;
            }

            Motor.SnapGroundedVerticalVelocity();
            base.UpdateState(deltaTime);
        }

        public void SetGroundedSubState(ParkourStateBase_y nextSubState)
        {
            SetSubState(nextSubState);
        }
    }
}
