using UnityEngine;

// 공격 재생과 입력 예약을 담당하며 Animator 트랜지션은 사용하지 않는다.
[DisallowMultipleComponent]
public class PlayerComboAnimation_s : MonoBehaviour
{
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
    private static readonly int Land = Animator.StringToHash("Base Layer.Land");

    private static readonly int[] BaseAttackStates =
    {
        Animator.StringToHash("Base Layer.Attack1"),
        Animator.StringToHash("Base Layer.Attack2"),
        Animator.StringToHash("Base Layer.Attack3"),
        Animator.StringToHash("Base Layer.Attack4")
    };

    [SerializeField] private Animator _animator;
    [Tooltip("상체 마스크 레이어 이름. 레이어가 있으면 공격을 상체에서 재생하고 하체는 이동 애니메이션을 유지합니다.")]
    [SerializeField] private string _attackLayerName = "UpperBody";
    [Tooltip("활성화하면 BeginComboSlash / BeginFinalComboSlash / EndComboSlash 이벤트로 명중 구간을 제어합니다.")]
    [SerializeField] private bool _useSlashEvents;
    private bool _slashOpen;
    private bool _finalSlash;
    private bool _slashesFinished;
    private int _hitSequence;
    private int _layer = -1;
    private int[] _attackStates = BaseAttackStates;
    private float _layerWeight;
    private float _layerTargetWeight;
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

    private bool UsesAttackLayer => _layer > 0;
    public bool AllowsMovement => UsesAttackLayer;

    public bool TryGetDamagePhase(out int swing, out int step, out float time, out Vector2 window)
    {
        swing = _swingSequence; step = ComboStep; time = 0f; window = Vector2.zero;
        if (!isActiveAndEnabled || !IsPlaying || !_animator || !_animator.isActiveAndEnabled) return false;
        var state = _animator.GetCurrentAnimatorStateInfo(_layer);
        if (state.fullPathHash != _attackStates[_step] || _animator.IsInTransition(_layer)) return false;
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
        var state = _animator.IsInTransition(_layer) ? _animator.GetNextAnimatorStateInfo(_layer)
            : _animator.GetCurrentAnimatorStateInfo(_layer);
        if (state.fullPathHash != _attackStates[_step]) return false;
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

        if (_layer >= 0 || _animator == null || _animator.runtimeAnimatorController == null) return;
        // 상체 레이어가 없으면 기존처럼 Base 레이어에서 전신 공격을 재생한다.
        int layer = string.IsNullOrEmpty(_attackLayerName) ? -1 : _animator.GetLayerIndex(_attackLayerName);
        _layer = layer > 0 ? layer : 0;
        if (!UsesAttackLayer) return;
        _attackStates = new int[BaseAttackStates.Length];
        for (int i = 0; i < _attackStates.Length; i++)
        {
            _attackStates[i] = Animator.StringToHash(_attackLayerName + ".Attack" + (i + 1));
        }
        _animator.SetLayerWeight(_layer, 0f);
    }

    public bool TryBeginCombo()
    {
        Initialize();
        if (!isActiveAndEnabled || IsPlaying || _animator == null || !_animator.isActiveAndEnabled
            || _animator.runtimeAnimatorController == null)
        {
            return false;
        }

        // 상체 레이어를 쓰면 하체 착지·이동 전환과 무관하게 공격할 수 있다.
        if (!UsesAttackLayer && _animator.IsInTransition(0))
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

        foreach (int state in _attackStates)
        {
            if (!_animator.HasState(_layer, state))
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

        var state = _animator.GetCurrentAnimatorStateInfo(_layer);
        bool currentStepActive = state.fullPathHash == _attackStates[_step];

        if (attackPressed && _step < _attackStates.Length - 1
                          && (!currentStepActive || state.normalizedTime < 1f))
        {
            _nextAttackQueued = true;
        }

        // CrossFade 도중에는 이전 상태의 진행률로 다음 단계를 실행하지 않는다.
        if (!currentStepActive || _animator.IsInTransition(_layer)) return;

        float chainTime = _step == 0 ? Mathf.Max(_firstChainTime, _firstDamageWindow.y)
            : _step == 1 ? Mathf.Max(_secondChainTime, _secondDamageWindow.y)
            : Mathf.Max(_thirdChainTime, _thirdDamageWindow.y);
        if (_nextAttackQueued && _step < _attackStates.Length - 1 && state.normalizedTime >= chainTime
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
        bool wasPlaying = IsPlaying;
        _step = step;
        _nextAttackQueued = false;
        if (UsesAttackLayer && (!wasPlaying || _layerWeight <= 0f))
        {
            // 쉬고 있던 상체 레이어는 첫 프레임부터 재생하고 가중치를 올려 하체 이동과 섞는다.
            _animator.Play(_attackStates[step], _layer, 0f);
            _layerTargetWeight = 1f;
            return;
        }
        _layerTargetWeight = 1f;
        _animator.CrossFadeInFixedTime(_attackStates[step], _crossFadeDuration, _layer, 0f);
    }

    public void EndCombo()
    {
        ResetSlashes();
        DamageWindowClosed?.Invoke();
        bool wasPlaying = IsPlaying;
        _step = -1;
        _nextAttackQueued = false;
        if (UsesAttackLayer)
        {
            // 하체는 이미 이동 애니메이션이므로 상체 레이어만 내린다.
            _layerTargetWeight = 0f;
            return;
        }
        if (wasPlaying && _animator != null && _animator.isActiveAndEnabled
            && _animator.runtimeAnimatorController != null && _animator.HasState(0, Locomotion))
        {
            _animator.CrossFadeInFixedTime(Locomotion, _crossFadeDuration, 0, 0f);
        }
    }

    private void Update()
    {
        if (!UsesAttackLayer || _animator == null || Mathf.Approximately(_layerWeight, _layerTargetWeight)) return;
        float step = _crossFadeDuration > 0f ? Time.deltaTime / _crossFadeDuration : 1f;
        _layerWeight = Mathf.MoveTowards(_layerWeight, _layerTargetWeight, step);
        _animator.SetLayerWeight(_layer, _layerWeight);
    }

    private void OnDisable()
    {
        EndCombo();
        if (!UsesAttackLayer || _animator == null) return;
        _layerWeight = 0f;
        _animator.SetLayerWeight(_layer, 0f);
    }
}
