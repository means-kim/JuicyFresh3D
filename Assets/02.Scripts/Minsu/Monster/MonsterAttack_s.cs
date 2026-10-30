using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health_s), typeof(MonsterAnimation_s))]
public class MonsterAttack_s : MonoBehaviour
{
    [SerializeField] private MonsterMovement_s _movement;
    [SerializeField] private MonsterAnimation_s _animation;
    [SerializeField] private MonsterState_s _state;
    [SerializeField] private Health_s _health;
    [SerializeField] private Collider[] _handHitboxes;
    [SerializeField, Min(0.1f)] private float _attackDistance = 0.95f;
    [SerializeField, Min(1)] private int _damage = 10;
    [SerializeField, Min(0f)] private float _cooldown = 1f;
    [SerializeField] private Vector2 _damageWindow = new Vector2(0.12f, 0.78f);
    private readonly HashSet<Health_s> _hitTargets = new HashSet<Health_s>();
    private bool _attacking;
    private float _nextAttackTime;

    public event System.Action AttackStarted;
    public bool IsAttacking => _attacking;
    public float DamageWindowStart => _damageWindow.x;
    public int HitsThisAttack => _hitTargets.Count;
    public bool IsFinished => !_attacking || !HasLivingTarget
        || (_animation.TryGetAttackTime(out float time) && time >= 1f);

    private bool HasLivingTarget
    {
        get
        {
            var target = _movement != null ? _movement.Target : null;
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            var health = target.GetComponentInParent<Health_s>();
            return health != null && health.isActiveAndEnabled && !health.IsDead;
        }
    }

    public bool CanBeginAttack
    {
        get
        {
            if (!isActiveAndEnabled || _attacking || _health == null || _health.IsDead
                || Time.time < _nextAttackTime || !_animation.CanPlayAttack || !HasLivingTarget)
            {
                return false;
            }
            Vector3 offset = _movement.Target.position - transform.position;
            return offset.sqrMagnitude <= _attackDistance * _attackDistance;
        }
    }

    public bool TryGetAttackTime(out float time)
    {
        return _animation.TryGetAttackTime(out time);
    }

    private void Awake()
    {
        if (_movement == null) _movement = GetComponent<MonsterMovement_s>();
        if (_animation == null) _animation = GetComponent<MonsterAnimation_s>();
        if (_state == null) _state = GetComponent<MonsterState_s>();
        if (_health == null) _health = GetComponent<Health_s>();
        SetHitboxes(false);
    }

    private void OnEnable()
    {
        _health.Damaged += OnDamaged;
        _health.Died += CancelAttack;
    }

    private void OnDamaged(int damage)
    {
        CancelAttack();
    }

    public void BeginAttack()
    {
        SetHitboxes(false);
        _hitTargets.Clear();
        _attacking = true;
        var target = _movement.Target;
        if (target != null)
        {
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        }
        AttackStarted?.Invoke();
    }

    public void CancelAttack()
    {
        if (_attacking) _nextAttackTime = Time.time + _cooldown;
        _attacking = false;
        SetHitboxes(false);
    }

    private void LateUpdate()
    {
        RefreshHitboxes();
    }

    private void FixedUpdate()
    {
        RefreshHitboxes();
    }

    public void RefreshHitboxes()
    {
        bool active = isActiveAndEnabled && _attacking && !_health.IsDead && HasLivingTarget
            && _state != null && _state.isActiveAndEnabled && _state.CurrentStateTypeS == MonsterStateType_s.Attack
            && _animation.TryGetAttackTime(out float time) && time >= _damageWindow.x && time <= _damageWindow.y;
        SetHitboxes(active);
    }

    private void SetHitboxes(bool active)
    {
        if (_handHitboxes == null) return;
        foreach (var hitbox in _handHitboxes)
        {
            if (hitbox != null) hitbox.enabled = active;
        }
    }

    public void TryHit(Collider source, Collider other)
    {
        RefreshHitboxes();
        if (source == null || !source.enabled || _handHitboxes == null
            || System.Array.IndexOf(_handHitboxes, source) < 0 || other.isTrigger
            || other.transform.IsChildOf(transform))
        {
            return;
        }
        // 몬스터끼리의 피해와 몸 콜라이더를 통한 공격을 제외한다.
        var player = other.GetComponentInParent<PlayerMovement_s>();
        if (player == null) return;
        var health = player.GetComponent<Health_s>();
        if (health == null || health.IsDead || !_hitTargets.Add(health)) return;
        if (!health.TakeDamage(_damage)) _hitTargets.Remove(health);
    }

    private void OnDisable()
    {
        CancelAttack();
        if (_health == null) return;
        _health.Damaged -= OnDamaged;
        _health.Died -= CancelAttack;
    }
}
