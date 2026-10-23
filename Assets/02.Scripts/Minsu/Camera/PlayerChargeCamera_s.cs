using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class PlayerChargeCamera_s : MonoBehaviour
{
    [SerializeField] private PlayerChargeSkill_s _skill;
    [SerializeField] private CinemachineCamera[] _cameras;
    [SerializeField, Range(0f, 15f)] private float _maxFovReduction = 6f;
    [SerializeField, Min(0.01f)] private float _smoothTime = 0.18f;
    private float[] _baseFovs;
    private float _targetReduction;
    private float _reduction;
    private float _velocity;

    private void OnEnable()
    {
        if (_cameras == null) return;
        _baseFovs = new float[_cameras.Length];
        for (int i = 0; i < _cameras.Length; i++)
        {
            if (_cameras[i] != null) _baseFovs[i] = _cameras[i].Lens.FieldOfView;
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
        if (_skill == null || !_skill.isActiveAndEnabled || !_skill.IsCharging) Restore();
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

    private void OnDisable()
    {
        if (_skill != null)
        {
            _skill.ChargeProgressChanged -= SetProgress;
            _skill.ChargeStopped -= Restore;
        }
        _reduction = 0f;
        _targetReduction = 0f;
        _velocity = 0f;
        ApplyFov();
    }
}
