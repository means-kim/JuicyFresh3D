using UnityEngine;

// 피격 연출 진입점입니다. Player와 Boss가 공용으로 사용합니다.
[DisallowMultipleComponent]
public sealed class HitEffectController_m : MonoBehaviour
{
    [Header("Effect Components")]
    [SerializeField] private ShilhouetteOutlineEffect_m _silhouetteOutlineEffect;

    [Header("Profiles")]
    [SerializeField] private OutlineEffectProfile_m _hitOutlineProfile;

    [Header("Boss")]
    [Tooltip("지정하면 보스 체력이 감소할 때 피격 연출을 재생합니다. 비어 있으면 같은 오브젝트에서 찾습니다.")]
    [SerializeField] private BossStatus_k _bossStatus;

    private float _lastBossHp;

    [ContextMenu("Play Hit Effect")]
    public void PlayHitEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        ResolveComponents();

        if (_silhouetteOutlineEffect != null)
        {
            _silhouetteOutlineEffect.PlayEffect(_hitOutlineProfile);
        }
    }

    public void StopHitEffect()
    {
        if (_silhouetteOutlineEffect != null)
        {
            _silhouetteOutlineEffect.StopEffect();
        }
    }

    private void Awake()
    {
        ResolveComponents();
    }

    private void OnEnable()
    {
        if (_bossStatus == null)
        {
            _bossStatus = GetComponent<BossStatus_k>();
        }

        if (_bossStatus != null)
        {
            _lastBossHp = _bossStatus.CurrentHp;
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
        if (_silhouetteOutlineEffect == null)
        {
            _silhouetteOutlineEffect = GetComponentInChildren<ShilhouetteOutlineEffect_m>();
        }
    }
}
