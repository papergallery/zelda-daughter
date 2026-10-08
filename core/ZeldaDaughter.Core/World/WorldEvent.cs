#nullable enable
using ZeldaDaughter.Core.Common;

namespace ZeldaDaughter.Core.World
{
    public enum WorldEventKind
    {
        CampfireBurntOut,
        RainStarted, RainStopped,
        WindChanged,
        GrassIgnited, GrassBurnedOut, GrassExtinguished, GrassWetted, GrassDried,
        HeroScorched,
        PredatorSpawned, PredatorDespawned,
    }

    /// <summary>Something happened in the world during <c>GameState.TickWorld</c> — for the view (particles, light, sounds, spawning an enemy).</summary>
    public readonly struct WorldEvent
    {
        public readonly WorldEventKind Kind;
        /// <summary>Campfire / grass cell / predator id; empty for weather.</summary>
        public readonly string Id;
        /// <summary>PredatorSpawned — the enemy definition id (enemies.json).</summary>
        public readonly string Detail;
        public readonly Vec2 Position;

        public WorldEvent(WorldEventKind kind, string id = "", Vec2 position = default, string detail = "")
        {
            Kind = kind; Id = id; Position = position; Detail = detail;
        }
        public override string ToString() => $"{Kind} {Id}".TrimEnd();
    }
}
