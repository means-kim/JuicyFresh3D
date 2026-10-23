using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CrossFlashEffect_m), typeof(ShilhouetteOutlineEffect_m), typeof(OutlineParticleLightEffect_m))]
public sealed class PlayerEffectController_m : MonoBehaviour
{
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
        if (!Application.isPlaying || !isActiveAndEnabled) return;

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
    }

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
        ResolveComponents();
        ApplySharedSettings();
    }

    private void OnEnable()
    {
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

    private void ResolveComponents()
    {
        if (_crossFlashEffect == null)
        {
            _crossFlashEffect = GetComponent<CrossFlashEffect_m>();
        }
        if (_silhouetteOutlineEffect == null)
        {
            _silhouetteOutlineEffect = GetComponent<ShilhouetteOutlineEffect_m>();
        }
        if (_outlineParticleLightEffect == null)
        {
            _outlineParticleLightEffect = GetComponent<OutlineParticleLightEffect_m>();
        }
    }

    private void OnDisable()
    {
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
