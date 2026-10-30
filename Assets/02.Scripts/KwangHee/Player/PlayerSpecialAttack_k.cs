using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSpecialAttack_k : MonoBehaviour
{
    [SerializeField] private PlayerMove _target;
    [SerializeField] private PlayerAttack _playerAttack;
    [Tooltip("칼에 붙어있는 Shiny Slash 이펙트 오브젝트. 평소엔 비활성 상태로 둡니다.")]
    [SerializeField] private GameObject _swordEffect;
    [SerializeField][Min(0f)] private float _duration = 2f;

    private float _remaining;

    public bool IsSpecialAttack { get; private set; }

    private void Awake()
    {
        if (_target == null)
        {
            _target = GetComponent<PlayerMove>();
        }
        if (_playerAttack == null)
        {
            _playerAttack = GetComponent<PlayerAttack>();
        }
        SetState(false);
        StopEffect();
    }

    private void OnEnable()
    {
        if (_target != null)
        {
            _target.DodgeSucceeded -= OnDodgeSucceeded;
            _target.DodgeSucceeded += OnDodgeSucceeded;
        }
        if (_playerAttack != null)
        {
            _playerAttack.AttackStarted -= OnAttackStarted;
            _playerAttack.AttackStarted += OnAttackStarted;
        }
    }

    private void OnDisable()
    {
        if (_target != null)
        {
            _target.DodgeSucceeded -= OnDodgeSucceeded;
        }
        if (_playerAttack != null)
        {
            _playerAttack.AttackStarted -= OnAttackStarted;
        }
        SetState(false);
        StopEffect();
    }

    private void Update()
    {
        if (!IsSpecialAttack) return;

        _remaining -= Time.deltaTime;
        if (_remaining <= 0f)
        {
            SetState(false);
        }
    }

    private void OnDodgeSucceeded()
    {
        // 이미 강화 중이면 남은 시간에 더하고, 아니면 새로 시작합니다.
        _remaining = IsSpecialAttack ? _remaining + _duration : _duration;
        IsSpecialAttack = true;
    }

    private void OnAttackStarted()
    {
        if (!IsSpecialAttack || _swordEffect == null) return;

        // 껐다 켜서 파티클을 처음부터 다시 재생합니다(연속 콤보에서도 매 공격마다 출력).
        _swordEffect.SetActive(false);
        _swordEffect.SetActive(true);
    }

    private void SetState(bool active)
    {
        IsSpecialAttack = active;
        if (!active)
        {
            _remaining = 0f;
        }
    }

    private void StopEffect()
    {
        if (_swordEffect != null)
        {
            _swordEffect.SetActive(false);
        }
    }
}
