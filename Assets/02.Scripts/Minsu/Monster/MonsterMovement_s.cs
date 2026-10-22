using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterMovement_s : MonoBehaviour
{
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private Transform _target;
    [SerializeField, Min(0.1f)] private float _walkSpeed = 1.2f;
    [SerializeField, Min(0.1f)] private float _stopDistance = 1.2f;
    [SerializeField, Min(0f)] private float _resumeMargin = 0.2f;
    private Health_s _targetHealth;
    private float _nextTargetSearch;
    private float _nextRepath;

    public Transform Target
    {
        get
        {
            FindTarget();
            return _target;
        }
    }

    private void Awake()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        _agent.speed = _walkSpeed;
        _agent.stoppingDistance = _stopDistance;
        if (_target != null) _targetHealth = _target.GetComponentInParent<Health_s>();
    }

    private void FindTarget()
    {
        if (_target != null || Time.time < _nextTargetSearch) return;
        _nextTargetSearch = Time.time + 1f;
        var player = FindFirstObjectByType<PlayerMovement_s>();
        if (player == null) return;
        _target = player.transform;
        _targetHealth = player.GetComponent<Health_s>();
    }

    public bool ShouldFollow(bool fromIdle)
    {
        FindTarget();
        if (!isActiveAndEnabled || _agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh
            || _target == null || !_target.gameObject.activeInHierarchy || (_targetHealth != null && _targetHealth.IsDead))
        {
            return false;
        }
        Vector3 offset = _target.position - transform.position;
        offset.y = 0f;
        float distance = _stopDistance + (fromIdle ? _resumeMargin : 0f);
        return offset.sqrMagnitude > distance * distance;
    }

    public void Follow()
    {
        if (!ShouldFollow(false))
        {
            Stop();
            return;
        }
        _agent.isStopped = false;
        if (Time.time < _nextRepath) return;
        _nextRepath = Time.time + 0.2f;
        _agent.SetDestination(_target.position);
    }

    public void Stop()
    {
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return;
        _agent.ResetPath();
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _nextRepath = 0f;
    }

    private void OnDisable()
    {
        Stop();
    }
}
