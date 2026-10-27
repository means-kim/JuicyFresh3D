using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    // 질주 애니메이션 위상에 맞춰 발 접지 리듬으로 카메라가 위아래, 좌우로 미세하게 흔들리는 값을 계산한다.
    public sealed class ParkourCameraStride_y : MonoBehaviour
    {
        // Anim_Sprint 실측 위상: 골반 최저(두 번)와 왼발 접지 중앙.
        private const float LowestHipsPhase = 0.469f;
        private const float LeftFootContactPhase = 0.31f;

        [SerializeField] private ParkourAnimationDriver _driver;
        [SerializeField] private float _heightAmplitude = 0.012f;
        [SerializeField] private float _rollAmplitude = 0.4f;
        [SerializeField] private float _weightSharpness = 6f;

        private float _weight;
        private float _lastPhase;

        public float HeightOffset { get; private set; }
        public float RollOffset { get; private set; }

        public void UpdateStride(float speedRatio, float deltaTime)
        {
            var isInSprintCycle = _driver.TryGetSprintCyclePhase(out float phase);
            if (isInSprintCycle)
            {
                _lastPhase = phase;
            }

            var targetWeight = isInSprintCycle ? speedRatio : 0f;
            _weight = Mathf.Lerp(_weight, targetWeight, 1f - Mathf.Exp(-_weightSharpness * deltaTime));

            // 한 루프에 걸음이 두 번이라 높이는 위상의 두 배 주기로 움직인다.
            HeightOffset = -_heightAmplitude * _weight
                           * Mathf.Cos(4f * Mathf.PI * (_lastPhase - LowestHipsPhase));
            RollOffset = _rollAmplitude * _weight
                         * Mathf.Cos(2f * Mathf.PI * (_lastPhase - LeftFootContactPhase));
        }
    }
}
