using System;
using UnityEngine;

public class BossStatus_k : MonoBehaviour
{
    [SerializeField][Min(1f)] private float _maxHp = 300f;

    private float _currentHp;
    private Animator _animator;
    private BossCombatController _combatController;
    private BossAttackController _attackController;
    private Collider _weaponCollider;

    private static readonly int DieHash = Animator.StringToHash("Die");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    public float MaxHp => _maxHp;
    public float CurrentHp => _currentHp;
    public bool IsDead => CurrentHp <= 0f;

    // 인자: 현재 체력, 최대 체력
    public event Action<float, float> Damaged;
    public event Action Died;

    private void Awake()
    {
        _currentHp = _maxHp;
        _animator = GetComponent<Animator>();
        _combatController = GetComponent<BossCombatController>();
        _attackController = GetComponent<BossAttackController>();

        BossWeaponHitbox_k weapon = GetComponentInChildren<BossWeaponHitbox_k>(true);
        if (weapon != null) _weaponCollider = weapon.GetComponent<Collider>();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        _currentHp = Mathf.Max(0f, _currentHp - amount);
        Damaged?.Invoke(_currentHp, _maxHp);

        if (IsDead)
        {
            Die();
            Died?.Invoke();
        }
    }

    // 공격·추적을 멈추고 Death → Death_Loop 로 전환합니다. BossMovement 는 중력 처리를 위해 켜 둡니다.
    private void Die()
    {
        if (_combatController != null) _combatController.enabled = false;
        if (_attackController != null) _attackController.enabled = false;
        if (_weaponCollider != null) _weaponCollider.enabled = false;

        if (_animator == null) return;
        _animator.SetBool(IsMovingHash, false);
        _animator.SetTrigger(DieHash);
    }

    // 테스트용 회복. 체력이 바뀌는 건 같아서 Damaged 이벤트를 그대로 사용한다.
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        _currentHp = Mathf.Min(_maxHp, _currentHp + amount);
        Damaged?.Invoke(_currentHp, _maxHp);
    }
}
