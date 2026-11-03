using UnityEngine;

// 피격 연출 진입점입니다. Player와 Boss가 공용으로 사용합니다.
[DisallowMultipleComponent]
public sealed class HitEffectController_m : MonoBehaviour
{
    [Header("Effect Components")]
    [SerializeField] private ShilhouetteOutlineEffect_m _silhouetteOutlineEffect;

    [Header("Profiles")]
    [SerializeField] private OutlineEffectProfile_m _hitOutlineProfile;

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

    private void ResolveComponents()
    {
        if (_silhouetteOutlineEffect == null)
        {
            _silhouetteOutlineEffect = GetComponentInChildren<ShilhouetteOutlineEffect_m>();
        }
    }
}
