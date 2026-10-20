namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourJumpState_y : ParkourStateBase_y
    {
        private readonly ParkourAirborneState_y _airborneState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.Jump;

        public ParkourJumpState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourAirborneState_y airborneState)
            : base(motor, stateMachine)
        {
            _airborneState = airborneState;
        }

        public override void StartState()
        {
            Motor.ApplyGroundJumpImpulse();
        }

        public override void UpdateState(float deltaTime)
        {
            if (Motor.IsObstacleVaulting)
            {
                Motor.UpdateObstacleVaultMovement(deltaTime);
                if (!Motor.IsObstacleVaulting)
                {
                    if (Motor.IsGrounded)
                    {
                        StateMachine.SetRootState(Motor.GroundedState);
                    }
                    else
                    {
                        _airborneState.SetAirborneSubState(_airborneState.FallState);
                    }
                }

                return;
            }

            Motor.UpdateAirPlanarVelocity(deltaTime);
            Motor.ApplyGravity(deltaTime);

            if (Motor.VerticalSpeed <= 0f)
            {
                _airborneState.SetAirborneSubState(_airborneState.FallState);
            }
        }
    }
}
