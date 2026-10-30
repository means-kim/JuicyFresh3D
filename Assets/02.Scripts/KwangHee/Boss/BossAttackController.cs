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

    [Header("돌진 회피 Window · Attack02_End 원본 애니메이션 프레임")]
    [SerializeField][Min(0)] private int _chargeDodgeStartFrame = 40;
    [SerializeField][Min(0)] private int _chargeDodgeEndFrame = 80;

    private Animator _animator;
    private BossCombatController _combatController;
    private PlayerMove _targetPlayer;
    private Transform _subscribedTarget;
    private bool _isAttacking;
    private bool _canJudgeMeleeDodge;
    private bool _canJudgeChargeDodge;
    private readonly List<AnimatorClipInfo> _clipInfos = new(2);
    private BossChargeArea_k _chargeArea;

    private static readonly int AttackTypeHash = Animator.StringToHash("AttackType");
    private static readonly int AttackStartHash = Animator.StringToHash("AttackStart");
    private static readonly int MeleeStateHash = Animator.StringToHash("Base Layer.Boss_Attack01");
    private static readonly int ChargeEndStateHash = Animator.StringToHash("Base Layer.Boss_ChargeEnd");

    public bool IsAttacking => _isAttacking;
    public float CurrentMeleeFrame => TryGetAttackFrame(MeleeStateHash, out float frame) ? frame : -1f;
    public float CurrentChargeFrame => TryGetAttackFrame(ChargeEndStateHash, out float frame) ? frame : -1f;
    public bool IsMeleeDodgeWindowOpen => _canJudgeMeleeDodge
        && IsWithinDodgeWindow(MeleeStateHash, _meleeDodgeStartFrame, _meleeDodgeEndFrame);
    public bool IsChargeDodgeWindowActive => IsWithinDodgeWindow(ChargeEndStateHash, _chargeDodgeStartFrame, _chargeDodgeEndFrame)
        && _animator.GetInteger(AttackTypeHash) == (int)BossAttackType.Charge;
    public bool IsChargeDodgeWindowOpen => _canJudgeChargeDodge && IsChargeDodgeWindowActive;
    public bool IsDodgeWindowOpen => IsMeleeDodgeWindowOpen || IsChargeDodgeWindowOpen;
    public event System.Action AttackFinished;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _combatController = GetComponent<BossCombatController>();
        _chargeArea = GetComponent<BossChargeArea_k>();

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
        _canJudgeChargeDodge = false;

        if (_chargeArea != null)
        {
            _chargeArea.ClearChargeArea();
        }
    }

    private void RefreshTargetSubscription()
    {
        Transform target = _combatController != null ? _combatController.Target : null;

        if (_subscribedTarget == target && _targetPlayer != null) return;

        UnsubscribeTarget();
        _subscribedTarget = target;
        _targetPlayer = target != null ? target.GetComponentInParent<PlayerMove>() : null;

        if (_targetPlayer != null) _targetPlayer.RollStarted += HandleRollStarted;
    }

    private void UnsubscribeTarget()
    {
        if (_targetPlayer != null) _targetPlayer.RollStarted -= HandleRollStarted;

        _targetPlayer = null;
        _subscribedTarget = null;
    }

    private bool IsWithinDodgeWindow(int stateHash, int startFrame, int endFrame)
    {
        return endFrame > startFrame && TryGetAttackFrame(stateHash, out float frame)
               && frame + 0.0001f >= startFrame && frame + 0.0001f < endFrame;
    }

    private bool TryGetAttackFrame(int stateHash, out float frame)
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

        if (isTransitioning && next.fullPathHash == stateHash)
        {
            state = next;
            _animator.GetNextAnimatorClipInfo(0, _clipInfos);
        }
        else if (state.fullPathHash == stateHash && !isTransitioning)
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
        if (player == null || player != _targetPlayer || !player.IsRolling)
        {
            return;
        }

        bool meleeDodge = IsMeleeDodgeWindowOpen && _combatController != null
            && _combatController.IsWithinMeleeRange(player.transform.position);
        bool chargeDodge = IsChargeDodgeWindowOpen && _chargeArea != null
            && _chargeArea.IsWithinChargeArea(player.transform.position);

        if (!meleeDodge && !chargeDodge)
        {
            return;
        }
        // 이벤트 처리 중에도 다시 성공하지 못하도록 먼저 이번 공격의 판정을 닫습니다.
        _canJudgeMeleeDodge = false;
        _canJudgeChargeDodge = false;
        Debug.Log("회피성공!", this);
        // 연출 담당에는 성공 신호만 전달합니다.
        player.NotifyDodgeSucceeded();
    }

    public void TryStartAttack(BossAttackType attackType)
    {
        if (!isActiveAndEnabled || _animator == null || IsAttacking) return;

        _isAttacking = true;
        _canJudgeMeleeDodge = attackType == BossAttackType.Melee;
        _canJudgeChargeDodge = attackType == BossAttackType.Charge;

        _animator.SetInteger(AttackTypeHash, (int)attackType);
        _animator.SetTrigger(AttackStartHash);
    }

    public void OnAttackFinished()
    {
        _canJudgeMeleeDodge = false;
        _canJudgeChargeDodge = false;

        if (_chargeArea != null)
        {
            _chargeArea.ClearChargeArea();
        }

        if (!_isAttacking)
        {
            return;
        }

        _isAttacking = false;
        AttackFinished?.Invoke();
    }

    public void TryStartRangedAttack()
    {
        if (_animator == null || IsAttacking) return;

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
            ? _combatController
            : GetComponent<BossCombatController>();

        if (combat == null) return;

        Gizmos.color = IsMeleeDodgeWindowOpen ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, combat.MeleeRange);
    }
}
