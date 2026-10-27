using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourLandState_y : ParkourStateBase_y
    {
        private readonly ParkourGroundedState_y _groundedState;
        private float _elapsedTime;
        private float _duration;
        private float _minCancelDuration;
        private bool _isHardLand;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.Land;

        public bool IsHardLand => _isHardLand;

        public ParkourLandState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourGroundedState_y groundedState)
            : base(motor, stateMachine)
        {
            _groundedState = groundedState;
        }

        public override void StartState()
        {
            _elapsedTime = 0f;
            _isHardLand = Motor.LastLandingImpactSpeed >= Motor.HardLandingSpeedThreshold;
            _duration = _isHardLand ? Motor.HardLandDuration : Motor.SoftLandDuration;
            _minCancelDuration = _isHardLand ? Motor.MinHardLandCancelDuration : Motor.MinLandCancelDuration;
            Motor.SetPlanarVelocity(Vector3.zero);
        }

        public override void UpdateState(float deltaTime)
        {
            _elapsedTime += deltaTime;
            Motor.SetPlanarVelocity(Vector3.zero);

            if (_elapsedTime >= _minCancelDuration)
            {
                if (Motor.CanStartJump())
                {
                    Motor.AirborneState.SetStartWithJump(true);
                    StateMachine.SetRootState(Motor.AirborneState);
                    return;
                }

                if (Motor.CanStartSlide())
                {
                    _groundedState.SetGroundedSubState(_groundedState.SlideState);
                    return;
                }

                if (Motor.HasMovementInput)
                {
                    _groundedState.SetGroundedSubState(_groundedState.SprintState);
                    return;
                }
            }

            if (_elapsedTime >= _duration)
            {
                ParkourStateBase_y nextState = Motor.HasMovementInput
                    ? _groundedState.SprintState
                    : _groundedState.IdleState;
                _groundedState.SetGroundedSubState(nextState);
            }
        }
    }
}
