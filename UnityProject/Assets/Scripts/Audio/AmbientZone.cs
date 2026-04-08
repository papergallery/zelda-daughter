using System.Collections;
using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Audio
{
    /// <summary>
    /// Spatial ambient audio zone. Fades in when the player enters a SphereCollider trigger,
    /// fades out on exit. Swaps day/night clips when TimeOfDay changes.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(SphereCollider))]
    public class AmbientZone : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] private AudioClip _dayClip;
        [SerializeField] private AudioClip _nightClip;

        [Header("Volume")]
        [SerializeField] private float _maxVolume = 0.5f;
        [SerializeField] private float _fadeSpeed = 1f;

        private AudioSource _audioSource;
        private SphereCollider _trigger;

        private bool _playerInZone;
        private float _targetVolume;

        private Coroutine _crossfadeRoutine;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.volume = 0f;

            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
        }

        private void OnEnable()
        {
            DayNightCycle.OnTimeOfDayChanged += HandleTimeOfDayChanged;
        }

        private void OnDisable()
        {
            DayNightCycle.OnTimeOfDayChanged -= HandleTimeOfDayChanged;
        }

        private void Start()
        {
            // Start with day clip by default; will be corrected by first OnTimeOfDayChanged event
            _audioSource.clip = _dayClip;
        }

        private void Update()
        {
            // Smooth volume toward target — avoids allocations, no coroutine for simple fade
            if (!Mathf.Approximately(_audioSource.volume, _targetVolume))
            {
                _audioSource.volume = Mathf.MoveTowards(
                    _audioSource.volume, _targetVolume, _fadeSpeed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            _playerInZone = true;

            if (!_audioSource.isPlaying)
                _audioSource.Play();

            _targetVolume = _maxVolume;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            _playerInZone = false;
            _targetVolume = 0f;
        }

        private void HandleTimeOfDayChanged(TimeOfDay timeOfDay)
        {
            var targetClip = timeOfDay switch
            {
                TimeOfDay.Night => _nightClip,
                TimeOfDay.Dusk  => _nightClip,
                _               => _dayClip    // Day, Dawn
            };

            if (targetClip == null || targetClip == _audioSource.clip)
                return;

            if (_crossfadeRoutine != null)
                StopCoroutine(_crossfadeRoutine);

            _crossfadeRoutine = StartCoroutine(CrossfadeClip(targetClip));
        }

        private IEnumerator CrossfadeClip(AudioClip nextClip)
        {
            // Fade out current clip
            float startVolume = _audioSource.volume;
            float elapsed = 0f;
            float fadeDuration = _fadeSpeed > 0f ? startVolume / _fadeSpeed : 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
                yield return null;
            }

            _audioSource.volume = 0f;
            _audioSource.clip = nextClip;

            if (_playerInZone)
            {
                _audioSource.Play();
                _targetVolume = _maxVolume;
            }
            else
            {
                // Clip swapped but zone not active — just update clip, don't play
                _targetVolume = 0f;
            }

            _crossfadeRoutine = null;
        }
    }
}
