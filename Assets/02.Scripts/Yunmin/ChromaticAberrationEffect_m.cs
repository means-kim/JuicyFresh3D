using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class ChromaticAberrationEffect_m : MonoBehaviour
{
    [Header("Chromatic Aberration")]
    [SerializeField, Range(0f, 1f)] private float _maxIntensity = 0.25f;

    private ChromaticAberration _chromaticAberration;

    internal void Initialize(VolumeProfile profile)
    {
        Clear();
        if (!profile.TryGet(out _chromaticAberration))
        {
            _chromaticAberration = profile.Add<ChromaticAberration>();
        }

        _chromaticAberration.intensity.overrideState = true;
        _chromaticAberration.active = false;
    }

    internal void ApplyStrength(float strength)
    {
        if (_chromaticAberration == null) return;

        strength = Mathf.Clamp01(strength);

        _chromaticAberration.intensity.value = Mathf.Clamp01(_maxIntensity) * strength;
        _chromaticAberration.active = isActiveAndEnabled && strength > 0f;
    }

    internal void Clear()
    {
        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = 0f;
            _chromaticAberration.active = false;
        }
    }

    private void OnDisable()
    {
        Clear();
    }
}
