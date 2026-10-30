using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health_s))]
public class PlayerHitFeedback_s : MonoBehaviour
{
    [Header("Vignette")]
    [SerializeField] private Color _vignetteColor = new Color(0.55f, 0f, 0f);
    [SerializeField, Range(0f, 1f)] private float _vignetteIntensity = 0.45f;
    [SerializeField, Range(0f, 1f)] private float _vignetteSmoothness = 0.6f;
    [SerializeField, Min(0.01f)] private float _vignetteDuration = 0.35f;
    [SerializeField] private bool _holdOnDeath = true;
    [SerializeField] private float _volumePriority = 100f;
    [Header("Camera")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;
    [SerializeField, Min(0f)] private float _impulseForce = 0.2f;
    [Header("Hit Stop")]
    [SerializeField] private HitStop_s _hitStop;
    [SerializeField, Min(0f)] private float _hitStopDuration = 0.04f;
    private Health_s _health;
    private Volume _volume;
    private VolumeProfile _profile;
    private float _remainingTime;
    private bool _holding;

    private void Awake()
    {
        _health = GetComponent<Health_s>();
        if (_impulseSource == null) _impulseSource = GetComponentInParent<CinemachineImpulseSource>();
        if (_hitStop == null) _hitStop = GetComponentInParent<HitStop_s>();
    }

    private void OnEnable()
    {
        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
    }

    private void CreateVolume()
    {
        if (_volume != null) return;
        // 씬 Volume 에셋을 수정하지 않도록 런타임 전용 프로필을 사용한다.
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = _profile.Add<Vignette>(true);
        vignette.color.Override(_vignetteColor);
        vignette.intensity.Override(_vignetteIntensity);
        vignette.smoothness.Override(_vignetteSmoothness);
        var child = new GameObject("Player Hit Vignette Volume");
        child.transform.SetParent(transform, false);
        _volume = child.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = _volumePriority;
        _volume.sharedProfile = _profile;
        _volume.weight = 0f;
    }

    private void OnDamaged(int damage)
    {
        CreateVolume();
        _remainingTime = _vignetteDuration;
        _volume.weight = 1f;
        if (_impulseSource != null && _impulseSource.isActiveAndEnabled && _impulseForce > 0f)
        {
            CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
            _impulseSource.GenerateImpulseWithForce(_impulseForce);
        }
        // 치명타는 사망 연출을 막지 않도록 정지하지 않는다.
        if (_hitStop != null && !_health.IsDead) _hitStop.Stop(_hitStopDuration);
    }

    private void OnDied()
    {
        if (!_holdOnDeath) return;
        CreateVolume();
        _holding = true;
        _volume.weight = 1f;
    }

    private void LateUpdate()
    {
        if (_volume == null || _holding || _remainingTime <= 0f) return;
        _remainingTime = Mathf.Max(0f, _remainingTime - Time.unscaledDeltaTime);
        float progress = _remainingTime / _vignetteDuration;
        _volume.weight = progress * progress;
    }

    private void OnDisable()
    {
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
        _remainingTime = 0f;
        _holding = false;
        if (_volume != null) _volume.weight = 0f;
    }

    private void OnDestroy()
    {
        if (_volume != null) Destroy(_volume.gameObject);
        if (_profile != null) Destroy(_profile);
    }
}
