namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourJumpState_y : ParkourStateBase_y
    {
        private readonly ParkourAirborneState_y _airborneState;
        private bool _applyGroundImpulseOnStart = true;

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

        public void SetApplyGroundImpulseOnStart(bool applyGroundImpulse)
        {
            _applyGroundImpulseOnStart = applyGroundImpulse;
        }

        public override void StartState()
        {
            if (_applyGroundImpulseOnStart)
            {
                Motor.ApplyGroundJumpImpulse();
                if (Motor.IsObstacleVaulting)
                {
                    _applyGroundImpulseOnStart = true;
                    _airborneState.SetAirborneSubState(_airborneState.ObstacleVaultState);
                    return;
                }
            }

            _applyGroundImpulseOnStart = true;
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateAirPlanarVelocity(deltaTime);
            Motor.ApplyGravity(deltaTime);

            if (Motor.VerticalSpeed <= 0f)
            {
                _airborneState.SetAirborneSubState(_airborneState.FallState);
            }
        }
    }
}
