using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]

public class SwordHitDetector_s : MonoBehaviour
{
    [SerializeField] private PlayerComboAnimation_s _combo;
    [SerializeField] private Collider _hitbox;
    [SerializeField] private Transform _owner;
    [SerializeField] private LayerMask _targetLayers = ~0;
    [SerializeField] private Vector3 _damage = new Vector3(20f, 25f, 35f);
    [SerializeField, Min(0)] private int _fourthDamage = 45;
    [Header("명중 카메라 충격")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;
    [SerializeField, Min(0f)] private float _hitImpulseForce = 0.1f;
    private int _lastImpulseSwing = -1;
    private bool _lastImpulseSkillSwing;
    private readonly HashSet<Health_s> _hitTargets = new HashSet<Health_s>();
    private int _swing = -1;
    private int _step;
    private PlayerChargeSkill_s _charge;
    private KatanaChargeSkill_s _katanaCharge;
    private PlayerLightningVFX_s _lightning;
    private HitStop_s _hitStop;
    private bool _skillSwing;
    private int _skillDamage;
    public event System.Action<int, bool, int> HitConfirmed;
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
        if (_katanaCharge != null) _katanaCharge.DamageWindowClosed += DisableHitbox;
    }
    private void Initialize()
    {
        if (_impulseSource == null) _impulseSource = GetComponentInParent<CinemachineImpulseSource>();
        if (_hitStop == null) _hitStop = GetComponentInParent<HitStop_s>();
        if (_lightning == null) _lightning = GetComponentInParent<PlayerLightningVFX_s>();
        if (_charge == null) _charge = GetComponentInParent<PlayerChargeSkill_s>();
        if (_katanaCharge == null) _katanaCharge = GetComponentInParent<KatanaChargeSkill_s>();
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
        bool skill = _katanaCharge != null
            ? _katanaCharge.TryGetDamagePhase(out swing, out _skillDamage, out time, out window)
            : _charge != null && _charge.TryGetDamagePhase(out swing, out _skillDamage, out time, out window);
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

    public void SweepDash(Vector3 start, Vector3 end, float radius)
    {
        if (!isActiveAndEnabled || _katanaCharge == null) return;
        foreach (var other in Physics.OverlapCapsule(start, end, radius, _targetLayers,
                     QueryTriggerInteraction.Ignore))
        {
            // 폭이 있는 판정이 벽 모서리 너머의 적에게 닿는 것도 차단한다.
            Vector3 segment = end - start;
            float t = segment.sqrMagnitude > 0.000001f
                ? Mathf.Clamp01(Vector3.Dot(other.bounds.center - start, segment) / segment.sqrMagnitude) : 0f;
            Vector3 origin = start + segment * t;
            Vector3 offset = other.bounds.center - origin;
            bool blocked = false;
            foreach (var hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if ((_owner && hit.transform.IsChildOf(_owner)) || hit.collider.GetComponentInParent<Health_s>() != null) continue;
                blocked = true;
                break;
            }
            if (!blocked) ApplyHit(other, true);
        }
    }

    private void TryHit(Collider other)
    {
        ApplyHit(other, false);
    }

    private void ApplyHit(Collider other, bool swept)
    {
        RefreshHitbox(); // 취소/전환 직후 도착한 물리 콜백도 무시한다.
        bool sweptSkill = swept && _katanaCharge != null
            && _katanaCharge.TryGetDamagePhase(out _, out _, out _, out _);
        if ((!IsHitboxActive && !sweptSkill) || other == null || other.isTrigger || (_owner && other.transform.IsChildOf(_owner))
            || (_targetLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }
        var health = other.GetComponentInParent<Health_s>();
        if (!health || _hitTargets.Contains(health)) return;
        if (_katanaCharge != null && _skillSwing && _owner != null)
        {
            Vector3 origin = _owner.position + Vector3.up;
            Vector3 offset = other.bounds.center - origin;
            foreach (var hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(_owner) && hit.collider.GetComponentInParent<Health_s>() == null) return;
            }
        }
        int damage = _skillSwing ? _skillDamage
            : _step == 4 ? _fourthDamage
            : Mathf.RoundToInt(_step == 1 ? _damage.x : _step == 2 ? _damage.y : _damage.z);
        // 피해 콜백이 대상을 제거하거나 공격 상태를 바꾸기 전에 명중 정보를 보관한다.
        Vector3 point = other.ClosestPoint(_hitbox.bounds.center);
        bool finisher = _skillSwing && (_katanaCharge != null
            ? _katanaCharge.IsFinisher : _charge != null && _charge.AttackStep == 3);
        int hitStep = _step;
        int hitSwing = _swing;
        bool hitSkillSwing = _skillSwing;
        // 피해 이벤트에서 재진입하더라도 중복 적용하지 않는다.
        _hitTargets.Add(health);
        if (!health.TakeDamage(damage))
        {
            _hitTargets.Remove(health);
            return;
        }
        if (_lightning != null) _lightning.PlayImpact(point, finisher);
        if (_hitStop != null) _hitStop.ScreenStop(hitSwing, hitSkillSwing);
        PlayHitImpulse(hitSwing, hitSkillSwing);
        HitConfirmed?.Invoke(hitSwing, hitSkillSwing, hitStep);
    }

    private void PlayHitImpulse(int swing, bool skillSwing)
    {
        if (!isActiveAndEnabled || _impulseSource == null || !_impulseSource.isActiveAndEnabled
            || _hitImpulseForce <= 0f || (_lastImpulseSwing == swing && _lastImpulseSkillSwing == skillSwing))
        {
            return;
        }
        // 정지가 끝난 뒤 다른 적을 맞히더라도 같은 공격에서는 한 번만 발생한다.
        _lastImpulseSwing = swing;
        _lastImpulseSkillSwing = skillSwing;
        CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
        _impulseSource.GenerateImpulseWithForce(_hitImpulseForce);
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
        if (_katanaCharge != null) _katanaCharge.DamageWindowClosed -= DisableHitbox;
        if (_combo != null)
        {
            _combo.DamageWindowClosed -= DisableHitbox;
        }
        DisableHitbox();
        _hitTargets.Clear();
        _swing = -1;
    }
}

