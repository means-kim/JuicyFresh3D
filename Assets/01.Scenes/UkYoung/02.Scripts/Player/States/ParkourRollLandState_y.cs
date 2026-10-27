using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourRollLandState_y : ParkourStateBase_y
    {
        private readonly ParkourGroundedState_y _groundedState;
        private float _elapsedTime;
        private Vector3 _rollDirection;
        private float _rollSpeed;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.Roll;

        public Vector3 RollDirection => _rollDirection;

        public ParkourRollLandState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourGroundedState_y groundedState)
            : base(motor, stateMachine)
        {
            _groundedState = groundedState;
        }

        public float ElapsedTime => _elapsedTime;

        public override void StartState()
        {
            _elapsedTime = 0f;
            _rollDirection = Motor.HasMovementInput ? Motor.DesiredDirection : Motor.transform.forward;
            _rollSpeed = Mathf.Max(Motor.PlanarSpeed, 5.6f);
            Motor.SetPlanarVelocity(_rollDirection * _rollSpeed);
        }

        public override void UpdateState(float deltaTime)
        {
            _elapsedTime += deltaTime;

            float currentSpeed;
            if (_elapsedTime <= 0.25f)
            {
                float t = _elapsedTime / 0.25f;
                currentSpeed = Mathf.Lerp(_rollSpeed, 6.2f, t);
            }
            else if (_elapsedTime <= 0.55f)
            {
                float t = (_elapsedTime - 0.25f) / 0.30f;
                currentSpeed = Mathf.Lerp(6.2f, 3.2f, t);
            }
            else
            {
                float duration = Mathf.Max(Motor.RollDuration - 0.55f, 0.01f);
                float t = Mathf.Clamp01((_elapsedTime - 0.55f) / duration);
                float targetSpeed = Motor.HasMovementInput ? Motor.GetTargetGroundSpeed() : 0f;
                currentSpeed = Mathf.Lerp(3.2f, targetSpeed, t);
            }

            Motor.SetPlanarVelocity(_rollDirection * currentSpeed);

            if (_elapsedTime >= Motor.MinRollCancelDuration)
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
            }

            if (_elapsedTime >= Motor.RollDuration)
            {
                ParkourStateBase_y nextState = Motor.HasMovementInput
                    ? _groundedState.SprintState
                    : _groundedState.IdleState;
                _groundedState.SetGroundedSubState(nextState);
            }
        }
    }
}
