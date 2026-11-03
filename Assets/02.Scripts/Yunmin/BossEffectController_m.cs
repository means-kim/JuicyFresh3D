using UnityEngine;

// 보스 연출 진입점입니다. 보스가 피격되면 아웃라인과 화이트 플래시를 함께 재생합니다.
[DisallowMultipleComponent]
public sealed class BossEffectController_m : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BossStatus_k _bossStatus;
    [SerializeField] private HitEffectController_m _hitEffectController;
    [SerializeField] private HitFlashEffect_m _hitFlashEffect;

    private float _lastBossHp;

    [ContextMenu("Play Hit Effect")]
    public void PlayHitEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        ResolveComponents();

        if (EffectToggleSettings_m.IsOn(EffectToggleType_m.BossHitOutline))
        {
            _hitEffectController?.PlayHitEffect();
        }

        if (_hitFlashEffect != null && EffectToggleSettings_m.IsOn(EffectToggleType_m.BossHitFlash))
        {
            _hitFlashEffect.PlayEffect();
        }
    }

    public void StopHitEffect()
    {
        _hitEffectController?.StopHitEffect();
        _hitFlashEffect?.StopEffect();
    }

    private void Awake()
    {
        ResolveComponents();
    }

    private void OnEnable()
    {
        ResolveComponents();

        if (_bossStatus != null)
        {
            _lastBossHp = _bossStatus.CurrentHp;
            _bossStatus.Damaged -= OnBossHpChanged;
            _bossStatus.Damaged += OnBossHpChanged;
        }
    }

    private void Start()
    {
        // 첫 OnEnable은 BossStatus_k.Awake보다 먼저 호출될 수 있어 체력을 다시 기록합니다.
        if (_bossStatus != null)
        {
            _lastBossHp = _bossStatus.CurrentHp;
        }
    }

    private void OnDisable()
    {
        if (_bossStatus != null)
        {
            _bossStatus.Damaged -= OnBossHpChanged;
        }
    }

    // Damaged는 회복 시에도 호출되므로 체력이 줄었을 때만 재생합니다.
    private void OnBossHpChanged(float currentHp, float maxHp)
    {
        bool isHit = currentHp < _lastBossHp;
        _lastBossHp = currentHp;

        if (isHit)
        {
            PlayHitEffect();
        }
    }

    private void ResolveComponents()
    {
        if (_bossStatus == null)
        {
            _bossStatus = GetComponent<BossStatus_k>();
        }
        if (_hitEffectController == null)
        {
            _hitEffectController = GetComponentInChildren<HitEffectController_m>();
        }
        if (_hitFlashEffect == null)
        {
            _hitFlashEffect = GetComponentInChildren<HitFlashEffect_m>();
        }
    }
}
