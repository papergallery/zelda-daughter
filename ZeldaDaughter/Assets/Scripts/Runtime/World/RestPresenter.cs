using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Input;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Sleep in the tavern (project-design.md §6, D-15): a tap on a bed → the eyes close (two black lids meet over the picture, the screen goes
    /// dark) → the clock jumps <c>SleepHours</c> (<c>g.Sleep()</c> through <see cref="GameSession.JumpTime"/>: wounds heal to the threshold,
    /// hunger grows, NPCs are put at their new places) → the eyes open. The hero stands still all along. The fade is the session's
    /// <see cref="ScreenFader"/>; the lids follow its darkness.
    /// </summary>
    public sealed class RestPresenter : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        [SerializeField] private ScreenFader _fader;
        [SerializeField] private WindowStack _windows;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private HeroController _hero;
        [SerializeField] private float _closeSeconds = 1.0f;
        [SerializeField] private float _holdSeconds = 0.5f;
        [SerializeField] private float _openSeconds = 1.0f;

        private GameState _g;
        private bool _sleeping;
        private RectTransform _lids;
        private RectTransform _top, _bottom;

        /// <summary>True from the tap until the eyes are open again.</summary>
        public bool Sleeping => _sleeping;
        /// <summary>How many times the hero slept in this session.</summary>
        public int Sleeps { get; private set; }
        /// <summary>How far the lids are closed, 0 open … 1 shut (tests and frames).</summary>
        public float LidsClosed { get; private set; }

        public void Configure(GameSession session, ScreenFader fader, WindowStack windows, SessionUI ui, HeroController hero)
        {
            _session = session;
            _fader = fader;
            _windows = windows;
            _ui = ui;
            _hero = hero;
        }

        /// <summary>Tests: shorter eyes.</summary>
        public void SetTimes(float closeSeconds, float holdSeconds, float openSeconds)
        {
            _closeSeconds = closeSeconds;
            _holdSeconds = holdSeconds;
            _openSeconds = openSeconds;
        }

        private void OnEnable() => _session.Events.StateReady += OnReady;

        private void OnDisable()
        {
            if (_session != null) _session.Events.StateReady -= OnReady;
        }

        private void Start() => _session.OnTap(TapKind.Bed, OnTap);

        private void OnReady(GameState g) => _g = g;

        private void OnTap(Tappable bed)
        {
            if (_g == null || _sleeping || (_windows != null && _windows.AnyOpen)) return;
            if (!ScreenParts.InReach(_session, _g, bed)) { ZdLog.Info("Rest", $"{bed.Id} too_far"); return; }
            if (!_g.NightSeen)
            {
                // D-23: the first night is not to be slept through — she has not yet seen the dark
                ZdLog.Info("Rest", $"{bed.Id} not_sleepy");
                _session.Say(Topics.NotSleepy);
                return;
            }
            Sleep(bed.Id);
        }

        /// <summary>Goes to sleep now (a tap on a bed, or a test). False if she cannot: already asleep, or knocked out.</summary>
        public bool Sleep(string bedId = null)
        {
            if (_g == null || _sleeping || _g.Condition.IsKnockedOut) return false;
            _sleeping = true;
            _windows?.CloseAll();
            _hero.CancelMove();
            _hero.Locked = true;
            _session.RestOverride = RestKind.Tavern;
            EnsureLids();
            _lids.gameObject.SetActive(true);
            ZdLog.Info("Rest", $"sleep begin bed={bedId} day={_g.Clock.Day} t={_g.Clock.TimeOfDay:0.000}");
            _fader.Sleep(AtDark, Wake, _closeSeconds, _holdSeconds, _openSeconds);
            return true;
        }

        private void AtDark()
        {
            double hours = _g.Data.World.SleepHours;
            _session.JumpTime(() => _g.Sleep(), hours);
            ZdLog.Info("Rest", $"slept hours={hours:0.#} day={_g.Clock.Day} t={_g.Clock.TimeOfDay:0.000} hp={_g.Condition.Hp:0.0}");
        }

        private void Wake()
        {
            _sleeping = false;
            Sleeps++;
            _session.RestOverride = RestKind.None;
            _hero.Locked = false;
            Paint(0f);
            if (_lids != null) _lids.gameObject.SetActive(false);
            ZdLog.Info("Rest", $"wake day={_g.Clock.Day} t={_g.Clock.TimeOfDay:0.000}");
        }

        private void Update()
        {
            if (_sleeping && _fader != null) Paint(_fader.Alpha);
        }

        /// <summary>The lids cover half of the screen each from its edge as the dark deepens (smoothly: slow at the start, fast in the middle).</summary>
        private void Paint(float darkness)
        {
            float f = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(darkness));
            LidsClosed = f;
            if (_top == null) return;
            _top.anchorMin = new Vector2(0f, 1f - 0.5f * f);
            _top.anchorMax = Vector2.one;
            _bottom.anchorMin = Vector2.zero;
            _bottom.anchorMax = new Vector2(1f, 0.5f * f);
            _top.offsetMin = _top.offsetMax = _bottom.offsetMin = _bottom.offsetMax = Vector2.zero;
        }

        private void EnsureLids()
        {
            if (_lids != null) return;
            _lids = UiKit.MakeRect(_ui.Overlay, "SleepLids");
            UiKit.Stretch(_lids);
            _top = Lid("Top");
            _bottom = Lid("Bottom");
            _lids.SetAsFirstSibling();
            Paint(0f);
        }

        private RectTransform Lid(string name)
        {
            var rt = UiKit.MakeRect(_lids, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.02f, 0.015f, 0.01f, 1f);
            img.raycastTarget = false;
            return rt;
        }
    }
}
