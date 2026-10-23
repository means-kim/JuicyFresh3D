using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController), typeof(PlayerComboAnimation_s))]
public class PlayerAttack_s : MonoBehaviour
{
    [SerializeField] private PlayerComboAnimation_s _comboAnimation;
    [SerializeField] private CharacterController _characterController;
    private PlayerMovement_s _movement;
    private Health_s _health;

    private bool IsActionBlocked => (_movement != null && (_movement.IsActionBlocked || _movement.IsUsingChargeSkill))
                                    || (_health != null && _health.IsDead);

    public bool IsAttacking => _comboAnimation != null && _comboAnimation.IsPlaying;
    public bool IsFinished => !IsAttacking;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_comboAnimation == null) _comboAnimation = GetComponent<PlayerComboAnimation_s>();
        if (_characterController == null) _characterController = GetComponent<CharacterController>();
        if (_movement == null) _movement = GetComponent<PlayerMovement_s>();
        if (_health == null) _health = GetComponent<Health_s>();
    }

    private void Update()
    {
        SimulateAttack(Input.GetMouseButtonDown(0));
    }

    public void SimulateAttack(bool attackPressed)
    {
        if (!isActiveAndEnabled) return;
        Initialize();
        if (IsActionBlocked) return;
        if (IsAttacking)
        {
            _comboAnimation.TickCombo(attackPressed);
            return;
        }

        if (attackPressed)
        {
            TryBeginAttack();
        }
    }

    public bool TryBeginAttack()
    {
        Initialize();
        if (!isActiveAndEnabled || IsActionBlocked || IsAttacking || _characterController == null
            || !_characterController.enabled || !_characterController.isGrounded
            || _characterController.velocity.y > 0f || _comboAnimation == null)
        {
            return false;
        }

        return _comboAnimation.TryBeginCombo();
    }

    public void EndAttack()
    {
        if (_comboAnimation != null) _comboAnimation.EndCombo();
    }

    private void OnDisable()
    {
        EndAttack();
    }
}
