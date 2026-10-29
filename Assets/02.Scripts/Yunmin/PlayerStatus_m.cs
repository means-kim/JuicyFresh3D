using UnityEngine;
using System;

public class PlayerStatus_m : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerEffectController_m _effectController;

    [Header("Player Status")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth = 100f;

    [Header("Near Death")]
    [Tooltip("최대 체력 대비 이 비율 이하로 떨어지면 빈사 연출을 켭니다.")]
    [SerializeField, Range(0f, 1f)] private float _nearDeathHealthRatio = 0.3f;

    private bool _isNearDeath;

    public bool IsNearDeath => _isNearDeath;

    private void Awake()
    {
        if (_effectController == null)
        {
            _effectController = GetComponentInChildren<PlayerEffectController_m>();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            TakeDamage(10f); // Example damage value
        }
    }

    /// <summary>
    /// 피격시 호출하는 함수. 필요시 인터페이스로 분리해야함.
    /// </summary>
    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        Debug.Log($"Player took {damage} damage. Current health: {_currentHealth}/{_maxHealth}");

        // 피격 이펙트 호출
        _effectController.PlayHitEffect();

        // 기준 체력을 처음 넘는 순간에만 빈사 연출을 켭니다.
        if (!_isNearDeath && _currentHealth <= _maxHealth * _nearDeathHealthRatio)
        {
            _isNearDeath = true;
            _effectController.PlayNearDeathEffect();
        }
    }
}
