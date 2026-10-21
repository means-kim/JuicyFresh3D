using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement_s : MonoBehaviour
{
    public MovementState CurrentState => _state != null ? _state.Kind : MovementState.Idle;

    private const float Gravity = -20f;
    private const int MaxJumps = 2;
    private float _yVelocity;
    private int _jumpCount;
    private State_s _state;
    private GroundedState_s _idle;
    private GroundedState_s _move;
    private GroundedState_s _sprint;
    private AirborneState_s _airborne;

    [Header("스탯")]
    [SerializeField] private float _moveSpeed = 7f;

    [FormerlySerializedAs("_strintSpeed")]
    [SerializeField] private float _sprintSpeed = 12f;

    [SerializeField] private float _jumpPower = 10f;

    [Header("컴포넌트")]
    [SerializeField] private CharacterController _characterController;

    [SerializeField] private PlayerAnimation_s _animation;
    [SerializeField] private PlayerAttack_s _combat;

    private struct FrameInput
    {
        public Vector3 Direction;
        public bool Sprint;
        public bool Jump;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        if (_animation == null) _animation = GetComponent<PlayerAnimation_s>();
        if (_combat == null) _combat = GetComponent<PlayerAttack_s>();
        _idle = new GroundedState_s(this, MovementState.Idle);
        _move = new GroundedState_s(this, MovementState.Move);
        _sprint = new GroundedState_s(this, MovementState.Sprint);
        _airborne = new AirborneState_s(this);
        // 첫 Move 전에는 isGrounded가 아직 갱신되지 않았을 수 있다.
        ChangeState(_airborne);
    }

    private void Update()
    {
        var input = new FrameInput
        {
            Direction = Vector3.ClampMagnitude(new Vector3(
                Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")), 1f),
            Sprint = Input.GetButton("Sprint"),
            Jump = Input.GetButtonDown("Jump")
        };
        SimulateMovement(input.Direction, input.Sprint, input.Jump, Time.deltaTime);
    }

    // 입력 수집과 분리하여 AI 입력이나 에디터 검증에서도 같은 FSM을 사용할 수 있다.
    public void SimulateMovement(Vector3 direction, bool sprint, bool jump, float deltaTime)
    {
        if (_state == null) Awake();
        Tick(new FrameInput { Direction = Vector3.ClampMagnitude(direction, 1f), Sprint = sprint, Jump = jump },
            deltaTime);
    }

    private void Tick(FrameInput input, float deltaTime)
    {
        if (!_characterController.enabled || deltaTime <= 0f) return;
        bool grounded = _characterController.isGrounded && _yVelocity <= 0f;
        // 공격 처리에는 관여하지 않고 이동/점프/질주 입력만 차단한다.
        if (_combat != null && _combat.IsAttacking) input = new FrameInput();
        ChangeState(grounded ? SelectGroundedState(input) : _airborne);
        _state.Tick(input, deltaTime);
    }

    private State_s SelectGroundedState(FrameInput input)
    {
        if (input.Direction.sqrMagnitude < 0.0001f) return _idle;
        return input.Sprint ? _sprint : _move;
    }

    private void ChangeState(State_s next)
    {
        if (_state == next) return;
        _state?.Exit();
        _state = next;
        _state.Enter();
    }

    private bool TryJump(FrameInput input)
    {
        if (!input.Jump || _jumpCount >= MaxJumps) return false;
        _yVelocity = _jumpPower;
        _jumpCount++;
        ChangeState(_airborne);
        return true;
    }

    private void MoveAndAnimate(FrameInput input, float speed, float deltaTime)
    {
        Vector3 velocity = transform.TransformDirection(input.Direction) * speed;
        velocity.y = _yVelocity;
        CollisionFlags collisions = _characterController.Move(velocity * deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && _yVelocity > 0f)
            _yVelocity = 0f;
        if ((collisions & CollisionFlags.Below) != 0 && _yVelocity <= 0f)
        {
            _yVelocity = -1f;
            _jumpCount = 0;
            ChangeState(SelectGroundedState(input));
        }
        else
        {
            ChangeState(_airborne);
        }

        if (_animation != null) _animation.UpdateLocomotion(_characterController.velocity, speed, deltaTime);
    }

    private abstract class State_s
    {
        protected readonly PlayerMovement_s Player;
        public MovementState Kind { get; }

        protected State_s(PlayerMovement_s player, MovementState kind)
        {
            Player = player;
            Kind = kind;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public abstract void Tick(FrameInput input, float deltaTime);
    }

    private sealed class GroundedState_s : State_s
    {
        public GroundedState_s(PlayerMovement_s player, MovementState kind) : base(player, kind)
        {
        }

        public override void Enter()
        {
            Player._yVelocity = -1f;
            Player._jumpCount = 0;
        }

        public override void Tick(FrameInput input, float deltaTime)
        {
            Player._yVelocity = -1f;
            Player.TryJump(input);
            float speed = Kind == MovementState.Sprint ? Player._sprintSpeed : Player._moveSpeed;
            Player.MoveAndAnimate(input, speed, deltaTime);
        }
    }

    private sealed class AirborneState_s : State_s
    {
        public AirborneState_s(PlayerMovement_s player) : base(player, MovementState.Airborne)
        {
        }

        public override void Tick(FrameInput input, float deltaTime)
        {
            Player._yVelocity += Gravity * deltaTime;
            Player.TryJump(input);
            // 기존 공중 방향 제어 및 Shift 가속을 유지한다.
            float speed = input.Sprint ? Player._sprintSpeed : Player._moveSpeed;
            Player.MoveAndAnimate(input, speed, deltaTime);
        }
    }
}

