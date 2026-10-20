using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    public sealed class ParkourSprintStartState_y : ParkourStateBase_y
    {
        private readonly ParkourGroundedState_y _groundedState;
        private float _elapsedTime;

        public override ParkourRootStateType_y RootStateType => ParkourRootStateType_y.Grounded;
        public override ParkourSubStateType_y SubStateType => ParkourSubStateType_y.SprintStart;

        public ParkourSprintStartState_y(
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
        }

        public override void UpdateState(float deltaTime)
        {
            if (!Motor.HasMovementInput)
            {
                _groundedState.SetGroundedSubState(_groundedState.IdleState);
                return;
            }

            _elapsedTime += deltaTime;
            if (UpdateSprintStartVelocity(_elapsedTime))
            {
                _groundedState.SetGroundedSubState(_groundedState.SprintState);
            }
        }

        private bool UpdateSprintStartVelocity(float elapsedTime)
        {
            var duration = Mathf.Max(Motor.SprintStartDuration, 0.01f);
            var normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            var speedRatio = Mathf.Clamp01(Motor.SprintStartSpeedCurve.Evaluate(normalizedTime));
            Motor.SetPlanarVelocity(Motor.DesiredDirection * (Motor.GetTargetGroundSpeed() * speedRatio));
            return normalizedTime >= 1f;
        }

        public static AnimationCurve CreateDefaultSpeedCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.12f, 0f),
                new Keyframe(0.25f, 0.22f),
                new Keyframe(0.55f, 0.6f),
                new Keyframe(0.8f, 0.85f),
                new Keyframe(1f, 1f));

            Keyframe[] keys = curve.keys;
            for (var index = 0; index < keys.Length; index++)
            {
                var inTangent = index == 0 ? 0f : CalculateCurveSegmentSlope(keys[index - 1], keys[index]);
                var outTangent = index == keys.Length - 1 ? 0f : CalculateCurveSegmentSlope(keys[index], keys[index + 1]);
                keys[index] = new Keyframe(keys[index].time, keys[index].value, inTangent, outTangent);
            }

            curve.keys = keys;
            return curve;
        }

        private static float CalculateCurveSegmentSlope(Keyframe start, Keyframe end)
        {
            return (end.value - start.value) / (end.time - start.time);
        }
    }
}
