#nullable enable
namespace ZeldaDaughter.Core.Combat
{
    /// <summary>
    /// The tap buffer (D-26, docs/demo/best-practices-feel.md): a tap on an enemy in the last <c>window</c> seconds of the cooldown is not lost —
    /// it is kept and the blow goes the moment the cooldown ends. A tap earlier than that is dropped (no queue of blows); the last tap wins.
    /// </summary>
    public sealed class StrikeBuffer
    {
        readonly float _window;
        string? _id;
        float _age;

        public StrikeBuffer(float windowSeconds) { _window = windowSeconds; }

        public bool HasPending => _id != null;

        /// <summary>A tap on <paramref name="enemyId"/> while the hero's cooldown has <paramref name="cooldownLeft"/> seconds to go. True — kept.</summary>
        public bool Offer(string enemyId, float cooldownLeft)
        {
            if (cooldownLeft > _window) return false;
            _id = enemyId; _age = 0f;
            return true;
        }

        /// <summary>Advance by real time. Returns the enemy to strike now (the cooldown is over), else null; a stale tap is forgotten.</summary>
        public string? Tick(float dt, float cooldownLeft)
        {
            if (_id == null) return null;
            _age += dt;
            if (cooldownLeft <= 0f) { var id = _id; _id = null; return id; }
            if (_age > _window) _id = null;
            return null;
        }

        public void Clear() { _id = null; _age = 0f; }
    }
}
