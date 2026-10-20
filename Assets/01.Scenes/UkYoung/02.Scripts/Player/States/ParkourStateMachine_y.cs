namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourStateMachine_y
    {
        private ParkourStateBase_y _currentRootState;

        public ParkourStateBase_y CurrentRootState => _currentRootState;

        public ParkourRootStateType_y CurrentRootStateType =>
            _currentRootState != null ? _currentRootState.RootStateType : ParkourRootStateType_y.None;

        public ParkourSubStateType_y CurrentSubStateType =>
            _currentRootState?.CurrentSubState != null
                ? _currentRootState.CurrentSubState.SubStateType
                : ParkourSubStateType_y.None;

        public void Initialize(ParkourStateBase_y initialRootState)
        {
            _currentRootState?.StopState();
            _currentRootState = initialRootState;
            _currentRootState?.StartState();
        }

        public void SetRootState(ParkourStateBase_y nextRootState)
        {
            if (_currentRootState == nextRootState)
            {
                return;
            }

            _currentRootState?.StopState();
            _currentRootState = nextRootState;
            _currentRootState?.StartState();
        }

        public void UpdateState(float deltaTime)
        {
            _currentRootState?.UpdateState(deltaTime);
        }
    }
}
