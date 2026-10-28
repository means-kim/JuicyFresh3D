using UnityEngine;

public enum PlayerMoveState
{
    Locomotion,
    Rolling,
    Dashing
}

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class PlayerMove : MonoBehaviour
{
    [Header("기본 이동")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField][Min(0.01f)] private float _rotationSmoothTime = 0.1f;
    [SerializeField] private float _gravity = -20f;
    [Tooltip("시점 회전 기준점. 미지정 시 Main Camera의 방향을 사용합니다.")]
    [SerializeField] private Transform _cameraTransform;

    [Header("구르기")]
    [SerializeField][Min(0f)] private float _rollDistance = 3f;
    [SerializeField][Min(0.05f)] private float _rollDuration = 0.6f;
    [SerializeField] private AnimationClip _rollClip;

    [Header("대쉬")]
    [SerializeField][Min(0f)] private float _dashDistance = 4f;
    [SerializeField][Min(0.05f)] private float _dashDuration = 0.3f;
    [SerializeField] private AnimationClip _dashClip;

    [Header("회피 성공 연출")]
    [Tooltip("미지정 시 같은 오브젝트의 DodgeTimeSlowEffect_m을 사용합니다.")]
    [SerializeField] private DodgeTimeSlowEffect_m _dodgeTimeSlowEffect;

    private CharacterController _characterController;
    private Animator _animator;
    private PlayerAttack _playerAttack;
    private PlayerCameraController_k _cameraController;
    private PlayerMoveState _currentState = PlayerMoveState.Locomotion;
    private float _verticalSpeed;
    private Vector3 _moveDirection;
    private float _rotationVelocity;

    private Vector3 _actionDirection;
    private float _actionDistance;
    private float _actionDuration;
    private float _actionElapsed;
    private int _actionStateHash;
    private bool _actionAnimationStarted;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int RollTriggerHash = Animator.StringToHash("Roll");
    private static readonly int DashTriggerHash = Animator.StringToHash("Dash");
    private static readonly int ActionSpeedHash = Animator.StringToHash("ActionSpeed");
    private static readonly int IdleStateHash = Animator.StringToHash("Idle_Battle");
    private static readonly int RunStateHash = Animator.StringToHash("RunForwardBattle");
    private static readonly int RollStateHash = Animator.StringToHash("RollForward");
    private static readonly int DashStateHash = Animator.StringToHash("DashForward");

    public PlayerMoveState CurrentState => _currentState;
    public bool IsRolling => _currentState == PlayerMoveState.Rolling;
    public bool IsDashing => _currentState == PlayerMoveState.Dashing;
    private bool CanReadGameplayInput => _cameraController == null || _cameraController.CanReadGameplayInput;
    public event System.Action<PlayerMove> RollStarted;
    public event System.Action DodgeSucceeded;

    // 회피 판정은 보스가 하지만 성공 신호는 플레이어가 발행합니다.
    public void NotifyDodgeSucceeded()
    {
        if (_dodgeTimeSlowEffect != null)
        {
            _dodgeTimeSlowEffect.PlayEffect();
        }
        DodgeSucceeded?.Invoke();
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _playerAttack = GetComponent<PlayerAttack>();

        if (_dodgeTimeSlowEffect == null)
        {
            _dodgeTimeSlowEffect = GetComponentInChildren<DodgeTimeSlowEffect_m>();
        }

        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        if (_cameraTransform == null)
        {
            Debug.LogError("PlayerMove에 카메라 기준점 또는 Main Camera의 Transform을 연결하세요.", this);
            enabled = false;
            return;
        }

        _cameraController = _cameraTransform.GetComponent<PlayerCameraController_k>();

        if (_animator.runtimeAnimatorController == null || _rollClip == null || _dashClip == null)
        {
            Debug.LogError("Player Animator Controller와 RollForward, DashForward 클립을 연결하세요.", this);
            enabled = false;
            return;
        }

        if (!HasAnimatorParameter(IsMovingHash, AnimatorControllerParameterType.Bool)
            || !HasAnimatorParameter(RollTriggerHash, AnimatorControllerParameterType.Trigger)
            || !HasAnimatorParameter(DashTriggerHash, AnimatorControllerParameterType.Trigger)
            || !HasAnimatorParameter(ActionSpeedHash, AnimatorControllerParameterType.Float))
        {
            Debug.LogError("Player Animator에 IsMoving(Bool), Roll·Dash(Trigger), ActionSpeed(Float)가 필요합니다.", this);
            enabled = false;
            return;
        }

        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _currentState = PlayerMoveState.Locomotion;
        _animator.SetBool(IsMovingHash, false);
    }

    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }
        return false;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f) return;

        Vector3 horizontalMovement = Vector3.zero;

        // 종료 프레임에도 예약 공격보다 구르기 취소를 먼저 처리합니다.
        if (CanReadGameplayInput && _currentState == PlayerMoveState.Locomotion && _playerAttack != null
                                                        && _playerAttack.IsAttacking && Input.GetKeyDown(KeyCode.Space))
        {
            ReadMoveDirection();
            TryStartAction(PlayerMoveState.Rolling);
        }

        if (_playerAttack != null)
        {
            _playerAttack.Tick(deltaTime);
        }

        if (_currentState == PlayerMoveState.Locomotion)
        {
            horizontalMovement = TickLocomotion(deltaTime);
        }

        if (_currentState != PlayerMoveState.Locomotion)
        {
            horizontalMovement = TickAction(deltaTime);
        }

        if (_characterController.isGrounded && _verticalSpeed < 0f)
        {
            _verticalSpeed = -2f;
        }
        else
        {
            _verticalSpeed += _gravity * deltaTime;
        }
        Vector3 movement = horizontalMovement + Vector3.up * _verticalSpeed * deltaTime;
        _characterController.Move(movement);

        Vector3 actualVelocity = _characterController.velocity;
        actualVelocity.y = 0f;

        bool isMoving = _currentState == PlayerMoveState.Locomotion
                        && (_playerAttack == null || !_playerAttack.IsAttacking)
                        && actualVelocity.sqrMagnitude > 0.01f;
        _animator.SetBool(IsMovingHash, isMoving);
    }

    private Vector3 TickLocomotion(float deltaTime)
    {
        ReadMoveDirection();

        if (!CanReadGameplayInput)
        {
            _rotationVelocity = 0f;
            return Vector3.zero;
        }

        if (Input.GetKeyDown(KeyCode.Space) && TryStartAction(PlayerMoveState.Rolling))
        {
            return Vector3.zero;
        }
        if (Input.GetMouseButtonDown(0) && _playerAttack != null && _playerAttack.TryStartAttack())
        {
            _rotationVelocity = 0f;
            return Vector3.zero;
        }

        if (_playerAttack != null && _playerAttack.IsAttacking)
        {
            _rotationVelocity = 0f;
            return Vector3.zero;
        }

        if (Input.GetMouseButtonDown(1) && Input.GetKey(KeyCode.W)
                                        && TryStartAction(PlayerMoveState.Dashing))
        {
            return Vector3.zero;
        }
        if (_moveDirection.sqrMagnitude < 0.0001f)
        {
            _rotationVelocity = 0f;
            return Vector3.zero;
        }

        float targetAngle = Mathf.Atan2(_moveDirection.x, _moveDirection.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle,
            ref _rotationVelocity, Mathf.Max(0.01f, _rotationSmoothTime), Mathf.Infinity, deltaTime);

        transform.rotation = Quaternion.Euler(0f, angle, 0f);
        return _moveDirection * _moveSpeed * deltaTime;
    }

    private void ReadMoveDirection()
    {
        if (!CanReadGameplayInput)
        {
            _moveDirection = Vector3.zero;
            return;
        }

        Vector2 moveInput = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        _moveDirection = GetCameraRelativeDirection(moveInput);
    }

    private Vector3 GetCameraRelativeDirection(Vector2 moveInput)
    {
        Vector3 cameraForward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up);

        // 카메라가 수직으로 내려다봐도 화면의 위쪽을 이동의 전방으로 사용합니다.
        if (cameraForward.sqrMagnitude < 0.0001f)
        {
            cameraForward = Vector3.ProjectOnPlane(_cameraTransform.up, Vector3.up);
        }
        cameraForward.Normalize();
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);
        return cameraForward * moveInput.y + cameraRight * moveInput.x;
    }

    private bool TryStartAction(PlayerMoveState nextState)
    {
        if (_currentState != PlayerMoveState.Locomotion || !_characterController.isGrounded)
        {
            return false;
        }
        bool isRoll = nextState == PlayerMoveState.Rolling;

        if (!isRoll && _playerAttack != null && _playerAttack.IsAttacking)
        {
            return false;
        }

        bool cancelAttack = isRoll && _playerAttack != null && _playerAttack.IsAttacking;

        if (cancelAttack)
        {
            _playerAttack.CancelForRoll();
        }

        AnimationClip clip = isRoll ? _rollClip : _dashClip;

        _actionDirection = isRoll && _moveDirection.sqrMagnitude > 0.0001f
            ? _moveDirection.normalized
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        if (isRoll)
        {
            transform.rotation = Quaternion.LookRotation(_actionDirection, Vector3.up);
        }

        _rotationVelocity = 0f;
        _actionDistance = Mathf.Max(0f, isRoll ? _rollDistance : _dashDistance);
        _actionDuration = Mathf.Max(0.05f, isRoll ? _rollDuration : _dashDuration);
        _actionElapsed = 0f;
        _actionStateHash = isRoll ? RollStateHash : DashStateHash;
        _actionAnimationStarted = false;
        _currentState = nextState;

        _animator.SetBool(IsMovingHash, false);
        _animator.SetFloat(ActionSpeedHash, clip.length / _actionDuration);
        _animator.ResetTrigger(RollTriggerHash);
        _animator.ResetTrigger(DashTriggerHash);
        if (cancelAttack)
        {
            // 공격의 남은 재생 시간이나 전환을 기다리지 않고 구르기로 바꿉니다.
            _animator.Play(RollStateHash, 0, 0f);
        }
        else
        {
            _animator.SetTrigger(isRoll ? RollTriggerHash : DashTriggerHash);
        }
        if (isRoll)
        {
            // 구르기가 실제로 수락된 순간, 첫 이동을 적용하기 전에 통지합니다.
            RollStarted?.Invoke(this);
        }

        return true;
    }

    private Vector3 TickAction(float deltaTime)
    {
        AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);

        if (currentState.shortNameHash == _actionStateHash || (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).shortNameHash == _actionStateHash))
        {
            _actionAnimationStarted = true;
        }
        // 이동 시간뿐 아니라 실제 모션과 Idle 복귀 전환까지 끝나야 입력을 다시 받습니다.
        if (_actionElapsed >= _actionDuration && _actionAnimationStarted && IsLocomotionReady())
        {
            _currentState = PlayerMoveState.Locomotion;
            _animator.SetFloat(ActionSpeedHash, 1f);
            return Vector3.zero;
        }

        float previousElapsed = _actionElapsed;
        _actionElapsed = Mathf.Min(_actionElapsed + deltaTime, _actionDuration);

        // 마지막 프레임은 남은 시간만 이동하므로 설정한 이동량을 초과하지 않습니다.
        float stepDistance = _actionDistance * ((_actionElapsed - previousElapsed) / _actionDuration);
        return _actionDirection * stepDistance;
    }

    private bool IsLocomotionReady()
    {
        if (_animator.IsInTransition(0))
        {
            return false;
        }

        int stateHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        return stateHash == IdleStateHash || stateHash == RunStateHash;
    }
}
