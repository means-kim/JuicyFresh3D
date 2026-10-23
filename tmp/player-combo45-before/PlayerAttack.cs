using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(CharacterController), typeof(PlayerMove))]
public class PlayerAttack : MonoBehaviour
{
    private const int ComboCount = 5;

    [Header("공격 클립: 01부터 05 순서")]
    [SerializeField] private AnimationClip[] _comboClips = new AnimationClip[ComboCount];

    [Header("클릭 시점부터 계산하는 콤보 연결 시간 (초)")]
    [Tooltip("공격 01 시작 클릭부터 02를 입력할 수 있는 시간. 공격과 구르기 시간도 포함합니다.")]
    [SerializeField] [Min(0f)] private float _comboDelay01 = 2.2f;
    [Tooltip("공격 02 시작 클릭부터 03을 입력할 수 있는 시간.")]
    [SerializeField] [Min(0f)] private float _comboDelay02 = 2.3f;
    [Tooltip("공격 03 시작 클릭부터 04를 입력할 수 있는 시간.")]
    [SerializeField] [Min(0f)] private float _comboDelay03 = 2.2f;
    [Tooltip("공격 04 시작 클릭부터 05를 입력할 수 있는 시간.")]
    [SerializeField] [Min(0f)] private float _comboDelay04 = 2.5f;
    [Tooltip("공격 05 이후의 연결 시간. 한 사이클을 완료했으므로 다음 공격은 항상 01입니다.")]
    [SerializeField] [Min(0f)] private float _comboDelay05 = 2.6f;

    [Header("콤보 입력 예약 및 블렌딩")]
    [Tooltip("다음 공격 클릭을 보관하는 시간. 이 시간 안에 현재 모션이 끝나야 연결됩니다. 0이면 예약하지 않습니다.")]
    [SerializeField] [Min(0f)] private float _inputBufferTime = 0.2f;
    [Tooltip("공격 시작·공격 간 연결·Idle 복귀 시 자세를 섞는 시간(초). 이전 공격은 끝까지 재생합니다.")]
    [SerializeField] [Min(0f)] private float _comboBlendDuration = 0.08f;

    private Animator _animator;
    private PlayerMove _playerMove;
    private CharacterController _characterController;
    private bool _isAttacking;
    private bool _attackAnimationStarted;
    private int _activeComboIndex = -1;
    private int _nextComboIndex;
    private int _lastComboIndex = -1;
    private float _comboElapsed;
    private bool _hasBufferedAttack;
    private int _bufferedComboIndex;
    private float _bufferedInputTime;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int RollTriggerHash = Animator.StringToHash("Roll");
    private static readonly int DashTriggerHash = Animator.StringToHash("Dash");
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle_Battle");
    private static readonly int[] ComboStateHashes =
    {
        Animator.StringToHash("Base Layer.AttackCombo01"),
        Animator.StringToHash("Base Layer.AttackCombo02"),
        Animator.StringToHash("Base Layer.AttackCombo03"),
        Animator.StringToHash("Base Layer.AttackCombo04"),
        Animator.StringToHash("Base Layer.AttackCombo05")
    };

    public bool IsAttacking => _isAttacking;
    public int CurrentCombo => _isAttacking ? _activeComboIndex + 1 : 0;
    public int NextCombo => ComboRemainingTime > 0f ? _nextComboIndex + 1 : 1;
    public bool HasBufferedAttack => _hasBufferedAttack && IsBufferValid();
    public float ComboRemainingTime => _lastComboIndex >= 0
        ? Mathf.Max(0f, GetComboDelay(_lastComboIndex) - _comboElapsed) : 0f;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _playerMove = GetComponent<PlayerMove>();
        _characterController = GetComponent<CharacterController>();

        if (_animator.runtimeAnimatorController == null || _comboClips == null
            || _comboClips.Length != ComboCount || !_animator.HasState(0, IdleStateHash))
        {
            Debug.LogError("PlayerAttack에 공격 클립 5개와 Player Animator Controller를 연결하세요.", this);
            enabled = false;
            return;
        }

