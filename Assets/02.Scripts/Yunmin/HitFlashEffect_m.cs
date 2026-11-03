using System.Collections.Generic;
using UnityEngine;

// 피격 시 모델 위에 같은 메시를 덮어 그려 잠깐 하얗게 보이게 합니다.
// 원본 머티리얼은 건드리지 않으므로 셰이더 종류와 상관없이 사용할 수 있습니다.
[DisallowMultipleComponent]
public sealed class HitFlashEffect_m : MonoBehaviour
{
    private static readonly int EffectColorId = Shader.PropertyToID("_EffectColor");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    [Header("Model")]
    [Tooltip("Leave empty to collect every child SkinnedMeshRenderer.")]
    [SerializeField] private SkinnedMeshRenderer[] _modelRenderers;

    [Header("Hit Flash")]
    [SerializeField] private Material _flashMaterial;
    [SerializeField, ColorUsage(true, true)] private Color _flashColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float _maxOpacity = 0.85f;
    [SerializeField, Min(0f)] private float _fadeInDuration = 0f;
    [SerializeField, Min(0f)] private float _holdDuration = 0.05f;
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.15f;

    private sealed class FlashLayer
    {
        public SkinnedMeshRenderer Source;
        public SkinnedMeshRenderer Renderer;
        public Material[] Materials;
    }

    private readonly List<FlashLayer> _flashLayers = new List<FlashLayer>();
    private MaterialPropertyBlock _flashProperties;
    private bool _isFlashPlaying;
    private float _opacity;
    private float _elapsedTime;

    private float TotalDuration => _fadeInDuration + _holdDuration + _fadeOutDuration;

    internal void PlayEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;

        if (_flashMaterial == null)
        {
            Debug.LogWarning("Assign a flash material before playing the hit flash.", this);
            return;
        }

        EnsureFlashObjects();
        if (_flashLayers.Count == 0)
        {
            Debug.LogWarning("Assign model renderers with skinned meshes before playing the hit flash.", this);
            return;
        }

        _isFlashPlaying = true;
        _elapsedTime = 0f;
        _opacity = EvaluateFlashOpacity(0f);
        UpdateFlashRenderers();
    }

    internal void StopEffect()
    {
        HideFlash();
    }

    private void LateUpdate()
    {
        if (!_isFlashPlaying) return;
        _elapsedTime += Time.deltaTime;
        if (_elapsedTime >= TotalDuration)
        {
            HideFlash();
            return;
        }

        _opacity = EvaluateFlashOpacity(_elapsedTime);

        UpdateFlashRenderers();
    }

    private float EvaluateFlashOpacity(float elapsedTime)
    {
        if (_fadeInDuration > 0f && elapsedTime < _fadeInDuration)
        {
            return _maxOpacity * Mathf.SmoothStep(0f, 1f, elapsedTime / _fadeInDuration);
        }

        float fadeOutTime = elapsedTime - _fadeInDuration - _holdDuration;
        if (fadeOutTime <= 0f) return _maxOpacity;

        return (_fadeOutDuration > 0f) ? _maxOpacity * (1f - Mathf.SmoothStep(0f, 1f, fadeOutTime / _fadeOutDuration)) : 0f;
    }

    private void EnsureFlashObjects()
    {
        // 렌더러를 지정하지 않았다면 런타임 렌더러 생성 전에 자식 모델 렌더러를 수집합니다.
        if ((_modelRenderers == null || _modelRenderers.Length == 0) && _flashLayers.Count == 0)
        {
            _modelRenderers = ShilhouetteOutlineEffect_m.CollectModelRenderers(this);
        }

        if (_modelRenderers == null) return;
        if (_flashProperties == null)
        {
            _flashProperties = new MaterialPropertyBlock();
        }

        foreach (SkinnedMeshRenderer source in _modelRenderers)
        {
            if (source == null || source.sharedMesh == null) continue;

            bool alreadyCreated = false;

            foreach (FlashLayer layer in _flashLayers)
            {
                if (layer.Source == source && layer.Renderer != null)
                {
                    alreadyCreated = true;
                    break;
                }
            }

            if (alreadyCreated) continue;

            _flashLayers.Add(new FlashLayer
            {
                Source = source,
                Renderer = ShilhouetteOutlineEffect_m.CreateOutlineRenderer(source, "Hit Flash")
            });
        }
    }

    private void UpdateFlashRenderers()
    {
        foreach (FlashLayer layer in _flashLayers)
        {
            SkinnedMeshRenderer source = layer.Source;
            SkinnedMeshRenderer flashRenderer = layer.Renderer;

            if (flashRenderer == null) continue;

            if (source == null || source.sharedMesh == null)
            {
                flashRenderer.enabled = false;
                continue;
            }

            Mesh mesh = source.sharedMesh;
            ShilhouetteOutlineEffect_m.SynchronizeOutlineRenderer(source, flashRenderer, mesh);

            flashRenderer.enabled = source.enabled && source.gameObject.activeInHierarchy;

            if (layer.Materials == null || layer.Materials.Length != mesh.subMeshCount
                || layer.Materials[0] != _flashMaterial)
            {
                layer.Materials = new Material[mesh.subMeshCount];

                for (int i = 0; i < layer.Materials.Length; i++)
                {
                    layer.Materials[i] = _flashMaterial;
                }

                flashRenderer.sharedMaterials = layer.Materials;
            }
        }

        ApplyFlashProperties();
    }

    private void ApplyFlashProperties()
    {
        if (_flashProperties == null) return;

        _flashProperties.SetColor(EffectColorId, _flashColor);
        _flashProperties.SetFloat(OpacityId, _opacity);

        foreach (FlashLayer layer in _flashLayers)
        {
            if (layer.Renderer != null)
            {
                layer.Renderer.SetPropertyBlock(_flashProperties);
            }
        }
    }

    private void HideFlash()
    {
        _isFlashPlaying = false;
        _opacity = 0f;
        _elapsedTime = 0f;

        foreach (FlashLayer layer in _flashLayers)
        {
            if (layer.Renderer != null)
            {
                layer.Renderer.enabled = false;
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
        HideFlash();
        foreach (FlashLayer layer in _flashLayers)
        {
            if (layer.Renderer != null) Destroy(layer.Renderer.gameObject);
        }

        _flashLayers.Clear();
        _flashProperties = null;
    }
}
