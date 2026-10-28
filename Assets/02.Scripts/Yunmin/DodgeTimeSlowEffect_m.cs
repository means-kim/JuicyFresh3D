using UnityEngine;

[DisallowMultipleComponent]
public sealed class DodgeTimeSlowEffect_m : MonoBehaviour
{
    [Header("Time Slow")]
    [SerializeField] private TimeSlowSettings_m _timeSlow = new();

    public void PlayEffect()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;

        TimeScaleController_m.Play(_timeSlow);
    }
}