        for (int index = 0; index < ComboCount; index++)
        {
            if (_comboClips[index] == null || !_animator.HasState(0, ComboStateHashes[index]))
            {
                Debug.LogError($"PlayerAttack의 AttackCombo{index + 1:00} 클립과 Animator 상태를 확인하세요.", this);
                enabled = false;
                return;
            }
        }
    }

    // PlayerMove.Update에서 구르기 취소 다음, 공격 입력 전에 호출해 처리 순서를 고정합니다.
    public void Tick(float deltaTime)
    {
        if (!isActiveAndEnabled || deltaTime <= 0f)
        {
            return;
        }

        if (_lastComboIndex >= 0)
        {
            _comboElapsed += deltaTime;

            if (ComboRemainingTime <= 0f)
            {
                ResetCombo();
            }
        }

        if (_hasBufferedAttack && !IsBufferValid())
        {
            ClearBufferedAttack();
        }

        if (!_isAttacking)
        {
            return;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);

        if (_animator.IsInTransition(0)
            && _animator.GetNextAnimatorStateInfo(0).fullPathHash == ComboStateHashes[_activeComboIndex])
        {
            // 블렌딩 중에는 이전 상태가 current에 남으므로 다음 공격의 종료 판정을 기다립니다.
            // 같은 번호로 재공격해도 이전 모션의 normalizedTime으로 종료하지 않습니다.
            return;
        }

        if (state.fullPathHash == ComboStateHashes[_activeComboIndex])
        {
            _attackAnimationStarted = true;

            // 공격 상태에는 자동 복귀 전환을 두지 않고 마지막 프레임까지 재생합니다.
            if (state.normalizedTime >= 1f)
            {
                FinishAttack();
            }
        }
        else if (_attackAnimationStarted)
        {
            // 외부에서 다른 상태로 전환된 경우에도 공격 잠금이 남지 않게 합니다.
            ClearAttackState();
        }
    }

    public bool TryStartAttack()
    {
        if (!isActiveAndEnabled || !_playerMove.isActiveAndEnabled
            || _playerMove.CurrentState != PlayerMoveState.Locomotion || !_characterController.isGrounded)
        {
            return false;
        }

        if (_isAttacking)
        {
            return TryBufferAttack();
        }

        if (ComboRemainingTime <= 0f)
        {
            ResetCombo();
        }

        StartAttack(_nextComboIndex, Time.time);
        return true;
    }

    private bool TryBufferAttack()
    {
        if (_inputBufferTime <= 0f || HasBufferedAttack)
        {
            return false;
        }

        // 연타로 여러 단계를 예약하지 않고, 받은 클릭의 번호와 시각을 한 건만 저장합니다.
        _hasBufferedAttack = true;
        _bufferedComboIndex = NextCombo - 1;
        _bufferedInputTime = Time.time;
        return true;
    }

    private void StartAttack(int comboIndex, float inputTime)
    {
        ClearBufferedAttack();
        _activeComboIndex = comboIndex;
        _nextComboIndex = (_activeComboIndex + 1) % ComboCount;
        _lastComboIndex = _activeComboIndex;
        // 예약 입력도 실행 시점이 아닌 원래 클릭 시각부터 ComboDelay를 계산합니다.
        _comboElapsed = Mathf.Max(0f, Time.time - inputTime);
        _attackAnimationStarted = false;
        _isAttacking = true;

        _animator.SetBool(IsMovingHash, false);
        _animator.ResetTrigger(RollTriggerHash);
        _animator.ResetTrigger(DashTriggerHash);
        _animator.CrossFadeInFixedTime(ComboStateHashes[_activeComboIndex],
            Mathf.Max(0f, _comboBlendDuration), 0, 0f);
    }

    public void CancelForRoll()
    {
        // 구르기는 현재 모션만 취소합니다. 콤보 순서와 클릭 이후 경과 시간은 유지합니다.
        ClearAttackState();
    }

    private void FinishAttack()
    {
        if (HasBufferedAttack && _characterController.isGrounded
            && _playerMove.isActiveAndEnabled && _playerMove.CurrentState == PlayerMoveState.Locomotion)
        {
            int comboIndex = _bufferedComboIndex;
            float inputTime = _bufferedInputTime;
            // IsAttacking을 유지해 Idle이나 이동 가능한 프레임을 사이에 넣지 않습니다.
            StartAttack(comboIndex, inputTime);
            return;
        }

        ClearAttackState();
        _animator.CrossFadeInFixedTime(IdleStateHash, Mathf.Max(0f, _comboBlendDuration), 0, 0f);
    }

    private void ClearAttackState()
    {
        _isAttacking = false;
        _activeComboIndex = -1;
        _attackAnimationStarted = false;
        ClearBufferedAttack();
    }

    private bool IsBufferValid()
    {
        return _inputBufferTime > 0f && Time.time - _bufferedInputTime <= _inputBufferTime;
    }

    private void ClearBufferedAttack()
    {
        _hasBufferedAttack = false;
        _bufferedComboIndex = 0;
        _bufferedInputTime = 0f;
    }

    private void ResetCombo()
    {
        _nextComboIndex = 0;
        _lastComboIndex = -1;
        _comboElapsed = 0f;
    }

    private float GetComboDelay(int index)
    {
        // 매번 Inspector 값을 읽으므로 진행 중인 타이머에도 실시간 수정이 반영됩니다.
        switch (index)
        {
            case 0:
                return Mathf.Max(0f, _comboDelay01);
            case 1:
                return Mathf.Max(0f, _comboDelay02);
            case 2:
                return Mathf.Max(0f, _comboDelay03);
            case 3:
                return Mathf.Max(0f, _comboDelay04);
            default:
                return Mathf.Max(0f, _comboDelay05);
        }
    }

    private void OnDisable()
    {
        bool wasAttacking = _isAttacking;
        ClearAttackState();
        ResetCombo();

        if (wasAttacking && _animator != null && _animator.isActiveAndEnabled)
        {
            _animator.Play(IdleStateHash, 0, 0f);
        }
    }
}
