using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health_s))]
public class MonsterBloodVFX_s : MonoBehaviour
{
    [SerializeField] private GameObject _effectPrefab;
    [SerializeField, Min(0.01f)] private float _emissionDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float _size = 1f;
    [SerializeField] private float _bodyCenterHeight = 0.9f;
    private Health_s _health;

    private void Awake()
    {
        _health = GetComponent<Health_s>();
    }

    private void OnEnable()
    {
        _health.DamagePresented += OnDamagePresented;
    }

    private void OnDamagePresented(int damage, Vector3 point, DamagePopupKind kind)
    {
        if (_effectPrefab == null || damage <= 0) return;
        // 공격자 정보 대신 몸 중심에서 명중점으로 향하는 방향을 사용한다.
        Vector3 direction = point - (transform.position + Vector3.up * _bodyCenterHeight);
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = -transform.forward;
        direction = (direction.normalized + Vector3.up * 0.25f).normalized;
        BloodSprayPool_s.Show(_effectPrefab, point, Quaternion.LookRotation(direction),
            _size, _emissionDuration, gameObject.scene);
    }

    private void OnDisable()
    {
        _health.DamagePresented -= OnDamagePresented;
    }
}
