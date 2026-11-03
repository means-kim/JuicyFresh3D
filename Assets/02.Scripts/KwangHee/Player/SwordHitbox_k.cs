using UnityEngine;

// 칼의 트리거 콜라이더(+ kinematic Rigidbody)에 붙입니다. 휘두르는 구간에서 스윙당 1회만 적중합니다.
[RequireComponent(typeof(Collider))]
public class SwordHitbox_k : MonoBehaviour
{
    [SerializeField] private PlayerAttack _playerAttack;

    private bool _hasHit;
    private bool _wasSwinging;

    private void Update()
    {
        bool swinging = _playerAttack != null && _playerAttack.IsSwinging;
        if (swinging && !_wasSwinging) _hasHit = false;
        _wasSwinging = swinging;
    }

    private void OnTriggerStay(Collider other)
    {
        if (_hasHit || _playerAttack == null || !_playerAttack.IsSwinging) return;

        BossStatus_k boss = other.GetComponentInParent<BossStatus_k>();
        if (boss == null) return;

        _hasHit = true;
        boss.TakeDamage(_playerAttack.CurrentDamage);
    }
}
