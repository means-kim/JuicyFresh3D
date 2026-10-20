using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimation_s : MonoBehaviour
{
    private static readonly int LocomotionState = Animator.StringToHash("Base Layer.Locomotion");
    private static readonly int AttackState = Animator.StringToHash("Base Layer.Attack");
    private bool _attackRequested;
    public bool CanAttack
    {
        get
        {
            Initialize();
            return _animator != null && _animator.isActiveAndEnabled
                && _animator.runtimeAnimatorController != null && _animator.HasState(0, AttackState)
                && !_animator.IsInTransition(0);
        }
    }
    public bool AttackFinished
    {
        get
        {
            if (!_attackRequested || _animator == null || !_animator.isActiveAndEnabled) return true;
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == AttackState && state.normalizedTime >= 1f
                && !_animator.IsInTransition(0);
        }
    }
    public void BeginAttack()
    {
        _attackRequested = true;
        // CrossFade의 전환 길이는 초가 아닌 정규화된 시간이다.
        _animator.CrossFade(AttackState, 0.08f, 0, 0f);
    }
    public void EndAttack()
    {
        _attackRequested = false;
        if (_animator != null && _animator.isActiveAndEnabled
                              && _animator.runtimeAnimatorController != null)
        {
            _animator.CrossFade(LocomotionState, 0.1f, 0, 0f);
        }
    }

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveZ = Animator.StringToHash("MoveZ");
    private static readonly int MoveRate = Animator.StringToHash("MoveRate");

    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _dampTime = 0.1f;
    [SerializeField, Min(0.01f)] private float _referenceRunSpeed = 5f;

    private void Awake()
    {
        Initialize();
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

