using UnityEngine;

namespace ZeldaDaughter.Audio
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Audio/City Ambience Data", fileName = "NewCityAmbienceData")]
    public class CityAmbienceData : ScriptableObject
    {
        [Header("Clips")]
        [SerializeField] private AudioClip[] _ambientLoops;

        [Header("Volume")]
        [SerializeField] private float _baseVolume = 0.4f;

        [Header("Fade")]
        [SerializeField] private float _fadeInDuration = 1.5f;
        [SerializeField] private float _fadeOutDuration = 2f;

        [Header("Identity")]
        [SerializeField] private string _raceName;

        public AudioClip[] AmbientLoops => _ambientLoops;
        public float BaseVolume => _baseVolume;
        public float FadeInDuration => _fadeInDuration;
        public float FadeOutDuration => _fadeOutDuration;
        public string RaceName => _raceName;
    }
}
