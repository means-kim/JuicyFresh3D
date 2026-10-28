using UnityEngine;

[System.Serializable]
public sealed class TimeSlowSettings_m
{
    private const float MaxTimeScale = 100f;

    [Tooltip("X: normalized progress (0-1), Y: Time.timeScale at that point. Y = 0 freezes time.")]
    [SerializeField]
    private AnimationCurve _timeScaleCurve = new(
        new Keyframe(0f, 1f),
        new Keyframe(0.1f, 0.2f),
        new Keyframe(0.7f, 0.2f),
        new Keyframe(1f, 1f));

    [Tooltip("Total length in real-time seconds, unaffected by the slowdown itself.")]
    [SerializeField, Min(0.01f)] private float _duration = 1f;

    public AnimationCurve Curve => _timeScaleCurve;
    public float Duration => Mathf.Max(0.01f, _duration);

    public float Evaluate(float normalizedTime)
    {
        if (_timeScaleCurve == null || _timeScaleCurve.length == 0) return 1f;

        return Mathf.Clamp(_timeScaleCurve.Evaluate(Mathf.Clamp01(normalizedTime)), 0f, MaxTimeScale);
    }
}
