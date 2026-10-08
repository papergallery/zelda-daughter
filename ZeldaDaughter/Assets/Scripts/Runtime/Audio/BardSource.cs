using UnityEngine;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.Audio
{
    /// <summary>
    /// D-17: the only music of the game (project-design.md §9) — the bard in the tavern, from <see cref="FromHour"/> to <see cref="ToHour"/> (evening),
    /// heard only near the tavern (a spatial source: full at <c>MinDistance</c>, gone at <c>MaxDistance</c>) and fading in and out; by day it is
    /// silent. One AudioSource, made once. Sound <c>bard_tavern</c> of the registry; without a clip (the library has no lute, the April tracks have
    /// no confirmed licence — docs/demo/backlog.md) it is silence and one line in the log.
    /// </summary>
    public sealed class BardSource : MonoBehaviour
    {
        private const float FadeSeconds = 2f;

        [SerializeField] private GameSession _session;
        [SerializeField] private SoundRegistry _sounds;
        [SerializeField] private float _fromHour = 17f;
        [SerializeField] private float _toHour = 23f;
        [SerializeField] private float _minDistance = 4f;
        [SerializeField] private float _maxDistance = 30f;
        [SerializeField] private AudioSource _source;

        private GameState _g;
        private float _level;
        private bool _reported;

        public float FromHour => _fromHour;
        public float ToHour => _toHour;
        /// <summary>0 … 1: how far the bard has faded in (before the sound's own volume).</summary>
        public float Level => _level;
        /// <summary>It is the bard's hour by the clock (and the state is loaded).</summary>
        public bool Evening => _g != null && IsBardHour((float)(_g.Clock.TimeOfDay * 24.0));
        /// <summary>The source is making sound right now.</summary>
        public bool Playing => _source != null && _source.isPlaying && _source.volume > 0.001f;
        public AudioSource Source => _source;

        public void Configure(GameSession session, SoundRegistry sounds, float fromHour, float toHour)
        {
            bool live = Application.isPlaying && isActiveAndEnabled;
            if (live && _session != null) _session.Events.StateReady -= OnStateReady;
            _session = session;
            _sounds = sounds;
            _fromHour = fromHour;
            _toHour = toHour;
            if (live && _session != null) _session.Events.StateReady += OnStateReady;
        }

        public bool IsBardHour(float hour) => hour >= _fromHour && hour < _toHour;

        private void Awake()
        {
            if (_source != null) return;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.volume = 0f;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = _minDistance;
            _source.maxDistance = _maxDistance;
        }

        private void OnEnable() { if (_session != null) { _session.Events.StateReady -= OnStateReady; _session.Events.StateReady += OnStateReady; } }
        private void OnDisable() { if (_session != null) _session.Events.StateReady -= OnStateReady; }
        private void OnStateReady(GameState g) => _g = g;

        private SoundDef Def()
        {
            if (_sounds == null) return null;
            foreach (var d in _sounds.Sounds) if (d != null && d.Id == "bard_tavern") return d;
            return null;
        }

        private void Update()
        {
            var def = Def();
            bool has = def != null && def.Clips != null && def.Clips.Length > 0;
            if (!has)
            {
                if (!_reported && _g != null)
                {
                    _reported = true;
                    ZdLog.Info("Audio", "bard silent: no clip for bard_tavern");
                }
                return;
            }
            if (_source.clip == null)
            {
                _source.clip = def.Clips[Random.Range(0, def.Clips.Length)];
                _source.loop = true;
            }
            _level = Mathf.MoveTowards(_level, Evening ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
            _source.volume = _level * def.Volume;
            if (_level > 0.002f)
            {
                if (!_source.isPlaying)
                {
                    _source.Play();
                    ZdLog.Info("Audio", "bard plays");
                }
            }
            else if (_source.isPlaying)
            {
                _source.Stop();
                ZdLog.Info("Audio", "bard stops");
            }
        }
    }
}
