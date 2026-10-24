using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimation_s : MonoBehaviour
{
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveZ = Animator.StringToHash("MoveZ");
    private static readonly int SprintBlend = Animator.StringToHash("SprintBlend");
    private static readonly int Hit = Animator.StringToHash("Base Layer.Hit");
    private static readonly int Dead = Animator.StringToHash("Base Layer.Death");
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
    private static readonly int Jump = Animator.StringToHash("Base Layer.Jump");
    private static readonly int Land = Animator.StringToHash("Base Layer.Land");
    private static readonly int DoubleJump = Animator.StringToHash("Base Layer.DoubleJump");
    private static readonly int Fall = Animator.StringToHash("Base Layer.Fall");
    private bool _airborne;

    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _dampTime = 0.1f;

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
        _animator.SetFloat(SprintBlend, 0f);
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

    public void PlayJump(int jumpCount)
    {
        Initialize();
        int jumpState = jumpCount > 1 ? DoubleJump : Jump;
        if (_animator == null || !_animator.HasState(0, jumpState)) return;
        _airborne = true;
        _animator.CrossFadeInFixedTime(jumpState, 0.06f, 0, 0f);
    }

    public void UpdateAirborne(bool airborne)
    {
        Initialize();
        if (_animator == null || !_animator.HasState(0, Jump)) return;
        if (airborne)
        {
            // 발판에서 떨어질 때는 도약 동작 없이 공중 유지 자세로 전환한다.
            if (!_airborne)
            {
                _airborne = true;
                _animator.CrossFadeInFixedTime(Fall, 0.06f, 0, 0f);
            }
            else if (!_animator.IsInTransition(0))
            {
                var state = _animator.GetCurrentAnimatorStateInfo(0);
                if ((state.fullPathHash == Jump || state.fullPathHash == DoubleJump)
                    && state.normalizedTime >= 0.95f)
                {
                    _animator.CrossFadeInFixedTime(Fall, 0.06f, 0, 0f);
                }
            }
        }
        else if (_airborne)
        {
            _airborne = false;
            if (_animator.HasState(0, Land))
            {
                _animator.CrossFadeInFixedTime(Land, 0.08f, 0, 0f);
            }
            else
            {
                PlayLocomotion();
            }
        }
        else if (!_animator.IsInTransition(0))
        {
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            // 착지 복귀를 코드에서 처리한다. 공격·피격 등으로 중단된 상태는 덮어쓰지 않는다.
            if (state.fullPathHash == Land && state.normalizedTime >= 0.94f)
            {
                _animator.CrossFadeInFixedTime(Locomotion, 0.12f, 0, 0f);
            }
        }
    }

    public void UpdateLocomotion(Vector3 worldVelocity, float speed, bool sprint, float deltaTime)
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
        _animator.SetFloat(SprintBlend, sprint ? 1f : 0f, _dampTime, deltaTime);
    }
}


