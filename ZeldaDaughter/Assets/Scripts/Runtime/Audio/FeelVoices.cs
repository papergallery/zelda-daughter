using UnityEngine;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Feel;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.Audio
{
    /// <summary>
    /// D-26, the voices that tell what the frame cannot (docs/demo/best-practices-feel.md п. 8, 12): a wolf that is awake and nearer than 15 m growls
    /// from where it is — panned to its side of the screen and louder as it comes (the camera sees about 3.5 m to each side, a wolf runs 4 m/s:
    /// the ear warns first); a badly hurt hero breathes hard (from severity 0.5), a hungry one's stomach growls. These are the second channel of the
    /// state besides her pose and the replies. Plays through <see cref="AudioDirector"/>'s pool; the numbers are data/combat-feel.json.
    /// </summary>
    public sealed class FeelVoices : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        [SerializeField] private AudioDirector _audio;
        [SerializeField] private Camera _camera;

        private GameState _g;
        private FeelSettings _s;
        private readonly System.Collections.Generic.Dictionary<string, float> _nextGrowl = new System.Collections.Generic.Dictionary<string, float>();
        private float _nextBreath = 3f, _nextStomach = 8f;

        /// <summary>The last growl: distance, pan, volume — for the tests and the log.</summary>
        public string LastGrowlWolf { get; private set; }
        public float LastGrowlPan { get; private set; }
        public float LastGrowlVolume { get; private set; }
        public int GrowlCount { get; private set; }
        public int BreathCount { get; private set; }
        public int StomachCount { get; private set; }

        public void Configure(GameSession session, AudioDirector audio, Camera cam)
        {
            _session = session;
            _audio = audio;
            _camera = cam;
        }

        private void OnEnable() => _session.Events.StateReady += OnReady;
        private void OnDisable() { if (_session != null) _session.Events.StateReady -= OnReady; }
        private void OnReady(GameState g) { _g = g; _s = g.Data.Feel; }

        private void Update()
        {
            if (_g == null || _audio == null) return;
            float now = Time.unscaledTime;
            Growls(now);
            Body(now);
        }

        private void Growls(float now)
        {
            var live = _g.Enemies.Active;
            var cam = _camera != null ? _camera : Camera.main;
            for (int i = 0; i < live.Count; i++)
            {
                var e = live[i];
                if (e.DefId != "wolf") continue;
                var st = e.State;
                if (st != EnemyState.Alert && st != EnemyState.Chase && st != EnemyState.Recover && st != EnemyState.Fleeing) continue;   // (the windup has its own growl)
                var off = e.Position - _g.HeroPosition;
                float dist = off.Length;
                if (dist >= _s.Growl.Meters) continue;
                if (_nextGrowl.TryGetValue(e.Id, out var due) && now < due) continue;
                _nextGrowl[e.Id] = now + _s.Growl.EverySeconds * Random.Range(0.8f, 1.3f);

                float dx = off.X;
                if (cam != null)
                {
                    var right = cam.transform.right; right.y = 0f; right.Normalize();
                    dx = off.X * right.x + off.Y * right.z;      // sideways on the screen, right positive
                }
                float pan = _s.Growl.Pan(dx, dist), vol = _s.Growl.Volume(dist);
                var voice = _audio.Play("wolf_growl_near", null, vol);
                if (voice != null) voice.panStereo = pan;
                LastGrowlWolf = e.Id; LastGrowlPan = pan; LastGrowlVolume = vol;
                GrowlCount++;
                ZdLog.Info("Audio", $"wolf_growl {e.Id} dist={dist:0.0} pan={pan:0.00} vol={vol:0.00}{(voice == null ? " (silent)" : "")}");
            }
        }

        private void Body(float now)
        {
            if (_g.Condition.IsKnockedOut) return;
            float severity = _s.Condition.Severity(_g.Condition, _g.Data.Wounds);
            if (_s.Condition.Breathes(severity) && now >= _nextBreath)
            {
                _nextBreath = now + Mathf.Lerp(9f, 5f, Mathf.Clamp01(severity)) * Random.Range(0.85f, 1.2f);
                var v = _audio.Play("breath_hurt", null, Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(severity)));
                BreathCount++;
                ZdLog.Info("Audio", $"breath severity={severity:0.00}{(v == null ? " (silent)" : "")}");
            }
            if (_s.Condition.StomachGrowls(_g.Hunger.Level) && now >= _nextStomach)
            {
                _nextStomach = now + _s.Condition.StomachEverySeconds * Random.Range(0.8f, 1.3f);
                var v = _audio.Play("stomach");
                StomachCount++;
                ZdLog.Info("Audio", $"stomach hunger={_g.Hunger.Level}{(v == null ? " (silent)" : "")}");
            }
        }
    }
}
