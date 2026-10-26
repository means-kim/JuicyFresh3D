using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(MonsterHitStun_s))]
public class MonsterKnockback_s : MonoBehaviour
{
    [Header("넉백 거리 / 시간")]
    [SerializeField, Min(0f)] private float _normalDistance = 0.15f;

    [SerializeField, Min(0.01f)] private float _normalDuration = 0.12f;
    [SerializeField, Min(0f)] private float _fourthDistance = 0.6f;
    [SerializeField, Min(0.01f)] private float _fourthDuration = 0.2f;

    [Tooltip("차징 1/2/3단계의 넉백 거리입니다.")]
    [SerializeField] private Vector3 _chargeDistances = new Vector3(0.45f, 0.75f, 1.1f);

    [Tooltip("차징 1/2/3단계의 넉백 시간입니다.")]
    [SerializeField] private Vector3 _chargeDurations = new Vector3(0.18f, 0.24f, 0.28f);

    [SerializeField] private LayerMask _obstacleLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.001f)] private float _skin = 0.02f;
    private NavMeshAgent _agent;
    private Collider _body;
    private Health_s _health;
    private MonsterState_s _state;
    private MonsterHitStun_s _hitStun;
    private Transform _attacker;
    private Vector3 _direction;
    private float _distance;
    private float _duration;
    private float _elapsed;
    private bool _active;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _body = GetComponent<Collider>();
        _health = GetComponent<Health_s>();
        _state = GetComponent<MonsterState_s>();
        _hitStun = GetComponent<MonsterHitStun_s>();
    }

    public void ApplyHit(Vector3 direction, Transform attacker, int step, bool charged, int chargeStage)
    {
        if (!isActiveAndEnabled || _health == null || _health.IsDead || _state == null || !_state.isActiveAndEnabled || _state.CurrentStateTypeS != MonsterStateType_s.Hit || !_hitStun.isActiveAndEnabled || !CanMove()) return;

        int stage = Mathf.Clamp(chargeStage, 1, 3) - 1;
        float distance = Mathf.Max(0f, charged ? _chargeDistances[stage]
            : step == 4 ? _fourthDistance : _normalDistance);
        float duration = Mathf.Max(0.01f, charged ? _chargeDurations[stage]
            : step == 4 ? _fourthDuration : _normalDuration);
        direction.y = 0f;
        if (distance <= 0f || direction.sqrMagnitude < 0.000001f) return;
        // 약한 재피격은 진행 중인 강한 넉백을 줄이거나 방향을 바꾸지 않는다.
        if (_active && distance < _distance) return;
        _direction = direction.normalized;
        _attacker = attacker;
        _distance = distance;
        _duration = duration;
        _elapsed = 0f;
        _active = true;
        _agent.ResetPath();
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _hitStun.ExtendHitStun(duration);
    }

    private bool CanMove()
    {
        return _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh && !_agent.isOnOffMeshLink;
    }

    public void TickKnockback(float deltaTime)
    {
        if (!_active) return;
        if (!isActiveAndEnabled || !CanMove() || _health.IsDead
            || _state.CurrentStateTypeS != MonsterStateType_s.Hit)
        {
            CancelKnockback();
            return;
        }

        if (deltaTime <= 0f) return;

        float previous = _elapsed / _duration;
        _elapsed = Mathf.Min(_duration, _elapsed + deltaTime);
        float current = _elapsed / _duration;
        // Ease-out 이동량을 구간 적분해 프레임마다 힘이 누적되지 않게 한다.
        float requested = _distance * ((2f * current - current * current) - (2f * previous - previous * previous));
        float allowed = LimitMovement(requested);
        if (allowed > 0f)
        {
            _agent.Move(_direction * allowed);
        }
        if (_elapsed >= _duration || allowed < requested - 0.0001f)
        {
            CancelKnockback();
        }
    }

    private float LimitMovement(float distance)
    {
        float skin = Mathf.Max(0.001f, _skin);
        if (_agent.Raycast(transform.position + _direction * (distance + skin), out NavMeshHit edge))
        {
            Vector3 offset = edge.position - transform.position;
            offset.y = 0f;
            distance = Mathf.Min(distance, Mathf.Max(0f, Vector3.Dot(offset, _direction) - skin));
        }

        if (distance <= 0f)
        {
            return 0f;
        }

        // 몸의 폭으로 이동 경로를 검사해 NavMesh에 반영되지 않은 장애물에서도 멈춘다.
        Bounds bounds = _body != null
            ? _body.bounds
            : new Bounds(transform.position + Vector3.up * (_agent.height * 0.5f),
                new Vector3(_agent.radius * 2f, _agent.height, _agent.radius * 2f));
        float radius = Mathf.Max(0.01f,
            Mathf.Min(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y) - skin);
        float halfSegment = Mathf.Max(0f, bounds.extents.y - radius - skin);
        Vector3 top = bounds.center + Vector3.up * halfSegment;
        Vector3 bottom = bounds.center - Vector3.up * halfSegment;
        foreach (var hit in Physics.CapsuleCastAll(top, bottom, radius, _direction, distance + skin,
                     _obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (_attacker != null && hit.transform.IsChildOf(_attacker)))
            {
                continue;
            }
            // 접선 방향의 바닥 접촉은 벽으로 취급하지 않는다.
            if (Vector3.Dot(hit.normal, _direction) >= -0.01f)
            {
                continue;
            }
            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - skin));
        }

        return distance;
    }

    public void CancelKnockback()
    {
        _active = false;
        _elapsed = 0f;
        _attacker = null;
        // 추적 재개는 기존 Walk 상태가 담당한다.
    }

    private void OnDisable()
    {
        CancelKnockback();
    }
}