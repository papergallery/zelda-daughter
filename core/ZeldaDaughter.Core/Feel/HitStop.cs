#nullable enable
namespace ZeldaDaughter.Core.Feel
{
    /// <summary>
    /// The freeze of a blow (D-26). Counts down in real time; while <see cref="Active"/> the views hold the poses and positions of the two
    /// who met (not <c>Time.timeScale</c>: the core keeps ticking). A longer request replaces a shorter one, never the other way.
    /// </summary>
    public sealed class HitStop
    {
        public float Remaining { get; private set; }
        public bool Active => Remaining > 0f;

        public void Request(float seconds) { if (seconds > Remaining) Remaining = seconds; }

        public void Tick(float realDt)
        {
            if (Remaining <= 0f) return;
            Remaining -= realDt;
            if (Remaining < 0f) Remaining = 0f;
        }
    }
}
