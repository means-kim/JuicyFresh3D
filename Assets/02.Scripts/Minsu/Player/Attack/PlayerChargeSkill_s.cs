using System;
using UnityEngine;

[DefaultExecutionOrder(-110)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement_s))]
public class PlayerChargeSkill_s : MonoBehaviour
{
    private static readonly int Charging = Animator.StringToHash("Base Layer.Charging");
    private static readonly int[] AttackStates =
    {
        Animator.StringToHash("Base Layer.ChargingAttack"),
        Animator.StringToHash("Base Layer.ChargingAttack2"),
        Animator.StringToHash("Base Layer.ChargingAttack3")
    };
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0.01f)] private float _partialChargeTime = 0.6f;
    [SerializeField, Min(0.02f)] private float _maxChargeTime = 1.5f;
    [SerializeField] private Vector3 _damage = new Vector3(30f, 50f, 80f);
    [SerializeField] private Vector2 _damageWindow = new Vector2(0.35f, 0.52f);
    [SerializeField] private Vector2 _secondDamageWindow = new Vector2(0.25f, 0.43f);
    [SerializeField] private Vector2 _thirdDamageWindow = new Vector2(0.35f, 0.52f);
    [SerializeField, Range(0.5f, 1f)] private float _firstChainTime = 0.58f;
    [SerializeField, Range(0.5f, 1f)] private float _secondChainTime = 0.5f;
    private PlayerMovement_s _movement;
    private float _chargeTime;
    private int _swingSequence;
    private bool _active;
    private int _step = -1;
    public int AttackStep => _step + 1;
    public int ReleasedStage { get; private set; }
    public float ChargeTime => _chargeTime;
    public int ChargeStage => _chargeTime >= _maxChargeTime ? 3 : _chargeTime >= _partialChargeTime ? 2 : 1;
    public event Action DamageWindowClosed;
    public bool IsCharging => _active && _movement != null && _movement.CurrentState == MovementState.Charging;
    public float ChargeProgress => Mathf.Clamp01(_chargeTime / _maxChargeTime);
    public event Action ChargeStarted;
    public event Action<float> ChargeProgressChanged;
    public event Action<int> ChargeStageChanged;
    public event Action ChargeStopped;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement_s>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    private void OnValidate()
    {
        _partialChargeTime = Mathf.Max(0.01f, _partialChargeTime);
        _maxChargeTime = Mathf.Max(_partialChargeTime + 0.01f, _maxChargeTime);
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
            || _animator == null || !_animator.isActiveAndEnabled))
        {
            CancelCharge();
            return;
        }
        if (pressed && !_active) TryBeginCharge();
        if (!_active) return;
        if (_movement.CurrentState == MovementState.Charging)
        {
            if (held)
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
            else if (!held) CancelCharge();
        }
        else if (_movement.CurrentState == MovementState.ChargingAttack)
        {
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (_step < 0 || _animator.IsInTransition(0) || state.fullPathHash != AttackStates[_step]) return;
            float chainTime = _step == 0 ? Mathf.Max(_firstChainTime, _damageWindow.y)
                : Mathf.Max(_secondChainTime, _secondDamageWindow.y);
            if (_step < 2 && state.normalizedTime >= chainTime)
            {
                PlayStep(_step + 1);
            }
            else if (_step == 2 && state.normalizedTime >= 1f)
            {
                CancelCharge();
            }
        }
    }

    public bool TryBeginCharge()
    {
        if (!isActiveAndEnabled || _active || _animator == null || !_animator.isActiveAndEnabled
            || !_animator.HasState(0, Charging) || !_animator.HasState(0, AttackStates[0])
            || !_animator.HasState(0, AttackStates[1]) || !_animator.HasState(0, AttackStates[2])
            || !_movement.TryBeginCharging())
        {
            return false;
        }

        _active = true;
        _chargeTime = 0f;
        ReleasedStage = 0;
        _animator.CrossFadeInFixedTime(Charging, 0.08f, 0, 0.1f);
        ChargeStarted?.Invoke();
        ChargeProgressChanged?.Invoke(0f);
        return true;
    }

    public void ReleaseCharge()
    {
        if (!_active || !_movement.BeginChargingAttack()) return;
        ReleasedStage = ChargeStage;
        ChargeStopped?.Invoke();
        PlayStep(0);
    }

    private void PlayStep(int step)
    {
        DamageWindowClosed?.Invoke();
        _step = step;
        _swingSequence++;
        _animator.CrossFadeInFixedTime(AttackStates[step], 0.05f, 0, 0f);
    }

    public bool TryGetDamagePhase(out int swing, out int damage, out float time, out Vector2 window)
    {
        swing = _swingSequence;
        damage = Mathf.Max(0, Mathf.RoundToInt(ReleasedStage == 1 ? _damage.x : ReleasedStage == 2 ? _damage.y : _damage.z));
        time = 0f;
        window = _step == 0 ? _damageWindow : _step == 1 ? _secondDamageWindow : _thirdDamageWindow;
        if (!_active || _step < 0 || !isActiveAndEnabled || _movement.CurrentState != MovementState.ChargingAttack
            || _movement.IsActionBlocked || _animator == null || !_animator.isActiveAndEnabled
            || _animator.IsInTransition(0))
        {
            return false;
        }

        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != AttackStates[_step]) return false;
        time = state.normalizedTime;
        return true;
    }

    public void CancelCharge(bool returnToMovement = true)
    {
        bool wasActive = _active;
        _active = false;
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
