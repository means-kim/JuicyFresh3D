using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourSprintState_y : ParkourStateBase_y
    {
        private readonly ParkourGroundedState_y _groundedState;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.Sprint;

        public ParkourSprintState_y(
            ParkourMotor motor,
            ParkourStateMachine_y stateMachine,
            ParkourGroundedState_y groundedState)
            : base(motor, stateMachine)
        {
            _groundedState = groundedState;
        }

        public override void UpdateState(float deltaTime)
        {
            if (!Motor.HasMovementInput)
            {
                _groundedState.SetGroundedSubState(_groundedState.SprintStopState);
                return;
            }

            Vector3 targetVelocity = Motor.DesiredDirection * Motor.GetTargetGroundSpeed();
            Motor.UpdatePlanarVelocityTowards(targetVelocity, Motor.GroundAcceleration, deltaTime);
        }
    }
}
