using System.Linq;
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>The public SplitMix64 (Core.Common): the same sequence everywhere — the Unity layer's roll streams rest on it.</summary>
    public class SplitMix64Tests
    {
        [Fact]
        public void The_same_seed_gives_the_same_sequence()
        {
            var a = new SplitMix64(42);
            var b = new SplitMix64(42);
            for (int i = 0; i < 100; i++) Assert.Equal(a.Next(), b.Next());
        }

        [Fact]
        public void Different_seeds_give_different_sequences()
        {
            Assert.NotEqual(new SplitMix64(1).Next(), new SplitMix64(2).Next());
        }

        [Fact]
        public void The_sequence_is_pinned()
        {
            // pinned (computed independently in Python): a change would reshuffle every scatter and every world stream
            var r = new SplitMix64(0);
            Assert.Equal(0x3a34ce6380fc0bc5UL, r.Next());
            Assert.Equal(0xc05a677850dc981aUL, r.Next());
            Assert.Equal(0x9e32cdf7948370bdUL, r.Next());
            var q = new SplitMix64(42);
            Assert.Equal(0x9814cde8cb99c6a2UL, q.Next());
            Assert.Equal(0x49fbb7b0c24a1146UL, q.Next());
        }

        [Fact]
        public void Floats_doubles_and_ints_stay_in_range()
        {
            var r = new SplitMix64(7);
            for (int i = 0; i < 5000; i++)
            {
                Assert.InRange(r.Float(), 0f, 0.99999994f);
                Assert.InRange(r.NextDouble(), 0.0, 0.9999999999999999);
                Assert.InRange(r.NextInt(5), 0, 4);
            }
            Assert.Throws<System.ArgumentOutOfRangeException>(() => r.NextInt(0));
        }

        [Fact]
        public void Doubles_cover_the_range_evenly_enough()
        {
            var r = new SplitMix64(99);
            int low = 0;
            for (int i = 0; i < 4000; i++) if (r.NextDouble() < 0.5) low++;
            Assert.InRange(low, 1800, 2200);
        }
    }
}
