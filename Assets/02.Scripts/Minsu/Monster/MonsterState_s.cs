using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health_s), typeof(MonsterAnimation_s))]
public class MonsterState_s : MonoBehaviour
{
    [SerializeField] private Health_s _health;
    [SerializeField] private MonsterAnimation_s _animation;
    [SerializeField] private Collider _bodyCollider;
    [SerializeField] private MonsterMovement_s _movement;
    private State_s _state;
    private State_s _idle;
    private State_s _hit;
    private State_s _dead;
    private State_s _walk;

    public MonsterStateType_s CurrentStateTypeS => _state != null ? _state.Kind : MonsterStateType_s.Idle;

    private void Awake()
    {
        if (_health == null) _health = GetComponent<Health_s>();
        if (_animation == null) _animation = GetComponent<MonsterAnimation_s>();
        if (_bodyCollider == null) _bodyCollider = GetComponent<Collider>();
        if (_movement == null) _movement = GetComponent<MonsterMovement_s>();
        _idle = new State_s(this, MonsterStateType_s.Idle);
        _hit = new HitState_s(this);
        _dead = new State_s(this, MonsterStateType_s.Dead);
        _walk = new WalkState_s(this);
    }

    private void OnEnable()
    {
        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
    }

    private void Start()
    {
        if (_state == null) ChangeState(_health.IsDead ? _dead : _idle);
    }

    private void Update()
    {
        _state?.Tick();
    }

    private void OnDisable()
    {
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
        _movement?.Stop();
    }

    private void OnDamaged(int damage)
    {
        if (_health.IsDead || CurrentStateTypeS == MonsterStateType_s.Dead) return;
        if (_state == _hit) _hit.Enter();
        else ChangeState(_hit);
    }

    private void OnDied()
    {
        ChangeState(_dead);
    }

    private void ChangeState(State_s next)
    {
        if (_state == next) return;
        _state?.Exit();
        _state = next;
        _state.Enter();
    }

    private class State_s
    {
        protected readonly MonsterState_s Monster;
        public MonsterStateType_s Kind { get; }

        public State_s(MonsterState_s monster, MonsterStateType_s kind)
        {
            Monster = monster;
            Kind = kind;
        }

        public virtual void Enter()
        {
            if (Kind != MonsterStateType_s.Walk) Monster._movement?.Stop();
            Monster._animation.PlayState(Kind);
            if (Kind == MonsterStateType_s.Dead && Monster._bodyCollider != null)
            {
                Monster._bodyCollider.enabled = false;
            }
        }

        public virtual void Tick()
        {
            if (Kind == MonsterStateType_s.Idle && Monster._movement != null && Monster._movement.ShouldFollow(true))
            {
                Monster.ChangeState(Monster._walk);
            }
        }

        public virtual void Exit()
        {
        }
    }

    private sealed class WalkState_s : State_s
    {
        public WalkState_s(MonsterState_s monster) : base(monster, MonsterStateType_s.Walk)
        {
        }

        public override void Tick()
        {
            if (Monster._movement == null || !Monster._movement.ShouldFollow(false))
            {
                Monster.ChangeState(Monster._idle);
                return;
            }
            Monster._movement.Follow();
        }

        public override void Exit()
        {
            Monster._movement?.Stop();
        }
    }

    private sealed class HitState_s : State_s
    {
        public HitState_s(MonsterState_s monster) : base(monster, MonsterStateType_s.Hit)
        {
        }

        public override void Tick()
        {
            if (Monster._animation.IsHitFinished) Monster.ChangeState(Monster._idle);
        }
    }
}
