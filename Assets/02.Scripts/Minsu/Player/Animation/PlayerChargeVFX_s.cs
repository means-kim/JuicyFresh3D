using UnityEngine;

[DisallowMultipleComponent]
public class PlayerChargeVFX_s : MonoBehaviour
{
    [SerializeField] private PlayerChargeSkill_s _skill;
    [SerializeField] private ParticleSystem _converge;
    [SerializeField] private ParticleSystem _flash;
    [SerializeField, Min(0.1f)] private float _radius = 0.65f;
    [SerializeField, Min(1f)] private float _emissionRate = 35f;
    private float _progress;
    private float _emission;
    private bool _charging;
    private int _stage = 1;

    private void OnEnable()
    {
        if (_skill == null) _skill = GetComponent<PlayerChargeSkill_s>();
        if (_skill == null) return;
        _skill.ChargeStarted += Begin;
        _skill.ChargeProgressChanged += SetProgress;
        _skill.ChargeStageChanged += Flash;
        _skill.ChargeStopped += Clear;
        if (_skill.IsCharging)
        {
            Begin();
            _progress = _skill.ChargeProgress;
            _stage = _skill.ChargeStage;
        }
    }

    private void Begin()
    {
        Clear();
        _charging = true;
        _stage = 1;
        if (_converge != null) _converge.Play();
    }

    private void SetProgress(float progress)
    {
        _progress = progress;
    }

    private Color StageColor => _stage == 1 ? new Color(0.15f, 0.65f, 1f)
        : _stage == 2 ? new Color(0.4f, 1f, 1f) : new Color(1f, 0.8f, 0.2f);

    private void Flash(int stage)
    {
        _stage = stage;
        if (_flash == null) return;
        _flash.Play();
        var burst = new ParticleSystem.EmitParams
        {
            startColor = StageColor,
            startSize = stage == 3 ? 0.22f : 0.13f
        };
        _flash.Emit(burst, stage == 3 ? 32 : 18);
    }

    private void Update()
    {
        TickVisuals(Time.deltaTime);
    }

    public void TickVisuals(float deltaTime)
    {
        if (!_charging || _converge == null || deltaTime <= 0f) return;
        _emission += _emissionRate * Mathf.Lerp(1f, 2.5f, _progress) * deltaTime;
        int count = Mathf.Min(Mathf.FloorToInt(_emission), 64);
        _emission -= count;
        float lifetime = Mathf.Lerp(0.5f, 0.25f, _progress);
        for (int i = 0; i < count; i++)
        {
            Vector3 position = Random.onUnitSphere * _radius;
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = -position / lifetime,
                startLifetime = lifetime,
                startSize = Mathf.Lerp(0.035f, 0.075f, _progress),
                startColor = StageColor
            };
            _converge.Emit(particle, 1);
        }
    }

    private void Clear()
    {
        _charging = false;
        _progress = 0f;
        _emission = 0f;
        if (_converge != null) _converge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_flash != null) _flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        if (_skill != null)
        {
            _skill.ChargeStarted -= Begin;
            _skill.ChargeProgressChanged -= SetProgress;
            _skill.ChargeStageChanged -= Flash;
            _skill.ChargeStopped -= Clear;
        }
        Clear();
    }
}
