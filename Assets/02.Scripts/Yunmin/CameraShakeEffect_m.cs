using Unity.Cinemachine;
using UnityEngine;

// 피격/빈사 카메라 쉐이크입니다. 카메라 쪽에 CinemachineImpulseListener가 있어야 흔들립니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineImpulseSource))]
public sealed class CameraShakeEffect_m : MonoBehaviour
{
    [Header("Shake")]
    [SerializeField, Min(0f)] private float _hitForce = 0.3f;
    [Tooltip("빈사 진입 시 피격 쉐이크에 더해지는 세기입니다.")]
    [SerializeField, Min(0f)] private float _nearDeathForce = 1f;

    private CinemachineImpulseSource _impulseSource;

    [ContextMenu("피격 쉐이크 재생")]
    public void PlayHitShake()
    {
        PlayShake(_hitForce);
    }

    [ContextMenu("빈사 쉐이크 재생")]
    public void PlayNearDeathShake()
    {
        PlayShake(_nearDeathForce);
    }

    private void Awake()
    {
        ResolveComponents();
    }

    private void PlayShake(float force)
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        ResolveComponents();
        if (_impulseSource == null || force <= 0f)
        {
            return;
        }

        _impulseSource.GenerateImpulseWithForce(force);
    }

    private void ResolveComponents()
    {
        if (_impulseSource == null)
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }
    }
}
