namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourAirborneState_y : ParkourStateBase_y
    {
        private readonly ParkourJumpState_y _jumpState;
        private readonly ParkourFallState_y _fallState;
        private bool _startWithGroundJump;
        private bool _startWithExistingAscent;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;

        public ParkourJumpState_y JumpState => _jumpState;
        public ParkourFallState_y FallState => _fallState;

        public ParkourAirborneState_y(ParkourMotor motor, ParkourStateMachine_y stateMachine)
            : base(motor, stateMachine)
        {
            _jumpState = new ParkourJumpState_y(motor, stateMachine, this);
            _fallState = new ParkourFallState_y(motor, stateMachine, this);
        }

        public void SetStartWithJump(bool startWithGroundJump)
        {
            _startWithGroundJump = startWithGroundJump;
            _startWithExistingAscent = false;
        }

        public void SetStartWithExistingAscent()
        {
            _startWithGroundJump = false;
            _startWithExistingAscent = true;
        }

        public override void StartState()
        {
            if (_startWithGroundJump)
            {
                _startWithGroundJump = false;
                _jumpState.SetApplyImpulseOnStart(true);
                SetSubState(_jumpState);
                return;
            }

            if (_startWithExistingAscent)
            {
                _startWithExistingAscent = false;
                _jumpState.SetApplyImpulseOnStart(false);
                SetSubState(_jumpState);
                return;
            }

            SetSubState(_fallState);
        }

        public override void UpdateState(float deltaTime)
        {
            if (Motor.CanStartOrContinueWallRun())
            {
                StateMachine.SetRootState(Motor.WallRunState);
                return;
            }

            if (Motor.IsGrounded && Motor.VerticalSpeed <= 0f)
            {
                if (Motor.ConsumePendingHardLandRoll())
                {
                    StateMachine.SetRootState(Motor.RollState);
                }
                else
                {
                    StateMachine.SetRootState(Motor.GroundedState);
                }

                return;
            }

            base.UpdateState(deltaTime);
        }

        public void SetAirborneSubState(ParkourStateBase_y nextSubState)
        {
            SetSubState(nextSubState);
        }
    }
}
