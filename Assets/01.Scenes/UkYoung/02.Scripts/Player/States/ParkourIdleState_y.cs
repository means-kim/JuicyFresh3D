using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourIdleState_y : ParkourStateBase_y
    {
        private readonly ParkourGroundedState_y _groundedState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.Idle;

        public ParkourIdleState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourGroundedState_y groundedState)
            : base(motor, stateMachine)
        {
            _groundedState = groundedState;
        }

        public override void UpdateState(float deltaTime)
        {
            if (Motor.HasMovementInput)
            {
                _groundedState.SetGroundedSubState(_groundedState.SprintStartState);
                return;
            }

            Motor.UpdatePlanarVelocityTowards(Vector3.zero, Motor.GroundDeceleration, deltaTime);
        }
    }
}
