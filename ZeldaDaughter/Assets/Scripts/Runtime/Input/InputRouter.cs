using UnityEngine;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;

namespace ZeldaDaughter.Input
{
    /// <summary>
    /// A recognised gesture → an action (docs/demo/unity-architecture.md §3): a tap goes to <see cref="GameSession.Tap"/> and from there to the
    /// handler of the target's kind; a swipe or a long press ticks the onboarding hints and, for the long press, tells the radial menu through the
    /// session's events. The session itself does not listen to gestures.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        [SerializeField] private HeroController _hero;
        [SerializeField] private GameSession _session;
        private GameState _g;

        public void Configure(HeroController hero, GameSession session)
        {
            _hero = hero;
            _session = session;
        }

        private void OnEnable()
        {
            _hero.Gesture += OnGesture;
            _session.Events.StateReady += OnReady;
        }

        private void OnDisable()
        {
            if (_hero != null) _hero.Gesture -= OnGesture;
            if (_session != null) _session.Events.StateReady -= OnReady;
            _g = null;
        }

        private void OnReady(GameState g) => _g = g;

        private void OnGesture(GestureEvent e)
        {
            switch (e.Kind)
            {
                case GestureKind.SwipeStarted:
                    _g?.Hints.Did("swipe");
                    break;
                case GestureKind.LongPressOnHero:
                    _g?.Hints.Did("long_press_hero");
                    _session.Events.RaiseLongPressHero();
                    break;
                case GestureKind.LongPressReleased:
                    _session.Events.RaiseLongPressReleased();
                    break;
                case GestureKind.Tap:
                    _session.Tap(e.TargetId);
                    break;
            }
        }
    }
}
