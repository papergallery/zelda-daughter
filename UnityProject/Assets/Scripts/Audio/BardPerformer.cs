using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.Audio
{
    /// <summary>
    /// Manages music playback for a bard NPC. Attach to the bard's GameObject.
    /// Plays tracks from BardMusicData sequentially or shuffled, with gaps between tracks.
    /// Call SetPerforming(true/false) from NPCSchedule when activity changes to/from Performing.
    /// </summary>
    public class BardPerformer : MonoBehaviour
    {
        [SerializeField] private BardMusicData _musicData;
        [SerializeField] private AudioSource _audioSource;

        private const float FadeDuration = 1f;

        private bool _isPerforming;
        private int _currentTrackIndex;
        private int[] _playlist;
        private Coroutine _playbackCoroutine;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            _audioSource.spatialBlend = 1f;
            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
            _audioSource.volume = 0f;
        }

        /// <summary>
        /// Called by NPCSchedule or external system when bard's activity changes.
        /// </summary>
        public void SetPerforming(bool performing)
        {
            if (_isPerforming == performing) return;
            _isPerforming = performing;

            if (_isPerforming)
                StartPlaying();
            else
                StopPlaying();
        }

        private void StartPlaying()
        {
            if (_musicData == null || _musicData.Tracks == null || _musicData.Tracks.Length == 0)
                return;

            BuildPlaylist();

            if (_playbackCoroutine != null)
                StopCoroutine(_playbackCoroutine);

            _playbackCoroutine = StartCoroutine(PlaylistRoutine());
        }

        private void StopPlaying()
        {
            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeVolume(_audioSource.volume, 0f, FadeDuration, () =>
            {
                _audioSource.Stop();
            }));
        }

        private IEnumerator PlaylistRoutine()
        {
            // Fade in first track
            _currentTrackIndex = 0;

            while (_isPerforming)
            {
                int trackIdx = _playlist[_currentTrackIndex % _playlist.Length];
                AudioClip clip = _musicData.Tracks[trackIdx];

                if (clip == null)
                {
                    AdvanceTrack();
                    continue;
                }

                _audioSource.clip = clip;
                _audioSource.Play();

                // Fade in
                if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
                yield return StartCoroutine(FadeVolume(_audioSource.volume, 1f, FadeDuration, null));

                // Wait for track to finish (minus fade out time so there's no gap)
                float waitTime = clip.length - FadeDuration;
                if (waitTime > 0f)
                    yield return new WaitForSeconds(waitTime);

                // Fade out at end of track
                if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
                yield return StartCoroutine(FadeVolume(1f, 0f, FadeDuration, null));

                _audioSource.Stop();
                AdvanceTrack();

                // Gap between tracks
                if (_musicData.TrackGap > 0f)
                    yield return new WaitForSeconds(_musicData.TrackGap);
            }
        }

        private void AdvanceTrack()
        {
            _currentTrackIndex++;
            if (_currentTrackIndex >= _playlist.Length)
            {
                // Reshuffle when playlist loops
                if (_musicData.Shuffle)
                    ShufflePlaylist();
                _currentTrackIndex = 0;
            }
        }

        private void BuildPlaylist()
        {
            int count = _musicData.Tracks.Length;
            _playlist = new int[count];
            for (int i = 0; i < count; i++)
                _playlist[i] = i;

            if (_musicData.Shuffle)
                ShufflePlaylist();
        }

        private void ShufflePlaylist()
        {
            for (int i = _playlist.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_playlist[i], _playlist[j]) = (_playlist[j], _playlist[i]);
            }
        }

        private IEnumerator FadeVolume(float from, float to, float duration, System.Action onComplete)
        {
            float elapsed = 0f;
            _audioSource.volume = from;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _audioSource.volume = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            _audioSource.volume = to;
            onComplete?.Invoke();
        }

        private void OnDestroy()
        {
            if (_playbackCoroutine != null) StopCoroutine(_playbackCoroutine);
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        }
    }
}
