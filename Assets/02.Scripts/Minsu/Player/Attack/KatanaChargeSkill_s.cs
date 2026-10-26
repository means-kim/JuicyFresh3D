using System;
using UnityEngine;

[DefaultExecutionOrder(-110)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement_s), typeof(PlayerDash_s))]
public class KatanaChargeSkill_s : MonoBehaviour
{
    private static readonly int Charging = Animator.StringToHash("Base Layer.Charging");
    private static readonly int ChargePoseTime = Animator.StringToHash("ChargePoseTime");
    private static readonly int Drawing = Animator.StringToHash("Base Layer.ChargeDraw");
    private static readonly int Dashing = Animator.StringToHash("Base Layer.ChargingAttack");
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0.01f)] private float _partialChargeTime = 0.6f;
    [SerializeField, Min(0.02f)] private float _maxChargeTime = 1.5f;
    [SerializeField] private Vector3 _damage = new Vector3(30f, 50f, 80f);
    [SerializeField] private AnimationClip _drawClip;
    [SerializeField, Min(0f)] private float _holdTime = 6f / 60f;
    [SerializeField, Min(0f)] private float _minimumPreparation = 0.12f;
    [SerializeField, Range(0.01f, 1f)] private float _drawEndTime = 0.1714286f;
    [SerializeField] private Vector2 _dashWindow = new Vector2(0f, 0.22f);
    private PlayerMovement_s _movement;
    private PlayerDash_s _dash;
    private float _chargeTime;
    private float _preparationTime;
    private int _swingSequence;
    private int _step = -1;
    private bool _active;
    private bool _releaseQueued;
    private Vector3 _releaseDirection;
    public int ReleasedStage { get; private set; }
    public Vector3 ReleaseDirection => _releaseDirection;
    public int AttackStep => _step + 1;
    public float ChargeTime => _chargeTime;
    public int ChargeStage => _chargeTime >= _maxChargeTime ? 3 : _chargeTime >= _partialChargeTime ? 2 : 1;
    public float ChargeProgress => Mathf.Clamp01(_chargeTime / _maxChargeTime);
    public bool IsCharging => _active && _movement != null && _movement.CurrentState == MovementState.Charging;
    public bool IsDirectionLocked => _active && (_releaseQueued || _step >= 0);
    public bool IsFinisher => _active && _step == 1;
    public event Action DamageWindowClosed;
    public event Action ChargeStarted;
    public event Action<float> ChargeProgressChanged;
    public event Action<int> ChargeStageChanged;
    public event Action ChargeStopped;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement_s>();
        _dash = GetComponent<PlayerDash_s>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    private void OnValidate()
    {
        _partialChargeTime = Mathf.Max(0.01f, _partialChargeTime);
        _maxChargeTime = Mathf.Max(_partialChargeTime + 0.01f, _maxChargeTime);
        _dashWindow.x = Mathf.Clamp(_dashWindow.x, 0f, 0.95f);
        _dashWindow.y = Mathf.Clamp(_dashWindow.y, _dashWindow.x + 0.01f, 1f);
    }

    private void Update()
    {
        SimulateCharge(Input.GetMouseButtonDown(1), Input.GetMouseButton(1),
            Input.GetMouseButtonUp(1), Time.deltaTime);
    }

    public void SimulateCharge(bool pressed, bool held, bool released, float deltaTime)
    {
        if (!isActiveAndEnabled || _movement == null) return;
        if (_active && (!_movement.IsUsingChargeSkill || _movement.IsActionBlocked
            || _animator == null || !_animator.isActiveAndEnabled || !_dash.isActiveAndEnabled))
        {
            CancelCharge();
            return;
        }
        if (pressed && !_active) TryBeginCharge();
        if (!_active) return;
        if (IsCharging)
        {
            _preparationTime += Mathf.Max(0f, deltaTime);
            // 이 상태의 재생 시간만 제어해 저프레임에서도 발도 구간을 넘어가지 않는다.
            float preparation = Mathf.Clamp01(_preparationTime / Mathf.Max(0.01f, _minimumPreparation));
            _animator.SetFloat(ChargePoseTime, preparation * _holdTime / _drawClip.length);
            if (held && !_releaseQueued)
            {
                int previousStage = ChargeStage;
                _chargeTime = Mathf.Min(_maxChargeTime, _chargeTime + Mathf.Max(0f, deltaTime));
                ChargeProgressChanged?.Invoke(ChargeProgress);
                for (int stage = previousStage + 1; stage <= ChargeStage; stage++)
                {
                    ChargeStageChanged?.Invoke(stage);
                }
            }
            if (released) ReleaseCharge();
            else if (!held && !_releaseQueued) CancelCharge();
            if (_releaseQueued && _preparationTime >= _minimumPreparation) BeginDraw();
            return;
        }
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (_animator.IsInTransition(0)) return;
        if (_step == 0 && state.fullPathHash == Drawing && state.normalizedTime >= _drawEndTime)
        {
            _step = 1;
            _dash.Begin(_releaseDirection);
            _animator.CrossFadeInFixedTime(Dashing, 0.035f, 0, 0f);
        }
        else if (_step == 1 && state.fullPathHash == Dashing && state.normalizedTime >= 1f)
        {
            CancelCharge();
        }
    }

    public bool TryBeginCharge()
    {
        if (!isActiveAndEnabled || _active || _animator == null || !_animator.isActiveAndEnabled
            || _drawClip == null || _drawClip.length <= 0f
            || _dash == null || !_dash.isActiveAndEnabled || !_animator.HasState(0, Charging)
            || !_animator.HasState(0, Drawing) || !_animator.HasState(0, Dashing)
            || !_movement.TryBeginCharging())
        {
            return false;
        }
        _active = true;
        _chargeTime = 0f;
        _preparationTime = 0f;
        _releaseQueued = false;
        _step = -1;
        ReleasedStage = 0;
        _animator.SetFloat(ChargePoseTime, 0f);
        _animator.CrossFadeInFixedTime(Charging, 0.08f, 0, 0f);
        ChargeStarted?.Invoke();
        ChargeProgressChanged?.Invoke(0f);
        return true;
    }

    public void ReleaseCharge()
    {
        if (!IsCharging || _releaseQueued) return;
        ReleasedStage = ChargeStage;
        _releaseDirection = transform.forward;
        _releaseQueued = true;
        if (_preparationTime >= _minimumPreparation) BeginDraw();
    }

    private void BeginDraw()
    {
        if (!_active || !_movement.BeginChargingAttack()) return;
        _releaseQueued = false;
        ChargeStopped?.Invoke();
        _step = 0;
        _swingSequence++;
        DamageWindowClosed?.Invoke();
        _animator.CrossFadeInFixedTime(Drawing, 0.025f, 0, _holdTime);
    }

    // 이동 FSM에서 중력과 돌진을 같은 업데이트에 적용한다.
    public bool MoveDash(float verticalDisplacement)
    {
        if (!_active || _step != 1 || _movement.IsActionBlocked) return false;
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (_animator.IsInTransition(0) || state.fullPathHash != Dashing) return false;
        return _dash.Move(Mathf.InverseLerp(_dashWindow.x, _dashWindow.y, state.normalizedTime),
            verticalDisplacement);
    }

    public bool TryGetDamagePhase(out int swing, out int damage, out float time, out Vector2 window)
    {
        swing = _swingSequence;
        damage = Mathf.Max(0, Mathf.RoundToInt(ReleasedStage == 1 ? _damage.x : ReleasedStage == 2 ? _damage.y : _damage.z));
        time = 0f;
        window = _dashWindow;
        if (!_active || _step != 1 || !isActiveAndEnabled || _movement.IsActionBlocked
            || _movement.CurrentState != MovementState.ChargingAttack || _animator == null
            || !_animator.isActiveAndEnabled || _animator.IsInTransition(0))
        {
            return false;
        }
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != Dashing) return false;
        time = state.normalizedTime;
        return true;
    }

    public void CancelCharge(bool returnToMovement = true)
    {
        bool wasActive = _active;
        _dash?.Stop();
        _active = false;
        _releaseQueued = false;
        _step = -1;
        _chargeTime = 0f;
        ReleasedStage = 0;
        DamageWindowClosed?.Invoke();
        if (wasActive) ChargeStopped?.Invoke();
        if (returnToMovement && _movement != null) _movement.EndChargeSkill();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) CancelCharge();
    }

    private void OnDisable()
    {
        CancelCharge();
    }
}
