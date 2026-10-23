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
    private PlayerChargeSkill_s _charge;
    private PlayerLightningVFX_s _lightning;
    private bool _skillSwing;
    private int _skillDamage;
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
        if (_charge != null) _charge.DamageWindowClosed += DisableHitbox;
    }
    private void Initialize()
    {
        if (_lightning == null) _lightning = GetComponentInParent<PlayerLightningVFX_s>();
        if (_charge == null) _charge = GetComponentInParent<PlayerChargeSkill_s>();
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
        int swing = 0;
        int step = 0;
        float time = 0f;
        Vector2 window = Vector2.zero;
        bool skill = _charge != null && _charge.TryGetDamagePhase(out swing, out _skillDamage, out time, out window);
        bool valid = skill || (_combo != null && _combo.TryGetDamagePhase(out swing, out step, out time, out window));
        if (!isActiveAndEnabled || _hitbox == null || !valid)
        {
            DisableHitbox();
            return;
        }
        if (_swing != swing || _skillSwing != skill)
        {
            _swing = swing;
            _hitTargets.Clear();
        }
        _step = step;
        _skillSwing = skill;
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
        int damage = _skillSwing ? _skillDamage
            : Mathf.RoundToInt(_step == 1 ? _damage.x : _step == 2 ? _damage.y : _damage.z);
        // 피해 콜백이 대상을 제거하거나 공격 상태를 바꾸기 전에 명중 정보를 보관한다.
        Vector3 point = other.ClosestPoint(_hitbox.bounds.center);
        bool finisher = _skillSwing && _charge != null && _charge.AttackStep == 3;
        // 피해 이벤트에서 재진입하더라도 중복 적용하지 않는다.
        _hitTargets.Add(health);
        if (!health.TakeDamage(damage))
        {
            _hitTargets.Remove(health);
            return;
        }
        if (_lightning != null) _lightning.PlayImpact(point, finisher);
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
        if (_charge != null) _charge.DamageWindowClosed -= DisableHitbox;
        if (_combo != null)
        {
            _combo.DamageWindowClosed -= DisableHitbox;
        }
        DisableHitbox();
        _hitTargets.Clear();
        _swing = -1;
    }
}

