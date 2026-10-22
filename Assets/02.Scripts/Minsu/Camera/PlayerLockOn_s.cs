using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerLockOn_s : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _searchDistance = 12f;
    [SerializeField, Min(0f)] private float _releaseDistance = 15f;
    private Health_s _health;
    private Health_s _targetHealth;
    public Transform Target { get; private set; }
    public bool IsLockedOn => Target != null;

    private void Awake()
    {
        _health = GetComponent<Health_s>();
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.Died += Release;
        }
    }

    private void Update()
    {
        ValidateTarget();

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleLockOn();
        }
    }

    public void ToggleLockOn()
    {
        if (IsLockedOn)
        {
            Release();

            return;
        }

        if (!isActiveAndEnabled || (_health != null && _health.IsDead)) return;

        Health_s nearest = null;

        float best = _searchDistance * _searchDistance;

        foreach (var monster in FindObjectsByType<MonsterState_s>(FindObjectsSortMode.None))
        {
            var health = monster.GetComponent<Health_s>();
            if (!monster.isActiveAndEnabled || health == null || !health.isActiveAndEnabled || health.IsDead)
            {
                continue;
            }

            float distance = (monster.transform.position - transform.position).sqrMagnitude;
            if (distance > best)
            {
                continue;
            }

            best = distance;
            nearest = health;
        }

        if (nearest == null) return;

        _targetHealth = nearest;
        Target = nearest.transform;

        _targetHealth.Died += Release;
    }

    public void ValidateTarget()
    {
        if (!Target || !Target.gameObject.activeInHierarchy || !_targetHealth || !_targetHealth.isActiveAndEnabled
            || _targetHealth.IsDead || (_health != null && _health.IsDead)
            || (Target.position - transform.position).sqrMagnitude > _releaseDistance * _releaseDistance)
        {
            Release();
        }
    }

    public void Release()
    {
        if (_targetHealth != null)
        {
            _targetHealth.Died -= Release;
        }

        _targetHealth = null;
        Target = null;
    }

    private void OnDisable()
    {
        Release();
        if (_health != null)
        {
            _health.Died -= Release;
        }
    }
}