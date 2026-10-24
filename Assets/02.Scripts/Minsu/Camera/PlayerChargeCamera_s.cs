using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class PlayerChargeCamera_s : MonoBehaviour
{
    [SerializeField] private PlayerChargeSkill_s _skill;
    [SerializeField] private KatanaChargeSkill_s _katanaSkill;
    [SerializeField] private CinemachineCamera[] _cameras;
    [SerializeField, Range(0f, 15f)] private float _maxFovReduction = 6f;
    [SerializeField, Min(0.01f)] private float _smoothTime = 0.18f;
    [Header("차징 단계별 카메라 흔들림")]
    [SerializeField, Min(0f)] private float _stageOneAmplitude = 0.1f;
    [SerializeField, Min(0f)] private float _stageTwoAmplitude = 0.2f;
    [SerializeField, Min(0f)] private float _stageThreeAmplitude = 0.35f;
    [SerializeField, Min(0.01f)] private float _shakeSmoothTime = 0.1f;
    private CinemachineBasicMultiChannelPerlin[] _noises;
    private float _shakeAmplitude;
    private float _shakeVelocity;
    private float[] _baseFovs;
    private float _targetReduction;
    private float _reduction;
    private float _velocity;

    private void OnEnable()
    {
        if (_cameras == null) return;
        _baseFovs = new float[_cameras.Length];
        _noises = new CinemachineBasicMultiChannelPerlin[_cameras.Length];
        for (int i = 0; i < _cameras.Length; i++)
        {
            if (_cameras[i] == null) continue;
            _baseFovs[i] = _cameras[i].Lens.FieldOfView;
            _noises[i] = _cameras[i].GetComponent<CinemachineBasicMultiChannelPerlin>();
        }
        ApplyShake();
        if (_katanaSkill != null)
        {
            _katanaSkill.ChargeProgressChanged += SetProgress;
            _katanaSkill.ChargeStopped += Restore;
            if (_katanaSkill.IsCharging) SetProgress(_katanaSkill.ChargeProgress);
            return;
        }
        if (_skill == null) return;
        _skill.ChargeProgressChanged += SetProgress;
        _skill.ChargeStopped += Restore;
        if (_skill.IsCharging) SetProgress(_skill.ChargeProgress);
    }

    private void SetProgress(float progress)
    {
        _targetReduction = Mathf.Clamp01(progress) * _maxFovReduction;
    }

    private void Restore()
    {
        _targetReduction = 0f;
    }

    private void Update()
    {
        TickCamera(Time.unscaledDeltaTime);
    }

    public void TickCamera(float deltaTime)
    {
        bool charging = _katanaSkill != null
            ? _katanaSkill.isActiveAndEnabled && _katanaSkill.IsCharging
            : _skill != null && _skill.isActiveAndEnabled && _skill.IsCharging;
        if (!charging) Restore();
        int stage = charging ? (_katanaSkill != null ? _katanaSkill.ChargeStage : _skill.ChargeStage) : 0;
        float targetAmplitude = stage == 0 ? 0f : stage == 1 ? _stageOneAmplitude
            : stage == 2 ? _stageTwoAmplitude : _stageThreeAmplitude;
        _shakeAmplitude = Mathf.SmoothDamp(_shakeAmplitude, Mathf.Max(0f, targetAmplitude),
            ref _shakeVelocity, Mathf.Max(0.01f, _shakeSmoothTime), Mathf.Infinity, Mathf.Max(0f, deltaTime));
        ApplyShake();
        _reduction = Mathf.SmoothDamp(_reduction, _targetReduction, ref _velocity,
            _smoothTime, Mathf.Infinity, Mathf.Max(0f, deltaTime));
        ApplyFov();
    }

    private void ApplyFov()
    {
        if (_cameras == null || _baseFovs == null) return;
        for (int i = 0; i < _cameras.Length; i++)
        {
            if (_cameras[i] == null) continue;
            var lens = _cameras[i].Lens;
            lens.FieldOfView = Mathf.Clamp(_baseFovs[i] - _reduction, 1f, 179f);
            _cameras[i].Lens = lens;
        }
    }

    private void ApplyShake()
    {
        if (_noises == null) return;
        foreach (var noise in _noises)
        {
            if (noise != null) noise.AmplitudeGain = _shakeAmplitude;
        }
    }

    private void OnDisable()
    {
        if (_katanaSkill != null)
        {
            _katanaSkill.ChargeProgressChanged -= SetProgress;
            _katanaSkill.ChargeStopped -= Restore;
        }
        if (_skill != null)
        {
            _skill.ChargeProgressChanged -= SetProgress;
            _skill.ChargeStopped -= Restore;
        }
        _reduction = 0f;
        _targetReduction = 0f;
        _velocity = 0f;
        _shakeAmplitude = 0f;
        _shakeVelocity = 0f;
        ApplyShake();
        ApplyFov();
    }
}
