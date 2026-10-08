using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// A black sheet over the whole screen (the Overlay layer): the dark of a knockout and the dim of sleep (docs/demo/unity-architecture.md §1).
    /// W0 gives the API; D-11 gives the knockout its look (darkness with glimpses), D-15 uses <see cref="Sleep"/>. It never takes a touch.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private SessionUI _ui;
        private Image _sheet;
        private Coroutine _running;

        // D-11 knockout look (project-design §6): darkness at once-ish, one to three glimpses, the eyes open at the end.
        public const float KnockoutDarkAlpha = 0.97f, KnockoutGlimpseAlpha = 0.5f;
        const float KnockoutInSeconds = 0.5f, KnockoutOutSeconds = 0.55f, GlimpseSeconds = 0.6f;
        private GameState _g;
        private SessionEvents _events;
        private bool _knockedOut, _coreDown;

        public void Configure(SessionUI ui) => _ui = ui;

        /// <summary>The hero is knocked out now: the screen is dark (glimpses come and go) until she gets up.</summary>
        public bool InKnockout => _knockedOut;

        /// <summary>Glimpses seen in the current/last knockout (1–3; longer knockouts show more).</summary>
        public int Glimpses { get; private set; }

        /// <summary>D-11: listens to the hero's knockout on the session bus. Called by the scene builder once.</summary>
        public void Bind(SessionEvents events)
        {
            if (_events != null) return;
            _events = events;
            events.StateReady += OnStateReady;
            events.Condition += OnCondition;
            events.Enemy += OnEnemy;
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.StateReady -= OnStateReady;
            _events.Condition -= OnCondition;
            _events.Enemy -= OnEnemy;
        }

        private void OnStateReady(GameState g)
        {
            _g = g;
            _coreDown = g.Condition.IsKnockedOut;
            if (_coreDown) KnockoutBegin(g.Condition.KnockoutLeft);
        }

        private void OnCondition(ConditionEvent e)
        {
            if (e.Kind == ConditionEventKind.KnockedOut) KnockoutBegin(_g != null ? _g.Condition.KnockoutLeft : 5f);
            else if (e.Kind == ConditionEventKind.Revived) KnockoutEnd();
        }

        private void OnEnemy(EnemyNotice n)
        {
            if (n.Event.Kind == EnemyEventKind.HeroKnockedOut) KnockoutBegin(_g != null ? _g.Condition.KnockoutLeft : 5f);
        }

        private void Update()
        {
            if (_g == null || _g.Condition.IsKnockedOut == _coreDown) return; // the state changed without an event (a load, a wound)
            _coreDown = _g.Condition.IsKnockedOut;
            if (_coreDown) KnockoutBegin(_g.Condition.KnockoutLeft); else KnockoutEnd();
        }

        /// <summary>The dark of a knockout lasting about <paramref name="seconds"/>: fades in, 1–3 glimpses of the world, then holds dark until <see cref="KnockoutEnd"/>.</summary>
        public void KnockoutBegin(float seconds)
        {
            if (_knockedOut) return;
            Ensure();
            Stop();
            _knockedOut = true;
            Glimpses = Mathf.Clamp(Mathf.RoundToInt(seconds / 2.5f), 1, 3);
            _running = StartCoroutine(Wrap(KnockoutRoutine(seconds, Glimpses)));
            ZdLog.Info("Fader", $"knockout dark glimpses={Glimpses}");
        }

        /// <summary>She gets up: the eyes open, the dark goes.</summary>
        public void KnockoutEnd()
        {
            if (!_knockedOut) return;
            Ensure();
            Stop();
            _knockedOut = false;
            _running = StartCoroutine(Wrap(Fade(0f, KnockoutOutSeconds)));
            ZdLog.Info("Fader", "knockout clear");
        }

        private IEnumerator KnockoutRoutine(float seconds, int glimpses)
        {
            yield return Fade(KnockoutDarkAlpha, KnockoutInSeconds);
            float spacing = Mathf.Max(GlimpseSeconds + 0.3f, seconds / (glimpses + 1)), t = KnockoutInSeconds;
            for (int i = 0; i < glimpses; i++)
            {
                float wait = spacing * (i + 1) - t;
                if (wait > 0f) { yield return new WaitForSeconds(wait); t += wait; }
                yield return Fade(KnockoutGlimpseAlpha, GlimpseSeconds * 0.35f);
                yield return new WaitForSeconds(GlimpseSeconds * 0.25f);
                yield return Fade(KnockoutDarkAlpha, GlimpseSeconds * 0.4f);
                t += GlimpseSeconds;
            }
            // dark until she stands (KnockoutEnd stops this routine)
            Paint(KnockoutDarkAlpha);
            while (true) yield return null;
        }

        /// <summary>0 clear … 1 black.</summary>
        public float Alpha { get { Ensure(); return _sheet.color.a; } }

        public bool Busy => _running != null;

        /// <summary>Alpha at once.</summary>
        public void SetAlpha(float alpha)
        {
            Ensure();
            Stop();
            Paint(alpha);
        }

        /// <summary>Alpha moves to the target over the seconds.</summary>
        public void FadeTo(float alpha, float seconds)
        {
            Ensure();
            Stop();
            if (seconds <= 0f) { Paint(alpha); return; }
            _running = StartCoroutine(Wrap(Fade(alpha, seconds)));
        }

        /// <summary>
        /// Sleep: fade to black, run <paramref name="atDark"/> (the time jump), hold, fade back. <paramref name="done"/> is called when the screen is clear again.
        /// </summary>
        public void Sleep(Action atDark, Action done = null, float outSeconds = 0.8f, float holdSeconds = 0.3f, float inSeconds = 0.8f)
        {
            Ensure();
            Stop();
            _running = StartCoroutine(SleepRoutine(atDark, done, outSeconds, holdSeconds, inSeconds));
        }

        private IEnumerator SleepRoutine(Action atDark, Action done, float outS, float holdS, float inS)
        {
            yield return Fade(1f, outS);
            atDark?.Invoke();
            if (holdS > 0f) yield return new WaitForSeconds(holdS);
            yield return Fade(0f, inS);
            _running = null;
            done?.Invoke();
        }

        private IEnumerator Wrap(IEnumerator inner)
        {
            yield return inner;
            _running = null;
        }

        private IEnumerator Fade(float to, float seconds)
        {
            float from = _sheet.color.a, t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                Paint(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            Paint(to);
        }

        private void Stop()
        {
            if (_running != null) StopCoroutine(_running);
            _running = null;
        }

        private void Paint(float alpha)
        {
            var c = _sheet.color;
            c.a = Mathf.Clamp01(alpha);
            _sheet.color = c;
            _sheet.enabled = c.a > 0.001f;
        }

        private void Ensure()
        {
            if (_sheet != null) return;
            var rt = UiKit.MakeRect(_ui.Overlay, "Fader");
            UiKit.Stretch(rt);
            _sheet = rt.gameObject.AddComponent<Image>();
            _sheet.color = new Color(0f, 0f, 0f, 0f);
            _sheet.raycastTarget = false;
            _sheet.enabled = false;
        }
    }
}
