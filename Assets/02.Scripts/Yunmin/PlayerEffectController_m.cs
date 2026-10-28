using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class PlayerEffectController_m : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private PostProcessEffectController_m _postProcessManager;
    [SerializeField] private HitEffectController_m _hitEffectController;
    [SerializeField] private CrossFlashEffect_m _crossFlashEffect;
    [SerializeField] private ShilhouetteOutlineEffect_m _silhouetteOutlineEffect;
    [SerializeField] private OutlineParticleLightEffect_m _outlineParticleLightEffect;
    [SerializeField] private PlayerMove _target;

    [Header("Shared Settings")]
    [Tooltip("Fallback color used only when no dodge outline profile is assigned.")]
    [SerializeField, ColorUsage(true, true), FormerlySerializedAs("_effectColor")]
    private Color _defaultEffectColor = new Color(0.35f, 1f, 0.85f, 1f);

    [Tooltip("Manual stop fade duration shared by both effects. Zero stops immediately.")]
    [SerializeField, Min(0f)] private float _stopFadeDuration = 0.4f;

    [Header("Outline Profiles")]
    [Tooltip("Outline width and timing for dodge. Its color is shared by every dodge effect.")]
    [SerializeField] private OutlineEffectProfile_m _dodgeOutlineProfile;

    // 회피 연출 전체가 프로필 색을 따르고, 프로필이 없을 때만 기본 색을 씁니다.
    private Color DodgeEffectColor => _dodgeOutlineProfile != null ? _dodgeOutlineProfile.Color : _defaultEffectColor;

    public void PlayEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        ResolveReferences();
        ApplySharedSettings();

        if (_silhouetteOutlineEffect != null)
        {
            _silhouetteOutlineEffect.PlayEffect(_dodgeOutlineProfile);
        }

        if (_outlineParticleLightEffect != null)
        {
            _outlineParticleLightEffect.PlayEffect();
        }

        if (_crossFlashEffect != null)
        {
            _crossFlashEffect.PlayEffect();
        }

        _postProcessManager?.PlayDodgeEffect();
    }
    // 안광희 수정

    public void StopEffect()
    {
        ApplySharedSettings();

        if (_crossFlashEffect != null)
        {
            _crossFlashEffect.StopEffect();
        }

        if (_outlineParticleLightEffect != null)
        {
            _outlineParticleLightEffect.StopEffect();
        }

        if (_silhouetteOutlineEffect != null)
        {
            _silhouetteOutlineEffect.StopEffect();
        }
    }

    public void SetEffectColor(Color color)
    {
        _defaultEffectColor = color;
        ApplySharedSettings();
    }

    public void PlayHitEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        ResolveReferences();
        ApplySharedSettings();

        _hitEffectController?.PlayHitEffect();
    }

    private void Awake()
    {
        ResolveReferences();
        ResolveComponents();
        ApplySharedSettings();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
        ApplySharedSettings();
    }


    private void Update()
    {
        ApplySharedSettings();
    }

    private void ApplySharedSettings()
    {
        Color color = DodgeEffectColor;
        if (_crossFlashEffect != null) _crossFlashEffect.SetSharedSettings(color, _stopFadeDuration);
        // 아웃라인 색은 재생 시점에만 전달해 피격 등 다른 연출 색을 덮어쓰지 않습니다.
        if (_silhouetteOutlineEffect != null) _silhouetteOutlineEffect.SetStopFadeDuration(_stopFadeDuration);
        if (_outlineParticleLightEffect != null) _outlineParticleLightEffect.SetSharedSettings(color, _stopFadeDuration);
    }

    private void ResolveReferences()
    {
        if (_target == null)
        {
            _target = GetComponent<PlayerMove>();
        }

        if (_postProcessManager == null)
        {
            _postProcessManager = FindAnyObjectByType<PostProcessEffectController_m>();
        }
    }

    private void SubscribeEvents()
    {
        if (_target == null)
        {
            return;
        }

        _target.DodgeSucceeded -= PlayEffect;
        _target.DodgeSucceeded += PlayEffect;
    }

    private void UnsubscribeEvents()
    {
        if (_target == null)
        {
            return;
        }

        _target.DodgeSucceeded -= PlayEffect;
    }
    // 안광희 수정
    private void ResolveComponents()
    {
        if (_crossFlashEffect == null)
        {
            _crossFlashEffect = GetComponentInChildren<CrossFlashEffect_m>();
        }
        if (_silhouetteOutlineEffect == null)
        {
            _silhouetteOutlineEffect = GetComponentInChildren<ShilhouetteOutlineEffect_m>();
        }
        if (_outlineParticleLightEffect == null)
        {
            _outlineParticleLightEffect = GetComponentInChildren<OutlineParticleLightEffect_m>();
        }
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        ResolveComponents();
        if (_crossFlashEffect != null)
        {
            _crossFlashEffect.Clear();
        }

        if (_silhouetteOutlineEffect != null)
        {
            _silhouetteOutlineEffect.Clear();
        }

        if (_outlineParticleLightEffect != null)
        {
            _outlineParticleLightEffect.Clear();
        }
    }
}
