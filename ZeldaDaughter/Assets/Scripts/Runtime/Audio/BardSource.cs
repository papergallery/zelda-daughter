using UnityEngine;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.Audio
{
    /// <summary>
    /// D-17: the only music of the game (project-design.md §9) — the bard in the tavern, from <see cref="FromHour"/> to <see cref="ToHour"/> (evening),
    /// heard only near the tavern (a spatial source: full at <c>MinDistance</c>, gone at <c>MaxDistance</c>) and fading in and out; by day it is
    /// silent. One AudioSource, made once. Sound <c>bard_tavern</c> of the registry — several tunes (D-17b, generated, Assets/Art/Audio/Generated/bard):
    /// when one comes round to its start the bard goes on to the next. Without a clip it is silence and one line in the log.
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
        private float _level, _lastTime;
        private int _tune = -1;
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
        /// <summary>How many times the bard has started a tune (the first one of the evening counts).</summary>
        public int TunesStarted { get; private set; }

        public void Configure(GameSession session, SoundRegistry sounds, float fromHour, float toHour)
        {
            bool live = Application.isPlaying && isActiveAndEnabled;
            if (live && _session != null) _session.Events.StateReady -= OnStateReady;
            _session = session;
            _sounds = sounds;
            _fromHour = fromHour;
            _toHour = toHour;
            _tune = -1;
            if (_source != null) { _source.Stop(); _source.clip = null; } // another registry: its own tunes
            _level = 0f;
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
            if (_source.clip == null) NextTune(def, false);
            _level = Mathf.MoveTowards(_level, Evening ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
            _source.volume = _level * def.Volume;
            if (_level > 0.002f)
            {
                if (!_source.isPlaying)
                {
                    _source.Play();
                    TunesStarted++;
                    _lastTime = 0f;
                    ZdLog.Info("Audio", $"bard plays {_source.clip.name}");
                }
                else if (def.Clips.Length > 1 && _source.time + 0.05f < _lastTime) NextTune(def, true); // came round to the start: the next tune
                _lastTime = _source.time;
            }
            else if (_source.isPlaying)
            {
                _source.Stop();
                NextTune(def, false); // the next evening — another tune
                ZdLog.Info("Audio", "bard stops");
            }
        }

        /// <summary>The next tune in turn (a random first one); <paramref name="play"/> — straight on, the source is looping the old one.</summary>
        private void NextTune(SoundDef def, bool play)
        {
            int n = def.Clips.Length;
            _tune = _tune < 0 ? Random.Range(0, n) : (_tune + 1) % n;
            _source.clip = def.Clips[_tune];
            _source.loop = true; // one tune alone loops; with several Update moves on when it comes round
            _lastTime = 0f;
            if (!play) return;
            _source.Play();
            TunesStarted++;
            ZdLog.Info("Audio", $"bard next {_source.clip.name}");
        }
    }
}
