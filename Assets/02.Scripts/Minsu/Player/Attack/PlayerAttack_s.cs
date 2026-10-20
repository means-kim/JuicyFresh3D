using UnityEngine;

// 공격 시작 프레임부터 이동 입력을 차단하도록 이동 컴포넌트보다 먼저 실행한다.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerAttack_s : MonoBehaviour
{
    [SerializeField] private PlayerAnimation_s _animation;
    [SerializeField] private CharacterController _characterController;

    public bool IsAttacking { get; private set; }
    public bool IsFinished => !IsAttacking || !isActiveAndEnabled
        || _animation == null || _animation.AttackFinished;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_animation == null) _animation = GetComponent<PlayerAnimation_s>();
        if (_characterController == null) _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        SimulateAttack(Input.GetMouseButtonDown(0));
    }

    // 공격 입력, 시작 조건, 중복 차단, 완료 처리를 이 컴포넌트에서 관리한다.
    public void SimulateAttack(bool attackPressed)
    {
        if (!isActiveAndEnabled) return;
        Initialize();
        if (IsAttacking)
        {
            if (IsFinished) EndAttack();
            return;
        }
        if (attackPressed) TryBeginAttack();
    }

    public bool TryBeginAttack()
    {
        Initialize();
        if (!isActiveAndEnabled || IsAttacking || _characterController == null
            || !_characterController.enabled || !_characterController.isGrounded
            || _characterController.velocity.y > 0f
            || _animation == null || !_animation.CanAttack)
        {
            return false;
        }
        IsAttacking = true;
        _animation.BeginAttack();
        return true;
    }

    public void EndAttack()
    {
        if (!IsAttacking) return;
        IsAttacking = false;
        if (_animation != null) _animation.EndAttack();
    }

    private void OnDisable()
    {
        EndAttack();
    }
}
