using System;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourStateMachine_y
    {
        private ParkourStateBase_y _currentRootState;
        private bool _isChangingRootState;

        public event Action<ParkourSubStateType_y, ParkourSubStateType_y> SubStateChanged;

        public ParkourStateBase_y CurrentRootState => _currentRootState;

        public ParkourRootStateType_y CurrentRootStateType =>
            _currentRootState != null ? _currentRootState.RootStateType : ParkourRootStateType_y.None;

        public ParkourSubStateType_y CurrentSubStateType =>
            _currentRootState?.ActiveLeafState != null
                ? _currentRootState.ActiveLeafState.SubStateType
                : ParkourSubStateType_y.None;

        public void Initialize(ParkourStateBase_y initialRootState)
        {
            ParkourSubStateType_y previousSubStateType = CurrentSubStateType;
            _isChangingRootState = true;
            _currentRootState?.StopState();
            _currentRootState = initialRootState;
            _currentRootState?.StartState();
            _isChangingRootState = false;
            NotifySubStateChanged(previousSubStateType);
        }

        public void SetRootState(ParkourStateBase_y nextRootState)
        {
            if (_currentRootState == nextRootState)
            {
                return;
            }

            ParkourSubStateType_y previousSubStateType = CurrentSubStateType;
            _isChangingRootState = true;
            try
            {
                _currentRootState?.StopState();
                _currentRootState = nextRootState;
                _currentRootState?.StartState();
            }
            finally
            {
                _isChangingRootState = false;
            }

            NotifySubStateChanged(previousSubStateType);
        }

        public void UpdateState(float deltaTime)
        {
            _currentRootState?.UpdateState(deltaTime);
        }

        public void UpdateAfterMoveState()
        {
            _currentRootState?.UpdateAfterMoveState();
        }

        internal void NotifySubStateChanged(ParkourSubStateType_y previousSubStateType)
        {
            if (_isChangingRootState)
            {
                return;
            }

            ParkourSubStateType_y currentSubStateType = CurrentSubStateType;
            if (previousSubStateType != currentSubStateType)
            {
                SubStateChanged?.Invoke(previousSubStateType, currentSubStateType);
            }
        }
    }
}
