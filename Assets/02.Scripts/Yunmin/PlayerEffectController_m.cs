using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class PlayerEffectController_m : MonoBehaviour
{
    private static readonly int EffectColorId = Shader.PropertyToID("_EffectColor");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
    private static readonly int ArmFalloffId = Shader.PropertyToID("_ArmFalloff");
    private static readonly int HorizontalStretchId = Shader.PropertyToID("_HorizontalStretch");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [Header("Shared Settings")]
    [SerializeField, ColorUsage(true, true)]
    private Color _effectColor = new Color(0.35f, 1f, 0.85f, 1f);
    [Tooltip("Assign the body and head renderers, excluding effect renderers.")]
    [SerializeField] private SkinnedMeshRenderer[] _modelRenderers;
    [Tooltip("Uses the MainCamera when no camera is assigned.")]
    [SerializeField] private Camera _effectCamera;

    [Header("Cross Flash")]
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

    [Header("Silhouette Outline")]
    [SerializeField, InspectorName("Outline Material")] private Material _rimMaterial;
    [Tooltip("Marks the original silhouette before drawing the outline. Uses stencil bit 0.")]
    [SerializeField] private Material _outlineMaskMaterial;
    [Tooltip("Thickness outside the silhouette in render-target pixels.")]
    [SerializeField, Range(0f, 10f), InspectorName("Outline Width (Pixels)")]
    private float _rimWidth = 2f;
    [SerializeField, Min(0f), InspectorName("Outline Brightness")]
    private float _rimBrightness = 3f;
    [Tooltip("Time to reach full outline brightness. Zero lights it immediately.")]
    [SerializeField, Min(0f)] private float _outlineFadeInDuration = 0.05f;
    [Tooltip("Time to automatically fade the outline to zero after reaching full brightness.")]
    [SerializeField, Min(0f)] private float _outlineFadeOutDuration = 1.5f;

    [Header("Timing (Seconds)")]
    [SerializeField, Min(0f)] private float _flashFadeInDuration = 0.1f;
    [SerializeField, Min(0f)] private float _flashHoldDuration = 2f;
    [Tooltip("Time to stretch horizontally to the final length while fading out.")]
    [SerializeField, Min(0f)] private float _flashFadeOutDuration = 1f;
    [SerializeField, Min(0f)] private float _stopFadeDuration = 0.4f;

    private GameObject _flashObject;
    private Mesh _flashMesh;
    private MeshRenderer _flashRenderer;
    private MaterialPropertyBlock _flashProperties;
    private float _elapsedTime;
    private float _opacity;
    private float _stopStartOpacity;
    private float _horizontalStretch = 1f;
    private float _stopStartHorizontalStretch;
    private bool _isFlashPlaying;
    private bool _isStopping;

    private sealed class RimLayer
    {
        public SkinnedMeshRenderer Source;
        public SkinnedMeshRenderer Renderer;
        public SkinnedMeshRenderer MaskRenderer;
        public Material[] Materials;
        public Material[] MaskMaterials;
        public Material Material;
        public Material MaskMaterial;
    }

    private readonly List<RimLayer> _rimLayers = new List<RimLayer>();
    private MaterialPropertyBlock _rimProperties;
    private bool _isRimPlaying;
    private bool _isRimStopping;
    private float _rimOpacity;
    private float _rimElapsedTime;
    private float _rimStopElapsedTime;
    private float _rimStopStartOpacity;

    public Color EffectColor => _effectColor;

    public void PlayEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        PlayRim();
        PlayFlash();
    }

    private void PlayFlash()
    {
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
        _isStopping = false;
        _isFlashPlaying = true;
        _horizontalStretch = 1f;
        _opacity = EvaluateFlashOpacity(0f);
        _flashObject.SetActive(true);
        UpdateFlash(bounds, effectCamera);
    }

    public void StopEffect()
    {
        if (_stopFadeDuration <= 0f)
        {
            HideFlash();
            HideRim();
            return;
        }

        if (_isFlashPlaying && !_isStopping)
        {
            _stopStartOpacity = _opacity;
            _stopStartHorizontalStretch = _horizontalStretch;
            _elapsedTime = 0f;
            _isStopping = true;
        }

        if (_isRimPlaying && !_isRimStopping)
        {
            _rimStopStartOpacity = _rimOpacity;
            _rimStopElapsedTime = 0f;
            _isRimStopping = true;
        }
    }

    public void SetEffectColor(Color color)
    {
        _effectColor = color;
        if (_flashRenderer != null)
            ApplyFlashProperties();
        if (_rimProperties != null)
            ApplyRimProperties();
    }

    private void LateUpdate()
    {
        TickRim();
        TickFlash();
    }

    private void TickFlash()
    {
        if (!_isFlashPlaying)
            return;

        _elapsedTime += Time.deltaTime;
        float duration = _isStopping
            ? Mathf.Max(0f, _stopFadeDuration)
            : Mathf.Max(0f, _flashFadeInDuration) + Mathf.Max(0f, _flashHoldDuration)
                + Mathf.Max(0f, _flashFadeOutDuration);

        if (_elapsedTime >= duration)
        {
            HideFlash();
            return;
        }

        _opacity = _isStopping
            ? _stopStartOpacity * (1f - Mathf.SmoothStep(0f, 1f, _elapsedTime / duration))
            : EvaluateFlashOpacity(_elapsedTime);
        float stretchProgress = _isStopping
            ? Mathf.SmoothStep(0f, 1f, _elapsedTime / duration)
            : EvaluateFadeOutProgress(_elapsedTime);
        _horizontalStretch = Mathf.Lerp(
            _isStopping ? _stopStartHorizontalStretch : 1f,
            Mathf.Max(1f, _flashEndHorizontalStretch), stretchProgress);

        Camera effectCamera = ResolveCamera();
        if (effectCamera == null || !TryGetModelBounds(out Bounds bounds))
        {
            _flashRenderer.enabled = false;
            return;
        }

        UpdateFlash(bounds, effectCamera);
    }

    private void PlayRim()
    {
        if (_rimMaterial == null || _outlineMaskMaterial == null)
        {
            Debug.LogWarning("Assign outline and silhouette mask materials before playing the outline.", this);
            return;
        }

        EnsureRimObjects();
        if (_rimLayers.Count == 0)
        {
            Debug.LogWarning("Assign model renderers with skinned meshes before playing the outline.", this);
            return;
        }

        _isRimPlaying = true;
        _isRimStopping = false;
        _rimElapsedTime = 0f;
        _rimOpacity = EvaluateOutlineOpacity(0f);
        _rimStopElapsedTime = 0f;
        UpdateRimRenderers();
    }

    private void TickRim()
    {
        if (!_isRimPlaying)
            return;

        if (_isRimStopping)
        {
            _rimStopElapsedTime += Time.deltaTime;
            float duration = Mathf.Max(0f, _stopFadeDuration);
            if (_rimStopElapsedTime >= duration)
            {
                HideRim();
                return;
            }

            _rimOpacity = _rimStopStartOpacity
                * (1f - Mathf.SmoothStep(0f, 1f, _rimStopElapsedTime / duration));
        }
        else
        {
            _rimElapsedTime += Time.deltaTime;
            float duration = Mathf.Max(0f, _outlineFadeInDuration)
                + Mathf.Max(0f, _outlineFadeOutDuration);
            if (_rimElapsedTime >= duration)
            {
                HideRim();
                return;
            }

            _rimOpacity = EvaluateOutlineOpacity(_rimElapsedTime);
        }

        UpdateRimRenderers();
    }

    private float EvaluateOutlineOpacity(float elapsedTime)
    {
        float fadeInDuration = Mathf.Max(0f, _outlineFadeInDuration);
        if (fadeInDuration > 0f && elapsedTime < fadeInDuration)
            return Mathf.SmoothStep(0f, 1f, elapsedTime / fadeInDuration);

        float fadeOutDuration = Mathf.Max(0f, _outlineFadeOutDuration);
        return fadeOutDuration > 0f
            ? 1f - Mathf.SmoothStep(0f, 1f, (elapsedTime - fadeInDuration) / fadeOutDuration)
            : 0f;
    }

    private void EnsureRimObjects()
    {
        if (_modelRenderers == null)
            return;

        if (_rimProperties == null)
            _rimProperties = new MaterialPropertyBlock();

        foreach (SkinnedMeshRenderer source in _modelRenderers)
        {
            if (source == null || source.sharedMesh == null)
                continue;

            bool alreadyCreated = false;
            foreach (RimLayer layer in _rimLayers)
            {
                if (layer.Source == source && layer.Renderer != null)
                {
                    alreadyCreated = true;
                    break;
                }
            }
            if (alreadyCreated)
                continue;

            _rimLayers.Add(new RimLayer
            {
                Source = source,
                Renderer = CreateOutlineRenderer(source, "Outline"),
                MaskRenderer = CreateOutlineRenderer(source, "Silhouette Mask")
            });
        }
    }

    private static SkinnedMeshRenderer CreateOutlineRenderer(SkinnedMeshRenderer source, string suffix)
    {
        GameObject outlineObject = new GameObject(source.name + " " + suffix + " (Runtime)")
        {
            hideFlags = HideFlags.DontSave,
            layer = source.gameObject.layer
        };
        outlineObject.transform.SetParent(source.transform, false);
        SkinnedMeshRenderer renderer = outlineObject.AddComponent<SkinnedMeshRenderer>();
        renderer.enabled = false;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.skinnedMotionVectors = false;
        return renderer;
    }

    private void UpdateRimRenderers()
    {
        foreach (RimLayer layer in _rimLayers)
        {
            SkinnedMeshRenderer source = layer.Source;
            SkinnedMeshRenderer rimRenderer = layer.Renderer;
            SkinnedMeshRenderer maskRenderer = layer.MaskRenderer;
            if (rimRenderer == null || maskRenderer == null)
            {
                if (rimRenderer != null)
                    rimRenderer.enabled = false;
                if (maskRenderer != null)
                    maskRenderer.enabled = false;
                continue;
            }

            if (source == null || source.sharedMesh == null)
            {
                rimRenderer.enabled = false;
                maskRenderer.enabled = false;
                continue;
            }

            Mesh mesh = source.sharedMesh;
            SynchronizeOutlineRenderer(source, rimRenderer, mesh);
            SynchronizeOutlineRenderer(source, maskRenderer, mesh);
            bool visible = source.enabled && source.gameObject.activeInHierarchy
                && _rimMaterial != null && _outlineMaskMaterial != null && _rimWidth > 0f;
            rimRenderer.enabled = visible;
            maskRenderer.enabled = visible;

            if (layer.Materials == null || layer.Materials.Length != mesh.subMeshCount
                || layer.Material != _rimMaterial)
            {
                layer.Materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < layer.Materials.Length; i++)
                    layer.Materials[i] = _rimMaterial;
                rimRenderer.sharedMaterials = layer.Materials;
                layer.Material = _rimMaterial;
            }

            if (layer.MaskMaterials == null || layer.MaskMaterials.Length != mesh.subMeshCount
                || layer.MaskMaterial != _outlineMaskMaterial)
            {
                layer.MaskMaterials = new Material[mesh.subMeshCount];
                for (int i = 0; i < layer.MaskMaterials.Length; i++)
                    layer.MaskMaterials[i] = _outlineMaskMaterial;
                maskRenderer.sharedMaterials = layer.MaskMaterials;
                layer.MaskMaterial = _outlineMaskMaterial;
            }
        }

        ApplyRimProperties();
    }

    private static void SynchronizeOutlineRenderer(
        SkinnedMeshRenderer source, SkinnedMeshRenderer target, Mesh mesh)
    {
        if (target.sharedMesh != mesh)
        {
            target.sharedMesh = mesh;
            target.bones = source.bones;
            target.rootBone = source.rootBone;
        }

        target.quality = source.quality;
        target.updateWhenOffscreen = source.updateWhenOffscreen;
        // Keep effect bounds separate from the bounds used to size and position the flash.
        Bounds bounds = source.localBounds;
        bounds.Expand(bounds.size.magnitude * 0.1f);
        target.localBounds = bounds;
        target.gameObject.layer = source.gameObject.layer;
        target.renderingLayerMask = source.renderingLayerMask;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.forceRenderingOff = source.forceRenderingOff;

        for (int i = 0; i < mesh.blendShapeCount; i++)
            target.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
    }

    private void ApplyRimProperties()
    {
        _rimProperties.SetColor(EffectColorId, _effectColor);
        _rimProperties.SetFloat(BrightnessId, Mathf.Max(0f, _rimBrightness));
        _rimProperties.SetFloat(OpacityId, _rimOpacity);
        _rimProperties.SetFloat(OutlineWidthId, Mathf.Clamp(_rimWidth, 0f, 10f));

        foreach (RimLayer layer in _rimLayers)
        {
            if (layer.Renderer != null)
                layer.Renderer.SetPropertyBlock(_rimProperties);
        }
    }

    private void HideRim()
    {
        _isRimPlaying = false;
        _isRimStopping = false;
        _rimOpacity = 0f;
        _rimElapsedTime = 0f;
        _rimStopElapsedTime = 0f;
        foreach (RimLayer layer in _rimLayers)
        {
            if (layer.Renderer != null)
                layer.Renderer.enabled = false;
            if (layer.MaskRenderer != null)
                layer.MaskRenderer.enabled = false;
        }
    }

    private float EvaluateFlashOpacity(float elapsedTime)
    {
        float fadeInDuration = Mathf.Max(0f, _flashFadeInDuration);

        if (fadeInDuration > 0f && elapsedTime < fadeInDuration)
            return Mathf.SmoothStep(0f, 1f, elapsedTime / fadeInDuration);

        return 1f - EvaluateFadeOutProgress(elapsedTime);
    }

    private float EvaluateFadeOutProgress(float elapsedTime)
    {
        float fadeOutTime = elapsedTime - Mathf.Max(0f, _flashFadeInDuration)
            - Mathf.Max(0f, _flashHoldDuration);
        float fadeOutDuration = Mathf.Max(0f, _flashFadeOutDuration);

        return fadeOutDuration > 0f
            ? Mathf.SmoothStep(0f, 1f, fadeOutTime / fadeOutDuration)
            : (fadeOutTime >= 0f ? 1f : 0f);
    }

    private Camera ResolveCamera()
    {
        return _effectCamera != null ? _effectCamera : Camera.main;
    }

    private bool TryGetModelBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        if (_modelRenderers == null)
            return false;

        foreach (SkinnedMeshRenderer modelRenderer in _modelRenderers)
        {
            if (modelRenderer == null || !modelRenderer.enabled
                || !modelRenderer.gameObject.activeInHierarchy || modelRenderer.sharedMesh == null)
            {
                continue;
            }

            if (hasBounds)
                bounds.Encapsulate(modelRenderer.bounds);
            else
                bounds = modelRenderer.bounds;

            hasBounds = true;
        }

        return hasBounds;
    }

    private void EnsureFlashObject()
    {
        if (_flashObject != null)
            return;

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
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_flashObject, gameObject.scene);
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
        float centerDepth = Vector3.Dot(bounds.center - cameraTransform.position, cameraTransform.forward);
        float minimumDepth = effectCamera.nearClipPlane + 0.01f;
        _flashRenderer.enabled = centerDepth > minimumDepth;
        if (!_flashRenderer.enabled)
            return;

        Vector3 towardCamera = effectCamera.orthographic
            ? -cameraTransform.forward
            : (cameraTransform.position - bounds.center).normalized;
        Vector3 absoluteDirection = new Vector3(
            Mathf.Abs(towardCamera.x), Mathf.Abs(towardCamera.y), Mathf.Abs(towardCamera.z));
        float frontDistance = Vector3.Dot(bounds.extents, absoluteDirection)
            + Mathf.Max(0f, _flashDepthOffset);
        float depthFactor = Vector3.Dot(towardCamera, -cameraTransform.forward);
        frontDistance = Mathf.Min(frontDistance, (centerDepth - minimumDepth) / depthFactor);

        Vector3 flashPosition = bounds.center + towardCamera * frontDistance;
        float size = Mathf.Max(0.01f, bounds.size.y) * Mathf.Max(0.01f, _flashSize);
        // Preserve the projected size when moving a perspective billboard in front of the model.
        if (!effectCamera.orthographic)
            size *= (centerDepth - frontDistance * depthFactor) / centerDepth;

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
        _isStopping = false;
        _opacity = 0f;
        _horizontalStretch = 1f;
        if (_flashObject != null)
            _flashObject.SetActive(false);
    }

    private void OnDisable()
    {
        ReleaseRuntimeResources();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeResources();
    }

    private void ReleaseRuntimeResources()
    {
        HideFlash();
        HideRim();
        foreach (RimLayer layer in _rimLayers)
        {
            if (layer.Renderer != null)
                Destroy(layer.Renderer.gameObject);
            if (layer.MaskRenderer != null)
                Destroy(layer.MaskRenderer.gameObject);
        }
        _rimLayers.Clear();
        _rimProperties = null;

        if (_flashObject != null)
            Destroy(_flashObject);
        if (_flashMesh != null)
            Destroy(_flashMesh);

        _flashObject = null;
        _flashMesh = null;
        _flashRenderer = null;
        _flashProperties = null;
    }
}
