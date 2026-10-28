using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class CrossFlashEffect_m : MonoBehaviour
{
    private static readonly int EffectColorId = Shader.PropertyToID("_EffectColor");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
    private static readonly int ArmFalloffId = Shader.PropertyToID("_ArmFalloff");
    private static readonly int HorizontalStretchId = Shader.PropertyToID("_HorizontalStretch");

    [Header("Model")]
    [Tooltip("Assign the body and head renderers, excluding effect renderers.")]
    [SerializeField] private SkinnedMeshRenderer[] _modelRenderers;
    [Tooltip("Uses the MainCamera when no camera is assigned.")]
    [SerializeField] private Camera _effectCamera;

    [Header("Cross Flash")]
    [Tooltip("Optional position anchor. Follows this Transform while playing; uses the model bounds center when unassigned. Camera-facing depth correction still keeps the flash in front of the model.")]
    [SerializeField] private Transform _flashOrigin;
    [SerializeField] private Material _flashMaterial;
    [Tooltip("Flash size as a multiple of the character's current bounds height.")]
    [SerializeField, Min(0.01f)] private float _flashSize = 1.25f;
    [Tooltip("Final horizontal length as a multiple of the starting length. Reached during fade-out.")]
    [SerializeField, Min(1f)] private float _flashEndHorizontalStretch = 3f;
    [SerializeField, Range(0.005f, 0.25f)] private float _flashLineWidth = 0.035f;
    [SerializeField, Range(0.2f, 8f)] private float _flashArmFalloff = 2f;
    [SerializeField, Min(0f)] private float _flashBrightness = 4f;
    [Tooltip("Extra distance toward the camera from the front of the model bounds.")]
    [SerializeField, Min(0f)] private float _flashDepthOffset = 0.02f;

    [Header("Timing (Seconds)")]
    [SerializeField, Min(0f)] private float _flashFadeInDuration = 0.1f;
    [SerializeField, Min(0f)] private float _flashHoldDuration = 2f;
    [Tooltip("Time to stretch horizontally to the final length while fading out.")]
    [SerializeField, Min(0f)] private float _flashFadeOutDuration = 1f;

    private Color _effectColor;

    private GameObject _flashObject;
    private Mesh _flashMesh;
    private MeshRenderer _flashRenderer;
    private MaterialPropertyBlock _flashProperties;
    private float _elapsedTime;
    private float _opacity;
    private float _horizontalStretch = 1f;
    private bool _isFlashPlaying;

    internal void PlayEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;

        if (_flashMaterial == null || !TryGetModelBounds(out Bounds bounds))
        {
            Debug.LogWarning("Assign a flash material and active model renderers before playing the effect.", this);
            return;
        }

        Camera effectCamera = ResolveCamera();

        if (effectCamera == null)
        {
            Debug.LogWarning("Assign an effect camera or tag a camera MainCamera before playing the effect.", this);
            return;
        }

        EnsureFlashObject();

        _elapsedTime = 0f;
        _isFlashPlaying = true;
        _horizontalStretch = 1f;
        _opacity = EvaluateFlashOpacity(0f);
        _flashObject.SetActive(true);

        UpdateFlash(bounds, effectCamera);
    }

    internal void StopEffect()
    {
        HideFlash();
    }

    internal void SetEffectColor(Color color)
    {
        bool colorChanged = _effectColor != color;

        _effectColor = color;

        if (colorChanged && _flashRenderer != null)
        {
            ApplyFlashProperties();
        }
    }

    private void LateUpdate()
    {
        if (!_isFlashPlaying) return;

        _elapsedTime += Time.deltaTime;
        float duration = Mathf.Max(0f, _flashFadeInDuration) + Mathf.Max(0f, _flashHoldDuration) + Mathf.Max(0f, _flashFadeOutDuration);

        if (_elapsedTime >= duration)
        {
            HideFlash();
            return;
        }

        _opacity = EvaluateFlashOpacity(_elapsedTime);

        float stretchProgress = EvaluateFadeOutProgress(_elapsedTime);

        _horizontalStretch = Mathf.Lerp(1f, Mathf.Max(1f, _flashEndHorizontalStretch), stretchProgress);

        Camera effectCamera = ResolveCamera();

        if (effectCamera == null || !TryGetModelBounds(out Bounds bounds))
        {
            _flashRenderer.enabled = false;
            return;
        }

        UpdateFlash(bounds, effectCamera);
    }

    private float EvaluateFlashOpacity(float elapsedTime)
    {
        float fadeInDuration = Mathf.Max(0f, _flashFadeInDuration);

        if (fadeInDuration > 0f && elapsedTime < fadeInDuration)
        {
            return Mathf.SmoothStep(0f, 1f, elapsedTime / fadeInDuration);
        }

        return 1f - EvaluateFadeOutProgress(elapsedTime);
    }

    private float EvaluateFadeOutProgress(float elapsedTime)
    {
        float fadeOutTime = elapsedTime - Mathf.Max(0f, _flashFadeInDuration) - Mathf.Max(0f, _flashHoldDuration);
        float fadeOutDuration = Mathf.Max(0f, _flashFadeOutDuration);

        return (fadeOutDuration > 0f) ? Mathf.SmoothStep(0f, 1f, fadeOutTime / fadeOutDuration) : (fadeOutTime >= 0f ? 1f : 0f);
    }

    private Camera ResolveCamera()
    {
        return _effectCamera != null ? _effectCamera : Camera.main;
    }

    private bool TryGetModelBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (_modelRenderers == null) return false;

        foreach (SkinnedMeshRenderer modelRenderer in _modelRenderers)
        {
            if (modelRenderer == null || !modelRenderer.enabled
                || !modelRenderer.gameObject.activeInHierarchy || modelRenderer.sharedMesh == null)
            {
                continue;
            }

            if (hasBounds)
            {
                bounds.Encapsulate(modelRenderer.bounds);
            }
            else
            {
                bounds = modelRenderer.bounds;
            }

            hasBounds = true;
        }

        return hasBounds;
    }

    private void EnsureFlashObject()
    {
        if (_flashObject != null) return;

        _flashMesh = new Mesh
        {
            name = "Player Cross Flash Quad",
            hideFlags = HideFlags.DontSave,
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
            triangles = new[] { 0, 2, 1, 0, 3, 2 }
        };

        _flashMesh.RecalculateBounds();

        // Keep the billboard in world space so non-uniform player scale cannot distort it.
        _flashObject = new GameObject(name + " Cross Flash (Runtime)")
        {
            hideFlags = HideFlags.DontSave,
            layer = gameObject.layer
        };

        SceneManager.MoveGameObjectToScene(_flashObject, gameObject.scene);

        _flashObject.AddComponent<MeshFilter>().sharedMesh = _flashMesh;

        _flashRenderer = _flashObject.AddComponent<MeshRenderer>();
        _flashRenderer.sharedMaterial = _flashMaterial;
        _flashRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _flashRenderer.receiveShadows = false;
        _flashRenderer.lightProbeUsage = LightProbeUsage.Off;
        _flashRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _flashRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        _flashProperties = new MaterialPropertyBlock();
    }

    private void UpdateFlash(Bounds bounds, Camera effectCamera)
    {
        Transform cameraTransform = effectCamera.transform;
        Vector3 originPosition = _flashOrigin != null ? _flashOrigin.position : bounds.center;

        float centerDepth = Vector3.Dot(originPosition - cameraTransform.position, cameraTransform.forward);
        float minimumDepth = effectCamera.nearClipPlane + 0.01f;

        _flashRenderer.enabled = (centerDepth > minimumDepth);
        if (!_flashRenderer.enabled) return;

        Vector3 towardCamera = (effectCamera.orthographic) ? -cameraTransform.forward : (cameraTransform.position - originPosition).normalized;
        Vector3 absoluteDirection = new Vector3(Mathf.Abs(towardCamera.x), Mathf.Abs(towardCamera.y), Mathf.Abs(towardCamera.z));

        // Move along the anchor's view ray so its screen position stays fixed.
        float modelFrontDistance = Vector3.Dot(bounds.center - originPosition, towardCamera) + Vector3.Dot(bounds.extents, absoluteDirection);
        float frontDistance = Mathf.Max(0f, modelFrontDistance) + Mathf.Max(0f, _flashDepthOffset);
        float depthFactor = Vector3.Dot(towardCamera, -cameraTransform.forward);

        frontDistance = Mathf.Min(frontDistance, (centerDepth - minimumDepth) / depthFactor);

        Vector3 flashPosition = originPosition + towardCamera * frontDistance;
        float size = Mathf.Max(0.01f, bounds.size.y) * Mathf.Max(0.01f, _flashSize);

        // Preserve the projected size when moving a perspective billboard in front of the model.
        if (!effectCamera.orthographic)
        {
            size *= (centerDepth - frontDistance * depthFactor) / centerDepth;
        }

        _flashObject.transform.SetPositionAndRotation(flashPosition, cameraTransform.rotation);
        _flashObject.transform.localScale = new Vector3(size * _horizontalStretch, size, size);
        _flashRenderer.sharedMaterial = _flashMaterial;

        ApplyFlashProperties();
    }

    private void ApplyFlashProperties()
    {
        _flashProperties.SetColor(EffectColorId, _effectColor);
        _flashProperties.SetFloat(BrightnessId, Mathf.Max(0f, _flashBrightness));
        _flashProperties.SetFloat(OpacityId, _opacity);
        _flashProperties.SetFloat(LineWidthId, Mathf.Clamp(_flashLineWidth, 0.005f, 0.25f));
        _flashProperties.SetFloat(ArmFalloffId, Mathf.Clamp(_flashArmFalloff, 0.2f, 8f));
        _flashProperties.SetFloat(HorizontalStretchId, _horizontalStretch);
        _flashRenderer.SetPropertyBlock(_flashProperties);
    }

    private void HideFlash()
    {
        _isFlashPlaying = false;
        _opacity = 0f;
        _horizontalStretch = 1f;

        if (_flashObject != null)
        {
            _flashObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnDestroy()
    {
        Clear();
    }

    internal void Clear()
    {
        HideFlash();
        if (_flashObject != null) Destroy(_flashObject);
        if (_flashMesh != null) Destroy(_flashMesh);

        _flashObject = null;
        _flashMesh = null;
        _flashRenderer = null;
        _flashProperties = null;
    }
}
