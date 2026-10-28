using System.Collections.Generic;
using UnityEngine;

// Time.timeScale과 Time.fixedDeltaTime은 이 컴포넌트만 변경합니다.
[DisallowMultipleComponent]
public sealed class TimeScaleController_m : MonoBehaviour
{
    private sealed class Request
    {
        public TimeSlowSettings_m Settings;
        public float Elapsed;
    }

    private static TimeScaleController_m _instance;
    private static bool _isQuitting;

    private readonly List<Request> _requests = new();
    private float _baseFixedDeltaTime;

    public static void Play(TimeSlowSettings_m settings)
    {
        if (settings == null || !Application.isPlaying || _isQuitting) return;

        if (_instance == null)
        {
            _instance = FindAnyObjectByType<TimeScaleController_m>();
        }

        if (_instance == null)
        {
            _instance = new GameObject(nameof(TimeScaleController_m)).AddComponent<TimeScaleController_m>();
        }

        // 비활성 상태면 Update가 돌지 않아 감속이 풀리지 않으므로 적용하지 않습니다.
        if (!_instance.isActiveAndEnabled) return;

        _instance.AddRequest(settings);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Domain Reload를 끈 상태에서도 이전 플레이의 값이 남지 않게 합니다.
        _instance = null;
        _isQuitting = false;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        _baseFixedDeltaTime = Time.fixedDeltaTime;
        Application.quitting += HandleQuitting;
    }

    private void OnDisable()
    {
        _requests.Clear();
        ApplyTimeScale(1f);
    }

    private void OnDestroy()
    {
        if (_instance != this) return;

        Application.quitting -= HandleQuitting;
        _instance = null;
    }

    private void HandleQuitting()
    {
        _isQuitting = true;
    }

    private void AddRequest(TimeSlowSettings_m settings)
    {
        // 같은 연출이 다시 요청되면 겹치지 않고 처음부터 다시 재생합니다.
        foreach (Request request in _requests)
        {
            if (request.Settings == settings)
            {
                request.Elapsed = 0f;
                ApplyTimeScale(EvaluateTimeScale());
                return;
            }
        }

        _requests.Add(new Request { Settings = settings });
        ApplyTimeScale(EvaluateTimeScale());
    }

    private void Update()
    {
        if (_requests.Count == 0) return;

        // 감속의 영향을 받지 않도록 실제 시간으로 진행합니다.
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            Request request = _requests[i];
            request.Elapsed += deltaTime;

            if (request.Elapsed >= request.Settings.Duration)
            {
                _requests.RemoveAt(i);
            }
        }

        ApplyTimeScale(EvaluateTimeScale());
    }

    private float EvaluateTimeScale()
    {
        if (_requests.Count == 0) return 1f;

        // 서로 다른 연출이 겹치면 가장 느린 값을 사용합니다.
        float timeScale = float.MaxValue;

        foreach (Request request in _requests)
        {
            float normalizedTime = request.Elapsed / request.Settings.Duration;
            timeScale = Mathf.Min(timeScale, request.Settings.Evaluate(normalizedTime));
        }

        return timeScale;
    }

    private void ApplyTimeScale(float timeScale)
    {
        if (_baseFixedDeltaTime <= 0f) return;

        Time.timeScale = timeScale;

        // timeScale이 0이면 물리가 멈추므로 fixedDeltaTime은 기본값을 유지합니다.
        Time.fixedDeltaTime = timeScale > 0f ? _baseFixedDeltaTime * timeScale : _baseFixedDeltaTime;
    }
}
