using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class HitStop_s : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _hitStopDuration = 0.05f;
    private Coroutine _routine;
    private float _previousTimeScale;
    private int _lastSwing = -1;
    private bool _lastSkillSwing;

    public void ScreenStop(int swing, bool skillSwing)
    {
        if (!isActiveAndEnabled || _hitStopDuration <= 0f
            || (_lastSwing == swing && _lastSkillSwing == skillSwing))
        {
            return;
        }

        // 같은 공격의 다른 대상은 정지 종료 후 맞더라도 다시 정지시키지 않는다.
        _lastSwing = swing;
        _lastSkillSwing = skillSwing;
        if (_routine != null || Time.timeScale <= 0f) return;

        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        _routine = StartCoroutine(StopRoutine());
    }

    private IEnumerator StopRoutine()
    {
        yield return new WaitForSecondsRealtime(_hitStopDuration);
        RestoreTimeScale();
        _routine = null;
    }

    private void RestoreTimeScale()
    {
        // 다른 시스템이 이미 배율을 바꿨다면 덮어쓰지 않는다.
        if (Time.timeScale == 0f) Time.timeScale = _previousTimeScale;
    }

    private void OnDisable()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
        RestoreTimeScale();
    }
}
