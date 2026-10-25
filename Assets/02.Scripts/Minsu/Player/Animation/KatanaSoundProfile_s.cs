using UnityEngine;

[CreateAssetMenu(fileName = "KatanaSoundProfile", menuName = "Audio/Katana Sound Profile")]
public class KatanaSoundProfile_s : ScriptableObject
{
    [Header("공격별 고정 휘두르기")]
    [SerializeField] private AudioClip _attack1Clip;
    [SerializeField] private AudioClip _attack2Clip;
    [SerializeField] private AudioClip _attack3Clip;
    [SerializeField] private AudioClip _attack4Clip;
    [Tooltip("차징 돌진은 1타 클립을 공유합니다.")]
    [SerializeField, Range(0f, 1f)] private float _swingVolume = 0.65f;
    [Header("1타 명중 (공격당 한 번)")]
    [SerializeField] private AudioClip _attack1HitClip;
    [SerializeField, Range(0f, 1f)] private float _attack1HitVolume = 0.75f;
    [Header("2타 명중 (공격당 한 번)")]
    [SerializeField] private AudioClip _attack2HitClip;
    [SerializeField, Range(0f, 1f)] private float _attack2HitVolume = 0.75f;
    [Header("3타 명중 (공격당 한 번)")]
    [SerializeField] private AudioClip _attack3HitClip;
    [SerializeField, Range(0f, 1f)] private float _attack3HitVolume = 0.75f;
    [Header("4타 명중 (공격당 한 번)")]
    [SerializeField] private AudioClip _attack4HitClip;
    [SerializeField, Range(0f, 1f)] private float _attack4HitVolume = 0.75f;
    [Header("차징 공격 명중 (공격당 한 번)")]
    [SerializeField] private AudioClip _chargeHitClip;
    [SerializeField, Range(0f, 1f)] private float _chargeHitVolume = 0.75f;
    [Header("차징 유지음")]
    [SerializeField] private AudioClip _chargeLoopClip;
    [SerializeField, Range(0f, 1f)] private float _chargeLoopVolume = 0.4f;
    [Header("최대 차징 완료음")]
    [SerializeField] private AudioClip _chargeCompleteClip;
    [SerializeField, Range(0f, 1f)] private float _chargeCompleteVolume = 0.75f;
    [Header("납도 전 칼 돌리기")]
    [SerializeField] private AudioClip _sheatheTurnClip;
    [SerializeField, Range(0f, 1f)] private float _sheatheTurnVolume = 0.65f;
    [Header("검집에 넣기")]
    [SerializeField] private AudioClip _sheatheClip;
    [SerializeField, Range(0f, 1f)] private float _sheatheVolume = 0.5f;

    public AudioClip GetSwingClip(int phase)
    {
        switch (phase)
        {
            case 1:
            case 5:
                return _attack1Clip;
            case 2:
                return _attack2Clip;
            case 3:
                return _attack3Clip;
            case 4:
                return _attack4Clip;
            default:
                return null;
        }
    }

    public AudioClip SheatheTurnClip => _sheatheTurnClip;
    public float SheatheTurnVolume => _sheatheTurnVolume;
    public AudioClip GetHitClip(int step, bool skill)
    {
        if (skill) return _chargeHitClip;
        switch (step)
        {
            case 1: return _attack1HitClip;
            case 2: return _attack2HitClip;
            case 3: return _attack3HitClip;
            case 4: return _attack4HitClip;
            default: return null;
        }
    }

    public float GetHitVolume(int step, bool skill)
    {
        if (skill) return _chargeHitVolume;
        switch (step)
        {
            case 1: return _attack1HitVolume;
            case 2: return _attack2HitVolume;
            case 3: return _attack3HitVolume;
            case 4: return _attack4HitVolume;
            default: return 0f;
        }
    }

    public AudioClip ChargeLoopClip => _chargeLoopClip;
    public float ChargeLoopVolume => _chargeLoopVolume;
    public AudioClip ChargeCompleteClip => _chargeCompleteClip;
    public float ChargeCompleteVolume => _chargeCompleteVolume;
    public AudioClip SheatheClip => _sheatheClip;
    public float SwingVolume => _swingVolume;
    public float SheatheVolume => _sheatheVolume;
}
