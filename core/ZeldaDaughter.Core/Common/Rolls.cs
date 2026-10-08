#nullable enable
using System;

namespace ZeldaDaughter.Core.Common
{
    /// <summary>
    /// Derived rolls: the view hands the core ONE random number per step (0..1); rules that need several independent ones take
    /// <c>At(roll, a, b)</c> — a stable hash of the roll and two integers (cell numbers, salts) in [0; 1). Same inputs, same answer (ADR-0008).
    /// </summary>
    public static class Rolls
    {
        public static double At(double roll, int a, int b = 0)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                h = (h ^ (ulong)BitConverter.DoubleToInt64Bits(roll)) * 1099511628211UL;
                h = (h ^ (uint)a) * 1099511628211UL;
                h = (h ^ (uint)b) * 1099511628211UL;
                h ^= h >> 33; h *= 0xff51afd7ed558ccdUL;
                h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL;
                h ^= h >> 33;
                return (h >> 11) * (1.0 / 9007199254740992.0);
            }
        }
    }
}
