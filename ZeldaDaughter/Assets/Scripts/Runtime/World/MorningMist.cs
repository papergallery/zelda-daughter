using UnityEngine;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// D-22: the morning mist over the meadow at the spawn (concept f1). A soft flat disc (an object of the scene config tagged <c>mist</c>, material
    /// nature_mist) that is there in the morning and thins out as the day grows: <see cref="Strength"/> of the time of day (0..1 from midnight, the clock of
    /// data/world.json: dawn from 0.2, day from 0.3, the game starts at 0.35). Drifts slowly. The look, not the balance: the hours are here, as the colours are in
    /// <see cref="SunController"/>. Outside Play Mode (frames from the editor) the material's own alpha shows — the morning.
    /// </summary>
    public sealed class MorningMist : MonoBehaviour
    {
        const float FullFrom = 0.3f, FullUntil = 0.4f, GoneAt = 0.52f, DawnFrom = 0.2f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private GameSession _session;
        [SerializeField] private Renderer _renderer;
        [SerializeField] private Color _color = new Color(0.95f, 0.92f, 0.85f, 0.4f);
        [SerializeField] private float _phase;

        private MaterialPropertyBlock _block;
        private Vector3 _home;

        /// <summary>0 at night, rising through the dawn, 1 in the early morning, thinning to 0 by about 12:30 (t = 0.52).</summary>
        public static float Strength(double timeOfDay)
        {
            float t = (float)timeOfDay;
            if (t <= DawnFrom || t >= GoneAt) return 0f;
            if (t < FullFrom) return Mathf.SmoothStep(0f, 1f, (t - DawnFrom) / (FullFrom - DawnFrom));
            if (t <= FullUntil) return 1f;
            return Mathf.SmoothStep(1f, 0f, (t - FullUntil) / (GoneAt - FullUntil));
        }

        public void Configure(GameSession session, Renderer renderer, Color color, float phase)
        {
            _session = session;
            _renderer = renderer;
            _color = color;
            _phase = phase;
        }

        private void Start()
        {
            _block = new MaterialPropertyBlock();
            _home = transform.localPosition;
        }

        private void Update()
        {
            if (_renderer == null) return;
            float k = _session != null && _session.State != null ? Strength(_session.State.Clock.TimeOfDay) : 1f;
            _renderer.enabled = k > 0.01f;
            if (k <= 0.01f) return;
            float t = Time.time * 0.06f + _phase;
            transform.localPosition = _home + new Vector3(Mathf.Sin(t) * 0.9f, 0f, Mathf.Cos(t * 0.8f) * 0.6f);
            var c = _color;
            c.a *= k * (0.85f + 0.15f * Mathf.Sin(t * 2.3f));
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, c);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
