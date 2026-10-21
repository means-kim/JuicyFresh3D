using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]

public class SwordHitDetector_s : MonoBehaviour
{
    [SerializeField] private PlayerComboAnimation_s _combo;
    [SerializeField] private Collider _hitbox;
    [SerializeField] private Transform _owner;
    [SerializeField] private LayerMask _targetLayers = ~0;
    [SerializeField] private Vector3 _damage = new Vector3(20f, 25f, 35f);
    private readonly HashSet<Health_s> _hitTargets = new HashSet<Health_s>();
    private int _swing = -1;
    private int _step;
    public int HitsThisSwing => _hitTargets.Count;
    public bool IsHitboxActive => _hitbox != null && _hitbox.enabled;

    private void Awake()
    {
        Initialize();
        DisableHitbox();
    }
    private void OnEnable()
    {
        Initialize();
        DisableHitbox();
        if (_combo != null) _combo.DamageWindowClosed += DisableHitbox;
    }
    private void Initialize()
    {
        if (_hitbox == null)
        {
            _hitbox = GetComponent<BoxCollider>();
        }
        if (_combo == null)
        {
            _combo = GetComponentInParent<PlayerComboAnimation_s>();
        }
        if (_owner == null && _combo != null)
        {
            _owner = _combo.transform;
        }
    }

    private void LateUpdate()
    {
        RefreshHitbox();
    }

    private void FixedUpdate()
    {
        RefreshHitbox();
    }

    public void RefreshHitbox()
    {
        if (!isActiveAndEnabled || _combo == null || _hitbox == null
            || !_combo.TryGetDamagePhase(out int swing, out int step, out float time, out Vector2 window))
        {
            DisableHitbox();
            return;
        }
        if (_swing != swing)
        {
            _swing = swing;
            _hitTargets.Clear();
        }
        _step = step;
        _hitbox.enabled = time >= window.x && time <= window.y;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }
    // 이전 타격부터 겹쳐 있는 대상도 다음 타에서 판정한다.
    private void OnTriggerStay(Collider other)
    {
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        RefreshHitbox(); // 취소/전환 직후 도착한 물리 콜백도 무시한다.
        if (!IsHitboxActive || other.isTrigger || (_owner && other.transform.IsChildOf(_owner))
            || (_targetLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }
        var health = other.GetComponentInParent<Health_s>();
        if (!health || _hitTargets.Contains(health)) return;
        int damage = Mathf.RoundToInt(_step == 1 ? _damage.x : _step == 2 ? _damage.y : _damage.z);
        // 피해 이벤트에서 재진입하더라도 중복 적용하지 않는다.
        _hitTargets.Add(health);
        if (!health.TakeDamage(damage))
        {
            _hitTargets.Remove(health);
        }
    }

    public void DisableHitbox()
    {
        if (_hitbox != null)
        {
            _hitbox.enabled = false;
        }
    }
    private void OnDisable()
    {
        if (_combo != null)
        {
            _combo.DamageWindowClosed -= DisableHitbox;
        }
        DisableHitbox();
        _hitTargets.Clear();
        _swing = -1;
    }
}

