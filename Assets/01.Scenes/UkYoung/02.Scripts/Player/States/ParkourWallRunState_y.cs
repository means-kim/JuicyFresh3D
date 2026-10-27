namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourWallRunState_y : ParkourStateBase_y
    {
        private readonly ParkourAirborneState_y _airborneState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.WallRun;

        public ParkourWallRunState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourAirborneState_y airborneState)
            : base(motor, stateMachine)
        {
            _airborneState = airborneState;
        }

        public override void StartState()
        {
            Motor.StartWallRun();
        }

        public override void UpdateState(float deltaTime)
        {
            if (Motor.IsGrounded && Motor.VerticalSpeed <= 0f)
            {
                float verticalDrop = _airborneState.AirborneStartY - Motor.transform.position.y;
                Motor.SetLandingFallMetrics(verticalDrop, false);
                StateMachine.SetRootState(Motor.GroundedState);
                return;
            }

            if (Motor.CanStartWallJump())
            {
                Motor.ApplyWallJumpImpulse();
                _airborneState.JumpState.SetApplyGroundImpulseOnStart(false);
                _airborneState.SetAirborneSubState(_airborneState.JumpState);
                return;
            }

            if (!Motor.UpdateWallRunMovement(deltaTime))
            {
                Motor.ApplyWallJumpImpulse();
                _airborneState.JumpState.SetApplyGroundImpulseOnStart(false);
                _airborneState.SetAirborneSubState(_airborneState.JumpState);
            }
        }

        public override void StopState()
        {
            Motor.StopWallRun();
            base.StopState();
        }
    }
}
