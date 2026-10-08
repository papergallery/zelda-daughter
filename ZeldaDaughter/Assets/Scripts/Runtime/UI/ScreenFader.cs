using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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

        public void Configure(SessionUI ui) => _ui = ui;

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
