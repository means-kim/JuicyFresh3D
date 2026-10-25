using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class KatanaAudio_s : MonoBehaviour
{
    [SerializeField] private KatanaSoundProfile_s _profile;

    [Tooltip("선택 사항. 미지정 시 기본 오디오 출력으로 재생합니다.")]
    [SerializeField] private AudioMixerGroup _output;

    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [SerializeField, Range(0f, 1f)] private float _spatialBlend = 0f;
    private AudioSource _motionSource;
    private AudioSource _hitSource;
    private PlayerComboAnimation_s _combo;
    private KatanaChargeSkill_s _charge;
    private SwordHitDetector_s _detector;
    private int _lastHitSwing = -1;
    private bool _lastHitSkill;

    private void Awake()
    {
        _combo = GetComponent<PlayerComboAnimation_s>();
        _charge = GetComponent<KatanaChargeSkill_s>();
        _detector = GetComponentInChildren<SwordHitDetector_s>(true);
        _motionSource = CreateSource("Katana Motion Audio");
        _hitSource = CreateSource("Katana Hit Audio");
    }

    private AudioSource CreateSource(string sourceName)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;
        return source;
    }

    private void OnEnable()
    {
        if (_detector != null)
        {
            _detector.HitConfirmed += OnHitConfirmed;
        }

        if (_combo != null)
        {
            _combo.DamageWindowClosed += StopMotion;
        }

        if (_charge != null)
        {
            _charge.DamageWindowClosed += StopMotion;
        }
    }

    // 전용 클립 이벤트 값: 일반 콤보 1~4, 차징 돌진 5.
    public void PlayKatanaSwing(int phase)
    {
        if (!CanPlayMotion(phase)) return;
        Play(_motionSource, _profile.GetSwingClip(phase), _profile.SwingVolume);
    }

    public void PlayKatanaSheatheTurn(int phase)
    {
        if (!CanPlayMotion(phase)) return;
        StopMotion();
        Play(_motionSource, _profile.SheatheTurnClip, _profile.SheatheTurnVolume);
    }

    public void PlayKatanaSheathe(int phase)
    {
        if (!CanPlayMotion(phase)) return;
        StopMotion();
        Play(_motionSource, _profile.SheatheClip, _profile.SheatheVolume);
    }

    private bool CanPlayMotion(int phase)
    {
        if (!isActiveAndEnabled || _profile == null) return false;
        // 전환되어 나가는 클립이나 취소된 공격의 이벤트는 재생하지 않는다.
        if (phase == 5)
        {
            return _charge != null && _charge.TryGetDamagePhase(out _, out _, out _, out _);
        }

        return _combo != null && _combo.TryGetDamagePhase(out _, out int step, out _, out _)
                              && step == phase;
    }

    private void OnHitConfirmed(int swing, bool skill, int step)
    {
        if (!isActiveAndEnabled || _profile == null || (_lastHitSwing == swing && _lastHitSkill == skill)) return;
        _lastHitSwing = swing;
        _lastHitSkill = skill;
        Play(_hitSource, _profile.GetHitClip(step, skill), _profile.GetHitVolume(step, skill));
    }

    private void Play(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null) return;
        source.outputAudioMixerGroup = _output;
        source.spatialBlend = _spatialBlend;
        source.volume = Mathf.Clamp01(_volume);
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void StopMotion()
    {
        if (_motionSource != null) _motionSource.Stop();
    }

    private void OnDisable()
    {
        if (_detector != null)
        {
            _detector.HitConfirmed -= OnHitConfirmed;
        }

        if (_combo != null)
        {
            _combo.DamageWindowClosed -= StopMotion;
        }

        if (_charge != null)
        {
            _charge.DamageWindowClosed -= StopMotion;
        }

        if (_motionSource != null)
        {
            _motionSource.Stop();
        }

        if (_hitSource != null)
        {
            _hitSource.Stop();
        }

        _lastHitSwing = -1;
    }
}