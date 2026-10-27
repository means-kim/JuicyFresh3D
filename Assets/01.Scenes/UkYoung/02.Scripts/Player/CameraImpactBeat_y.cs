using System;
using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    // 상태 진입이나 레지 클립 진행도 시점에 카메라가 보일 일회성 반응 하나를 정의한다.
    [Serializable]
    public struct CameraImpactBeat_y
    {
        [SerializeField] private ParkourSubStateType_y _state;
        // 음수면 상태 진입 시 발동하고, 0 이상이면 레지 클립 정규화 진행도에서 발동한다.
        [Range(-1f, 1f)][SerializeField] private float _triggerProgress;
        [Range(0f, 1f)][SerializeField] private float _trauma;
        [Range(-8f, 8f)][SerializeField] private float _pitch;
        [Range(-8f, 8f)][SerializeField] private float _roll;
        [Range(-1f, 1f)][SerializeField] private float _distance;
        [Range(-8f, 8f)][SerializeField] private float _fieldOfView;
        [Min(0f)][SerializeField] private float _riseDuration;
        [Min(0f)][SerializeField] private float _fallDuration;

        public ParkourSubStateType_y State => _state;
        public float TriggerProgress => _triggerProgress;
        public float Trauma => _trauma;
        public float Pitch => _pitch;
        public float Roll => _roll;
        public float Distance => _distance;
        public float FieldOfView => _fieldOfView;
        public float RiseDuration => _riseDuration;
        public float FallDuration => _fallDuration;
        public bool HasKick => _pitch != 0f || _roll != 0f || _distance != 0f || _fieldOfView != 0f;

        public static CameraImpactBeat_y Create(
            ParkourSubStateType_y state,
            float triggerProgress,
            float trauma,
            float pitch,
            float roll,
            float distance,
            float fieldOfView,
            float riseDuration,
            float fallDuration)
        {
            return new CameraImpactBeat_y
            {
                _state = state,
                _triggerProgress = triggerProgress,
                _trauma = trauma,
                _pitch = pitch,
                _roll = roll,
                _distance = distance,
                _fieldOfView = fieldOfView,
                _riseDuration = riseDuration,
                _fallDuration = fallDuration,
            };
        }
    }
}
