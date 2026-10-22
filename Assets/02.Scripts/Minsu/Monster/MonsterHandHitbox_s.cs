using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class MonsterHandHitbox_s : MonoBehaviour
{
    [SerializeField] private MonsterAttack_s _attack;
    private Collider _hitbox;

    private void Awake()
    {
        _hitbox = GetComponent<Collider>();
        if (_attack == null) _attack = GetComponentInParent<MonsterAttack_s>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_attack != null) _attack.TryHit(_hitbox, other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (_attack != null) _attack.TryHit(_hitbox, other);
    }
}
