using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Volume))]
[RequireComponent(typeof(VignetteEffect_m), typeof(DepthOfFieldEffect_m), typeof(ChromaticAberrationEffect_m))]
public sealed class PostProcessEffectController_m : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera _effectCamera;

    [Header("Shared Timing")]
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.4f;
    [SerializeField, Min(0f)] private float _holdDuration = 2f;
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.8f;

    [Header("Near Death")]
    [Tooltip("빈사 비네팅. 비워두면 자식 오브젝트에서 찾습니다.")]
    [SerializeField] private NearDeathEffect_m _nearDeathEffect;
    [Tooltip("빈사 화면 깨짐. 비워두면 자신과 자식 오브젝트에서 찾습니다.")]
    [SerializeField] private ScreenCrackEffect_m _screenCrackEffect;

    private enum Phase { Idle, FadeIn, Hold, FadeOut }

    private Volume _volume;
    private VolumeProfile _runtimeProfile;
    private VolumeProfile _nearDeathRuntimeProfile;
    private VignetteEffect_m _vignette;
    private DepthOfFieldEffect_m _depthOfField;
    private ChromaticAberrationEffect_m _chromaticAberration;
    private Phase _phase;
    private float _strength;
    private float _phaseElapsed;
    private float _phaseStartStrength;

    public bool IsPlaying => _phase != Phase.Idle;
    public float CurrentStrength => _strength;
    public bool IsNearDeath => _nearDeathEffect != null && _nearDeathEffect.IsPlaying;

    [ContextMenu("연출 재생")]
    public void PlayDodgeEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        if (!EnsureInitialized())
        {
            Debug.LogWarning("Assign an effect camera before playing post-processing.", this);
            return;
        }

        // Preserve the displayed strength when replacing any previous playback.
        _phaseStartStrength = _strength;
        _phaseElapsed = 0f;
        _phase = _strength >= 1f ? Phase.Hold : Phase.FadeIn;

        AdvanceTime(0f);
        ApplyDodgeEffects();
    }

    [ContextMenu("연출 정지")]
    public void StopDodgeEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !IsPlaying || _phase == Phase.FadeOut)
        {
            return;
        }

        _phaseStartStrength = _strength;
        _phaseElapsed = 0f;
        _phase = Phase.FadeOut;
        AdvanceTime(0f);
        ApplyDodgeEffects();
    }

    [ContextMenu("빈사 연출 재생")]
    public void PlayNearDeathEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        // 비네팅과 화면 깨짐은 서로 독립적으로 재생합니다.
        if (EnsureNearDeathInitialized())
        {
            _nearDeathEffect.Play();
        }
        else
        {
            Debug.LogWarning("Add a NearDeathEffect_m child before playing near death vignette.", this);
        }

        if (_screenCrackEffect != null)
        {
            _screenCrackEffect.Play();
        }
    }

    [ContextMenu("빈사 연출 정지")]
    public void StopNearDeathEffect()
    {
        if (_nearDeathEffect != null)
        {
            _nearDeathEffect.Stop();
        }
        if (_screenCrackEffect != null)
        {
            _screenCrackEffect.Stop();
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveComponents();
        Clear();
        EnsureInitialized();
    }

    private void LateUpdate()
    {
        if (IsPlaying)
        {
            if (EnsureInitialized())
            {
                AdvanceTime(Time.deltaTime);
                ApplyDodgeEffects();
            }
            else
            {
                ClearDodge();
            }
        }

        if (_nearDeathEffect != null && _nearDeathEffect.IsVisible)
        {
            // 박동은 슬로우 연출과 무관하게 일정한 속도로 뜁니다.
            _nearDeathEffect.Tick(Time.unscaledDeltaTime);
        }

        if (_screenCrackEffect != null && _screenCrackEffect.IsAnimating)
        {
            _screenCrackEffect.Tick(Time.unscaledDeltaTime);
        }
    }

    private void AdvanceTime(float deltaTime)
    {
        // Carry unused time across phases, including phases with zero duration.
        while (_phase != Phase.Idle)
        {
            float duration = _phase == Phase.FadeIn ? Mathf.Max(0f, _fadeInDuration) : (_phase == Phase.Hold ? Mathf.Max(0f, _holdDuration) : Mathf.Max(0f, _fadeOutDuration));
            float remaining = Mathf.Max(0f, duration - _phaseElapsed);

            if (duration > 0f && deltaTime < remaining)
            {
                _phaseElapsed += deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, _phaseElapsed / duration);
                if (_phase == Phase.FadeIn)
                {
                    _strength = Mathf.Lerp(_phaseStartStrength, 1f, progress);
                }
                else if (_phase == Phase.FadeOut)
                {
                    _strength = Mathf.Lerp(_phaseStartStrength, 0f, progress);
                }

                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime - remaining);
            _phaseElapsed = 0f;
            switch (_phase)
            {
                case Phase.FadeIn:
                    _strength = 1f;
                    _phase = Phase.Hold;
                    break;
                case Phase.Hold:
                    _phaseStartStrength = _strength;
                    _phase = Phase.FadeOut;
                    break;
                case Phase.FadeOut:
                    _strength = 0f;
                    _phase = Phase.Idle;
                    break;
            }
        }
    }

    private bool EnsureInitialized()
    {
        ResolveComponents();
        if (_effectCamera == null)
        {
            _effectCamera = Camera.main;
        }
        if (_effectCamera == null || _volume == null) return false;

        if (_runtimeProfile == null)
        {
            _runtimeProfile = CreateRuntimeProfile(_volume, " Post Process (Runtime)");

            _vignette.Initialize(_runtimeProfile);
            _depthOfField.Initialize(_runtimeProfile, _effectCamera);
            _chromaticAberration.Initialize(_runtimeProfile);
        }

        return true;
    }

    private bool EnsureNearDeathInitialized()
    {
        ResolveComponents();
        if (_nearDeathEffect == null || _nearDeathEffect.TargetVolume == null) return false;

        if (_nearDeathRuntimeProfile == null)
        {
            Volume nearDeathVolume = _nearDeathEffect.TargetVolume;

            // 회피 Volume보다 우선순위를 높여 빈사 중엔 빨간 비네팅이 이깁니다.
            if (_volume != null && nearDeathVolume.priority <= _volume.priority)
            {
                nearDeathVolume.priority = _volume.priority + 1f;
            }

            _nearDeathRuntimeProfile = CreateRuntimeProfile(nearDeathVolume, " Near Death (Runtime)");
            _nearDeathEffect.Initialize(_nearDeathRuntimeProfile);
        }

        return true;
    }

    private VolumeProfile CreateRuntimeProfile(Volume volume, string suffix)
    {
        volume.weight = 0f;
        volume.isGlobal = true;

        // Volume.profile deep-copies the shared profile and its components.
        VolumeProfile profile = volume.profile;
        profile.name = name + suffix;
        profile.hideFlags = HideFlags.DontSave;

        foreach (VolumeComponent component in profile.components)
        {
            component.hideFlags = HideFlags.DontSave;
        }

        return profile;
    }

    private void ResolveComponents()
    {
        if (_volume == null)
        {
            _volume = GetComponent<Volume>();
        }
        if (_vignette == null)
        {
            _vignette = GetComponent<VignetteEffect_m>();
        }
        if (_depthOfField == null)
        {
            _depthOfField = GetComponent<DepthOfFieldEffect_m>();
        }
        if (_chromaticAberration == null)
        {
            _chromaticAberration = GetComponent<ChromaticAberrationEffect_m>();
        }
        if (_nearDeathEffect == null)
        {
            _nearDeathEffect = GetComponentInChildren<NearDeathEffect_m>(true);
        }
        if (_screenCrackEffect == null)
        {
            _screenCrackEffect = GetComponentInChildren<ScreenCrackEffect_m>(true);
        }
    }

    private void ApplyDodgeEffects()
    {
        if (_runtimeProfile == null || _volume == null) return;

        _vignette.ApplyStrength(_strength);
        _depthOfField.ApplyStrength(_strength, _effectCamera);
        _chromaticAberration.ApplyStrength(_strength);
        _volume.weight = IsPlaying && _strength > 0f ? 1f : 0f;
    }

    private void Clear()
    {
        ClearDodge();
        ClearNearDeath();
    }

    private void ClearNearDeath()
    {
        if (_nearDeathEffect != null)
        {
            _nearDeathEffect.Clear();
        }
        if (_screenCrackEffect != null)
        {
            _screenCrackEffect.Clear();
        }
    }

    private void ClearDodge()
    {
        _phase = Phase.Idle;
        _strength = 0f;
        _phaseElapsed = 0f;
        _phaseStartStrength = 0f;

        if (_volume != null)
        {
            _volume.weight = 0f;
        }
        if (_vignette != null)
        {
            _vignette.Clear();
        }
        if (_depthOfField != null)
        {
            _depthOfField.Clear();
        }
        if (_chromaticAberration != null)
        {
            _chromaticAberration.Clear();
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnDestroy()
    {
        Clear();

        DestroyRuntimeProfile(_volume, _runtimeProfile);
        _runtimeProfile = null;
        DestroyRuntimeProfile(_nearDeathEffect != null ? _nearDeathEffect.TargetVolume : null, _nearDeathRuntimeProfile);
        _nearDeathRuntimeProfile = null;
    }

    private static void DestroyRuntimeProfile(Volume volume, VolumeProfile profile)
    {
        if (profile == null) return;
        if (volume != null) volume.profile = null;

        foreach (VolumeComponent component in profile.components)
        {
            Destroy(component);
        }

        Destroy(profile);
    }
}
