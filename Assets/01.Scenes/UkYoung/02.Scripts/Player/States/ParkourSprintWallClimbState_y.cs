namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourSprintWallClimbState_y : ParkourStateBase_y
    {
        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.SprintWallClimb;

        public ParkourSprintWallClimbState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine)
            : base(motor, stateMachine)
        {
        }

        public override void StartState()
        {
            Motor.StartSprintWallClimb();
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateClimbMovement(deltaTime);
            if (!Motor.IsClimbMotionComplete)
            {
                return;
            }

            Motor.GroundedState.SetStartWithCrouchedToSprint();
            StateMachine.SetRootState(Motor.GroundedState);
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
