using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Audio
{
    /// <summary>
    /// Point-source 3D sound emitter (forge, tavern, mill, etc.).
    /// Active only during configured work hours. Uses DayNightCycle for current time.
    /// Requires a DayNightCycle instance in the scene.
    /// </summary>
    public class PointSoundEmitter : MonoBehaviour
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField] private AudioSource _audioSource;

        [Header("Work Hours")]
        [SerializeField] private float _startHour = 6f;
        [SerializeField] private float _endHour = 22f;

        [Header("Playback")]
        [SerializeField] private bool _looping = true;

        private DayNightCycle _dayNightCycle;
        private bool _isActive;

        private void Awake()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            _audioSource.spatialBlend = 1f;
            _audioSource.loop = _looping;
            _audioSource.playOnAwake = false;
            _audioSource.clip = _clip;
        }

        private void Start()
        {
            // FindObjectOfType is acceptable in Start (not Update); called once at init
            _dayNightCycle = FindObjectOfType<DayNightCycle>();

            if (_dayNightCycle == null)
            {
                Debug.LogWarning($"[PointSoundEmitter] DayNightCycle not found in scene. '{gameObject.name}' will not respond to time changes.", this);
                return;
            }

            // Evaluate initial state immediately
            UpdateActiveState(_dayNightCycle.CurrentHour);
        }

        private void Update()
        {
            if (_dayNightCycle == null) return;

            bool shouldBeActive = IsWithinWorkHours(_dayNightCycle.CurrentHour);

            if (shouldBeActive != _isActive)
                UpdateActiveState(_dayNightCycle.CurrentHour);
        }

        private void UpdateActiveState(float currentHour)
        {
            bool shouldBeActive = IsWithinWorkHours(currentHour);

            if (shouldBeActive == _isActive) return;

            _isActive = shouldBeActive;

            if (_isActive)
            {
                if (_clip != null && !_audioSource.isPlaying)
                    _audioSource.Play();
            }
            else
            {
                _audioSource.Stop();
            }
        }

        private bool IsWithinWorkHours(float hour)
        {
            // Handles overnight ranges (e.g., 22 to 6) as well as normal ranges (6 to 22)
            if (_startHour <= _endHour)
                return hour >= _startHour && hour < _endHour;
            else
                return hour >= _startHour || hour < _endHour;
        }

        private void OnDestroy()
        {
            _dayNightCycle = null;
        }
    }
}
