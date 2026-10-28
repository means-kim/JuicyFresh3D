using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health_s))]
public class MonsterDamagePopup_s : MonoBehaviour
{
    [SerializeField] private Color _color = Color.white;
    [SerializeField, Min(12)] private int _fontSize = 32;
    [SerializeField, Min(0.1f)] private float _duration = 0.7f;
    [SerializeField, Min(0f)] private float _rise = 65f;
    [SerializeField, Min(0f)] private float _horizontalSpacing = 24f;
    [SerializeField] private float _worldHeightOffset = 0.25f;
    [SerializeField, Min(0.1f)] private float _intermediateScale = 0.85f;
    [SerializeField, Min(0.1f)] private float _finalScale = 1.15f;
    [SerializeField, Min(0.1f)] private float _chargedScale = 1.3f;
    private Health_s _health;
    private int _sequence;

    private void Awake()
    {
        _health = GetComponent<Health_s>();
    }

    private void OnEnable()
    {
        _health.DamagePresented += ShowDamage;
    }

    private void ShowDamage(int amount, Vector3 point, DamagePopupKind kind)
    {
        float scale = kind == DamagePopupKind.Intermediate ? _intermediateScale
            : kind == DamagePopupKind.Final ? _finalScale
            : kind == DamagePopupKind.Charged ? _chargedScale : 1f;
        int lane = _sequence;
        _sequence = (_sequence + 1) % 4;
        float horizontal = (lane % 2 == 0 ? -1f : 1f) * (1f + lane / 2) * _horizontalSpacing;
        DamagePopupCanvas_s.Show(amount, point + Vector3.up * _worldHeightOffset, _color,
            _fontSize, _duration, _rise, horizontal, scale);
    }

    private void OnDisable()
    {
        _health.DamagePresented -= ShowDamage;
    }
}
