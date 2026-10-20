namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourWallRunState_y : ParkourStateBase_y
    {
        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.WallRun;

        public ParkourWallRunState_y(ParkourMotor motor, ParkourStateMachine_y stateMachine)
            : base(motor, stateMachine)
        {
        }

        public override void StartState()
        {
            Motor.StartWallRunStatus();
        }

        public override void UpdateState(float deltaTime)
        {
            if (!Motor.CanStartOrContinueWallRun())
            {
                if (Motor.IsGrounded)
                {
                    StateMachine.SetRootState(Motor.GroundedState);
                }
                else
                {
                    Motor.AirborneState.SetStartWithJump(false);
                    StateMachine.SetRootState(Motor.AirborneState);
                }

                return;
            }

            Motor.UpdateWallRunMovement(deltaTime);

            if (Motor.HasBufferedJump())
            {
                Motor.ApplyWallJumpImpulse();
                Motor.AirborneState.SetStartWithExistingAscent();
                StateMachine.SetRootState(Motor.AirborneState);
            }
        }

        public override void StopState()
        {
            Motor.StopWallRunStatus();
            base.StopState();
        }
    }
}
