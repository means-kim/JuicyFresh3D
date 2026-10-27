namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourAirborneState_y : ParkourStateBase_y
    {
        private readonly ParkourJumpState_y _jumpState;
        private readonly ParkourFallState_y _fallState;
        private readonly ParkourObstacleVaultState_y _obstacleVaultState;
        private readonly ParkourWallRunState_y _wallRunState;
        private readonly ParkourSprintWallClimbState_y _sprintWallClimbState;
        private readonly ParkourLedgeTraversalState_y _ledgeTraversalState;
        private bool _startWithGroundJump;
        private bool _startWithExistingAscent;
        private bool _startWithSprintWallClimb;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;

        public ParkourJumpState_y JumpState => _jumpState;
        public ParkourFallState_y FallState => _fallState;
        public ParkourObstacleVaultState_y ObstacleVaultState => _obstacleVaultState;
        public ParkourWallRunState_y WallRunState => _wallRunState;
        public ParkourSprintWallClimbState_y SprintWallClimbState => _sprintWallClimbState;
        public ParkourLedgeTraversalState_y LedgeTraversalState => _ledgeTraversalState;

        public ParkourAirborneState_y(ParkourMotor motor, ParkourStateMachine_y stateMachine)
            : base(motor, stateMachine)
        {
            _jumpState = new ParkourJumpState_y(motor, stateMachine, this);
            _fallState = new ParkourFallState_y(motor, stateMachine, this);
            _obstacleVaultState = new ParkourObstacleVaultState_y(motor, stateMachine, this);
            _wallRunState = new ParkourWallRunState_y(motor, stateMachine, this);
            _sprintWallClimbState = new ParkourSprintWallClimbState_y(motor, stateMachine);
            _ledgeTraversalState = new ParkourLedgeTraversalState_y(motor, stateMachine, this);
        }

        public void SetFallFromElevation(float elevationY, bool wasJump)
        {
            _airborneStartY = elevationY;
            _wasJump = wasJump;
        }

        public void SetAirborneSubState(ParkourStateBase_y nextSubState)
        {
            if (CurrentSubState == _wallRunState)
            {
                float estimatedApex = Motor.transform.position.y + Motor.WallJumpHeight;
                _airborneStartY = estimatedApex;
                _wasJump = false;
            }

            SetSubState(nextSubState);
        }

        public void SetStartWithJump(bool startWithGroundJump)
        {
            _startWithGroundJump = startWithGroundJump;
            _startWithExistingAscent = false;
            _startWithSprintWallClimb = false;
        }

        public void SetStartWithExistingAscent()
        {
            _startWithExistingAscent = true;
            _startWithGroundJump = false;
            _startWithSprintWallClimb = false;
        }

        public void SetStartWithSprintWallClimb()
        {
            _startWithSprintWallClimb = true;
            _startWithGroundJump = false;
            _startWithExistingAscent = false;
        }

        private float _airborneStartY;
        private bool _wasJump;

        public float AirborneStartY => _airborneStartY;
        public bool WasJump => _wasJump;

        public override void StartState()
        {
            _airborneStartY = Motor.transform.position.y;
            _wasJump = _startWithGroundJump || _startWithExistingAscent;

            if (_startWithSprintWallClimb)
            {
                _startWithSprintWallClimb = false;
                SetSubState(_sprintWallClimbState);
                return;
            }

            if (_startWithGroundJump)
            {
                _startWithGroundJump = false;
                _jumpState.SetApplyGroundImpulseOnStart(true);
                SetSubState(_jumpState);
                return;
            }

            if (_startWithExistingAscent)
            {
                _startWithExistingAscent = false;
                _jumpState.SetApplyGroundImpulseOnStart(false);
                SetSubState(_jumpState);
                return;
            }

            SetSubState(_fallState);
        }

        public override void UpdateState(float deltaTime)
        {
            if (CurrentSubState == _obstacleVaultState
                || CurrentSubState == _wallRunState
                || CurrentSubState == _sprintWallClimbState
                || CurrentSubState == _ledgeTraversalState)
            {
                base.UpdateState(deltaTime);
                return;
            }

            if (Motor.IsGrounded && Motor.VerticalSpeed <= 0f)
            {
                float verticalDrop = _airborneStartY - Motor.transform.position.y;
                Motor.SetLandingFallMetrics(verticalDrop, _wasJump);
                StateMachine.SetRootState(Motor.GroundedState);
                return;
            }

            if (Motor.CanStartWallRun())
            {
                SetSubState(_wallRunState);
                return;
            }

            if (Motor.CanStartJumpingToHanging())
            {
                SetSubState(_ledgeTraversalState);
                return;
            }

            base.UpdateState(deltaTime);
        }

        public override void StopState()
        {
            Motor.StopObstacleVault();
            base.StopState();
        }
    }
}
