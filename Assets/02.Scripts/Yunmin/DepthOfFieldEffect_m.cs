using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class DepthOfFieldEffect_m : MonoBehaviour
{
    [Header("Focus")]
    [Tooltip("Focus follows this transform's depth along the camera's forward axis.")]
    [SerializeField] private Transform _focusTarget;

    [Header("Maximum Blur")]
    [SerializeField, Range(1f, 300f)] private float _maxFocalLength = 75f;
    [Tooltip("Smaller f-numbers produce a shallower depth of field.")]
    [SerializeField, Range(1f, 32f)] private float _aperture = 5.6f;

    private DepthOfField _depthOfField;
    private float _lastValidFocusDistance = 7f;

    internal void Initialize(VolumeProfile profile, Camera effectCamera)
    {
        Clear();
        if (!profile.TryGet(out _depthOfField))
        {
            _depthOfField = profile.Add<DepthOfField>();
        }

        _depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
        _depthOfField.focusDistance.overrideState = true;
        _depthOfField.focalLength.overrideState = true;
        _depthOfField.aperture.overrideState = true;

        UpdateFocusDistance(effectCamera);

        _depthOfField.active = false;
    }

    internal void ApplyStrength(float strength, Camera effectCamera)
    {
        if (_depthOfField == null) return;

        strength = Mathf.Clamp01(strength);
        UpdateFocusDistance(effectCamera);

        _depthOfField.focalLength.value = Mathf.Lerp(1f, Mathf.Clamp(_maxFocalLength, 1f, 300f), strength);
        _depthOfField.aperture.value = Mathf.Clamp(_aperture, 1f, 32f);
        _depthOfField.active = isActiveAndEnabled && strength > 0f;
    }

    private void UpdateFocusDistance(Camera effectCamera)
    {
        if (_focusTarget != null && effectCamera != null)
        {
            Vector3 cameraToTarget = _focusTarget.position - effectCamera.transform.position;
            float depth = Vector3.Dot(cameraToTarget, effectCamera.transform.forward);

            if (depth > 0f && !float.IsNaN(depth) && !float.IsInfinity(depth))
            {
                _lastValidFocusDistance = depth;
            }
        }

        // URP divides by (focus distance - focal length in metres).
        // Keep even a very close target beyond the longest supported lens.
        float minimumDistance = Mathf.Clamp(_maxFocalLength, 1f, 300f) * 0.001f + 0.01f;
        _depthOfField.focusDistance.value = Mathf.Max(_lastValidFocusDistance, minimumDistance);
    }

    internal void Clear()
    {
        if (_depthOfField != null)
        {
            _depthOfField.focalLength.value = 1f;
            _depthOfField.active = false;
        }
    }

    private void OnDisable()
    {
        Clear();
    }
}
