#nullable enable
using System;

namespace ZeldaDaughter.Core.Common
{
    /// <summary>
    /// SplitMix64: a tiny deterministic random generator with the same sequence on every machine and every runtime (no framework random,
    /// no engine random; ADR-0008). Used by the scene scatterer and by the Unity layer's named roll streams.
    /// </summary>
    public sealed class SplitMix64
    {
        ulong _s;

        public SplitMix64(int seed) { _s = unchecked((ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x1234567UL); }

        /// <summary>The next 64 random bits.</summary>
        public ulong Next()
        {
            unchecked
            {
                _s += 0x9E3779B97F4A7C15UL;
                ulong z = _s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>A float in [0; 1) with 24 random bits.</summary>
        public float Float() => (float)((Next() >> 40) / (double)(1UL << 24));

        /// <summary>A double in [0; 1) with 53 random bits — what <c>GameState.TickWorld(dt, roll)</c> and <c>Enemies.Tick</c> take.</summary>
        public double NextDouble() => (Next() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>An integer in [0; <paramref name="max"/>).</summary>
        public int NextInt(int max)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
            return (int)(Next() % (ulong)max);
        }
    }
}
