using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Game
{
    /// <summary>
    /// Named streams of random numbers derived from the session seed (docs/demo/unity-architecture.md §8 «Детерминизм»): the world, the
    /// fight, enemies, talk, remarks and effects each draw from their own, so adding a roll in one never shifts another.
    /// <c>UnityEngine.Random</c> is not used anywhere in the game.
    /// </summary>
    public sealed class RollStreams
    {
        public Stream World { get; }
        public Stream Combat { get; }
        public Stream Enemies { get; }
        public Stream Talk { get; }
        public Stream Remarks { get; }
        /// <summary>Visual effects only: never feeds the rules, so a different frame rate may draw a different count.</summary>
        public Stream Fx { get; }

        public RollStreams(int seed)
        {
            World = new Stream(seed, 1);
            Combat = new Stream(seed, 2);
            Enemies = new Stream(seed, 3);
            Talk = new Stream(seed, 4);
            Remarks = new Stream(seed, 5);
            Fx = new Stream(seed, 6);
        }

        /// <summary>One stream: the core's SplitMix64 (same numbers on every machine), seeded from the session seed and the stream's own salt.</summary>
        public sealed class Stream
        {
            private readonly SplitMix64 _rng;

            public Stream(int seed, int salt) { _rng = new SplitMix64(unchecked(seed * 31 + salt * 7919)); }

            /// <summary>0 ≤ x &lt; 1.</summary>
            public double Next() => _rng.NextDouble();

            /// <summary>0 ≤ x &lt; n (n ≥ 1; smaller n gives 0).</summary>
            public int Next(int n) => n <= 1 ? 0 : _rng.NextInt(n);

            public float NextFloat() => _rng.Float();
        }
    }
}
