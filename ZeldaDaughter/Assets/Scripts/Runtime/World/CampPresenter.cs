using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// What the hero left in the world (docs/demo/unity-architecture.md §1, project-design.md §6–7): every thing in <c>g.Camp.Objects</c> lies on the ground as its
    /// model, every <c>g.Camp.Campfires</c> burns — the campfire model, the fire (the <c>campfire</c> effect of FxRegistry, a flame primitive without it) and a warm
    /// light that fades with <c>Campfire.Light</c> and flickers. The views follow the core: they are reconciled when something is placed, used, burnt out, picked up, after
    /// a save loads and after time jumps, so a view never outlives its object. A tap on a placed thing takes it back into the bag (<c>g.Camp.PickUp</c>).
    /// </summary>
    public sealed class CampPresenter : MonoBehaviour
    {
        private const float LightIntensity = 3f;
        private const float FlameScale = 2f;
        private static readonly Color FireColor = new Color(1f, 0.62f, 0.3f);

        [SerializeField] private GameSession _session;
        [SerializeField] private ArtAssets _art;
        [SerializeField] private string[] _itemIds = new string[0];
        [SerializeField] private GameObject[] _itemModels = new GameObject[0];
        [SerializeField] private float[] _itemScales = new float[0];
        [SerializeField] private GameObject _campfireModel;
        [SerializeField] private Material _thingMaterial;
        [SerializeField] private Material _flameMaterial;

        private sealed class View
        {
            public string Id;
            public bool Fire;
            public GameObject Go;
            public Tappable Tap;
            public Light Light;
            public Campfire Campfire;
            public Transform Flame;
            public GameObject Fx;
            public float Phase;
        }

        private GameState _g;
        private readonly Dictionary<string, View> _views = new Dictionary<string, View>();
        private readonly List<View> _fires = new List<View>();
        private readonly HashSet<string> _alive = new HashSet<string>();
        private readonly List<string> _stale = new List<string>();
        private Transform _holder;
        private int _groundMask;

        public void Configure(GameSession session, ArtAssets art, string[] itemIds, GameObject[] itemModels, float[] itemScales,
            GameObject campfireModel, Material thingMaterial, Material flameMaterial)
        {
            _session = session;
            _art = art;
            _itemIds = itemIds;
            _itemModels = itemModels;
            _itemScales = itemScales;
            _campfireModel = campfireModel;
            _thingMaterial = thingMaterial;
            _flameMaterial = flameMaterial;
        }

        public int ViewCount => _views.Count;
        public bool HasView(string id) => _views.ContainsKey(id);
        public bool IsFire(string id) => _views.TryGetValue(id, out var v) && v.Fire;
        public GameObject ViewOf(string id) => _views.TryGetValue(id, out var v) ? v.Go : null;
        /// <summary>The intensity of the campfire's light now (0 when there is no such fire).</summary>
        public float LightOf(string id) => _views.TryGetValue(id, out var v) && v.Light != null ? v.Light.intensity : 0f;
        public Light LightComponentOf(string id) => _views.TryGetValue(id, out var v) ? v.Light : null;

        private void OnEnable()
        {
            var e = _session.Events;
            e.StateReady += OnReady;
            e.Placed += OnPlaced;
            e.UsedOnWorld += OnUsed;
            e.World += OnWorld;
            e.TimeJumped += OnJumped;
        }

        private void OnDisable()
        {
            if (_session == null) return;
            var e = _session.Events;
            e.StateReady -= OnReady;
            e.Placed -= OnPlaced;
            e.UsedOnWorld -= OnUsed;
            e.World -= OnWorld;
            e.TimeJumped -= OnJumped;
            _g = null;
        }

        private void Start() => _session.OnTap(TapKind.Placed, OnTapPlaced);

        private void OnReady(GameState g)
        {
            _g = g;
            _groundMask = LayerMask.GetMask("Ground");
            Sync();
        }

        private void OnPlaced(PlacedObject o) => Sync();
        private void OnUsed(string id, UseResult r) => Sync();
        private void OnJumped(double hours) => Sync();

        private void OnWorld(WorldEvent e)
        {
            if (e.Kind == WorldEventKind.CampfireBurntOut) Sync();
        }

        private void OnTapPlaced(Tappable t)
        {
            PlacedObject found = null;
            var objects = _g.Camp.Objects;
            for (int i = 0; i < objects.Count; i++) if (objects[i].Id == t.Id) { found = objects[i]; break; }
            if (found == null) return;
            if (!_g.Camp.PickUp(found.Id)) { _session.Say(Topics.CraftNoRoom); return; }
            Sync();
            _session.Events.RaiseHeroActed(new HeroAct(HeroActKind.Pickup, t.transform.position, found.Item));
            _session.BagChanged("pickup_placed");
            ZdLog.Info("Items", $"picked up {found.Id} ({found.Item})");
        }

        // ------------------------------------------------------------------ views follow the core

        /// <summary>Makes the views match <c>g.Camp</c>: a view for each placed thing and each fire, none for what is gone.</summary>
        public void Sync()
        {
            if (_g == null) return;
            if (_holder == null) _holder = new GameObject("Camp").transform;
            _alive.Clear();
            var objects = _g.Camp.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                var o = objects[i];
                _alive.Add(o.Id);
                if (_views.TryGetValue(o.Id, out var v) && !v.Fire) continue;
                if (v != null) Remove(v);
                _views[o.Id] = MakeThing(o);
            }
            var fires = _g.Camp.Campfires;
            for (int i = 0; i < fires.Count; i++)
            {
                var f = fires[i];
                _alive.Add(f.Id);
                if (_views.TryGetValue(f.Id, out var v) && v.Fire && v.Campfire == f) continue;
                if (v != null) Remove(v);
                _views[f.Id] = MakeFire(f);
            }
            _stale.Clear();
            foreach (var kv in _views) if (!_alive.Contains(kv.Key)) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++) Remove(_views[_stale[i]]);
        }

        private void Remove(View v)
        {
            _views.Remove(v.Id);
            _fires.Remove(v);
            if (v.Tap != null) _session.Index.UnregisterDynamic(v.Tap);
            if (v.Go != null) Destroy(v.Go);
            ZdLog.Info("Items", $"view gone {v.Id}");
        }

        private View MakeThing(PlacedObject o)
        {
            var go = new GameObject(o.Id);
            go.transform.SetParent(_holder, false);
            go.transform.position = OnGround(o.Position.X, o.Position.Y);
            go.transform.rotation = Quaternion.Euler(0f, Hash(o.Id) * 360f, 0f);

            int at = System.Array.IndexOf(_itemIds, o.Item);
            if (at >= 0 && _itemModels[at] != null)
            {
                var model = Instantiate(_itemModels[at], go.transform, false);
                model.name = "model";
                model.transform.localScale = Vector3.one * _itemScales[at];
                StripColliders(model);
            }
            else
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "model";
                StripColliders(cube);
                cube.transform.SetParent(go.transform, false);
                cube.transform.localScale = new Vector3(0.3f, 0.18f, 0.22f);
                cube.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                if (_thingMaterial != null) cube.GetComponent<Renderer>().sharedMaterial = _thingMaterial;
            }
            var view = new View { Id = o.Id, Go = go };
            view.Tap = go.AddComponent<Tappable>();
            view.Tap.Configure(o.Id, TapKind.Placed, -1f, 0.25f);
            _session.Index.RegisterDynamic(view.Tap);
            ZdLog.Info("Items", $"view thing {o.Id} {o.Item} at {o.Position.X:0.0},{o.Position.Y:0.0}");
            return view;
        }

        private View MakeFire(Campfire f)
        {
            var go = new GameObject(f.Id);
            go.transform.SetParent(_holder, false);
            go.transform.position = OnGround(f.Position.X, f.Position.Y);
            go.transform.rotation = Quaternion.Euler(0f, Hash(f.Id) * 360f, 0f);

            if (_campfireModel != null)
            {
                var model = Instantiate(_campfireModel, go.transform, false);
                model.name = "model";
                StripColliders(model);
            }
            var view = new View { Id = f.Id, Fire = true, Go = go, Campfire = f, Phase = Hash(f.Id) * 20f };

            var fxPrefab = _art != null && _art.Fx != null ? _art.Fx.Get("campfire") : null;
            if (fxPrefab != null)
            {
                view.Fx = Instantiate(fxPrefab, go.transform, false);
                view.Fx.name = "fire";
                view.Fx.transform.localScale = Vector3.one * FlameScale; // D-21: the pack's flame is ~0.45 m — lost in the lit ground; the concept's is ~1 m
            }
            else
            {
                var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flame.name = "fire";
                StripColliders(flame);
                flame.transform.SetParent(go.transform, false);
                flame.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                flame.transform.localScale = new Vector3(0.35f, 0.55f, 0.35f);
                if (_flameMaterial != null) flame.GetComponent<Renderer>().sharedMaterial = _flameMaterial;
                view.Flame = flame.transform;
                view.Fx = flame;
            }

            var lightGo = new GameObject("light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            view.Light = lightGo.AddComponent<Light>();
            view.Light.type = LightType.Point;
            view.Light.color = FireColor;
            view.Light.range = _g.Data.Camp.LightRadius;
            view.Light.shadows = LightShadows.None;
            view.Light.intensity = LightIntensity * f.Light;

            view.Tap = go.AddComponent<Tappable>();
            view.Tap.Configure(f.Id, TapKind.Campfire, -1f, 0.4f);
            _session.Index.RegisterDynamic(view.Tap);
            _fires.Add(view);
            ZdLog.Info("Items", $"view campfire {f.Id} light={view.Light.intensity:0.0} fx={(fxPrefab != null ? "prefab" : "primitive")}");
            return view;
        }

        /// <summary>The light fades with the fire's last seconds and flickers; the flame primitive breathes with it.</summary>
        private void Update()
        {
            float t = Time.time;
            for (int i = 0; i < _fires.Count; i++)
            {
                var v = _fires[i];
                float k = v.Campfire.Light;
                float flicker = 1f + 0.14f * Mathf.Sin(t * 13f + v.Phase) + 0.09f * Mathf.Sin(t * 7.3f + v.Phase * 2f);
                v.Light.intensity = LightIntensity * k * flicker;
                if (v.Flame != null) v.Flame.localScale = new Vector3(0.35f, 0.55f * (0.4f + 0.6f * k) * flicker, 0.35f);
                if (v.Fx != null && v.Fx.activeSelf != (k > 0f)) v.Fx.SetActive(k > 0f);
            }
        }

        // ------------------------------------------------------------------ small helpers

        private Vector3 OnGround(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 60f, z), Vector3.down, out var hit, 120f, _groundMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(x, 0f, z);
        }

        /// <summary>No colliders on what lies in the world: taps are by screen radius (WorldPicker), and a log must not stop a thrown torch or the hero.</summary>
        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }

        /// <summary>0..1 from an id — the same id always lies the same way.</summary>
        private static float Hash(string id)
        {
            uint h = 2166136261u;
            for (int i = 0; i < id.Length; i++) h = (h ^ id[i]) * 16777619u;
            return (h & 0xFFFFu) / 65536f;
        }
    }
}
