using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health_s))]
public class MonsterHitFlash_s : MonoBehaviour
{
    private static readonly int FlashColor = Shader.PropertyToID("_FlashColor");
    [SerializeField] private SkinnedMeshRenderer[] _bodyRenderers;
    [SerializeField] private Material _overlayMaterial;
    [SerializeField, ColorUsage(false, true)] private Color _flashColor = new Color(0.65f, 0.9f, 1f);
    [SerializeField, Range(0f, 1f)] private float _flashOpacity = 0.7f;
    [SerializeField, Min(0.001f)] private float _flashDuration = 0.08f;
    private Health_s _health;
    private SkinnedMeshRenderer[] _overlays;
    private MaterialPropertyBlock _properties;
    private float _remainingTime;
    private float _activeDuration;
    private Color _activeColor;
    private float _activeOpacity;
    private bool _activeUnscaled;

    private void Awake()
    {
        _health = GetComponent<Health_s>();
        _properties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        _health.Damaged += OnDamaged;
        _health.Died += Hide;
    }

    private void CreateOverlays()
    {
        if (_overlays != null || _overlayMaterial == null) return;
        // 지정한 몸만 대상으로 하며 무기·UI·VFX는 자동 수집하지 않는다.
        if (_bodyRenderers == null) return;
        _overlays = new SkinnedMeshRenderer[_bodyRenderers.Length];
        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            var source = _bodyRenderers[i];
            if (source == null || source.sharedMesh == null) continue;
            var child = new GameObject("Hit Flash Overlay");
            child.layer = source.gameObject.layer;
            child.transform.SetParent(source.transform, false);
            var overlay = child.AddComponent<SkinnedMeshRenderer>();
            overlay.enabled = false;
            overlay.sharedMesh = source.sharedMesh;
            overlay.bones = source.bones;
            overlay.rootBone = source.rootBone;
            overlay.localBounds = source.localBounds;
            overlay.quality = source.quality;
            overlay.updateWhenOffscreen = source.updateWhenOffscreen;
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlay.renderingLayerMask = 1u;
            var materials = new Material[source.sharedMesh.subMeshCount];
            for (int slot = 0; slot < materials.Length; slot++) materials[slot] = _overlayMaterial;
            overlay.sharedMaterials = materials;
            _overlays[i] = overlay;
        }
    }

    private void OnDamaged(int damage)
    {
        Flash(_flashColor, _flashOpacity, _flashDuration, true);
    }

    // 피격 외의 연출(공격 예고 등)도 같은 오버레이를 재사용한다.
    public void Flash(Color color, float opacity, float duration, bool unscaled)
    {
        if (!isActiveAndEnabled || _health.IsDead) return;
        CreateOverlays();
        _activeColor = color;
        _activeOpacity = opacity;
        _activeDuration = Mathf.Max(0.001f, duration);
        _activeUnscaled = unscaled;
        _remainingTime = _activeDuration;
        ApplyFlash(1f);
    }

    private void LateUpdate()
    {
        if (_remainingTime <= 0f) return;
        float deltaTime = _activeUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        _remainingTime = Mathf.Max(0f, _remainingTime - deltaTime);
        if (_remainingTime <= 0f)
        {
            Hide();
            return;
        }
        float progress = _remainingTime / _activeDuration;
        ApplyFlash(progress * progress);
    }

    private void ApplyFlash(float strength)
    {
        if (_overlays == null) return;
        Color color = _activeColor;
        color.a = Mathf.Clamp01(_activeOpacity * strength);
        _properties.SetColor(FlashColor, color);
        for (int i = 0; i < _overlays.Length; i++)
        {
            var overlay = _overlays[i];
            var source = _bodyRenderers[i];
            if (overlay == null) continue;
            overlay.enabled = source != null && source.enabled && source.gameObject.activeInHierarchy
                && !source.forceRenderingOff && color.a > 0f;
            if (!overlay.enabled) continue;
            overlay.localBounds = source.localBounds;
            for (int shape = 0; shape < source.sharedMesh.blendShapeCount; shape++)
            {
                overlay.SetBlendShapeWeight(shape, source.GetBlendShapeWeight(shape));
            }
            overlay.SetPropertyBlock(_properties);
        }
    }

    private void Hide()
    {
        _remainingTime = 0f;
        if (_overlays == null) return;
        foreach (var overlay in _overlays)
        {
            if (overlay != null) overlay.enabled = false;
        }
    }

    private void OnDisable()
    {
        _health.Damaged -= OnDamaged;
        _health.Died -= Hide;
        Hide();
    }

    private void OnDestroy()
    {
        if (_overlays == null) return;
        foreach (var overlay in _overlays)
        {
            if (overlay != null) Destroy(overlay.gameObject);
        }
    }
}
