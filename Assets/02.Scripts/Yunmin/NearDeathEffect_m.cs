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

    [Header("Heartbeat")]
    [Tooltip("박동 한 주기(초). 슬로우 연출 중에도 일정하도록 unscaled time을 씁니다.")]
    [SerializeField, Min(0.05f)] private float _heartbeatPeriod = 0.9f;
    [Tooltip("X는 한 주기의 진행도(0~1), Y는 비네팅 강도(0~1)입니다. 시작/종료 페이드는 별도로 적용됩니다.")]
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
        // 가장자리의 공간적 번짐은 고정하고, 박동 강도는 곡선으로만 제어합니다.
        _vignette.smoothness.Override(0.4f);
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
        float intensity = _fade * pulse;

        _vignette.color.value = _color;
        _vignette.intensity.value = intensity;
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
