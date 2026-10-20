using UnityEngine;

// 공격 재생과 입력 예약을 담당하며 Animator 트랜지션은 사용하지 않는다.
[DisallowMultipleComponent]
public class PlayerComboAnimation_s : MonoBehaviour
{
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");

    private static readonly int[] AttackStates =
    {
        Animator.StringToHash("Base Layer.Attack1"),
        Animator.StringToHash("Base Layer.Attack2"),
        Animator.StringToHash("Base Layer.Attack3")
    };

    [SerializeField] private Animator _animator;

    [Header("정규화된 재생 시간 (0~1)")]
    [SerializeField, Range(0.1f, 0.95f)] private float _firstChainTime = 0.65f;

    [SerializeField, Range(0.1f, 0.95f)] private float _secondChainTime = 0.65f;
    [SerializeField, Range(0f, 0.2f)] private float _crossFadeDuration = 0.05f;

    private int _step = -1;
    private bool _nextAttackQueued;
    public bool IsPlaying => _step >= 0;
    public int ComboStep => _step + 1;
    public bool HasQueuedAttack => _nextAttackQueued;

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

    public bool TryBeginCombo()
    {
        Initialize();
        if (!isActiveAndEnabled || IsPlaying || _animator == null || !_animator.isActiveAndEnabled
            || _animator.runtimeAnimatorController == null || _animator.IsInTransition(0))
        {
            return false;
        }

        if (!_animator.HasState(0, Locomotion))
        {
            return false;
        }

        foreach (int state in AttackStates)
        {
            if (!_animator.HasState(0, state))
            {
                return false;
            }
        }

        PlayStep(0);

        return true;
    }

    // PlayerAttack_s가 한 프레임에 한 번 호출한다. 연타해도 다음 한 단계만 예약된다.
    public void TickCombo(bool attackPressed)
    {
        if (!IsPlaying) return;
        if (!isActiveAndEnabled || _animator == null || !_animator.isActiveAndEnabled
            || _animator.runtimeAnimatorController == null)
        {
            EndCombo();
            return;
        }

        var state = _animator.GetCurrentAnimatorStateInfo(0);
        bool currentStepActive = state.fullPathHash == AttackStates[_step];

        if (attackPressed && _step < AttackStates.Length - 1
                          && (!currentStepActive || state.normalizedTime < 1f))
        {
            _nextAttackQueued = true;
        }

        // CrossFade 도중에는 이전 상태의 진행률로 다음 단계를 실행하지 않는다.
        if (!currentStepActive || _animator.IsInTransition(0)) return;

        float chainTime = _step == 0 ? _firstChainTime : _secondChainTime;
        if (_nextAttackQueued && _step < AttackStates.Length - 1 && state.normalizedTime >= chainTime)
        {
            PlayStep(_step + 1);
            return;
        }

        if (state.normalizedTime >= 1f)
        {
            EndCombo();
        }
    }

    private void PlayStep(int step)
    {
        _step = step;
        _nextAttackQueued = false;
        _animator.CrossFade(AttackStates[step], _crossFadeDuration, 0, 0f);
    }

    public void EndCombo()
    {
        bool wasPlaying = IsPlaying;
        _step = -1;
        _nextAttackQueued = false;
        if (wasPlaying && _animator != null && _animator.isActiveAndEnabled
            && _animator.runtimeAnimatorController != null && _animator.HasState(0, Locomotion))
        {
            _animator.CrossFade(Locomotion, _crossFadeDuration, 0, 0f);
        }
    }

    private void OnDisable()
    {
        EndCombo();
    }
}