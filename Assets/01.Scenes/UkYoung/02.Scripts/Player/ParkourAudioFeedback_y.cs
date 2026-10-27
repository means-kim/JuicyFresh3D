using UnityEngine;

namespace JuicyFresh3D.Parkour
{
    [RequireComponent(typeof(ParkourFeedbackSignals_y))]
    public sealed class ParkourAudioFeedback_y : MonoBehaviour
    {
        [SerializeField] private ParkourFeedbackSignals_y _signals;
        [SerializeField] private ParkourMotor _motor;

        [Header("Clips")]
        [SerializeField] private AudioClip[] _footsteps;
        [SerializeField] private AudioClip _clothMovement;
        [SerializeField] private AudioClip _bodyImpact;
        [SerializeField] private AudioClip _handContactLight;
        [SerializeField] private AudioClip _handContactStrong;
        [SerializeField] private AudioClip _surfaceScrapeShort;
        [SerializeField] private AudioClip _surfaceScrapeLong;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 0.35f;

        private AudioSource _oneShotSource;
        private AudioSource _scrapeSource;
        private int _previousFootstepIndex = -1;

        private void Awake()
        {
            _signals ??= GetComponent<ParkourFeedbackSignals_y>();
            _motor ??= GetComponent<ParkourMotor>();
            _oneShotSource = CreateAudioSource("Parkour One Shots", false);
            _scrapeSource = CreateAudioSource("Parkour Surface Loop", true);
            _scrapeSource.clip = _surfaceScrapeLong;
        }

        private void OnEnable()
        {
            if (_signals != null)
            {
                _signals.FootContact += HandleFootContact;
                _signals.JumpTakeoff += HandleJumpTakeoff;
                _signals.LandingContact += HandleLandingContact;
                _signals.HandContact += HandleHandContact;
            }

            if (_motor != null)
            {
                _motor.SubStateChanged += HandleSubStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_signals != null)
            {
                _signals.FootContact -= HandleFootContact;
                _signals.JumpTakeoff -= HandleJumpTakeoff;
                _signals.LandingContact -= HandleLandingContact;
                _signals.HandContact -= HandleHandContact;
            }

            if (_motor != null)
            {
                _motor.SubStateChanged -= HandleSubStateChanged;
            }
        }

        private void Update()
        {
            bool shouldScrape = _motor != null && (_motor.IsSliding || _motor.IsWallRunning);
            float targetVolume = shouldScrape
                ? _masterVolume * Mathf.InverseLerp(3f, 12f, _motor.PlanarSpeed) * 0.45f
                : 0f;
            _scrapeSource.volume = Mathf.MoveTowards(
                _scrapeSource.volume,
                targetVolume,
                Time.deltaTime * 2.5f);

            if (shouldScrape && !_scrapeSource.isPlaying && _scrapeSource.clip != null)
            {
                _scrapeSource.Play();
            }
            else if (!shouldScrape && _scrapeSource.isPlaying && _scrapeSource.volume <= 0.001f)
            {
                _scrapeSource.Stop();
            }
        }

        private AudioSource CreateAudioSource(string sourceName, bool loop)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = _spatialBlend;
            source.minDistance = 2f;
            source.maxDistance = 20f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        private void HandleFootContact(Vector3 point, Vector3 normal, float intensity)
        {
            AudioClip clip = GetNextFootstep();
            PlayOneShot(clip, Mathf.Lerp(0.32f, 0.52f, intensity), 0.97f, 1.04f);
        }

        private void HandleJumpTakeoff(Vector3 point, Vector3 normal, float intensity)
        {
            PlayOneShot(GetNextFootstep(), 0.28f, 1.02f, 1.07f);
            PlayOneShot(_clothMovement, 0.25f, 1.02f, 1.08f);
        }

        private void HandleLandingContact(Vector3 point, Vector3 normal, float intensity)
        {
            PlayOneShot(GetNextFootstep(), Mathf.Lerp(0.48f, 0.78f, intensity), 0.92f, 1.01f);
            if (intensity >= 0.35f)
            {
                PlayOneShot(_bodyImpact, Mathf.Lerp(0.24f, 0.7f, intensity), 0.94f, 1.02f);
            }
        }

        private void HandleHandContact(Vector3 point, Vector3 normal, float intensity)
        {
            AudioClip clip = intensity >= 0.6f ? _handContactStrong : _handContactLight;
            PlayOneShot(clip, Mathf.Lerp(0.34f, 0.62f, intensity), 0.96f, 1.04f);
        }

        private void HandleSubStateChanged(
            ParkourSubStateType_y previousState,
            ParkourSubStateType_y currentState)
        {
            if (currentState == ParkourSubStateType_y.Slide
                || currentState == ParkourSubStateType_y.WallRun)
            {
                PlayOneShot(_surfaceScrapeShort, 0.36f, 0.97f, 1.03f);
            }
            else if (currentState == ParkourSubStateType_y.Roll)
            {
                PlayOneShot(_clothMovement, 0.52f, 0.92f, 1f);
                PlayOneShot(_bodyImpact, 0.32f, 0.96f, 1.02f);
            }
        }

        private AudioClip GetNextFootstep()
        {
            if (_footsteps == null || _footsteps.Length == 0)
            {
                return null;
            }

            if (_footsteps.Length == 1)
            {
                return _footsteps[0];
            }

            int index = Random.Range(0, _footsteps.Length - 1);
            if (index >= _previousFootstepIndex)
            {
                index++;
            }

            _previousFootstepIndex = index;
            return _footsteps[index];
        }

        private void PlayOneShot(AudioClip clip, float volume, float minimumPitch, float maximumPitch)
        {
            if (clip == null || _oneShotSource == null)
            {
                return;
            }

            _oneShotSource.pitch = Random.Range(minimumPitch, maximumPitch);
            _oneShotSource.PlayOneShot(clip, volume * _masterVolume);
        }
    }
}
