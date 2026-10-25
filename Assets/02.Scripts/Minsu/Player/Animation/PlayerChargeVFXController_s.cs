using UnityEngine;

public enum ChargeVFXStyle
{
    Original = 0,
    Uni = 1,
    Both = 2
}

[DisallowMultipleComponent]
public class PlayerChargeVFXController_s : MonoBehaviour
{
    [SerializeField] private ChargeVFXStyle _style = ChargeVFXStyle.Both;
    [SerializeField] private KatanaChargeSkill_s _skill;
    [SerializeField] private PlayerChargeVFX_s _original;
    [SerializeField] private PlayerUniChargeVFX_s _uni;
    private ChargeVFXStyle _activeStyle;
    private bool UsesOriginal => _activeStyle == ChargeVFXStyle.Original || _activeStyle == ChargeVFXStyle.Both;
    private bool UsesUni => _activeStyle == ChargeVFXStyle.Uni || _activeStyle == ChargeVFXStyle.Both;

    private void OnEnable()
    {
        if (_skill == null) _skill = GetComponent<KatanaChargeSkill_s>();
        if (_original == null) _original = GetComponent<PlayerChargeVFX_s>();
        if (_uni == null) _uni = GetComponent<PlayerUniChargeVFX_s>();
        _activeStyle = _style;
        StopEffects(true);
        if (_skill == null) return;
        _skill.ChargeStarted += Begin;
        _skill.ChargeProgressChanged += SetProgress;
        _skill.ChargeStageChanged += SetStage;
        _skill.ChargeStopped += Stop;
        if (_skill.IsCharging) Begin();
    }

    private void Update()
    {
        if (_activeStyle == _style) return;
        StopEffects(true);
        _activeStyle = _style;
        if (_skill != null && _skill.IsCharging) Begin();
    }

    private void Begin()
    {
        StopEffects(true);
        _activeStyle = _style;
        if (UsesOriginal)
        {
            if (_original != null && _original.isActiveAndEnabled) _original.Begin();
        }
        if (UsesUni && _uni != null && _uni.isActiveAndEnabled)
        {
            _uni.Begin();
        }
        if (_skill == null) return;
        SetProgress(_skill.ChargeProgress);
        if (_skill.ChargeStage > 1) SetStage(_skill.ChargeStage);
    }

    private void SetProgress(float progress)
    {
        if (UsesOriginal) _original?.SetProgress(progress);
    }

    private void SetStage(int stage)
    {
        if (UsesOriginal) _original?.Flash(stage);
    }

    private void Stop()
    {
        StopEffects(false);
    }

    private void StopEffects(bool immediate)
    {
        _original?.Clear();
        _uni?.Stop(immediate);
    }

    private void OnDisable()
    {
        if (_skill != null)
        {
            _skill.ChargeStarted -= Begin;
            _skill.ChargeProgressChanged -= SetProgress;
            _skill.ChargeStageChanged -= SetStage;
            _skill.ChargeStopped -= Stop;
        }
        StopEffects(true);
    }
}
