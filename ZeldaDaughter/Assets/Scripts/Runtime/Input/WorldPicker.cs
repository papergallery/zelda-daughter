using UnityEngine;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;

namespace ZeldaDaughter.Input
{
    /// <summary>
    /// What is under a finger (docs/demo/unity-architecture.md §3), the one place that decides:
    /// 1) the hero's own touch circle → <c>TouchHit.Hero</c>;
    /// 2) tappable things whose tap target (radius in pixels × screen density) holds the point — the nearest by (priority, distance):
    ///    enemy, carcass, NPC, pickup, placed/campfire, station/bed;
    /// 3) a ray: the first collider with a <see cref="Tappable"/> on it or above it;
    /// 4) otherwise the ground.
    /// </summary>
    public sealed class WorldPicker : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private HeroController _hero;
        [SerializeField] private WorldIndex _index;
        [SerializeField] private float _rayLength = 500f;

        public void Configure(Camera cam, HeroController hero, WorldIndex index)
        {
            _camera = cam;
            _hero = hero;
            _index = index;
        }

        public TouchHit Pick(Vec2 screen)
        {
            if (_hero.IsOnHero(screen)) return TouchHit.Hero;

            var target = NearestTarget(screen);
            if (target != null) return TouchHit.Object(target.Id);

            if (Physics.Raycast(_camera.ScreenPointToRay(new Vector3(screen.X, screen.Y)), out var hit, _rayLength))
            {
                var t = hit.collider.GetComponentInParent<Tappable>();
                if (t != null && t.Enabled) return TouchHit.Object(t.Id);
            }
            return TouchHit.Ground;
        }

        /// <summary>The tappable whose tap target holds the point, best by (priority, distance); null if none.</summary>
        public Tappable NearestTarget(Vec2 screen)
        {
            var list = _index.Tappables;
            float scale = _hero.DpiScale;
            Tappable best = null;
            int bestPriority = int.MaxValue;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.Enabled || t.ScreenRadiusPx <= 0f || !t.gameObject.activeInHierarchy) continue;
                var p = _camera.WorldToScreenPoint(t.AimPoint);
                if (p.z <= 0f) continue; // behind the camera
                float dx = p.x - screen.X, dy = p.y - screen.Y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > t.ScreenRadiusPx * scale) continue;
                int priority = t.Priority;
                if (priority < bestPriority || (priority == bestPriority && d < bestDistance))
                {
                    best = t;
                    bestPriority = priority;
                    bestDistance = d;
                }
            }
            return best;
        }
    }
}
