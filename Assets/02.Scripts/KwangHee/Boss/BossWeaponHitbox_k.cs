using UnityEngine;

// 보스 무기의 트리거 콜라이더(+ kinematic Rigidbody)에 붙입니다. 피해 Window가 열릴 때마다 1회만 적중합니다.
[RequireComponent(typeof(Collider))]
public class BossWeaponHitbox_k : MonoBehaviour
{
    [SerializeField] private BossAttackController _attackController;

    private bool _hasHit;
    private bool _wasOpen;
    private bool _loggedContact;

    private void Update()
    {
        bool open = _attackController != null && _attackController.IsDamageWindowOpen;
        if (open && !_wasOpen) _hasHit = false;
        _wasOpen = open;
    }

    private void OnTriggerStay(Collider other)
    {
        if (_attackController == null) return;

        // 플레이어 몸통(CharacterController)만 피격 대상으로 인정합니다. 플레이어 칼 등 트리거는 제외됩니다.
        if (other is not CharacterController)
        {
            return;
        }

        if (!_loggedContact && _attackController.IsAttacking)
        {
            _loggedContact = true;
        }

        if (_hasHit || !_attackController.IsDamageWindowOpen || _attackController.HasPlayerDodged) return;

        PlayerStatus_m player = other.GetComponentInParent<PlayerStatus_m>();
        if (player == null) return;

        _hasHit = true;
        player.TakeDamage(_attackController.CurrentDamage);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other is CharacterController) _loggedContact = false;
    }
}
