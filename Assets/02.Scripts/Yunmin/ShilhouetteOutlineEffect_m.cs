using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class ShilhouetteOutlineEffect_m : MonoBehaviour
{
    private static readonly int EffectColorId = Shader.PropertyToID("_EffectColor");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [Header("Model")]
    [Tooltip("Assign the body and head renderers, excluding effect renderers.")]
    [SerializeField] private SkinnedMeshRenderer[] _modelRenderers;

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

    private Color _effectColor;
    private float _stopFadeDuration = 0.4f;

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

    internal void PlayEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;

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

    internal void StopEffect()
    {
        if (_stopFadeDuration <= 0f)
        {
            HideRim();
            return;
        }

        if (_isRimPlaying && !_isRimStopping)
        {
            _rimStopStartOpacity = _rimOpacity;
            _rimStopElapsedTime = 0f;
            _isRimStopping = true;
        }
    }

    internal void SetSharedSettings(Color color, float stopFadeDuration)
    {
        bool colorChanged = _effectColor != color;
        _effectColor = color;
        _stopFadeDuration = Mathf.Max(0f, stopFadeDuration);
        if (colorChanged && _rimProperties != null)
            ApplyRimProperties();
    }

    private void LateUpdate()
    {
        if (!_isRimPlaying) return;
        if (_isRimStopping)
        {
            _rimStopElapsedTime += Time.deltaTime;
            float duration = Mathf.Max(0f, _stopFadeDuration);

            if (_rimStopElapsedTime >= duration)
            {
                HideRim();
                return;
            }

            _rimOpacity = _rimStopStartOpacity * (1f - Mathf.SmoothStep(0f, 1f, _rimStopElapsedTime / duration));
        }
        else
        {
            _rimElapsedTime += Time.deltaTime;
            float duration = Mathf.Max(0f, _outlineFadeInDuration) + Mathf.Max(0f, _outlineFadeOutDuration);

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
        {
            return Mathf.SmoothStep(0f, 1f, elapsedTime / fadeInDuration);
        }

        float fadeOutDuration = Mathf.Max(0f, _outlineFadeOutDuration);

        return (fadeOutDuration > 0f) ? (1f - Mathf.SmoothStep(0f, 1f, (elapsedTime - fadeInDuration) / fadeOutDuration)) : 0f;
    }

    private void EnsureRimObjects()
    {
        if (_modelRenderers == null) return;
        if (_rimProperties == null)
        {
            _rimProperties = new MaterialPropertyBlock();
        }

        foreach (SkinnedMeshRenderer source in _modelRenderers)
        {
            if (source == null || source.sharedMesh == null) continue;

            bool alreadyCreated = false;

            foreach (RimLayer layer in _rimLayers)
            {
                if (layer.Source == source && layer.Renderer != null)
                {
                    alreadyCreated = true;
                    break;
                }
            }

            if (alreadyCreated) continue;

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
        GameObject outlineObject = new GameObject($"{source.name} {suffix}(Runtime)")
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
                if (rimRenderer != null) rimRenderer.enabled = false;
                if (maskRenderer != null) maskRenderer.enabled = false;
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

            bool visible = source.enabled
                        && source.gameObject.activeInHierarchy
                        && _rimMaterial != null && _outlineMaskMaterial != null
                        && _rimWidth > 0f;

            rimRenderer.enabled = visible;
            maskRenderer.enabled = visible;

            if (layer.Materials == null || layer.Materials.Length != mesh.subMeshCount
                || layer.Material != _rimMaterial)
            {
                layer.Materials = new Material[mesh.subMeshCount];

                for (int i = 0; i < layer.Materials.Length; i++)
                {
                    layer.Materials[i] = _rimMaterial;
                }

                rimRenderer.sharedMaterials = layer.Materials;
                layer.Material = _rimMaterial;
            }

            if (layer.MaskMaterials == null || layer.MaskMaterials.Length != mesh.subMeshCount
                || layer.MaskMaterial != _outlineMaskMaterial)
            {
                layer.MaskMaterials = new Material[mesh.subMeshCount];

                for (int i = 0; i < layer.MaskMaterials.Length; i++)
                {
                    layer.MaskMaterials[i] = _outlineMaskMaterial;
                }

                maskRenderer.sharedMaterials = layer.MaskMaterials;
                layer.MaskMaterial = _outlineMaskMaterial;
            }
        }

        ApplyRimProperties();
    }

    private static void SynchronizeOutlineRenderer(SkinnedMeshRenderer source, SkinnedMeshRenderer target, Mesh mesh)
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
        {
            target.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
        }
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
            {
                layer.Renderer.SetPropertyBlock(_rimProperties);
            }
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
            {
                layer.Renderer.enabled = false;
            }

            if (layer.MaskRenderer != null)
            {
                layer.MaskRenderer.enabled = false;
            }
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
        HideRim();
        foreach (RimLayer layer in _rimLayers)
        {
            if (layer.Renderer != null) Destroy(layer.Renderer.gameObject);
            if (layer.MaskRenderer != null) Destroy(layer.MaskRenderer.gameObject);
        }

        _rimLayers.Clear();
        _rimProperties = null;
    }
}
