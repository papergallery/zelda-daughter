#nullable enable
using System;

namespace ZeldaDaughter.Core.Feel
{
    /// <summary>
    /// Camera shake by trauma (Eiserloh, GDC 2016): events add trauma 0..1, it falls linearly, the shift is trauma² × max along smooth noise
    /// (not a random jump per frame). <see cref="Enabled"/> = the «no shake» setting.
    /// </summary>
    public sealed class Trauma
    {
        readonly ShakeSettings _s;

        public Trauma(ShakeSettings settings) { _s = settings ?? throw new ArgumentNullException(nameof(settings)); }

        public bool Enabled { get; set; } = true;
        public float Value { get; private set; }

        /// <summary>Largest shift now, metres.</summary>
        public float Amplitude => Enabled ? Value * Value * _s.MaxMeters : 0f;

        public void Add(float amount)
        {
            if (!Enabled || amount <= 0f) return;
            Value = Math.Min(1f, Value + amount);
        }

        public void Tick(float realDt)
        {
            if (Value <= 0f) return;
            Value -= _s.DecaySeconds > 0 ? realDt / _s.DecaySeconds : 1f;
            if (Value < 0f) Value = 0f;
        }

        public void Clear() => Value = 0f;

        /// <summary>The shift of the camera at <paramref name="time"/> (seconds, any origin), metres on the two screen axes.</summary>
        public void Offset(double time, out float x, out float y)
        {
            float a = Amplitude;
            if (a <= 0f) { x = 0f; y = 0f; return; }
            x = a * Noise(time * _s.NoiseHz, 0x9E3779B1u);
            y = a * Noise(time * _s.NoiseHz, 0x85EBCA77u);
        }

        /// <summary>Smooth value noise, -1..1: random values at whole numbers, smoothstep between them.</summary>
        static float Noise(double x, uint seed)
        {
            double fl = Math.Floor(x);
            int i = (int)fl;
            float f = (float)(x - fl);
            f = f * f * (3f - 2f * f);
            float a = At(i, seed), b = At(i + 1, seed);
            return a + (b - a) * f;
        }

        static float At(int i, uint seed)
        {
            unchecked
            {
                uint h = (uint)i * 0x27D4EB2Fu ^ seed;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFu) / 32767.5f - 1f;
            }
        }
    }
}
