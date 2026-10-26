using UnityEngine;

// 공격 재생과 입력 예약을 담당하며 Animator 트랜지션은 사용하지 않는다.
[DisallowMultipleComponent]
public class PlayerComboAnimation_s : MonoBehaviour
{
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
    private static readonly int Land = Animator.StringToHash("Base Layer.Land");

    private static readonly int[] AttackStates =
    {
        Animator.StringToHash("Base Layer.Attack1"),
        Animator.StringToHash("Base Layer.Attack2"),
        Animator.StringToHash("Base Layer.Attack3"),
        Animator.StringToHash("Base Layer.Attack4")
    };

    [SerializeField] private Animator _animator;
    [Tooltip("활성화하면 BeginComboSlash / BeginFinalComboSlash / EndComboSlash 이벤트로 명중 구간을 제어합니다.")]
    [SerializeField] private bool _useSlashEvents;
    private bool _slashOpen;
    private bool _finalSlash;
    private bool _slashesFinished;
    private int _hitSequence;
    public bool IsIntermediateSlash => _useSlashEvents && !_finalSlash;
    public event System.Action SlashOpened;
    public event System.Action SlashClosed;

    [Header("정규화된 재생 시간 (0~1)")]
    [SerializeField, Range(0.1f, 0.95f)] private float _firstChainTime = 0.45f;

    [SerializeField, Range(0.1f, 0.95f)] private float _secondChainTime = 0.5f;
    [SerializeField, Range(0.1f, 0.95f)] private float _thirdChainTime = 0.36f;
    [Tooltip("공격 전환 시간(초)")]
    [SerializeField, Range(0f, 0.2f)] private float _crossFadeDuration = 0.05f;

    [Header("타격 구간 (정규화된 재생 시간)")]
    [SerializeField] private Vector2 _firstDamageWindow = new Vector2(0.16f, 0.44f);
    [SerializeField] private Vector2 _secondDamageWindow = new Vector2(0.32f, 0.49f);
    [SerializeField] private Vector2 _thirdDamageWindow = new Vector2(0.2f, 0.5f);
    [SerializeField] private Vector2 _fourthDamageWindow = new Vector2(0.025f, 0.27f);
    private int _swingSequence;
    public event System.Action DamageWindowClosed;

    public bool TryGetDamagePhase(out int swing, out int step, out float time, out Vector2 window)
    {
        swing = _swingSequence; step = ComboStep; time = 0f; window = Vector2.zero;
        if (!isActiveAndEnabled || !IsPlaying || !_animator || !_animator.isActiveAndEnabled) return false;
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != AttackStates[_step] || _animator.IsInTransition(0)) return false;
        time = state.normalizedTime;
        window = _step == 0 ? _firstDamageWindow : _step == 1 ? _secondDamageWindow
            : _step == 2 ? _thirdDamageWindow : _fourthDamageWindow;
        return true;
    }

    private int _step = -1;
    public bool TryGetHitPhase(out int hit, out int step, out float time, out Vector2 window)
    {
        hit = _hitSequence;
        if (!_useSlashEvents) return TryGetDamagePhase(out _, out step, out time, out window);
        step = ComboStep;
        window = new Vector2(0f, 1f);
        return TryGetSlashTime(step, out time) && _slashOpen;
    }

    private bool TryGetSlashTime(int step, out float time)
    {
        time = 0f;
        if (!isActiveAndEnabled || !IsPlaying || step != ComboStep || _animator == null || !_animator.isActiveAndEnabled)
        {
            return false;
        }
        // 크로스페이드 중 들어오는 공격의 초반 이벤트도 받되, 나가는 공격은 무시한다.
        var state = _animator.IsInTransition(0) ? _animator.GetNextAnimatorStateInfo(0)
            : _animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != AttackStates[_step]) return false;
        time = state.normalizedTime;
        return true;
    }

    // intParameter는 콤보 타수(1~4). 중간 베기와 마지막 베기를 구분한다.
    public void BeginComboSlash(int step)
    {
        BeginSlash(step, false);
    }

    public void BeginFinalComboSlash(int step)
    {
        BeginSlash(step, true);
    }

    private void BeginSlash(int step, bool final)
    {
        if (!_useSlashEvents || !TryGetSlashTime(step, out _)) return;
        _hitSequence++;
        _slashOpen = true;
        _finalSlash = final;
        _slashesFinished = false;
        SlashOpened?.Invoke();
    }

    public void EndComboSlash(int step)
    {
        if (!_useSlashEvents || !TryGetSlashTime(step, out _)) return;
        if (_slashOpen && _finalSlash) _slashesFinished = true;
        _slashOpen = false;
        SlashClosed?.Invoke();
    }

    private void ResetSlashes()
    {
        _slashOpen = false;
        _finalSlash = false;
        _slashesFinished = false;
        SlashClosed?.Invoke();
    }
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
            || _animator.runtimeAnimatorController == null)
        {
            return false;
        }

        if (_animator.IsInTransition(0))
        {
            var current = _animator.GetCurrentAnimatorStateInfo(0);
            var next = _animator.GetNextAnimatorStateInfo(0);
            // 접지 검사는 PlayerAttack_s에서 수행한다. 착지 블렌드는 공격으로 끊을 수 있다.
            bool landing = next.fullPathHash == Land
                           || (current.fullPathHash == Land && next.fullPathHash == Locomotion);
            if (!landing) return false;
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

        float chainTime = _step == 0 ? Mathf.Max(_firstChainTime, _firstDamageWindow.y)
            : _step == 1 ? Mathf.Max(_secondChainTime, _secondDamageWindow.y)
            : Mathf.Max(_thirdChainTime, _thirdDamageWindow.y);
        if (_nextAttackQueued && _step < AttackStates.Length - 1 && state.normalizedTime >= chainTime
            && (!_useSlashEvents || (_slashesFinished && !_slashOpen)))
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
        ResetSlashes();
        _hitSequence++;
        DamageWindowClosed?.Invoke();
        _swingSequence++;
        _step = step;
        _nextAttackQueued = false;
        _animator.CrossFadeInFixedTime(AttackStates[step], _crossFadeDuration, 0, 0f);
    }

    public void EndCombo()
    {
        ResetSlashes();
        DamageWindowClosed?.Invoke();
        bool wasPlaying = IsPlaying;
        _step = -1;
        _nextAttackQueued = false;
        if (wasPlaying && _animator != null && _animator.isActiveAndEnabled
            && _animator.runtimeAnimatorController != null && _animator.HasState(0, Locomotion))
        {
            _animator.CrossFadeInFixedTime(Locomotion, _crossFadeDuration, 0, 0f);
        }
    }

    private void OnDisable()
    {
        EndCombo();
    }
}

