using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Dialogue;
using ZeldaDaughter.Core.Input;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Hero;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Game
{
    /// <summary>
    /// T-10: one playthrough in a scene. Holds the core's GameState and wires it to the scene — clock → sun, state → the
    /// hero's lines, taps → pick-ups and conversations, onboarding hints, autosave (project-design.md §6 «Система
    /// сохранения»). Rules live in the core; this only feeds it time, input and dice, and shows the result.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private HeroController _hero;
        [SerializeField] private SunController _sun;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private string _zone;
        [SerializeField] private SceneTags[] _objects = new SceneTags[0];
        [SerializeField] private int _seed = 20261008;

        private GameState _state;
        private Remarks _remarks;
        private System.Random _rng;
        private readonly Dictionary<string, SceneTags> _byId = new Dictionary<string, SceneTags>();
        private float _autosaveLeft;
        private float _remarkCheckLeft;
        private Conversation _talk;
        private string _talkNpc;
        private Transform _talkNpcTransform;

        public GameState State => _state;
        public SessionUI UI => _ui;
        public static string SlotPath => Path.Combine(Application.persistentDataPath, "slot.json");

        public void Configure(HeroController hero, SunController sun, SessionUI ui, string zone, SceneTags[] objects)
        {
            _hero = hero;
            _sun = sun;
            _ui = ui;
            _zone = zone;
            _objects = objects;
        }

        private void Awake()
        {
            var data = GameData.Current;
            _state = new GameState(data) { Zone = _zone };
            _remarks = new Remarks(data.Remarks);
            _rng = new System.Random(_seed);
            foreach (var o in _objects) if (o != null) _byId[o.Id] = o;
            _autosaveLeft = data.Session.AutosaveSeconds;
            Load();
        }

        private void OnEnable() { if (_hero != null) _hero.Gesture += OnGesture; }
        private void OnDisable() { if (_hero != null) _hero.Gesture -= OnGesture; }

        private void Update()
        {
            float dt = Time.deltaTime;
            var data = _state.Data;
            _state.Clock.Advance(dt);
            _state.Condition.Tick(dt, RestKind.None);
            _state.Hunger.Advance(dt);
            if (_sun != null) _sun.Apply(_state.Clock.Daylight);

            var p = _hero.transform.position;
            _state.HeroPosition = new Vec2(p.x, p.z);
            _state.HeroHeight = p.y;
            _state.HeroFacingDegrees = _hero.transform.eulerAngles.y;

            _state.Hints.Set("tappable_nearby", _objects.Any(o => o != null && o.gameObject.activeSelf && (o.Item != null || o.Has("npc"))
                                                               && Vector3.Distance(o.transform.position, p) < 8f));
            _state.Hints.Set("has_item", _state.Bag.UsedSlots > 0);
            var hint = _state.Hints.Visible;
            _ui.ShowHint(hint != null ? _state.Hints.TextOf(hint) : null);

            _remarkCheckLeft -= dt;
            if (_remarkCheckLeft <= 0)
            {
                _remarkCheckLeft = data.Session.RemarkCheckSeconds;
                bool overloaded = _state.Bag.IsOverloaded(_state.Skills.CapacityMultiplier());
                var topics = _remarks.ConditionTopics(_state.Condition, _state.Hunger, overloaded, _state.Clock.Daylight < 0.1f);
                if (topics.Count > 0) Say(topics[0]);
            }

            _autosaveLeft -= dt;
            if (_autosaveLeft <= 0)
            {
                _autosaveLeft = data.Session.AutosaveSeconds;
                Save("timer");
            }
        }

        private void OnApplicationPause(bool paused) { if (paused) Save("pause"); }
        private void OnApplicationQuit() => Save("quit");

        private void OnGesture(GestureEvent e)
        {
            switch (e.Kind)
            {
                case GestureKind.SwipeStarted: _state.Hints.Did("swipe"); break;
                case GestureKind.LongPressOnHero: _state.Hints.Did("long_press_hero"); break;
                case GestureKind.Tap: Tap(e.TargetId); break;
            }
        }

        /// <summary>The player tapped a scene object (from a touch, or a test).</summary>
        public void Tap(string objectId)
        {
            _state.Hints.Did("tap");
            if (objectId == null || !_byId.TryGetValue(objectId, out var obj) || !obj.gameObject.activeSelf) return;
            if (obj.Item != null) { PickUp(obj); return; }
            if (obj.Has("npc")) Talk(obj);
        }

        private void PickUp(SceneTags obj)
        {
            if (!_state.Bag.Add(obj.Item)) { Say(Topics.CraftNoRoom); return; }
            _state.Picked.Add(obj.Id);
            obj.gameObject.SetActive(false);
            _ui.HeroSay(_hero.transform, _state.Data.Items[obj.Item].Pickup, _state.Data.Session.RemarkBubbleSeconds);
            ZdLog.Info("Pickup", $"{obj.Id} → {obj.Item}");
        }

        private void Talk(SceneTags npc)
        {
            string key = npc.Id.StartsWith("npc_") ? npc.Id.Substring(4) : npc.Id;
            if (!_state.Data.Dialogues.Npcs.ContainsKey(key)) return;
            _talk = new Conversation(_state.Data.Dialogues, _state.Language, key);
            _talkNpc = key;
            _talkNpcTransform = npc.transform;
            ShowTalk();
        }

        /// <summary>The hero answered with an icon (reply button, or a test).</summary>
        public void Reply(string icon)
        {
            if (_talk == null) return;
            var r = _talk.Reply(icon, _rng.NextDouble());
            if (!r.Accepted) return;
            if (r.Misunderstood)
            {
                ZdLog.Info("Talk", $"{_talkNpc} did not understand [{icon}]");
                _ui.NpcSay(_talkNpcTransform, "?", _state.Data.Session.NpcBubbleSeconds);
                return;
            }
            ShowTalk();
        }

        private void ShowTalk()
        {
            var v = _talk.Current;
            string icons = string.Join(" ", v.Icons.Select(i => "[" + i + "]"));
            _ui.NpcSay(_talkNpcTransform, v.Text + "\n" + icons, _state.Data.Session.NpcBubbleSeconds);
            ZdLog.Info("Talk", $"{_talkNpc} → {_talk.NodeId}");
            if (_talk.Ended)
            {
                _ui.HideReplies();
                if (v.Gesture != null && _byId.TryGetValue(v.Gesture.Target, out var target))
                {
                    var look = target.transform.position - _talkNpcTransform.position;
                    look.y = 0;
                    if (look.sqrMagnitude > 0.01f) _talkNpcTransform.rotation = Quaternion.LookRotation(look);
                    ZdLog.Info("Talk", $"{_talkNpc} {v.Gesture.Type} {v.Gesture.Target}");
                }
                _talk = null;
                return;
            }
            _ui.ShowReplies(v.Replies, Reply);
        }

        private void Say(string topic)
        {
            var line = _remarks.Say(topic, Time.time, n => _rng.Next(n));
            if (line == null) return;
            _ui.HeroSay(_hero.transform, line, _state.Data.Session.RemarkBubbleSeconds);
            ZdLog.Info("Remark", $"{topic}: {line}");
        }

        /// <summary>One save slot, written atomically (core SaveGame).</summary>
        public void Save(string reason)
        {
            SaveGame.WriteAtomic(SlotPath, SaveGame.Capture(_state));
            ZdLog.Info("Save", $"saved {reason}");
        }

        private void Load()
        {
            var text = SaveGame.ReadSlot(SlotPath);
            if (text == null) return;
            SaveGame.Restore(_state, text);
            foreach (var id in _state.Picked) if (_byId.TryGetValue(id, out var o)) o.gameObject.SetActive(false);
            if (_state.Zone == _zone)
                _hero.Teleport(new Vector3(_state.HeroPosition.X, _state.HeroHeight, _state.HeroPosition.Y), _state.HeroFacingDegrees);
            _state.Zone = _zone;
            ZdLog.Info("Save", $"loaded day={_state.Clock.Day} t={_state.Clock.TimeOfDay:0.000}");
        }
    }
}
