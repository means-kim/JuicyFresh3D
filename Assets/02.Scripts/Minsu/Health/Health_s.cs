using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Health_s : MonoBehaviour
{
    [SerializeField, Min(1)] private int _maxHealth = 100;
    private int _currentHealth = 100;
    public int MaxHealth => _maxHealth;
    public int CurrentHealth => _currentHealth;
    public bool IsDead => _currentHealth <= 0;

    public event Action<int, int> HealthChanged;
    public event Action<int> Damaged;
    public event Action<int> Healed;
    public event Action Died;

    private void Awake()
    {
        _maxHealth = Mathf.Max(1, _maxHealth);
        _currentHealth = _maxHealth;
    }

    public bool TakeDamage(int damage)
    {
        if (!isActiveAndEnabled || IsDead || damage <= 0)
        {
            return false;
        }

        int applied = Mathf.Min(damage, _currentHealth);

        _currentHealth -= applied;

        bool died = IsDead;

        Damaged?.Invoke(applied);

        HealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (died)
        {
            Died?.Invoke();
        }

        return true;
    }

    public bool Heal(int healAmount)
    {
        if (!isActiveAndEnabled || IsDead || healAmount <= 0)
        {
            return false;
        }

        int applied = Mathf.Min(healAmount, _maxHealth - _currentHealth);

        if (applied <= 0)
        {
            return false;
        }

        _currentHealth += applied;

        Healed?.Invoke(applied);

        HealthChanged?.Invoke(_currentHealth, _maxHealth);

        return true;
    }
}
