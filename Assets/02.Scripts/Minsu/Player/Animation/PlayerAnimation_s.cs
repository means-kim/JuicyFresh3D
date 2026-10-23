using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimation_s : MonoBehaviour
{
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveZ = Animator.StringToHash("MoveZ");
    private static readonly int MoveRate = Animator.StringToHash("MoveRate");
    private static readonly int Hit = Animator.StringToHash("Base Layer.Hit");
    private static readonly int Dead = Animator.StringToHash("Base Layer.Death");
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
    private static readonly int Jump = Animator.StringToHash("Base Layer.Jump");
    private static readonly int Land = Animator.StringToHash("Base Layer.Land");
    private static readonly int JumpTime = Animator.StringToHash("JumpTime");
    private bool _airborne;
    private float _jumpProgress;

    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _dampTime = 0.1f;
    [SerializeField, Min(0.01f)] private float _referenceRunSpeed = 5f;

    private void Awake()
    {
        Initialize();
    }

    public bool IsHitFinished
    {
        get
        {
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.IsInTransition(0)) return false;
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == Hit && state.normalizedTime >= 1f;
        }
    }

    public void PlayReaction(MovementState state)
    {
        _airborne = false;
        Initialize();
        if (_animator == null || !_animator.isActiveAndEnabled) return;
        _animator.CrossFadeInFixedTime(state == MovementState.Dead ? Dead : Hit, 0.05f, 0, 0f);
    }

    public void PlayLocomotion()
    {
        _airborne = false;
        Initialize();
        if (_animator == null || !_animator.isActiveAndEnabled) return;
        _animator.SetFloat(MoveX, 0f);
        _animator.SetFloat(MoveZ, 0f);
        _animator.CrossFadeInFixedTime(Locomotion, 0.05f, 0, 0f);
    }

    private void Initialize()
    {
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        if (_animator != null)
        {
            _animator.applyRootMotion = false;
        }
    }

    public void PlayJump()
    {
        Initialize();
        if (_animator == null || !_animator.HasState(0, Jump)) return;
        _airborne = true;
        _jumpProgress = 0.12f;
        _animator.SetFloat(JumpTime, _jumpProgress);
        _animator.CrossFadeInFixedTime(Jump, 0.06f, 0, 0f);
    }

    public void UpdateAirborne(bool airborne, float verticalVelocity, float jumpPower, float deltaTime)
    {
        Initialize();
        if (_animator == null || !_animator.HasState(0, Jump)) return;
        if (airborne)
        {
            if (!_airborne) PlayJump();
            float target = Mathf.Lerp(0.12f, 0.7f,
                Mathf.InverseLerp(jumpPower, -jumpPower, verticalVelocity));
            _jumpProgress = Mathf.MoveTowards(_jumpProgress, target, deltaTime * 1.5f);
            _animator.SetFloat(JumpTime, _jumpProgress);
        }
        else if (_airborne)
        {
            _airborne = false;
            if (_animator.HasState(0, Land))
            {
                _animator.CrossFadeInFixedTime(Land, 0.08f, 0, 1f);
            }
            else
            {
                PlayLocomotion();
            }
        }
    }

    public void UpdateLocomotion(Vector3 worldVelocity, float speed, float deltaTime)
    {
        if (_animator == null)
        {
            Initialize();
        }

        if (_animator == null || _animator.runtimeAnimatorController == null || deltaTime <= 0f) return;
        // 실제 이동량을 사용하여 벽에 막힌 경우 달리기를 멈춘다.
        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
        Vector2 movement = Vector2.ClampMagnitude(new Vector2(localVelocity.x, localVelocity.z)
                                                  / Mathf.Max(speed, 0.01f), 1f);
        _animator.SetFloat(MoveX, movement.x, _dampTime, deltaTime);
        _animator.SetFloat(MoveZ, movement.y, _dampTime, deltaTime);
        _animator.SetFloat(MoveRate, Mathf.Lerp(1f,
            Mathf.Max(speed / Mathf.Max(_referenceRunSpeed, 0.01f), 0.01f), movement.magnitude));
    }
}


