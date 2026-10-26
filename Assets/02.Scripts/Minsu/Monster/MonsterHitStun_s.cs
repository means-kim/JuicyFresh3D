using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health_s))]
public class MonsterHitStun_s : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _hitStunDuration = 0.3f;
    private Health_s _health;
    private MonsterState_s _monster;
    private float _remainingTime;

    private void Awake()
    {
        _health = GetComponent<Health_s>();
        _monster = GetComponent<MonsterState_s>();
    }

    private void OnEnable()
    {
        _health.Damaged += OnDamaged;
    }

    private void OnDamaged(int damage)
    {
        if (_health.IsDead || _monster == null || !_monster.isActiveAndEnabled) return;
        _remainingTime = Mathf.Max(_remainingTime, _hitStunDuration);
        _monster.EnterHit();
    }

    public void TickHitStun(float deltaTime)
    {
        if (!isActiveAndEnabled) return;
        // 히트스톱 중에는 경직 시간도 흐르지 않는다.
        _remainingTime = Mathf.Max(0f, _remainingTime - deltaTime);
        if (_remainingTime <= 0f) _monster.EndHit();
    }

    public void CancelHitStun()
    {
        _remainingTime = 0f;
    }

    private void OnDisable()
    {
        _health.Damaged -= OnDamaged;
        CancelHitStun();
        if (_monster != null && _monster.isActiveAndEnabled) _monster.EndHit();
    }
}
