using UnityEngine;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Hero
{
    /// <summary>
    /// D-16: while the hero carries a burning torch (<c>g.Bag.Count("torch") &gt; 0</c> — the core has no «in hand», the bag is the hand) she is a
    /// light: a flickering warm point light on her and a small flame at her side. It reads the bag on <c>StateReady</c> and on every
    /// <c>BagChanged</c>; a torch that is lit by the game itself, picked up, traded or dropped is followed without a special case.
    /// The range is a share of the campfire's light radius (data/camp.json), so a torch lights less than a fire.
    /// </summary>
    public sealed class HeroTorchLight : MonoBehaviour
    {
        public const string TorchItem = "torch";
        private const float RangeShare = 0.75f;
        private const float BaseIntensity = 2.4f;

        [SerializeField] private GameSession _session;
        [SerializeField] private Transform _hero;
        [SerializeField] private FxRegistry _fx;
        [SerializeField] private Material _flameMat;

        private GameState _g;
        private Light _light;
        private GameObject _flame;
        private bool _on;
        private float _phase;

        /// <summary>The torch burns on her now.</summary>
        public bool IsOn => _on;
        /// <summary>1 while the torch burns fully, falling to 0 over its last seconds (the core's <c>g.Torch.Light</c>, D-23).</summary>
        public float Fade => _g != null && _on ? _g.Torch.Light : 0f;
        public float Intensity => _light != null && _light.enabled ? _light.intensity : 0f;
        public float Range => _light != null ? _light.range : 0f;
        public Light Light => _light;

        public void Configure(GameSession session, Transform hero, FxRegistry fx, Material flameMat)
        {
            _session = session;
            _hero = hero;
            _fx = fx;
            _flameMat = flameMat;
        }

        private void Start()
        {
            var lightGo = new GameObject("TorchLight");
            lightGo.transform.SetParent(_hero, false);
            lightGo.transform.localPosition = new Vector3(0.35f, 0.4f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.64f, 0.32f);
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;

            var prefab = _fx != null ? _fx.Get("torch_flame") : null;
            if (prefab != null) _flame = Instantiate(prefab, lightGo.transform);
            else
            {
                _flame = new GameObject("TorchFlame");
                _flame.transform.SetParent(lightGo.transform, false);
                FireParticles.Flame(_flame.transform, _flameMat, 0.35f);
            }
            _flame.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            _flame.SetActive(false);

            _session.Events.StateReady += OnStateReady;
            _session.Events.BagChanged += OnBagChanged;
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.Events.StateReady -= OnStateReady;
            _session.Events.BagChanged -= OnBagChanged;
        }

        private void OnStateReady(GameState g)
        {
            _g = g;
            Refresh();
        }

        private void OnBagChanged(string why) => Refresh();

        private void Refresh()
        {
            if (_g == null || _light == null) return;
            bool on = _g.Bag.Count(TorchItem) > 0;
            if (on == _on) return;
            _on = on;
            _light.range = _g.Data.Camp.LightRadius * RangeShare;
            _light.enabled = on;
            _flame.SetActive(on);
            if (on) foreach (var ps in _flame.GetComponentsInChildren<ParticleSystem>(true)) { ps.Clear(); ps.Play(); }
            ZdLog.Info("Nature", on ? "torch_on" : "torch_off");
        }

        private void Update()
        {
            if (!_on) return;
            float t = Time.time + _phase;
            float fade = _g.Torch.Light;                                        // D-23: the torch dies — the light weakens, shrinks and gutters
            float gutter = 1f + (1f - fade) * 0.5f * Mathf.Sin(t * 47f);
            _light.intensity = BaseIntensity * fade * gutter * (0.86f + 0.14f * Mathf.Sin(t * 12f) + 0.08f * Mathf.Sin(t * 31f));
            _light.range = _g.Data.Camp.LightRadius * RangeShare * Mathf.Lerp(0.5f, 1f, fade);
            _flame.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, fade);
        }
    }
}
