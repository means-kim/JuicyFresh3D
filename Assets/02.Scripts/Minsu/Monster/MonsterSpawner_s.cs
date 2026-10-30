using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class MonsterSpawner_s : MonoBehaviour
{
    [SerializeField] private Health_s _monsterPrefab;
    [SerializeField] private Health_s _player;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField, Min(1)] private int _maxAlive = 10;
    [SerializeField, Min(0.1f)] private float _spawnInterval = 1f;
    [SerializeField, Min(0f)] private float _initialDelay = 2f;
    [SerializeField, Min(0f)] private float _minimumPlayerDistance = 5f;
    [SerializeField, Min(0.1f)] private float _sampleRadius = 0.75f;
    [SerializeField, Min(0f)] private float _clearance = 0.15f;
    [SerializeField, Min(0f)] private float _corpseLifetime = 5f;
    [SerializeField] private LayerMask _blockingLayers = Physics.DefaultRaycastLayers;
    [Header("Spawn Telegraph")]
    [SerializeField] private GameObject _spawnTelegraph;
    [SerializeField, Min(0f)] private float _telegraphDuration = 0.8f;
    [SerializeField, Min(0f)] private float _telegraphLinger = 1f;
    [SerializeField, Min(0f)] private float _telegraphPlayerClearance = 1f;
    [Header("Corpse Sink")]
    [SerializeField, Min(0f)] private float _sinkDuration = 1.5f;
    [SerializeField, Min(0f)] private float _sinkDepth = 1.2f;
    private readonly List<SpawnedMonster_s> _monsters = new List<SpawnedMonster_s>();
    private readonly List<PendingSpawn_s> _pending = new List<PendingSpawn_s>();
    private NavMeshAgent _prefabAgent;
    private NavMeshPath _path;
    private float _nextSpawnTime;
    private int _nextPoint;
    public int AliveCount { get; private set; }

    private sealed class SpawnedMonster_s
    {
        public Health_s Health;
        public Action DeathHandler;
        public bool Dead;
        public float RemoveAt;
        public bool Sinking;
        public float SinkStart;
        public Vector3 SinkOrigin;
    }

    private sealed class PendingSpawn_s
    {
        public Vector3 Position;
        public float SpawnAt;
        public GameObject Effect;
    }

    private void Awake()
    {
        _path = new NavMeshPath();
        if (_monsterPrefab != null)
        {
            _prefabAgent = _monsterPrefab.GetComponent<NavMeshAgent>();
        }
        if (_monsterPrefab == null || _prefabAgent == null || !_prefabAgent.enabled
            || !_monsterPrefab.gameObject.activeSelf || _player == null
            || _spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogError("MonsterSpawner: 활성 몬스터 프리팹, NavMeshAgent, 플레이어, 스폰 지점이 필요합니다.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        _nextSpawnTime = Time.time + Mathf.Max(0f, _initialDelay);
        _nextPoint = 0;
    }

    private void Update()
    {
        CleanupMonsters();
        if (_player == null || !_player.isActiveAndEnabled || _player.IsDead)
        {
            ClearPending();
            return;
        }
        if (Time.deltaTime <= 0f) return;
        UpdatePending();
        // 예고 중인 몬스터도 동시 생존 수에 포함한다.
        if (AliveCount + _pending.Count >= Mathf.Max(1, _maxAlive) || Time.time < _nextSpawnTime) return;

        // 실패해도 간격을 두고 재시도하며, 한 프레임에 여러 마리를 몰아서 생성하지 않는다.
        _nextSpawnTime = Time.time + Mathf.Max(0.1f, _spawnInterval);
        for (int attempt = 0; attempt < _spawnPoints.Length; attempt++)
        {
            int index = (_nextPoint + attempt) % _spawnPoints.Length;
            if (_spawnPoints[index] == null || !TryGetSpawnPosition(_spawnPoints[index].position, out Vector3 position)) continue;
            if (!BeginSpawn(position)) continue;
            _nextPoint = (index + 1) % _spawnPoints.Length;
            break;
        }
    }

    private bool BeginSpawn(Vector3 position)
    {
        if (_spawnTelegraph == null || _telegraphDuration <= 0f) return Spawn(position);
        var effect = Instantiate(_spawnTelegraph, position, Quaternion.identity);
        _pending.Add(new PendingSpawn_s { Position = position, SpawnAt = Time.time + _telegraphDuration, Effect = effect });
        return true;
    }

    private void UpdatePending()
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var pending = _pending[i];
            if (Time.time < pending.SpawnAt) continue;
            Vector3 offset = _player.transform.position - pending.Position;
            offset.y = 0f;
            // 예고 지점 위에 플레이어가 서 있으면 겹쳐 생성되지 않도록 잠시 미룬다.
            if (offset.sqrMagnitude < _telegraphPlayerClearance * _telegraphPlayerClearance)
            {
                pending.SpawnAt = Time.time + 0.2f;
                continue;
            }
            _pending.RemoveAt(i);
            Spawn(pending.Position);
            if (pending.Effect != null) Destroy(pending.Effect, _telegraphLinger);
        }
    }

    private void ClearPending()
    {
        foreach (var pending in _pending)
        {
            if (pending.Effect != null) Destroy(pending.Effect);
        }
        _pending.Clear();
    }

    private bool TryGetSpawnPosition(Vector3 point, out Vector3 position)
    {
        position = default;
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = _prefabAgent.agentTypeID,
            areaMask = _prefabAgent.areaMask
        };
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, Mathf.Max(0.1f, _sampleRadius), filter)) return false;
        if (Mathf.Abs(hit.position.y - point.y) > 0.5f)
        {
            return false;
        }
        Vector3 offset = hit.position - _player.transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude < _minimumPlayerDistance * _minimumPlayerDistance)
        {
            return false;
        }
        if (!NavMesh.SamplePosition(_player.transform.position, out NavMeshHit target, 2f, filter)
            || !NavMesh.CalculatePath(hit.position, target.position, filter, _path)
            || _path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        Vector3 scale = _monsterPrefab.transform.lossyScale;
        float radius = _prefabAgent.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) + _clearance;
        float height = Mathf.Max(_prefabAgent.height * Mathf.Abs(scale.y), radius * 2f);
        // 지면 자체는 제외하고 몸이 들어갈 공간을 검사한다.
        Vector3 bottom = hit.position + Vector3.up * (radius + 0.08f);
        Vector3 top = hit.position + Vector3.up * (height - radius + 0.08f);
        if (Physics.CheckCapsule(bottom, top, radius, _blockingLayers, QueryTriggerInteraction.Ignore)) return false;
        foreach (var monster in _monsters)
        {
            if (monster.Dead || monster.Health == null)
            {
                continue;
            }
            Vector3 separation = monster.Health.transform.position - hit.position;
            separation.y = 0f;
            if (separation.sqrMagnitude < radius * radius * 4f)
            {
                return false;
            }
        }
        foreach (var pending in _pending)
        {
            Vector3 separation = pending.Position - hit.position;
            separation.y = 0f;
            if (separation.sqrMagnitude < radius * radius * 4f) return false;
        }
        position = hit.position;
        return true;
    }

    private bool Spawn(Vector3 position)
    {
        Vector3 direction = _player.transform.position - position;
        direction.y = 0f;
        Quaternion rotation = direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        var health = Instantiate(_monsterPrefab, position, rotation);
        var agent = health.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isOnNavMesh || !agent.Warp(position))
        {
            health.gameObject.SetActive(false);
            Destroy(health.gameObject);
            return false;
        }
        var monster = new SpawnedMonster_s { Health = health };
        monster.DeathHandler = () => OnMonsterDied(monster);
        health.Died += monster.DeathHandler;
        _monsters.Add(monster);
        AliveCount++;
        return true;
    }

    private void OnMonsterDied(SpawnedMonster_s monster)
    {
        if (monster.Dead) return;
        monster.Dead = true;
        monster.RemoveAt = Time.time + Mathf.Max(0f, _corpseLifetime);
        AliveCount--;
        if (monster.Health == null) return;
        monster.Health.Died -= monster.DeathHandler;
        // 시체가 살아 있는 적의 경로 회피를 계속 방해하지 않도록 한다.
        var agent = monster.Health.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }
    }

    private void CleanupMonsters()
    {
        for (int i = _monsters.Count - 1; i >= 0; i--)
        {
            var monster = _monsters[i];
            if (monster.Health != null && monster.Health.IsDead && !monster.Dead)
            {
                OnMonsterDied(monster);
            }
            bool unavailable = monster.Health == null || !monster.Health.gameObject.activeInHierarchy;
            if (!unavailable && (!monster.Dead || Time.time < monster.RemoveAt)) continue;
            if (!unavailable && UpdateSink(monster)) continue;
            if (!monster.Dead)
            {
                AliveCount--;
            }
            Release(monster);
            _monsters.RemoveAt(i);
        }
    }

    // 가라앉는 중이면 true를 반환하고, 끝나면 false를 반환해 제거로 넘어간다.
    private bool UpdateSink(SpawnedMonster_s monster)
    {
        if (_sinkDuration <= 0f || _sinkDepth <= 0f) return false;
        var body = monster.Health.transform;
        if (!monster.Sinking)
        {
            monster.Sinking = true;
            monster.SinkStart = Time.time;
            monster.SinkOrigin = body.position;
        }
        float progress = (Time.time - monster.SinkStart) / _sinkDuration;
        if (progress >= 1f) return false;
        body.position = monster.SinkOrigin + Vector3.down * (_sinkDepth * progress * progress);
        return true;
    }

    private void Release(SpawnedMonster_s monster)
    {
        if (monster.Health == null) return;
        monster.Health.Died -= monster.DeathHandler;
        monster.Health.gameObject.SetActive(false);
        Destroy(monster.Health.gameObject);
    }

    private void OnDisable()
    {
        foreach (var monster in _monsters) Release(monster);
        _monsters.Clear();
        ClearPending();
        AliveCount = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (_spawnPoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (var point in _spawnPoints)
        {
            if (point == null) continue;
            Gizmos.DrawWireSphere(point.position, _sampleRadius);
            Gizmos.DrawLine(point.position, point.position + Vector3.up * 2f);
        }
    }
}
