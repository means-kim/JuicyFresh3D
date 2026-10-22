using UnityEngine;

public enum BossAttackType
{
    Melee = 0,
    Charge = 1,
    Throw = 2
}

public class BossAttackController : MonoBehaviour
{
    [Header("원거리공격 가중치")]
    [SerializeField] [Min(0)] private int chargeWeight = 50;
    [SerializeField] [Min(0)] private int throwWeight = 50;
    private Animator _animator;
    private bool _isAttacking;

    public bool IsAttacking => _isAttacking;
    public event System.Action AttackFinished;

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        if (_animator == null)
        {
            Debug.LogError("보스 애니메이터 확인하세요", this);
            enabled = false;
        }
    }

    public void TryStartAttack(BossAttackType attackType)
    {
        if (_animator == null || IsAttacking) return;

        _isAttacking = true;

        _animator.SetInteger("AttackType", (int)attackType);
        _animator.SetTrigger("AttackStart");
    }

    public void OnAttackFinished()
    {
        if (!_isAttacking) return;
        _isAttacking = false;
        AttackFinished?.Invoke();
    }

    public void TryStartRangedAttack()
    {
        if (_animator == null || IsAttacking) return;

        if (chargeWeight < 0 || throwWeight < 0)
        {
            Debug.LogWarning("공격 가중치 다시설정하세요", this);
            return;
        }

        int totalWeight = chargeWeight + throwWeight;
        int randomValue = Random.Range(0, totalWeight);

        BossAttackType selectedAttack;

        if (randomValue < chargeWeight)
            selectedAttack = BossAttackType.Charge;
        else
            selectedAttack = BossAttackType.Throw;

        TryStartAttack(selectedAttack);
    }
}