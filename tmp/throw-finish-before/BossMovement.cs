using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class BossMovement : MonoBehaviour
{
    [Header("추적 대상")]
    [SerializeField] private Transform _target;

    [Header("이동 설정")]
    [SerializeField] [Min(0f)] private float _moveSpeed = 3f;
    [SerializeField] [Min(0f)] private float _rotationSpeed = 360f;
    [SerializeField] [Min(0f)] private float _stopDistance = 2f;

    [Header("중력 설정")]
    [SerializeField] private float _gravity = -20f;

    private CharacterController _characterController;
    private BossAttackController _attackController;
    private Animator _animator;
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int ChargeMotionTagHash = Animator.StringToHash("ChargeRootMotion");

    private float _verticalSpeed;

    // 공격 종료 이벤트 이후의 Idle 전환 구간도 포함
    private bool IsChargeMotion =>
        _animator.GetCurrentAnimatorStateInfo(0).tagHash == ChargeMotionTagHash
        || (_animator.IsInTransition(0)
            && _animator.GetNextAnimatorStateInfo(0).tagHash == ChargeMotionTagHash);

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _attackController = GetComponent<BossAttackController>();

        if (_attackController == null)
        {
            Debug.LogError("BossAttackController 연결하세요", this);
            enabled = false;
        }

        _animator = GetComponent<Animator>();

        if (_animator == null)
        {
            Debug.LogError("Animator 연결하세요", this);
            enabled = false;
            return;
        }

        // OnAnimatorMove에서 돌진 이동만 CharacterController로 전달
        // 실행 중 공격마다 이 값을 변경하면 Animator가 재초기화되므로 한 번만 설정
        _animator.applyRootMotion = true;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void Update()
    {
        Vector3 horizontalVelocity = Vector3.zero;

        bool chargeMotion = IsChargeMotion;

        if (chargeMotion)
        {
            // 돌진의 준비·점프·착지는 애니메이션의 XYZ 이동을 그대로 사용합니다.
            // 이 구간에 코드 중력을 함께 적용하면 원본 점프 궤적이 낮아집니다.
            _verticalSpeed = 0f;
            _animator.SetBool(IsMovingHash, false);
            return;
        }

        if (_target != null && !_attackController.IsAttacking && !chargeMotion)
        {
            Vector3 direction = _target.position - transform.position;

            // 추적 거리와 방향은 수평면을 기준으로 계산
            direction.y = 0f;

            float distance = direction.magnitude;

            if (distance > _stopDistance)
            {
                Vector3 moveDirection = direction.normalized;

                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);

                // 이번 프레임에 정지거리를 지나치지 않도록 제한
                float remainingDistance = distance - _stopDistance;

                float moveDistance = Mathf.Min(_moveSpeed * Time.deltaTime, remainingDistance);

                if (Time.deltaTime > 0f)
                {
                    horizontalVelocity = moveDirection * (moveDistance / Time.deltaTime);
                }
            }
        }

        // 공격 중에도 지면 접촉과 중력 처리는 유지
        if (_characterController.isGrounded && _verticalSpeed < 0f)
        {
            _verticalSpeed = -2f;
        }
        else
        {
            _verticalSpeed += _gravity * Time.deltaTime;
        }
        Vector3 velocity = horizontalVelocity + Vector3.up * _verticalSpeed;
        _characterController.Move(velocity * Time.deltaTime);

        // Move 호출 결과로 실제 이동한 속도를 확인
        Vector3 actualVelocity = _characterController.velocity;

        // 낙하나 지면 접촉은 걷기로 판단X
        actualVelocity.y = 0f;

        bool isMoving = actualVelocity.sqrMagnitude > 0.01f
                        && !_attackController.IsAttacking && !chargeMotion;

        _animator.SetBool(IsMovingHash, isMoving);
    }

    private void OnAnimatorMove()
    {
        if (!enabled || _characterController == null || !_characterController.enabled)
        {
            return;
        }

        if (!IsChargeMotion)
        {
            return;
        }

        // 점프의 높이도 Root Motion에 들어 있으므로 Y를 버리지 않습니다.
        // deltaPosition은 이동량이므로 deltaTime을 다시 곱하지 않습니다.
        _characterController.Move(_animator.deltaPosition);
    }
}
