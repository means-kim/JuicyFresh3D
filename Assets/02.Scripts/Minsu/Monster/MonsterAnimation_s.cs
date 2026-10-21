using UnityEngine;

[DisallowMultipleComponent]
public class MonsterAnimation_s : MonoBehaviour
{
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
    private static readonly int Hit = Animator.StringToHash("Base Layer.Hit");
    private static readonly int Dead = Animator.StringToHash("Base Layer.Death");

    [SerializeField] private Animator _animator;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator != null) _animator.applyRootMotion = false;
    }

    public void PlayState(MonsterStateType_s stateTypeS)
    {
        Initialize();
        if (_animator == null || !_animator.isActiveAndEnabled) return;
        int hash = stateTypeS == MonsterStateType_s.Dead ? Dead : stateTypeS == MonsterStateType_s.Hit ? Hit
            : stateTypeS == MonsterStateType_s.Walk ? Walk : Idle;
        _animator.CrossFadeInFixedTime(hash, 0.05f, 0, 0f);
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
}
