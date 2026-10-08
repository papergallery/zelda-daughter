using UnityEngine;
using ZeldaDaughter.Game;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// D-22: the morning mist over the meadow (concept f1). D-22b: drawn by the watercolour post pass (low over the ground, in drifting patches of
    /// world noise — Hidden/Zelda/Watercolor, WatercolorSettings.mistColor) instead of a stack of flat discs (overdraw, research §В.6): this sets
    /// its strength, the global <c>_ZD_Mist</c>, from the time of day — <see cref="Strength"/> (0..1 from midnight, the clock of data/world.json:
    /// dawn from 0.2, day from 0.3, the game starts at 0.35). The look, not the balance: the hours are here, as the colours are in <see cref="SunController"/>.
    /// Outside Play Mode the global is left as it is (a frame from the editor sets it itself).
    /// </summary>
    public sealed class MorningMist : MonoBehaviour
    {
        const float FullFrom = 0.3f, FullUntil = 0.4f, GoneAt = 0.52f, DawnFrom = 0.2f;

        public static readonly int MistId = Shader.PropertyToID("_ZD_Mist");

        [SerializeField] private GameSession _session;

        /// <summary>The strength set last (tests).</summary>
        public float Current { get; private set; }

        /// <summary>0 at night, rising through the dawn, 1 in the early morning, thinning to 0 by about 12:30 (t = 0.52).</summary>
        public static float Strength(double timeOfDay)
        {
            float t = (float)timeOfDay;
            if (t <= DawnFrom || t >= GoneAt) return 0f;
            if (t < FullFrom) return Mathf.SmoothStep(0f, 1f, (t - DawnFrom) / (FullFrom - DawnFrom));
            if (t <= FullUntil) return 1f;
            return Mathf.SmoothStep(1f, 0f, (t - FullUntil) / (GoneAt - FullUntil));
        }

        public void Configure(GameSession session) => _session = session;

        private void Update()
        {
            if (_session == null || _session.State == null) return;
            float k = Strength(_session.State.Clock.TimeOfDay);
            if (Mathf.Abs(k - Current) < 0.002f && k != 0f) return;
            Current = k;
            Shader.SetGlobalFloat(MistId, k);
        }

        private void OnDisable() => Shader.SetGlobalFloat(MistId, 0f);
    }
}
