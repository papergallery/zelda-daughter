using UnityEngine;

namespace ZeldaDaughter.Audio
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Audio/Bard Music Data", fileName = "NewBardMusicData")]
    public class BardMusicData : ScriptableObject
    {
        [Header("Tracks")]
        [SerializeField] private AudioClip[] _tracks;
        [SerializeField] private bool _shuffle;

        [Header("Playback")]
        [SerializeField] private float _trackGap = 2f;

        [Header("Identity")]
        [SerializeField] private string _raceName;

        public AudioClip[] Tracks => _tracks;
        public bool Shuffle => _shuffle;
        public float TrackGap => _trackGap;
        public string RaceName => _raceName;
    }
}
