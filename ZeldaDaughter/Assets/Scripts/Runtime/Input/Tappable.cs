using UnityEngine;

namespace ZeldaDaughter.Input
{
    /// <summary>What a tap on the object does (docs/demo/unity-architecture.md §3). Scenery — nothing, but the touch still names it.</summary>
    public enum TapKind { Scenery, Enemy, Carcass, Npc, Pickup, Placed, Campfire, Station, Bed }

    /// <summary>
    /// A thing the player can tap: id, kind, how big its tap target is on screen. Every SceneTags object gets one from SceneBuilder
    /// (Scenery by default); presenters add them for things created in play (enemies, carcasses, placed items) and register them with
    /// <see cref="ZeldaDaughter.Game.WorldIndex.RegisterDynamic"/>.
    /// </summary>
    public sealed class Tappable : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private TapKind _kind = TapKind.Scenery;
        [SerializeField] private float _screenRadiusPx;
        [SerializeField] private float _aimHeight = 0.5f;
        [SerializeField] private bool _enabled = true;

        public string Id => _id;
        public TapKind Kind => _kind;
        /// <summary>Tap target radius in pixels at the reference density (data/input.json referenceDpi); the picker scales it by the device's.</summary>
        public float ScreenRadiusPx => _screenRadiusPx;
        /// <summary>False while the thing cannot be tapped (picked up, hidden, asleep).</summary>
        public bool Enabled { get => _enabled; set => _enabled = value; }

        /// <summary>The point the tap target is centred on: a hand above the object's base.</summary>
        public Vector3 AimPoint => transform.position + Vector3.up * _aimHeight;

        /// <summary>Picker priority: smaller wins when targets overlap (§3: enemy, carcass, NPC, pickup, placed/campfire, station/bed).</summary>
        public int Priority
        {
            get
            {
                switch (_kind)
                {
                    case TapKind.Enemy: return 0;
                    case TapKind.Carcass: return 1;
                    case TapKind.Npc: return 2;
                    case TapKind.Pickup: return 3;
                    case TapKind.Placed:
                    case TapKind.Campfire: return 4;
                    case TapKind.Station:
                    case TapKind.Bed: return 5;
                    default: return int.MaxValue;
                }
            }
        }

        /// <summary>Default tap-target radius by kind, in reference pixels (a finger tip is ~9 mm ≈ 55 px at 160 dpi; targets are made a little larger).</summary>
        public static float DefaultRadius(TapKind kind)
        {
            switch (kind)
            {
                case TapKind.Enemy: return 110f;
                case TapKind.Carcass: return 90f;
                case TapKind.Npc: return 100f;
                case TapKind.Pickup: return 80f;
                case TapKind.Placed:
                case TapKind.Campfire: return 90f;
                case TapKind.Station:
                case TapKind.Bed: return 90f;
                default: return 0f;
            }
        }

        public void Configure(string id, TapKind kind, float screenRadiusPx = -1f, float aimHeight = 0.5f)
        {
            _id = id;
            _kind = kind;
            _screenRadiusPx = screenRadiusPx >= 0f ? screenRadiusPx : DefaultRadius(kind);
            _aimHeight = aimHeight;
        }
    }
}
