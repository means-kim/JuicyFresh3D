namespace JuicyFresh3D.Parkour
{
    public abstract class ParkourStateBase_y
    {
        protected ParkourMotor Motor { get; }
        protected ParkourStateMachine_y StateMachine { get; }

        private ParkourStateBase_y _currentSubState;

        public virtual ParkourRootStateType_y RootStateType => ParkourRootStateType_y.None;
        public virtual ParkourSubStateType_y SubStateType => ParkourSubStateType_y.None;
        public ParkourStateBase_y CurrentSubState => _currentSubState;
        public ParkourStateBase_y ActiveLeafState => _currentSubState?.ActiveLeafState ?? this;

        protected ParkourStateBase_y(ParkourMotor motor, ParkourStateMachine_y stateMachine)
        {
            Motor = motor;
            StateMachine = stateMachine;
        }

        public virtual void StartState()
        {
        }

        public virtual void UpdateState(float deltaTime)
        {
            _currentSubState?.UpdateState(deltaTime);
        }

        public virtual void UpdateAfterMoveState()
        {
            _currentSubState?.UpdateAfterMoveState();
        }

        public virtual void StopState()
        {
            StopSubState();
        }

        protected void SetSubState(ParkourStateBase_y nextSubState)
        {
            if (_currentSubState == nextSubState)
            {
                return;
            }

            ParkourSubStateType_y previousSubStateType = StateMachine.CurrentSubStateType;
            _currentSubState?.StopState();
            _currentSubState = nextSubState;
            _currentSubState?.StartState();
            StateMachine.NotifySubStateChanged(previousSubStateType);
        }

        protected void StopSubState()
        {
            if (_currentSubState == null)
            {
                return;
            }

            _currentSubState.StopState();
            _currentSubState = null;
        }
    }
}
