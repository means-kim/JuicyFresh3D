using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerEffectController_m : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private PostProcessEffectController_m _postProcessManager;
    [SerializeField] private PlayerMove _target;

    [Header("Shared Settings")]
    [SerializeField, ColorUsage(true, true)]
    private Color _effectColor = new Color(0.35f, 1f, 0.85f, 1f);

    [Tooltip("Manual stop fade duration shared by both effects. Zero stops immediately.")]
    [SerializeField, Min(0f)] private float _stopFadeDuration = 0.4f;

    [Header("Effect Components")]
    [SerializeField] private CrossFlashEffect_m _crossFlashEffect;
    [SerializeField] private ShilhouetteOutlineEffect_m _silhouetteOutlineEffect;
    [SerializeField] private OutlineParticleLightEffect_m _outlineParticleLightEffect;

    public Color EffectColor => _effectColor;

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
            _silhouetteOutlineEffect.PlayEffect();
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
        _effectColor = color;
        ApplySharedSettings();
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
        if (_crossFlashEffect != null) _crossFlashEffect.SetSharedSettings(_effectColor, _stopFadeDuration);
        if (_silhouetteOutlineEffect != null) _silhouetteOutlineEffect.SetSharedSettings(_effectColor, _stopFadeDuration);
        if (_outlineParticleLightEffect != null) _outlineParticleLightEffect.SetSharedSettings(_effectColor, _stopFadeDuration);
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
