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

    private enum Phase { Idle, FadeIn, Hold, FadeOut }

    private Volume _volume;
    private VolumeProfile _runtimeProfile;
    private VignetteEffect_m _vignette;
    private DepthOfFieldEffect_m _depthOfField;
    private ChromaticAberrationEffect_m _chromaticAberration;
    private Phase _phase;
    private float _strength;
    private float _phaseElapsed;
    private float _phaseStartStrength;

    public bool IsPlaying => _phase != Phase.Idle;
    public float CurrentStrength => _strength;

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
        if (!IsPlaying) return;
        if (!EnsureInitialized())
        {
            Clear();
            return;
        }

        AdvanceTime(Time.deltaTime);
        ApplyDodgeEffects();
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
            _volume.weight = 0f;
            _volume.isGlobal = true;

            // Volume.profile deep-copies the shared profile and its components.

            _runtimeProfile = _volume.profile;
            _runtimeProfile.name = name + " Post Process (Runtime)";
            _runtimeProfile.hideFlags = HideFlags.DontSave;

            foreach (VolumeComponent component in _runtimeProfile.components)
            {
                component.hideFlags = HideFlags.DontSave;
            }

            _vignette.Initialize(_runtimeProfile);
            _depthOfField.Initialize(_runtimeProfile, _effectCamera);
            _chromaticAberration.Initialize(_runtimeProfile);
        }

        return true;
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

        if (_runtimeProfile == null) return;
        if (_volume != null) _volume.profile = null;

        foreach (VolumeComponent component in _runtimeProfile.components)
        {
            Destroy(component);
        }


        Destroy(_runtimeProfile);
        _runtimeProfile = null;
    }
}
