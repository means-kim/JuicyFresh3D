namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourRollState_y : ParkourStateBase_y
    {
        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Roll;

        public ParkourRollState_y(ParkourMotor motor, ParkourStateMachine_y stateMachine)
            : base(motor, stateMachine)
        {
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateRollMovement(deltaTime);
            if (Motor.IsRolling)
            {
                return;
            }

            if (Motor.IsGrounded)
            {
                StateMachine.SetRootState(Motor.GroundedState);
            }
            else
            {
                Motor.AirborneState.SetStartWithJump(false);
                StateMachine.SetRootState(Motor.AirborneState);
            }
        }
    }
}
