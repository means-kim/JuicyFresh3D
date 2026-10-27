using System;
using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    // 모터 이벤트와 레지 클립 진행도에 맞춰 카메라에 더할 일회성 킥과 트라우마 셰이크 값을 계산한다.
    public sealed class ParkourCameraImpact_y : MonoBehaviour
    {
        private const int MaximumActiveKickCount = 8;
        private const float PitchNoiseSeed = 3.7f;
        private const float RollNoiseSeed = 41.9f;

        [Header("Connections")]
        [SerializeField] private ParkourMotor _motor;

        [Header("Beats")]
        [SerializeField] private CameraImpactBeat_y[] _beats;

        [Header("Landing")]
        [SerializeField] private float _lightLandingTrauma = 0.1f;
        [SerializeField] private float _hardLandingTrauma = 0.45f;

        [Header("Jump")]
        [SerializeField] private float _jumpFieldOfViewKick = 2f;
        [SerializeField] private float _jumpKickRiseDuration = 0.05f;
        [SerializeField] private float _jumpKickFallDuration = 0.15f;

        [Header("Shake")]
        [SerializeField] private float _traumaDecayPerSecond = 1.6f;
        // 모터가 카메라 정면 방향으로 이동 방향을 계산하므로 요 흔들림은 넣지 않는다.
        [SerializeField] private float _maximumShakePitch = 4f;
        [SerializeField] private float _maximumShakeRoll = 5f;
        [SerializeField] private float _shakeFrequency = 22f;

        private readonly ActiveKick[] _activeKicks = new ActiveKick[MaximumActiveKickCount];
        private bool[] _hasFiredBeats;
        private ParkourSubStateType_y _currentState;
        private int _nextKickIndex;
        private float _trauma;

        public float PitchKick { get; private set; }
        public float RollKick { get; private set; }
        public float DistanceKick { get; private set; }
        public float FieldOfViewKick { get; private set; }
        public Vector3 ShakeEuler { get; private set; }

        private void Awake()
        {
            _hasFiredBeats = new bool[_beats.Length];
        }

        private void OnEnable()
        {
            _currentState = _motor.CurrentSubStateType;
            _motor.Landed += OnLanded;
            _motor.Jumped += OnJumped;
            _motor.SubStateChanged += OnSubStateChanged;
        }

        private void OnDisable()
        {
            _motor.Landed -= OnLanded;
            _motor.Jumped -= OnJumped;
            _motor.SubStateChanged -= OnSubStateChanged;
        }

        public void UpdateImpact(float deltaTime)
        {
            FireProgressBeats();
            AccumulateKicks(deltaTime);
            UpdateShake(deltaTime);
        }

        private void OnLanded(float impactSpeed)
        {
            // 슬라이딩 중 단차를 내려가는 것은 충격 사건이 아니므로 흔들지 않는다.
            if (_motor.IsSliding || impactSpeed < _motor.LightLandingSpeedThreshold)
            {
                return;
            }

            var severity = Mathf.InverseLerp(
                _motor.LightLandingSpeedThreshold,
                _motor.HardLandingSpeedThreshold,
                impactSpeed);
            AddTrauma(Mathf.Lerp(_lightLandingTrauma, _hardLandingTrauma, severity));
        }

        private void OnJumped()
        {
            AddKick(0f, 0f, 0f, _jumpFieldOfViewKick, _jumpKickRiseDuration, _jumpKickFallDuration);
        }

        private void OnSubStateChanged(ParkourSubStateType_y previousState, ParkourSubStateType_y currentState)
        {
            _currentState = currentState;
            Array.Clear(_hasFiredBeats, 0, _hasFiredBeats.Length);

            foreach (CameraImpactBeat_y beat in _beats)
            {
                if (beat.State == currentState && beat.TriggerProgress < 0f)
                {
                    FireBeat(beat);
                }
            }
        }

        private void FireProgressBeats()
        {
            var progress = _motor.ClimbProgress;
            for (var index = 0; index < _beats.Length; index++)
            {
                CameraImpactBeat_y beat = _beats[index];
                var isWaitingForProgress = beat.State == _currentState
                                           && beat.TriggerProgress >= 0f
                                           && !_hasFiredBeats[index];
                if (!isWaitingForProgress || progress < beat.TriggerProgress)
                {
                    continue;
                }

                _hasFiredBeats[index] = true;
                FireBeat(beat);
            }
        }

        private void FireBeat(CameraImpactBeat_y beat)
        {
            AddTrauma(beat.Trauma);
            if (beat.HasKick)
            {
                AddKick(
                    beat.Pitch,
                    beat.Roll,
                    beat.Distance,
                    beat.FieldOfView,
                    beat.RiseDuration,
                    beat.FallDuration);
            }
        }

        private void AddTrauma(float trauma)
        {
            _trauma = Mathf.Min(_trauma + trauma, 1f);
        }

        // 가득 차면 가장 오래된 킥을 덮어쓴다.
        private void AddKick(
            float pitch,
            float roll,
            float distance,
            float fieldOfView,
            float riseDuration,
            float fallDuration)
        {
            _activeKicks[_nextKickIndex] = new ActiveKick
            {
                Pitch = pitch,
                Roll = roll,
                Distance = distance,
                FieldOfView = fieldOfView,
                RiseDuration = riseDuration,
                FallDuration = fallDuration,
            };
            _nextKickIndex = (_nextKickIndex + 1) % MaximumActiveKickCount;
        }

        private void AccumulateKicks(float deltaTime)
        {
            var pitch = 0f;
            var roll = 0f;
            var distance = 0f;
            var fieldOfView = 0f;

            for (var index = 0; index < MaximumActiveKickCount; index++)
            {
                ActiveKick kick = _activeKicks[index];
                if (kick.ElapsedTime >= kick.RiseDuration + kick.FallDuration)
                {
                    continue;
                }

                var weight = CalculateKickWeight(kick);
                pitch += kick.Pitch * weight;
                roll += kick.Roll * weight;
                distance += kick.Distance * weight;
                fieldOfView += kick.FieldOfView * weight;
                kick.ElapsedTime += deltaTime;
                _activeKicks[index] = kick;
            }

            PitchKick = pitch;
            RollKick = roll;
            DistanceKick = distance;
            FieldOfViewKick = fieldOfView;
        }

        private void UpdateShake(float deltaTime)
        {
            _trauma = Mathf.MoveTowards(_trauma, 0f, _traumaDecayPerSecond * deltaTime);
            var amplitude = _trauma * _trauma;
            var noiseTime = Time.time * _shakeFrequency;
            ShakeEuler = new Vector3(
                SampleNoise(noiseTime, PitchNoiseSeed) * _maximumShakePitch,
                0f,
                SampleNoise(noiseTime, RollNoiseSeed) * _maximumShakeRoll) * amplitude;
        }

        private static float CalculateKickWeight(ActiveKick kick)
        {
            if (kick.ElapsedTime < kick.RiseDuration)
            {
                return Mathf.SmoothStep(0f, 1f, kick.ElapsedTime / kick.RiseDuration);
            }

            var fallProgress = (kick.ElapsedTime - kick.RiseDuration) / kick.FallDuration;
            return 1f - Mathf.SmoothStep(0f, 1f, fallProgress);
        }

        private static float SampleNoise(float noiseTime, float seed)
        {
            return Mathf.PerlinNoise(noiseTime, seed) * 2f - 1f;
        }

        private struct ActiveKick
        {
            public float Pitch;
            public float Roll;
            public float Distance;
            public float FieldOfView;
            public float RiseDuration;
            public float FallDuration;
            public float ElapsedTime;
        }
    }
}
