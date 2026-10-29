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
    private readonly List<SpawnedMonster_s> _monsters = new List<SpawnedMonster_s>();
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
        if (_player == null || !_player.isActiveAndEnabled || _player.IsDead || Time.deltaTime <= 0f) return;
        if (AliveCount >= Mathf.Max(1, _maxAlive) || Time.time < _nextSpawnTime) return;

        // 실패해도 간격을 두고 재시도하며, 한 프레임에 여러 마리를 몰아서 생성하지 않는다.
        _nextSpawnTime = Time.time + Mathf.Max(0.1f, _spawnInterval);
        for (int attempt = 0; attempt < _spawnPoints.Length; attempt++)
        {
            int index = (_nextPoint + attempt) % _spawnPoints.Length;
            if (_spawnPoints[index] == null || !TryGetSpawnPosition(_spawnPoints[index].position, out Vector3 position)) continue;
            if (!Spawn(position)) continue;
            _nextPoint = (index + 1) % _spawnPoints.Length;
            break;
        }
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
            if (!monster.Dead)
            {
                AliveCount--;
            }
            Release(monster);
            _monsters.RemoveAt(i);
        }
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
