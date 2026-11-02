using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSpecialAttack_k : MonoBehaviour
{
    [SerializeField] private PlayerMove _target;
    [SerializeField] private PlayerAttack _playerAttack;
    [Tooltip("검에 붙은 얼음 오라와 궤적 이펙트. 평소에는 비활성 상태로 둡니다.")]
    [SerializeField] private GameObject _swordEffect;
    [SerializeField][Min(0f)] private float _duration = 2f;

    private float _remaining;
    private TrailRenderer[] _swingTrails;

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
        _swingTrails = _swordEffect != null
            ? _swordEffect.GetComponentsInChildren<TrailRenderer>(true)
            : System.Array.Empty<TrailRenderer>();
        SetState(false);
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

    private void LateUpdate()
    {
        if (_swingTrails == null)
        {
            return;
        }

        bool emitting = IsSpecialAttack && _playerAttack != null && _playerAttack.IsSwinging
                        && _swordEffect != null && _swordEffect.activeInHierarchy;

        foreach (TrailRenderer trail in _swingTrails)
        {
            if (trail != null)
            {
                trail.emitting = emitting;
            }
        }
    }

    private void OnAttackStarted()
    {
        if (!IsSpecialAttack || _swordEffect == null) return;

        _swordEffect.SetActive(false);
        ClearTrails();
        _swordEffect.SetActive(true);
    }

    private void SetState(bool active)
    {
        IsSpecialAttack = active;
        if (!active)
        {
            _remaining = 0f;
            StopEffect();
        }
    }

    private void StopEffect()
    {
        ClearTrails();
        if (_swordEffect != null)
        {
            _swordEffect.SetActive(false);
        }
    }

    private void ClearTrails()
    {
        if (_swingTrails == null)
        {
            return;
        }

        foreach (TrailRenderer trail in _swingTrails)
        {
            if (trail != null)
            {
                trail.emitting = false;
                trail.Clear();
            }
        }
    }
}
