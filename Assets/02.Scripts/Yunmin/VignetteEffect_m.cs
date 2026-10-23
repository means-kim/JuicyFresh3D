using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class VignetteEffect_m : MonoBehaviour
{
    [Header("Vignette")]
    [SerializeField, ColorUsage(false)] private Color _color = Color.black;
    [SerializeField, Range(0f, 1f)] private float _maxIntensity = 0.35f;
    [SerializeField, Range(0.01f, 1f)] private float _smoothness = 0.5f;

    private Vignette _vignette;
    private float _strength;

    public void SetColor(Color color)
    {
        _color = color;
        ApplyStrength(_strength);
    }

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

    internal void ApplyStrength(float strength)
    {
        _strength = Mathf.Clamp01(strength);

        if (_vignette == null)
        {
            return;
        }

        _vignette.color.value = _color;
        _vignette.intensity.value = Mathf.Clamp01(_maxIntensity) * _strength;
        _vignette.smoothness.value = Mathf.Clamp(_smoothness, 0.01f, 1f);
        _vignette.active = isActiveAndEnabled && _strength > 0f;
    }

    internal void Clear()
    {
        _strength = 0f;
        if (_vignette != null)
        {
            _vignette.intensity.value = 0f;
            _vignette.active = false;
        }
    }

    private void OnDisable()
    {
        Clear();
    }
}
