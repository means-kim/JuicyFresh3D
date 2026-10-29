using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 빈사 상태의 심장 박동 비네팅입니다. 회피 비네팅과 겹치지 않도록 전용 Volume과 함께 자식 오브젝트에 둡니다.
// 재생/정지는 PostProcessEffectController_m을 통해 호출합니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Volume))]
public sealed class NearDeathEffect_m : MonoBehaviour
{
    [Header("Vignette")]
    [SerializeField, ColorUsage(false)] private Color _color = new Color(0.6f, 0f, 0f, 1f);
    [Tooltip("박동이 가장 약할 때의 비네팅 강도입니다.")]
    [SerializeField, Range(0f, 1f)] private float _minIntensity = 0.2f;
    [Tooltip("박동이 가장 강할 때의 비네팅 강도입니다.")]
    [SerializeField, Range(0f, 1f)] private float _maxIntensity = 0.45f;
    [SerializeField, Range(0.01f, 1f)] private float _smoothness = 0.4f;

    [Header("Heartbeat")]
    [Tooltip("박동 한 주기(초). 슬로우 연출 중에도 일정하도록 unscaled time을 씁니다.")]
    [SerializeField, Min(0.05f)] private float _heartbeatPeriod = 0.9f;
    [Tooltip("한 주기(0~1) 동안의 박동 곡선. 0이면 최소 강도, 1이면 최대 강도입니다.")]
    [SerializeField]
    private AnimationCurve _heartbeatCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.1f, 1f),
        new Keyframe(0.22f, 0.35f),
        new Keyframe(0.32f, 0.8f),
        new Keyframe(0.6f, 0f),
        new Keyframe(1f, 0f));

    [Header("Fade")]
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.3f;
    [SerializeField, Min(0f)] private float _fadeOutDuration = 1f;

    private Volume _volume;
    private Vignette _vignette;
    private bool _isPlaying;
    private float _fade;
    private float _heartbeatTime;

    public Volume TargetVolume => _volume != null ? _volume : _volume = GetComponent<Volume>();
    public bool IsPlaying => _isPlaying;
    public bool IsVisible => _isPlaying || _fade > 0f;

    internal void Initialize(VolumeProfile profile)
    {
        Clear();
        if (!profile.TryGet(out _vignette))
        {
            _vignette = profile.Add<Vignette>();
        }

        _vignette.color.overrideState = true;
        _vignette.intensity.overrideState = true;
        _vignette.smoothness.overrideState = true;
        _vignette.center.Override(new Vector2(0.5f, 0.5f));
        _vignette.rounded.Override(false);
        _vignette.active = false;
    }

    internal void Play()
    {
        if (!IsVisible)
        {
            _heartbeatTime = 0f;
        }

        _isPlaying = true;
        Apply();
    }

    internal void Stop()
    {
        _isPlaying = false;
    }

    internal void Tick(float deltaTime)
    {
        float target = _isPlaying ? 1f : 0f;
        float duration = _isPlaying ? _fadeInDuration : _fadeOutDuration;
        _fade = duration > 0f ? Mathf.MoveTowards(_fade, target, deltaTime / duration) : target;
        _heartbeatTime = Mathf.Repeat(_heartbeatTime + deltaTime, _heartbeatPeriod);
        Apply();
    }

    private void Apply()
    {
        if (_vignette == null)
        {
            return;
        }

        float pulse = Mathf.Clamp01(_heartbeatCurve.Evaluate(_heartbeatTime / _heartbeatPeriod));
        float intensity = _fade * Mathf.Lerp(_minIntensity, _maxIntensity, pulse);

        _vignette.color.value = _color;
        _vignette.intensity.value = intensity;
        _vignette.smoothness.value = Mathf.Clamp(_smoothness, 0.01f, 1f);
        _vignette.active = isActiveAndEnabled && intensity > 0f;
        TargetVolume.weight = intensity > 0f ? 1f : 0f;
    }

    internal void Clear()
    {
        _isPlaying = false;
        _fade = 0f;
        _heartbeatTime = 0f;

        if (_vignette != null)
        {
            _vignette.intensity.value = 0f;
            _vignette.active = false;
        }
        if (TargetVolume != null)
        {
            TargetVolume.weight = 0f;
        }
    }

    private void OnDisable()
    {
        Clear();
    }
}
