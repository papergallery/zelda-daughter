using UnityEngine;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// A tap on a thing lying on the ground (T-10, moved out of the session): it goes into the bag, is remembered as picked (<c>g.Picked</c>, so a
    /// load does not bring it back), disappears from the scene, and the hero says her line. On start it hides what the loaded save has picked.
    /// </summary>
    public sealed class PickupPresenter : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        private GameState _g;

        public void Configure(GameSession session) => _session = session;

        private void OnEnable() => _session.Events.StateReady += OnReady;

        private void OnDisable()
        {
            if (_session != null) _session.Events.StateReady -= OnReady;
        }

        private void Start() => _session.OnTap(TapKind.Pickup, OnTap);

        private void OnReady(GameState g)
        {
            _g = g;
            foreach (var id in g.Picked)
            {
                var o = _session.Index.Find(id);
                if (o != null) o.gameObject.SetActive(false);
            }
        }

        private void OnTap(Tappable t)
        {
            var obj = _session.Index.Find(t.Id);
            if (obj == null || obj.Item == null) return;
            if (!_g.Bag.Add(obj.Item)) { _session.Say(Topics.CraftNoRoom); return; }
            _g.Picked.Add(obj.Id);
            t.Enabled = false;
            obj.gameObject.SetActive(false);
            var events = _session.Events;
            events.RaiseHeroActed(new HeroAct(HeroActKind.Pickup, obj.transform.position, obj.Item));
            events.RaisePickedUp(obj.Id, obj.Item);
            _session.BagChanged("pickup");
            events.RaiseHeroSaid("pickup", _g.Data.Items[obj.Item].Pickup);
            ZdLog.Info("Pickup", $"{obj.Id} → {obj.Item}");
        }
    }
}
