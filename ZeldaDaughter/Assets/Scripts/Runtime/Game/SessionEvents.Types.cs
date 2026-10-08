using UnityEngine;

namespace ZeldaDaughter.Game
{
    public enum HeroActKind { Strike, Pickup, Eat, Treat, Butcher, Place, Craft }

    /// <summary>The hero's hands are busy with something: toward — the world point she faces (zero vector: nowhere in particular), item — the item id or null.</summary>
    public readonly struct HeroAct
    {
        public readonly HeroActKind Kind;
        public readonly Vector3 Toward;
        public readonly string Item;

        public HeroAct(HeroActKind kind, Vector3 toward = default, string item = null) { Kind = kind; Toward = toward; Item = item; }
        public override string ToString() => Item == null ? Kind.ToString() : Kind + " " + Item;
    }
}
