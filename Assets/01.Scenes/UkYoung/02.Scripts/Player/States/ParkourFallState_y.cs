namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourFallState_y : ParkourStateBase_y
    {
        private readonly ParkourAirborneState_y _airborneState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.Fall;

        public ParkourFallState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourAirborneState_y airborneState)
            : base(motor, stateMachine)
        {
            _airborneState = airborneState;
        }

        public override void UpdateState(float deltaTime)
        {
            if (Motor.CanStartJump())
            {
                _airborneState.SetAirborneSubState(_airborneState.JumpState);
                return;
            }

            Motor.UpdateAirPlanarVelocity(deltaTime);
            Motor.ApplyGravity(deltaTime);
        }
    }
}
