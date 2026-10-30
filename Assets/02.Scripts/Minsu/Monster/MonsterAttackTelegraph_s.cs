using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAttack_s), typeof(MonsterHitFlash_s))]
public class MonsterAttackTelegraph_s : MonoBehaviour
{
    [SerializeField, ColorUsage(false, true)] private Color _color = new Color(1f, 0.35f, 0.1f);
    [SerializeField, Range(0f, 1f)] private float _opacity = 0.6f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.15f;
    private MonsterAttack_s _attack;
    private MonsterHitFlash_s _flash;
    private bool _holding;

    private void Awake()
    {
        _attack = GetComponent<MonsterAttack_s>();
        _flash = GetComponent<MonsterHitFlash_s>();
    }

    private void OnEnable()
    {
        _attack.AttackStarted += OnAttackStarted;
    }

    private void OnAttackStarted()
    {
        _holding = true;
        _flash.Flash(_color, _opacity, _fadeDuration, false);
    }

    private void LateUpdate()
    {
        if (!_holding) return;
        // 공격 시작(0%)부터 판정 시작까지 최대 밝기를 유지하고 이후 감쇠시킨다.
        // 피격으로 공격이 취소되면 유지를 멈춰 피격 플래시가 대신 보이게 한다.
        if (!_attack.IsAttacking
            || (_attack.TryGetAttackTime(out float time) && time >= _attack.DamageWindowStart))
        {
            _holding = false;
            return;
        }
        _flash.Flash(_color, _opacity, _fadeDuration, false);
    }

    private void OnDisable()
    {
        _attack.AttackStarted -= OnAttackStarted;
        _holding = false;
    }
}
