namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourObstacleVaultState_y : ParkourStateBase_y
    {
        private readonly ParkourAirborneState_y _airborneState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.ObstacleVault;

        public ParkourObstacleVaultState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourAirborneState_y airborneState)
            : base(motor, stateMachine)
        {
            _airborneState = airborneState;
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateObstacleVaultMovement(deltaTime);
            if (Motor.IsObstacleVaulting)
            {
                return;
            }

            if (Motor.IsGrounded)
            {
                float verticalDrop = _airborneState.AirborneStartY - Motor.transform.position.y;
                Motor.SetLandingFallMetrics(verticalDrop, _airborneState.WasJump);
                StateMachine.SetRootState(Motor.GroundedState);
                return;
            }

            _airborneState.SetAirborneSubState(_airborneState.FallState);
        }
    }
}
