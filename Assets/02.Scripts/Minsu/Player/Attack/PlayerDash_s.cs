using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerDash_s : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _distance = 6f;
    [SerializeField, Min(0.1f)] private float _hitRadius = 0.7f;
    [SerializeField, Min(0.02f)] private float _stepLength = 0.1f;
    [SerializeField] private LayerMask _environmentLayers = ~0;
    private readonly List<Collider> _ignoredColliders = new List<Collider>();
    private CharacterController _controller;
    private SwordHitDetector_s _sword;
    private Vector3 _direction;
    private float _progress;
    private bool _active;
    private bool _blocked;
    public bool IsActive => _active;
    public bool WasBlocked => _blocked;
    public float TravelledDistance { get; private set; }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _sword = GetComponentInChildren<SwordHitDetector_s>();
    }

    public void Begin(Vector3 direction)
    {
        Stop();
        if (_controller == null) Awake();
        _direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        _progress = 0f;
        TravelledDistance = 0f;
        _blocked = false;
        _active = isActiveAndEnabled && _controller.enabled && _direction.sqrMagnitude > 0.5f;
    }

    public bool Move(float progress, float verticalDisplacement)
    {
        if (!_active || !_controller.enabled) return false;
        progress = Mathf.Clamp01(Mathf.Max(_progress, progress));
        float remaining = _distance * (Mathf.SmoothStep(0f, 1f, progress)
            - Mathf.SmoothStep(0f, 1f, _progress));
        _progress = progress;
        while (remaining > 0.0001f && _active && !_blocked)
        {
            float step = Mathf.Min(remaining, Mathf.Max(0.02f, _stepLength));
            Vector3 start = transform.position;
            Vector3 center = transform.TransformPoint(_controller.center);
            float radius = _controller.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            float half = Mathf.Max(radius, _controller.height * transform.lossyScale.y * 0.5f);
            Vector3 bottom = center - Vector3.up * (half - radius);
            Vector3 top = center + Vector3.up * (half - radius);
            // 바닥과 맞닿은 캡슐은 초기 겹침을 전방 충돌로 반환하므로 발밑 여유를 둔다.
            float castRadius = Mathf.Max(0.01f, radius - _controller.skinWidth);
            float allowed = step;
            foreach (var hit in Physics.CapsuleCastAll(bottom, top, castRadius, _direction,
                         step + 2f * _controller.skinWidth, _environmentLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsOwn(hit.collider)) continue;
                if (hit.collider.GetComponentInParent<Health_s>() != null)
                {
                    IgnoreBody(hit.collider);
                    continue;
                }
                // 접지면은 CharacterController의 경사/계단 처리가 담당한다.
                if (Vector3.Dot(hit.normal, Vector3.up) >= Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - 2f * _controller.skinWidth));
            }
            foreach (var other in Physics.OverlapCapsule(bottom, top, radius,
                         _environmentLayers, QueryTriggerInteraction.Ignore))
            {
                if (!IsOwn(other) && other.GetComponentInParent<Health_s>() != null) IgnoreBody(other);
            }
            Vector3 next = start + _direction * allowed;
            if (allowed <= 0.0001f || !HasGround(next, center.y - half))
            {
                _blocked = true;
                break;
            }
            _controller.Move(_direction * allowed);
            Vector3 end = transform.position;
            TravelledDistance += Vector3.ProjectOnPlane(end - start, Vector3.up).magnitude;
            // 실제 이동한 구간만 판정하므로 벽 뒤로 피해가 새지 않는다.
            _sword?.SweepDash(start + Vector3.up, end + Vector3.up, _hitRadius);
            remaining -= step;
            if (allowed < step - 0.001f || Vector3.Dot(end - start, _direction) < allowed * 0.5f)
                _blocked = true;
        }
        if (!_active) return true; // 피해 이벤트에서 스킬이 취소된 경우 추가 이동하지 않는다.
        _controller.Move(Vector3.up * verticalDisplacement);
        if (_blocked || progress >= 1f) Stop();
        return true;
    }

    private bool HasGround(Vector3 position, float footHeight)
    {
        float reach = _controller.stepOffset + 0.15f;
        Vector3 origin = new Vector3(position.x, footHeight + reach, position.z);
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, reach * 2f,
                     _environmentLayers, QueryTriggerInteraction.Ignore))
        {
            if (IsOwn(hit.collider) || hit.collider.GetComponentInParent<Health_s>() != null) continue;
            if (Vector3.Dot(hit.normal, Vector3.up) >= Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad)) return true;
        }
        return false;
    }

    private bool IsOwn(Collider other)
    {
        return other == _controller || other.transform.IsChildOf(transform);
    }

    private void IgnoreBody(Collider other)
    {
        if (Physics.GetIgnoreCollision(_controller, other)) return;
        Physics.IgnoreCollision(_controller, other, true);
        _ignoredColliders.Add(other);
    }

    public void Stop()
    {
        _active = false;
        foreach (var other in _ignoredColliders)
        {
            if (_controller != null && other != null) Physics.IgnoreCollision(_controller, other, false);
        }
        _ignoredColliders.Clear();
    }

    private void OnDisable()
    {
        Stop();
    }
}
