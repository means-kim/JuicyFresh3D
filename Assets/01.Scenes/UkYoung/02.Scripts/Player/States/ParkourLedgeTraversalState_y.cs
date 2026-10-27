namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourLedgeTraversalState_y : ParkourStateBase_y
    {
        private readonly ParkourJumpingToHangingState_y _jumpingToHangingState;
        private readonly ParkourHangingIdleState_y _hangingIdleState;
        private readonly ParkourHangHopState_y _hopLeftState;
        private readonly ParkourHangHopState_y _hopRightState;
        private readonly ParkourBracedHangToCrouchState_y _bracedHangToCrouchState;
        private readonly ParkourJumpFromWallState_y _jumpFromWallState;
        private readonly ParkourAirborneState_y _airborneState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;

        public ParkourHangingIdleState_y HangingIdleState => _hangingIdleState;
        public ParkourHangHopState_y HopLeftState => _hopLeftState;
        public ParkourHangHopState_y HopRightState => _hopRightState;
        public ParkourBracedHangToCrouchState_y BracedHangToCrouchState => _bracedHangToCrouchState;
        public ParkourJumpFromWallState_y JumpFromWallState => _jumpFromWallState;

        public ParkourLedgeTraversalState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourAirborneState_y airborneState)
            : base(motor, stateMachine)
        {
            _airborneState = airborneState;
            _jumpingToHangingState = new ParkourJumpingToHangingState_y(motor, stateMachine, this);
            _hangingIdleState = new ParkourHangingIdleState_y(motor, stateMachine, this, airborneState);
            _hopLeftState = new ParkourHangHopState_y(motor, stateMachine, this, false);
            _hopRightState = new ParkourHangHopState_y(motor, stateMachine, this, true);
            _bracedHangToCrouchState = new ParkourBracedHangToCrouchState_y(motor, stateMachine, this);
            _jumpFromWallState = new ParkourJumpFromWallState_y(motor, stateMachine, airborneState);
        }

        public override void StartState()
        {
            SetSubState(_jumpingToHangingState);
        }

        public override void UpdateState(float deltaTime)
        {
            base.UpdateState(deltaTime);
        }

        public override void StopState()
        {
            bool wasTraversingLedge = Motor.IsLedgeTraversing;
            base.StopState();
            if (wasTraversingLedge)
            {
                Motor.StopClimbMotion();
            }

            if (_airborneState.AirborneStartY <= Motor.transform.position.y)
            {
                _airborneState.SetFallFromElevation(Motor.transform.position.y, false);
            }
        }

        public void SetLedgeSubState(ParkourStateBase_y nextState)
        {
            SetSubState(nextState);
        }

        public void FinishOnTop()
        {
            Motor.GroundedState.SetStartWithCrouchedToSprint();
            StateMachine.SetRootState(Motor.GroundedState);
        }
    }

    public sealed class ParkourJumpingToHangingState_y : ParkourStateBase_y
    {
        private readonly ParkourLedgeTraversalState_y _ledgeState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.JumpingToHanging;

        public ParkourJumpingToHangingState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourLedgeTraversalState_y ledgeState)
            : base(motor, stateMachine)
        {
            _ledgeState = ledgeState;
        }

        public override void StartState()
        {
            Motor.StartJumpingToHanging();
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateClimbMovement(deltaTime);
            if (Motor.IsClimbMotionComplete)
            {
                _ledgeState.SetLedgeSubState(_ledgeState.HangingIdleState);
            }
        }
    }

    public sealed class ParkourHangingIdleState_y : ParkourStateBase_y
    {
        private readonly ParkourLedgeTraversalState_y _ledgeState;
        private readonly ParkourAirborneState_y _airborneState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.HangingIdle;

        public ParkourHangingIdleState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourLedgeTraversalState_y ledgeState,
            ParkourAirborneState_y airborneState)
            : base(motor, stateMachine)
        {
            _ledgeState = ledgeState;
            _airborneState = airborneState;
        }

        public override void StartState()
        {
            Motor.HoldHanging();
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateClimbMovement(deltaTime);
            if (Motor.CanStartBracedHangToCrouch())
            {
                _ledgeState.SetLedgeSubState(_ledgeState.BracedHangToCrouchState);
            }
            else if (Motor.CanStartJumpFromWall())
            {
                _ledgeState.SetLedgeSubState(_ledgeState.JumpFromWallState);
            }
            else if (Motor.CanDropFromHanging())
            {
                float releaseHeight = Motor.LedgeLeftHandContactPoint.y > 0.1f
                    ? Motor.LedgeLeftHandContactPoint.y
                    : Motor.transform.position.y + 1.8f;
                Motor.StopClimbMotion();
                _airborneState.SetFallFromElevation(releaseHeight, false);
                _airborneState.SetAirborneSubState(_airborneState.FallState);
            }
            else if (Motor.CanStartHangHop(true))
            {
                _ledgeState.SetLedgeSubState(_ledgeState.HopRightState);
            }
            else if (Motor.CanStartHangHop(false))
            {
                _ledgeState.SetLedgeSubState(_ledgeState.HopLeftState);
            }
        }
    }

    public sealed class ParkourHangHopState_y : ParkourStateBase_y
    {
        private readonly ParkourLedgeTraversalState_y _ledgeState;
        private readonly bool _moveRight;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => _moveRight
            ? ParkourSubStateType_y.BracedHangHopRight
            : ParkourSubStateType_y.BracedHangHopLeft;

        public ParkourHangHopState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourLedgeTraversalState_y ledgeState,
            bool moveRight)
            : base(motor, stateMachine)
        {
            _ledgeState = ledgeState;
            _moveRight = moveRight;
        }

        public override void StartState()
        {
            Motor.StartHangHop(_moveRight);
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateClimbMovement(deltaTime);
            if (Motor.IsClimbMotionComplete)
            {
                _ledgeState.SetLedgeSubState(_ledgeState.HangingIdleState);
            }
        }
    }

    public sealed class ParkourBracedHangToCrouchState_y : ParkourStateBase_y
    {
        private readonly ParkourLedgeTraversalState_y _ledgeState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.BracedHangToCrouch;

        public ParkourBracedHangToCrouchState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourLedgeTraversalState_y ledgeState)
            : base(motor, stateMachine)
        {
            _ledgeState = ledgeState;
        }

        public override void StartState()
        {
            Motor.StartBracedHangToCrouch();
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateClimbMovement(deltaTime);
            if (Motor.IsClimbMotionComplete)
            {
                _ledgeState.FinishOnTop();
            }
        }
    }

    public sealed class ParkourJumpFromWallState_y : ParkourStateBase_y
    {
        private readonly ParkourAirborneState_y _airborneState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Airborne;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.JumpFromWall;

        public ParkourJumpFromWallState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourAirborneState_y airborneState)
            : base(motor, stateMachine)
        {
            _airborneState = airborneState;
        }

        public override void StartState()
        {
            float releaseHeight = Motor.LedgeLeftHandContactPoint.y > 0.1f
                ? Motor.LedgeLeftHandContactPoint.y
                : Motor.transform.position.y + 1.8f;
            _airborneState.SetFallFromElevation(releaseHeight, false);
            Motor.StartJumpFromWall();
        }

        public override void UpdateState(float deltaTime)
        {
            Motor.UpdateClimbMovement(deltaTime);
            if (Motor.IsClimbMotionComplete)
            {
                _airborneState.SetAirborneSubState(_airborneState.FallState);
            }
        }
    }
}
