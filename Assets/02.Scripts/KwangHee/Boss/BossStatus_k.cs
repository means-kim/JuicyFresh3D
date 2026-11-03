using System;
using UnityEngine;

public class BossStatus_k : MonoBehaviour
{
    [SerializeField][Min(1f)] private float _maxHp = 300f;

    private float _currentHp;

    public float MaxHp => _maxHp;
    public float CurrentHp => _currentHp;
    public bool IsDead => CurrentHp <= 0f;

    // 인자: 현재 체력, 최대 체력
    public event Action<float, float> Damaged;
    public event Action Died;

    private void Awake()
    {
        _currentHp = _maxHp;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        _currentHp = Mathf.Max(0f, _currentHp - amount);
        Damaged?.Invoke(_currentHp, _maxHp);

        if (IsDead) Died?.Invoke();
    }

    // 테스트용 회복. 체력이 바뀌는 건 같아서 Damaged 이벤트를 그대로 사용한다.
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        _currentHp = Mathf.Min(_maxHp, _currentHp + amount);
        Damaged?.Invoke(_currentHp, _maxHp);
    }
}
