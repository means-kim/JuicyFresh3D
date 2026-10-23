using UnityEngine;

public enum BossState
{
    Chase,
    MeleeAttack,
    ChargeAttack,
    ThrowAttack,
    Recovery
}

[RequireComponent(typeof(BossAttackController))]
public class BossCombatController : MonoBehaviour
{
    [Header("공격 대상")]
    [SerializeField] private Transform _target;

    [Header("공격 범위")]
    [SerializeField][Min(0f)] private float _meleeRange = 2.5f;

    [SerializeField][Min(0f)] private float _rangedRange = 12f;

    [Header("공격 간격")]
    [SerializeField][Min(0f)] private float _attackCooldown = 2f;

    private BossAttackController _attackController;
    private Animator _animator;

    [Header("현재 상태 · 실행 중 자동 관리")]
    [SerializeField] private BossState _currentState = BossState.Chase;

    private float _cooldownRemaining;

    private static readonly int AttackTypeHash = Animator.StringToHash("AttackType");
    private static readonly int IdleStateHash = Animator.StringToHash("Idle");
    private static readonly int WalkStateHash = Animator.StringToHash("Boss_Walk");

    public BossState CurrentState => _currentState;

    // 공격 간격 중에도 추적은 가능하지만, 남은 공격 모션과 복귀 전이는 먼저 끝나야 합니다.
    public bool CanChase => _attackController != null
                            && _animator != null
                            && _animator.isActiveAndEnabled
                            && _animator.runtimeAnimatorController != null
                            && !_attackController.IsAttacking
                            && (_currentState == BossState.Chase || _currentState == BossState.Recovery)
                            && IsChaseAnimationReady();

    private void Awake()
    {
        _attackController = GetComponent<BossAttackController>();
        _animator = GetComponent<Animator>();

        if (_attackController == null || _animator == null)
        {
            Debug.LogError("BossAttackController와 Animator가 필요합니다.", this);
            enabled = false;
            return;
        }

        _currentState = BossState.Chase;
        _cooldownRemaining = 0f;
    }

    private void OnEnable()
    {
        if (_attackController != null)
        {
            _attackController.AttackFinished += HandleAttackFinished;
        }
    }

    private void OnDisable()
    {
        if (_attackController != null)
        {
            _attackController.AttackFinished -= HandleAttackFinished;
        }
    }

    private void Update()
    {
        if (!_attackController.isActiveAndEnabled || !_animator.isActiveAndEnabled
                                                  || _animator.runtimeAnimatorController == null)
        {
            return;
        }

        switch (_currentState)
        {
            case BossState.Chase:
                TickChase();
                break;

            case BossState.MeleeAttack:
            case BossState.ChargeAttack:
            case BossState.ThrowAttack:
                TickAttack();
                break;

            case BossState.Recovery:
                TickRecovery(Time.deltaTime);
                break;
        }
    }

    private void TickChase()
    {
        // 기존 public 공격 메서드로 시작한 공격도 FSM에 반영합니다.
        if (_attackController.IsAttacking)
        {
            ChangeState(GetSelectedAttackState());
            return;
        }

        if (_target == null || !IsLocomotionReady())
        {
            return;
        }

        Vector3 direction = _target.position - transform.position;

        // 높이 차이를 제외한 수평 거리로 판단
        direction.y = 0f;

        float distance = direction.magnitude;

        // 근접범위가 원거리범위 안에 포함되므로 먼저 검사
        if (distance <= _meleeRange)
        {
            _attackController.TryStartAttack(BossAttackType.Melee);
        }
        else if (distance <= _rangedRange)
        {
            // 가중치와 Inspector 설정은 기존 AttackController를 그대로 사용합니다.
            _attackController.TryStartRangedAttack();
        }

        // 공격 요청이 실제로 수락된 경우에만 공격 상태로 전환합니다.
        if (_attackController.IsAttacking)
        {
            ChangeState(GetSelectedAttackState());
        }
    }

    private void TickAttack()
    {
        // 이 상태에서는 거리 재평가나 다른 패턴 선택을 하지 않습니다.
        // 컴포넌트 비활성화 중 종료 이벤트를 놓쳤을 때도 복구할 수 있습니다.
        if (!_attackController.IsAttacking)
        {
            ChangeState(BossState.Recovery);
        }
    }

    private void TickRecovery(float deltaTime)
    {
        if (_attackController.IsAttacking)
        {
            ChangeState(GetSelectedAttackState());
            return;
        }

        // 추적 이동은 BossMovement가 CanChase를 확인한 뒤 재개합니다.
        _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);

        if (_cooldownRemaining > 0f || !IsLocomotionReady())
        {
            return;
        }

        ChangeState(BossState.Chase);
    }

    private BossState GetSelectedAttackState()
    {
        // AttackController가 실제로 선택해 Animator에 전달한 값을 읽습니다.
        BossAttackType attackType = (BossAttackType)_animator.GetInteger(AttackTypeHash);

        switch (attackType)
        {
            case BossAttackType.Charge:
                return BossState.ChargeAttack;

            case BossAttackType.Throw:
                return BossState.ThrowAttack;

            default:
                return BossState.MeleeAttack;
        }
    }

    private bool IsLocomotionReady()
    {
        if (_animator.IsInTransition(0))
        {
            return false;
        }

        int stateHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        return IsLocomotionState(stateHash);
    }

    private bool IsChaseAnimationReady()
    {
        int currentStateHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        if (!IsLocomotionState(currentStateHash))
        {
            return false;
        }

        // Idle와 Walk 사이의 전이는 이동을 유지해야 걷기가 끊기지 않습니다.
        // 공격에서 Idle로 복귀하는 전이는 위의 현재 상태 검사에서 차단됩니다.
        return !_animator.IsInTransition(0)
               || IsLocomotionState(_animator.GetNextAnimatorStateInfo(0).shortNameHash);
    }

    private bool IsLocomotionState(int stateHash)
    {
        return stateHash == IdleStateHash || stateHash == WalkStateHash;
    }

    private void ChangeState(BossState nextState)
    {
        if (_currentState == nextState)
        {
            return;
        }

        _currentState = nextState;

        if (nextState == BossState.Recovery)
        {
            // 기존과 같이 공격 종료 신호를 받은 시점부터 간격을 계산합니다.
            _cooldownRemaining = Mathf.Max(0f, _attackCooldown);
        }
        else
        {
            _cooldownRemaining = 0f;
        }
    }

    private void HandleAttackFinished()
    {
        if (_currentState == BossState.MeleeAttack
            || _currentState == BossState.ChargeAttack
            || _currentState == BossState.ThrowAttack)
        {
            ChangeState(BossState.Recovery);
        }
    }
}