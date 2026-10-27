using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourGroundedState_y : ParkourStateBase_y
    {
        private readonly ParkourIdleState_y _idleState;
        private readonly ParkourSprintStartState_y _sprintStartState;
        private readonly ParkourSprintState_y _sprintState;
        private readonly ParkourSprintStopState_y _sprintStopState;
        private readonly ParkourSlideState_y _slideState;
        private readonly ParkourLandState_y _landState;
        private readonly ParkourRollLandState_y _rollState;
        private readonly ParkourCrouchedToSprintState_y _crouchedToSprintState;
        private bool _startWithCrouchedToSprint;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;

        public ParkourIdleState_y IdleState => _idleState;
        public ParkourSprintStartState_y SprintStartState => _sprintStartState;
        public ParkourSprintState_y SprintState => _sprintState;
        public ParkourSprintStopState_y SprintStopState => _sprintStopState;
        public ParkourSlideState_y SlideState => _slideState;
        public ParkourLandState_y LandState => _landState;
        public ParkourRollLandState_y RollLandState => _rollState;
        public ParkourCrouchedToSprintState_y CrouchedToSprintState => _crouchedToSprintState;

        public ParkourGroundedState_y(ParkourMotor motor, ParkourStateMachine_y stateMachine)
            : base(motor, stateMachine)
        {
            _idleState = new ParkourIdleState_y(motor, stateMachine, this);
            _sprintStartState = new ParkourSprintStartState_y(motor, stateMachine, this);
            _sprintState = new ParkourSprintState_y(motor, stateMachine, this);
            _sprintStopState = new ParkourSprintStopState_y(motor, stateMachine, this);
            _slideState = new ParkourSlideState_y(motor, stateMachine, this);
            _landState = new ParkourLandState_y(motor, stateMachine, this);
            _rollState = new ParkourRollLandState_y(motor, stateMachine, this);
            _crouchedToSprintState = new ParkourCrouchedToSprintState_y(motor, stateMachine, this);
        }

        public override void StartState()
        {
            Motor.SnapGroundedVerticalVelocity();

            if (_startWithCrouchedToSprint)
            {
                _startWithCrouchedToSprint = false;
                SetSubState(_crouchedToSprintState);
                return;
            }

            var impactSpeed = Motor.LastLandingImpactSpeed;
            var dropDistance = Motor.LastVerticalDropDistance;

            bool isLowStepOrFlatJump = dropDistance <= Motor.FlatJumpMaxElevationTolerance;

            bool isHighFall = !isLowStepOrFlatJump
                && (dropDistance >= Motor.MinRollFallHeight || impactSpeed >= Motor.HardLandingSpeedThreshold);

            if (isHighFall)
            {
                bool hasMovementInput = Motor.HasMovementInput;
                Vector3 rollDirection = Motor.HasMovementInput
                    ? Motor.DesiredDirection
                    : (Motor.PlanarSpeed > 0.5f ? Vector3.ProjectOnPlane(Motor.PlanarVelocity, Vector3.up).normalized : Motor.transform.forward);

                bool wallInFront = Physics.Raycast(
                    Motor.transform.position + Vector3.up * 0.8f,
                    rollDirection,
                    0.45f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                if (hasMovementInput && !wallInFront)
                {
                    SetSubState(_rollState);
                    return;
                }

                SetSubState(_landState);
                return;
            }

            if (Motor.HasMovementInput || Motor.PlanarSpeed > 0.5f)
            {
                SetSubState(Motor.PlanarSpeed > 0.5f ? _sprintState : _sprintStartState);
                return;
            }

            if (!isLowStepOrFlatJump && impactSpeed >= Motor.LightLandingSpeedThreshold)
            {
                SetSubState(_landState);
                return;
            }

            SetSubState(_idleState);
        }

        public override void UpdateState(float deltaTime)
        {
            if (CurrentSubState == _slideState
                || CurrentSubState == _landState
                || CurrentSubState == _rollState
                || CurrentSubState == _crouchedToSprintState)
            {
                base.UpdateState(deltaTime);
                return;
            }

            if (!Motor.IsGrounded)
            {
                Motor.AirborneState.SetStartWithJump(false);
                StateMachine.SetRootState(Motor.AirborneState);
                return;
            }

            if (Motor.CanStartSprintWallClimb())
            {
                Motor.AirborneState.SetStartWithSprintWallClimb();
                StateMachine.SetRootState(Motor.AirborneState);
                return;
            }

            if (Motor.CanStartJump())
            {
                Motor.AirborneState.SetStartWithJump(true);
                StateMachine.SetRootState(Motor.AirborneState);
                return;
            }

            if (Motor.CanStartSlide())
            {
                SetSubState(_slideState);
                return;
            }

            if (!Motor.IsGrounded)
            {
                Motor.AirborneState.SetStartWithJump(false);
                StateMachine.SetRootState(Motor.AirborneState);
                return;
            }

            Motor.SnapGroundedVerticalVelocity();
            base.UpdateState(deltaTime);
        }

        public void SetGroundedSubState(ParkourStateBase_y nextSubState)
        {
            SetSubState(nextSubState);
        }

        public void SetStartWithCrouchedToSprint()
        {
            _startWithCrouchedToSprint = true;
        }
    }
}
