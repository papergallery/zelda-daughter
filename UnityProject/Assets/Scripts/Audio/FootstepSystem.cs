using UnityEngine;
using ZeldaDaughter.Input;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Audio
{
    /// <summary>
    /// Plays footstep sounds based on movement speed and surface type.
    /// Accumulates distance traveled; fires a sound when threshold is reached.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FootstepSystem : MonoBehaviour
    {
        [Header("Intervals")]
        [SerializeField] private float _walkStepInterval = 0.65f;
        [SerializeField] private float _runStepInterval = 0.5f;

        [Header("Pitch")]
        [SerializeField] private float _pitchVariation = 0.1f;

        [Header("Clips by Surface")]
        [SerializeField] private AudioClip[] _grassClips;
        [SerializeField] private AudioClip[] _stoneClips;
        [SerializeField] private AudioClip[] _dirtClips;
        [SerializeField] private AudioClip[] _woodClips;
        [SerializeField] private AudioClip[] _waterClips;

        private AudioSource _audioSource;
        private SurfaceType _currentSurface = SurfaceType.Grass;
        private float _currentSpeed;
        private float _distanceAccumulated;

        private const float StopThreshold = 0.1f;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f; // 2D — own footsteps
            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
            _audioSource.volume = 0.3f; // не слишком громко
        }

        private void OnEnable()
        {
            CharacterMovement.OnSpeedChanged += HandleSpeedChanged;
            SurfaceDetector.OnSurfaceChanged += HandleSurfaceChanged;
        }

        private void OnDisable()
        {
            CharacterMovement.OnSpeedChanged -= HandleSpeedChanged;
            SurfaceDetector.OnSurfaceChanged -= HandleSurfaceChanged;
        }

        private void Update()
        {
            if (_currentSpeed < StopThreshold)
                return;

            _distanceAccumulated += _currentSpeed * Time.deltaTime;

            // Run threshold mirrors CharacterMovement._runThreshold (0.7 * runSpeed ≈ 3.5 m/s).
            // Use a configurable speed boundary: if speed > half of max walk, treat as run interval.
            float stepInterval = _currentSpeed > 3f ? _runStepInterval : _walkStepInterval;

            if (_distanceAccumulated >= stepInterval)
            {
                _distanceAccumulated -= stepInterval;
                PlayFootstep();
            }
        }

        private void PlayFootstep()
        {
            if (_audioSource.isPlaying) return;

            var clips = GetClipsForSurface(_currentSurface);
            if (clips == null || clips.Length == 0)
                return;

            var clip = clips[Random.Range(0, clips.Length)];
            _audioSource.pitch = 1f + Random.Range(-_pitchVariation, _pitchVariation);
            _audioSource.PlayOneShot(clip);
        }

        private AudioClip[] GetClipsForSurface(SurfaceType surface)
        {
            return surface switch
            {
                SurfaceType.Grass  => _grassClips,
                SurfaceType.Stone  => _stoneClips,
                SurfaceType.Dirt   => _dirtClips,
                SurfaceType.Wood   => _woodClips,
                SurfaceType.Water  => _waterClips,
                SurfaceType.Sand   => _dirtClips, // fallback: sand uses dirt clips
                _                  => _grassClips
            };
        }

        private void HandleSpeedChanged(float speed)
        {
            _currentSpeed = speed;

            // Reset accumulator when player stops to avoid instant step on resume
            if (speed < StopThreshold)
                _distanceAccumulated = 0f;
        }

        private void HandleSurfaceChanged(SurfaceType surface)
        {
            _currentSurface = surface;
        }
    }
}
