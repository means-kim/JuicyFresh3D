using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

public enum BossAttackType
{
    Melee = 0,
    Charge = 1,
    Throw = 2
}

public class BossAttackController : MonoBehaviour
{
    [FormerlySerializedAs("chargeWeight")]
    [Header("원거리공격 가중치")]
    [SerializeField][Min(0)] private int _chargeWeight = 50;
    [SerializeField][Min(0)] private int _throwWeight = 50;

    [Header("근접 회피 Window · 원본 애니메이션 프레임")]
    [SerializeField][Min(0)] private int _meleeDodgeStartFrame = 40;
    [SerializeField][Min(0)] private int _meleeDodgeEndFrame = 60;
    [SerializeField] private bool _logDodgeSuccess = true;

    private Animator _animator;
    private BossCombatController _combatController;
    private PlayerMove _targetPlayer;
    private Transform _subscribedTarget;
    private bool _isAttacking;
    private bool _canJudgeMeleeDodge;
    private readonly List<AnimatorClipInfo> _clipInfos = new List<AnimatorClipInfo>(2);

    private static readonly int AttackTypeHash = Animator.StringToHash("AttackType");
    private static readonly int AttackStartHash = Animator.StringToHash("AttackStart");
    private static readonly int MeleeStateHash = Animator.StringToHash("Base Layer.Boss_Attack01");

    public bool IsAttacking => _isAttacking;
    public float CurrentMeleeFrame => TryGetMeleeFrame(out float frame) ? frame : -1f;
    public bool IsDodgeWindowOpen => _canJudgeMeleeDodge && TryGetMeleeFrame(out float frame)
        && _meleeDodgeEndFrame > _meleeDodgeStartFrame
        && frame + 0.0001f >= _meleeDodgeStartFrame && frame + 0.0001f < _meleeDodgeEndFrame;
    public event System.Action AttackFinished;
    public event System.Action DodgeSucceeded;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _combatController = GetComponent<BossCombatController>();

        if (_animator == null)
        {
            Debug.LogError("보스 애니메이터 확인하세요", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        RefreshTargetSubscription();
    }

    private void Update()
    {
        // Inspector에서 공격 대상이 바뀌어도 이전 구독을 해제하고 새 대상으로 연결합니다.
        RefreshTargetSubscription();
    }

    private void OnDisable()
    {
        UnsubscribeTarget();
        _canJudgeMeleeDodge = false;
    }

    private void RefreshTargetSubscription()
    {
        Transform target = _combatController != null ? _combatController.Target : null;

        if (_subscribedTarget == target && _targetPlayer != null)
        {
            return;
        }

        UnsubscribeTarget();
        _subscribedTarget = target;
        _targetPlayer = target != null ? target.GetComponentInParent<PlayerMove>() : null;

        if (_targetPlayer != null)
        {
            _targetPlayer.RollStarted += HandleRollStarted;
        }
    }

    private void UnsubscribeTarget()
    {
        if (_targetPlayer != null)
        {
            _targetPlayer.RollStarted -= HandleRollStarted;
        }

        _targetPlayer = null;
        _subscribedTarget = null;
    }

    private bool TryGetMeleeFrame(out float frame)
    {
        frame = -1f;

        if (!isActiveAndEnabled || !_isAttacking || _animator == null
            || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        bool isTransitioning = _animator.IsInTransition(0);
        AnimatorStateInfo next = isTransitioning ? _animator.GetNextAnimatorStateInfo(0) : default;

        if (isTransitioning && next.fullPathHash == MeleeStateHash)
        {
            state = next;
            _animator.GetNextAnimatorClipInfo(0, _clipInfos);
        }
        else if (state.fullPathHash == MeleeStateHash && !isTransitioning)
        {
            _animator.GetCurrentAnimatorClipInfo(0, _clipInfos);
        }
        else
        {
            // 공격에서 다른 모션으로 나가는 도중에는 Window가 남지 않습니다.
            return false;
        }

        if (_clipInfos.Count == 0 || _clipInfos[0].clip == null)
        {
            return false;
        }

        AnimationClip clip = _clipInfos[0].clip;
        frame = state.normalizedTime * clip.length * clip.frameRate;
        return true;
    }

    private void HandleRollStarted(PlayerMove player)
    {
        if (player == null || player != _targetPlayer || !player.IsRolling
            || !IsDodgeWindowOpen || _combatController == null
            || !_combatController.IsWithinMeleeRange(player.transform.position))
        {
            return;
        }

        float frame = CurrentMeleeFrame;
        // 이벤트 처리 중에도 다시 성공하지 못하도록 먼저 이번 공격의 판정을 닫습니다.
        _canJudgeMeleeDodge = false;

        if (_logDodgeSuccess)
        {
            Debug.Log($"회피 성공 · 근접 공격 {frame:F2}프레임", this);
        }

        // 연출 담당에는 성공 신호만 전달합니다.
        DodgeSucceeded?.Invoke();
    }

    public void TryStartAttack(BossAttackType attackType)
    {
        if (!isActiveAndEnabled || _animator == null || IsAttacking)
        {
            return;
        }

        _isAttacking = true;
        _canJudgeMeleeDodge = attackType == BossAttackType.Melee;

        _animator.SetInteger(AttackTypeHash, (int)attackType);
        _animator.SetTrigger(AttackStartHash);
    }

    public void OnAttackFinished()
    {
        _canJudgeMeleeDodge = false;

        if (!_isAttacking)
        {
            return;
        }

        _isAttacking = false;
        AttackFinished?.Invoke();
    }

    public void TryStartRangedAttack()
    {
        if (_animator == null || IsAttacking)
        {
            return;
        }

        if (_chargeWeight < 0 || _throwWeight < 0)
        {
            Debug.LogWarning("공격 가중치 다시설정하세요", this);
            return;
        }

        int totalWeight = _chargeWeight + _throwWeight;
        int randomValue = Random.Range(0, totalWeight);

        BossAttackType selectedAttack;

        if (randomValue < _chargeWeight)
        {
            selectedAttack = BossAttackType.Charge;
        }
        else
        {
            selectedAttack = BossAttackType.Throw;
        }

        TryStartAttack(selectedAttack);
    }

    private void OnDrawGizmosSelected()
    {
        BossCombatController combat = _combatController != null
            ? _combatController : GetComponent<BossCombatController>();

        if (combat == null)
        {
            return;
        }

        Gizmos.color = IsDodgeWindowOpen ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, combat.MeleeRange);
    }
}
