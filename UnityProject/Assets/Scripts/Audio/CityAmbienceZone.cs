using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.Audio
{
    /// <summary>
    /// Ambient sound zone for city areas. Place on an empty GameObject with a BoxCollider (isTrigger).
    /// AudioSources array must match CityAmbienceData.AmbientLoops array in length.
    /// Player enters → all loops fade in. Player exits → all loops fade out.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CityAmbienceZone : MonoBehaviour
    {
        [SerializeField] private CityAmbienceData _ambienceData;
        [SerializeField] private AudioSource[] _audioSources;

        private Coroutine[] _fadeCoroutines;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;

            _fadeCoroutines = new Coroutine[_audioSources.Length];

            for (int i = 0; i < _audioSources.Length; i++)
            {
                var src = _audioSources[i];
                if (src == null) continue;

                src.spatialBlend = 0f; // City ambience is non-spatial (fills the zone evenly)
                src.loop = true;
                src.playOnAwake = false;
                src.volume = 0f;

                if (_ambienceData != null && i < _ambienceData.AmbientLoops.Length)
                    src.clip = _ambienceData.AmbientLoops[i];
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            for (int i = 0; i < _audioSources.Length; i++)
            {
                var src = _audioSources[i];
                if (src == null || src.clip == null) continue;

                if (!src.isPlaying)
                    src.Play();

                StartFade(i, src.volume, _ambienceData.BaseVolume, _ambienceData.FadeInDuration);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            for (int i = 0; i < _audioSources.Length; i++)
            {
                var src = _audioSources[i];
                if (src == null) continue;

                StartFade(i, src.volume, 0f, _ambienceData.FadeOutDuration, () => src.Stop());
            }
        }

        private void StartFade(int index, float from, float to, float duration, System.Action onComplete = null)
        {
            if (_fadeCoroutines[index] != null)
                StopCoroutine(_fadeCoroutines[index]);

            _fadeCoroutines[index] = StartCoroutine(FadeVolume(_audioSources[index], from, to, duration, onComplete));
        }

        private IEnumerator FadeVolume(AudioSource source, float from, float to, float duration, System.Action onComplete)
        {
            float elapsed = 0f;
            source.volume = from;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            source.volume = to;
            onComplete?.Invoke();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _fadeCoroutines.Length; i++)
            {
                if (_fadeCoroutines[i] != null)
                    StopCoroutine(_fadeCoroutines[i]);
            }
        }
    }
}
